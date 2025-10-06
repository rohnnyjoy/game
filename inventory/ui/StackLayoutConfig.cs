using Godot;

public partial class StackLayoutConfig : Resource
{
  [Export] public float Offset { get; set; } = 120.0f;
  [Export] public float Padding { get; set; } = 20.0f;
  [Export] public float SlotPadding { get; set; } = 6.0f;
  [Export] public int SlotNinePatchMargin { get; set; } = 18;
  [Export] public Texture2D SlotNinePatchTexture { get; set; } = GD.Load<Texture2D>("res://assets/ui/3x/ninepatch.png");
}

