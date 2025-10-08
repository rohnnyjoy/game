using Godot;
using System;

public partial class Coin : Area3D
{
  [Export]
  public int Value { get; set; } = 10;
  [Export]
  public float PixelSize { get; set; } = 0.035f; // Smaller default; crisp pixel art

  private AnimatedSprite3D _sprite;
  private CollisionShape3D _collision;
  private static SpriteFrames _sharedFrames;
  private static Texture2D _sheet;
  private const int FrameW = 20;
  private const int FrameH = 20;

  public static void Prewarm()
  {
    // Force-build shared frames early to avoid first-spawn hitch
    if (_sharedFrames == null)
    {
      _sharedFrames = BuildFrames();
    }
  }

  public static SpriteFrames GetSharedFrames()
  {
    if (_sharedFrames == null)
      _sharedFrames = BuildFrames();
    return _sharedFrames;
  }

  private static SpriteFrames BuildFrames()
  {
    var frames = new SpriteFrames();
    frames.AddAnimation("spin");
    frames.SetAnimationSpeed("spin", 12f);

    if (_sheet == null)
      _sheet = GD.Load<Texture2D>("res://assets/sprites/items/coin_20x20.png");
    if (_sheet == null)
      return frames;

    int cols = Math.Max(1, _sheet.GetWidth() / FrameW);
    int rows = Math.Max(1, _sheet.GetHeight() / FrameH);

    for (int y = 0; y < rows; y++)
    {
      for (int x = 0; x < cols; x++)
      {
        var region = new Rect2I(x * FrameW, y * FrameH, FrameW, FrameH);
        if (region.Position.X + region.Size.X <= _sheet.GetWidth() &&
            region.Position.Y + region.Size.Y <= _sheet.GetHeight())
        {
          var atlas = new AtlasTexture();
          atlas.Atlas = _sheet;
          atlas.Region = new Rect2(region.Position, region.Size);
          frames.AddFrame("spin", atlas);
        }
      }
    }
    return frames;
  }

  public override void _Ready()
  {
    // Create visuals
    _sprite = new AnimatedSprite3D();
    AddChild(_sprite);
    // Preserve crisp pixel-art: nearest sampling
    _sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;

    if (_sharedFrames == null)
      _sharedFrames = BuildFrames();
    _sprite.SpriteFrames = _sharedFrames;
    _sprite.Animation = "spin";
    _sprite.Play();
    _sprite.PixelSize = PixelSize; // enlarge for readability
    // Correct mirroring if sprite appears flipped horizontally
    _sprite.FlipH = true;

    // Collision for pickup
    _collision = new CollisionShape3D();
    var shape = new SphereShape3D();
    shape.Radius = 0.4f;
    _collision.Shape = shape;
    AddChild(_collision);

    // Slight vertical offset so it doesn't clip the ground
    _sprite.Position = new Vector3(0, 0.2f, 0);
    _collision.Position = new Vector3(0, 0.2f, 0);

    // Connect pickup logic
    Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
  }

  public override void _Process(double delta)
  {
    // Billboard: keep sprite facing the active camera
    var cam = GetViewport()?.GetCamera3D();
    if (cam != null && IsInstanceValid(_sprite))
    {
      _sprite.LookAt(cam.GlobalTransform.Origin, Vector3.Up);
    }
  }

  private void OnBodyEntered(Node body)
  {
    if (body is Player)
    {
      var inv = Player.Instance?.Inventory;
      if (inv != null && GlobalEvents.Instance != null)
      {
        int oldAmount = inv.Money;
        int newAmount = oldAmount + Value;
        GlobalEvents.Instance.EmitMoneyUpdated(oldAmount, newAmount);
        inv.Money = newAmount;
      }

      QueueFree();
    }
  }
}
