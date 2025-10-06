using Godot;

public partial class CardCore : Resource
{
  [Export]
  public Color CardColor { get; set; } = Colors.White;

  [Export]
  public Vector2 CardSize { get; set; } = new Vector2(100, 100);

  [Export]
  public Texture2D CardTexture { get; set; }

  [Export]
  public string CardDescription { get; set; } = "";
}
