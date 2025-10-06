using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Combat;

public partial class Enemy : CharacterBody3D
{
  // Signals
  [Signal]
  public delegate void EnemyDetectedEventHandler(Node3D target);
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
    // Always try to find a valid player target; chase if present
    target = FindNearestPlayer();

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

  private Node3D FindNearestPlayer()
  {
    var players = GetTree().GetNodesInGroup("players");
    Node3D nearest = null;
    float minDist = float.PositiveInfinity;

    foreach (Node player in players)
    {
      if (player is Node3D player3D)
      {
        float dist = GlobalTransform.Origin.DistanceTo(player3D.GlobalTransform.Origin);
        if (dist < minDist)
        {
          minDist = dist;
          nearest = player3D;
        }
      }
    }
    return nearest;
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
    Velocity = Vector3.Zero;
    SetPhysicsProcess(false);

    EmitSignal(nameof(EnemyDied));

    GD.Print("GlobalEvents", GlobalEvents.Instance);
    GlobalEvents.Instance.EmitEnemyDied();

    var weaponModuleCard = new WeaponModuleCard3D();
    weaponModuleCard.Initialize(ItemPool.Instance.SampleModules(1)[0]);
    weaponModuleCard.GlobalTransform = GlobalTransform;

    // GetParent().AddChild(weaponModuleCard);
    // Spawn coins on death (prefer MultiMesh renderer if present)
    int coinCount = Math.Max(0, CoinsOnDeath);
    if (ItemRenderer.Instance != null)
      ItemRenderer.Instance.SpawnCoinsAt(GlobalTransform.Origin, coinCount);
    else
      SpawnCoins(coinCount);

    // Spawn loot from table (e.g., health potion drop)
    LootOnDeath?.SpawnDrops(GlobalTransform.Origin, GetParent());
    QueueFree();
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
}
