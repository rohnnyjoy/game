using System;
using System.Globalization;
using Godot;
#nullable enable

public partial class RunTimerDisplay : Control
{
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx { get; set; } = 52;
  [Export] public Color TextColor { get; set; } = new Color(0.7f, 0.85f, 1f);
  [Export] public string LabelPrefix { get; set; } = "Time";

  private DynaTextControl _textControl = default!;
  private string _lastValueText = string.Empty;
  private ulong _startTicks;
  private int _lastWholeSeconds = -1;

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Ignore;
    SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    SizeFlagsVertical = SizeFlags.ShrinkBegin;
    CustomMinimumSize = Vector2.Zero;

    _textControl = new DynaTextControl
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
    _textControl.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    _textControl.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    _textControl.CustomMinimumSize = Vector2.Zero;
    _textControl.SetColours(new System.Collections.Generic.List<Color> { TextColor });
    AddChild(_textControl);

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
    string prefix = string.IsNullOrEmpty(LabelPrefix) ? string.Empty : $"{LabelPrefix} ";
    string combined = prefix + formatted;
    _textControl.SetText(combined);
    _lastValueText = formatted;
    UpdateMinimumSize();
    QueueRedraw();
  }

  public override Vector2 _GetMinimumSize()
  {
    if (_textControl != null)
    {
      Vector2 min = _textControl.GetMinimumSize();
      float w = Mathf.Ceil(Mathf.Max(min.X, 0f));
      float h = Mathf.Ceil(Mathf.Max(min.Y, FontPx));
      return new Vector2(w, h);
    }
    return new Vector2(FontPx, FontPx);
  }
}
