using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
  
  // Knockback state
  private Vector3 _knockbackVelocity = Vector3.Zero;
  [Export] public float KnockbackDamping { get; set; } = 10.0f;
  [Export] public float MaxKnockbackSpeed { get; set; } = 20.0f;
  
  [Export]
  public int CoinsOnDeath { get; set; } = 2;
  
  // Flash-on-damage state
  private readonly List<MeshInstance3D> _meshInstances = new List<MeshInstance3D>();
  private StandardMaterial3D _flashMaterial;
  private int _flashToken = 0;

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
    CollectMeshInstances(this);
    // Prepare a simple white unshaded material to overlay as a flash
    _flashMaterial = new StandardMaterial3D
    {
      AlbedoColor = new Color(1, 1, 1, 1),
      EmissionEnabled = true,
      Emission = new Color(1, 1, 1, 1)
    };

    // Connect local damage signal to visual feedback
    Connect(nameof(Damaged), new Callable(this, nameof(OnDamaged)));
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
    if (CoinRenderer.Instance != null)
      CoinRenderer.Instance.SpawnCoinsAt(GlobalTransform.Origin, coinCount);
    else
      SpawnCoins(coinCount);
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

  private void CollectMeshInstances(Node node)
  {
    foreach (Node child in node.GetChildren())
    {
      if (child is MeshInstance3D mi)
      {
        _meshInstances.Add(mi);
      }
      // Recurse into Node3D children (typical for visual hierarchies)
      if (child is Node3D || child is Node)
      {
        CollectMeshInstances(child);
      }
    }
  }

  private void OnDamaged(float amount)
  {
    // Fire-and-forget the flash
    _ = FlashAsync(0.08f);
  }

  private async Task FlashAsync(float durationSeconds)
  {
    _flashToken++;
    int token = _flashToken;

    foreach (var mi in _meshInstances)
    {
      // Apply a white flash overlay using MaterialOverride.
      // Clearing MaterialOverride later restores any per-surface materials.
      mi.MaterialOverride = _flashMaterial;
    }

    var timer = GetTree().CreateTimer(Mathf.Max(0.01f, durationSeconds));
    await ToSignal(timer, "timeout");

    // Only the most recent flash call should clear the override
    if (token == _flashToken)
    {
      foreach (var mi in _meshInstances)
      {
        mi.MaterialOverride = null;
      }
    }
  }

  public void ApplyKnockback(Vector3 impulse)
  {
    _knockbackVelocity += impulse;
    float len = _knockbackVelocity.Length();
    if (len > MaxKnockbackSpeed && len > 0.0001f)
      _knockbackVelocity = _knockbackVelocity / len * MaxKnockbackSpeed;
  }
}
