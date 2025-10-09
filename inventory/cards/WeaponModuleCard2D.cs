using Godot;
using Godot.Collections;

public partial class WeaponModuleCard2D : Card2D
{
  private static System.Collections.Generic.Dictionary<WeaponModule, WeaponModuleCard2D> _registry = new System.Collections.Generic.Dictionary<WeaponModule, WeaponModuleCard2D>();

  [Export]
  public WeaponModule Module { get; set; }

  public override void _EnterTree()
  {
    base._EnterTree();
    if (Module != null)
    {
      _registry[Module] = this;
    }
  }

  public override void _Ready()
  {
    // Initialize CardCore using the module's properties.
    CardCore = new CardCore();
    CardCore.CardTexture = Module.CardTexture;
    CardCore.CardDescription = Module.ModuleDescription;
    // Use manual drag for Balatro-style reordering inside stacks.
    UseDnD = false;
    base._Ready();
  }

  public override void _ExitTree()
  {
    if (Module != null && _registry.TryGetValue(Module, out var existing) && existing == this)
      _registry.Remove(Module);
    base._ExitTree();
  }

  public static bool TryGetForModule(WeaponModule module, out WeaponModuleCard2D card)
  {
    if (module != null && _registry.TryGetValue(module, out var c))
    {
      card = c;
      return true;
    }
    card = null;
    return false;
  }

  protected override void OnDroppedOutsideStacks()
  {
    ConvertTo3D();
    var parent = GetParent();
    var store = InventoryStore.Instance;
    if (store != null && store.TryGetModuleId(Module, out string moduleId))
    {
      store.RemoveModule(moduleId, ChangeOrigin.Gameplay);
    }
    else if (parent is InventoryStack)
    {
      var newModules = new Array<WeaponModule>(Player.Instance.Inventory.WeaponModules);
      newModules.Remove(Module);
      Player.Instance.Inventory.WeaponModules = newModules;
    }
    else if (parent is PrimaryWeaponStack)
    {
      var newModules = new Array<WeaponModule>(Player.Instance.Inventory.PrimaryWeapon.Modules);
      newModules.Remove(Module);
      var newPrimaryWeapon = Player.Instance.Inventory.PrimaryWeapon;
      newPrimaryWeapon.Modules = newModules;
      Player.Instance.Inventory.PrimaryWeapon = newPrimaryWeapon;
    }
    else { }
  }

  private void ConvertTo3D()
  {
    ModuleWorldDropper.Drop(this, Module, CardCore);

    QueueFree();
  }
}
