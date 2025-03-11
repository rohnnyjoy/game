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

  // Export a new scene for damage numbers.
  [Export]
  public PackedScene DamageNumberScene { get; set; }

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
  private Weapon activeWeapon; // Reference to the actual Weapon instance

  // Onready nodes
  private AnimationPlayer animPlayer;
  private Camera3D camera;
  private WeaponHolder weaponHolder;

  public override void _Ready()
  {
    startX = GlobalTransform.Origin.X;
    AddToGroup("enemies");
    SetPhysicsProcess(true);

    // Get child nodes (adjust paths if necessary)
    animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
    camera = GetNode<Camera3D>("Camera3D");
    weaponHolder = GetNode<WeaponHolder>("Camera3D/WeaponHolder");

    // Ensure enemy camera never becomes the current camera
    if (camera != null)
    {
      camera.Current = false;
    }

    // Initialize weapon if PistolScene is set
    InitializeWeapon();
  }

  private void InitializeWeapon()
  {
    if (PistolScene == null || weaponHolder == null)
    {
      GD.PrintErr("Enemy: Missing PistolScene or WeaponHolder");
      return;
    }

    // Cleanup any existing weapon children first
    foreach (Node child in weaponHolder.GetChildren())
    {
      child.QueueFree();
    }

    try
    {
      // Instantiate the weapon
      Node weaponNode = PistolScene.Instantiate();

      // Add it to the weapon holder
      weaponHolder.AddChild(weaponNode);

      // Find the Weapon component - could be the node itself or a child
      if (weaponNode is Weapon weapon)
      {
        activeWeapon = weapon;
        GD.Print("Enemy: Successfully obtained Weapon reference (direct)");
      }
    }
    catch (Exception e)
    {
      GD.PrintErr($"Enemy: Error initializing weapon: {e.Message}");
    }
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

    if (target != null)
    {
      AimAtTarget();
      float distance = GlobalTransform.Origin.DistanceTo(target.GlobalTransform.Origin);
      if (distance <= ATTACK_RADIUS)
      {
        AttackTarget();
      }
      else if (isFiring)
      {
        StopFiring();
      }
      else if (distance <= DETECTION_RADIUS)
      {
        MoveTowardsTarget((float)delta);
      }
      else
      {
        target = null; // Lost target
        StopAndReset();
      }
    }
    else
    {
      PatrolMovement((float)delta);
    }

    ProcessGravity((float)delta);
    MoveAndSlide();
  }

  private void AttackTarget()
  {
    if (timeSinceLastAttack >= attackCooldown)
    {
      isFiring = true;
      timeSinceLastAttack = 0.0f;

      if (animPlayer != null && animPlayer.HasAnimation("shoot"))
      {
        animPlayer.Play("shoot");
      }
      FireWeapon();
    }
  }

  private void FireWeapon()
  {
    GD.Print("Enemy: Attempting to fire weapon");
    if (activeWeapon != null)
    {
      GD.Print($"Enemy: Firing using activeWeapon ({activeWeapon.Name})");
      activeWeapon.OnPress();
      return;
    }
    GD.PrintErr("Enemy: No weapon found to fire");
  }

  private void StopFiring()
  {
    isFiring = false;
    if (activeWeapon != null)
    {
      activeWeapon.OnRelease();
    }
    if (animPlayer != null && animPlayer.HasAnimation("idle"))
    {
      animPlayer.Play("idle");
    }
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
    Vector3 lookRotation = new Vector3(directionVec.X, 0, directionVec.Z);
    LookAt(GlobalTransform.Origin + lookRotation, Vector3.Up);

    if (camera != null)
    {
      camera.LookAt(target.GlobalTransform.Origin, Vector3.Up);
    }
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

    if (animPlayer != null && animPlayer.HasAnimation("move"))
    {
      animPlayer.Play("move");
    }

    Vector3 moveDirection = (target.GlobalTransform.Origin - GlobalTransform.Origin).Normalized();
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

    // Show a floating damage number
    ShowDamageNumber(amount);

    if (health <= 0)
    {
      Die();
    }
  }

  // New method to instantiate and show damage numbers.
  private void ShowDamageNumber(float damage)
  {
    var font = ResourceLoader.Load<Font>("res://fonts/Pixel.ttf");
    Label3D damageLabel = new Label3D();
    damageLabel.Text = Math.Round(damage).ToString();
    damageLabel.Modulate = Colors.White;
    damageLabel.OutlineSize = 1;
    damageLabel.Font = font;

    // Set the label to billboard mode with fixed size.
    // This ensures the label always faces the camera and its size remains constant.
    damageLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
    damageLabel.FixedSize = true;
    damageLabel.FontSize = 24;

    // Set the world position (with an upward offset so it appears above the enemy).
    Vector3 offset = new Vector3(0, 2, 0);
    damageLabel.GlobalTransform = new Transform3D(damageLabel.GlobalTransform.Basis, GlobalTransform.Origin + offset);

    // Add the label to the enemy's parent (or another appropriate 3D container).
    GetParent().AddChild(damageLabel);

    // Create a Tween to animate the label.
    Tween tween = damageLabel.CreateTween();

    Vector3 startPos = damageLabel.GlobalTransform.Origin;
    Vector3 endPos = startPos + new Vector3(0, 1, 0);
    tween.TweenProperty(damageLabel, "global_position", endPos, 1.0f)
         .SetTrans(Tween.TransitionType.Linear)
         .SetEase(Tween.EaseType.InOut);

    // Animate fade out: change the modulate alpha from 1 to 0 over 1 second.
    Color startColor = damageLabel.Modulate;
    Color endColor = new Color(startColor.R, startColor.G, startColor.B, 0);
    tween.TweenProperty(damageLabel, "modulate", endColor, 1.0f)
         .SetTrans(Tween.TransitionType.Linear)
         .SetEase(Tween.EaseType.InOut);

    // Once the tween finishes, remove the damage label.
    tween.Finished += () => damageLabel.QueueFree();
    damageLabel.Scale = new Vector3(0.3f, 0.3f, 0.3f);

  }




  private void Die()
  {
    Velocity = Vector3.Zero;
    SetPhysicsProcess(false);

    EmitSignal(nameof(EnemyDiedEventHandler));

    GD.Print("GlobalEvents", GlobalEvents.Instance);
    GlobalEvents.Instance.EmitEnemyDied();

    var weaponModuleCard = new WeaponModuleCard3D();
    weaponModuleCard.Initialize(ItemPool.Instance.SampleModules(1)[0]);
    weaponModuleCard.GlobalTransform = GlobalTransform;

    GetParent().AddChild(weaponModuleCard);
    QueueFree();
  }

  private void _on_animation_player_animation_finished(StringName animName)
  {
    if (animName == "shoot")
    {
      if (target != null)
      {
        float distance = GlobalTransform.Origin.DistanceTo(target.GlobalTransform.Origin);
        if (distance <= ATTACK_RADIUS)
        {
          isFiring = false;
        }
        else if (distance <= DETECTION_RADIUS)
        {
          isFiring = false;
          if (animPlayer != null && animPlayer.HasAnimation("move"))
          {
            animPlayer.Play("move");
          }
        }
        else
        {
          isFiring = false;
          if (animPlayer != null && animPlayer.HasAnimation("idle"))
          {
            animPlayer.Play("idle");
          }
        }
      }
      else
      {
        isFiring = false;
        if (animPlayer != null && animPlayer.HasAnimation("idle"))
        {
          animPlayer.Play("idle");
        }
      }
    }
  }
}
