using Godot;

public partial class GameUI : CanvasLayer
{
  [Export] public RichTextLabel InteractionLabel;

  public override void _Ready()
  {
    InteractionLabel = GetNode<RichTextLabel>("InteractionLabel");
  }
}
