using System;
using Godot;
#nullable enable

public partial class RunTimerDisplay : Control
{
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx { get; set; } = 52;
  [Export] public Color TextColor { get; set; } = new Color(0.7f, 0.85f, 1f);
  [Export] public string LabelPrefix { get; set; } = "Time";

  private HBoxContainer _layout = default!;
  private DynaTextControl _labelControl = default!;
  private DynaTextControl _valueControl = default!;
  private ulong _startTicks;
  private int _lastWholeSeconds = -1;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Ignore;
    SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    SizeFlagsVertical = SizeFlags.ShrinkBegin;
    CustomMinimumSize = Vector2.Zero;

    _layout = new HBoxContainer
    {
      MouseFilter = MouseFilterEnum.Ignore
    };
    _layout.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    _layout.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    _layout.AddThemeConstantOverride("separation", 6);
    AddChild(_layout);

    _labelControl = new DynaTextControl
    {
      FontPath = FontPath,
      FontPx = FontPx,
      Shadow = true,
      UseShadowParallax = true,
      AmbientRotate = true,
      AmbientFloat = true,
      AmbientBump = false,
      CenterInRect = false,
      AlignX = 0f,
      AlignY = 0f,
      TextHeightScale = 0.85f,
      LetterSpacingExtraPx = 0.5f,
      OffsetYExtraPx = 0f,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _labelControl.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    _labelControl.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    _labelControl.SetColours(new System.Collections.Generic.List<Color> { TextColor });
    _layout.AddChild(_labelControl);

    _valueControl = new DynaTextControl
    {
      FontPath = FontPath,
      FontPx = FontPx,
      Shadow = true,
      UseShadowParallax = true,
      AmbientRotate = true,
      AmbientFloat = true,
      AmbientBump = false,
      CenterInRect = false,
      AlignX = 0f,
      AlignY = 0f,
      TextHeightScale = 0.85f,
      LetterSpacingExtraPx = 1.0f,
      OffsetYExtraPx = 0f,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _valueControl.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    _valueControl.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    _valueControl.SetColours(new System.Collections.Generic.List<Color> { TextColor });
    _layout.AddChild(_valueControl);

    if (!string.IsNullOrEmpty(LabelPrefix))
      _labelControl.SetText(LabelPrefix);

    _startTicks = Time.GetTicksMsec();
    UpdateDisplay(0, true);
    SetProcess(true);
  }

  public override void _Process(double delta)
  {
    ulong ticks = Time.GetTicksMsec();
    ulong deltaTicks = ticks >= _startTicks ? ticks - _startTicks : 0UL;
    int elapsedSeconds = (int)Mathf.Max(0, (long)(deltaTicks / 1000UL));
    if (elapsedSeconds == _lastWholeSeconds)
      return;
    UpdateDisplay(elapsedSeconds, false);
  }

  private void UpdateDisplay(int elapsedSeconds, bool force)
  {
    if (!force && elapsedSeconds == _lastWholeSeconds)
      return;

    _lastWholeSeconds = elapsedSeconds;
    var span = TimeSpan.FromSeconds(elapsedSeconds);
    string formatted = span.Hours > 0
      ? $"{span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}"
      : $"{span.Minutes:00}:{span.Seconds:00}";
    _valueControl.SetText(formatted);
    if (!force)
      _valueControl.Pulse(0.12f);
    bool showLabel = !string.IsNullOrEmpty(LabelPrefix);
    _labelControl.Visible = showLabel;
    if (showLabel)
      _labelControl.SetText(LabelPrefix);
    UpdateMinimumSize();
    QueueRedraw();
  }

  public override Vector2 _GetMinimumSize()
  {
    if (_layout != null)
    {
      Vector2 min = _layout.GetCombinedMinimumSize();
      float w = Mathf.Ceil(Mathf.Max(min.X, FontPx * 1.2f));
      float h = Mathf.Ceil(Mathf.Max(min.Y, FontPx));
      return new Vector2(w, h);
    }
    return new Vector2(FontPx * 1.2f, FontPx);
  }
}
