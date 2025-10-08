using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class InventoryStore : Node
{
  public static InventoryStore Instance { get; private set; }

  public event Action<InventoryState, ChangeOrigin> StateChanged;

  public InventoryState State { get; private set; } = InventoryState.Empty;

  private readonly System.Collections.Generic.Dictionary<string, ModuleData> _catalogById = new();
  private readonly System.Collections.Generic.Dictionary<WeaponModule, string> _idsByModule = new();
  private readonly System.Collections.Generic.Dictionary<WeaponModule, Action<ModuleBadge?>> _badgeHandlers = new();
  private readonly System.Collections.Generic.Dictionary<WeaponModule, ModuleBadge?> _lastBadges = new();
  private readonly List<string> _inventoryOrder = new();
  private readonly List<string> _primaryWeaponOrder = new();
  private Weapon _primaryWeapon;

  public override void _EnterTree()
  {
    if (Instance != null && Instance != this)
    {
      GD.PushWarning("InventoryStore instance replaced");
    }
    Instance = this;
  }

  public override void _ExitTree()
  {
    if (Instance == this)
      Instance = null;
    base._ExitTree();
  }

  public void Initialize(Weapon primaryWeapon, IReadOnlyList<WeaponModule> inventoryModules, IReadOnlyList<WeaponModule> primaryModules, ChangeOrigin origin = ChangeOrigin.System)
  {
    _primaryWeapon = primaryWeapon;

    _catalogById.Clear();
    _idsByModule.Clear();
    _inventoryOrder.Clear();
    _primaryWeaponOrder.Clear();

    if (inventoryModules != null)
    {
      foreach (WeaponModule module in inventoryModules)
      {
        if (module == null) continue;
        string id = EnsureModuleRegistered(module);
        _inventoryOrder.Add(id);
      }
    }

    if (primaryModules != null)
    {
      foreach (WeaponModule module in primaryModules)
      {
        if (module == null) continue;
        string id = EnsureModuleRegistered(module);
        _primaryWeaponOrder.Add(id);
      }
    }

    CommitState(origin);
  }

  

  public ModuleVm[] GetModules(StackKind kind)
  {
    List<string> list = GetList(kind);
    var result = new ModuleVm[list.Count];
    for (int i = 0; i < list.Count; i++)
    {
      if (_catalogById.TryGetValue(list[i], out ModuleData data))
        result[i] = data.ToViewModel();
      else
        result[i] = new ModuleVm(list[i], null, list[i]);
    }
    return result;
  }

  public void MoveModule(string moduleId, StackKind from, StackKind to, int toIndex, ChangeOrigin origin = ChangeOrigin.UI)
  {
    if (string.IsNullOrEmpty(moduleId)) return;
    if (!_catalogById.ContainsKey(moduleId)) return;

    if (from == to)
    {
      List<string> list = GetList(from);
      int currentIndex = list.IndexOf(moduleId);
      if (currentIndex == -1) return;

      // Interpret toIndex in the pre-removal index space.
      int insertionIndex = Math.Clamp(toIndex, 0, list.Count);

      // Remove first to get the post-removal list size, but adjust the
      // target relative to the original currentIndex before any post-removal clamp.
      list.RemoveAt(currentIndex);

      if (insertionIndex > currentIndex)
        insertionIndex -= 1;

      // Finally, clamp to the post-removal bounds [0..list.Count].
      insertionIndex = Math.Clamp(insertionIndex, 0, list.Count);

      list.Insert(insertionIndex, moduleId);
      CommitState(origin);
      return;
    }

    List<string> fromList = GetList(from);
    List<string> toList = GetList(to);

    int removeIndex = fromList.IndexOf(moduleId);
    if (removeIndex == -1) return;
    fromList.RemoveAt(removeIndex);

    int clampedIndex = Math.Clamp(toIndex, 0, toList.Count);
    toList.Insert(clampedIndex, moduleId);
    CommitState(origin);
  }

  public void AddModule(WeaponModule module, StackKind destination, int index, ChangeOrigin origin = ChangeOrigin.Gameplay)
  {
    if (module == null) return;
    string id = EnsureModuleRegistered(module);
    List<string> list = GetList(destination);
    int insertion = Math.Clamp(index, 0, list.Count);
    list.Insert(insertion, id);
    CommitState(origin);
  }

  public void RemoveModule(string moduleId, ChangeOrigin origin = ChangeOrigin.Gameplay)
  {
    if (string.IsNullOrEmpty(moduleId)) return;
    bool removed = _inventoryOrder.Remove(moduleId);
    removed = _primaryWeaponOrder.Remove(moduleId) || removed;
    if (!removed) return;

    PruneCatalog();
    CommitState(origin);
  }

  public void SetAllModules(IReadOnlyList<WeaponModule> inventoryModules, IReadOnlyList<WeaponModule> primaryModules, ChangeOrigin origin = ChangeOrigin.Gameplay)
  {
    _inventoryOrder.Clear();
    if (inventoryModules != null)
    {
      foreach (WeaponModule module in inventoryModules)
      {
        if (module == null) continue;
        string id = EnsureModuleRegistered(module);
        _inventoryOrder.Add(id);
      }
    }

    _primaryWeaponOrder.Clear();
    if (primaryModules != null)
    {
      foreach (WeaponModule module in primaryModules)
      {
        if (module == null) continue;
        string id = EnsureModuleRegistered(module);
        _primaryWeaponOrder.Add(id);
      }
    }
    PruneCatalog();
    CommitState(origin);
  }

  public void ReplaceInventoryModules(IReadOnlyList<WeaponModule> modules, ChangeOrigin origin = ChangeOrigin.Gameplay)
  {
    _inventoryOrder.Clear();
    if (modules != null)
    {
      foreach (WeaponModule module in modules)
      {
        if (module == null) continue;
        string id = EnsureModuleRegistered(module);
        _inventoryOrder.Add(id);
      }
    }
    PruneCatalog();
    CommitState(origin);
  }

  public void ReplacePrimaryWeaponModules(IReadOnlyList<WeaponModule> modules, ChangeOrigin origin = ChangeOrigin.Gameplay)
  {
    _primaryWeaponOrder.Clear();
    if (modules != null)
    {
      foreach (WeaponModule module in modules)
      {
        if (module == null) continue;
        string id = EnsureModuleRegistered(module);
        _primaryWeaponOrder.Add(id);
      }
    }
    PruneCatalog();
    CommitState(origin);
  }

  public void SetPrimaryWeapon(Weapon weapon, IReadOnlyList<WeaponModule> modules = null, ChangeOrigin origin = ChangeOrigin.System)
  {
    _primaryWeapon = weapon;

    _primaryWeaponOrder.Clear();
    IReadOnlyList<WeaponModule> source = modules ?? weapon?.Modules;
    if (source != null)
    {
      foreach (WeaponModule module in source)
      {
        if (module == null) continue;
        string id = EnsureModuleRegistered(module);
        _primaryWeaponOrder.Add(id);
      }
    }

    PruneCatalog();
    CommitState(origin);
  }

  public Array<WeaponModule> GetInventoryModules()
  {
    return BuildModulesArray(_inventoryOrder);
  }

  public Array<WeaponModule> GetPrimaryWeaponModules()
  {
    return BuildModulesArray(_primaryWeaponOrder);
  }

  public bool TryGetModuleData(string moduleId, out ModuleData data) => _catalogById.TryGetValue(moduleId, out data);

  public bool TryGetModuleId(WeaponModule module, out string moduleId)
  {
    if (module == null)
    {
      moduleId = null;
      return false;
    }
    if (_idsByModule.TryGetValue(module, out string existingId))
    {
      moduleId = existingId;
      return true;
    }
    moduleId = null;
    return false;
  }

  private string EnsureModuleRegistered(WeaponModule module)
  {
    if (_idsByModule.TryGetValue(module, out string existingId))
    {
      HookModuleBadge(module, existingId);
      return existingId;
    }

    string id = Guid.NewGuid().ToString("N");
    var entry = new ModuleData(id, module);
    _idsByModule[module] = id;
    _catalogById[id] = entry;
    HookModuleBadge(module, id);
    return id;
  }

  private void CommitState(ChangeOrigin origin)
  {
    PruneCatalog();

    State = new InventoryState(_inventoryOrder.ToArray(), _primaryWeaponOrder.ToArray());

    if (_primaryWeapon != null)
    {
      Array<WeaponModule> modules = BuildModulesArray(_primaryWeaponOrder);
      // Avoid reassigning the same array when possible.
      _primaryWeapon.Modules = modules;
    }

    StateChanged?.Invoke(State, origin);
  }

  private Array<WeaponModule> BuildModulesArray(List<string> order)
  {
    var result = new Array<WeaponModule>();
    foreach (string id in order)
    {
      if (_catalogById.TryGetValue(id, out ModuleData data) && data.Module != null)
      {
        result.Add(data.Module);
      }
    }
    return result;
  }

  private void PruneCatalog()
  {
    var used = new HashSet<string>();
    used.EnsureCapacity(_inventoryOrder.Count + _primaryWeaponOrder.Count);
    foreach (string id in _inventoryOrder)
      used.Add(id);
    foreach (string id in _primaryWeaponOrder)
      used.Add(id);

    var toRemove = new List<string>();
    foreach (KeyValuePair<string, ModuleData> kvp in _catalogById)
    {
      if (!used.Contains(kvp.Key))
        toRemove.Add(kvp.Key);
    }

    foreach (string id in toRemove)
    {
      if (_catalogById.TryGetValue(id, out ModuleData data) && data.Module != null)
      {
        _idsByModule.Remove(data.Module);
        if (_badgeHandlers.TryGetValue(data.Module, out var handler))
        {
          data.Module.BadgeChanged -= handler;
          _badgeHandlers.Remove(data.Module);
        }
        _lastBadges.Remove(data.Module);
        ModuleBadgeRegistry.Instance?.ClearBadge(id);
      }
      _catalogById.Remove(id);
    }
  }

  private void HookModuleBadge(WeaponModule module, string moduleId)
  {
    if (module == null || string.IsNullOrEmpty(moduleId))
      return;

    if (_badgeHandlers.ContainsKey(module))
      return;

    Action<ModuleBadge?> handler = badge =>
    {
      _lastBadges[module] = badge;
      if (ModuleBadgeRegistry.Instance == null)
        return;
      if (badge.HasValue && !string.IsNullOrEmpty(badge.Value.Text))
        ModuleBadgeRegistry.Instance.SetBadge(moduleId, badge.Value);
      else
        ModuleBadgeRegistry.Instance.ClearBadge(moduleId);
    };

    _badgeHandlers[module] = handler;
    module.BadgeChanged += handler;

    ModuleBadge? initial = module.GetInitialBadge();
    _lastBadges[module] = initial;
    handler(initial);
  }

  public void ReplayModuleBadges()
  {
    foreach (var pair in _badgeHandlers)
    {
      var module = pair.Key;
      if (_lastBadges.TryGetValue(module, out var badge))
      {
        pair.Value(badge);
      }
    }
  }

  private List<string> GetList(StackKind kind)
  {
    return kind == StackKind.Inventory ? _inventoryOrder : _primaryWeaponOrder;
  }
}
