using Godot;
using Godot.Collections;

public partial class Inventory : Node
{
  [Signal] public delegate void InventoryChangedEventHandler();

  private Weapon _primaryWeapon = GD.Load<PackedScene>("res://weapons/unique/OlReliable.tscn").Instantiate<Weapon>();
  private Array<WeaponModule> _weaponModules = new()
  {
    new PenetratingModule(),
  };

  public Weapon PrimaryWeapon
  {
    get => _primaryWeapon;
    set
    {
      _primaryWeapon = value;
      EmitSignal(SignalName.InventoryChanged);
    }
  }

  public Array<WeaponModule> WeaponModules
  {
    get => _weaponModules;
    set
    {
      _weaponModules = value;
      EmitSignal(SignalName.InventoryChanged);
    }
  }
}