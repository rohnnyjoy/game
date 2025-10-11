using System;
using System.Collections.Generic;
using Godot;

public partial class PauseMenu : CanvasLayer
{
  private Button _resumeButton;
  private Button _exitButton;
  private Control _root;
  private Input.MouseModeEnum _previousMouseMode = Input.MouseModeEnum.Captured;
  private DynaTextControl _resumeLabel;
  private DynaTextControl _exitLabel;
  private Button _optionsButton;
  private Button _optionsBackButton;
  private DynaTextControl _optionsLabel;
  private DynaTextControl _backLabel;
  private VBoxContainer _mainMenuContainer;
  private VBoxContainer _optionsContainer;
  private OptionButton _fpsOptionButton;
  private OptionButton _vsyncOptionButton;
  private OptionButton _fullscreenOptionButton;
  private bool _suppressOptionSignals = false;
  private readonly List<int> _currentFpsValues = new();

  private readonly (int value, string text)[] _fpsOptions = new (int, string)[]
  {
    (0, "Unlimited"),
    (30, "30 FPS"),
    (60, "60 FPS"),
    (75, "75 FPS"),
    (90, "90 FPS"),
    (120, "120 FPS"),
    (144, "144 FPS"),
    (165, "165 FPS"),
    (240, "240 FPS"),
    (360, "360 FPS")
  };

  private bool OptionsVisible => _optionsContainer != null && _optionsContainer.Visible;

  private static readonly Texture2D FrameTexture = GD.Load<Texture2D>("res://assets/ui/3x/ninepatch.png");
  private const int FramePatchMargin = 18;
  private const float PanelContentPadding = 32f;
  private const float ButtonMinWidth = 324f;
  private static readonly Color _buttonHighlight = new Color(1f, 0.88f, 0.54f);

  public static PauseMenu Instance { get; private set; }

  public bool IsOpen => Visible;

  public override void _EnterTree()
  {
    base._EnterTree();
    Instance = this;
  }

  public override void _ExitTree()
  {
    base._ExitTree();
    UserSettings.MaxFpsChanged -= OnMaxFpsChanged;
    UserSettings.VsyncChanged -= OnVsyncChanged;
    UserSettings.FullscreenChanged -= OnFullscreenChanged;
    if (Instance == this)
      Instance = null;
  }

  public override void _Ready()
  {
    Name = nameof(PauseMenu);
    Layer = 120;
    ProcessMode = ProcessModeEnum.Always;
    EnsurePauseAction();
    BuildUi();
    UserSettings.MaxFpsChanged += OnMaxFpsChanged;
    UserSettings.VsyncChanged += OnVsyncChanged;
    UserSettings.FullscreenChanged += OnFullscreenChanged;
    Visible = false;
  }

  private void BuildUi()
  {
    _root = new Control
    {
      Name = "Root",
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
    _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    _root.OffsetLeft = 0;
    _root.OffsetTop = 0;
    _root.OffsetRight = 0;
    _root.OffsetBottom = 0;
    AddChild(_root);

    var overlay = new ColorRect
    {
      Name = "Dimmer",
      Color = new Color(0f, 0f, 0f, 0.6f),
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
    overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    overlay.OffsetLeft = 0;
    overlay.OffsetTop = 0;
    overlay.OffsetRight = 0;
    overlay.OffsetBottom = 0;
    _root.AddChild(overlay);

    var center = new CenterContainer
    {
      Name = "Center",
      MouseFilter = Control.MouseFilterEnum.Pass
    };
    center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    center.OffsetLeft = 0;
    center.OffsetTop = 0;
    center.OffsetRight = 0;
    center.OffsetBottom = 0;
    _root.AddChild(center);

    var panel = new PanelContainer
    {
      Name = "Panel",
      MouseFilter = Control.MouseFilterEnum.Pass,
      CustomMinimumSize = new Vector2(486, 306)
    };
    ApplyPanelStyle(panel);
    center.AddChild(panel);

    var layout = new VBoxContainer
    {
      Name = "Layout",
      Alignment = BoxContainer.AlignmentMode.Center,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };
    layout.AddThemeConstantOverride("separation", 0);
    layout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    panel.AddChild(layout);

    _mainMenuContainer = new VBoxContainer
    {
      Name = "MainMenu",
      Alignment = BoxContainer.AlignmentMode.Center,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };
    _mainMenuContainer.AddThemeConstantOverride("separation", 24);
    layout.AddChild(_mainMenuContainer);

    var title = CreateHeaderText("Paused");
    _mainMenuContainer.AddChild(title);

    var resume = CreateMenuButton("Resume", OnResumePressed);
    _resumeButton = resume.button;
    _resumeLabel = resume.label;
    _mainMenuContainer.AddChild(_resumeButton);

    var options = CreateMenuButton("Options", OnOptionsPressed);
    _optionsButton = options.button;
    _optionsLabel = options.label;
    _mainMenuContainer.AddChild(_optionsButton);

    var exitButton = CreateMenuButton("Exit Game", OnExitPressed);
    _exitButton = exitButton.button;
    _exitLabel = exitButton.label;
    _mainMenuContainer.AddChild(_exitButton);

    var hint = CreateSubHeaderText("Press Esc to resume");
    _mainMenuContainer.AddChild(hint);

    _optionsContainer = new VBoxContainer
    {
      Name = "OptionsMenu",
      Alignment = BoxContainer.AlignmentMode.Center,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill,
      Visible = false
    };
    _optionsContainer.AddThemeConstantOverride("separation", 24);
    layout.AddChild(_optionsContainer);

    var optionsTitle = CreateHeaderText("Options");
    _optionsContainer.AddChild(optionsTitle);

    var videoControls = BuildVideoControls();
    _optionsContainer.AddChild(videoControls);

    var optionsHint = CreateSubHeaderText("Press Esc to return");
    _optionsContainer.AddChild(optionsHint);

    var backButton = CreateMenuButton("Back", OnOptionsBackPressed);
    _optionsBackButton = backButton.button;
    _backLabel = backButton.label;
    _optionsContainer.AddChild(_optionsBackButton);

    RefreshVideoOptionsFromSettings();
  }

  private void ApplyPanelStyle(PanelContainer panel)
  {
    var style = new StyleBoxTexture
    {
      Texture = FrameTexture,
      AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
      AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
      DrawCenter = true
    };
    style.TextureMarginLeft = FramePatchMargin;
    style.TextureMarginRight = FramePatchMargin;
    style.TextureMarginTop = FramePatchMargin;
    style.TextureMarginBottom = FramePatchMargin;
    style.ContentMarginLeft = PanelContentPadding;
    style.ContentMarginRight = PanelContentPadding;
    style.ContentMarginTop = PanelContentPadding;
    style.ContentMarginBottom = PanelContentPadding;
    panel.AddThemeStyleboxOverride("panel", style);
  }

  private DynaTextControl CreateHeaderText(string text)
  {
    var control = CreateDynaLabel(text, 88, 1f, uppercase: true, ambientFloat: true);
    control.Name = "Title";
    return control;
  }

  private DynaTextControl CreateSubHeaderText(string text)
  {
    var control = CreateDynaLabel(text, 38, 0.78f, uppercase: false, ambientFloat: false);
    control.Name = "Hint";
    return control;
  }

  private DynaTextControl CreateDynaLabel(string text, int fontPx, float alpha, bool uppercase, bool ambientFloat)
  {
    string display = uppercase ? text.ToUpperInvariant() : text;
    var label = new DynaTextControl
    {
      FontPx = fontPx,
      Shadow = true,
      ShadowAlpha = 0.38f,
      UseShadowParallax = true,
      CenterInRect = true,
      AmbientFloat = ambientFloat,
      AmbientRotate = true,
      AmbientBump = false,
      LetterSpacingExtraPx = 0f,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
    label.SetColours(new List<Color> { new Color(1f, 1f, 1f, alpha) });
    label.SetText(display);
    return label;
  }

  private (Button button, DynaTextControl label) CreateMenuButton(string text, Action onPressed)
  {
    var button = new Button
    {
      Name = $"{text.Replace(" ", string.Empty)}Button",
      FocusMode = Control.FocusModeEnum.All,
      MouseFilter = Control.MouseFilterEnum.Stop,
      CustomMinimumSize = new Vector2(ButtonMinWidth, 80f),
      Text = string.Empty,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };
    button.ClipContents = false;
    ApplyButtonStyle(button);

    var label = new DynaTextControl
    {
      Name = "Label",
      FontPx = 49,
      Shadow = true,
      ShadowAlpha = 0.35f,
      UseShadowParallax = true,
      CenterInRect = true,
      AmbientFloat = true,
      AmbientRotate = true,
      AmbientBump = false,
      LetterSpacingExtraPx = 0f,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill,
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
    var normalColours = new List<Color> { Colors.White };
    var highlightColours = new List<Color> { _buttonHighlight };
    label.SetColours(normalColours);
    label.SetText(text.ToUpperInvariant());
    button.AddChild(label);
    label.SetAnchorsPreset(Control.LayoutPreset.FullRect);

    if (onPressed != null)
      button.Pressed += onPressed;

    bool hovered = false;
    bool focused = false;
    bool highlightActive = false;
    void UpdateHighlight()
    {
      bool active = hovered || focused;
      if (active == highlightActive)
        return;
      highlightActive = active;
      label.SetColours(active ? highlightColours : normalColours);
      if (active)
        label.Pulse(0.18f);
    }

    button.MouseEntered += () =>
    {
      hovered = true;
      if (!button.HasFocus())
        button.GrabFocus();
      UpdateHighlight();
    };
    button.MouseExited += () => { hovered = false; UpdateHighlight(); };
    button.FocusEntered += () => { focused = true; UpdateHighlight(); };
    button.FocusExited += () => { focused = false; UpdateHighlight(); };
    button.VisibilityChanged += () =>
    {
      if (button.Visible)
        return;
      hovered = false;
      focused = false;
      highlightActive = false;
      label.SetColours(normalColours);
    };

    return (button, label);
  }

  private Control BuildVideoControls()
  {
    var container = new VBoxContainer
    {
      Name = "VideoContainer",
      Alignment = BoxContainer.AlignmentMode.Center,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };
    container.AddThemeConstantOverride("separation", 12);

    var label = CreateSubHeaderText("FPS Cap");
    label.Name = "FpsLabel";
    container.AddChild(label);

    _fpsOptionButton = new OptionButton
    {
      Name = "FpsOptionButton",
      FocusMode = Control.FocusModeEnum.All,
      MouseFilter = Control.MouseFilterEnum.Stop,
      CustomMinimumSize = new Vector2(ButtonMinWidth, 68f),
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };
    ApplyOptionButtonStyle(_fpsOptionButton);
    _fpsOptionButton.ItemSelected += OnFpsSelectionChanged;
    container.AddChild(_fpsOptionButton);

    var vsyncLabel = CreateSubHeaderText("VSync");
    vsyncLabel.Name = "VsyncLabel";
    container.AddChild(vsyncLabel);

    _vsyncOptionButton = new OptionButton
    {
      Name = "VsyncOptionButton",
      FocusMode = Control.FocusModeEnum.All,
      MouseFilter = Control.MouseFilterEnum.Stop,
      CustomMinimumSize = new Vector2(ButtonMinWidth, 68f),
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };
    ApplyOptionButtonStyle(_vsyncOptionButton);
    _vsyncOptionButton.AddItem("Enabled");
    _vsyncOptionButton.AddItem("Disabled");
    _vsyncOptionButton.ItemSelected += OnVsyncSelectionChanged;
    container.AddChild(_vsyncOptionButton);

    var fullscreenLabel = CreateSubHeaderText("Fullscreen");
    fullscreenLabel.Name = "FullscreenLabel";
    container.AddChild(fullscreenLabel);

    _fullscreenOptionButton = new OptionButton
    {
      Name = "FullscreenOptionButton",
      FocusMode = Control.FocusModeEnum.All,
      MouseFilter = Control.MouseFilterEnum.Stop,
      CustomMinimumSize = new Vector2(ButtonMinWidth, 68f),
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };
    ApplyOptionButtonStyle(_fullscreenOptionButton);
    _fullscreenOptionButton.AddItem("Enabled");
    _fullscreenOptionButton.AddItem("Disabled");
    _fullscreenOptionButton.ItemSelected += OnFullscreenSelectionChanged;
    container.AddChild(_fullscreenOptionButton);

    return container;
  }

  private void ApplyButtonStyle(Button button)
  {
    button.AddThemeStyleboxOverride("normal", CreateButtonStyle());
    button.AddThemeStyleboxOverride("hover", CreateButtonStyle());
    button.AddThemeStyleboxOverride("pressed", CreateButtonStyle());
    button.AddThemeStyleboxOverride("focus", CreateButtonStyle());
  }

  private void ApplyOptionButtonStyle(OptionButton option)
  {
    option.AddThemeStyleboxOverride("normal", CreateButtonStyle());
    option.AddThemeStyleboxOverride("hover", CreateButtonStyle());
    option.AddThemeStyleboxOverride("pressed", CreateButtonStyle());
    option.AddThemeStyleboxOverride("focus", CreateButtonStyle());
    option.AddThemeStyleboxOverride("disabled", CreateButtonStyle());
  }

  private StyleBoxTexture CreateButtonStyle()
  {
    var style = new StyleBoxTexture
    {
      Texture = FrameTexture,
      AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
      AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
      DrawCenter = true
    };
    style.TextureMarginLeft = FramePatchMargin;
    style.TextureMarginRight = FramePatchMargin;
    style.TextureMarginTop = FramePatchMargin;
    style.TextureMarginBottom = FramePatchMargin;
    style.ContentMarginLeft = PanelContentPadding * 0.5f;
    style.ContentMarginRight = PanelContentPadding * 0.5f;
    style.ContentMarginTop = 18f;
    style.ContentMarginBottom = 18f;
    return style;
  }

  private void RefreshVideoOptionsFromSettings()
  {
    if (_fpsOptionButton == null)
      return;

    _suppressOptionSignals = true;
    _fpsOptionButton.Clear();
    _currentFpsValues.Clear();

    for (int i = 0; i < _fpsOptions.Length; i++)
    {
      var option = _fpsOptions[i];
      _fpsOptionButton.AddItem(option.text);
      _currentFpsValues.Add(option.value);
    }

    UserSettings.EnsureLoaded();
    int current = UserSettings.MaxFps;
    int index = FindFpsOptionIndex(current);
    if (index >= 0)
    {
      _fpsOptionButton.Select(index);
    }
    else if (current > 0)
    {
      string label = $"{current} FPS (Custom)";
      _fpsOptionButton.AddItem(label);
      _currentFpsValues.Add(current);
      _fpsOptionButton.Select(_fpsOptionButton.GetItemCount() - 1);
    }
    else
    {
      _fpsOptionButton.Select(0);
    }

    if (_vsyncOptionButton != null)
    {
      bool vsyncEnabled = UserSettings.VsyncEnabled;
      _vsyncOptionButton.Select(vsyncEnabled ? 0 : 1);
    }

    if (_fullscreenOptionButton != null)
    {
      bool fullscreenEnabled = UserSettings.FullscreenEnabled;
      _fullscreenOptionButton.Select(fullscreenEnabled ? 0 : 1);
    }

    _suppressOptionSignals = false;
  }

  private int FindFpsOptionIndex(int value)
  {
    for (int i = 0; i < _fpsOptions.Length; i++)
    {
      if (_fpsOptions[i].value == value)
        return i;
    }
    return -1;
  }

  private int GetFpsValue(int index)
  {
    if (index < 0 || index >= _currentFpsValues.Count)
      return 0;
    return _currentFpsValues[index];
  }

  private void ResetButtonHighlights()
  {
    if (_resumeLabel != null)
      _resumeLabel.SetColours(new List<Color> { Colors.White });
    if (_exitLabel != null)
      _exitLabel.SetColours(new List<Color> { Colors.White });
    if (_optionsLabel != null)
      _optionsLabel.SetColours(new List<Color> { Colors.White });
    if (_backLabel != null)
      _backLabel.SetColours(new List<Color> { Colors.White });
  }

  private void OnFpsSelectionChanged(long index)
  {
    if (_suppressOptionSignals)
      return;
    int value = GetFpsValue((int)index);
    UserSettings.SetMaxFps(value);
    RefreshVideoOptionsFromSettings();
  }

  private void OnMaxFpsChanged(int value)
  {
    if (!IsInsideTree())
      return;
    RefreshVideoOptionsFromSettings();
  }

  private void OnVsyncSelectionChanged(long index)
  {
    if (_suppressOptionSignals)
      return;
    bool enabled = index == 0;
    UserSettings.SetVsyncEnabled(enabled);
    RefreshVideoOptionsFromSettings();
  }

  private void OnVsyncChanged(bool enabled)
  {
    if (!IsInsideTree())
      return;
    RefreshVideoOptionsFromSettings();
  }

  private void OnFullscreenSelectionChanged(long index)
  {
    if (_suppressOptionSignals)
      return;
    bool enabled = index == 0;
    UserSettings.SetFullscreenEnabled(enabled);
    RefreshVideoOptionsFromSettings();
  }

  private void OnFullscreenChanged(bool enabled)
  {
    if (!IsInsideTree())
      return;
    RefreshVideoOptionsFromSettings();
  }

  private void OnOptionsPressed()
  {
    ShowOptionsMenu();
  }

  private void OnOptionsBackPressed()
  {
    ShowMainMenu();
    _optionsButton?.GrabFocus();
  }

  private void ShowMainMenu()
  {
    if (_mainMenuContainer != null)
      _mainMenuContainer.Visible = true;
    if (_optionsContainer != null)
      _optionsContainer.Visible = false;
    ResetButtonHighlights();
  }

  private void ShowOptionsMenu()
  {
    if (_mainMenuContainer != null)
      _mainMenuContainer.Visible = false;
    if (_optionsContainer != null)
      _optionsContainer.Visible = true;
    ResetButtonHighlights();
    RefreshVideoOptionsFromSettings();
    _fpsOptionButton?.GrabFocus();
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    base._UnhandledInput(@event);
    if (!IsPauseToggleEvent(@event))
      return;
    if (IsOpen && OptionsVisible)
    {
      ShowMainMenu();
      _optionsButton?.GrabFocus();
      GetViewport()?.SetInputAsHandled();
      return;
    }
    ToggleMenu();
    GetViewport()?.SetInputAsHandled();
  }

  public void ToggleFromWorld()
  {
    ToggleMenu();
  }

  private void ToggleMenu()
  {
    if (IsOpen)
      CloseMenu();
    else
      OpenMenu();
  }

  private bool IsPauseToggleEvent(InputEvent @event)
  {
    if (@event.IsActionPressed("pause_menu"))
      return true;
    if (@event.IsActionPressed("ui_cancel"))
      return true;
    if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
      return true;
    return false;
  }

  private void OpenMenu()
  {
    _previousMouseMode = Input.MouseMode;
    CloseInventoryIfOpen();
    ShowMainMenu();
    Visible = true;
    GlobalEvents.Instance?.SetMenuOpen(true);
    Input.MouseMode = Input.MouseModeEnum.Visible;
    _resumeButton?.GrabFocus();
  }

  private void CloseMenu()
  {
    ShowMainMenu();
    Visible = false;
    _resumeButton?.ReleaseFocus();
    _exitButton?.ReleaseFocus();
    _optionsButton?.ReleaseFocus();
    _optionsBackButton?.ReleaseFocus();
    _fpsOptionButton?.ReleaseFocus();
    _vsyncOptionButton?.ReleaseFocus();
    _fullscreenOptionButton?.ReleaseFocus();
    ResetButtonHighlights();
    bool inventoryStillOpen = IsInventoryVisible();
    if (!inventoryStillOpen)
    {
      GlobalEvents.Instance?.SetMenuOpen(false);
      Input.MouseMode = _previousMouseMode;
    }
    else
    {
      Input.MouseMode = Input.MouseModeEnum.Visible;
    }
  }

  private void OnResumePressed()
  {
    CloseMenu();
  }

  private void OnExitPressed()
  {
    CloseMenu();
    GetTree().Quit();
  }

  private void CloseInventoryIfOpen()
  {
    var menuCanvas = FindMenuCanvas();
    if (menuCanvas == null)
      return;
    if ((bool)menuCanvas.Call("is_inventory_visible"))
      menuCanvas.Call("close_inventory_if_open");
  }

  private bool IsInventoryVisible()
  {
    var menuCanvas = FindMenuCanvas();
    if (menuCanvas == null)
      return false;
    return (bool)menuCanvas.Call("is_inventory_visible");
  }

  private CanvasLayer FindMenuCanvas()
  {
    var root = GetTree()?.Root;
    if (root == null)
      return null;
    return root.FindChild("MenuCanvas", recursive: true, owned: false) as CanvasLayer;
  }

  private void EnsurePauseAction()
  {
    if (InputMap.HasAction("pause_menu"))
      return;
    InputMap.AddAction("pause_menu");
    var escapeEvent = new InputEventKey
    {
      Keycode = Key.Escape
    };
    InputMap.ActionAddEvent("pause_menu", escapeEvent);
  }
}
