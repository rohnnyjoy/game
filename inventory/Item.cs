using Godot;

[Tool]
public partial class Item : Resource
{
  [Export] public string ItemName { get; set; } = "New Item";
  [Export] public Texture2D Icon { get; set; }
}
