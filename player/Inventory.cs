using Godot;
using Godot.Collections;

public partial class Inventory : Node
{
  [Signal]
  public delegate void InventoryChangedEventHandler();

  private Weapon _primaryWeapon = GD.Load<PackedScene>("res://weapons/ol_reliable/OlReliable.tscn").Instantiate<Weapon>();
  private Array<WeaponModule> _initialInventoryModules = new()
    {
        new ScatterModule(),
        new PiercingModule(),
        new HomingModule(),
        new ExplosiveModule(),
        new SlowModule(),
        new TrackingModule(),
        new AimbotModule(),
        new BouncingModule(),
        new StickyModule(),
        new FastModule(),
        new WeightedGloveModule(),
        new MetronomeModule(),
        new CursedSkullModule(),
    };

  public Weapon PrimaryWeapon
  {
    get => _primaryWeapon;
    set
    {
      _primaryWeapon = value;
      DebugTrace.Log($"Inventory.PrimaryWeapon set -> {value?.GetType().Name}");
      var store = InventoryStore.Instance;
      if (store != null)
      {
        store.SetPrimaryWeapon(value, value?.Modules, ChangeOrigin.Gameplay);
      }
      else
      {
        EmitSignal(nameof(InventoryChanged));
      }
    }
  }

  public Array<WeaponModule> WeaponModules
  {
    get
    {
      var store = InventoryStore.Instance;
      if (store != null)
        return store.GetInventoryModules();
      return new Array<WeaponModule>(_initialInventoryModules);
    }
    set
    {
      var modules = value ?? new Array<WeaponModule>();
      DebugTrace.Log($"Inventory.WeaponModules set count={modules.Count}");
      var store = InventoryStore.Instance;
      if (store != null)
      {
        store.ReplaceInventoryModules(modules, ChangeOrigin.Gameplay);
      }
      else
      {
        _initialInventoryModules = modules;
        EmitSignal(nameof(InventoryChanged));
      }
    }
  }

  public int Money;

  public override void _Ready()
  {
    GlobalEvents.Instance.Connect(nameof(GlobalEvents.EnemyDied), new Callable(this, nameof(OnEnemyDied)));

    var store = InventoryStore.Instance;
    if (store != null)
    {
      store.StateChanged += OnStoreStateChanged;
      store.Initialize(_primaryWeapon, _initialInventoryModules, _primaryWeapon?.Modules, ChangeOrigin.System);
    }
    else
    {
      EmitSignal(nameof(InventoryChanged));
    }
  }

  public override void _ExitTree()
  {
    var store = InventoryStore.Instance;
    if (store != null)
      store.StateChanged -= OnStoreStateChanged;
    base._ExitTree();
  }

  private void OnStoreStateChanged(InventoryState state, ChangeOrigin origin)
  {
    EmitSignal(nameof(InventoryChanged));
  }

  public void OnEnemyDied()
  {
    // Coin pickups now handle money awards; no direct grant on enemy death.
  }

  // Atomically update both inventory and primary weapon module lists, then emit a single InventoryChanged.
  public void SetModulesBoth(Array<WeaponModule> inventoryModules, Array<WeaponModule> primaryWeaponModules)
  {
    int invCount = inventoryModules?.Count ?? 0;
    int weapCount = primaryWeaponModules?.Count ?? 0;
    DebugTrace.Log($"Inventory.SetModulesBoth inv={invCount} weap={weapCount}");
    var store = InventoryStore.Instance;
    if (store != null)
    {
      store.SetAllModules(inventoryModules, primaryWeaponModules, ChangeOrigin.Gameplay);
    }
    else
    {
      _initialInventoryModules = inventoryModules ?? new Array<WeaponModule>();
      if (_primaryWeapon != null)
      {
        _primaryWeapon.Modules = primaryWeaponModules ?? new Array<WeaponModule>();
      }
      EmitSignal(nameof(InventoryChanged));
    }
  }
}
