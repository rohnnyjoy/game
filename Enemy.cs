using Godot;
using System;

public partial class Enemy : CharacterBody3D
{
  // Signals
  [Signal]
  public delegate void EnemyDetectedEventHandler(Node3D target);
  [Signal]
  public delegate void EnemyDiedEventHandler();

  // Exported variables
  [Export]
  public PackedScene PistolScene { get; set; }

  [Export]
  public bool Patrol { get; set; } = true;

  [Export]
  public bool Move { get; set; } = true;

  // Constants
  private const float SPEED = 5.0f;
  private const float DETECTION_RADIUS = 20.0f;
  private const float ATTACK_RADIUS = 10.0f;
  private const float MOVE_DISTANCE = 10.0f;
  private const float GRAVITY = 60.0f;

  // Variables
  private float health = 100;
  private Node3D target = null;
  private float startX;
  private int direction = 1;
  private float attackCooldown = 0.5f;
  private float timeSinceLastAttack = 0.0f;
  private bool isFiring = false;
  private float speedMultiplier = 1.0f;

  // Onready nodes
  private AnimationPlayer animPlayer;
  private Camera3D camera;

  public override void _Ready()
  {
    startX = GlobalTransform.Origin.X;
    AddToGroup("enemies");
    SetPhysicsProcess(true);

    // Get child nodes (adjust paths if necessary)
    animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
    camera = GetNode<Camera3D>("Camera3D");
  }

  public override void _Process(double delta)
  {
    if (timeSinceLastAttack < attackCooldown)
    {
      timeSinceLastAttack += (float)delta;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    if (target == null)
    {
      target = FindNearestPlayer();
      if (target != null)
      {
        EmitSignal(nameof(EnemyDetectedEventHandler), target);
      }
    }

    // Uncomment and implement target-related behavior as needed:
    /*
    if (target != null)
    {
        AimAtTarget();
        float distance = GlobalTransform.Origin.DistanceTo(target.GlobalTransform.origin);
        if (distance <= ATTACK_RADIUS)
        {
            // _attack_target();
        }
        else if (isFiring)
        {
            // _stop_firing();
        }
        else if (distance <= DETECTION_RADIUS)
        {
            MoveTowardsTarget((float)delta);
        }
        else
        {
            StopAndReset();
        }
    }
    else
    {
        PatrolMovement((float)delta);
    }
    */

    ProcessGravity((float)delta);
    MoveAndSlide();
  }

  private void ProcessGravity(float delta)
  {
    if (!IsOnFloor())
    {
      // Apply simple gravity
      Velocity = new Vector3(Velocity.X, Velocity.Y - GRAVITY * delta, Velocity.Z);

      Vector3 floorNormal = GetFloorNormal();
      Vector3 gravityVector = Vector3.Down;
      Vector3 naturalDownhill = (gravityVector - floorNormal * gravityVector.Dot(floorNormal)).Normalized();

      float slopeAngle = (float)Math.Acos(floorNormal.Dot(Vector3.Up));
      float gravityAccel = GRAVITY * (float)Math.Sin(slopeAngle);

      Velocity += naturalDownhill * gravityAccel * delta;
      Velocity = new Vector3(Mathf.MoveToward(Velocity.X, 0, delta),
                             Velocity.Y,
                             Mathf.MoveToward(Velocity.Z, 0, delta));
    }
  }

  private void AimAtTarget()
  {
    if (target == null)
      return;

    Vector3 directionVec = (target.GlobalTransform.Origin - GlobalTransform.Origin).Normalized();
    // Ignore Y-axis for aiming.
    Vector3 lookRotation = new Vector3(directionVec.X, 0, directionVec.Z);
    LookAt(GlobalTransform.Origin + lookRotation, Vector3.Up);
  }

  private void PatrolMovement(float delta)
  {
    if (!Patrol)
      return;

    // Move along the X axis; Z velocity is set to zero.
    Velocity = new Vector3(direction * SPEED * speedMultiplier, Velocity.Y, 0);

    if (GlobalTransform.Origin.X >= startX + MOVE_DISTANCE)
    {
      direction = -1;
    }
    else if (GlobalTransform.Origin.X <= startX - MOVE_DISTANCE)
    {
      direction = 1;
    }

    // Optionally, play move animation:
    // animPlayer.Play("move");
  }

  private void StopAndReset()
  {
    Velocity = Vector3.Zero;
    // Optionally, play idle animation:
    // animPlayer.Play("idle");
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
        if (dist < minDist && dist <= DETECTION_RADIUS)
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

    // Optionally, play move animation:
    // animPlayer.Play("move");

    Vector3 moveDirection = (target.GlobalTransform.Origin - GlobalTransform.Origin).Normalized();
    // Keep enemy on the ground plane
    moveDirection.Y = 0;
    moveDirection = moveDirection.Normalized();

    Velocity = moveDirection * SPEED * speedMultiplier;
  }

  public void SetSpeedMultiplier(float multiplier)
  {
    speedMultiplier = multiplier;
  }

  public void TakeDamage(float amount)
  {
    health -= amount;

    if (health <= 0)
    {
      Die();
    }
    else
    {
      // Optional: play a hit animation or sound.
    }
  }

  private void Die()
  {
    // Stop any ongoing actions.
    Velocity = Vector3.Zero;
    SetPhysicsProcess(false);

    // Optionally, play a death animation here.
    EmitSignal(nameof(EnemyDiedEventHandler));

    // Emit global enemy died event via GlobalEvents.
    GD.Print("GlobalEvents", GlobalEvents.Instance);
    GlobalEvents.Instance.EmitEnemyDied();

    // Spawn the weapon module card.
    var weaponModuleCard = new WeaponModuleCard3D();
    weaponModuleCard.Initialize(ItemPool.Instance.SampleModules(1)[0]);
    weaponModuleCard.GlobalTransform = GlobalTransform;

    // Add the card to the scene tree.
    GetParent().AddChild(weaponModuleCard);

    // Remove the enemy.
    QueueFree();
  }
}
