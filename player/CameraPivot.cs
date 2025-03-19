using Godot;

public partial class CameraPivot : Node3D
{
  public Camera3D Camera { get; private set; }
  public WeaponHolder WeaponHolder { get; private set; }
  public override void _Ready()
  {
    Camera = GetNode<Camera3D>("Camera");
    WeaponHolder = GetNode<WeaponHolder>("WeaponHolder");
  }
}
