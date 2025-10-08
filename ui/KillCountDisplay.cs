using Godot;
#nullable enable

public partial class KillCountDisplay : Control
{
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx { get; set; } = 52;
  [Export] public Color TextColor { get; set; } = new Color(1f, 0.55f, 0.55f);
  [Export] public string LabelPrefix { get; set; } = "Kills";

  private HBoxContainer _layout = default!;
  private DynaTextControl _labelControl = default!;
  private DynaTextControl _valueControl = default!;
  private int _killCount = 0;
  private bool _connected;
  private Callable _enemyDiedCallable;

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
    if (!string.IsNullOrEmpty(LabelPrefix))
      _labelControl.SetText(LabelPrefix);
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
    _valueControl.SetText(_killCount.ToString());
    if (!force)
      _valueControl.Pulse(0.18f);
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
      float w = Mathf.Ceil(Mathf.Max(min.X, FontPx));
      float h = Mathf.Ceil(Mathf.Max(min.Y, FontPx));
      return new Vector2(w, h);
    }
    return new Vector2(FontPx, FontPx);
  }
}
