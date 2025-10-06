using Godot;
#nullable enable

public static class IconAtlas
{
  private static Texture2D? _itemsAtlas;

  private static Texture2D ItemsAtlas
  {
    get
    {
      _itemsAtlas ??= GD.Load<Texture2D>("res://assets/ui/items.png");
      return _itemsAtlas!;
    }
  }

  // Creates a 32x32 subtexture from the items atlas.
  // Layout (left→right):
  // 0 bouncy, 1 aimbot, 2 penetrating, 3 scatter, 4 sticky,
  // 5 explosive, 6 homing, 7 speed, 8 slow, 9 tracking
  public static Texture2D MakeItemsIcon(int index)
  {
    var region = new Rect2(index * 32, 0, 32, 32);
    var atlas = new AtlasTexture
    {
      Atlas = ItemsAtlas,
      Region = region
    };
    return atlas;
  }
}
