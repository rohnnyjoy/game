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

  private HBoxContainer _layout = default!;
  private DynaTextControl _labelControl = default!;
  private DynaTextControl _valueControl = default!;
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
      TextHeightScale = TextHeightScale,
      LetterSpacingExtraPx = 0.5f,
      OffsetYExtraPx = 0f,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _labelControl.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    _labelControl.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    _labelControl.SetColours(new System.Collections.Generic.List<Color> { TextColor });
    _labelControl.SetText(string.IsNullOrEmpty(LabelPrefix) ? string.Empty : $"{LabelPrefix}");
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
      TextHeightScale = TextHeightScale,
      LetterSpacingExtraPx = 1.0f,
      OffsetYExtraPx = 0f,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _valueControl.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    _valueControl.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    _valueControl.SetColours(new System.Collections.Generic.List<Color> { TextColor });
    _layout.AddChild(_valueControl);

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
    _valueControl.SetText(valueText);
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
      var min = _layout.GetCombinedMinimumSize();
      float width = Mathf.Ceil(Mathf.Max(min.X, FontPx));
      float height = Mathf.Ceil(Mathf.Max(min.Y, FontPx * 0.9f));
      return new Vector2(width, height);
    }
    return new Vector2(FontPx, FontPx);
  }
}
