using Godot;
using System.Collections.Generic;

// Reusable style for configuring DynaText/DynaTextHost in a consistent way across UI.
[GlobalClass]
public partial class DynaTextStyle : Resource
{
  [Export] public FontFile Font = GD.Load<FontFile>("res://assets/fonts/Born2bSportyV2.ttf");
  [Export] public int FontPx = 30;

  // Shadow settings
  [Export] public bool Shadow = true;
  [Export] public bool UseShadowParallax = true;
  [Export] public Vector2 ShadowOffset = new Vector2(0, 0);
  [Export(PropertyHint.Range, "0,1,0.01")] public float ShadowAlpha = 0.35f;
  [Export] public float ParallaxPixelScale = 0f; // 0 = auto

  // Ambient animation defaults
  [Export] public bool AmbientRotate = true;
  [Export] public bool AmbientFloat = true;
  [Export] public bool AmbientBump = false;

  // Metrics/spacing
  [Export] public float LetterSpacingExtraPx = 1.0f;
  [Export(PropertyHint.Range, "0.5,2,0.01")] public float TextHeightScale = 1.0f;
  [Export] public float OffsetYExtraPx = 0f;

  // Colors palette default (fallback used by DynaText when letters don’t specify explicit colors)
  [Export] public Godot.Collections.Array<Color> BaseColours = new() { Colors.White };

  // Applies this style to the provided config (mutates cfg).
  public void ApplyTo(DynaText.Config cfg)
  {
    if (cfg == null) return;
    cfg.Font = Font;
    cfg.FontSizePx = FontPx;
    cfg.Shadow = Shadow;
    cfg.ShadowUseParallax = UseShadowParallax;
    cfg.ShadowOffsetPx = ShadowOffset;
    cfg.ShadowColor = new Color(0, 0, 0, Mathf.Clamp(ShadowAlpha, 0f, 1f));
    cfg.ParallaxPixelScale = ParallaxPixelScale;
    cfg.Rotate = AmbientRotate;
    cfg.Float = AmbientFloat;
    cfg.Bump = AmbientBump;
    cfg.SpacingExtraPx = LetterSpacingExtraPx;
    cfg.TextHeightScale = Mathf.Clamp(TextHeightScale, 0.5f, 2f);
    cfg.OffsetYExtraPx = OffsetYExtraPx;
    cfg.Colours = new List<Color>(BaseColours);
  }
}

