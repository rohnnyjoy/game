using System;
using System.Globalization;
using Godot;
#nullable enable

public partial class KillCountDisplay : Control
{
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx { get; set; } = 47;
  [Export] public Color TextColor { get; set; } = new Color(1f, 0.55f, 0.55f);
  [Export] public string LabelPrefix { get; set; } = "Kills";

  private DynaTextControl _textControl = default!;
  private string _lastValueText = string.Empty;
  private int _killCount = 0;
  private bool _connected;
  private Callable _enemyDiedCallable;

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
    UpdateDisplay(true);

    _enemyDiedCallable = new Callable(this, nameof(OnEnemyDied));
    TryConnect();
  }

  public override void _ExitTree()
  {
    if (_connected && GlobalEvents.Instance != null)
    {
      if (GlobalEvents.Instance.IsConnected(nameof(GlobalEvents.EnemyDied), _enemyDiedCallable))
        GlobalEvents.Instance.Disconnect(nameof(GlobalEvents.EnemyDied), _enemyDiedCallable);
    }
    base._ExitTree();
  }

  private void TryConnect()
  {
    if (GlobalEvents.Instance == null)
    {
      CallDeferred(nameof(TryConnect));
      return;
    }

    if (!GlobalEvents.Instance.IsConnected(nameof(GlobalEvents.EnemyDied), _enemyDiedCallable))
      GlobalEvents.Instance.Connect(nameof(GlobalEvents.EnemyDied), _enemyDiedCallable);
    _connected = true;
  }

  private void OnEnemyDied()
  {
    _killCount = Mathf.Max(0, _killCount + 1);
    UpdateDisplay(false);
  }

  private void UpdateDisplay(bool force)
  {
    string valueText = _killCount.ToString();
    string prefix = string.IsNullOrEmpty(LabelPrefix) ? string.Empty : $"{LabelPrefix} ";
    string combined = prefix + valueText;
    _textControl.SetText(combined);
    if (!force && !_lastValueText.Equals(valueText, System.StringComparison.Ordinal))
    {
      int prefixGlyphs = new System.Globalization.StringInfo(prefix).LengthInTextElements;
      int valueGlyphs = new System.Globalization.StringInfo(valueText).LengthInTextElements;
      _textControl.PulseRange(prefixGlyphs, valueGlyphs);
    }
    _lastValueText = valueText;
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
