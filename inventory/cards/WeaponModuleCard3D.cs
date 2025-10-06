using Godot;

public partial class WeaponModuleCard3D : Card3D
{
  [Export]
  public WeaponModule Module { get; set; }

  /// <summary>
  /// Initializes the card with an optional module.
  /// </summary>
  /// <param name="module">A WeaponModule instance (optional).</param>
  public void Initialize(WeaponModule module = null)
  {
    Module = module;
    OutlineColor = RarityExtensions.GetColor(module.Rarity);
    OutlineWidth = 0.05f;
    // Ensure CardCore is valid.
    if (CardCore == null)
    {
      CardCore = new CardCore();
    }
    if (Module != null)
    {
      // Associate the module's properties with the card.
      CardCore.CardTexture = Module.CardTexture;
      CardCore.CardDescription = Module.ModuleDescription;
      // Optionally set other module-specific data here.
    }
  }

  public override void OnInteract()
  {
    // Add to inventory
    var store = InventoryStore.Instance;
    if (store != null)
    {
      int insertIndex = store.State.InventoryModuleIds.Count;
      store.AddModule(Module, StackKind.Inventory, insertIndex, ChangeOrigin.Gameplay);
    }
    else
    {
      var newModules = new Godot.Collections.Array<WeaponModule>(Player.Instance.Inventory.WeaponModules)
      {
          Module
      };
      Player.Instance.Inventory.WeaponModules = newModules;
    }
    QueueFree();
  }

  public override void _Ready()
  {
    base._Ready();
    // Additional setup can be done here if needed.
  }
}
