using Godot;
using System;

public partial class Player : CharacterBody3D
{
  public static Player Instance { get; private set; }
  public Inventory Inventory { get; private set; }
  public WeaponHolder WeaponHolder { get; private set; }
  public CameraShake CameraShake { get; private set; }
  // public AnimationPlayer AnimPlayer { get; private set; }
  public Weapon CurrentWeapon { get; set; }
  public Vector3 PreSlideHorizontalVelocity { get; set; }
  public AirLurchManager AirLurchManager { get; set; }

  public float JumpBufferTimer { get; set; }
  public int JumpsRemaining { get; set; }
  public bool IsSliding { get; set; }
  public Vector3 Velocity
  {
    get => base.Velocity;
    set => base.Velocity = value;
  }

  [Export] public NodePath WeaponHolderPath;
  [Export] public NodePath CameraShakePath;
  // [Export] public NodePath AnimPlayerPath;

  private PlayerInput playerInput;

  public override void _Ready()
  {
    GD.Print("Player ready");
    Instance = this;
    WeaponHolder = GetNode<WeaponHolder>(WeaponHolderPath);
    CameraShake = GetNode<CameraShake>(CameraShakePath);
    // AnimPlayer = GetNode<AnimationPlayer>(AnimPlayerPath);
    AddToGroup("players");
    Inventory = new Inventory();
    AddChild(Inventory);
    playerInput = new PlayerInput(this);
    Input.MouseMode = Input.MouseModeEnum.Captured;
    EquipDefaultWeapon();
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
      WeaponHolder.AddChild(CurrentWeapon);
    }
  }

  public override void _ExitTree()
  {
    base._ExitTree();
  }
}
