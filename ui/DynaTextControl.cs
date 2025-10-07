using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Control wrapper around DynaText (Node2D) so it can be placed in Godot Control containers.
/// It centers the inner DynaText within its own rect and exposes simple helpers to set text/colors.
/// </summary>
public partial class DynaTextControl : Control
{
  public DynaText Inner { get; private set; }
  public DynaText.Config Config { get; private set; }

  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public int FontPx = 30; // bump default size for general UI
  [Export] public bool Shadow = true;
  [Export] public Vector2 ShadowOffset = new Vector2(0, 0); // default to pure parallax
  [Export] public bool UseShadowParallax = true;
  [Export] public float ShadowAlpha = 0.35f;
  [Export] public float ParallaxPixelScale = 0f; // 0 = auto from FontPx
  [Export] public bool AmbientRotate = true;
  [Export] public bool AmbientFloat = true;
  [Export] public bool AmbientBump = false;    // keep off for subtle ambient
  [Export] public float AmbientQuiverAmount = 0.0f; // default off; use transient quiver to match Balatro
  [Export] public float AmbientQuiverSpeed = 0.5f;
  [Export] public bool CenterInRect = true;
  [Export] public float LetterSpacingExtraPx = 1.0f; // extra per-letter spacing fed to DynaText (default matches prior behavior)
  // Small vertical nudge applied inside DynaText to account for font metrics vs. visual
  // centering (e.g., cap-height vs ascender/descender). Positive moves down.
  [Export] public float OffsetYExtraPx = 0f;

  private string _text = string.Empty;
  private System.Collections.Generic.List<Color> _deferredColours = null;

  public override void _Ready()
  {
    Inner = new DynaText();
    Config = new DynaText.Config
    {
      Font = GD.Load<FontFile>(FontPath),
      FontSizePx = FontPx,
      Shadow = Shadow,
      ShadowOffsetPx = ShadowOffset,
      ShadowUseParallax = UseShadowParallax,
      ShadowColor = new Color(0,0,0, Mathf.Clamp(ShadowAlpha, 0f, 1f)),
      ParallaxPixelScale = ParallaxPixelScale,
      Colours = new List<Color> { Colors.White },
      Rotate = AmbientRotate,
      Float = AmbientFloat,
      Bump = AmbientBump,
      TextHeightScale = 1f,
      OffsetYExtraPx = OffsetYExtraPx,
      SpacingExtraPx = LetterSpacingExtraPx,
      Silent = true,
    };
    Config.Parts.Add(new DynaText.TextPart { Provider = () => _text });
    Inner.Init(Config);
    AddChild(Inner);
    // No ambient quiver by default; call Quiver(...) for transient juice

    // Apply any colours set before _Ready
    if (_deferredColours != null)
    {
      Config.Colours = _deferredColours;
      Inner.Init(Config);
      _deferredColours = null;
    }
  }

  public override void _Process(double delta)
  {
    if (CenterInRect && Inner != null)
    {
      var b = Inner.GetBoundsPx();
      var pos = new Vector2((Size.X - b.X) * 0.5f, (Size.Y - b.Y) * 0.5f);
      Inner.Position = pos;
    }
  }

  public override Vector2 _GetMinimumSize()
  {
    if (Inner != null)
    {
      var b = Inner.GetBoundsPx();
      return new Vector2(Mathf.Max(4f, b.X), Mathf.Max(4f, b.Y));
    }
    // Fallback estimate before _Ready runs
    return new Vector2(Mathf.Max(4f, FontPx * 0.6f), Mathf.Max(4f, FontPx * 1.0f));
  }

  public void SetText(string text)
  {
    _text = text ?? string.Empty;
  }

  public void SetColours(List<Color> colours)
  {
    if (colours == null || colours.Count == 0) return;
    if (Config == null || Inner == null)
    {
      // Defer until _Ready initializes Inner/Config
      _deferredColours = new List<Color>(colours);
      return;
    }
    Config.Colours = colours;
    // Re-init to apply new colour list to parts
    Inner.Init(Config);
  }

  public void Pulse(float amount = 0.22f)
  {
    Inner.TriggerPulse(amount);
  }

  public void Quiver(float amount, float speed, float duration)
  {
    Inner.SetQuiver(amount, speed, duration);
  }
}
