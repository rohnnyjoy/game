using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class GameUI : CanvasLayer
{
  public static GameUI Instance;
  [Export] public bool UseFullFrameShake { get; set; } = false;
  [Export] public bool DriveUiOffsetFallback { get; set; } = false;
  [Export(PropertyHint.Range, "0,100,1")] public int ScreenShakeSetting { get; set; } = 50; // Balatro default when not reduced_motion
  [Export] public bool ReducedMotion { get; set; } = false;
  [Export] public bool EnableUiShake { get; set; } = false;
  [Export] public RichTextLabel InteractionLabel;
  [Export] public RichTextLabel ComboLabel;
  [Export] public RichTextLabel MoneyCounter;  // Label for the banked money
  private MoneyComboUi _comboUi;
  private Control _bottomRight;
  private Control _crosshair;
  private MoneyTotalUi _totalUi;
  private InteractionPromptUi _interactionUi;
  private Container _bottomCenterContainer;
  private CanvasLayer _tooltipLayer;
  private TooltipUi _tooltipUi;
  private HealthUi _healthUi;
  private PrimaryWeaponStack _primaryModulesUi;
  private Control _topLeftHud;
  private string _activeInteractionText = string.Empty;
  private bool _interactionVisible;

  // Running values.
  private int moneyBank = 0;           // Finalized banked money.
  private int comboValue = 0;          // The current combo value (accumulated delta).
  private float currentDisplayedCombo = 0; // Used for smooth drain interpolation.
  private bool draining = false;       // Whether a drain coroutine is actively running.
  private Task drainTask = null;       // Reference to the active drain coroutine.

  // Visual properties.
  private Vector2 originalPosition;
  private Vector2 originalScale;

  // Timing settings.
  private const float drainDuration = 0.5f; // Duration of the drain animation.
  private const float drainDelay = 0.5f;    // Delay before starting the drain after a combo update.

  // Maximum scaling cap for the text label.
  private const float maxScaleFactor = 2.0f;

  // Screen shake state (Balatro-style)
  private float _jiggle = 0f; // corresponds to G.ROOM.jiggle; decays over time
  private Vector2 _shakeCurrentOffset = Vector2.Zero; // pixels
  private float _shakeCurrentRotation = 0f; // radians
  // Screen-space shader overlay to shift the whole frame (3D + UI) in pixels
  private CanvasLayer _shakeLayer;
  private ColorRect _shakeRect;
  private ShaderMaterial _shakeMaterial;
  // Dedicated layer to render crosshair above the shake overlay
  private CanvasLayer _crosshairLayer;
  private CenterContainer _crosshairCenter;
  private ProfilerOverlay _profiler;

  public override void _Ready()
  {
    // Prefer a maximized window (fills screen without true fullscreen)
    // Defer changing window mode to avoid add_child() during scene setup.
    CallDeferred(nameof(ApplyWindowMode));
    SetProcess(true);

    GlobalEvents.Instance.Connect(nameof(GlobalEvents.MoneyUpdated), new Callable(this, nameof(OnMoneyUpdated)));
    Instance = this;
    SetupUi();

    // Hide old labels if present
    if (ComboLabel != null) ComboLabel.Visible = false;
    if (MoneyCounter != null) MoneyCounter.Visible = false;
    if (InteractionLabel != null) InteractionLabel.Visible = false;

    // Create DynaText-based money combo counter anchored to BottomRight
    _bottomRight = GetNodeOrNull<Control>("UIRoot/BottomRight");
    _crosshair = GetNodeOrNull<Control>("UIRoot/Center/Crosshair");
    _bottomCenterContainer = GetNodeOrNull<Container>("UIRoot/BottomCenter/Center");
    _comboUi = new MoneyComboUi();
    _totalUi = new MoneyTotalUi();
    _interactionUi = new InteractionPromptUi();

    // Ensure a dedicated top-most CanvasLayer for tooltips exists before adding the tooltip UI.
    if (_tooltipLayer == null)
    {
      _tooltipLayer = new CanvasLayer();
      // Use a very high layer to ensure it renders above all other UI layers.
      _tooltipLayer.Layer = 200;
      AddChild(_tooltipLayer);
    }
    _tooltipUi = new TooltipUi
    {
      // Make ZIndex absolute and very high to ensure top draw order within the tooltip layer.
      ZAsRelative = false,
      ZIndex = 4096
    };
    // Build top-left HUD from a scene so layout/resources live outside code
    var topLeftHudScene = GD.Load<PackedScene>("res://ui/TopLeftHud.tscn");
    AddChild(_comboUi);
    AddChild(_totalUi);
    AddChild(_interactionUi);
    // Parent tooltip UI directly under the high-priority tooltip layer so it always draws on top
    _tooltipLayer.AddChild(_tooltipUi);

    if (topLeftHudScene != null)
    {
      _topLeftHud = topLeftHudScene.Instantiate<Control>();
      _topLeftHud.ZIndex = 4092;
      AddChild(_topLeftHud);
      _healthUi = _topLeftHud.GetNodeOrNull<HealthUi>("Health");
      _primaryModulesUi = _topLeftHud.GetNodeOrNull<PrimaryWeaponStack>("PrimaryWeaponStack");
      // Scene/resource are the source of truth for HUD layout; avoid runtime overrides here.
    }

    // Always-on lightweight profiler overlay (top-right)
    _profiler = new ProfilerOverlay();
    AddChild(_profiler);
    // Initialize health display early
    if (Player.Instance != null)
      _healthUi.SetHealth(Player.Instance.Health, Player.Instance.MaxHealth);
    else
      _healthUi.SetHealth(100, 100);
    // Defer attach to after layout so rects are valid
    CallDeferred(nameof(DeferredAttach));
    if (_bottomRight != null)
      _bottomRight.Connect("resized", new Callable(this, nameof(OnBottomRightResized)));
    if (_crosshair != null)
      _crosshair.Connect("resized", new Callable(this, nameof(OnCrosshairResized)));

  }

  private void ApplyWindowMode()
  {
    var win = GetWindow();
    if (win != null)
    {
      win.Mode = Window.ModeEnum.Maximized;
    }
  }

  private void DeferredAttach()
  {
    if (_crosshair != null && _comboUi != null)
      _comboUi.AttachToCrosshair(_crosshair);
    else if (_bottomRight != null && _comboUi != null)
      _comboUi.AttachToBottomRight(_bottomRight);
    if (_bottomRight != null && _totalUi != null)
      _totalUi.AttachToBottomRight(_bottomRight);
    if (_bottomCenterContainer != null && _interactionUi != null)
      _interactionUi.AttachTo(_bottomCenterContainer);
    if (_comboUi != null)
    {
      // CanvasItem z_index has an engine-defined max; use a safe high value.
      // The CanvasLayer already controls draw order; a modestly high ZIndex suffices.
      _comboUi.ZIndex = 4095;
    }
    if (_healthUi != null) _healthUi.ZIndex = 4093; // below combo/high overlays
    if (_primaryModulesUi != null) _primaryModulesUi.ZIndex = 4092;
    if (_totalUi != null)
    {
      _totalUi.ZIndex = 4094;
    }
    if (_interactionUi != null)
    {
      _interactionUi.ZIndex = 4096;
    }
    // Tooltip layer and tooltip are already set up in _Ready

    if (UseFullFrameShake)
    {
      // Create a screen-space shake overlay that offsets SCREEN_TEXTURE.
      // Draw it above normal UI/world but below the crosshair.
      if (_shakeLayer == null)
      {
        _shakeLayer = new CanvasLayer();
        _shakeLayer.Layer = 50; // above default UI
        AddChild(_shakeLayer);

        _shakeRect = new ColorRect();
        _shakeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _shakeRect.AnchorLeft = 0; _shakeRect.AnchorTop = 0; _shakeRect.AnchorRight = 1; _shakeRect.AnchorBottom = 1;
        _shakeRect.OffsetLeft = 0; _shakeRect.OffsetTop = 0; _shakeRect.OffsetRight = 0; _shakeRect.OffsetBottom = 0;
        _shakeRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _shakeRect.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        // Transparent base color so if material fails, it won't occlude the frame.
        _shakeRect.Color = new Color(1, 1, 1, 0);

      var shader = new Shader();
      shader.Code = @"shader_type canvas_item;
render_mode unshaded, screen_texture;
uniform vec2 shake_offset_px = vec2(0.0);
uniform float shake_rotation_rad = 0.0;
void fragment() {
    vec2 center = vec2(0.5);
    vec2 uv = SCREEN_UV - center;
    float c = cos(shake_rotation_rad);
    float s = sin(shake_rotation_rad);
    uv = mat2(c, -s, s, c) * uv;
    uv += center;
    uv += (shake_offset_px * SCREEN_PIXEL_SIZE);
    COLOR = texture(SCREEN_TEXTURE, uv);
}";
        _shakeMaterial = new ShaderMaterial();
        _shakeMaterial.Shader = shader;
        _shakeRect.Material = _shakeMaterial;
        _shakeLayer.AddChild(_shakeRect);
      }

      // Move crosshair (and the money combo) to a top-most CanvasLayer so they stay fixed while the frame shakes.
      if (_crosshairLayer == null)
      {
        _crosshairLayer = new CanvasLayer();
        _crosshairLayer.Layer = 99; // draw above shake overlay
        AddChild(_crosshairLayer);

        _crosshairCenter = new CenterContainer();
        _crosshairCenter.MouseFilter = Control.MouseFilterEnum.Ignore;
        _crosshairCenter.AnchorLeft = 0; _crosshairCenter.AnchorTop = 0; _crosshairCenter.AnchorRight = 1; _crosshairCenter.AnchorBottom = 1;
        _crosshairCenter.OffsetLeft = 0; _crosshairCenter.OffsetTop = 0; _crosshairCenter.OffsetRight = 0; _crosshairCenter.OffsetBottom = 0;
        _crosshairCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _crosshairCenter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _crosshairLayer.AddChild(_crosshairCenter);
      }

      if (_crosshair != null)
      {
        Node prevParent = _crosshair.GetParent();
        if (IsInstanceValid(prevParent))
          prevParent.CallDeferred(Node.MethodName.RemoveChild, _crosshair);
        _crosshairCenter.CallDeferred(Node.MethodName.AddChild, _crosshair);
      }

      // Also move the money combo to this layer so it remains locked to the crosshair during shake.
      if (_comboUi != null)
      {
        Node prevParent = _comboUi.GetParent();
        if (IsInstanceValid(prevParent))
          prevParent.CallDeferred(Node.MethodName.RemoveChild, _comboUi);
        _crosshairCenter.CallDeferred(Node.MethodName.AddChild, _comboUi);
        // After reparenting, re-attach to update position against the crosshair center in the new layer.
        Callable.From(() => _comboUi.AttachToCrosshair(_crosshair)).CallDeferred();
      }

      // Ensure profiler draws above shake overlay as well
      if (_profiler != null)
      {
        Node prevParent = _profiler.GetParent();
        if (IsInstanceValid(prevParent))
          prevParent.CallDeferred(Node.MethodName.RemoveChild, _profiler);
        _crosshairLayer.CallDeferred(Node.MethodName.AddChild, _profiler);
      }
    }
  }

  public void ShowTooltip(Control anchor, string text)
  {
    _tooltipUi?.ShowTooltip(anchor, text);
  }

  public void HideTooltip()
  {
    _tooltipUi?.HideTooltip();
  }

  public void SetHealth(float current, float max)
  {
    _healthUi?.SetHealth(current, max);
  }

  private void OnBottomRightResized()
  {
    if (_bottomRight != null && _comboUi != null)
      _comboUi.AttachToBottomRight(_bottomRight);
    if (_bottomRight != null && _totalUi != null)
      _totalUi.AttachToBottomRight(_bottomRight);
  }

  // Back-compat API: translate a pixel intensity into a jiggle pulse
  public void TriggerScreenShake(float durationSeconds, float intensityPixels)
  {
    if (!EnableUiShake) return;
    // Map roughly: 4-8px nudge -> ~0.5-1.0 jiggle
    float add = Mathf.Clamp(intensityPixels / 8f, 0.05f, 2.5f);
    AddJiggle(add);
  }

  public void AddJiggle(float amount)
  {
    if (!EnableUiShake) return;
    _jiggle += Mathf.Max(0f, amount);
  }

  public Vector2 GetScreenShakeOffset() => EnableUiShake ? _shakeCurrentOffset : Vector2.Zero;
  public float GetScreenShakeRotation() => EnableUiShake ? _shakeCurrentRotation : 0f;

  public override void _Process(double delta)
  {
    if (!EnableUiShake)
    {
      _jiggle = 0f;
      _shakeCurrentOffset = _shakeCurrentOffset.Lerp(Vector2.Zero, 0.8f);
      _shakeCurrentRotation = Mathf.Lerp(_shakeCurrentRotation, 0f, 0.8f);
      if (UseFullFrameShake && _shakeMaterial != null)
      {
        _shakeMaterial.SetShaderParameter("shake_offset_px", Vector2.Zero);
        _shakeMaterial.SetShaderParameter("shake_rotation_rad", 0f);
      }
      else
      {
        Offset = Vector2.Zero;
      }
      return;
    }

    // Balatro-equivalent easing/timing based on reference/balatro/functions/common_events.lua
    float dt = (float)delta;
    // Step 1: User setting scales maximum intensity; actual amplitude fades with _jiggle
    float setting = Mathf.Clamp(ScreenShakeSetting, 0, 100);
    float baseStrength = (ReducedMotion ? 0f : 1f) * (setting / 100f) * 3f;

    // Step 2: Decay jiggle smoothly over time (no ambient motion when jiggle ~ 0)
    _jiggle = Mathf.Max(0f, _jiggle * (1f - 5f * dt));

    // Effective amplitude tied to current jiggle level
    float amp = baseStrength * Mathf.Clamp(_jiggle, 0f, 1f);
    if (amp < 0.0005f) amp = 0f; // snap to rest to avoid lingering drift

    // Step 3: Compute offset and rotation using the same sinusoidal blend, scaled by amp
    float t = (float)Time.GetTicksMsec() / 1000f;
    // Rotation (slow + fast components)
    float rot = amp * (0.001f * Mathf.Sin(0.3f * t) + 0.002f * Mathf.Sin(39.913f * t));
    // Pixel dimensions
    Vector2 vpSize = GetViewport().GetVisibleRect().Size;
    float S = Mathf.Min(vpSize.X, vpSize.Y); // match Balatro room-based scaling more closely across aspects
    // Eased cursor term omitted (mouse captured); treat as centered, so delta=0
    float offsetX = amp * (0.015f * Mathf.Sin(0.913f * t) + 0.01f * Mathf.Sin(19.913f * t));
    float offsetY = amp * (0.015f * Mathf.Sin(0.952f * t) + 0.01f * Mathf.Sin(21.913f * t));
    Vector2 targetOffset = new Vector2(offsetX * S, offsetY * S);
    _shakeCurrentOffset = _shakeCurrentOffset.Lerp(targetOffset, 0.8f);
    _shakeCurrentRotation = Mathf.Lerp(_shakeCurrentRotation, rot, 0.8f);

    if (UseFullFrameShake && _shakeMaterial != null)
    {
      _shakeMaterial.SetShaderParameter("shake_offset_px", _shakeCurrentOffset);
      _shakeMaterial.SetShaderParameter("shake_rotation_rad", _shakeCurrentRotation);
    }
    else
    {
      // UI-only fallback if shader missing or disabled
      Offset = _shakeCurrentOffset;
    }
  }

  private void OnCrosshairResized()
  {
    if (_crosshair != null && _comboUi != null)
      _comboUi.AttachToCrosshair(_crosshair);
  }

  public void ShowInteractionLines(IReadOnlyList<string> lines)
  {
    if (_interactionUi == null) return;
    if (lines == null || lines.Count == 0)
    {
      HideInteractionText();
      return;
    }

    string joined = string.Join('\n', lines);
    bool textChanged = !_activeInteractionText.Equals(joined);
    if (textChanged)
    {
      _activeInteractionText = joined;
      _interactionUi.SetLines(lines);
    }

    if (!_interactionVisible)
    {
      _interactionUi.ShowPrompt();
      _interactionVisible = true;
    }
  }

  public void ShowInteractionText(string text)
  {
    if (string.IsNullOrEmpty(text))
    {
      HideInteractionText();
      return;
    }

    ShowInteractionLines(new[] { text });
  }

  public void HideInteractionText()
  {
    if (_interactionUi == null) return;
    if (!_interactionVisible)
      return;
    _interactionUi.HidePrompt();
    _interactionVisible = false;
    _activeInteractionText = string.Empty;
  }

  private void SetupUi()
  {
    if (ComboLabel != null)
    {
      originalPosition = ComboLabel.Position;
      originalScale = ComboLabel.Scale;
    }
    if (MoneyCounter != null)
    {
      MoneyCounter.Text = moneyBank.ToString();
    }
  }

  public async void OnMoneyUpdated(int oldAmount, int newAmount)
  {
    // Let MoneyComboUi drive visuals; keep legacy logic if desired
    int delta = newAmount - oldAmount;

    if (draining)
    {
      // Drain is in progress. Immediately finish the drain:
      // Bank the entire current combo.
      moneyBank += comboValue;
      if (MoneyCounter != null)
      {
        MoneyCounter.Text = moneyBank.ToString();
      }

      // Cancel the drain and wait for it to finish.
      draining = false;
      await drainTask;
      ResetComboMeterVisuals();

      // Start a new combo with only the new delta.
      comboValue = delta;
      currentDisplayedCombo = comboValue;
    }
    else
    {
      // Not draining yet, simply accumulate the new delta.
      comboValue = Mathf.RoundToInt(currentDisplayedCombo) + delta;
      currentDisplayedCombo = comboValue;
    }

    // Old label tweens removed; handled by DynaText pulses

    // Only start the drain delay if we're not in drain already.
    if (!draining)
    {
      await ToSignal(GetTree().CreateTimer(drainDelay), "timeout");
      if (!draining)
      {
        drainTask = DrainCombo();
        await drainTask;
      }
    }
  }

  // Coroutine that drains the combo meter from the current value down to 0.
  private async Task DrainCombo()
  {
    draining = true;
    float elapsed = 0f;
    int startValue = comboValue;  // Save the starting combo value.
    while (elapsed < drainDuration && draining)
    {
      float t = elapsed / drainDuration;
      // Smoothly interpolate currentDisplayedCombo from startValue to 0.
      currentDisplayedCombo = Mathf.Lerp(startValue, 0, t);
      UpdateDrainDisplay();
      await ToSignal(GetTree().CreateTimer(0.0167f), "timeout");  // ~60 FPS.
      elapsed += 0.0167f;
    }
    // If the drain completes normally (was not interrupted)
    if (draining)
    {
      currentDisplayedCombo = 0;
      UpdateDrainDisplay();
      moneyBank += startValue;
      draining = false;
      ResetComboMeterVisuals();
      if (MoneyCounter != null)
      {
        MoneyCounter.Text = moneyBank.ToString();
      }
    }
  }

  // Update the ComboLabel and MoneyCounter based on the current displayed combo value.
  private void UpdateDrainDisplay()
  {
    int remaining = Mathf.RoundToInt(currentDisplayedCombo);
    int drained = comboValue - remaining;
    if (ComboLabel != null)
    {
      ComboLabel.Text = remaining > 0 ? $"+{remaining}" : "0";
    }
    // While draining, MoneyCounter shows banked money plus what's been drained so far.
    if (MoneyCounter != null)
    {
      MoneyCounter.Text = (moneyBank + drained).ToString();
    }
  }

  // Reset the combo meter's visuals.
  private void ResetComboMeterVisuals()
  {
    comboValue = 0;
    currentDisplayedCombo = 0;
    if (ComboLabel != null)
    {
      ComboLabel.Text = "0";
      ComboLabel.Scale = originalScale;
      ComboLabel.Position = originalPosition;
      ComboLabel.Visible = false;
    }
  }

}
