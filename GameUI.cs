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
  private int comboValue = 0;          // The total combo value (accumulated delta).
  private float currentDisplayedCombo = 0; // The number currently shown on the ComboLabel.
  private bool draining = false;       // Whether a drain coroutine is running.
  private Task drainTask = null;       // Reference to the active drain coroutine.

  // Visual properties.
  private Vector2 originalPosition;
  private Vector2 originalScale;

  // Duration for the drain effect.
  private const float drainDuration = 0.5f;

  public override void _Ready()
  {
    InteractionLabel = GetNode<RichTextLabel>("InteractionLabel");
    ComboLabel = GetNode<RichTextLabel>("ComboLabel");
    MoneyCounter = GetNode<RichTextLabel>("MoneyCounter");

    // Center the scaling (so the label scales from its center).
    ComboLabel.PivotOffset = ComboLabel.Size / 2;
    originalPosition = ComboLabel.Position;
    originalScale = ComboLabel.Scale;

    // Start hidden.
    ComboLabel.Visible = false;
    MoneyCounter.Text = moneyBank.ToString();

    GlobalEvents.Instance.Connect("MoneyUpdated", new Callable(this, nameof(OnMoneyUpdated)));
    Instance = this;
  }

  public async void OnMoneyUpdated(int oldAmount, int newAmount)
  {
    int delta = newAmount - oldAmount;
    GD.Print($"Money updated: delta = {delta}");

    // If a drain is in progress, finalize it:
    if (draining)
    {
      // Finalize the drain: add the amount that has already drained.
      int alreadyDrained = (int)(comboValue - currentDisplayedCombo);
      moneyBank += alreadyDrained;
      // Cancel the ongoing drain.
      draining = false;
      // Await the drainTask so it completes (it should exit early).
      await drainTask;
      ResetComboMeterVisuals();
    }

    // Add the new delta to the remaining combo (if any) or start fresh.
    // currentDisplayedCombo holds the displayed amount from previous drain (or 0).
    comboValue = (int)currentDisplayedCombo + delta;
    currentDisplayedCombo = comboValue;

    GD.Print($"New comboValue = {comboValue}");
    // Update the display with a plus sign.
    ComboLabel.Text = $"+{comboValue}";
    ComboLabel.Visible = true;

    // Pop & shake effects (using tweens):
    Tween scaleTween = CreateTween();
    // Scale up for a pop.
    scaleTween.TweenProperty(ComboLabel, "scale", ComboLabel.Scale * 1.5f, 0.2f)
              .SetTrans(Tween.TransitionType.Back)
              .SetEase(Tween.EaseType.Out);
    // Scale back down.
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

    // Wait a brief delay before starting the drain.
    await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

    // Start draining if not already draining.
    if (!draining)
    {
      drainTask = DrainCombo();
      await drainTask;
    }
  }

  // Coroutine that drains the combo meter from currentDisplayedCombo to 0.
  private async Task DrainCombo()
  {
    draining = true;
    float elapsed = 0f;
    int startValue = comboValue;  // Value at start of drain.
    while (elapsed < drainDuration && draining)
    {
      float t = elapsed / drainDuration;
      // Interpolate currentDisplayedCombo from startValue to 0.
      currentDisplayedCombo = Mathf.Lerp(startValue, 0, t);
      UpdateDrainDisplay();
      await ToSignal(GetTree().CreateTimer(0.0167f), "timeout");  // ~60 FPS.
      elapsed += 0.0167f;
    }
    // If not cancelled, finish the drain.
    if (draining)
    {
      currentDisplayedCombo = 0;
      UpdateDrainDisplay();
      // Finalize: add the full drained amount to moneyBank.
      moneyBank += startValue;
      draining = false;
      ResetComboMeterVisuals();
    }
  }

  // Updates the ComboLabel and MoneyCounter based on currentDisplayedCombo.
  private void UpdateDrainDisplay()
  {
    int remaining = (int)currentDisplayedCombo;
    int drainedAmount = comboValue - remaining;
    // Show the remaining combo (with plus sign if > 0).
    ComboLabel.Text = remaining > 0 ? $"+{remaining}" : "0";
    // MoneyCounter shows the money bank plus the drained amount so far.
    MoneyCounter.Text = (moneyBank + drainedAmount).ToString();
  }

  // Resets the visual properties of the combo meter.
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
