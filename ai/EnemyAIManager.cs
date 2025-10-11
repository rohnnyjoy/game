using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AI;

#nullable enable

public partial class EnemyAIManager : Node
{
  public static EnemyAIManager? Instance { get; private set; }

  internal static ulong CurrentPhysicsFrame { get; private set; }

  [Export(PropertyHint.Range, "0,1000,1")] public int MaxAiUpdatesPerFrame { get; set; } = 0;

  private float _midRangeDistance = 40f;
  private float _farRangeDistance = 100f;
  private float _maxConsiderDistance = 200f;
  private float _midRangeDistanceSquared = 40f * 40f;
  private float _farRangeDistanceSquared = 100f * 100f;
  private float _maxConsiderDistanceSquared = 200f * 200f;

  [Export(PropertyHint.Range, "0,500,1")] public float MidRangeDistance
  {
    get => _midRangeDistance;
    set
    {
      float clamped = Mathf.Max(0f, value);
      if (Mathf.IsEqualApprox(_midRangeDistance, clamped))
        return;
      _midRangeDistance = clamped;
      _midRangeDistanceSquared = clamped * clamped;
    }
  }

  [Export(PropertyHint.Range, "0,1000,1")] public float FarRangeDistance
  {
    get => _farRangeDistance;
    set
    {
      float clamped = Mathf.Max(0f, value);
      if (Mathf.IsEqualApprox(_farRangeDistance, clamped))
        return;
      _farRangeDistance = clamped;
      _farRangeDistanceSquared = clamped * clamped;
    }
  }

  [Export(PropertyHint.Range, "0,500,1")] public float MaxConsiderDistance
  {
    get => _maxConsiderDistance;
    set
    {
      float clamped = Mathf.Max(0f, value);
      if (Mathf.IsEqualApprox(_maxConsiderDistance, clamped))
        return;
      _maxConsiderDistance = clamped;
      _maxConsiderDistanceSquared = clamped * clamped;
    }
  }

  [Export] public int MidRangeIntervalFrames { get; set; } = 2;
  [Export] public int FarRangeIntervalFrames { get; set; } = 5;
  [Export] public bool EnableLod { get; set; } = true;
  [Export] public bool DisableActivationRadius { get; set; } = true;

  private struct EnemySimData
  {
    public bool Active;
    public Enemy Proxy;
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 HorizontalVelocity;
    public Vector3 KnockbackVelocity;
    public float SpeedMultiplier;
    public Enemy.SimulationState State;
    public float AccumulatedDelta;
    public float RestrictedCheckTimer;
    public int ForcedPhysicsSteps;
    public uint LodFrameOffset;
    public bool OnFloor;
    public float StartX;
    public int PatrolDirection;
    public float CapsuleRadius;
  }

  private readonly List<EnemySimData> _simData = new();
  private readonly Stack<int> _freeIndices = new();
  private readonly Dictionary<Enemy, int> _indices = new();

  private int _cursor = 0;
  private ulong _frameIndex = 0;

  private readonly HashSet<Enemy> _activeSimEnemies = new();

  private static readonly List<Node3D> PlayerCache = new(2);
  private static readonly List<Vector3> PlayerPositions = new(2);
  private static readonly Enemy[] EmptyEnemies = Array.Empty<Enemy>();

  private readonly RandomNumberGenerator _lodPhaseRng = CreateLodRng();

  public static IReadOnlyList<Node3D> ActivePlayers => PlayerCache;
  public static IReadOnlyCollection<Enemy> ActiveSimulationEnemies
    => Instance != null ? (IReadOnlyCollection<Enemy>)Instance._activeSimEnemies : EmptyEnemies;

  public override void _Ready()
  {
    if (Instance != null && Instance != this)
    {
      QueueFree();
      return;
    }

    Instance = this;
    Name = nameof(EnemyAIManager);
    AddToGroup("ai_manager");
    ProcessPriority = -10;

    foreach (var node in GetTree().GetNodesInGroup("enemies"))
    {
      if (node is Enemy enemy)
        Register(enemy);
    }
  }

  public override void _ExitTree()
  {
    if (Instance == this)
    {
      Instance = null;
      PlayerCache.Clear();
      PlayerPositions.Clear();
      _activeSimEnemies.Clear();
      _indices.Clear();
      _simData.Clear();
      _freeIndices.Clear();
    }

    base._ExitTree();
  }

  public void Register(Enemy enemy)
  {
    if (enemy == null || !GodotObject.IsInstanceValid(enemy))
      return;
    if (_indices.ContainsKey(enemy))
      return;

    int index = _freeIndices.Count > 0 ? _freeIndices.Pop() : _simData.Count;
    if (index >= _simData.Count)
      _simData.Add(default);

    (float capsuleRadius, float capsuleHalfHeight) = ExtractCapsuleDimensions(enemy);

    EnemySimData data = new EnemySimData
    {
      Active = true,
      Proxy = enemy,
      Position = enemy.GlobalTransform.Origin,
      Velocity = Vector3.Zero,
      HorizontalVelocity = Vector3.Zero,
      KnockbackVelocity = Vector3.Zero,
      SpeedMultiplier = 1.0f,
      State = Enemy.SimulationState.Active,
      AccumulatedDelta = 0f,
      RestrictedCheckTimer = 0f,
      ForcedPhysicsSteps = 0,
      LodFrameOffset = (uint)_lodPhaseRng.RandiRange(0, 1023),
      OnFloor = false,
      StartX = enemy.GlobalTransform.Origin.X,
      PatrolDirection = 1,
      CapsuleRadius = capsuleRadius,
      
    };

    CollectionsMarshal.AsSpan(_simData)[index] = data;
    _indices[enemy] = index;
    _activeSimEnemies.Add(enemy);
    enemy.SimulationHandle = index;
    enemy.PlayIdleAnimation();
  }

  public void Unregister(Enemy enemy)
  {
    if (enemy == null)
      return;
    if (!_indices.TryGetValue(enemy, out int index))
      return;

    RemoveIndex(index);
  }

  public override void _PhysicsProcess(double delta)
  {
    base._PhysicsProcess(delta);

    CurrentPhysicsFrame = Engine.GetPhysicsFrames();

    if (_frameIndex % 120 == 0)
      PruneInvalidEnemies();

    int total = _simData.Count;
    if (total == 0)
    {
      _frameIndex++;
      return;
    }

    RefreshPlayerCache();

    float dt = (float)delta;
    var (start, count) = AIScheduler.ComputeSlice(total, MaxAiUpdatesPerFrame, _cursor);

    for (int i = 0; i < count; i++)
    {
      int idx = (start + i) % total;
      ref EnemySimData data = ref CollectionsMarshal.AsSpan(_simData)[idx];
      if (!data.Active)
        continue;

      Enemy enemy = data.Proxy;
      if (enemy == null || !GodotObject.IsInstanceValid(enemy))
      {
        RemoveIndex(idx);
        continue;
      }

      if (enemy.IsDying)
      {
        if (data.State != Enemy.SimulationState.Sleeping)
        {
          enemy.HandleSimulationStateTransition(data.State, Enemy.SimulationState.Sleeping);
          data.State = Enemy.SimulationState.Sleeping;
          data.HorizontalVelocity = Vector3.Zero;
          data.Velocity = Vector3.Zero;
          data.KnockbackVelocity = Vector3.Zero;
          data.OnFloor = true;
          data.AccumulatedDelta = 0f;
          _activeSimEnemies.Remove(enemy);
          enemy.ApplySimulation(data.Position, data.Velocity);
        }
        continue;
      }

      Node3D? nearestPlayer = null;
      float nearestDist2 = float.PositiveInfinity;
      if (PlayerCache.Count > 0)
        nearestPlayer = NearestPlayer(PlayerCache, PlayerPositions, data.Position, out nearestDist2);

      float activeRadiusSq = enemy.ActiveSimulationRadius * enemy.ActiveSimulationRadius;
      float midRadiusSq = enemy.MidSimulationRadius * enemy.MidSimulationRadius;
      float farRadiusSq = enemy.FarSimulationRadius * enemy.FarSimulationRadius;
      float sleepRadiusSq = enemy.SleepSimulationRadius * enemy.SleepSimulationRadius;

      Enemy.SimulationState desiredState = DisableActivationRadius
        ? Enemy.SimulationState.Active
        : DetermineSimulationState(data, nearestDist2,
          activeRadiusSq, midRadiusSq, farRadiusSq, sleepRadiusSq,
          enemy.SleepSimulationRadius, enemy.FarSimulationRadius);

      if (data.State != desiredState)
      {
        enemy.HandleSimulationStateTransition(data.State, desiredState);
        if (desiredState == Enemy.SimulationState.Sleeping)
          _activeSimEnemies.Remove(enemy);
        else
          _activeSimEnemies.Add(enemy);

        data.State = desiredState;
        data.KnockbackVelocity = Vector3.Zero;
        data.AccumulatedDelta = 0f;
        data.RestrictedCheckTimer = 0f;
        data.ForcedPhysicsSteps = 0;
        data.HorizontalVelocity = Vector3.Zero;
        data.Velocity = Vector3.Zero;
      }

      if (data.State == Enemy.SimulationState.Sleeping)
      {
        enemy.ApplySimulation(data.Position, data.Velocity);
        continue;
      }

      _activeSimEnemies.Add(enemy);

      Node3D? target = null;
      if (enemy.TargetOverride != null && GodotObject.IsInstanceValid(enemy.TargetOverride))
        target = enemy.TargetOverride;
      else if (nearestPlayer != null && (DisableActivationRadius || nearestDist2 <= _maxConsiderDistanceSquared))
        target = nearestPlayer;

      data.AccumulatedDelta += dt;

      if (ShouldUpdateSteering(ref data))
        UpdateSteering(ref data, enemy, target);

      data.Velocity = new Vector3(data.HorizontalVelocity.X, data.Velocity.Y, data.HorizontalVelocity.Z);
      bool shouldRunPhysics = ShouldRunPhysics(ref data);
      if (!shouldRunPhysics)
      {
        // Skip writing to the physics body so engine gravity and contacts
        // continue to act normally during LOD-skipped frames.
        continue;
      }

      float effectiveDelta = MathF.Max(data.AccumulatedDelta, dt);
      data.AccumulatedDelta = 0f;

      ApplyGravity(ref data, enemy, effectiveDelta);

      // Knockback is applied in PerformMovement alongside XZ steering.

      PerformMovement(ref data, enemy, effectiveDelta);

      data.KnockbackVelocity = data.KnockbackVelocity.MoveToward(Vector3.Zero, enemy.KnockbackDamping * effectiveDelta);
      if (data.ForcedPhysicsSteps > 0)
        data.ForcedPhysicsSteps--;

      TickRestrictedVolumes(enemy, ref data, effectiveDelta);

      // Cheap contact damage check without per-enemy Areas/signals
      TryDealContactDamage(enemy, ref data);

      enemy.ApplySimulation(data.Position, data.Velocity);
    }

    _cursor = AIScheduler.AdvanceCursor(total, MaxAiUpdatesPerFrame, _cursor);
    _frameIndex++;
  }

  public Enemy.SimulationState GetSimulationState(Enemy enemy)
  {
    if (enemy == null)
      return Enemy.SimulationState.Active;
    if (_indices.TryGetValue(enemy, out int index))
    {
      ref EnemySimData data = ref CollectionsMarshal.AsSpan(_simData)[index];
      if (data.Active)
        return data.State;
    }
    return Enemy.SimulationState.Active;
  }

  public void ApplyKnockback(Enemy enemy, Vector3 impulse)
  {
    if (enemy == null)
      return;
    if (!_indices.TryGetValue(enemy, out int index))
      return;

    ref EnemySimData data = ref CollectionsMarshal.AsSpan(_simData)[index];
    if (!data.Active)
      return;

    Vector3 adjusted = impulse;
    float len = adjusted.Length();
    float max = MathF.Max(0.01f, enemy.MaxKnockbackSpeed);
    if (len > max)
      adjusted = adjusted / len * max;

    data.KnockbackVelocity = adjusted;
    data.ForcedPhysicsSteps = Math.Max(data.ForcedPhysicsSteps, 2);
  }

  public void SetSpeedMultiplier(Enemy enemy, float multiplier)
  {
    if (enemy == null)
      return;
    if (!_indices.TryGetValue(enemy, out int index))
      return;

    ref EnemySimData data = ref CollectionsMarshal.AsSpan(_simData)[index];
    if (!data.Active)
      return;

    data.SpeedMultiplier = MathF.Max(0f, multiplier);
  }

  public void SyncEnemyTransform(Enemy enemy)
  {
    if (enemy == null)
      return;
    if (!_indices.TryGetValue(enemy, out int index))
      return;

    ref EnemySimData data = ref CollectionsMarshal.AsSpan(_simData)[index];
    if (!data.Active)
      return;

    data.Position = enemy.GlobalTransform.Origin;
    data.Velocity = Vector3.Zero;
    data.HorizontalVelocity = Vector3.Zero;
    data.KnockbackVelocity = Vector3.Zero;
    data.AccumulatedDelta = 0f;
    data.RestrictedCheckTimer = 0f;
    data.ForcedPhysicsSteps = 0;
    data.OnFloor = true;

    enemy.ApplySimulation(data.Position, data.Velocity);
  }

  private void PerformMovement(ref EnemySimData data, Enemy enemy, float dt)
  {
    // Preserve vertical velocity (engine gravity + contacts), drive XZ.
    Vector3 v = enemy.Velocity;
    v.X = data.HorizontalVelocity.X;
    v.Z = data.HorizontalVelocity.Z;
    v += data.KnockbackVelocity;
    enemy.Velocity = v;

    data.Position = enemy.GlobalTransform.Origin;
    data.Velocity = enemy.Velocity;
    data.HorizontalVelocity = new Vector3(data.Velocity.X, 0f, data.Velocity.Z);
    data.OnFloor = false;
  }

  private void ApplyGravity(ref EnemySimData data, Enemy enemy, float delta)
  {
    // Rely on engine gravity (via Enemy.GravityScale). Nothing to do here.
  }

  // Floor probing logic removed; physics engine handles grounding.

  private void UpdateSteering(ref EnemySimData data, Enemy enemy, Node3D? target)
  {
    if (target != null && GodotObject.IsInstanceValid(target))
    {
      Vector3 direction = target.GlobalTransform.Origin - data.Position;
      direction.Y = 0f;
      if (direction.LengthSquared() > 0.000001f)
      {
        direction = direction.Normalized();
        Vector3 desired = direction * Enemy.SPEED * data.SpeedMultiplier;
        data.HorizontalVelocity = desired;
        enemy.UpdateFacing(desired);
        enemy.PlayMoveAnimation();
        return;
      }
    }

    if (enemy.Patrol)
    {
      float right = data.StartX + Enemy.MOVE_DISTANCE;
      float left = data.StartX - Enemy.MOVE_DISTANCE;
      if (data.Position.X >= right)
        data.PatrolDirection = -1;
      else if (data.Position.X <= left)
        data.PatrolDirection = 1;

      float xSpeed = data.PatrolDirection * Enemy.SPEED * data.SpeedMultiplier;
      Vector3 desired = new Vector3(xSpeed, 0f, 0f);
      data.HorizontalVelocity = desired;
      enemy.UpdateFacing(desired);
      enemy.PlayMoveAnimation();
    }
    else
    {
      data.HorizontalVelocity = Vector3.Zero;
      enemy.PlayIdleAnimation();
    }
  }

  private bool ShouldUpdateSteering(ref EnemySimData data)
  {
    switch (data.State)
    {
      case Enemy.SimulationState.Active:
        return true;
      case Enemy.SimulationState.BudgetMid:
        return ShouldRunLodFrame(ref data, Enemy.MidUpdateStride);
      case Enemy.SimulationState.BudgetFar:
        return ShouldRunLodFrame(ref data, Enemy.FarUpdateStride);
      default:
        return false;
    }
  }

  private bool ShouldRunPhysics(ref EnemySimData data)
  {
    if (data.ForcedPhysicsSteps > 0)
      return true;

    switch (data.State)
    {
      case Enemy.SimulationState.Active:
        return true;
      case Enemy.SimulationState.BudgetMid:
        return ShouldRunLodFrame(ref data, Enemy.MidUpdateStride);
      case Enemy.SimulationState.BudgetFar:
        return ShouldRunLodFrame(ref data, Enemy.FarUpdateStride);
      default:
        return false;
    }
  }

  private bool ShouldRunLodFrame(ref EnemySimData data, int stride)
  {
    if (!EnableLod || stride <= 1)
      return true;

    ulong frame = CurrentPhysicsFrame;
    if (frame == 0)
      frame = Engine.GetPhysicsFrames();
    return ((frame + data.LodFrameOffset) % (ulong)stride) == 0;
  }

  private void TickRestrictedVolumes(Enemy enemy, ref EnemySimData data, float delta)
  {
    if (!enemy.EnforceRestrictedVolumes || enemy.IsDying)
      return;

    data.RestrictedCheckTimer -= delta;
    if (data.RestrictedCheckTimer > 0f)
      return;

    float interval = Enemy.RestrictedCheckInterval;
    if (data.State == Enemy.SimulationState.BudgetMid)
      interval *= 1.8f;
    else if (data.State == Enemy.SimulationState.BudgetFar)
      interval *= 3.5f;
    data.RestrictedCheckTimer = interval;

    var zones = ShopSafeZone.ActiveZones;
    if (zones == null || zones.Count == 0)
      return;

    Vector3 position = data.Position;
    Vector3 cumulativePush = Vector3.Zero;

    foreach (ShopSafeZone zone in zones)
    {
      if (zone == null)
        continue;
      if (!zone.TryGetRepulsion(position, enemy.RestrictedVolumePadding, out Vector3 push))
        continue;
      position += push;
      cumulativePush += push;
    }

    if (cumulativePush.LengthSquared() <= 0.000001f)
      return;

    // Apply horizontal displacement directly to the rigidbody and keep data in sync.
    Vector3 newPos = new Vector3(position.X, enemy.GlobalTransform.Origin.Y, position.Z);
    enemy.GlobalPosition = newPos;
    data.Position = new Vector3(position.X, data.Position.Y, position.Z);

    Vector3 normal = cumulativePush.Normalized();
    if (normal.LengthSquared() > 0.000001f)
    {
      data.Velocity = data.Velocity.Slide(normal);
      data.KnockbackVelocity = data.KnockbackVelocity.Slide(normal);
      data.HorizontalVelocity = new Vector3(data.Velocity.X, 0f, data.Velocity.Z);
    }
  }

  private void TryDealContactDamage(Enemy enemy, ref EnemySimData data)
  {
    if (PlayerCache.Count == 0)
      return;

    // Use capsule radius + approximate player radius as contact threshold.
    float contactRadius = MathF.Max(0.2f, data.CapsuleRadius + 0.6f);
    float contactRadiusSq = contactRadius * contactRadius;

    Vector3 enemyPos = data.Position;
    for (int i = 0; i < PlayerCache.Count; i++)
    {
      Node3D node = PlayerCache[i];
      if (node is not Player player || !GodotObject.IsInstanceValid(player))
        continue;

      Vector3 diff = player.GlobalTransform.Origin - enemyPos;
      // Favor mostly horizontal hit to avoid vertical contact during jumps.
      diff.Y *= 0.4f;
      if (diff.LengthSquared() <= contactRadiusSq)
      {
        enemy.TryDamagePlayer(player);
      }
    }
  }

  private void RemoveIndex(int index)
  {
    if (index < 0 || index >= _simData.Count)
      return;

    ref EnemySimData data = ref CollectionsMarshal.AsSpan(_simData)[index];
    if (data.Active)
      _activeSimEnemies.Remove(data.Proxy);

    if (data.Proxy != null)
    {
      data.Proxy.SimulationHandle = -1;
      _indices.Remove(data.Proxy);
    }

    data.Active = false;
    data.Proxy = null!;
    _freeIndices.Push(index);
  }

  private void PruneInvalidEnemies()
  {
    for (int i = 0; i < _simData.Count; i++)
    {
      ref EnemySimData data = ref CollectionsMarshal.AsSpan(_simData)[i];
      if (!data.Active)
        continue;
      Enemy enemy = data.Proxy;
      if (enemy == null || !GodotObject.IsInstanceValid(enemy))
        RemoveIndex(i);
    }

    _activeSimEnemies.RemoveWhere(e => e == null || !GodotObject.IsInstanceValid(e));
  }

  private static void RefreshPlayerCache()
  {
    PlayerCache.Clear();
    PlayerPositions.Clear();

    if (Player.Instance != null && GodotObject.IsInstanceValid(Player.Instance))
    {
      PlayerCache.Add(Player.Instance);
      PlayerPositions.Add(Player.Instance.GlobalTransform.Origin);
    }

    var scenePlayers = Instance?.GetTree().GetNodesInGroup("players");
    if (scenePlayers == null)
      return;

    foreach (var node in scenePlayers)
    {
      if (node is not Node3D n3 || !GodotObject.IsInstanceValid(n3))
        continue;
      if (Player.Instance != null && GodotObject.IsInstanceValid(Player.Instance) && n3 == Player.Instance)
        continue;
      PlayerCache.Add(n3);
      PlayerPositions.Add(n3.GlobalTransform.Origin);
    }
  }

  private static Node3D? NearestPlayer(List<Node3D> players, List<Vector3> positions, Vector3 origin, out float bestDist2)
  {
    Node3D? best = null;
    bestDist2 = float.PositiveInfinity;
    for (int i = 0; i < players.Count; i++)
    {
      Vector3 diff = origin - positions[i];
      float d2 = diff.LengthSquared();
      if (d2 < bestDist2)
      {
        bestDist2 = d2;
        best = players[i];
      }
    }
    return best;
  }

  private static (float radius, float halfHeight) ExtractCapsuleDimensions(Enemy enemy)
  {
    var shape = enemy.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
    if (shape?.Shape is CapsuleShape3D capsule)
    {
      float radius = MathF.Max(0.1f, capsule.Radius);
      float halfHeight = capsule.Height * 0.5f + radius;
      return (radius, halfHeight);
    }

    return (0.5f, 1.0f);
  }

  private static RandomNumberGenerator CreateLodRng()
  {
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    return rng;
  }

  private static Enemy.SimulationState DetermineSimulationState(in EnemySimData data,
    float distanceSquared,
    float activeRadiusSq,
    float midRadiusSq,
    float farRadiusSq,
    float sleepRadiusSq,
    float sleepRadius,
    float farRadius)
  {
    if (float.IsPositiveInfinity(distanceSquared))
      return Enemy.SimulationState.Sleeping;

    if (data.State == Enemy.SimulationState.Sleeping)
    {
      float wakeRadius = MathF.Max(farRadius, sleepRadius - Enemy.LodHysteresis);
      float wakeRadiusSq = wakeRadius * wakeRadius;
      if (distanceSquared > wakeRadiusSq)
        return Enemy.SimulationState.Sleeping;
    }

    if (distanceSquared > sleepRadiusSq)
      return Enemy.SimulationState.Sleeping;
    if (distanceSquared <= activeRadiusSq)
      return Enemy.SimulationState.Active;
    if (distanceSquared <= midRadiusSq)
      return Enemy.SimulationState.BudgetMid;
    if (distanceSquared <= farRadiusSq)
      return Enemy.SimulationState.BudgetFar;
    return Enemy.SimulationState.Sleeping;
  }
}
