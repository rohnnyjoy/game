using Godot;

#nullable enable
using System;
using System.Collections.Generic;
using Shared.Runtime;

/// <summary>
/// Centralized bullet simulation and rendering using MultiMesh.
/// - Simulates bullets as lightweight structs
/// - Renders per archetype (mesh/material) via one MultiMeshInstance3D each
/// - Provides API to register archetypes and spawn bullets
///
/// Initial feature scope:
/// - Linear motion + gravity per archetype
/// - Ray collision against world + enemies; calls Enemy.TakeDamage
/// - Destroy on impact per archetype
/// - Lifetime expiry
///
/// Notes:
/// - Future: add bounce, homing, tracking, effects. Keep API stable.
/// - MultiMesh shows first N active bullets; inactive bullets do not render.
/// </summary>
public partial class BulletManager : Node3D
{
  public static BulletManager? Instance { get; private set; }

  private static readonly Vector3 DefaultBeamVerticalOffset = Vector3.Up * 0.9f;

  private class Archetype
  {
    public int Id;
    public Mesh Mesh = null!;
    public Material? Material;
    public MultiMesh MultiMesh = null!;
    public MultiMeshInstance3D Instance = null!;
    public uint CollisionMask = uint.MaxValue;
    public float Gravity = 0.0f;
    public bool DestroyOnImpact = true;
    public float Radius = 0.05f;
    public float VisualScale = 1.0f;
    public Transform3D LocalMeshTransform = Transform3D.Identity;
    public bool AlignToVelocity = false;

    // Owning weapon reference
    public BulletWeapon? OwnerWeapon;

    // Bullet storage for this archetype
    public List<BulletData> Bullets = new List<BulletData>(256);

    // Trail config/data
    public bool TrailEnabled = false;
    public float TrailWidth = 0.05f;
    public int TrailMaxPoints = 6;
    public float TrailMinDistance = 0.05f;
    public float TrailLifetime = 0.5f;
    public bool TrailViewAligned = true;
    public MeshInstance3D? TrailInstance;
    public Dictionary<uint, TrailBuffer>? TrailBuffers;

    public BulletBehaviorConfig BehaviorConfig = BulletBehaviorConfig.None;
    public CollisionOp[] CollisionOps = System.Array.Empty<CollisionOp>();

    public DamagePreStep[] DamagePreSteps = System.Array.Empty<DamagePreStep>();
    public DamagePostStep[] DamagePostSteps = System.Array.Empty<DamagePostStep>();
    public MetronomeState[] MetronomeStates = System.Array.Empty<MetronomeState>();
    public SteeringOp[] SteeringOps = System.Array.Empty<SteeringOp>();
  }

  private struct DamagePreStep
  {
    public DamagePreStepKind Kind;
    public float ParamA;
    public float ParamB;
    public float ParamC;
    public bool Flag;
    public int StateIndex;
  }

  private struct DamagePostStep
  {
    public DamagePostStepKind Kind;
    public float ParamA;
    public float ParamB;
    public float ParamC;
  }

  private struct MetronomeState
  {
    public float StackIncrement;
    public float MaxMultiplier;
    public float ResetDelay;
    public bool ResetOnReload;
    public int Streak;
    public ulong LastEnemyId;
    public float LastHitAt;
    public bool HasEnemy;
    public int MaxStacks;
  }

  private enum SteeringOpKind
  {
    Homing,
    Tracking,
  }

  private struct SteeringOp
  {
    public SteeringOpKind Kind;
    public float ParamA;
    public float ParamB;
  }

  private struct CollisionOp
  {
    public CollisionActionType Action;
  }

  private readonly struct SteeringFrameCache
  {
    public static readonly SteeringFrameCache Empty = new SteeringFrameCache(false, Vector3.Zero);

    public bool HasTrackingTarget { get; }
    public Vector3 TrackingTarget { get; }

    public SteeringFrameCache(bool hasTrackingTarget, Vector3 trackingTarget)
    {
      HasTrackingTarget = hasTrackingTarget;
      TrackingTarget = trackingTarget;
    }
  }

  private struct BulletData
  {
    public bool Active;
    public uint Id;
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public float Damage;
    public float LifeRemaining;
    public float InitialSpeed;
    public bool HasEnemyHit;
    public int BounceCount;
    public int PenetrationCount;
    public ulong LastColliderId;
    public float CollisionCooldown;
    public float StuckTimeLeft;
    public ulong StuckTargetId;
    public Vector3 StuckLocalOffset;
    public int PendingActionIndex;
    public ImpactSnapshot? PendingImpactSnapshot;
    public Vector3 StuckLocalNormal;
    public Vector3 StuckWorldNormal;
    public Vector3 StuckPreVelocity;
    public float StickyCooldown;
    public bool LastCrit;
    public float LastCritMultiplier;
  }

  public readonly struct ImpactSnapshot
  {
    public float Damage { get; }
    public float KnockbackScale { get; }
    public bool EnemyHit { get; }
    public ulong EnemyId { get; }
    public Vector3 HitPosition { get; }
    public Vector3 HitNormal { get; }
    public bool IsCrit { get; }
    public float CritMultiplier { get; }

    public ImpactSnapshot(float damage, float knockbackScale, bool enemyHit, ulong enemyId, Vector3 hitPosition, Vector3 hitNormal, bool isCrit = false, float critMultiplier = 1.0f)
    {
      Damage = damage;
      KnockbackScale = knockbackScale;
      EnemyHit = enemyHit;
      EnemyId = enemyId;
      HitPosition = hitPosition;
      HitNormal = hitNormal;
      IsCrit = isCrit;
      CritMultiplier = critMultiplier;
    }
  }

  private enum CollisionActionType
  {
    Pierce,
    Bounce,
    Sticky,
    Explode,
  }

  private class TrailPoint
  {
    public Vector3 Pos;
    public float AgeLeft;
    public TrailPoint(Vector3 pos, float ageLeft)
    {
      Pos = pos;
      AgeLeft = ageLeft;
    }
  }

  private class TrailBuffer
  {
    public List<TrailPoint> Points = new List<TrailPoint>(8);
    public Vector3 LastAddedPos;
  }

  private readonly Dictionary<int, Archetype> _archetypes = new();
  private int _nextArchetypeId = 1;
  private uint _nextBulletId = 1;
  private readonly Dictionary<BulletWeapon, Archetype> _weaponArchetypes = new();

  private static readonly uint DefaultArchetypeCollisionMask = PhysicsLayers.Mask(
    PhysicsLayers.Layer.World,
    PhysicsLayers.Layer.Enemy,
    PhysicsLayers.Layer.SafeZone,
    PhysicsLayers.Layer.DamageBarrier
  );

  [Export]
  public bool DebugLogCollisions { get; set; } = true;
  [Export]
  public float DefaultKnockback { get; set; } = 3.5f;

  public override void _EnterTree()
  {
    Instance = this;
  }

  public override void _ExitTree()
  {
    if (Instance == this)
      Instance = null;
  }

  public override void _Ready()
  {
    // Auto-register archetypes for any weapons already in the scene
    RegisterExistingWeapons();
    // And register for weapons added later
    GetTree().Connect(SceneTree.SignalName.NodeAdded, new Callable(this, nameof(OnNodeAdded)));
  }

  private void RegisterExistingWeapons()
  {
    var weapons = GetTree().GetNodesInGroup("weapons");
    foreach (var node in weapons)
    {
      if (node is BulletWeapon bw)
      {
        EnsureArchetypeForWeapon(bw);
      }
    }
  }

  private void OnNodeAdded(Node node)
  {
    if (node is BulletWeapon bw)
    {
      EnsureArchetypeForWeapon(bw);
    }
  }

  public void EnsureArchetypeForWeapon(BulletWeapon bw)
  {
    if (bw == null || !IsInstanceValid(bw)) return;
    if (!bw.UseBulletManager) return;

    BounceConfig? bounceConfig = null;
    PierceConfig? pierceConfig = null;
    HomingConfig? homingConfig = null;
    TrackingConfig? trackingConfig = null;
    AimbotConfig? aimbotConfig = null;
    ExplosiveConfig? explosiveConfig = null;
    StickyConfig? stickyConfig = null;

    var preStepConfigs = new List<(DamagePreStepConfig Config, int Order)>();
    var postStepConfigs = new List<(DamagePostStepConfig Config, int Order)>();
    int preOrder = 0;
    int postOrder = 0;

    void InspectModule(WeaponModule module)
    {
      if (module == null)
        return;
      if (module is IBounceProvider bounceProvider && bounceProvider.TryGetBounceConfig(out var bounceCfg) && bounceCfg.MaxBounces > 0)
      {
        bounceConfig = new BounceConfig(bounceCfg.DamageReduction, bounceCfg.Bounciness, bounceCfg.MaxBounces);
      }
      if (module is IPierceProvider pierceProvider && pierceProvider.TryGetPierceConfig(out var pierceCfg) && pierceCfg.MaxPenetrations > 0)
      {
        pierceConfig = new PierceConfig(pierceCfg.DamageReduction, pierceCfg.VelocityFactor, pierceCfg.MaxPenetrations, pierceCfg.Cooldown);
      }
      if (module is IHomingProvider homingProvider && homingProvider.TryGetHomingConfig(out var homingCfg) && homingCfg.Strength > 0.0f && homingCfg.Radius > 0.0f)
      {
        homingConfig = new HomingConfig(homingCfg.Radius, homingCfg.Strength);
      }
      if (module is ITrackingProvider trackingProvider && trackingProvider.TryGetTrackingConfig(out var trackingCfg) && trackingCfg.Strength > 0.0f)
      {
        trackingConfig = new TrackingConfig(trackingCfg.Strength, trackingCfg.MaxRayDistance);
      }
      if (module is IAimbotProvider aimbotProvider && aimbotProvider.TryGetAimbotConfig(out var aimbotCfg))
      {
        aimbotConfig = new AimbotConfig(aimbotCfg.ConeAngle, aimbotCfg.VerticalOffset, aimbotCfg.Radius, aimbotCfg.LineWidth, aimbotCfg.LineDuration);
      }
      if (module is IExplosiveProvider explosiveProvider && explosiveProvider.TryGetExplosiveConfig(out var explosiveCfg))
      {
        explosiveConfig = new ExplosiveConfig(explosiveCfg.Radius, explosiveCfg.DamageMultiplier);
      }
      if (module is IStickyProvider stickyProvider && stickyProvider.TryGetStickyConfig(out var stickyCfg))
      {
        stickyConfig = new StickyConfig(stickyCfg.Duration, stickyCfg.CollisionDamage);
      }
      if (module is IDamagePreStepProvider preProvider)
      {
        foreach (var cfg in preProvider.GetDamagePreSteps())
        {
          preStepConfigs.Add((cfg, preOrder++));
        }
      }
      if (module is IDamagePostStepProvider postProvider)
      {
        foreach (var cfg in postProvider.GetDamagePostSteps())
        {
          postStepConfigs.Add((cfg, postOrder++));
        }
      }
    }

    if (bw.ImmutableModules != null)
      foreach (WeaponModule module in bw.ImmutableModules)
        InspectModule(module);
    if (bw.Modules != null)
      foreach (WeaponModule module in bw.Modules)
        InspectModule(module);

    if (bw.BulletArchetypeId >= 0 && _archetypes.TryGetValue(bw.BulletArchetypeId, out var existingArch))
    {
      existingArch.BehaviorConfig = BulletBehaviorConfig.Create(bounceConfig, pierceConfig, homingConfig, trackingConfig, aimbotConfig, explosiveConfig, stickyConfig);
      existingArch.CollisionOps = BuildCollisionOps(bw);
      existingArch.SteeringOps = BuildSteeringOps(existingArch.BehaviorConfig);
      existingArch.OwnerWeapon = bw;
      existingArch.CollisionMask = DefaultArchetypeCollisionMask;
      BuildDamagePipelines(existingArch, preStepConfigs, postStepConfigs);
      _weaponArchetypes[bw] = existingArch;
      return;
    }

    Mesh? mesh = null;
    Material? material = null;
    Transform3D local = Transform3D.Identity;
    if (bw.BulletScene != null)
    {
      TryExtractMeshInfoFromScene(bw.BulletScene, out mesh, out material, out local);
    }
    if (mesh == null)
    {
      var sphere = new SphereMesh { RadialSegments = 8, Rings = 4, Radius = 0.05f };
      mesh = sphere;
    }

    int id = RegisterArchetype(
      mesh,
      material,
      radius: bw.ManagerBulletRadius,
      collisionMask: DefaultArchetypeCollisionMask,
      gravity: 0.0f,
      destroyOnImpact: true,
      visualScale: bw.ManagerVisualScale,
      localMeshTransform: local,
      alignToVelocity: bw.ManagerAlignToVelocity,
      trailEnabled: bw.ManagerTrailEnabled,
      trailWidth: bw.ManagerTrailWidth,
      trailMaxPoints: bw.ManagerTrailMaxPoints,
      trailMinDistance: bw.ManagerTrailMinDistance,
      trailLifetime: bw.ManagerTrailLifetime,
      trailViewAligned: bw.ManagerTrailViewAligned,
      trailMaterial: null,
      bounceEnabled: bounceConfig != null,
      bounceDamageReduction: bounceConfig?.DamageReduction ?? 0f,
      bounceBounciness: bounceConfig?.Bounciness ?? 0f,
      bounceMaxBounces: bounceConfig?.MaxBounces ?? 0,
      pierceEnabled: pierceConfig != null,
      pierceDamageReduction: pierceConfig?.DamageReduction ?? 0f,
      pierceVelocityFactor: pierceConfig?.VelocityFactor ?? 0f,
      pierceMaxPenetrations: pierceConfig?.MaxPenetrations ?? 0,
      pierceCooldown: pierceConfig?.Cooldown ?? 0f
    );
    bw.BulletArchetypeId = id;
    if (_archetypes.TryGetValue(id, out var newArch))
    {
      newArch.BehaviorConfig = BulletBehaviorConfig.Create(bounceConfig, pierceConfig, homingConfig, trackingConfig, aimbotConfig, explosiveConfig, stickyConfig);
      newArch.CollisionOps = BuildCollisionOps(bw);
      newArch.SteeringOps = BuildSteeringOps(newArch.BehaviorConfig);
      newArch.OwnerWeapon = bw;
      BuildDamagePipelines(newArch, preStepConfigs, postStepConfigs);
      _weaponArchetypes[bw] = newArch;
    }
  }

  private static CollisionOp[] BuildCollisionOps(BulletWeapon bw)
  {
    if (bw == null)
      return System.Array.Empty<CollisionOp>();

    var order = new List<CollisionActionType>();
    var seen = new HashSet<CollisionActionType>();

    void Add(CollisionActionType t)
    {
      if (seen.Add(t))
        order.Add(t);
    }

    void Inspect(WeaponModule m)
    {
      if (m == null)
        return;

      if (m is IPierceProvider pierce && pierce.TryGetPierceConfig(out var pierceCfg) && pierceCfg.MaxPenetrations > 0)
        Add(CollisionActionType.Pierce);
      if (m is IBounceProvider bounce && bounce.TryGetBounceConfig(out var bounceCfg) && bounceCfg.MaxBounces > 0)
        Add(CollisionActionType.Bounce);
      if (m is IStickyProvider sticky && sticky.TryGetStickyConfig(out _))
        Add(CollisionActionType.Sticky);
      if (m is IExplosiveProvider explode && explode.TryGetExplosiveConfig(out var explosiveCfg) && explosiveCfg.Radius > 0.0f && explosiveCfg.DamageMultiplier > 0.0f)
        Add(CollisionActionType.Explode);
    }

    foreach (WeaponModule module in EnumerateModulesInOrder(bw))
      Inspect(module);

    if (order.Count == 0)
      return System.Array.Empty<CollisionOp>();

    var ops = new CollisionOp[order.Count];
    for (int i = 0; i < order.Count; i++)
      ops[i] = new CollisionOp { Action = order[i] };
    return ops;
  }

  private static IEnumerable<WeaponModule> EnumerateModulesInOrder(BulletWeapon bw)
  {
    if (bw == null)
      yield break;

    if (bw.Modules != null)
    {
      foreach (WeaponModule module in bw.Modules)
      {
        if (module != null)
          yield return module;
      }
    }

    if (bw.ImmutableModules != null)
    {
      foreach (WeaponModule module in bw.ImmutableModules)
      {
        if (module != null)
          yield return module;
      }
    }
  }

  private static SteeringOp[] BuildSteeringOps(BulletBehaviorConfig behavior)
  {
    if (behavior == null || ReferenceEquals(behavior, BulletBehaviorConfig.None))
      return System.Array.Empty<SteeringOp>();

    List<SteeringOp> ops = new List<SteeringOp>(2);

    if (behavior.Homing is HomingConfig homing)
    {
      ops.Add(new SteeringOp
      {
        Kind = SteeringOpKind.Homing,
        ParamA = homing.Radius,
        ParamB = homing.Strength,
      });
    }

    if (behavior.Tracking is TrackingConfig tracking)
    {
      ops.Add(new SteeringOp
      {
        Kind = SteeringOpKind.Tracking,
        ParamA = tracking.Strength,
        ParamB = tracking.MaxRayDistance,
      });
    }

    return ops.Count == 0 ? System.Array.Empty<SteeringOp>() : ops.ToArray();
  }

  private SteeringFrameCache PrepareSteeringFrameCache(Archetype arch, PhysicsDirectSpaceState3D? space)
  {
    var ops = arch.SteeringOps;
    if (ops.Length == 0)
      return SteeringFrameCache.Empty;

    for (int i = 0; i < ops.Length; i++)
    {
      var op = ops[i];
      if (op.Kind == SteeringOpKind.Tracking && TryComputeTrackingTarget(in op, space, out var target))
        return new SteeringFrameCache(true, target);
    }

    return SteeringFrameCache.Empty;
  }

  private bool TryComputeTrackingTarget(in SteeringOp trackingOp, PhysicsDirectSpaceState3D? space, out Vector3 target)
  {
    target = Vector3.Zero;

    var viewport = GetViewport();
    if (viewport == null)
      return false;

    var camera = viewport.GetCamera3D();
    if (camera == null)
      return false;

    Vector2 mouse = viewport.GetMousePosition();
    Vector3 rayOrigin = camera.ProjectRayOrigin(mouse);
    Vector3 rayDir = camera.ProjectRayNormal(mouse);
    if (rayDir.LengthSquared() < 0.000001f)
      return false;

    rayDir = rayDir.Normalized();
    float rayLength = Math.Max(0.01f, trackingOp.ParamB);
    Vector3 rayEnd = rayOrigin + rayDir * rayLength;
    target = rayEnd;

    if (space != null)
    {
      var query = new PhysicsRayQueryParameters3D
      {
        From = rayOrigin,
        To = rayEnd,
      };
      var hit = space.IntersectRay(query);
      if (hit.Count > 0 && hit.ContainsKey("position"))
        target = (Vector3)hit["position"];
    }

    return true;
  }

  private static void BuildDamagePipelines(Archetype arch, List<(DamagePreStepConfig Config, int Order)> preConfigs, List<(DamagePostStepConfig Config, int Order)> postConfigs)
  {
    if (preConfigs.Count > 0)
    {
      preConfigs.Sort(static (a, b) =>
      {
        int cmp = b.Config.Priority.CompareTo(a.Config.Priority);
        return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
      });

      var preSteps = new DamagePreStep[preConfigs.Count];
      var metronomeStates = new List<MetronomeState>();
      for (int i = 0; i < preConfigs.Count; i++)
      {
        var cfg = preConfigs[i].Config;
        var step = new DamagePreStep
        {
          Kind = cfg.Kind,
          ParamA = cfg.ParamA,
          ParamB = cfg.ParamB,
          ParamC = cfg.ParamC,
          Flag = cfg.Flag,
          StateIndex = -1,
        };

        if (cfg.Kind == DamagePreStepKind.Metronome)
        {
          var state = new MetronomeState
          {
            StackIncrement = cfg.ParamA,
            MaxMultiplier = cfg.ParamB,
            ResetDelay = cfg.ParamC,
            ResetOnReload = cfg.Flag,
            Streak = 0,
            LastEnemyId = 0,
            LastHitAt = 0,
            HasEnemy = false,
            MaxStacks = ComputeMaxStacks(cfg.ParamA, cfg.ParamB),
          };
          step.StateIndex = metronomeStates.Count;
          metronomeStates.Add(state);
        }

        preSteps[i] = step;
      }

      arch.DamagePreSteps = preSteps;
      arch.MetronomeStates = metronomeStates.Count > 0 ? metronomeStates.ToArray() : System.Array.Empty<MetronomeState>();
    }
    else
    {
      arch.DamagePreSteps = System.Array.Empty<DamagePreStep>();
      arch.MetronomeStates = System.Array.Empty<MetronomeState>();
    }

    if (postConfigs.Count > 0)
    {
      postConfigs.Sort(static (a, b) =>
      {
        int cmp = b.Config.Priority.CompareTo(a.Config.Priority);
        return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
      });

      var postSteps = new DamagePostStep[postConfigs.Count];
      for (int i = 0; i < postConfigs.Count; i++)
      {
        var cfg = postConfigs[i].Config;
        postSteps[i] = new DamagePostStep
        {
          Kind = cfg.Kind,
          ParamA = cfg.ParamA,
          ParamB = cfg.ParamB,
          ParamC = cfg.ParamC,
        };
      }

      arch.DamagePostSteps = postSteps;
    }
    else
    {
      arch.DamagePostSteps = System.Array.Empty<DamagePostStep>();
    }

    ResetDamageStates(arch, forReload: false);
  }

  private static int ComputeMaxStacks(float stackIncrement, float maxMultiplier)
  {
    if (stackIncrement <= 0.0f)
      return 0;
    if (maxMultiplier <= 1.0f)
      return 0;
    return (int)MathF.Ceiling(MathF.Max(0.0f, (maxMultiplier - 1.0f) / stackIncrement));
  }

  private static float GetTimeSeconds() => (float)Time.GetTicksMsec() / 1000f;

  private static void ApplyDamagePreSteps(Archetype arch, ref BulletData bullet, bool enemyHit, ulong enemyId, ref float damage, ref float knockbackScale)
  {
    var steps = arch.DamagePreSteps;
    if (steps.Length == 0)
      return;

    float currentSpeed = bullet.Velocity.Length();
    // Reset per-impact transient state
    bullet.LastCrit = false;
    bullet.LastCritMultiplier = 1.0f;
    for (int i = 0; i < steps.Length; i++)
    {
      var step = steps[i];
      switch (step.Kind)
      {
        case DamagePreStepKind.SpeedScale:
        {
          if (currentSpeed <= 0.0001f)
            break;

          float extraDamage = currentSpeed * MathF.Max(0.0f, step.ParamA);
          float extraKnockback = currentSpeed * MathF.Max(0.0f, step.ParamB);

          damage += extraDamage;
          knockbackScale += extraKnockback;
          break;
        }
        case DamagePreStepKind.CritChance:
        {
          float chance = MathF.Max(0.0f, step.ParamA);
          float mult = MathF.Max(1.0f, step.ParamB);
          // Roll crit; apply once per impact
          if (!bullet.LastCrit)
          {
            float roll = (float)GD.RandRange(0.0, 1.0);
            if (roll < chance)
            {
              damage *= mult;
              bullet.LastCrit = true;
              bullet.LastCritMultiplier = mult;
            }
          }
          break;
        }
        case DamagePreStepKind.Metronome:
        {
          if (step.StateIndex < 0 || arch.MetronomeStates.Length == 0 || step.StateIndex >= arch.MetronomeStates.Length)
            break;

          var state = arch.MetronomeStates[step.StateIndex];
          float now = GetTimeSeconds();
          if (state.ResetDelay > 0.0f && (now - state.LastHitAt) > state.ResetDelay)
          {
            state.Streak = 0;
            state.HasEnemy = false;
            state.LastEnemyId = 0;
          }

          if (!enemyHit || enemyId == 0)
          {
            state.Streak = 0;
            state.HasEnemy = false;
            state.LastEnemyId = 0;
            state.LastHitAt = now;
            arch.MetronomeStates[step.StateIndex] = state;
            Instance?.UpdateMetronomeBadge(arch);
            break;
          }

          if (!state.HasEnemy || state.LastEnemyId != enemyId)
          {
            state.Streak = 0;
            state.LastEnemyId = enemyId;
            state.HasEnemy = true;
          }

          float multiplier = 1.0f + state.StackIncrement * Math.Max(0, state.Streak);
          if (state.MaxMultiplier > 0.0f)
            multiplier = MathF.Min(multiplier, state.MaxMultiplier);
          damage *= MathF.Max(0.0f, multiplier);

          if (state.StackIncrement > 0.0f)
          {
            int next = state.Streak + 1;
            if (state.MaxStacks > 0)
              next = Math.Min(next, state.MaxStacks);
            state.Streak = Math.Max(0, next);
          }
          else
          {
            state.Streak = 0;
          }

          state.LastHitAt = now;
          arch.MetronomeStates[step.StateIndex] = state;
          // Update HUD badge with current multiplier snapshot
          Instance?.UpdateMetronomeBadge(arch);
          break;
        }
      }
    }
  }

  private void UpdateMetronomeBadge(Archetype arch)
  {
    try
    {
      // Compute the highest active metronome multiplier across states
      float mult = 1.0f;
      var states = arch.MetronomeStates;
      if (states != null && states.Length > 0)
      {
        for (int i = 0; i < states.Length; i++)
        {
          var s = states[i];
          float m = 1.0f + s.StackIncrement * Math.Max(0, s.Streak);
          if (s.MaxMultiplier > 0.0f)
            m = MathF.Min(m, s.MaxMultiplier);
          if (m > mult)
            mult = m;
        }
      }

      Weapon? owner = arch.OwnerWeapon;
      if (owner == null)
        return;

      // Find the first MetronomeModule on the weapon
      WeaponModule? found = null;
      if (owner.Modules != null)
      {
        foreach (var m in owner.Modules)
        {
          if (m is MetronomeModule) { found = m; break; }
        }
      }
      if (found == null && owner.ImmutableModules != null)
      {
        foreach (var m in owner.ImmutableModules)
        {
          if (m is MetronomeModule) { found = m; break; }
        }
      }
      if (found == null)
        return;

      // Resolve module id and publish badge
      if (found is MetronomeModule met)
      {
        met.PublishMultiplier(mult);
      }
    }
    catch { }
  }

  private void ApplyDamagePostSteps(Archetype arch, Node3D enemy, float appliedDamage, float leftover, ref BulletData bullet, ImpactSnapshot snapshot)
  {
    var steps = arch.DamagePostSteps;
    if (steps.Length == 0)
      return;

    for (int i = 0; i < steps.Length; i++)
    {
      var step = steps[i];
      switch (step.Kind)
      {
        case DamagePostStepKind.OverkillTransfer:
        {
          GD.Print($"[CursedSkull] leftover={leftover:0.##} enemy={(enemy != null ? enemy.Name : "null")} radius={step.ParamA:0.##}");
          if (leftover <= 0.0f || enemy == null)
            break;

          Node3D? neighbor = FindNearestEnemy(enemy.GlobalTransform.Origin, step.ParamA, enemy);
          GD.Print($"[CursedSkull] neighbor={(neighbor != null ? neighbor.Name : "null")}");
          if (neighbor == null || neighbor == enemy || !IsInstanceValid(neighbor))
          {
            GD.Print("[CursedSkull] transfer aborted (no valid neighbor)");
            break;
          }

          try
          {
            float beamStrength = Mathf.Clamp(0.35f + leftover * 0.05f, 0.35f, 2.5f);
            float beamWidth = Mathf.Clamp(0.45f + leftover * 0.01f, 0.45f, 1.35f);
            Vector3 originOffset = CalculateBeamVerticalOffset(enemy);
            Vector3 targetOffset = CalculateBeamVerticalOffset(neighbor);
            BeamVfxManager.Spawn(enemy, originOffset, neighbor, targetOffset, beamStrength, widthOverride: beamWidth);
          }
          catch (Exception fxEx)
          {
            GD.PrintErr($"CursedSkull beam spawn failed: {fxEx.Message}");
          }

          try
          {
            bool applied = false;
            if (neighbor is Enemy en)
            {
              en.TakeDamage(leftover);
              applied = true;
            }
            else if (neighbor.HasMethod("take_damage"))
            {
              neighbor.CallDeferred("take_damage", leftover);
              applied = true;
            }

            if (applied)
            {
            FloatingNumber3D.Spawn(this, neighbor, leftover);
            Vector3 dir = bullet.Velocity.LengthSquared() > 0.000001f ? bullet.Velocity.Normalized() : Vector3.Forward;
            dir = new Vector3(dir.X, 0.15f * dir.Y, dir.Z).Normalized();
            var leftoverSnap = new ImpactSnapshot(
              damage: leftover,
              knockbackScale: snapshot.KnockbackScale,
              enemyHit: true,
              enemyId: (ulong)neighbor.GetInstanceId(),
              hitPosition: neighbor.GlobalTransform.Origin,
              hitNormal: -dir
            );
            GlobalEvents.Instance?.EmitDamageDealt(neighbor, leftoverSnap, dir, DefaultKnockback);
              GD.Print("[CursedSkull] transfer applied");
            }
            else
            {
              GD.Print("[CursedSkull] transfer aborted (neighbor lacks take_damage)");
            }
          }
          catch (Exception e)
          {
            GD.PrintErr($"CursedSkull transfer failed: {e.Message}");
          }
          break;
        }
      }
    }
  }

  // Derive a world-space offset so beam endpoints target the visual midpoint of a node.
  private static Vector3 CalculateBeamVerticalOffset(Node3D? node)
  {
    if (node == null || !GodotObject.IsInstanceValid(node))
      return DefaultBeamVerticalOffset;

    if (!TryGetVerticalBounds(node, out float minY, out float maxY))
      return DefaultBeamVerticalOffset;

    float centerY = (minY + maxY) * 0.5f;
    float offsetY = centerY - node.GlobalTransform.Origin.Y;
    return Vector3.Up * offsetY;
  }

  private static bool TryGetVerticalBounds(Node3D node, out float minY, out float maxY)
  {
    minY = float.MaxValue;
    maxY = float.MinValue;
    bool found = false;

    AccumulateVisualExtents(node, ref minY, ref maxY, ref found);
    AccumulateCollisionExtents(node, ref minY, ref maxY, ref found);

    if (!found)
    {
      minY = maxY = node.GlobalTransform.Origin.Y;
      return false;
    }

    return true;
  }

  private static void AccumulateVisualExtents(Node node, ref float minY, ref float maxY, ref bool found)
  {
    if (!GodotObject.IsInstanceValid(node))
      return;

    if (node is VisualInstance3D visual)
    {
      var aabb = visual.GetAabb();
      ProcessAabb(visual.GlobalTransform, aabb, ref minY, ref maxY, ref found);
    }

    foreach (Node child in node.GetChildren())
      AccumulateVisualExtents(child, ref minY, ref maxY, ref found);
  }

  private static void AccumulateCollisionExtents(Node node, ref float minY, ref float maxY, ref bool found)
  {
    if (!GodotObject.IsInstanceValid(node))
      return;

    if (node is CollisionShape3D collisionShape)
    {
      try
      {
        var shape = collisionShape.Shape;
        Mesh? mesh = shape?.GetDebugMesh();
        if (mesh != null)
        {
          var aabb = mesh.GetAabb();
          ProcessAabb(collisionShape.GlobalTransform, aabb, ref minY, ref maxY, ref found);
        }
      }
      catch (Exception)
      {
        // Ignore debug mesh failures; rely on other nodes or fall back to default offset.
      }
    }

    foreach (Node child in node.GetChildren())
      AccumulateCollisionExtents(child, ref minY, ref maxY, ref found);
  }

  private static void ProcessAabb(Transform3D transform, Aabb aabb, ref float minY, ref float maxY, ref bool found)
  {
    Vector3 basePos = aabb.Position;
    Vector3 size = aabb.Size;

    for (int i = 0; i < 8; i++)
    {
      Vector3 corner = new Vector3(
        basePos.X + ((i & 1) != 0 ? size.X : 0f),
        basePos.Y + ((i & 2) != 0 ? size.Y : 0f),
        basePos.Z + ((i & 4) != 0 ? size.Z : 0f)
      );

      Vector3 worldCorner = transform * corner;
      float y = worldCorner.Y;

      if (!found)
      {
        minY = maxY = y;
        found = true;
      }
      else
      {
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }
  }

  private static void ResetDamageStates(Archetype arch, bool forReload)
  {
    var states = arch.MetronomeStates;
    if (states.Length == 0)
      return;

    for (int i = 0; i < states.Length; i++)
    {
      var state = states[i];
      if (forReload && !state.ResetOnReload)
        continue;
      state.Streak = 0;
      state.LastEnemyId = 0;
      state.LastHitAt = 0;
      state.HasEnemy = false;
      states[i] = state;
    }
    // Refresh HUD after reset
    Instance?.UpdateMetronomeBadge(arch);
  }

  public void NotifyWeaponReloaded(BulletWeapon weapon)
  {
    if (weapon == null)
      return;

    if (_weaponArchetypes.TryGetValue(weapon, out var arch))
    {
      ResetDamageStates(arch, forReload: true);
    }
  }

  private void TryExtractMeshInfoFromScene(PackedScene scene, out Mesh? mesh, out Material? material, out Transform3D localTransform)
  {
    mesh = null;
    material = null;
    localTransform = Transform3D.Identity;
    if (scene == null) return;
    Node? inst = null;
    try
    {
      inst = scene.Instantiate();
      Transform3D acc = Transform3D.Identity;
      FindFirstMeshRecursive(inst, acc, ref mesh, ref material, ref localTransform);
    }
    catch (Exception e)
    {
      GD.PrintErr($"BulletManager: Failed to extract mesh: {e.Message}");
    }
    finally
    {
      if (IsInstanceValid(inst))
        inst.Free();
    }
  }

  private void FindFirstMeshRecursive(Node node, Transform3D acc, ref Mesh? mesh, ref Material? material, ref Transform3D local)
  {
    Transform3D nextAcc = acc;
    if (node is Node3D n3)
    {
      nextAcc = acc * n3.Transform;
    }
    if (node is MeshInstance3D mi && mi.Mesh != null)
    {
      mesh = mi.Mesh;
      material = mi.MaterialOverride;
      local = nextAcc;
      return;
    }
    foreach (Node child in node.GetChildren())
    {
      if (!IsInstanceValid(child)) continue;
      if (mesh != null) return;
      FindFirstMeshRecursive(child, nextAcc, ref mesh, ref material, ref local);
      if (mesh != null) return;
    }
  }

  /// <summary>
  /// Registers a new bullet archetype (visual + physics defaults) and returns its id.
  /// </summary>
  public int RegisterArchetype(
    Mesh mesh,
    Material? material = null,
    float radius = 0.05f,
    uint collisionMask = uint.MaxValue,
    float gravity = 0.0f,
    bool destroyOnImpact = true,
    float visualScale = 1.0f,
    Transform3D localMeshTransform = default,
    bool alignToVelocity = false,
    // Trail config
    bool trailEnabled = false,
    float trailWidth = 0.05f,
    int trailMaxPoints = 6,
    float trailMinDistance = 0.05f,
    float trailLifetime = 0.5f,
    bool trailViewAligned = true,
    Material? trailMaterial = null,
    // Bounce
    bool bounceEnabled = false,
    float bounceDamageReduction = 0.2f,
    float bounceBounciness = 0.8f,
    int bounceMaxBounces = 0,
    // Pierce
    bool pierceEnabled = false,
    float pierceDamageReduction = 0.2f,
    float pierceVelocityFactor = 0.9f,
    int pierceMaxPenetrations = 0,
    float pierceCooldown = 0.2f)
  {
    if (mesh == null)
      throw new ArgumentNullException(nameof(mesh));

    int id = _nextArchetypeId++;
    var mm = new MultiMesh
    {
      TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
      Mesh = mesh,
      UseCustomData = false,
    };

    var mmi = new MultiMeshInstance3D
    {
      Multimesh = mm,
      Visible = true,
    };
    if (material != null)
      mmi.MaterialOverride = material;

    AddChild(mmi);

    // Trail setup (optional)
    MeshInstance3D? trailMI = null;
    if (trailEnabled)
    {
      trailMI = new MeshInstance3D
      {
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
      };
      var mat = trailMaterial ?? new StandardMaterial3D
      {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(1, 1, 1, 0.5f),
        VertexColorUseAsAlbedo = false,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      };
      trailMI.MaterialOverride = mat;
      AddChild(trailMI);
    }

    BounceConfig? bounceConfig = (bounceEnabled && bounceMaxBounces > 0)
      ? new BounceConfig(bounceDamageReduction, bounceBounciness, bounceMaxBounces)
      : null;
    PierceConfig? pierceConfig = (pierceEnabled && pierceMaxPenetrations > 0)
      ? new PierceConfig(pierceDamageReduction, pierceVelocityFactor, pierceMaxPenetrations, pierceCooldown)
      : null;

    var behavior = BulletBehaviorConfig.Create(bounceConfig, pierceConfig);

    _archetypes[id] = new Archetype
    {
      Id = id,
      Mesh = mesh,
      Material = material,
      MultiMesh = mm,
      Instance = mmi,
      CollisionMask = collisionMask,
      Gravity = gravity,
      DestroyOnImpact = destroyOnImpact,
      Radius = radius,
      VisualScale = visualScale,
      LocalMeshTransform = localMeshTransform.Equals(default(Transform3D)) ? Transform3D.Identity : localMeshTransform,
      AlignToVelocity = alignToVelocity,
      // Trail fields
      TrailEnabled = trailEnabled,
      TrailWidth = trailWidth,
      TrailMaxPoints = Math.Max(2, trailMaxPoints),
      TrailMinDistance = Math.Max(0.0f, trailMinDistance),
      TrailLifetime = Math.Max(0.05f, trailLifetime),
      TrailViewAligned = trailViewAligned,
      TrailInstance = trailMI,
      TrailBuffers = trailEnabled ? new Dictionary<uint, TrailBuffer>() : null,
      BehaviorConfig = behavior,
      SteeringOps = BuildSteeringOps(behavior),
    };
    // Prewarm rendering resources to prevent first-shot hitch
    PrewarmArchetype(id);
    return id;
  }

  private void PrewarmArchetype(int id)
  {
    if (!_archetypes.TryGetValue(id, out var arch))
      return;
    // Force allocation of MultiMesh buffers and material/shader compilation
    arch.MultiMesh.InstanceCount = 1;
    arch.MultiMesh.SetInstanceTransform(0, Transform3D.Identity);
    arch.Instance.Visible = true;
    // Prewarm trail mesh path if enabled
    if (arch.TrailEnabled && arch.TrailInstance != null && arch.TrailInstance.Mesh == null)
    {
      SurfaceTool st = new SurfaceTool();
      st.Begin(Mesh.PrimitiveType.Triangles);
      st.AddVertex(Vector3.Zero);
      st.AddVertex(new Vector3(0, 0, 0.01f));
      st.AddVertex(new Vector3(0.01f, 0, 0));
      ArrayMesh mesh = st.Commit();
      arch.TrailInstance.Mesh = mesh;
    }
    // Reset to zero so nothing renders until fired
    arch.MultiMesh.InstanceCount = 0;
  }

  /// <summary>
  /// Spawns a bullet instance in the given archetype.
  /// </summary>
  public void SpawnBullet(int archetypeId, Vector3 position, Vector3 velocity, float damage, float lifetime = 5.0f)
  {
    if (!_archetypes.TryGetValue(archetypeId, out var arch))
    {
      GD.PrintErr($"BulletManager: Unknown archetype id {archetypeId}");
      return;
    }

    BulletData data = new BulletData
    {
      Active = true,
      Id = _nextBulletId++,
      Position = position,
      PrevPosition = position,
      Velocity = velocity,
      Damage = damage,
      LifeRemaining = Math.Max(0.01f, lifetime),
      InitialSpeed = velocity.Length(),
      BounceCount = 0,
      PenetrationCount = 0,
      LastColliderId = 0,
      CollisionCooldown = 0,
      StuckTimeLeft = 0,
      PendingActionIndex = -1,
      PendingImpactSnapshot = null,
      StuckTargetId = 0,
      StuckLocalOffset = Vector3.Zero,
      StuckLocalNormal = Vector3.Zero,
      StuckWorldNormal = Vector3.Forward,
      StuckPreVelocity = Vector3.Zero,
      StickyCooldown = 0f,
      LastCrit = false,
      LastCritMultiplier = 1f,
    };
    // Spawn-time aim assist (aimbot)
    if (arch.BehaviorConfig != null && arch.BehaviorConfig.Aimbot is AimbotConfig aimCfg)
    {
      TryApplyAimbot(ref data, aimCfg);
    }
    arch.Bullets.Add(data);

    // Initialize trail buffer
    if (arch.TrailEnabled && arch.TrailBuffers != null)
    {
      var tb = new TrailBuffer();
      tb.Points.Add(new TrailPoint(position, arch.TrailLifetime));
      tb.LastAddedPos = position;
      arch.TrailBuffers[data.Id] = tb;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    float dt = (float)delta;
    var space = GetWorld3D().DirectSpaceState;

    foreach (var kv in _archetypes)
    {
      Archetype arch = kv.Value;
      // Update bullets and build transforms for active ones.
      List<BulletData> list = arch.Bullets;
      int count = list.Count;
      if (count == 0)
      {
        arch.MultiMesh.InstanceCount = 0;
        continue;
      }

      SteeringFrameCache steeringCache = PrepareSteeringFrameCache(arch, space);

      // We’ll compact the list in-place and collect transforms for active bullets.
      int write = 0;
      int activeCount = 0;

      // To avoid per-frame allocations, reuse a local array when possible.
      // For simplicity now, we’ll just write transforms directly via index after compaction.

      for (int i = 0; i < count; i++)
      {
        BulletData b = list[i];
        if (!b.Active)
          continue;

        b.LifeRemaining -= dt;
        if (b.LifeRemaining <= 0)
        {
          if (!b.HasEnemyHit)
          {
            float damage = b.Damage;
            float knockbackScale = 1.0f;
            ApplyDamagePreSteps(arch, ref b, false, 0, ref damage, ref knockbackScale);
          }
          // drop
        }
        else
        {
          if (b.CollisionCooldown > 0.0f)
            b.CollisionCooldown = Math.Max(0.0f, b.CollisionCooldown - dt);

          // If stuck, count down and hold position
          // Decay sticky immunity cooldown if any
          if (b.StickyCooldown > 0.0f)
            b.StickyCooldown = Math.Max(0.0f, b.StickyCooldown - dt);

          if (b.StuckTimeLeft > 0.0f)
          {
            b.StuckTimeLeft = Math.Max(0.0f, b.StuckTimeLeft - dt);
            // While stuck, follow the collider if it still exists
            if (b.StuckTargetId != 0)
            {
              var obj = GodotObject.InstanceFromId(b.StuckTargetId);
              if (obj is Node3D stuckNode && IsInstanceValid(stuckNode))
              {
                Vector3 followPos = stuckNode.ToGlobal(b.StuckLocalOffset);
                b.Position = followPos;
                b.PrevPosition = followPos;
              }
            }
            if (b.StuckTimeLeft <= 0.0f)
            {
              // Sticky finished: restore pre-stick velocity and give a small push out along normal
              Vector3 normalWorld = b.StuckWorldNormal;
              if (b.StuckTargetId != 0)
              {
                var objN = GodotObject.InstanceFromId(b.StuckTargetId);
                if (objN is Node3D stuckNodeN && IsInstanceValid(stuckNodeN))
                  normalWorld = ToGlobalNormal(stuckNodeN, b.StuckLocalNormal);
              }
              float epsilon = MathF.Max(0.01f, arch.Radius);
              b.Position += normalWorld * epsilon;
              b.PrevPosition = b.Position;
              b.Velocity = b.StuckPreVelocity;
              // Short immunity to avoid immediately re-sticking
              b.StickyCooldown = 0.08f;
              // Resume any actions after sticky (e.g., bounce/explode), then continue simulation
              if (b.PendingActionIndex >= 0)
              {
                PerformPendingActionsOnStickyEnd(ref b, arch);
              }
              b.StuckTargetId = 0;
              b.StuckLocalOffset = Vector3.Zero;
              b.StuckWorldNormal = Vector3.Zero;
              b.StuckLocalNormal = Vector3.Zero;
              // Continue simulation normally this frame
            }
            // Still stuck: keep bullet alive without movement
            list[write++] = b;
            activeCount++;
            continue;
          }

          // Integrate motion
          b.PrevPosition = b.Position;
          // Apply steering behaviors before gravity
          ApplyPerTickBehaviors(ref b, arch, in steeringCache);
          // Gravity is positive downward (Vector3.Down)
          if (Math.Abs(arch.Gravity) > 0.0001f)
          {
            b.Velocity += Vector3.Down * arch.Gravity * dt;
          }
          Vector3 nextPos = b.Position + b.Velocity * dt;

          // Raycast from prev to next; approximate a sphere cast using offset rays
          Vector3 hitPos = nextPos;
          Vector3 hitNormal = Vector3.Zero;
          Node3D? collider = null;
          ulong colliderId = 0;
          bool hasHit = false;

          Vector3 seg = nextPos - b.Position;
          float segLen = seg.Length();
          if (segLen > 0.000001f)
          {
            Vector3 dir = seg / segLen;
            Vector3 upRef = MathF.Abs(dir.Y) < 0.99f ? Vector3.Up : Vector3.Right;
            Vector3 u = dir.Cross(upRef).Normalized();
            Vector3 v = dir.Cross(u).Normalized();
            float r = MathF.Max(0.001f, arch.Radius);

            float bestFrac = float.MaxValue;

            void ConsiderHit(Vector3 position, Vector3 normal, Node3D? hitCollider, ulong hitId, float frac)
            {
              if (frac < 0.0f || frac > 1.0f)
                return;
              if (frac >= bestFrac)
                return;
              bestFrac = frac;
              hasHit = true;
              hitPos = position;
              hitNormal = normal;
              collider = hitCollider;
              colliderId = hitId;
            }

            var barrierQuery = new DamageBarrierQuery(
              b.Position,
              nextPos,
              arch.Radius,
              DamageKind.Projectile,
              arch.OwnerWeapon as Node3D,
              null
            );

            if (DamageBarrierRegistry.TryGetFirstBlockingHit(barrierQuery, out DamageBarrierHit barrierHit))
            {
              float frac = segLen > 0.000001f ? barrierHit.Distance / segLen : 0.0f;
              ulong barrierId = (ulong)barrierHit.Barrier.GetInstanceId();
              ConsiderHit(barrierHit.Position, barrierHit.Normal, barrierHit.Barrier, barrierId, frac);
            }

            void TestRay(Vector3 from, Vector3 to)
            {
              var q = new PhysicsRayQueryParameters3D
              {
                From = from,
                To = to,
                CollisionMask = arch.CollisionMask,
                HitFromInside = true,
                HitBackFaces = true,
              };
              var h = space.IntersectRay(q);
              if (h.Count == 0) return;
              Vector3 p = h.ContainsKey("position") ? (Vector3)h["position"] : to;
              Vector3 normal = h.ContainsKey("normal") ? (Vector3)h["normal"] : Vector3.Zero;
              Node3D? hitCollider = h.ContainsKey("collider") ? h["collider"].As<Node3D>() : null;
              ulong hitId = h.ContainsKey("collider_id") ? (ulong)h["collider_id"] : 0;
              float rayLen = (to - from).Length();
              float frac = rayLen > 0.000001f ? (p - from).Length() / rayLen : 0.0f;
              ConsiderHit(p, normal, hitCollider, hitId, frac);
            }

            // Center ray + four offsets roughly covering the bullet radius
            TestRay(b.Position, nextPos);
            TestRay(b.Position + u * r, nextPos + u * r);
            TestRay(b.Position - u * r, nextPos - u * r);
            TestRay(b.Position + v * r, nextPos + v * r);
            TestRay(b.Position - v * r, nextPos - v * r);
          }

          if (hasHit)
          {

            if (colliderId != 0 && colliderId == b.LastColliderId && b.CollisionCooldown > 0.0f)
            {
              b.Position = nextPos;
              b.LastColliderId = colliderId;
            }
            else
            {
              bool isEnemy = collider != null && collider.IsInGroup("enemies");
              ImpactSnapshot impact = new ImpactSnapshot(
                damage: b.Damage,
                knockbackScale: 1.0f,
                enemyHit: isEnemy,
                enemyId: colliderId,
                hitPosition: hitPos,
                hitNormal: hitNormal,
                isCrit: false,
                critMultiplier: 1.0f
              );
              if (isEnemy)
              {
                try
                {
                  if (collider is Enemy enemy)
                  {
                    float damage = b.Damage;
                    float knockbackScale = 1.0f;
                    ApplyDamagePreSteps(arch, ref b, true, colliderId, ref damage, ref knockbackScale);

                    float hpBefore = enemy.CurrentHealth;
                    enemy.TakeDamage(damage);
                    Vector3 dir = b.Velocity.LengthSquared() > 0.000001f ? b.Velocity.Normalized() : Vector3.Forward;
                    dir = new Vector3(dir.X, 0.15f * dir.Y, dir.Z).Normalized();
                    GlobalEvents.Instance?.EmitDamageDealt(enemy, impact, dir, DefaultKnockback);

                    float leftover = damage - hpBefore;
                    ApplyDamagePostSteps(arch, enemy, damage, leftover, ref b, impact);

                    Color? numColor = (b.LastCrit ? Colors.Yellow : (Color?)null);
                    FloatingNumber3D.Spawn(this, enemy, damage, numColor);
                    b.HasEnemyHit = true;
                    impact = new ImpactSnapshot(
                      damage: damage,
                      knockbackScale: knockbackScale,
                      enemyHit: true,
                      enemyId: colliderId,
                      hitPosition: hitPos,
                      hitNormal: hitNormal,
                      isCrit: b.LastCrit,
                      critMultiplier: b.LastCrit ? b.LastCritMultiplier : 1.0f
                    );
                  }
                }
                catch (Exception e)
                {
                  GD.PrintErr($"BulletManager damage call failed: {e.Message}");
                }
              }
              else
              {
                float damage = b.Damage;
                float knockbackScale = 1.0f;
                ApplyDamagePreSteps(arch, ref b, false, 0, ref damage, ref knockbackScale);
                if (collider != null && collider.HasMethod("take_damage"))
                  collider.CallDeferred("take_damage", damage);
                Vector3 dir = b.Velocity.LengthSquared() > 0.000001f ? b.Velocity.Normalized() : Vector3.Forward;
                dir = new Vector3(dir.X, 0.15f * dir.Y, dir.Z).Normalized();
                GlobalEvents.Instance?.EmitDamageDealt(collider, impact, dir, DefaultKnockback);
                impact = new ImpactSnapshot(
                  damage: damage,
                  knockbackScale: knockbackScale,
                  enemyHit: false,
                  enemyId: 0,
                  hitPosition: hitPos,
                  hitNormal: hitNormal,
                  isCrit: b.LastCrit,
                  critMultiplier: b.LastCrit ? b.LastCritMultiplier : 1.0f
                );
              }

              // Broadcast impact for FX and other listeners
              Vector3 travelDir = b.Velocity.LengthSquared() > 0.000001f ? b.Velocity.Normalized() : Vector3.Forward;
              GlobalEvents.Instance?.EmitImpactOccurred(hitPos, hitNormal, travelDir);
              if (DebugLogCollisions)
              {
                GD.Print($"[BulletManager] impact at ({hitPos.X:0.00},{hitPos.Y:0.00},{hitPos.Z:0.00}) n=({hitNormal.X:0.00},{hitNormal.Y:0.00},{hitNormal.Z:0.00}) collider={(collider!=null?collider.Name:"null")} id={colliderId}");
              }

              ProcessCollisionOrdered(ref b, arch, hitPos, hitNormal, nextPos, colliderId, isEnemy, collider, impact);
            }
          }
          else
          {
            b.Position = nextPos;
            b.LastColliderId = 0;
          }
        }

        // Keep active bullets by compacting
        if (b.Active && b.LifeRemaining > 0)
        {
          list[write++] = b;
          activeCount++;
        }
      }

      if (write < list.Count)
        list.RemoveRange(write, list.Count - write);

      // Update MultiMesh transforms for active bullets
      arch.MultiMesh.InstanceCount = activeCount;
      for (int i = 0; i < activeCount; i++)
      {
        BulletData b = list[i];
        Basis basis;
        if (arch.AlignToVelocity)
        {
          Vector3 forward = b.Velocity.LengthSquared() > 0.000001f ? b.Velocity.Normalized() : Vector3.Forward;
          basis = Basis.LookingAt(forward, Vector3.Up);
        }
        else
        {
          basis = Basis.Identity;
        }
        Transform3D xform = new Transform3D(basis, b.Position);
        if (Math.Abs(arch.VisualScale - 1.0f) > 0.0001f)
        {
          // Apply uniform scale by baking into basis (MultiMesh has no per-instance scale separate from transform)
          xform.Basis = xform.Basis.Scaled(new Vector3(arch.VisualScale, arch.VisualScale, arch.VisualScale));
        }
        Transform3D finalXform = xform * arch.LocalMeshTransform;
        arch.MultiMesh.SetInstanceTransform(i, finalXform);
      }

      // Trails update and render
      if (arch.TrailEnabled && arch.TrailBuffers != null)
      {
        var cam = GetViewport()?.GetCamera3D();
        Vector3 camPos = cam != null ? cam.GlobalTransform.Origin : (GlobalTransform.Origin + Vector3.Back * 10);

        // Age and append points, collect active ids
        var activeIds = new HashSet<uint>();
        for (int i = 0; i < activeCount; i++)
        {
          BulletData b = list[i];
          activeIds.Add(b.Id);

          if (!arch.TrailBuffers.TryGetValue(b.Id, out var buf))
          {
            buf = new TrailBuffer();
            arch.TrailBuffers[b.Id] = buf;
          }

          // Age points
          for (int p = 0; p < buf.Points.Count; p++)
          {
            var tp = buf.Points[p];
            tp.AgeLeft -= dt;
            buf.Points[p] = tp;
          }
          buf.Points.RemoveAll(tp => tp.AgeLeft <= 0);

          // Append new sample if moved enough
          if (buf.Points.Count == 0 || b.Position.DistanceSquaredTo(buf.LastAddedPos) >= arch.TrailMinDistance * arch.TrailMinDistance)
          {
            buf.Points.Add(new TrailPoint(b.Position, arch.TrailLifetime));
            buf.LastAddedPos = b.Position;
          }
          // Clamp max points
          if (buf.Points.Count > arch.TrailMaxPoints)
          {
            int remove = buf.Points.Count - arch.TrailMaxPoints;
            buf.Points.RemoveRange(0, remove);
          }
        }

        // Remove buffers for inactive bullets
        var toRemove = new List<uint>();
        foreach (var kvId in arch.TrailBuffers)
        {
          if (!activeIds.Contains(kvId.Key))
            toRemove.Add(kvId.Key);
        }
        foreach (var idrm in toRemove)
          arch.TrailBuffers.Remove(idrm);

        SurfaceTool st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        bool hasTriangles = false;

        foreach (var kvb in arch.TrailBuffers)
        {
          var pts = kvb.Value.Points;
          int ptCount = pts.Count;
          if (ptCount < 2)
            continue;

          Vector3[] leftPts = new Vector3[ptCount];
          Vector3[] rightPts = new Vector3[ptCount];
          float[] alphas = new float[ptCount];

          for (int i = 0; i < ptCount; i++)
          {
            Vector3 pos = pts[i].Pos;
            Vector3 tangent;
            if (i == 0)
              tangent = (pts[i + 1].Pos - pos).Normalized();
            else if (i == ptCount - 1)
              tangent = (pos - pts[i - 1].Pos).Normalized();
            else
              tangent = (pts[i + 1].Pos - pts[i - 1].Pos).Normalized();

            if (tangent.LengthSquared() < 1e-6)
              tangent = Vector3.Forward;

            Vector3 normal;
            if (arch.TrailViewAligned)
            {
              Vector3 view = (camPos - pos).Normalized();
              normal = view.Cross(tangent);
            }
            else
            {
              normal = Vector3.Up.Cross(tangent);
            }
            if (normal.LengthSquared() < 1e-6)
            {
              normal = Vector3.Up;
            }
            normal = normal.Normalized();

            float lifeNorm = Mathf.Clamp(pts[i].AgeLeft / arch.TrailLifetime, 0f, 1f);
            float width = arch.TrailWidth * lifeNorm;
            leftPts[i] = pos - normal * width;
            rightPts[i] = pos + normal * width;
            alphas[i] = lifeNorm;
          }

          for (int i = 1; i < ptCount; i++)
          {
            Vector3 l0 = leftPts[i - 1];
            Vector3 r0 = rightPts[i - 1];
            Vector3 l1 = leftPts[i];
            Vector3 r1 = rightPts[i];

            float a0 = alphas[i - 1];
            float a1 = alphas[i];

            st.SetColor(new Color(1f, 1f, 1f, a0));
            st.SetUV(new Vector2(0, 0));
            st.AddVertex(l0);

            st.SetColor(new Color(1f, 1f, 1f, a0));
            st.SetUV(new Vector2(1, 0));
            st.AddVertex(r0);

            st.SetColor(new Color(1f, 1f, 1f, a1));
            st.SetUV(new Vector2(0, 1));
            st.AddVertex(l1);

            st.SetColor(new Color(1f, 1f, 1f, a0));
            st.SetUV(new Vector2(1, 0));
            st.AddVertex(r0);

            st.SetColor(new Color(1f, 1f, 1f, a1));
            st.SetUV(new Vector2(1, 1));
            st.AddVertex(r1);

            st.SetColor(new Color(1f, 1f, 1f, a1));
            st.SetUV(new Vector2(0, 1));
            st.AddVertex(l1);

            hasTriangles = true;
          }
        }

        if (arch.TrailInstance != null)
        {
          if (hasTriangles)
          {
            Mesh mesh = st.Commit();
            var oldMesh = arch.TrailInstance.Mesh;
            arch.TrailInstance.Mesh = mesh;
            if (oldMesh != null)
              oldMesh.Dispose();
          }
          else
          {
            var oldMesh = arch.TrailInstance.Mesh;
            arch.TrailInstance.Mesh = null;
            if (oldMesh != null)
              oldMesh.Dispose();
          }
        }
      }
      else if (arch.TrailInstance != null)
      {
        var oldMesh = arch.TrailInstance.Mesh;
        arch.TrailInstance.Mesh = null;
        if (oldMesh != null)
          oldMesh.Dispose();
      }
    }
  }
}

// ===== Helper behavior methods =====
public partial class BulletManager
{
  private static Vector3 BlendDirections(Vector3 currentDir, Vector3 desiredDir, float t)
  {
    t = Mathf.Clamp(t, 0f, 1f);
    if (t <= 0f)
      return currentDir.LengthSquared() > 0 ? currentDir.Normalized() : Vector3.Forward;
    if (t >= 1f)
      return desiredDir.LengthSquared() > 0 ? desiredDir.Normalized() : Vector3.Forward;

    Vector3 a = currentDir.LengthSquared() > 0 ? currentDir.Normalized() : Vector3.Forward;
    Vector3 b = desiredDir.LengthSquared() > 0 ? desiredDir.Normalized() : Vector3.Forward;
    Vector3 mix = a * (1f - t) + b * t;
    if (mix.LengthSquared() < 1e-8f)
      return b; // fallback if opposite
    return mix.Normalized();
  }
  private static Vector3 ToLocalNormal(Node3D node, Vector3 worldNormal)
  {
    var basis = node.GlobalTransform.Basis;
    return (basis.Inverse() * worldNormal).Normalized();
  }

  private static Vector3 ToGlobalNormal(Node3D node, Vector3 localNormal)
  {
    var basis = node.GlobalTransform.Basis;
    return (basis * localNormal).Normalized();
  }
  private void PerformPendingActionsOnStickyEnd(ref BulletData b, Archetype arch)
  {
    int start = b.PendingActionIndex;
    if (start < 0)
      return;

    if (start >= arch.CollisionOps.Length)
    {
      b.PendingActionIndex = -1;
      return;
    }

    Vector3 pos = b.Position;
    Vector3 normalWorld = b.StuckWorldNormal;
    Node3D? collider = null;
    if (b.StuckTargetId != 0)
    {
      var obj = GodotObject.InstanceFromId(b.StuckTargetId);
      if (obj is Node3D stuckNode && IsInstanceValid(stuckNode))
      {
        collider = stuckNode;
        pos = stuckNode.ToGlobal(b.StuckLocalOffset);
        normalWorld = ToGlobalNormal(stuckNode, b.StuckLocalNormal);
      }
    }

    Vector3 dir = b.Velocity.LengthSquared() > 0.000001f ? b.Velocity.Normalized() : Vector3.Forward;
    Vector3 nextPos = pos + dir * MathF.Max(arch.Radius * 0.5f, 0.01f);
    bool isEnemy = collider != null && collider.IsInGroup("enemies");

    ImpactSnapshot snapshot = b.PendingImpactSnapshot ?? new ImpactSnapshot(
      damage: b.Damage,
      knockbackScale: 1.0f,
      enemyHit: isEnemy,
      enemyId: b.StuckTargetId,
      hitPosition: pos,
      hitNormal: normalWorld,
      isCrit: false,
      critMultiplier: 1.0f
    );
    ProcessCollisionOps(ref b, arch, pos, normalWorld, nextPos, b.StuckTargetId, isEnemy, collider, Math.Max(0, start), snapshot);
    b.PendingActionIndex = -1;
    b.PendingImpactSnapshot = null;
  }
  private void ProcessCollisionOrdered(ref BulletData b, Archetype arch, Vector3 hitPos, Vector3 hitNormal, Vector3 nextPos, ulong colliderId, bool isEnemy, Node3D? collider, ImpactSnapshot snapshot)
  {
    ProcessCollisionOps(ref b, arch, hitPos, hitNormal, nextPos, colliderId, isEnemy, collider, 0, snapshot);
  }

  // Note: All collision-op executions require an applied-damage snapshot.
  // For deferred paths (e.g., sticky), callers must persist and supply it.

  private void ProcessCollisionOps(ref BulletData b, Archetype arch, Vector3 hitPos, Vector3 hitNormal, Vector3 nextPos, ulong colliderId, bool isEnemy, Node3D? collider, int startIndex, ImpactSnapshot snapshot)
  {
    var state = new BulletCollisionState
    {
      Position = b.Position,
      PrevPosition = b.PrevPosition,
      Velocity = b.Velocity,
      Damage = b.Damage,
      BounceCount = b.BounceCount,
      PenetrationCount = b.PenetrationCount,
      LastColliderId = b.LastColliderId,
      CollisionCooldown = b.CollisionCooldown,
    };

    b.LastColliderId = colliderId;
    b.CollisionCooldown = 0.0f;
    b.PendingActionIndex = -1;
    state.LastColliderId = colliderId;

    var behavior = arch.BehaviorConfig;
    var ops = arch.CollisionOps;

    if (ops.Length == 0)
    {
      b.Position = hitPos;
      b.PrevPosition = hitPos;
      b.Velocity = state.Velocity;
      if (arch.DestroyOnImpact)
        b.Active = false;
      return;
    }

    bool resolvedMotion = false;
    bool keepAlive = false;

    float actionDamage = snapshot.Damage;
    for (int i = Math.Max(0, startIndex); i < ops.Length; i++)
    {
      var action = ops[i].Action;
      switch (action)
      {
        case CollisionActionType.Explode:
          if (behavior?.Explosive is ExplosiveConfig expCfg)
          {
            // Use a snapshot carrying the adjusted actionDamage for AoE scaling
            var aoeSnapshot = new ImpactSnapshot(
              damage: actionDamage,
              knockbackScale: snapshot.KnockbackScale,
              enemyHit: snapshot.EnemyHit,
              enemyId: snapshot.EnemyId,
              hitPosition: snapshot.HitPosition,
              hitNormal: snapshot.HitNormal,
              isCrit: snapshot.IsCrit,
              critMultiplier: snapshot.CritMultiplier
            );
            ApplyExplosionAOE(hitPos, expCfg, aoeSnapshot);
          }
          break;

        case CollisionActionType.Sticky:
          if (behavior?.Sticky is StickyConfig stickyCfg && b.StickyCooldown <= 0.0f)
          {
            if (isEnemy)
            {
              Node3D? damageTarget = null;
              try
              {
                if (collider is Enemy enemyStick)
                {
                  enemyStick.TakeDamage(stickyCfg.CollisionDamage);
                  Vector3 dirK = state.Velocity.LengthSquared() > 0.000001f ? state.Velocity.Normalized() : Vector3.Forward;
                  dirK = new Vector3(dirK.X, 0.15f * dirK.Y, dirK.Z).Normalized();
                  GlobalEvents.Instance?.EmitDamageDealt(enemyStick, snapshot, dirK, DefaultKnockback);
                  damageTarget = enemyStick;
                }
                else if (collider != null && collider.HasMethod("take_damage"))
                {
                  collider.CallDeferred("take_damage", stickyCfg.CollisionDamage);
                  Vector3 dirK = state.Velocity.LengthSquared() > 0.000001f ? state.Velocity.Normalized() : Vector3.Forward;
                  dirK = new Vector3(dirK.X, 0.15f * dirK.Y, dirK.Z).Normalized();
                  GlobalEvents.Instance?.EmitDamageDealt(collider, snapshot, dirK, DefaultKnockback);
                  damageTarget = collider;
                }
              }
              catch (Exception e)
              {
                GD.PrintErr($"BulletManager sticky damage call failed: {e.Message}");
              }
              if (damageTarget != null)
                FloatingNumber3D.Spawn(this, damageTarget, stickyCfg.CollisionDamage);
            }

            b.Position = hitPos;
            b.PrevPosition = b.Position;
            b.StuckPreVelocity = state.Velocity;
            b.Velocity = Vector3.Zero;
            b.StuckTimeLeft = stickyCfg.Duration;
            b.StuckTargetId = colliderId;
            b.StuckLocalOffset = collider != null ? collider.ToLocal(hitPos) : Vector3.Zero;
            b.StuckWorldNormal = hitNormal;
            b.StuckLocalNormal = collider != null ? ToLocalNormal(collider, hitNormal) : hitNormal;
            if (collider != null)
              SpawnStickyBlob(collider, hitPos, hitNormal, arch.Radius, stickyCfg.Duration);
            b.PendingActionIndex = i + 1;
            // Carry forward any action-based damage adjustments before deferred continuation
            b.PendingImpactSnapshot = new ImpactSnapshot(
              damage: actionDamage,
              knockbackScale: snapshot.KnockbackScale,
              enemyHit: snapshot.EnemyHit,
              enemyId: snapshot.EnemyId,
              hitPosition: snapshot.HitPosition,
              hitNormal: snapshot.HitNormal,
              isCrit: snapshot.IsCrit,
              critMultiplier: snapshot.CritMultiplier
            );
            return;
          }
          break;

        case CollisionActionType.Pierce:
          if (behavior?.Pierce is PierceConfig pierceCfg && !resolvedMotion && isEnemy && state.PenetrationCount < pierceCfg.MaxPenetrations)
          {
            state.PenetrationCount++;
            state.Damage *= (1.0f - pierceCfg.DamageReduction);
            actionDamage *= (1.0f - pierceCfg.DamageReduction);
            state.Velocity *= pierceCfg.VelocityFactor;
            Vector3 forward = state.Velocity.LengthSquared() > 0.0001f
              ? state.Velocity.Normalized()
              : (nextPos - state.Position).Normalized();
            if (forward.LengthSquared() < 0.0001f)
              forward = Vector3.Forward;
            float epsilon = MathF.Max(0.01f, arch.Radius);
            state.Position = hitPos + forward * epsilon;
            state.PrevPosition = state.Position;
            state.CollisionCooldown = pierceCfg.Cooldown;
            keepAlive = true;
            resolvedMotion = true;
          }
          break;

        case CollisionActionType.Bounce:
          if (behavior?.Bounce is BounceConfig bounceCfg && !resolvedMotion && hitNormal.LengthSquared() > 0.000001f && state.BounceCount < bounceCfg.MaxBounces)
          {
            Vector3 normal = hitNormal.Normalized();
            state.BounceCount++;
            state.Velocity = state.Velocity.Bounce(normal) * bounceCfg.Bounciness;
            state.Damage *= (1.0f - bounceCfg.DamageReduction);
            actionDamage *= (1.0f - bounceCfg.DamageReduction);
            float epsilon = MathF.Max(0.01f, arch.Radius);
            state.Position = hitPos + normal * epsilon;
            state.PrevPosition = state.Position;
            keepAlive = true;
            resolvedMotion = true;
          }
          break;
      }
    }

    b.Position = state.Position;
    b.PrevPosition = state.PrevPosition;
    b.Velocity = state.Velocity;
    b.Damage = state.Damage;
    b.BounceCount = state.BounceCount;
    b.PenetrationCount = state.PenetrationCount;
    b.LastColliderId = state.LastColliderId;
    b.CollisionCooldown = state.CollisionCooldown;

    if (!keepAlive)
    {
      if (arch.DestroyOnImpact)
        b.Active = false;
    }
    else if (behavior?.Aimbot is AimbotConfig aimCfg)
    {
      _ = TryApplyAimbot(ref b, aimCfg, isEnemy ? colliderId : 0);
    }
  }

  private void ApplyPerTickBehaviors(ref BulletData b, Archetype arch, in SteeringFrameCache steeringCache)
  {
    var ops = arch.SteeringOps;
    if (ops.Length == 0)
      return;

    float speed = b.Velocity.Length();
    if (speed <= 0.0001f)
      speed = 0.0001f;

    Vector3 curDir = b.Velocity.LengthSquared() > 0.0f ? b.Velocity.Normalized() : Vector3.Forward;

    for (int i = 0; i < ops.Length; i++)
    {
      var op = ops[i];
      switch (op.Kind)
      {
        case SteeringOpKind.Homing:
        {
          Node3D? nearest = FindNearestEnemy(b.Position, op.ParamA);
          if (nearest != null)
          {
            Vector3 desiredDir = nearest.GlobalTransform.Origin - b.Position;
            if (desiredDir.LengthSquared() > 0.000001f)
            {
              desiredDir = desiredDir.Normalized();
              float strength = Mathf.Clamp(op.ParamB, 0f, 1f);
              Vector3 blendedDir = BlendDirections(curDir, desiredDir, strength);
              b.Velocity = blendedDir * speed;
              curDir = blendedDir;
            }
          }
          break;
        }
        case SteeringOpKind.Tracking:
        {
          if (!steeringCache.HasTrackingTarget)
            break;

          Vector3 desiredDir = steeringCache.TrackingTarget - b.Position;
          if (desiredDir.LengthSquared() < 0.000001f)
            break;

          desiredDir = desiredDir.Normalized();
          float strength = Mathf.Clamp(op.ParamA, 0f, 1f);
          Vector3 blendedDir = BlendDirections(curDir, desiredDir, strength);
          b.Velocity = blendedDir * speed;
          curDir = blendedDir;
          break;
        }
      }
    }
  }


  private bool TryApplyAimbot(ref BulletData b, AimbotConfig cfg, ulong excludeId = 0)
  {
    Vector3 origin = b.Position;
    Vector3 baseDir = b.Velocity.LengthSquared() > 0.0001f ? b.Velocity.Normalized() : Vector3.Forward;
    float bestAngle = cfg.AimConeAngle;
    Node3D best = null!;

    foreach (Node node in GetTree().GetNodesInGroup("enemies"))
    {
      if (node is not Node3D enemy || !IsInstanceValid(enemy))
        continue;
      // Do not re-target the same enemy consecutively.
      if (excludeId != 0 && enemy.GetInstanceId() == excludeId)
        continue;
      Vector3 toEnemy = (enemy.GlobalTransform.Origin - origin);
      float dist = toEnemy.Length();
      if (dist > cfg.Radius || dist <= 0.0001f)
        continue;
      Vector3 dir = toEnemy.Normalized();
      float angle = Mathf.Acos(Mathf.Clamp(baseDir.Dot(dir), -1f, 1f));
      if (angle < bestAngle)
      {
        bestAngle = angle;
        best = enemy;
      }
    }

    if (best != null)
    {
      Vector3 target = best.GlobalTransform.Origin + new Vector3(0, cfg.VerticalOffset, 0);
      Vector3 newDir = (target - origin).Normalized();
      float speed = b.Velocity.Length();
      if (speed <= 0.0001f) speed = 0.0001f;
      b.Velocity = newDir * speed;
      // Visual: draw a quick line to target
      SpawnAimbotLine(origin, target, cfg.LineWidth, cfg.LineDuration);
      return true;
    }
    return false;
  }

  private void ApplyExplosionAOE(Vector3 center, ExplosiveConfig cfg, ImpactSnapshot snapshot)
  {
    // Emit VFX event to allow batched rendering and other listeners
    GlobalEvents.Instance?.EmitExplosionOccurred(center, cfg.Radius);
    float radius = cfg.Radius;
    float damage = snapshot.Damage * cfg.DamageMultiplier;
    foreach (Node node in GetTree().GetNodesInGroup("enemies"))
    {
      if (node is not Node3D enemyNode || !IsInstanceValid(enemyNode))
        continue;
      float dist = enemyNode.GlobalTransform.Origin.DistanceTo(center);
      if (dist <= radius)
      {
        if (DamageOcclusionService.IsBlocked(this, center, enemyNode.GlobalTransform.Origin, out _, out _))
          continue;
        try
        {
          if (enemyNode is Enemy enemy)
            enemy.TakeDamage(damage);
          else if (enemyNode.HasMethod("take_damage"))
            enemyNode.CallDeferred("take_damage", damage);
          else
            continue;
        }
        catch (Exception e)
        {
          GD.PrintErr($"BulletManager explosion damage failed: {e.Message}");
        }

        // Visual: damage number above the enemy on explosion hit (yellow on crit)
        Color? numColor = snapshot.IsCrit ? Colors.Yellow : (Color?)null;
        FloatingNumber3D.Spawn(this, enemyNode, damage, numColor);

        // Emit global damage for knockback with simple radial falloff
        Vector3 radial = (enemyNode.GlobalTransform.Origin - center);
        Vector3 dir = radial.LengthSquared() > 0.000001f ? radial.Normalized() : Vector3.Up;
        float falloff = Mathf.Clamp(1.0f - (dist / Mathf.Max(0.0001f, radius)), 0.0f, 1.0f);
        GlobalEvents.Instance?.EmitDamageDealt(enemyNode, snapshot, dir, DefaultKnockback, falloff);
      }
    }
  }

  private Node3D? FindNearestEnemy(Vector3 position, float radius, Node3D? exclude = null)
  {
    Node3D? nearest = null;
    float best = radius > 0.0f ? radius : float.PositiveInfinity;
    foreach (Node node in GetTree().GetNodesInGroup("enemies"))
    {
      if (node is not Node3D enemy || !IsInstanceValid(enemy))
        continue;
      if (exclude != null && enemy == exclude)
        continue;
      float d = enemy.GlobalTransform.Origin.DistanceTo(position);
      if (d < best)
      {
        best = d;
        nearest = enemy;
      }
    }
    return nearest;
  }

  private void SpawnAimbotLine(Vector3 start, Vector3 end, float width, float duration)
  {
    try
    {
      Node parent = GetTree().CurrentScene ?? this;
      float distance = start.DistanceTo(end);
      if (distance <= 0.0001f)
        return;

      float thickness = Mathf.Max(0.001f, width);
      Vector3 mid = (start + end) * 0.5f;
      Vector3 direction = (end - start).Normalized();

      Camera3D? camera = GetViewport()?.GetCamera3D();
      Vector3 cameraVector = camera != null
        ? (camera.GlobalTransform.Origin - mid)
        : Vector3.Up;
      if (cameraVector.LengthSquared() <= 0.000001f)
        cameraVector = Vector3.Up;

      Vector3 normal = direction.Cross(cameraVector);
      if (normal.LengthSquared() <= 0.000001f)
      {
        normal = direction.Cross(Vector3.Up);
        if (normal.LengthSquared() <= 0.000001f)
          normal = direction.Cross(Vector3.Right);
      }
      normal = normal.Normalized();
      Vector3 offset = normal * (thickness * 0.5f);

      Vector3 startLocal = start - mid;
      Vector3 endLocal = end - mid;

      Vector3 v0 = startLocal + offset;
      Vector3 v1 = startLocal - offset;
      Vector3 v2 = endLocal - offset;
      Vector3 v3 = endLocal + offset;

      var st = new SurfaceTool();
      st.Begin(Mesh.PrimitiveType.Triangles);

      var color = new Color(1, 0, 0, 1);
      st.SetColor(color);
      st.AddVertex(v0);
      st.SetColor(color);
      st.AddVertex(v1);
      st.SetColor(color);
      st.AddVertex(v2);

      st.SetColor(color);
      st.AddVertex(v2);
      st.SetColor(color);
      st.AddVertex(v3);
      st.SetColor(color);
      st.AddVertex(v0);

      ArrayMesh? mesh = st.Commit();
      if (mesh == null)
        return;

      var line = new MeshInstance3D
      {
        Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
      };

      var mat = new StandardMaterial3D
      {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = color,
        Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
      };
      line.MaterialOverride = mat;

      parent.AddChild(line);
      line.GlobalTransform = new Transform3D(Basis.Identity, mid);

      // Auto-remove after duration using a Timer
      var timer = new Godot.Timer { OneShot = true, WaitTime = Math.Max(0.01f, duration) };
      timer.Timeout += () =>
      {
        if (IsInstanceValid(line)) line.QueueFree();
        if (IsInstanceValid(timer)) timer.QueueFree();
      };
      parent.AddChild(timer);
      timer.Start();
    }
    catch (Exception e)
    {
      GD.PrintErr($"Aimbot line failed: {e.Message}");
    }
  }

  private void SpawnStickyBlob(Node3D collider, Vector3 worldPos, Vector3 normal, float radius, float duration)
  {
    try
    {
      Node parent = collider ?? (GetTree().CurrentScene ?? this);
      var holder = new Node3D();
      parent.AddChild(holder);
      holder.GlobalPosition = worldPos;

      var blob = new MeshInstance3D();
      var sphere = new SphereMesh { RadialSegments = 4, Rings = 4 };
      float slimeThickness = radius / 1.5f;
      float randomOffset = (float)GD.RandRange(0.0, slimeThickness / 2.0);
      sphere.Radius = radius + slimeThickness + randomOffset;
      sphere.Height = sphere.Radius * 2f;
      blob.Mesh = sphere;

      var mat = new StandardMaterial3D
      {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(0.2f, 1f, 0.2f, 0.35f),
      };
      blob.MaterialOverride = mat;
      holder.AddChild(blob);

      // Clean up after duration
      var timer = new Godot.Timer { OneShot = true, WaitTime = Math.Max(0.05f, duration) };
      timer.Timeout += () => { if (IsInstanceValid(holder)) holder.QueueFree(); if (IsInstanceValid(timer)) timer.QueueFree(); };
      parent.AddChild(timer);
      timer.Start();
    }
    catch (Exception e)
    {
      GD.PrintErr($"Sticky blob failed: {e.Message}");
    }
  }
}
