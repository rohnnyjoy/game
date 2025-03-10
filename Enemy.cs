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
  private Weapon activeWeapon; // Reference to the actual Weapon instance

  // Onready nodes
  private AnimationPlayer animPlayer;
  private Camera3D camera;
  private WeaponHolder weaponHolder;
  private ProgressBar healthBar;

  public override void _Ready()
  {
	startX = GlobalTransform.Origin.X;
	AddToGroup("enemies");
	SetPhysicsProcess(true);

	// Get child nodes (adjust paths if necessary)
	animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
	healthBar = GetNode<ProgressBar>("HealthBar");
	camera = GetNode<Camera3D>("Camera3D");
	weaponHolder = GetNode<WeaponHolder>("Camera3D/WeaponHolder");

	// Initialize the health bar
	if (healthBar != null)
	{
	  healthBar.MaxValue = health;
	  healthBar.Value = health;
	}

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
	  // First check if the instantiated node is a Weapon
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
	UpdateHealthBarPosition();

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
	// Only attack if cooldown has elapsed
	if (timeSinceLastAttack >= attackCooldown)
	{
	  isFiring = true;
	  timeSinceLastAttack = 0.0f;
	  
	  // Play shoot animation (this is the animation name in your scene)
	  if (animPlayer != null && animPlayer.HasAnimation("shoot"))
	  {
		animPlayer.Play("shoot");
	  }
	  
	  // Fire the weapon
	  FireWeapon();
	}
  }
  
  private void FireWeapon()
  {
	// Log attempt to fire
	GD.Print("Enemy: Attempting to fire weapon");
	
	// Check if we have a direct reference to the weapon
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
	
	// Release weapon trigger if we have a direct reference
	if (activeWeapon != null)
	{
	  activeWeapon.OnRelease();
	}
	
	// Play idle animation
	if (animPlayer != null && animPlayer.HasAnimation("idle"))
	{
	  animPlayer.Play("idle");
	}
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
	
	// Also aim the camera at the target
	if (camera != null)
	{
	  Vector3 cameraDir = (target.GlobalTransform.Origin - camera.GlobalTransform.Origin).Normalized();
	  camera.LookAt(target.GlobalTransform.Origin, Vector3.Up);
	}
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

	// Play move animation
	if (animPlayer != null && animPlayer.HasAnimation("move"))
	{
	  animPlayer.Play("move");
	}
  }

  private void StopAndReset()
  {
	Velocity = Vector3.Zero;
	// Play idle animation
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

	// Play move animation
	if (animPlayer != null && animPlayer.HasAnimation("move"))
	{
	  animPlayer.Play("move");
	}

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
	if (healthBar != null)
	  healthBar.Value = health;

	if (health <= 0)
	{
	  Die();
	}
  }

  private void Die()
  {
	// Stop any ongoing actions.
	Velocity = Vector3.Zero;
	SetPhysicsProcess(false);
	
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

  private void UpdateHealthBarPosition()
  {
	// Get the player camera from the CameraManager instead of using GetViewport().GetCamera3D()
	Camera3D playerCam = null;
	
	if (CameraManager.Instance != null)
	{
	  playerCam = CameraManager.Instance.GetPlayerCamera();
	}
	else
	{
	  // Fallback to viewport camera if CameraManager isn't available
	  playerCam = GetViewport().GetCamera3D();
	}
	
	if (playerCam != null && healthBar != null)
	{
	  Vector3 headWorldPosition = GlobalTransform.Origin + new Vector3(0, 2.0f, 0);
	  Vector2 screenPosition = playerCam.UnprojectPosition(headWorldPosition);
	  screenPosition -= healthBar.GetRect().Size * 0.5f;
	  healthBar.Position = screenPosition;
	}
  }
  
  // This method is connected to the AnimationPlayer's animation_finished signal in your scene
  private void _on_animation_player_animation_finished(StringName animName)
  {
	if (animName == "shoot")
	{
	  // After shoot animation finishes, return to idle if still in attack range
	  // or move if not
	  if (target != null)
	  {
		float distance = GlobalTransform.Origin.DistanceTo(target.GlobalTransform.Origin);
		if (distance <= ATTACK_RADIUS)
		{
		  // Ready for another shot if in range
		  isFiring = false;
		}
		else if (distance <= DETECTION_RADIUS)
		{
		  // Start moving if out of attack range but in detection range
		  isFiring = false;
		  if (animPlayer != null && animPlayer.HasAnimation("move"))
		  {
			animPlayer.Play("move");
		  }
		}
		else
		{
		  // Return to idle if out of detection range
		  isFiring = false;
		  if (animPlayer != null && animPlayer.HasAnimation("idle"))
		  {
			animPlayer.Play("idle");
		  }
		}
	  }
	  else
	  {
		// Return to idle if no target
		isFiring = false;
		if (animPlayer != null && animPlayer.HasAnimation("idle"))
		{
		  animPlayer.Play("idle");
		}
	  }
	}
  }
}
