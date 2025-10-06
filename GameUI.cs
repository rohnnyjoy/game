using Godot;
using System.Threading.Tasks;

public partial class GameUi : CanvasLayer
{
  public static GameUi Instance;
  [Export] public RichTextLabel InteractionLabel;
  [Export] public RichTextLabel ComboLabel;
  [Export] public RichTextLabel MoneyCounter;  // Label for the banked money
  private MoneyComboUi _comboUi;
  private Control _bottomRight;
  private Control _crosshair;
  private MoneyTotalUi _totalUi;
  private InteractionPromptUi _interactionUi;
  private Container _bottomCenterContainer;

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

  // No screen shake state here; screen shake handled elsewhere

  public override void _Ready()
  {
    // Prefer a maximized window (fills screen without true fullscreen)
    GetWindow().Mode = Window.ModeEnum.Maximized;

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
    AddChild(_comboUi);
    AddChild(_totalUi);
    AddChild(_interactionUi);
    // Defer attach to after layout so rects are valid
    CallDeferred(nameof(DeferredAttach));
    if (_bottomRight != null)
      _bottomRight.Connect("resized", new Callable(this, nameof(OnBottomRightResized)));
    if (_crosshair != null)
      _crosshair.Connect("resized", new Callable(this, nameof(OnCrosshairResized)));

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
    if (_totalUi != null)
    {
      _totalUi.ZIndex = 4094;
    }
    if (_interactionUi != null)
    {
      _interactionUi.ZIndex = 4096;
    }
  }

  private void OnBottomRightResized()
  {
    if (_bottomRight != null && _comboUi != null)
      _comboUi.AttachToBottomRight(_bottomRight);
    if (_bottomRight != null && _totalUi != null)
      _totalUi.AttachToBottomRight(_bottomRight);
  }

  private void OnCrosshairResized()
  {
    if (_crosshair != null && _comboUi != null)
      _comboUi.AttachToCrosshair(_crosshair);
  }

  public void ShowInteractionText(string text)
  {
    if (_interactionUi == null) return;
    _interactionUi.SetText(text);
    _interactionUi.ShowPrompt();
  }

  public void HideInteractionText()
  {
    if (_interactionUi == null) return;
    _interactionUi.HidePrompt();
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
