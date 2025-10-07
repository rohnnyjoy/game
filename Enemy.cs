using Godot;
using System;
using System.Collections.Generic;
using Combat;
using Shared.Effects;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Enemy : CharacterBody3D
{
  public Node3D TargetOverride { get; set; } = null;
  // Signals
  [Signal]
  public delegate void EnemyDiedEventHandler();
  [Signal]
  public delegate void DamagedEventHandler(float amount);

  // Exported variables
  // No exported weapon or UI scenes; enemy just chases player.

  [Export]
  public bool Patrol { get; set; } = true;

  [Export]
  public bool Move { get; set; } = true;

  // Constants
  private const float SPEED = 5.0f;
  private const float MOVE_DISTANCE = 10.0f;
  private const float GRAVITY = 60.0f;

  // Variables
  private float health = 100;
  private Node3D target = null;
  private float startX;
  private int direction = 1;
  private float speedMultiplier = 1.0f;
  private bool _isDying = false;

  [Export(PropertyHint.Range, "0.1,5.0,0.05")] public float DissolveDuration { get; set; } = 0.9f;
  [Export] public Color DissolveBurnColorInner { get; set; } = new Color(0.215686f, 0.258823f, 0.266667f, 1f);
  [Export] public Color DissolveBurnColorOuter { get; set; } = new Color(0.992156f, 0.635294f, 0f, 1f);
  [Export] public Vector2 DissolveSeamOffset { get; set; } = new Vector2(0.5f, 0f);

  [Export]
  public DropTableResource LootOnDeath { get; set; }

  // Knockback state
  private Vector3 _knockbackVelocity = Vector3.Zero;
  [Export] public float KnockbackDamping { get; set; } = 10.0f;
  [Export] public float MaxKnockbackSpeed { get; set; } = 20.0f;

  [Export]
  public int CoinsOnDeath { get; set; } = 2;

  // Shared damage FX helper (flash + impact sprite)
  private DamageFeedback _damageFeedback;
  private readonly List<MeshInstance3D> _dissolveMeshes = new();
  private static Shader _dissolveShader;
  private const string DissolveShaderPath = "res://shared/shaders/dissolve_enemy.gdshader";
  private static readonly Color[] DefaultDissolvePalette =
  {
    new Color(0.215686f, 0.258823f, 0.266667f, 1f),           // BLACK
    new Color(0.992156f, 0.635294f, 0f, 1f),                   // ORANGE
    new Color(0.996078f, 0.372549f, 0.333333f, 1f),            // RED
    new Color(0.917647f, 0.752941f, 0.345098f, 1f),            // GOLD
    new Color(0.74902f, 0.780392f, 0.835294f, 1f)              // JOKER_GREY
  };

  // Contact damage to player
  [Export] public float ContactDamage { get; set; } = 10f;
  [Export] public float ContactKnockbackStrength { get; set; } = 3f;
  [Export] public float ContactDamageCooldown { get; set; } = 0.5f;
  private Area3D _contactArea;
  private readonly Dictionary<long, float> _lastHitAt = new();

  // Onready nodes
  private AnimationPlayer animPlayer;
  // private WeaponHolder weaponHolder;

  public override void _Ready()
  {
    startX = GlobalTransform.Origin.X;
    AddToGroup("enemies");
    SetPhysicsProcess(true);

    // Register with the global AI manager for centralized targeting.
    EnemyAIManager.Instance?.Register(this);

    // Get child nodes (adjust paths if necessary)
    animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

    // Gather mesh instances for damage flash
    _damageFeedback = new DamageFeedback();
    _damageFeedback.VisualRoot = this;
    AddChild(_damageFeedback);

    // Connect local damage signal to shared visual feedback
    Connect(nameof(Damaged), new Callable(this, nameof(OnDamaged)));

    // Setup contact damage detection area
    SetupContactDamageArea();

    _dissolveMeshes.Clear();
    CollectDissolveMeshes(this);

    // Ensure a default loot table if none assigned (5% health potion by default)
    if (LootOnDeath == null)
    {
      var table = new DropTableResource();
      table.RollMode = DropTableResource.DropRollMode.Independent;
      var entry = new DropEntryResource
      {
        Kind = LootKind.HealthPotion,
        DropChance = 0.05f,
        MinQuantity = 1,
        MaxQuantity = 1,
        Weight = 1.0f
      };
      table.Entries.Add(entry);
      LootOnDeath = table;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    // Rely solely on the EnemyAIManager-assigned target.
    target = (TargetOverride != null && IsInstanceValid(TargetOverride)) ? TargetOverride : null;

    if (target != null)
    {
      AimAtTarget();
      MoveTowardsTarget((float)delta);
    }
    else
    {
      PatrolMovement((float)delta);
    }

    ProcessGravity((float)delta);
    // Apply and decay knockback
    Velocity += _knockbackVelocity;
    MoveAndSlide();
    _knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, KnockbackDamping * (float)delta);
  }

  private void ProcessGravity(float delta)
  {
    if (!IsOnFloor())
    {
      Velocity = new Vector3(Velocity.X, Velocity.Y - GRAVITY * delta, Velocity.Z);
      Vector3 floorNormal = GetFloorNormal();
      Vector3 gravityVector = Vector3.Down;
      Vector3 naturalDownhill = (gravityVector - floorNormal * gravityVector.Dot(floorNormal)).Normalized();

      float slopeAngle = (float)Math.Acos(floorNormal.Dot(Vector3.Up));
      float gravityAccel = GRAVITY * (float)Math.Sin(slopeAngle);

      Velocity += naturalDownhill * gravityAccel * delta;
    }
  }

  private void AimAtTarget()
  {
    if (target == null)
      return;

    Vector3 directionVec = (target.GlobalTransform.Origin - GlobalTransform.Origin).Normalized();
    Vector3 lookRotation = new Vector3(directionVec.X, 0, directionVec.Z);
    LookAt(GlobalTransform.Origin + lookRotation, Vector3.Up);
  }

  private void PatrolMovement(float delta)
  {
    if (!Patrol)
      return;

    Velocity = new Vector3(direction * SPEED * speedMultiplier, Velocity.Y, 0);

    if (GlobalTransform.Origin.X >= startX + MOVE_DISTANCE)
    {
      direction = -1;
    }
    else if (GlobalTransform.Origin.X <= startX - MOVE_DISTANCE)
    {
      direction = 1;
    }

    if (animPlayer != null && animPlayer.HasAnimation("move"))
    {
      animPlayer.Play("move");
    }
  }

  private void StopAndReset()
  {
    Velocity = Vector3.Zero;
    if (animPlayer != null && animPlayer.HasAnimation("idle"))
    {
      animPlayer.Play("idle");
    }
  }



  private void MoveTowardsTarget(float delta)
  {
    if (!Move || target == null)
      return;

    if (animPlayer != null && animPlayer.HasAnimation("move"))
    {
      animPlayer.Play("move");
    }

    // Direct straight-line chase toward player
    Vector3 targetPos = target.GlobalTransform.Origin;
    Vector3 desired = (targetPos - GlobalTransform.Origin);

    desired.Y = 0;
    if (desired.Length() > 0.001f)
    {
      desired = desired.Normalized();
    }

    Velocity = desired * SPEED * speedMultiplier;
  }

  public void SetSpeedMultiplier(float multiplier)
  {
    speedMultiplier = multiplier;
  }

  public void TakeDamage(float amount)
  {
    health -= amount;

    // Emit a Damaged signal for any listeners (e.g., visual FX)
    EmitSignal(nameof(Damaged), amount);

    if (health <= 0)
    {
      Die();
    }
  }




  private void Die()
  {
    if (_isDying)
      return;
    _isDying = true;

    Velocity = Vector3.Zero;
    SetPhysicsProcess(false);
    SetProcess(false);

    CollisionLayer = 0;
    CollisionMask = 0;
    DisableCollisionShapes();
    if (_contactArea != null)
    {
      _contactArea.Monitoring = false;
      _contactArea.Monitorable = false;
      _contactArea.CollisionLayer = 0;
      _contactArea.CollisionMask = 0;
    }

    if (_damageFeedback != null)
    {
      _damageFeedback.QueueFree();
      _damageFeedback = null;
    }

    EmitSignal(nameof(EnemyDied));
    GlobalEvents.Instance.EmitEnemyDied();
    // Spawn coins on death (prefer MultiMesh renderer if present)
    int coinCount = Math.Max(0, CoinsOnDeath);
    if (ItemRenderer.Instance != null)
      ItemRenderer.Instance.SpawnCoinsAt(GlobalTransform.Origin, coinCount);
    else
      SpawnCoins(coinCount);

    // Spawn loot from table (e.g., health potion drop)
    LootOnDeath?.SpawnDrops(GlobalTransform.Origin, GetParent());

    SpawnDissolveParticles();

    _ = RunDeathDissolveAsync();
  }

  private void SpawnCoins(int count)
  {
    var parent = GetParent();
    if (parent == null)
      return;

    var rng = new RandomNumberGenerator();
    rng.Randomize();

    for (int i = 0; i < count; i++)
    {
      var coin = new Coin();
      Vector3 offset = new Vector3(
        rng.RandfRange(-0.6f, 0.6f),
        rng.RandfRange(0.2f, 0.6f),
        rng.RandfRange(-0.6f, 0.6f)
      );
      coin.GlobalTransform = new Transform3D(Basis.Identity, GlobalTransform.Origin + offset);
      parent.AddChild(coin);
    }
  }

  public override void _ExitTree()
  {
    EnemyAIManager.Instance?.Unregister(this);
    base._ExitTree();
  }

  private void _on_animation_player_animation_finished(StringName animName)
  {
    // No shooting logic; keep animations simple
    if (animName == "move" && animPlayer != null && animPlayer.HasAnimation("idle"))
      animPlayer.Play("idle");
  }

  private void OnDamaged(float amount)
  {
    _damageFeedback?.Trigger(amount);
  }

  public void ApplyKnockback(Vector3 impulse)
  {
    _knockbackVelocity += impulse;
    float len = _knockbackVelocity.Length();
    if (len > MaxKnockbackSpeed && len > 0.0001f)
      _knockbackVelocity = _knockbackVelocity / len * MaxKnockbackSpeed;
  }

  private void SetupContactDamageArea()
  {
    _contactArea = new Area3D
    {
      Monitoring = true,
      Monitorable = true,
      // Sense only the player layer (layer 2 in Player.tscn)
      CollisionMask = (uint)(1 << 1)
    };
    AddChild(_contactArea);

    // Try to mirror the main collider's shape for more accurate contact.
    var bodyShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
    if (bodyShape?.Shape is CapsuleShape3D cap)
    {
      var capCopy = new CapsuleShape3D { Radius = cap.Radius + 0.05f, Height = cap.Height };
      var cs = new CollisionShape3D { Shape = capCopy };
      _contactArea.AddChild(cs);
    }
    else
    {
      // Fallback small sphere around the body center.
      var sphere = new SphereShape3D { Radius = 0.7f };
      var cs = new CollisionShape3D { Shape = sphere };
      _contactArea.AddChild(cs);
    }

    _contactArea.BodyEntered += OnContactBodyEntered;
  }

  private void OnContactBodyEntered(Node3D body)
  {
    if (body == null || !IsInstanceValid(body)) return;
    if (!body.IsInGroup("players")) return;
    if (body is Player player)
      TryDamagePlayer(player);
  }

  private void TryDamagePlayer(Player player)
  {
    if (player == null || !IsInstanceValid(player)) return;
    long id = (long)player.GetInstanceId();
    float now = (float)Time.GetTicksMsec() / 1000f;
    if (_lastHitAt.TryGetValue(id, out float last) && (now - last) < ContactDamageCooldown)
      return;

    _lastHitAt[id] = now;

    // Apply damage and knockback
    player.TakeDamage(ContactDamage);

    Vector3 dir = (player.GlobalTransform.Origin - GlobalTransform.Origin);
    dir.Y = 0.1f; // keep mostly horizontal to avoid excessive pop
    if (dir.LengthSquared() < 0.0001f)
      dir = Vector3.Forward;
    dir = dir.Normalized();

    GlobalEvents.Instance?.EmitDamageDealt(player, ContactDamage, dir * MathF.Max(0f, ContactKnockbackStrength));

    // Spawn a contact impact sprite on the player's surface with a normal opposing the hit direction
    Vector3 normal = (-dir).Normalized();
    Vector3 hitPos = player.GlobalTransform.Origin + Vector3.Up * 0.5f;
    ImpactSprite.Spawn(this, hitPos, normal);
  }

  private void SpawnDissolveParticles()
  {
    var parent = GetParent();
    if (parent == null || !IsInstanceValid(parent))
      return;

    var palette = new List<Color>(DefaultDissolvePalette);
    if (DissolveBurnColorInner.A > 0.01f)
      palette.Insert(0, DissolveBurnColorInner);
    if (DissolveBurnColorOuter.A > 0.01f)
      palette.Insert(0, DissolveBurnColorOuter);

    var (center, halfExtents) = ComputeDissolveBounds();
    var spawnXform = GlobalTransform;
    spawnXform.Origin += spawnXform.Basis * center;

    DissolveBurst.Spawn(parent, spawnXform, palette, halfExtents);
  }

  private (Vector3 center, Vector3 halfExtents) ComputeDissolveBounds()
  {
    if (_dissolveMeshes.Count == 0)
      return (Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f));

    Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    foreach (var mesh in _dissolveMeshes)
    {
      if (mesh == null || !IsInstanceValid(mesh)) continue;
      Aabb aabb = mesh.GetAabb();
      for (int ix = 0; ix <= 1; ix++)
      {
        for (int iy = 0; iy <= 1; iy++)
        {
          for (int iz = 0; iz <= 1; iz++)
          {
            Vector3 corner = aabb.Position + new Vector3(aabb.Size.X * ix, aabb.Size.Y * iy, aabb.Size.Z * iz);
            corner = mesh.Transform * corner;
            min = new Vector3(MathF.Min(min.X, corner.X), MathF.Min(min.Y, corner.Y), MathF.Min(min.Z, corner.Z));
            max = new Vector3(MathF.Max(max.X, corner.X), MathF.Max(max.Y, corner.Y), MathF.Max(max.Z, corner.Z));
          }
        }
      }
    }

    if (!IsFinite(min) || !IsFinite(max))
      return (Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f));

    Vector3 center = (min + max) * 0.5f;
    Vector3 halfExtents = (max - min) * 0.5f;
    halfExtents.X = MathF.Max(halfExtents.X, 0.2f);
    halfExtents.Y = MathF.Max(halfExtents.Y, 0.2f);
    halfExtents.Z = MathF.Max(halfExtents.Z, 0.2f);
    return (center, halfExtents);
  }

  private static bool IsFinite(Vector3 v)
  {
    return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
  }

  private void CollectDissolveMeshes(Node node)
  {
    foreach (Node child in node.GetChildren())
    {
      if (child == _damageFeedback)
        continue;

      if (child is MeshInstance3D mesh)
      {
        if (!_dissolveMeshes.Contains(mesh))
          _dissolveMeshes.Add(mesh);
      }

      CollectDissolveMeshes(child);
    }
  }

  private void DisableCollisionShapes()
  {
    DisableCollisionShapesRecursive(this);
  }

  private void DisableCollisionShapesRecursive(Node node)
  {
    foreach (Node child in node.GetChildren())
    {
      if (child is CollisionShape3D cs)
        cs.Disabled = true;

      if (child is CollisionObject3D body)
      {
        body.CollisionLayer = 0;
        body.CollisionMask = 0;
        if (child is Area3D area)
        {
          area.Monitorable = false;
          area.Monitoring = false;
        }
      }

      DisableCollisionShapesRecursive(child);
    }
  }

  private async Task RunDeathDissolveAsync()
  {
    var materials = SetupDissolveMaterials();
    if (materials.Count == 0)
    {
      QueueFree();
      return;
    }

    float duration = MathF.Max(0.1f, DissolveDuration);
    var tween = CreateTween();
    foreach (var mat in materials)
    {
      tween.Parallel().TweenProperty(mat, "shader_parameter/dissolve", 1.0f, duration)
        .SetTrans(Tween.TransitionType.Cubic)
        .SetEase(Tween.EaseType.In);
    }

    await ToSignal(tween, Tween.SignalName.Finished);
    QueueFree();
  }

  private List<ShaderMaterial> SetupDissolveMaterials()
  {
    var result = new List<ShaderMaterial>();
    if (_dissolveMeshes.Count == 0)
      return result;

    _dissolveShader ??= ResourceLoader.Load<Shader>(DissolveShaderPath);
    if (_dissolveShader == null)
    {
      GD.PushError($"Failed to load dissolve shader at {DissolveShaderPath}");
      return result;
    }

    foreach (var mesh in _dissolveMeshes)
    {
      if (mesh == null || !IsInstanceValid(mesh))
        continue;

      Material originalOverride = mesh.MaterialOverride;
      mesh.MaterialOverride = null;

      var meshResource = mesh.Mesh;
      int surfaceCount = meshResource?.GetSurfaceCount() ?? 0;

      if (surfaceCount == 0)
      {
        var mat = CreateDissolveMaterial(originalOverride);
        if (mat != null)
        {
          mesh.MaterialOverride = mat;
          result.Add(mat);
        }
        continue;
      }

      for (int i = 0; i < surfaceCount; i++)
      {
        Material source = mesh.GetSurfaceOverrideMaterial(i);
        if (source == null && meshResource != null)
          source = meshResource.SurfaceGetMaterial(i);

        var shaderMat = CreateDissolveMaterial(source);
        if (shaderMat != null)
        {
          mesh.SetSurfaceOverrideMaterial(i, shaderMat);
          result.Add(shaderMat);
        }
      }
    }

    return result;
  }

  private ShaderMaterial CreateDissolveMaterial(Material source)
  {
    var mat = new ShaderMaterial
    {
      Shader = _dissolveShader
    };

    Color baseColor = Colors.White;
    float alpha = 1f;
    bool hasTexture = false;

    if (source is BaseMaterial3D baseMat)
    {
      baseColor = baseMat.AlbedoColor;
      alpha = Mathf.Clamp(baseColor.A, 0f, 1f);
      if (baseMat.AlbedoTexture is Texture2D tex)
      {
        hasTexture = true;
        mat.SetShaderParameter("albedo_texture", tex);
        float w = tex.GetWidth();
        float h = tex.GetHeight();
        mat.SetShaderParameter("texture_details", new Vector4(0f, 0f, w, h));
        mat.SetShaderParameter("image_details", new Vector2(w, h));
      }
    }
    if (!hasTexture)
    {
      mat.SetShaderParameter("texture_details", new Vector4(0f, 0f, 256f, 256f));
      mat.SetShaderParameter("image_details", new Vector2(256f, 256f));
    }
    mat.SetShaderParameter("base_color", new Color(baseColor.R, baseColor.G, baseColor.B, alpha));
    mat.SetShaderParameter("use_albedo_texture", hasTexture);
    mat.SetShaderParameter("burn_color_1", DissolveBurnColorInner);
    mat.SetShaderParameter("burn_color_2", DissolveBurnColorOuter);
    mat.SetShaderParameter("dissolve", 0.0f);
    mat.SetShaderParameter("edge_softness", 0f);
    mat.SetShaderParameter("seam_offset", DissolveSeamOffset);

    return mat;
  }
}
