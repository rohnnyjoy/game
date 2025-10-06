using Godot;
using System.Collections.Generic;

public partial class HealthUi : Control
{
  private DynaTextControl _text;

  
  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx = 72; // bigger
  [Export] public Color BarRed = new Color(1f, 0.24f, 0.26f); // brighter healthbar red

  public override void _Ready()
  {
    AnchorLeft = 0; AnchorTop = 0; AnchorRight = 0; AnchorBottom = 0;
    SizeFlagsHorizontal = SizeFlags.ShrinkBegin; // keep compact
    SizeFlagsVertical = SizeFlags.ShrinkBegin;

    OffsetLeft = 0; OffsetTop = 0; OffsetRight = 0; OffsetBottom = 0;
    MouseFilter = MouseFilterEnum.Ignore;

    _text = new DynaTextControl
    {
      FontPath = FontPath,
      FontPx = FontPx,
      Shadow = true,
      UseShadowParallax = true,
      ShadowAlpha = 0.4f,
      ParallaxPixelScale = 0f,
      AmbientRotate = false,
      AmbientFloat = false,
      AmbientBump = false,
      CenterInRect = false,
      Position = Vector2.Zero,
    };
    AddChild(_text);
    Visible = true;
    // Show immediately with a sensible default; will be updated by GameUi/Player if available
    SetHealth(100, 100);
  }

  public override Vector2 _GetMinimumSize()
  {
    if (_text != null)
      return _text.GetCombinedMinimumSize();
    return new Vector2(Mathf.Max(4f, FontPx * 0.6f), Mathf.Max(4f, FontPx * 1.0f));
  }

  public void SetHealth(float current, float max)
  {
    current = Mathf.Max(0f, current);
    max = Mathf.Max(1f, max);
    // Always use deep healthbar red
    _text.SetColours(new List<Color> { BarRed });
    _text.SetText($"HP {Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}");
    EmitSignal(SignalName.MinimumSizeChanged);
    float pct = current / max;
    if (pct < 0.25f) _text.Pulse(0.28f); // subtle pulse when low
  }
}
