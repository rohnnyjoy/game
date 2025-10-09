using Godot;
using Godot.Collections;

/// <summary>
/// Tracks the active module drag so we can handle drops outside the UI stacks.
/// </summary>
public static class ModuleDragContext
{
  private static SlotView _sourceSlot;
  private static StackKind _sourceStack;
  private static string _moduleId = string.Empty;
  private static WeaponModule _module;
  private static CardCore _cardCore;
  private static bool _active;

  public static void Begin(SlotView slot, ModuleVm moduleVm)
  {
    Reset();

    if (slot == null || moduleVm == null)
      return;

    InventoryStore store = InventoryStore.Instance;
    ModuleData data = null;
    store?.TryGetModuleData(moduleVm.ModuleId, out data);

    _sourceSlot = slot;
    _sourceStack = slot.Kind;
    _moduleId = moduleVm.ModuleId;
    _module = data?.Module;
    _cardCore = BuildCardCore(data?.Module);
    _active = !string.IsNullOrEmpty(_moduleId) && _module != null;
  }

  public static void MarkHandled(string moduleId)
  {
    if (!_active)
      return;
    if (!string.Equals(_moduleId, moduleId))
      return;
    Reset();
  }

  public static void HandleDragEnd(SlotView slot)
  {
    if (!_active)
      return;
    if (slot != _sourceSlot)
      return;

    if (slot.GetGlobalRect().HasPoint(slot.GetGlobalMousePosition()))
    {
      Reset();
      return;
    }

    WeaponModule module = _module;
    CardCore cardCore = _cardCore;
    StackKind stack = _sourceStack;

    Reset();

    if (module == null)
      return;

    ModuleWorldDropper.Drop(slot, module, cardCore);
    RemoveModuleFromDataStructures(module, stack);
  }

  private static CardCore BuildCardCore(WeaponModule module)
  {
    if (module == null)
      return null;

    var core = new CardCore
    {
      CardTexture = module.CardTexture,
      CardDescription = module.ModuleDescription,
      CardSize = new Vector2(ModuleUiConfig.IconSize, ModuleUiConfig.IconSize)
    };
    return core;
  }

  private static void RemoveModuleFromDataStructures(WeaponModule module, StackKind stack)
  {
    InventoryStore store = InventoryStore.Instance;
    bool removedFromStore = false;
    if (store != null && store.TryGetModuleId(module, out string moduleId))
    {
      store.RemoveModule(moduleId, ChangeOrigin.Gameplay);
      removedFromStore = true;
    }

    if (removedFromStore)
      return;

    Player player = Player.Instance;
    if (player == null)
      return;

    switch (stack)
    {
      case StackKind.Inventory:
        {
          Array<WeaponModule> modules = new Array<WeaponModule>(player.Inventory.WeaponModules);
          modules.Remove(module);
          player.Inventory.WeaponModules = modules;
          break;
        }
      case StackKind.PrimaryWeapon:
        {
          Weapon weapon = player.Inventory.PrimaryWeapon;
          if (weapon != null)
          {
            Array<WeaponModule> modules = new Array<WeaponModule>(weapon.Modules);
            modules.Remove(module);
            weapon.Modules = modules;
            player.Inventory.PrimaryWeapon = weapon;
          }
          break;
        }
    }
  }

  private static void Reset()
  {
    _sourceSlot = null;
    _sourceStack = StackKind.Inventory;
    _moduleId = string.Empty;
    _module = null;
    _cardCore = null;
    _active = false;
  }
}
