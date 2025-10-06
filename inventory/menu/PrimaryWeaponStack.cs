using Godot;

public partial class PrimaryWeaponStack : ModuleStackView
{
  public override void _Ready()
  {
    Kind = StackKind.PrimaryWeapon;
    base._Ready();
  }
}
