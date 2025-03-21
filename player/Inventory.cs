using Godot;
using Godot.Collections;

public partial class Inventory : Node
{
  [Signal]
  public delegate void InventoryChangedEventHandler();

  private Weapon _primaryWeapon = GD.Load<PackedScene>("res://weapons/shotgun/Shotgun.tscn").Instantiate<Weapon>();
  private Array<WeaponModule> _weaponModules = new()
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
    };

  public Weapon PrimaryWeapon
  {
    get => _primaryWeapon;
    set
    {
      _primaryWeapon = value;
      EmitSignal(nameof(InventoryChanged));
    }
  }

  public Array<WeaponModule> WeaponModules
  {
    get => _weaponModules;
    set
    {
      _weaponModules = value;
      EmitSignal(nameof(InventoryChanged));
    }
  }

  public int Money;

  public override void _Ready()
  {
    GlobalEvents.Instance.Connect(nameof(GlobalEvents.EnemyDied), new Callable(this, nameof(OnEnemyDied)));
    EmitSignal(nameof(InventoryChanged));
  }

  public void OnEnemyDied()
  {
    GD.Print("Enemy died");
    GlobalEvents.Instance.EmitMoneyUpdated(Money, Money + 50);
    Money += 50;
  }
}
