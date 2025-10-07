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
  // Alignment factors used when CenterInRect is false. 0 = start (left/top), 0.5 = center, 1 = end (right/bottom).
  [Export(PropertyHint.Range, "0,1,0.01")] public float AlignX = 0.5f;
  [Export(PropertyHint.Range, "0,1,0.01")] public float AlignY = 0.5f;
  [Export] public float LetterSpacingExtraPx = 1.0f; // extra per-letter spacing fed to DynaText (default matches prior behavior)
  // Small vertical nudge applied inside DynaText to account for font metrics vs. visual
  // centering (e.g., cap-height vs ascender/descender). Positive moves down.
  [Export] public float OffsetYExtraPx = 0f;
  // Line-height style control to influence measured text height for centering.
  // 1 = font metrics height; <1 compresses, >1 expands.
  [Export(PropertyHint.Range, "0.5,2,0.01")] public float TextHeightScale = 1.0f;

  private string _text = string.Empty;
  private System.Collections.Generic.List<Color> _deferredColours = null;
  private List<Color> _overrideColours = null;

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
      TextHeightScale = MathF.Max(0.5f, MathF.Min(2f, TextHeightScale)),
      OffsetYExtraPx = OffsetYExtraPx,
      SpacingExtraPx = LetterSpacingExtraPx,
      Silent = true,
    };
    RebuildParts();
    AddChild(Inner);
    // No ambient quiver by default; call Quiver(...) for transient juice

    // Apply any colours set before _Ready
    if (_deferredColours != null)
    {
      _overrideColours = new List<Color>(_deferredColours);
      ApplyColoursOverride();
      Inner.Init(Config);
      _deferredColours = null;
    }
  }

  public override void _Process(double delta)
  {
    if (CenterInRect && Inner != null)
    {
      var b = Inner.GetBoundsPx();
      float ax = 0.5f;
      float ay = 0.5f;
      var pos = new Vector2((Size.X - b.X) * ax, (Size.Y - b.Y) * ay);
      Inner.Position = pos;
    }
    else if (Inner != null)
    {
      var b = Inner.GetBoundsPx();
      float ax = Mathf.Clamp(AlignX, 0f, 1f);
      float ay = Mathf.Clamp(AlignY, 0f, 1f);
      var pos = new Vector2((Size.X - b.X) * ax, (Size.Y - b.Y) * ay);
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
    _overrideColours = null;
    if (Config != null && Inner != null)
      RebuildParts();
  }

  public void SetColours(List<Color> colours)
  {
    if (colours == null || colours.Count == 0) return;
    if (Config == null || Inner == null)
    {
      // Defer until _Ready initializes Inner/Config
      _deferredColours = new List<Color>(colours);
      _overrideColours = new List<Color>(colours);
      return;
    }
    _overrideColours = new List<Color>(colours);
    ApplyColoursOverride();
    Inner.Init(Config);
  }

  public void Pulse(float amount = 0.22f)
  {
    Inner.TriggerPulse(amount);
  }

  public void Pulse(float amount, float width, float speed)
  {
    Inner.TriggerPulse(amount, width, speed);
  }

  public void Quiver(float amount, float speed, float duration)
  {
    Inner.SetQuiver(amount, speed, duration);
  }

  public void SetTextSegments(List<DynaText.TextPart> parts)
  {
    if (parts == null || parts.Count == 0)
    {
      SetTextWithPerLetterColours(string.Empty, null);
      return;
    }

    var combined = new System.Text.StringBuilder();
    var colours = new List<Color>();
    foreach (var part in parts)
    {
      string literal = part.Provider != null ? part.Provider() : part.Literal ?? string.Empty;
      string prefix = part.Prefix ?? string.Empty;
      string suffix = part.Suffix ?? string.Empty;
      Color inner = part.InnerColour ?? Colors.White;
      Color outer = part.OuterColour ?? inner;

      AppendTextAndColours(combined, colours, prefix, outer);
      AppendTextAndColours(combined, colours, literal, inner);
      AppendTextAndColours(combined, colours, suffix, outer);
    }

    SetTextWithPerLetterColours(combined.ToString(), colours);
  }

  public void SetScale(float scale)
  {
    Inner.SetScale(scale);
  }

  public void SetTextHeightScale(float scale)
  {
    TextHeightScale = scale;
    if (Config != null && Inner != null)
    {
      Config.TextHeightScale = MathF.Max(0.5f, MathF.Min(2f, TextHeightScale));
      Inner.Init(Config);
    }
  }

  public void SetTextWithPerLetterColours(string text, List<Color> perLetterColours)
  {
    _text = text ?? string.Empty;
    _overrideColours = (perLetterColours != null && perLetterColours.Count > 0)
      ? new List<Color>(perLetterColours)
      : null;

    if (Config != null && Inner != null)
      RebuildParts();
  }

  private void RebuildParts()
  {
    if (Config == null || Inner == null)
      return;

    Config.Parts.Clear();
    Config.Parts.Add(new DynaText.TextPart { Provider = () => _text });

    ApplyColoursOverride();
    Inner.Init(Config);
  }

  private void ApplyColoursOverride()
  {
    if (_overrideColours != null && _overrideColours.Count > 0)
      Config.Colours = new List<Color>(_overrideColours);
    else if (Config.Colours == null || Config.Colours.Count == 0)
      Config.Colours = new List<Color> { Colors.White };
  }

  private static void AppendTextAndColours(System.Text.StringBuilder builder, List<Color> colours, string text, Color colour)
  {
    if (string.IsNullOrEmpty(text))
      return;

    builder.Append(text);
    var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
    while (enumerator.MoveNext())
      colours.Add(colour);
  }
}
