using Godot;

public partial class CameraPivot : Node3D
{
  [Export] public NodePath PlayerPath;

  private Player player;
  public override void _Ready()
  {
    player = GetNode<Player>(PlayerPath);
    SetProcessInput(true);
  }

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventMouseMotion mouseMotion)
    {
      player.RotateY(-mouseMotion.Relative.X * 0.005f);
      RotateX(-mouseMotion.Relative.Y * 0.005f);
      Rotation = new Vector3(
        Mathf.Clamp(Rotation.X, -Mathf.Pi / 2, Mathf.Pi / 2),
        Rotation.Y,
        Rotation.Z
      );
    }
  }

}
