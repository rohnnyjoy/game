using System;
using Godot;
using System.Globalization;
#nullable enable

public partial class DifficultyDisplay : Control
{
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx { get; set; } = 52;
  [Export] public Color TextColor { get; set; } = new Color(0.98f, 0.95f, 0.9f);
  [Export] public string LabelPrefix { get; set; } = "Difficulty";
  [Export] public string MultiplierFormat { get; set; } = "0.##";
  [Export(PropertyHint.Range, "0.5,2,0.01")] public float TextHeightScale { get; set; } = 0.85f;

  private DynaTextControl _textControl = default!;
  private string _lastValueText = string.Empty;
  private EnemySpawner? _spawner;
  private float _displayedDifficulty = -1f;
  private Callable _difficultyCallable;
  private Callable _nodeAddedCallable;
  private Callable _nodeRemovedCallable;

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
      TextHeightScale = TextHeightScale,
      LetterSpacingExtraPx = 1.0f,
      OffsetYExtraPx = 0f,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _textControl.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    _textControl.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    _textControl.CustomMinimumSize = Vector2.Zero;
    _textControl.SetColours(new System.Collections.Generic.List<Color> { TextColor });
    AddChild(_textControl);

    UpdateText(1.0f, force: true);

    _difficultyCallable = new Callable(this, nameof(OnDifficultyChanged));
    _nodeAddedCallable = new Callable(this, nameof(OnNodeAdded));
    _nodeRemovedCallable = new Callable(this, nameof(OnNodeRemoved));

    TryAttachSpawner();

    var tree = GetTree();
    if (tree != null)
    {
      tree.Connect(SceneTree.SignalName.NodeAdded, _nodeAddedCallable);
      tree.Connect(SceneTree.SignalName.NodeRemoved, _nodeRemovedCallable);
    }
  }

  public override void _ExitTree()
  {
    DetachSpawner();
    var tree = GetTree();
    if (tree != null)
    {
      if (tree.IsConnected(SceneTree.SignalName.NodeAdded, _nodeAddedCallable))
        tree.Disconnect(SceneTree.SignalName.NodeAdded, _nodeAddedCallable);
      if (tree.IsConnected(SceneTree.SignalName.NodeRemoved, _nodeRemovedCallable))
        tree.Disconnect(SceneTree.SignalName.NodeRemoved, _nodeRemovedCallable);
    }
    base._ExitTree();
  }

  private void TryAttachSpawner()
  {
    if (_spawner != null && IsInstanceValid(_spawner))
    {
      OnDifficultyChanged(_spawner.Difficulty);
      return;
    }

    var tree = GetTree();
    if (tree == null)
    {
      SetNoSpawnerState();
      return;
    }

    foreach (var node in tree.GetNodesInGroup("enemy_spawners"))
    {
      if (node is EnemySpawner spawner)
      {
        AttachSpawner(spawner);
        return;
      }
    }

    SetNoSpawnerState();
  }

  private void AttachSpawner(EnemySpawner spawner)
  {
    if (!IsInstanceValid(spawner))
    {
      SetNoSpawnerState();
      return;
    }

    if (_spawner == spawner)
    {
      OnDifficultyChanged(spawner.Difficulty);
      return;
    }

    DetachSpawner();

    _spawner = spawner;
    if (!_spawner.IsConnected(EnemySpawner.SignalName.DifficultyChanged, _difficultyCallable))
      _spawner.Connect(EnemySpawner.SignalName.DifficultyChanged, _difficultyCallable);

    Visible = true;
    OnDifficultyChanged(_spawner.Difficulty);
  }

  private void DetachSpawner()
  {
    if (_spawner != null && IsInstanceValid(_spawner))
    {
      if (_spawner.IsConnected(EnemySpawner.SignalName.DifficultyChanged, _difficultyCallable))
        _spawner.Disconnect(EnemySpawner.SignalName.DifficultyChanged, _difficultyCallable);
    }
    _spawner = null;
  }

  private void SetNoSpawnerState()
  {
    Visible = false;
  }

  private void OnDifficultyChanged(float value)
  {
    UpdateText(value);
  }

  private void OnNodeAdded(Node node)
  {
    if (_spawner != null && IsInstanceValid(_spawner))
      return;

    if (node is EnemySpawner spawner)
      AttachSpawner(spawner);
  }

  private void OnNodeRemoved(Node node)
  {
    if (_spawner == null)
      return;

    if (node == _spawner)
    {
      DetachSpawner();
      TryAttachSpawner();
    }
  }

  private void UpdateText(float rawDifficulty, bool force = false)
  {
    float value = MathF.Max(0.0f, rawDifficulty);
    if (!force && _displayedDifficulty >= 0.0f && Mathf.IsEqualApprox(value, _displayedDifficulty))
      return;

    _displayedDifficulty = value;
    string formatted = value.ToString(MultiplierFormat, CultureInfo.InvariantCulture);
    string valueText = $"x{formatted}";
    string prefix = string.IsNullOrEmpty(LabelPrefix) ? string.Empty : $"{LabelPrefix} ";
    string combined = prefix + valueText;
    _textControl.SetText(combined);
    if (!force && !_lastValueText.Equals(valueText, StringComparison.Ordinal))
    {
      int prefixGlyphs = new StringInfo(prefix).LengthInTextElements;
      int valueGlyphs = new StringInfo(valueText).LengthInTextElements;
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
      var min = _textControl.GetMinimumSize();
      float width = Mathf.Ceil(Mathf.Max(min.X, 0f));
      float height = Mathf.Ceil(Mathf.Max(min.Y, FontPx * 0.9f));
      return new Vector2(width, height);
    }
    return new Vector2(FontPx, FontPx * 0.9f);
  }
}
