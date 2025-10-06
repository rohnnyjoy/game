using Godot;
using System.Threading.Tasks;

public partial class MoneyTotalUi : Control
{
  private DynaText text;
  private string display = "0";
  private int displayedAmount = 0;
  private int targetAmount = 0;
  private bool animating = false;
  private Tween countTween = null;
  private float _totalAnimValue = 0f;
  private AudioStreamPlayer _coin;
  private AudioStreamPlayer _paperTick;
  // Active money attention alerts stacked near the total
  private System.Collections.Generic.List<DynaText> _alerts = new();
  private float TotalAnimValue
  {
    get => _totalAnimValue;
    set
    {
      _totalAnimValue = value;
      displayedAmount = Mathf.RoundToInt(value);
      display = $"${displayedAmount}";
    }
  }

  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx = 80; // significantly larger for prominence
  [Export] public Color TextColor = new Color(1f, 0.95f, 0.4f); // bright yellow
  [Export] public bool Shadow = true;
  [Export] public bool UseShadowParallax = true;  // enable auto parallax by default
  [Export] public float ShadowAlpha = 0.4f;       // closer to Balatro feel
  [Export] public Vector2 ShadowOffset = new Vector2(0, 0); // rely on auto; no manual offset
  [Export] public float ParallaxPixelScale = 0f;   // 0 = auto from FontPx
  [Export] public float DrainDelay = 0.5f;     // wait before counting up (sync with combo meter)
  [Export] public float DrainDuration = 0.3f;  // Balatro Event:ease typical duration (~0.3s)
  [Export] public float TextRotation = 0.0f;   // global phrase rotation (Balatro: text_rot)
  [Export] public string SfxCoinPath = "res://reference/balatro/resources/sounds/coin3.ogg"; // per-dollar drain tick
  [Export] public string SfxCoinAccentPath = "res://reference/balatro/resources/sounds/coin6.ogg"; // accent at drain start
  [Export] public string SfxPaperPath = "res://reference/balatro/resources/sounds/paper1.ogg"; // per-letter pop-in (if used)
  // Volume controls in decibels (negative is quieter). Adjust in Inspector as needed.
  [Export] public float SfxCoinVolumeDb = -12f;
  [Export] public float SfxCoinAccentVolumeDb = -10f;
  [Export] public float SfxPaperVolumeDb = -18f;
  [Export] public string SfxBus = "Master"; // set to an SFX/UI bus if defined in your project
  [Export] public float AlertSpacingPx = 28f;  // vertical spacing between stacked alerts
  [Export] public float AlertFirstOffsetPx = 40f; // distance above the total for the newest alert
  [Export] public int MaxAlerts = 5;          // limit to avoid unbounded stacking
  // Offset this control relative to the bottom-right container's center to give it breathing room
  [Export] public Vector2 BottomRightOffset = new Vector2(-120, -80);

  public override void _Ready()
  {
    Visible = false;
    text = new DynaText();
    // SFX for per-letter pop-in
    _paperTick = new AudioStreamPlayer { Autoplay = false, Bus = SfxBus };
    _paperTick.Stream = GD.Load<AudioStream>(SfxPaperPath);
    _paperTick.VolumeDb = SfxPaperVolumeDb;
    AddChild(_paperTick);

    var cfg = new DynaText.Config
    {
      Font = GD.Load<FontFile>(FontPath),
      FontSizePx = FontPx,
      Colours = new() { TextColor },
      Shadow = Shadow,
      ShadowUseParallax = UseShadowParallax,
      ShadowOffsetPx = ShadowOffset,
      ShadowColor = new Color(0, 0, 0, Mathf.Clamp(ShadowAlpha, 0f, 1f)),
      ParallaxPixelScale = ParallaxPixelScale,
      SpacingExtraPx = 1.0f,
      TextRotationRad = TextRotation,
      Rotate = true,
      Float = true,
      Bump = false,
    };
    cfg.OnPopInTickSfx = (pitch) =>
    {
      if (_paperTick == null) return;
      _paperTick.PitchScale = MathF.Max(0.05f, pitch);
      _paperTick.Play();
    };
    cfg.Parts.Add(new DynaText.TextPart { Provider = () => display });
    text.Init(cfg);
    AddChild(text);
    // Subtle ambient quiver (lighter)
    text.SetQuiver(0.02f, 0.45f, 9999f);
    // Audio for coin ticks
    _coin = new AudioStreamPlayer { Autoplay = false, Bus = SfxBus };
    _coin.Stream = GD.Load<AudioStream>(SfxCoinPath);
    _coin.VolumeDb = SfxCoinVolumeDb;
    AddChild(_coin);
    // Accent player
    var accent = new AudioStreamPlayer { Autoplay = false, Bus = SfxBus };
    accent.Stream = GD.Load<AudioStream>(SfxCoinAccentPath);
    accent.VolumeDb = SfxCoinAccentVolumeDb;
    AddChild(accent);
    _coinAccent = accent;

    if (Player.Instance?.Inventory != null)
    {
      displayedAmount = Player.Instance.Inventory.Money;
      display = $"${displayedAmount}";
    }

    GlobalEvents.Instance.Connect(nameof(GlobalEvents.MoneyUpdated), new Callable(this, nameof(OnMoneyUpdated)));
  }

  public void AttachToBottomRight(Control bottomRight)
  {
    var rect = bottomRight.GetGlobalRect();
    GlobalPosition = rect.Position + rect.Size * 0.5f + BottomRightOffset;
    if (text != null) text.Position = Vector2.Zero;
    Visible = true;
  }

  public void OnMoneyUpdated(int oldAmount, int newAmount)
  {
    // True-to-source: update total during drain only. Animate from current displayed to newAmount starting at shared time.
    if (animating) { animating = false; if (countTween != null && countTween.IsRunning()) countTween.Kill(); }
    _ = AnimateToTarget(displayedAmount, newAmount);
  }

  private async Task AnimateToTarget(int from, int to)
  {
    animating = true;
    // Start at displayed, not necessarily 'from' in case of overlapping updates
    int start = displayedAmount;
    int end = to;
    // Reserve a start time (push-out semantics) and then wait until the actual shared start.
    GlobalEvents.Instance?.ClaimMoneyDrainStartAt(DrainDelay);
    while (true)
    {
      float now = (float)Time.GetTicksMsec() / 1000f;
      float startAt = GlobalEvents.Instance != null ? GlobalEvents.Instance.NextMoneyDrainStartAt : (now + DrainDelay);
      float wait = MathF.Max(0f, startAt - now);
      if (wait <= 0.0001f) break;
      await ToSignal(GetTree().CreateTimer(wait), "timeout");
      // Loop again in case another increment pushed the start further out
    }
    // Accent at start (Balatro plays coin6 at drops)
    PlayCoinAccent();
    // Kill existing tween if any
    if (countTween != null && countTween.IsRunning()) countTween.Kill();
    // Tween with OutQuad ease to mimic Balatro's ease_value
    countTween = GetTree().CreateTween();
    // Balatro Event:ease default type is 'lerp' (linear). Adopt linear interpolation over a fixed duration.
    countTween.SetEase(Tween.EaseType.In);
    countTween.SetTrans(Tween.TransitionType.Linear);
    TotalAnimValue = start;
    float dur = Mathf.Max(0.0001f, DrainDuration);
    countTween.TweenProperty(this, nameof(TotalAnimValue), (float)end, Mathf.Max(0.0001f, dur));
    // Balatro-style transient quiver at drain start based on magnitude
    int power = (int)MathF.Max(0f, MathF.Floor(MathF.Log10(MathF.Max(1, Math.Abs(end)))));
    float quiverAmt = 0.03f * power;
    if (quiverAmt > 0f) text.SetQuiver(quiverAmt, 0.5f, 0.5f);
    int lastShown = displayedAmount;
    float nextTickAt = 0f;
    while (countTween.IsRunning() && animating)
    {
      // Play a soft coin tick each time the integer increases, rate-limited
      if (displayedAmount != lastShown)
      {
        float now = (float)Time.GetTicksMsec() / 1000f;
        if (now >= nextTickAt) { PlayCoinTick(displayedAmount - lastShown); nextTickAt = now + 0.05f; }
        lastShown = displayedAmount;
      }
      await ToSignal(GetTree().CreateTimer(0.0167f), "timeout");
    }
    if (!animating) return; // cancelled by a new update, don't stomp new target
    displayedAmount = end;
    display = $"${displayedAmount}";
    text.TriggerPulse(0.18f, 2.5f, 40f);
    animating = false;
  }

  private void ShowAttention(int delta)
  {
    if (delta == 0) return;
    var dt = new DynaText();
    var col = delta > 0 ? new Color(0.95f, 0.95f, 0.3f) : new Color(1f, 0.3f, 0.3f);
    var cfg = new DynaText.Config
    {
      Font = GD.Load<FontFile>(FontPath),
      FontSizePx = Math.Max(1, (int)(FontPx * 0.8f)), // scale ~0.8
      Colours = new() { col },
      Shadow = true,
      ShadowUseParallax = UseShadowParallax,
      ShadowOffsetPx = new Vector2(0, 0),
      ShadowColor = new Color(0, 0, 0, Mathf.Clamp(ShadowAlpha, 0f, 1f)),
      ParallaxPixelScale = ParallaxPixelScale,
      PopInRate = 6f,
      Silent = true,
      Rotate = false,
      Float = true,
    };
    cfg.Parts.Add(new DynaText.TextPart { Literal = (delta > 0 ? "+$" : "-$") + Math.Abs(delta).ToString() });
    dt.Init(cfg);
    AddChild(dt);
    dt.ZIndex = ZIndex + 1;
    // Add to alert list and layout; newest goes on top (index 0) for visibility
    _alerts.Insert(0, dt);
    // Cull oldest beyond limit
    while (_alerts.Count > MaxAlerts)
    {
      var old = _alerts[_alerts.Count - 1];
      _alerts.RemoveAt(_alerts.Count - 1);
      if (IsInstanceValid(old)) old.QueueFree();
    }
    // Place the newest alert immediately at its target to avoid flashing on top of the total
    dt.Position = GetAlertTargetPosition(0);
    RelayoutAlerts(skip: dt);
    dt.TriggerPulse(0.5f, 2.5f, 40f); // Balatro pulse amount ~0.5
    // Hold 0.7s, then pop_out(3) and fade quickly (~0.33s)
    _ = FadeAttention(dt, 0.7f);
  }

  private async Task FadeAttention(DynaText dt, float hold)
  {
    await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, hold)), "timeout");
    dt.StartPopOut(3f);
    var tw = GetTree().CreateTween();
    tw.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Linear);
    tw.TweenProperty(dt, "modulate:a", 0f, 0.333f);
    await ToSignal(tw, "finished");
    _alerts.Remove(dt);
    dt.QueueFree();
    RelayoutAlerts();
  }

  // Simple vertical stack layout for active attention alerts
  private void RelayoutAlerts(DynaText skip = null)
  {
    // Newest at index 0 above the total; older alerts stack upward (more negative Y)
    for (int i = 0; i < _alerts.Count; i++)
    {
      var a = _alerts[i];
      if (!IsInstanceValid(a)) continue;
      Vector2 target = GetAlertTargetPosition(i);
      if (a == skip)
      {
        a.Position = target;
        continue;
      }
      var t = GetTree().CreateTween();
      t.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);
      t.TweenProperty(a, nameof(Position), target, 0.1f);
    }
  }

  private Vector2 GetAlertTargetPosition(int index)
  {
    return new Vector2(0f, -AlertFirstOffsetPx - index * AlertSpacingPx);
  }

  private void PlayCoinTick(int step)
  {
    if (_coin == null) return;
    // Balatro uses coin3 per tick; pitch varies 0.9..1.1
    _coin.PitchScale = 0.9f + 0.2f * GD.Randf();
    _coin.Play();
  }

  private AudioStreamPlayer _coinAccent;
  private void PlayCoinAccent()
  {
    if (_coinAccent == null) return;
    _coinAccent.PitchScale = 1.3f;
    _coinAccent.Play();
  }
}
