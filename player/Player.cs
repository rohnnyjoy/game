// Player.cs
using Godot;
using System;

public partial class Player : CharacterBody3D
{
  public static Player Instance { get; private set; }

  [Export]
  public NodePath InventoryPath; // Assigned in the editor.

  public Inventory Inventory { get; private set; }
  public Camera3D Camera { get; private set; }
  public AnimationPlayer AnimPlayer { get; private set; }
  public Weapon CurrentWeapon { get; set; }
  public Vector3 PreSlideHorizontalVelocity { get; set; }
  public AirLurchManager AirLurchManager { get; set; }
  public InteractionManager InteractionManager;

  // Expose some fields to helper classes.
  public float JumpBufferTimer { get; set; }
  public int JumpsRemaining { get; set; }
  public bool IsSliding { get; set; }
  public Vector3 Velocity
  {
    get => base.Velocity;
    set => base.Velocity = value;
  }

  // Constants moved to public for access in helper classes.
  public const float DASH_SPEED = 20.0f;
  public const float EXTRA_UPWARD_BOOST = 3.0f;
  public const float SPEED = 10.0f;
  public const float JUMP_VELOCITY = 18.0f;
  public const float GROUND_ACCEL = 80.0f;
  public const float GROUND_DECEL = 150.0f;
  public const float INITIAL_BOOST_FACTOR = 0.8f;
  public const float GRAVITY = 60.0f;
  public const float SLIDE_FRICTION_COEFFICIENT = 15.0f;
  public const float WALL_HOP_BOOST = 1.2f;
  public const float WALL_HOP_UPWARD_BOOST = 18.0f;
  public const float WALL_HOP_MIN_NORMAL_Y = 0.7f;
  public const float JUMP_BUFFER_TIME = 0.2f;
  public const int MAX_JUMPS = 2;

  // Sub-components.
  private PlayerInput playerInput;
  private PlayerMovement playerMovement;

  public override void _Ready()
  {
    GD.Print("Player ready");
    Instance = this;

    // Cache nodes.
    Camera = GetNode<Camera3D>("Camera3D");
    AnimPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

    Inventory = new Inventory();
    AddChild(Inventory);

    // Initialize sub-components.
    playerInput = new PlayerInput(this);
    playerMovement = new PlayerMovement(this);

    // Additional setup.
    Input.MouseMode = Input.MouseModeEnum.Captured;
    InteractionManager = new InteractionManager();
    EquipDefaultWeapon();
  }

  public override void _PhysicsProcess(double delta)
  {
    playerMovement.ProcessMovement((float)delta);
    InteractionManager.DetectInteractable();
  }

  public override void _Process(double delta)
  {
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    playerInput.HandleInput(@event);
  }

  public Vector3 GetInputDirection()
  {
    Vector2 rawInput = Input.GetVector("left", "right", "up", "down");
    return (Transform.Basis * new Vector3(rawInput.X, 0, rawInput.Y)).Normalized();
  }

  private void EquipDefaultWeapon()
  {
    GD.Print("Equipping default weapon");
    if (Inventory?.PrimaryWeapon != null)
    {
      CurrentWeapon = Inventory.PrimaryWeapon;
      GetNode<Node3D>("Camera3D/WeaponHolder").AddChild(CurrentWeapon);
    }
  }

  // You can continue moving interaction and damage methods here or create further helper classes.
}
