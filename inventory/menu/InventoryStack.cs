using Godot;

public partial class InventoryStack : ModuleStackView
{
  public override void _Ready()
  {
    Kind = StackKind.Inventory;
    base._Ready();
  }
}
