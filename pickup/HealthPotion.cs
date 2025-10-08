using Godot;
using System;

public partial class HealthPotion : Area3D
{
  [Export] public int HealAmount { get; set; } = 25;
  [Export] public float PixelSize { get; set; } = 0.035f;
  [Export] public float AnimationFps { get; set; } = 10.0f;

  private AnimatedSprite3D _sprite;
  private CollisionShape3D _collision;
  private static SpriteFrames _sharedFrames;
  private static Texture2D _sheet;
  private const int FrameW = 24;
  private const int FrameH = 24;

  public static void Prewarm()
  {
    // Build frames early to avoid first-spawn hitch
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
    frames.AddAnimation("idle");
    frames.SetAnimationSpeed("idle", 10f);

    if (_sheet == null)
      _sheet = GD.Load<Texture2D>("res://assets/sprites/items/health_potion_24x24.png");
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
          frames.AddFrame("idle", atlas);
        }
      }
    }
    return frames;
  }

  public override void _Ready()
  {
    _sprite = new AnimatedSprite3D();
    AddChild(_sprite);
    _sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
    _sprite.PixelSize = PixelSize;
    _sprite.SpriteFrames = GetSharedFrames();
    _sprite.Animation = "idle";
    _sprite.SpeedScale = MathF.Max(0.01f, AnimationFps / 10.0f);
    _sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
    _sprite.Play();
    _sprite.FlipH = false;
    _sprite.Position = new Vector3(0, 0.2f, 0);

    _collision = new CollisionShape3D
    {
      Shape = new SphereShape3D { Radius = 0.35f },
      Position = new Vector3(0, 0.2f, 0)
    };
    AddChild(_collision);

    BodyEntered += OnBodyEntered;
  }

  private void OnBodyEntered(Node3D body)
  {
    if (body == null || !IsInstanceValid(body)) return;
    if (!body.IsInGroup("players")) return;
    if (body is Player player)
    {
      player.Heal(HealAmount);
      QueueFree();
    }
  }
}
