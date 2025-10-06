using Godot;
using System;

public partial class InteractionPromptUi : Control
{
  private DynaText _text;
  private DynaText.Config _cfg;
  private string _display = "";
  private Tween _fadeTween;

  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx = 48; // larger default for readability
  [Export] public Color TextColor = new Color(1f, 1f, 1f);
  [Export] public bool Shadow = true;
  [Export] public bool UseShadowParallax = true;
  [Export] public float ShadowAlpha = 0.35f;
  [Export] public Vector2 ShadowOffset = new Vector2(0, 0);
  [Export] public float ParallaxPixelScale = 0f; // 0 = auto from FontPx
  [Export] public float TextRotation = 0.0f;
  [Export] public float PulseAmount = 0.35f;
  [Export] public float QuiverAmount = 0.12f;
  [Export] public float QuiverSpeed = 0.6f;
  [Export] public float FadeDuration = 0.12f;

  public override void _Ready()
  {
    Visible = false;
    Modulate = new Color(Modulate.R, Modulate.G, Modulate.B, 0f);
    _text = new DynaText();
    _cfg = new DynaText.Config
    {
      Font = GD.Load<FontFile>(FontPath),
      FontSizePx = FontPx,
      Colours = new() { TextColor },
      Shadow = Shadow,
      ShadowUseParallax = UseShadowParallax,
      ShadowOffsetPx = ShadowOffset,
      ShadowColor = new Color(0,0,0, Mathf.Clamp(ShadowAlpha, 0f, 1f)),
      ParallaxPixelScale = ParallaxPixelScale,
      SpacingExtraPx = 1.0f,
      TextRotationRad = TextRotation,
      Rotate = true,
      Float = true,
      Bump = true,
      PopInRate = 3f,
      BumpRate = 2.666f,
      BumpAmount = 1f,
      TextHeightScale = 1f,
      Silent = true,
    };
    _cfg.Parts.Add(new DynaText.TextPart { Provider = () => _display });
    _text.Init(_cfg);
    AddChild(_text);
    _text.Position = Vector2.Zero;
  }

  public void AttachTo(Container container)
  {
    // Place centered within provided container
    GlobalPosition = container.GetGlobalRect().Position + container.GetGlobalRect().Size * 0.5f;
    Size = Vector2.Zero;
    _text.Position = Vector2.Zero;
  }

  public void SetText(string text)
  {
    _display = text ?? string.Empty;
    // add some juice on change
    _text.TriggerPulse(PulseAmount);
    _text.SetQuiver(QuiverAmount, QuiverSpeed, 0.3f);
    _text.TriggerTilt(0.25f);
  }

  public void ShowPrompt()
  {
    if (string.IsNullOrEmpty(_display)) return;
    if (_fadeTween != null && _fadeTween.IsRunning()) _fadeTween.Kill();
    Visible = true;
    var c = Modulate; c.A = 0f; Modulate = c;
    _fadeTween = GetTree().CreateTween();
    _fadeTween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);
    _fadeTween.TweenProperty(this, "modulate:a", 1f, MathF.Max(0.0001f, FadeDuration));
  }

  public void HidePrompt()
  {
    if (_fadeTween != null && _fadeTween.IsRunning()) _fadeTween.Kill();
    _fadeTween = GetTree().CreateTween();
    _fadeTween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);
    _fadeTween.TweenProperty(this, "modulate:a", 0f, MathF.Max(0.0001f, FadeDuration));
    _fadeTween.Finished += () => { Visible = false; };
  }
}
