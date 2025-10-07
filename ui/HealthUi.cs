using Godot;
using System.Collections.Generic;

public partial class HealthUi : Control
{
  private DynaTextControl _text;
  private ColorRect _barBg;
  private ColorRect _barFill;

  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  // Standardize top-left UI text size to match gold total
  [Export] public int FontPx = 80;
  [Export] public Color BarRed = new Color(1f, 0.24f, 0.26f);
  [Export] public Color BarBack = new Color(0f, 0f, 0f, 0.85f);
  [Export(PropertyHint.Range, "64,1200,1")] public int BarWidth = 520;
  // Make the bar tall enough to contain 80px text with comfortable padding
  [Export(PropertyHint.Range, "8,256,1")] public int BarHeight = 120;
  [Export(PropertyHint.Range, "0,48,1")] public int BarPadding = 12;

  public override void _Ready()
  {
    AnchorLeft = 0; AnchorTop = 0; AnchorRight = 0; AnchorBottom = 0;
    SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    SizeFlagsVertical = SizeFlags.ShrinkBegin;
    OffsetLeft = 0; OffsetTop = 0; OffsetRight = 0; OffsetBottom = 0;
    MouseFilter = MouseFilterEnum.Ignore;

    // Explicit minimum so containers reserve consistent space
    CustomMinimumSize = new Vector2(BarWidth, BarHeight);

    _barBg = new ColorRect
    {
      Color = BarBack,
      MouseFilter = MouseFilterEnum.Ignore,
    };
    _barBg.AnchorLeft = 0; _barBg.AnchorTop = 0; _barBg.AnchorRight = 0; _barBg.AnchorBottom = 0;
    _barBg.OffsetLeft = 0; _barBg.OffsetTop = 0; _barBg.OffsetRight = BarWidth; _barBg.OffsetBottom = BarHeight;
    _barBg.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; _barBg.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    AddChild(_barBg);

    _barFill = new ColorRect
    {
      Color = BarRed,
      MouseFilter = MouseFilterEnum.Ignore,
    };
    _barFill.AnchorLeft = 0; _barFill.AnchorTop = 0; _barFill.AnchorRight = 0; _barFill.AnchorBottom = 0;
    _barFill.OffsetLeft = BarPadding; _barFill.OffsetTop = BarPadding;
    _barFill.OffsetRight = BarPadding; _barFill.OffsetBottom = BarPadding;
    _barFill.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; _barFill.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    AddChild(_barFill);

    _text = new DynaTextControl
    {
      FontPath = FontPath,
      FontPx = FontPx,
      Shadow = true,
      UseShadowParallax = true,
      ShadowAlpha = 0.4f,
      ParallaxPixelScale = 0f,
      AmbientRotate = true,
      AmbientFloat = true,
      AmbientBump = false,
      CenterInRect = true,
      MouseFilter = MouseFilterEnum.Ignore,
      ClipContents = true,
    };
    // White overlay text by default
    _text.SetColours(new List<Color> { Colors.White });
    AddChild(_text);
    Visible = true;

    // Initialize visuals
    UpdateBarGeometry(1f);
    SetHealth(100, 100);
  }

  public override Vector2 _GetMinimumSize()
  {
    // Ensure the bar has priority; height also accounts for padding
    int h = Mathf.Max(BarHeight, (int)(_text?.GetCombinedMinimumSize().Y ?? BarHeight));
    return new Vector2(Mathf.Max(BarWidth, (int)(_text?.GetCombinedMinimumSize().X ?? BarWidth)), h);
  }

  private void UpdateBarGeometry(float pct)
  {
    EnsureBarFitsText();
    pct = Mathf.Clamp(pct, 0f, 1f);
    // Background size
    _barBg.OffsetLeft = 0; _barBg.OffsetTop = 0; _barBg.OffsetRight = BarWidth; _barBg.OffsetBottom = BarHeight;

    // Inner fill rect scales with percentage, leaving padding around
    int innerW = Mathf.Max(0, BarWidth - BarPadding * 2);
    int innerH = Mathf.Max(1, BarHeight - BarPadding * 2);
    int fillW = Mathf.RoundToInt(innerW * pct);
    _barFill.OffsetLeft = BarPadding;
    _barFill.OffsetTop = BarPadding;
    _barFill.OffsetRight = BarPadding + fillW;
    _barFill.OffsetBottom = BarPadding + innerH;

    // Left-align overlay text with a small inset; vertically center within bar
    // Center overlay text inside the bar's content rect (excluding padding)
    int vpad = BarPadding;
    _text.Position = new Vector2(vpad, vpad);
    _text.Size = new Vector2(Mathf.Max(0, BarWidth - vpad * 2), Mathf.Max(1, BarHeight - vpad * 2));
  }

  private void EnsureBarFitsText()
  {
    if (_text == null) return;
    Vector2 ts = _text.GetCombinedMinimumSize();
    int minW = (int)Mathf.Ceil(ts.X) + BarPadding * 2;
    int minH = (int)Mathf.Ceil(ts.Y) + BarPadding * 2;
    if (BarWidth < minW) BarWidth = minW;
    if (BarHeight < minH) BarHeight = minH;
    CustomMinimumSize = new Vector2(BarWidth, BarHeight);
  }

  public void SetHealth(float current, float max)
  {
    current = Mathf.Max(0f, current);
    max = Mathf.Max(1f, max);
    float pct = current / max;
    // White overlay text inside the bar
    _text.SetColours(new List<Color> { Colors.White });
    _text.SetText($"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}");
    UpdateBarGeometry(pct);
    EmitSignal(SignalName.MinimumSizeChanged);
    if (pct < 0.25f) _text.Pulse(0.28f);
  }
}
