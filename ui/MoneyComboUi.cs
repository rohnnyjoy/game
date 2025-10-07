using Godot;
using System;
using System.Threading.Tasks;

public partial class MoneyComboUi : Control
{
  private DynaText text;
  private DynaText.Config cfg;
  private string display = "0";
  private Control _crosshairAnchor;
  private bool _attachToCrosshair;

  // Running state (ported to match Balatro's event-queue model)
  private int pendingCombo = 0;             // queued amount to drain (not shown while a drain is active)
  private bool draining = false;            // drain in progress
  private bool waitingToDrain = false;      // coalescing window active
  private Tween drainTween = null;
  private AudioStreamPlayer _coinFill;      // SFX when the combo meter is filled (on increment)
  private AudioStreamPlayer _paperTick;     // SFX for per-letter pop-in (paper1)

  // Tween target property (Godot tweens require a property, not a field)
  private float _comboAnimValue = 0f;
  private float ComboAnimValue
  {
    get => _comboAnimValue;
    set
    {
      _comboAnimValue = value;
      int remaining = Mathf.Max(0, Mathf.FloorToInt(value + 0.0001f));
      display = remaining > 0 ? $"+${remaining}" : "+0";
    }
  }

  // Timing
  [Export] public float DrainDuration = 0.3f; // Balatro Event:ease default timings (~0.3s)
  [Export] public float DrainDelay = 2.0f;

  [Export] public Color TextColor = new Color(1f, 0.95f, 0.4f);
  [Export] public bool Shadow = true;
  [Export] public Vector2 ShadowOffset = new Vector2(0, 0); // pure auto by default
  [Export] public bool UseShadowParallax = true;            // enable automatic parallax shadows
  [Export] public float ShadowAlpha = 0.4f;                 // closer to Balatro default visibility
  [Export] public float ParallaxPixelScale = 0f;            // 0 = auto based on FontPx
  [Export] public float ScaleBase = 1.0f; // leave scale at 1; use FontPx for size
  [Export] public int FontPx = 52;        // pixel size for crisp font rendering (larger for readability)
  [Export] public Vector2 CrosshairOffset = new Vector2(80, 0);
  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public float TextRotation = 0.0f; // global phrase rotation (Balatro: text_rot)
  [Export] public int ScaleClampMax = 10000;  // Balatro scale_number default max when shrinking large values
  [Export] public string SfxComboFillPath = "res://reference/balatro/resources/sounds/coin1.ogg"; // Balatro coin1 on fill
  [Export] public string SfxPaperPath = "res://reference/balatro/resources/sounds/paper1.ogg";    // Balatro paper1 for pop-in ticks
  // Audio normalization: default volumes tuned to sit under gun/impact SFX
  [Export] public float SfxCoinFillVolumeDb = -12f;
  [Export] public float SfxPaperVolumeDb = -18f;
  [Export] public string SfxBus = "Master"; // set to an SFX/UI bus if defined in your project
  // Intensity calibration for our game's money range (normalized-log scale)
  [Export] public int JuiceScaleMaxValue = 5000;   // value that maps to full intensity (≈ millions in Balatro)
  [Export] public float PulseBase = 0.3f;          // min pulse amount
  [Export] public float PulseMaxExtra = 0.6f;      // extra pulse added at full intensity
  [Export] public float QuiverMax = 0.06f;         // toned down: subtle quiver at full intensity
  [Export] public float TiltMax = 0.35f;           // tilt amount at full intensity

  // --- Pop lifecycle tuning (tweak in Inspector) ---
  [Export] public float ComboPopDelay = 0.12f;      // Wait before pop-out starts once armed
  [Export] public float ComboPopOutRate = 4.0f;     // Pop-out decay speed; 4 ≈ 0.25s tail
  [Export] public float MinVisibleTime = 0.35f;     // Ensures short blips still feel intentional
  [Export] public float HideGraceAfterPop = 0.10f;  // Extra time after pop-out before hiding

  // --- Internal session state ---
  private bool _poppedIn = false;     // combo phrase is currently popped in (visible & animated)
  private int _popGen = 0;            // bumps to cancel older pop-out tasks
  private float _lastActivityAt = 0f; // last time we saw money activity affecting the combo

  public override void _Ready()
  {
    Visible = false;
    // Build DynaText with provider-based part so we can update text without re-init
    text = new DynaText();
    cfg = new DynaText.Config
    {
      Font = GD.Load<FontFile>(FontPath),
      Scale = ScaleBase,
      FontSizePx = FontPx,
      SpacingExtraPx = 1.0f,
      Colours = new() { TextColor },
      Shadow = Shadow,
      ShadowOffsetPx = ShadowOffset,
      ShadowUseParallax = UseShadowParallax,
      ShadowColor = new Color(0, 0, 0, Mathf.Clamp(ShadowAlpha, 0f, 1f)),
      ParallaxPixelScale = ParallaxPixelScale,
      TextRotationRad = TextRotation,
      Rotate = true,
      Float = true,
      Bump = false,
      PopInRate = 3f,
      BumpRate = 2.666f,
      BumpAmount = 1f,
      TextHeightScale = 1f,
    };
    cfg.Parts.Add(new DynaText.TextPart { Provider = () => display });
    cfg.PopDelay = ComboPopDelay;
    cfg.Silent = false;
    text.Init(cfg);
    AddChild(text);
    // Initialize; vertical centering handled each frame in _Process
    text.Position = Vector2.Zero;

    // SFX: combo fill (coin1)
    _coinFill = new AudioStreamPlayer { Autoplay = false, Bus = SfxBus };
    _coinFill.Stream = GD.Load<AudioStream>(SfxComboFillPath);
    _coinFill.VolumeDb = SfxCoinFillVolumeDb;
    AddChild(_coinFill);

    // SFX: per-letter pop-in tick (paper1) via DynaText.OnPopInTickSfx
    _paperTick = new AudioStreamPlayer { Autoplay = false, Bus = SfxBus };
    _paperTick.Stream = GD.Load<AudioStream>(SfxPaperPath);
    _paperTick.VolumeDb = SfxPaperVolumeDb;
    AddChild(_paperTick);
    cfg.OnPopInTickSfx = (pitch) =>
    {
      if (_paperTick == null) return;
      _paperTick.PitchScale = MathF.Max(0.05f, pitch);
      _paperTick.Play();
    };

    // Subscribe to money updates
    GlobalEvents.Instance.Connect(nameof(GlobalEvents.MoneyUpdated), new Callable(this, nameof(OnMoneyUpdated)));

    // Start hidden until we get first combo
    display = "";
  }

  public override void _Process(double delta)
  {
    // Keep the combo text vertically centered relative to this control's origin.
    // This ensures the crosshair Y aligns with the text's vertical midpoint when attached.
    if (text != null)
    {
      var bounds = text.GetBoundsPx();
      // X stays at 0 so the text grows to the right of the crosshair; Y is centered.
      text.Position = new Vector2(0f, -0.5f * bounds.Y);
    }

    if (_attachToCrosshair)
    {
      UpdateCrosshairAttachment();
    }
  }

  public void AttachToBottomRight(Control bottomRight)
  {
    // Size and center under the given container
    var rect = bottomRight.GetGlobalRect();
    GlobalPosition = rect.Position + rect.Size * 0.5f;
    PivotOffset = Vector2.Zero;
    Size = rect.Size;
    AnchorLeft = 0; AnchorTop = 0; AnchorRight = 0; AnchorBottom = 0;
    // Draw from center: offset DynaText to center within this control
    if (text != null)
      text.Position = Vector2.Zero;
    Visible = true;
    _attachToCrosshair = false;
    _crosshairAnchor = null;
  }

  public void AttachToCrosshair(Control crosshair)
  {
    _crosshairAnchor = crosshair;
    _attachToCrosshair = true;
    UpdateCrosshairAttachment();
    Size = Vector2.Zero; // size not used; we position by global center
    if (text != null)
      text.Position = new Vector2(0f, text.Position.Y);
    Visible = true;
  }

  private void UpdateCrosshairAttachment()
  {
    if (!IsInstanceValid(_crosshairAnchor))
    {
      _attachToCrosshair = false;
      _crosshairAnchor = null;
      Visible = false;
      return;
    }

    var crosshair = _crosshairAnchor;
    var xform = crosshair.GetGlobalTransformWithCanvas();
    Vector2 size = crosshair.Size;
    if (size == Vector2.Zero)
    {
      size = crosshair.GetMinimumSize();
      if (size == Vector2.Zero)
        size = crosshair.CustomMinimumSize;
    }

    Vector2 center = xform.Origin + size * 0.5f;
    Vector2 pos = center + CrosshairOffset;
    var gui = GameUi.Instance;
    if (gui != null && !gui.UseFullFrameShake)
      pos -= gui.GetScreenShakeOffset();

    GlobalPosition = pos;
  }

  private bool RegisterComboActivity(string cause = null)
  {
    _lastActivityAt = Now();

    if (text == null || cfg == null)
      return false;

    bool freshPop = !_poppedIn;
    if (freshPop)
    {
      cfg.PopDelay = ComboPopDelay;
      cfg.PopInStartAt = 0f;
      text.Init(cfg);
      Visible = true;
      _poppedIn = true;
    }
    else
    {
      text.CancelPopOut(restorePopIn: true);
      Visible = true;
    }

    _popGen++;
    return freshPop;
  }

  private void ApplyComboJuice(int comboValue, bool allowTilt = true)
  {
    if (text == null) return;

    int absVal = Math.Max(1, comboValue);
    float logVal = MathF.Log10(absVal);
    float maxLog = JuiceScaleMaxValue > 1 ? MathF.Log10(MathF.Max(2f, JuiceScaleMaxValue)) : 1f;
    float intensity = maxLog <= 0f ? 1f : Mathf.Clamp(logVal / maxLog, 0f, 1f);

    float pulse = PulseBase + PulseMaxExtra * intensity;
    if (pulse > 0f)
    {
      text.TriggerPulse(pulse, 2.5f, 40f);
    }

    float quiverAmt = QuiverMax * intensity;
    if (quiverAmt > 0f)
    {
      text.SetQuiver(quiverAmt, 0.5f, 0.5f + 0.3f * intensity);
    }

    if (allowTilt)
    {
      float tiltAmt = TiltMax * intensity;
      if (tiltAmt > 0f)
      {
        text.TriggerTilt(tiltAmt);
      }
    }
  }

  private async Task ArmPopOutIfIdle()
  {
    int gen = _popGen;

    float waitMin = MathF.Max(0f, MinVisibleTime - (Now() - _lastActivityAt));
    if (waitMin > 0f)
    {
      await ToSignal(GetTree().CreateTimer(waitMin), "timeout");
    }
    if (gen != _popGen) return;

    if (pendingCombo > 0 || draining || waitingToDrain)
      return;

    if (text == null || cfg == null)
      return;

    cfg.PopDelay = ComboPopDelay;
    text.StartPopOut(ComboPopOutRate);

    float popTail = 1f / MathF.Max(0.0001f, ComboPopOutRate);
    float total = ComboPopDelay + popTail + HideGraceAfterPop;

    await ToSignal(GetTree().CreateTimer(total), "timeout");
    if (gen != _popGen) return;
    if (pendingCombo > 0 || draining || waitingToDrain)
      return;

    display = "";
    _poppedIn = false;
    Visible = false;
  }

  private static float Now() => (float)Time.GetTicksMsec() / 1000f;

  public void OnMoneyUpdated(int oldAmount, int newAmount)
  {
    int delta = newAmount - oldAmount;

    // Accumulate into a pending bucket; if a drain is active, we do not change the visible text mid-drain
    pendingCombo += delta;
    int absVal = Math.Max(1, Math.Abs(pendingCombo));
    bool freshPop = RegisterComboActivity("money-updated");

    // Play fill SFX on increment (coin1), slight pitch variance for texture
    if (_coinFill != null && delta != 0)
    {
      _coinFill.PitchScale = 0.95f + 0.05f * GD.Randf();
      _coinFill.Play();
    }

    // If not currently draining, reflect the pending total in the UI immediately (Balatro: set chip_total before ease)
    if (!draining)
    {
      display = pendingCombo > 0 ? $"+${pendingCombo}" : "";
      float newScale = ScaleNumber(absVal, ScaleBase, ScaleClampMax);
      text.SetScale(newScale);
      ApplyComboJuice(absVal, allowTilt: true);
      // Add Balatro-style jiggle on coin gain; strength scales gently with delta
      if (delta != 0)
      {
        float jiggle = Mathf.Clamp(0.2f + 0.02f * MathF.Abs(delta), 0.2f, 1.5f);
        GameUi.Instance?.AddJiggle(jiggle);
      }

      // Reserve/start a shared drain start time and wait until it elapses, handling push-outs from subsequent updates
      GlobalEvents.Instance?.ClaimMoneyDrainStartAt(DrainDelay);
      if (!waitingToDrain)
      {
        waitingToDrain = true;
        _ = WaitAndDrain();
      }
    }
    else if (freshPop)
    {
      ApplyComboJuice(absVal, allowTilt: true);
    }
  }

  private async Task WaitAndDrain()
  {
    // Wait until the shared start time; new updates may keep pushing it out
    while (true)
    {
      float now = (float)Time.GetTicksMsec() / 1000f;
      float startAt = GlobalEvents.Instance != null ? GlobalEvents.Instance.NextMoneyDrainStartAt : (now + DrainDelay);
      float wait = MathF.Max(0f, startAt - now);
      if (wait <= 0.0001f) break;
      await ToSignal(GetTree().CreateTimer(wait), "timeout");
    }
    waitingToDrain = false;
    await DrainOnceFromPending();
    // If more arrived while draining, display them and schedule the next window
    if (pendingCombo > 0)
    {
      // Show newly pending amount immediately, then coalesce again
      int absVal = Math.Max(1, Math.Abs(pendingCombo));
      display = pendingCombo > 0 ? $"+${pendingCombo}" : "";
      RegisterComboActivity("post-drain-new-pending");
      text.SetScale(ScaleNumber(absVal, ScaleBase, ScaleClampMax));
      ApplyComboJuice(absVal, allowTilt: true);
      // Nudge on subsequent batches too
      GameUi.Instance?.AddJiggle(Mathf.Clamp(0.15f + 0.01f * absVal, 0.1f, 1.0f));
      GlobalEvents.Instance?.ClaimMoneyDrainStartAt(DrainDelay);
      waitingToDrain = true;
      _ = WaitAndDrain();
    }
  }

  private async Task DrainOnceFromPending()
  {
    draining = true;
    int startValue = Math.Max(0, pendingCombo);
    pendingCombo = 0;
    float dur = Mathf.Max(0.0001f, DrainDuration);
    if (drainTween != null && drainTween.IsRunning()) drainTween.Kill();
    drainTween = GetTree().CreateTween();
    drainTween.SetEase(Tween.EaseType.In);
    drainTween.SetTrans(Tween.TransitionType.Linear);
    ComboAnimValue = startValue;
    drainTween.TweenProperty(this, nameof(ComboAnimValue), 0f, dur);
    await ToSignal(drainTween, "finished");
    if (!draining) return;
    ComboAnimValue = 0f;
    draining = false;
    text.TriggerPulse(0.18f, 2.5f, 36f);
    if (pendingCombo <= 0 && !waitingToDrain)
    {
      _ = ArmPopOutIfIdle();
    }
  }

  // Balatro scale_number: shrink scale for values above a threshold using log ratio
  private static float ScaleNumber(int number, float baseScale, int max)
  {
    const float E_SWITCH_POINT = 100000000000f; // matches Balatro's G.E_SWITCH_POINT
    if (number <= 0) return baseScale;
    if (max <= 0) max = 10000;
    if (number >= E_SWITCH_POINT)
    {
      float num = MathF.Floor(MathF.Log10(max * 10f));
      float den = MathF.Floor(MathF.Log10(1000000f * 10f));
      if (den <= 0) return baseScale;
      return baseScale * (num / den);
    }
    if (number >= max)
    {
      float num = MathF.Floor(MathF.Log10(max * 10f));
      float den = MathF.Floor(MathF.Log10(number * 10f));
      if (den <= 0) return baseScale;
      return baseScale * (num / den);
    }
    return baseScale;
  }
}
