using Godot;
using System.Threading.Tasks;

public partial class GameUi : CanvasLayer
{
  public static GameUi Instance;
  [Export] public RichTextLabel InteractionLabel;
  [Export] public RichTextLabel ComboLabel;
  [Export] public RichTextLabel MoneyCounter;  // Label for the banked money

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

  public override void _Ready()
  {
    GlobalEvents.Instance.Connect("MoneyUpdated", new Callable(this, nameof(OnMoneyUpdated)));
    Instance = this;
  }

  private void SetupUi()
  {
    originalPosition = ComboLabel.Position;
    originalScale = ComboLabel.Scale;
    MoneyCounter.Text = moneyBank.ToString();
  }

  public async void OnMoneyUpdated(int oldAmount, int newAmount)
  {
    int delta = newAmount - oldAmount;
    GD.Print($"Money updated: delta = {delta}");

    if (draining)
    {
      // Drain is in progress. Immediately finish the drain:
      // Bank the entire current combo.
      moneyBank += comboValue;
      MoneyCounter.Text = moneyBank.ToString();

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

    GD.Print($"New comboValue = {comboValue}");
    ComboLabel.Text = $"+{comboValue}";
    ComboLabel.Visible = true;

    // Pop & shake effects using tweens:
    Tween scaleTween = CreateTween();

    // Calculate the target scale and cap it using maxScaleFactor.
    Vector2 targetScale = originalScale * 1.5f;
    targetScale.X = Mathf.Min(targetScale.X, originalScale.X * maxScaleFactor);
    targetScale.Y = Mathf.Min(targetScale.Y, originalScale.Y * maxScaleFactor);

    scaleTween.TweenProperty(ComboLabel, "scale", targetScale, 0.2f)
              .SetTrans(Tween.TransitionType.Back)
              .SetEase(Tween.EaseType.Out);
    scaleTween.TweenProperty(ComboLabel, "scale", originalScale, 0.2f)
              .SetTrans(Tween.TransitionType.Back)
              .SetEase(Tween.EaseType.In);

    Tween shakeTween = CreateTween();
    shakeTween.TweenProperty(ComboLabel, "position", originalPosition + new Vector2(5, 0), 0.1f)
              .SetTrans(Tween.TransitionType.Linear)
              .SetEase(Tween.EaseType.InOut);
    shakeTween.TweenProperty(ComboLabel, "position", originalPosition, 0.1f)
              .SetTrans(Tween.TransitionType.Linear)
              .SetEase(Tween.EaseType.InOut);

    await scaleTween.ToSignal(scaleTween, "finished");

    // Only start the drain delay if we're not in drain already.
    if (!draining)
    {
      // Wait for a brief delay (allowing accumulation if new updates come in quickly).
      await ToSignal(GetTree().CreateTimer(drainDelay), "timeout");

      // If still not interrupted, then start draining.
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
      MoneyCounter.Text = moneyBank.ToString();
    }
  }

  // Update the ComboLabel and MoneyCounter based on the current displayed combo value.
  private void UpdateDrainDisplay()
  {
    int remaining = Mathf.RoundToInt(currentDisplayedCombo);
    int drained = comboValue - remaining;
    ComboLabel.Text = remaining > 0 ? $"+{remaining}" : "0";
    // While draining, MoneyCounter shows banked money plus what's been drained so far.
    MoneyCounter.Text = (moneyBank + drained).ToString();
  }

  // Reset the combo meter's visuals.
  private void ResetComboMeterVisuals()
  {
    comboValue = 0;
    currentDisplayedCombo = 0;
    ComboLabel.Text = "0";
    ComboLabel.Scale = originalScale;
    ComboLabel.Position = originalPosition;
    ComboLabel.Visible = false;
  }
}
