#nullable enable
using Godot;
using Godot.Collections;

/// <summary>
/// World-space representation of a weapon module as a sprite-based pickup that requires interaction.
/// </summary>
public partial class ModulePickup : RigidBody3D, IInteractable
{
  private const float DefaultPixelSize = 0.035f;
  private const float SpriteYOffset = 0.25f;
  private const float ColliderHeight = 0.1f;

  [Export]
  public WeaponModule? Module { get; set; }

  [Export]
  public CardCore? CardCore { get; set; }

  [Export(PropertyHint.Range, "0.01,0.08,0.005")]
  public float PixelSize { get; set; } = DefaultPixelSize;

  [Export]
  public bool LockRotationToUp { get; set; } = true;

  private Sprite3D? _sprite;
  private CollisionShape3D? _collision;
  private Texture2D? _texture;
  private string _moduleName = "Module";
  private string _interactionText = "Pick up module";

  public void Initialize(WeaponModule? module, CardCore? cardCore)
  {
    Module = module;
    CardCore = cardCore;
    CaptureMetadata();
    if (_sprite != null)
      ApplyVisual();
  }

  public override void _Ready()
  {
    base._Ready();

    CaptureMetadata();
    SetupPhysics();
    BuildVisuals();

    AddToGroup("interactable");
  }

  public override void _Process(double delta)
  {
    base._Process(delta);
    if (_sprite == null)
      return;

    Camera3D? camera = GetViewport()?.GetCamera3D();
    if (camera == null)
      return;

    Vector3 lookTarget = camera.GlobalTransform.Origin;
    _sprite.LookAt(lookTarget, Vector3.Up);
  }

  public override void _IntegrateForces(PhysicsDirectBodyState3D state)
  {
    base._IntegrateForces(state);
    if (!LockRotationToUp)
      return;

    state.AngularVelocity = Vector3.Zero;
    state.Transform = new Transform3D(Basis.Identity, state.Transform.Origin);
  }

  public virtual void OnInteract(string actionName)
  {
    if (actionName != InteractionOption.DefaultAction)
      return;

    if (!TryTransferToInventory())
      return;

    QueueFree();
  }

  public virtual string GetInteractionText() => _interactionText;

  protected bool TryTransferToInventory()
  {
    if (Module == null)
      return false;

    InventoryStore? store = InventoryStore.Instance;
    if (store != null)
    {
      int insertIndex = store.State.InventoryModuleIds.Count;
      store.AddModule(Module, StackKind.Inventory, insertIndex, ChangeOrigin.Gameplay);
      return true;
    }

    Player? player = Player.Instance;
    if (player?.Inventory == null)
      return false;

    Array<WeaponModule> modules = new Array<WeaponModule>(player.Inventory.WeaponModules);
    modules.Add(Module);
    player.Inventory.WeaponModules = modules;
    return true;
  }

  private void CaptureMetadata()
  {
    _moduleName = Module?.ModuleName ?? Module?.GetType().Name ?? "Module";
    _interactionText = $"Pick up {_moduleName}";
    _texture = CardCore?.CardTexture ?? Module?.CardTexture;

    float sizeHint = CardCore?.CardSize.X > 0 ? CardCore.CardSize.X * 0.00035f : PixelSize;
    PixelSize = Mathf.Clamp(sizeHint, 0.02f, 0.08f);
  }

  private void SetupPhysics()
  {
    PhysicsMaterialOverride ??= new PhysicsMaterial
    {
      Friction = 0.6f,
      Bounce = 0.05f
    };

    Mass = 0.3f;
    LinearDamp = 0.4f;
    AngularDamp = 8.0f;
    ContactMonitor = true;
    MaxContactsReported = 4;
  }

  private void BuildVisuals()
  {
    _sprite = new Sprite3D
    {
      Name = "Sprite",
      PixelSize = PixelSize,
      Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
      TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
      Position = new Vector3(0f, SpriteYOffset, 0f)
    };
    AddChild(_sprite);

    ApplyVisual();

    _collision = new CollisionShape3D
    {
      Name = "Collision",
      Shape = new CylinderShape3D
      {
        Radius = PixelSize * 10f,
        Height = ColliderHeight
      },
      Position = new Vector3(0f, ColliderHeight * 0.5f, 0f)
    };
    AddChild(_collision);
  }

  private void ApplyVisual()
  {
    if (_sprite == null)
      return;

    if (_texture != null)
    {
      _sprite.Texture = _texture;
      _sprite.Modulate = Colors.White;
    }
    else
    {
      _sprite.Texture = null;
      _sprite.Modulate = CardCore?.CardColor ?? Colors.White;
    }
  }
}
