using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
  [Signal]
  public delegate void HealthChangedEventHandler(int healthValue);

  // Constants
  private const float INTERACT_RADIUS = 2.0f;
  private const float SPEED = 10.0f;
  private const float JUMP_VELOCITY = 14.0f;
  private const float GROUND_ACCEL = 80.0f;
  private const float GROUND_DECEL = 150.0f;
  private const float INITIAL_BOOST_FACTOR = 0.8f;
  private const float AIR_ACCEL = 8.0f;
  private const float JUMP_BUFFER_TIME = 0.2f;
  private const int MAX_JUMPS = 2;
  private const float GRAVITY = 60.0f;
  private const float DASH_SPEED = 20.0f;

  // Sliding settings
  private const float SLIDE_COLLISION_SPEED_FACTOR = 0.7f;
  private const float SLIDE_FRICTION_COEFFICIENT = 15.0f;

  private Camera3D camera;
  private AnimationPlayer animPlayer;
  private Node3D muzzleFlash;
  private RayCast3D raycast;

  private int health = 3;
  private float jumpBufferTimer = 0.0f;
  private int jumpsRemaining = MAX_JUMPS;

  private AirLurchManager airLurchManager = null;
  private bool isSliding = false;
  private Weapon currentWeapon = null;
  private Vector3 preSlideHorizontalVelocity = Vector3.Zero;
  private IInteractable nearbyInteractable = null;
  private GameUI gameUI;


  public override void _Ready()
  {
    AddToGroup("players");

    if (!IsMultiplayerAuthority()) return;

    SetupInput();
    Input.MouseMode = Input.MouseModeEnum.Captured;

    camera = GetNode<Camera3D>("Camera3D");
    animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
    muzzleFlash = GetNode<Node3D>("Camera3D/Pistol/MuzzleFlash");
    raycast = GetNode<RayCast3D>("Camera3D/RayCast3D");
    gameUI = GetTree().Root.FindChild("GameUI", true, false) as GameUI;

    camera.Current = true;
    GD.Print(GetTree().Root.HasNode("InventorySingleton") ? "Singleton Exists!" : "Singleton Not Found!");
    EquipDefaultWeapon();
  }

  private void SetupInput()
  {
    if (!InputMap.HasAction("dash"))
    {
      InputMap.AddAction("dash");
      var ev = new InputEventKey { Keycode = Key.Shift };
      InputMap.ActionAddEvent("dash", ev);
    }
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (!IsMultiplayerAuthority()) return;

    HandleCameraRotation(@event);
    HandleShooting(@event);
    HandleDash(@event);

    if (nearbyInteractable != null && Input.IsActionJustPressed("interact"))
    {
      nearbyInteractable.OnInteract();
    }
  }

  private void HandleCameraRotation(InputEvent @event)
  {
    if (@event is InputEventMouseMotion mouseMotion)
    {
      RotateY(-mouseMotion.Relative.X * 0.005f);
      camera.RotateX(-mouseMotion.Relative.Y * 0.005f);
      camera.Rotation = new Vector3(Mathf.Clamp(camera.Rotation.X, -Mathf.Pi / 2, Mathf.Pi / 2), camera.Rotation.Y, camera.Rotation.Z);
    }
  }

  private void HandleShooting(InputEvent @event)
  {
    if (Input.IsActionJustPressed("shoot"))
      currentWeapon?.OnPress();
    else if (Input.IsActionJustReleased("shoot"))
      currentWeapon?.OnRelease();
  }

  private void HandleDash(InputEvent @event)
  {
    if (Input.IsActionJustPressed("dash"))
    {
      var newVelocity = GetInputDirection() * DASH_SPEED;
      newVelocity.Y = Velocity.Y;
      Velocity = newVelocity;
    }
  }

  private Vector3 GetInputDirection()
  {
    Vector2 rawInput = Input.GetVector("left", "right", "up", "down");
    return (Transform.Basis * new Vector3(rawInput.X, 0, rawInput.Y)).Normalized();
  }

  public override void _PhysicsProcess(double delta)
  {
    if (!IsMultiplayerAuthority()) return;

    ProcessJumpAndGravity((float)delta);
    Vector3 inputDirection = GetInputDirection();

    if (IsOnFloor())
      ProcessGroundMovement(inputDirection, (float)delta);
    else
      ProcessAirMovement(inputDirection, (float)delta);

    preSlideHorizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);
    MoveAndSlide();

    if (isSliding)
      ProcessSlideCollisionsPost();
  }

  private void ProcessJumpAndGravity(float delta)
  {
    jumpBufferTimer = Mathf.Max(jumpBufferTimer - delta, 0);
    if (Input.IsActionJustPressed("ui_accept"))
      jumpBufferTimer = JUMP_BUFFER_TIME;

    if (IsOnFloor())
      jumpsRemaining = MAX_JUMPS;

    if (jumpBufferTimer > 0 && jumpsRemaining > 0)
    {
      Velocity = new Vector3(Velocity.X, JUMP_VELOCITY, Velocity.Z);
      jumpBufferTimer = 0;
      jumpsRemaining--;
    }

    if (!IsOnFloor())
      Velocity -= new Vector3(0, GRAVITY * delta, 0);
  }

  private void ProcessGroundMovement(Vector3 inputDirection, float delta)
  {
    // Reset air lurch manager on landing.
    airLurchManager = null;

    // Cancel sliding when jumping to allow a proper jump.
    if (Input.IsActionJustPressed("ui_accept"))
    {
      isSliding = false;
      ProcessStandardGroundMovement(inputDirection, delta);
    }
    else if (Input.IsActionPressed("dash"))
    {
      ProcessSlide(delta);
    }
    else
    {
      // If not sliding, ensure standard movement is processed.
      if (isSliding)
      {
        isSliding = false;
      }
      ProcessStandardGroundMovement(inputDirection, delta);
    }
  }

  private void ProcessStandardGroundMovement(Vector3 inputDirection, float delta)
  {
    // Process movement input for non-sliding movement.
    if (inputDirection != Vector3.Zero)
    {
      Vector3 currentHorizontal = new Vector3(Velocity.X, 0, Velocity.Z);

      if (currentHorizontal.Length() < 0.1f)
      {
        // Give an initial boost.
        Velocity = new Vector3(
            inputDirection.X * SPEED * INITIAL_BOOST_FACTOR,
            Velocity.Y,
            inputDirection.Z * SPEED * INITIAL_BOOST_FACTOR
        );
      }
      else
      {
        Velocity = new Vector3(
            Mathf.MoveToward(Velocity.X, inputDirection.X * SPEED, GROUND_ACCEL * delta),
            Velocity.Y,
            Mathf.MoveToward(Velocity.Z, inputDirection.Z * SPEED, GROUND_ACCEL * delta)
        );
      }

      animPlayer.Play("move");
    }
    else
    {
      Velocity = new Vector3(
          Mathf.MoveToward(Velocity.X, 0, GROUND_DECEL * delta),
          Velocity.Y,
          Mathf.MoveToward(Velocity.Z, 0, GROUND_DECEL * delta)
      );

      animPlayer.Play("idle");
    }
  }

  private void ProcessSlide(float delta)
  {
    if (!isSliding)
    {
      isSliding = true;
      animPlayer.Play("slide");
    }

    Vector3 floorNormal = GetFloorNormal();
    Vector3 gravityVector = Vector3.Down;

    // Compute the natural downhill direction
    Vector3 naturalDownhill = (gravityVector - floorNormal * gravityVector.Dot(floorNormal)).Normalized();

    // Compute slope angle
    float slopeAngle = Mathf.Acos(floorNormal.Dot(Vector3.Up));

    // Compute gravity acceleration along the slope
    float gravityAccel = GRAVITY * Mathf.Sin(slopeAngle);

    // Apply sliding motion
    Velocity += naturalDownhill * gravityAccel * delta;

    // Apply sliding friction to reduce velocity over time
    Velocity = new Vector3(
        Mathf.MoveToward(Velocity.X, 0, SLIDE_FRICTION_COEFFICIENT * delta),
        Velocity.Y,
        Mathf.MoveToward(Velocity.Z, 0, SLIDE_FRICTION_COEFFICIENT * delta)
    );
  }


  private void ProcessSlideCollisionsPost()
  {
    for (int i = 0; i < GetSlideCollisionCount(); i++)
    {
      KinematicCollision3D collision = GetSlideCollision(i);
      Vector3 normal = collision.GetNormal();

      if (Mathf.Abs(normal.Dot(Vector3.Up)) < 0.7f)
      {
        Vector3 reflected = preSlideHorizontalVelocity.Bounce(normal);
        if (reflected.Length() < 0.1f)
          reflected = -preSlideHorizontalVelocity;

        Velocity = reflected * SLIDE_COLLISION_SPEED_FACTOR;
        break;
      }
    }
  }


  private void ProcessAirMovement(Vector3 inputDirection, float delta)
  {
    if (airLurchManager == null)
      airLurchManager = new AirLurchManager(new Vector2(Velocity.X, Velocity.Z));

    Vector2 currentVel = new Vector2(Velocity.X, Velocity.Z);
    Vector2 newVel = airLurchManager.PerformLurch(currentVel, new Vector2(inputDirection.X, inputDirection.Z));
    Velocity = new Vector3(newVel.X, Velocity.Y, newVel.Y);
  }

  private void EquipDefaultWeapon()
  {
    Inventory inventory = GetTree().Root.GetNode<Inventory>("InventorySingleton");
    GD.Print("Equipping default weapon");


    if (inventory?.PrimaryWeapon != null)
    {
      currentWeapon = inventory.PrimaryWeapon;
      GetNode<Node3D>("Camera3D/WeaponHolder").AddChild(currentWeapon);
    }
  }

  public void ReceiveDamage()
  {
    health--;
    if (health <= 0)
    {
      health = 3;
      Position = Vector3.Zero;
    }
    EmitSignal(SignalName.HealthChanged, health);
  }

  private class AirLurchManager
  {
    private const float LURCH_SPEED = 10.0f;
    private const float LURCH_SPEED_LOSS = 0.2f;
    private const float CONE_HALF_ANGLE = Mathf.Pi / 4;  // Loosen angle restriction
    private const float LURCH_DURATION = 15.0f;          // Duration in seconds

    private List<float> usedConeAngles = new();
    private double lurchEndTime;
    private Vector2 airInitialDir;

    public AirLurchManager(Vector2 initialDirection)
    {
      Reset(initialDirection);
    }

    public void Reset(Vector2 initialDirection)
    {
      airInitialDir = initialDirection.Normalized();
      usedConeAngles.Clear(); // Reset used angles
      usedConeAngles.Add(airInitialDir.Angle());
      lurchEndTime = Time.GetUnixTimeFromSystem() + LURCH_DURATION;  // Use correct time function
    }

    private float AngleDifference(float angleA, float angleB)
    {
      return Mathf.Atan2(Mathf.Sin(angleA - angleB), Mathf.Cos(angleA - angleB));
    }

    private bool CanLurch(Vector2 inputDirection)
    {
      if (Time.GetUnixTimeFromSystem() > lurchEndTime)
      {
        return false;
      }

      if (inputDirection.Length() < 0.1f)  // Prevent processing near-zero input
      {
        return false;
      }

      float inputAngle = inputDirection.Angle();
      foreach (float usedAngle in usedConeAngles)
      {
        float diff = Mathf.Abs(AngleDifference(inputAngle, usedAngle));
        if (diff < CONE_HALF_ANGLE)
        {
          return false;
        }
      }

      return true;
    }

    private void MarkLurchUsed(Vector2 inputDirection)
    {
      usedConeAngles.Add(inputDirection.Angle());
    }

    public Vector2 PerformLurch(Vector2 currentVel, Vector2 inputDirection)
    {
      if (!CanLurch(inputDirection))
        return currentVel;

      Vector2 lurchVector = inputDirection.Normalized() * LURCH_SPEED;
      Vector2 newVel = currentVel + lurchVector;
      float newSpeed = newVel.Length() * (1.0f - LURCH_SPEED_LOSS);
      newVel = newVel.Normalized() * newSpeed;

      MarkLurchUsed(inputDirection);

      return newVel;
    }
  }

  public override void _Process(double delta)
  {
    DetectInteractable();
  }

  private void DetectInteractable()
  {
    var spaceState = GetWorld3D().DirectSpaceState;

    var query = new PhysicsShapeQueryParameters3D();
    query.Transform = new Transform3D(Basis.Identity, GlobalTransform.Origin);
    query.Shape = new SphereShape3D { Radius = INTERACT_RADIUS };
    query.CollideWithBodies = true;

    var results = spaceState.IntersectShape(query, 32); // Max 32 results

    nearbyInteractable = null;

    foreach (var result in results)
    {
      if (result["collider"].As<Node3D>() is Node3D node && node is IInteractable interactable)
      {
        nearbyInteractable = interactable;
        gameUI.InteractionLabel.Text = interactable.GetInteractionText();
        gameUI.InteractionLabel.Visible = true;
        gameUI.InteractionLabel.QueueRedraw();
        return; // Only detect the closest one
      }
    }

    gameUI.InteractionLabel.Visible = false;
    nearbyInteractable = null;
  }
}
