using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// DynaText is a per-letter animated text renderer for Godot 4.
/// It reproduces the behavior of the original LÖVE/Lua DynaText:
/// - Progressive pop-in/pop-out with sound hooks
/// - Letter-level rotation/scale/offset animations (pulse, quiver, float, bump)
/// - Multi-string cycling with delays, optional random pick
/// - Colored inner/outer (prefix/suffix) spans per part
/// - Optional shadow pass with configurable offset or color
/// Rendering is performed in _Draw() using CanvasItem.DrawSetTransform + DrawString.
/// </summary>
public partial class DynaText : Node2D
{
  // ------------------------------
  // Public configuration container
  // ------------------------------
  public sealed class Config
  {
    // Text content: list of parts. Each part is either a literal or a provider (dynamic).
    public List<TextPart> Parts = new();

    // Visuals & animation
    public bool Shadow = false;
    public Color? ShadowColor = null;              // If null, uses 30% black scaled by first letter alpha (as in original)
    public Vector2 ShadowOffsetPx = Vector2.Zero;  // Legacy fixed offset
    public Vector2 ShadowParallaxPx = Vector2.Zero; // Explicit parallax vector; if zero and ShadowUseParallax=true, compute dynamically
    public bool ShadowUseParallax = true;          // True-to-Balatro: shadow direction varies with screen position
    public float ShadowParallaxStrength = 1.5f;    // Matches Moveable: default magnitude (x from -1.5..1.5, y ~ -1.5)
    public float ParallaxPixelScale = 0f;          // 0 = auto; otherwise multiply parallax by this many pixels
    public float Scale = 1f;
    public float PopInRate = 3f;
    public float BumpRate = 2.666f;
    public float BumpAmount = 1f;
    public float? PopInStartAt = null;         // If not null, start with pop-in enabled (time offset in seconds, typically 0)
    public float? MaxWidthPx = null;           // If set, downscales uniformly so the text fits this width
    public float? SpacingExtraPx = null;       // Additional x spacing per letter (added to glyph advance)
    public float TextRotationRad = 0f;         // Global text rotation (original: config.text_rot)
    public bool Lean = true;                    // Add small rotation based on horizontal movement
    public float LeanFactor = 0.00015f;         // radians per (pixel/second)
    public float LeanClampRad = 0.25f;          // clamp lean to avoid extreme tilt

    // Cycling through multiple strings
    public float PopDelay = 1.5f;              // Time to wait after full pop-in before pop-out starts
    public bool RandomElement = false;         // Randomize next string when cycling
    public float MinCycleTime = 1f;            // As in original; 0 means pop-in immediately at 1

    // Offsets (equivalent to font.TEXT_OFFSET + optional x/y offsets in original)
    public Vector2 TextPixelOffset = Vector2.Zero; // Applied before drawing letters
    public float OffsetYExtraPx = 0f;

    // Colors
    public List<Color> Colours = new() { Colors.Red };

    // Sound behavior
    public bool Silent = false;
    public float PitchShift = 0f;

    // Font bundle (Godot fonts carry metrics; size is defined by the using node/theme)
    public Font Font;                  // REQUIRED: a loaded Font/FontFile/FontVariation resource
    public int FontSizePx = 16;        // Explicit pixel size for shaping/drawing (must be > 0 in Godot 4)
    public float TextHeightScale = 1f; // Original TEXT_HEIGHT_SCALE (defaults to 1)
    public float SpacingFactor = 2.7f;  // Multiplier for spacing (matches Balatro feel)
    public bool PixelSnap = true;       // Snap per-letter positions to whole pixels for crisp rendering

    // Feature flags
    public bool Rotate = false;        // If true: per-letter sway (Rotate==2 flips sign, like original)
    public int RotateMode = 1;         // 1 or 2 (2 = negative)
    public PulseSpec Pulse = null;     // Optional transient scale pulse
    public QuiverSpec Quiver = null;   // Optional micro-rotation wobble
    public bool Float = false;         // Vertical sinusoidal float
    public bool Bump = false;          // Bouncy vertical bump

    // Optional: if you want to be notified when a “paper” tick should play.
    public Action<float> OnPopInTickSfx = null;
  }

  /// <summary> One logical segment with optional (prefix/suffix) and color spans. </summary>
  public sealed class TextPart
  {
    // Provide text from code OR use Literal; Provider takes priority when present.
    public Func<string> Provider = null;
    public string Literal = null;

    // Optional adornments
    public string Prefix = "";
    public string Suffix = "";
    public float? Scale = null;

    // Coloring
    public Color? InnerColour = null;      // Inner letters’ color (v.colour)
    public Color? OuterColour = null;      // Prefix & suffix color
  }

  /// <summary> Pulse animation spec (short-lived scale/rotation impulse). </summary>
  public sealed class PulseSpec
  {
    public float Speed = 40f;     // Speed factor used in original
    public float Width = 2.5f;    // Envelope width
    public float Amount = 0.2f;   // Scale delta
    public float StartTime;       // Seconds (TimeSinceStart) when pulse begins
    public int StartGlyphIndex = 0;  // First glyph affected (0-based)
    public int GlyphCount = -1;      // Number of glyphs affected; -1 = until end of line
  }

  /// <summary> Quiver animation spec (micro jitter rotation/scale). </summary>
  public sealed class QuiverSpec
  {
    public float Speed = 0.5f;
    public float Amount = 0.7f;
  }

  // ------------------------------
  // Internal structures
  // ------------------------------
  private struct Letter
  {
    public string Grapheme; // Single grapheme cluster to draw (handles surrogate pairs)
    public float Scale;     // Current per-letter scale (modified by animations each frame)
    public float BaseScale; // Base per-letter scale (from part.Scale)
    public float PopIn;     // 0..1 envelope for pop-in/out
    public Vector2 Offset;  // Per-letter offset from anim (float/bump); in pixels
    public Vector2 Dims;    // Unscaled local dims: (advanceX, fontHeightScaled) in pixels
    public float Rotation;  // Per-letter rotation in radians
    public Color? PrefixColor; // If letter index <= prefix length
    public Color? SuffixColor; // If letter index > suffix start
    public Color? InnerColor;  // Inner color override
  }

  private sealed class BuiltString
  {
    public string Content = "";
    public List<Letter> Letters = new();
    public float WidthPx;
    public float HeightPx;
    public float WOffsetPx; // Horizontal centering offset when multiple lines differ in size
    public float HOffsetPx; // Vertical centering offset plus config.OffsetYExtraPx
  }

  // ------------------------------
  // Fields
  // ------------------------------
  private readonly Config _cfg = new();
  private readonly List<BuiltString> _built = new();   // Built strings (per part)
  private int _focusedIndex = 0;

  private float _createdTime;            // Seconds since engine start when current pop-in cycle started
  private float _popOutStartTime = 0f;   // When pop-out decays began
  private float _popOutRate = 0f;        // Pop-out rate (e.g., 4)
  private bool _popOutActive = false;    // Whether pop-out decay is active
  private bool _popCyclePending = false;
  private float _quiverStopAt = 0f;      // Optional quiver end time
  private float _bumpStopAt = 0f;        // Optional bump end time
  // Simple tilt (juice) effect, ported from Moveable:juice_up (rotation only)
  private bool _tiltActive = false;
  private float _tiltStart = 0f;
  private float _tiltEnd = 0f;
  private float _tiltRAmt = 0f;         // rotation amplitude
  // Movement lean state
  private Vector2 _prevGlobalPos = Vector2.Zero;
  private float _prevVelSampleTime = -1f;
  private float _xVelPxPerSec = 0f;

  // Cached bounds (assigned to "T.w/h" in original)
  private float _boundsW, _boundsH;

  // Public read-only bounds in pixels for layout wrappers.
  public Vector2 GetBoundsPx() => new Vector2(_boundsW, _boundsH);

  // Precompute GRAPHEMES instead of char to be Unicode-correct (matches utf8.chars())
  private static IEnumerable<string> Graphemes(string s)
  {
    if (string.IsNullOrEmpty(s)) yield break;
    var e = StringInfo.GetTextElementEnumerator(s);
    while (e.MoveNext()) yield return e.GetTextElement();
  }

  // Counts user-perceived characters (grapheme clusters); needed for prefix/suffix coloring parity.
  private static int GraphemeCount(string s) => string.IsNullOrEmpty(s) ? 0 : new StringInfo(s).LengthInTextElements;

  // ------------------------------
  // API: initialization & text
  // ------------------------------

  /// <summary>
  /// Initializes with a given configuration. You can also set properties on _cfg,
  /// then call Rebuild(true) to apply.
  /// </summary>
  public void Init(Config config)
  {
    if (config == null) throw new ArgumentNullException(nameof(config));
    CopyConfig(config, _cfg);

    // Build from parts at least once.
    _built.Clear();
    _focusedIndex = 0;
    Rebuild(firstPass: true);

    // Fit to MaxWidth if needed (uniform downscale).
    if (_cfg.MaxWidthPx.HasValue && _boundsW > _cfg.MaxWidthPx.Value)
    {
      var factor = _cfg.MaxWidthPx.Value / Math.Max(1f, _boundsW);
      _cfg.Scale *= factor;
      Rebuild(firstPass: true);
    }

    // Multi-string auto-cycle handling (start with pop-out armed if multiple parts)
    if (_built.Count > 1)
    {
      _cfg.PopDelay = _cfg.PopDelay <= 0 ? 1.5f : _cfg.PopDelay;
      PopOut(4f); // matches original default
    }

    // Start timing
    _createdTime = TimeSinceStart;
    if (_cfg.PopInStartAt.HasValue)
    {
      // If PopInStartAt is specified, force a fresh pop-in from zero.
      _cfg.PopInStartAt = _cfg.PopInStartAt.Value;
      ResetLettersForPopIn(_focusedIndex);
    }

    QueueRedraw();
  }

  /// <summary> Convenience: sets a single literal string. </summary>
  public void SetText(string text)
  {
    var cfg = new Config
    {
      Font = _cfg.Font,
      Scale = _cfg.Scale,
      Colours = new List<Color>(_cfg.Colours),
      Shadow = _cfg.Shadow,
      ShadowColor = _cfg.ShadowColor,
      ShadowOffsetPx = _cfg.ShadowOffsetPx,
      PopInRate = _cfg.PopInRate,
      BumpRate = _cfg.BumpRate,
      BumpAmount = _cfg.BumpAmount,
      SpacingExtraPx = _cfg.SpacingExtraPx,
      TextRotationRad = _cfg.TextRotationRad,
      PopDelay = _cfg.PopDelay,
      RandomElement = _cfg.RandomElement,
      MinCycleTime = _cfg.MinCycleTime,
      TextPixelOffset = _cfg.TextPixelOffset,
      OffsetYExtraPx = _cfg.OffsetYExtraPx,
      Silent = _cfg.Silent,
      PitchShift = _cfg.PitchShift,
      TextHeightScale = _cfg.TextHeightScale,
      Rotate = _cfg.Rotate,
      RotateMode = _cfg.RotateMode,
      Pulse = _cfg.Pulse,
      Quiver = _cfg.Quiver,
      Float = _cfg.Float,
      Bump = _cfg.Bump,
      PopInStartAt = _cfg.PopInStartAt,
      MaxWidthPx = _cfg.MaxWidthPx
    };
    cfg.Parts.Clear();
    cfg.Parts.Add(new TextPart { Literal = text ?? "" });
    Init(cfg);
  }

  // ------------------------------
  // Lifecycle
  // ------------------------------
  public override void _Process(double delta)
  {
    // Update textual content (providers may change between frames)
    Rebuild(firstPass: false);

    // Update per-letter animation state
    AlignLetters();

    // Track horizontal velocity for movement-based lean (approximate Moveable:move_r behavior)
    float now = TimeSinceStart;
    Vector2 gp = GlobalPosition;
    if (_prevVelSampleTime >= 0f)
    {
      float dt = now - _prevVelSampleTime;
      if (dt > 0.0005f && dt < 0.5f)
      {
        _xVelPxPerSec = (gp.X - _prevGlobalPos.X) / dt;
      }
    }
    _prevGlobalPos = gp;
    _prevVelSampleTime = now;

    // Redraw this frame
    QueueRedraw();
  }

  // ------------------------------
  // Runtime animation triggers
  // ------------------------------
  public void TriggerPulse(float amount = 0.22f, float width = 2.5f, float speed = 40f)
  {
    TriggerPulseRange(0, -1, amount, width, speed);
  }

  public void TriggerPulseRange(int startGlyphIndex, int glyphCount = -1, float amount = 0.22f, float width = 2.5f, float speed = 40f)
  {
    int start = Math.Max(0, startGlyphIndex);
    int count = glyphCount < 0 ? -1 : Math.Max(0, glyphCount);
    _cfg.Pulse = new PulseSpec
    {
      Amount = amount,
      Width = width,
      Speed = speed,
      StartTime = TimeSinceStart,
      StartGlyphIndex = start,
      GlyphCount = count
    };
  }

  public void CancelPopOut(bool restorePopIn = false)
  {
    _popOutActive = false;
    _popCyclePending = false;
    _popOutRate = 0f;
    _popOutStartTime = 0f;
    if (restorePopIn && _built.Count > 0)
    {
      var line = _built[Mathf.Clamp(_focusedIndex, 0, _built.Count - 1)];
      for (int i = 0; i < line.Letters.Count; i++)
      {
        var letter = line.Letters[i];
        letter.PopIn = 1f;
        line.Letters[i] = letter;
      }
    }
  }

  public void SetQuiver(float amount, float speed, float duration)
  {
    _cfg.Quiver = new QuiverSpec { Amount = amount, Speed = speed };
    _quiverStopAt = TimeSinceStart + MathF.Max(0.01f, duration);
  }

  public void SetBump(bool enabled, float amount = 1f, float rate = 2.666f, float duration = 0.2f)
  {
    _cfg.Bump = enabled;
    _cfg.BumpAmount = amount;
    _cfg.BumpRate = rate;
    _bumpStopAt = enabled ? (TimeSinceStart + MathF.Max(0.01f, duration)) : 0f;
  }

  public override void _Draw()
  {
    if (_built.Count == 0 || _cfg.Font == null) return;

    var sIdx = Mathf.Clamp(_focusedIndex, 0, _built.Count - 1);
    var line = _built[sIdx];

    // Establish a base transform equivalent to the original "prep_draw(self, 1)" + text rotation.
    // We let Node2D handle Position/Rotation/Scale at the node level; per-letter transforms are relative.
    // We still apply a global text rotation (config.TextRotationRad).
    // To avoid accumulating transforms, we reset to identity before per-letter draw.
    var baseOffset = new Vector2(line.WOffsetPx, line.HOffsetPx + _cfg.TextPixelOffset.Y + _cfg.OffsetYExtraPx)
                   + new Vector2(_cfg.TextPixelOffset.X, 0f);

    // Optional shadow pass (draw first, slightly offset).
    if (_cfg.Shadow)
    {
      DrawStringLine(line, baseOffset, isShadow: true);
    }

    // Main colored pass
    DrawStringLine(line, baseOffset, isShadow: false);

    // Optional: draw debug bounds (similar to draw_boundingrect in original)
    // Uncomment if needed:
    // DrawRect(new Rect2(Vector2.Zero, new Vector2(_boundsW, _boundsH)), new Color(1,1,1,0.05f), filled:false);
  }

  // Public helper to initiate pop-out decay like Balatro's DynaText:pop_out
  public void StartPopOut(float popOutTimer = 3f)
  {
    PopOut(popOutTimer);
  }

  // ------------------------------
  // Drawing helpers
  // ------------------------------

  private void DrawStringLine(BuiltString line, Vector2 baseOffset, bool isShadow)
  {
    var font = _cfg.Font;
    int fsBase = Math.Max(1, _cfg.FontSizePx);
    float ascentBase = font.GetAscent(fsBase); // Baseline offset for base size

    // Compute shadow parallax + normalized bias (Balatro: shadow_parrallax and normalized vector)
    // Combine dynamic parallax with any explicit pixel offset so exported offsets always take effect.
    Vector2 basePar = Vector2.Zero;
    if (_cfg.ShadowUseParallax)
      basePar = (_cfg.ShadowParallaxPx != Vector2.Zero) ? _cfg.ShadowParallaxPx : ComputeShadowParallax();
    Vector2 par = basePar + _cfg.ShadowOffsetPx;
    float pxScale = (_cfg.ParallaxPixelScale > 0f) ? _cfg.ParallaxPixelScale : MathF.Max(0.25f, _cfg.FontSizePx / 24f);
    // Use a very small normalized nudge; Lua scales by FONTSCALE/G.TILESIZE which is << 1 pixel.
    Vector2 tinyNormal = par == Vector2.Zero ? Vector2.Zero : par.Normalized() * 0.5f;

    // Current pen position (local coordinates)
    Vector2 pen = baseOffset;

    // If global rotation is defined, apply once per line
    // We'll apply per-letter transforms individually (rotation + scale), so here we only rotate position if needed.
    float globalTextRot = _cfg.TextRotationRad;
    // Add movement-based lean (clamped), like Moveable:move_r uses velocity.x to bias rotation
    if (_cfg.Lean)
    {
      float lean = _cfg.LeanFactor * _xVelPxPerSec;
      if (_cfg.LeanClampRad > 0)
        lean = MathF.Max(-_cfg.LeanClampRad, MathF.Min(_cfg.LeanClampRad, lean));
      globalTextRot += lean;
    }
    // Add transient tilt (juice_up rotation) like Moveable:move_juice
    if (_tiltActive)
    {
      float now = TimeSinceStart;
      if (now >= _tiltEnd)
      {
        _tiltActive = false;
      }
      else
      {
        float t = now - _tiltStart;
        float dur = MathF.Max(0.0001f, _tiltEnd - _tiltStart);
        float falloff = MathF.Max(0f, (_tiltEnd - now) / dur);
        // r = r_amt * sin(40.8 * t) * (falloff^2)
        globalTextRot += _tiltRAmt * MathF.Sin(40.8f * t) * (falloff * falloff);
      }
    }

    // Draw each letter with its own transform
    for (int i = 0; i < line.Letters.Count; i++)
    {
      var letter = line.Letters[i];

      // Pop-in factor: 1 when MinCycleTime==0, else letter.PopIn (Lua: real_pop_in)
      float popFactor = (_cfg.MinCycleTime <= 0f ? 1f : letter.PopIn);
      // Dims already contain _cfg.Scale * BaseScale; the per-letter transform should apply only pop + pulse
      float transformScale = popFactor * letter.Scale;

      // If shadow: use either configured color or 30% black scaled by primary alpha (match original’s intent).
      Color modulate = Colors.White;
      if (isShadow)
      {
        if (_cfg.ShadowColor.HasValue)
        {
          modulate = _cfg.ShadowColor.Value;
        }
        else
        {
          float a = _cfg.Colours.Count > 0 ? _cfg.Colours[0].A : 1f;
          modulate = new Color(0, 0, 0, 0.3f * a);
        }
      }
      else
      {
        // Priority: prefix/suffix/inner-colour overrides -> cycling colours list
        Color baseCol = _cfg.Colours.Count > 0 ? _cfg.Colours[i % _cfg.Colours.Count] : Colors.White;
        if (letter.PrefixColor.HasValue) baseCol = letter.PrefixColor.Value;
        else if (letter.SuffixColor.HasValue) baseCol = letter.SuffixColor.Value;
        else if (letter.InnerColor.HasValue) baseCol = letter.InnerColor.Value;
        modulate = baseCol;
      }

      // Baseline position for DrawString (top-left would be y+ascent)
      // Snap the base pen position (stable across frames), not the per-letter animated transform origin.
      Vector2 penForThisLetter = pen;
      if (_cfg.PixelSnap) penForThisLetter = new Vector2(Mathf.Round(pen.X), Mathf.Round(pen.Y));
      // Center anchor stays based on base (unscaled) advance to match Balatro spacing
      Vector2 centerAnchor = penForThisLetter + new Vector2(0.5f * letter.Dims.X, 0.5f * letter.Dims.Y);
      Vector2 letterTopLeft = centerAnchor;
      if (isShadow)
      {
        // Cast shadow behind using parallax scaled by overall scale (approximate Balatro FONTSCALE/TILESCALE)
        Vector2 shadowOffset = par * MathF.Max(0.0001f, _cfg.Scale) * pxScale;
        letterTopLeft -= shadowOffset;
      }
      else
        letterTopLeft += tinyNormal;              // gentle nudge to avoid overdraw artifacts

      // Apply global text rotation around the current pen center before per-letter local transform.
      // We emulate by rotating the letter anchor relative to origin.
      if (!Mathf.IsZeroApprox(globalTextRot))
      {
        letterTopLeft = letterTopLeft.Rotated(globalTextRot);
      }

      // Per-letter transform (position, rotation, scale)
      // Balatro subtracts offset before global rotation; rotate offset with the phrase
      Vector2 offsetLocal = letter.Offset;
      if (!Mathf.IsZeroApprox(globalTextRot)) offsetLocal = offsetLocal.Rotated(globalTextRot);
      Vector2 xformPos = letterTopLeft - offsetLocal;
      // Apply rotation and local transform scaling
      DrawSetTransform(xformPos, letter.Rotation, new Vector2(transformScale, transformScale));

      // DrawString positions at BASELINE; anchor by half of the BASE size so scaling grows around center
      Vector2 baseline = new Vector2(-0.5f * letter.Dims.X, -0.5f * letter.Dims.Y + ascentBase);

      // Draw the letter grapheme
      DrawString(_cfg.Font, baseline, letter.Grapheme, HorizontalAlignment.Left, -1, fsBase, modulate);

      // Advance pen by base (Balatro-style) advance which already includes global scale and spacing
      float adv = letter.Dims.X;
      pen.X += adv;

      // Reset local transform to identity for the next letter
      DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
  }

  private Vector2 ComputeShadowParallax()
  {
    try
    {
      var vp = GetViewport();
      if (vp == null) return _cfg.ShadowOffsetPx;
      Rect2 vr = vp.GetVisibleRect();
      Vector2 center = vr.Position + vr.Size * 0.5f;
      Vector2 gp = GlobalPosition;
      float strength = _cfg.ShadowParallaxStrength;
      if (strength <= 0f) strength = 1.5f;
      float x = 0f;
      float halfW = MathF.Max(1f, vr.Size.X * 0.5f);
      x = ((gp.X - center.X) / halfW) * strength;   // -strength .. +strength across screen
      float y = -strength;                           // Upward, matching Moveable default of -1.5
      return new Vector2(x, y);
    }
    catch
    {
      return _cfg.ShadowOffsetPx;
    }
  }

  // ------------------------------
  // String building & metrics
  // ------------------------------

  private void Rebuild(bool firstPass)
  {
    // Establish new parts list (resolve providers on first pass or when provider parts are present)
    EnsureBuiltListSize(_cfg.Parts.Count);

    _boundsW = 0f;
    _boundsH = 0f;

    for (int p = 0; p < _cfg.Parts.Count; p++)
    {
      var part = _cfg.Parts[p];
      string computed = (part.Provider != null ? part.Provider() : part.Literal) ?? "";
      string prefix = part.Prefix ?? "";
      string suffix = part.Suffix ?? "";
      float partScale = part.Scale.HasValue ? part.Scale.Value : 1f;

      // Compose full string with prefix & suffix
      string full = prefix + computed + suffix;

      // Build letters if content changed or this is the first pass
      if (firstPass || _built[p].Content != full)
      {
        // If we are starting a new pop-in, mark letters as 0 initially (like original reset_pop_in path)
        bool resetPopIn = _cfg.PopInStartAt.HasValue;

        _built[p].Content = full;
        _built[p].Letters = new List<Letter>();

        float widthAccum = 0f;
        float heightMax = 0f;
        int totalLetters = 0;

        // Precompute indices for prefix/suffix coloring regions using grapheme counts (Lua uses utf8.chars)
        int partA = GraphemeCount(prefix);
        int suffixG = GraphemeCount(suffix);
        int fullG = GraphemeCount(full);
        int partB = fullG - suffixG;

        foreach (var g in Graphemes(full))
        {
          // Determine whether this grapheme is in prefix/suffix to set colors
          totalLetters++;
          Color? prefixCol = (totalLetters <= partA) ? part.OuterColour : (Color?)null;
          Color? suffixCol = (totalLetters > partB) ? part.OuterColour : (Color?)null;
          Color? innerCol = part.InnerColour;

          // Glyph metrics
          int fs = Math.Max(1, _cfg.FontSizePx);
          Vector2 sz = _cfg.Font.GetStringSize(g, HorizontalAlignment.Left, -1, fs);  // X = advance; Y = font height (not glyph height)
          float fontHeight = _cfg.Font.GetHeight(fs); // ascent+descent at fs
          // Advance packs base glyph width scaled by global and part scale; spacing is unscaled like Balatro
          float spacingPx = (_cfg.SpacingExtraPx ?? 0f);
          float adv = (sz.X * _cfg.Scale * partScale) + (_cfg.SpacingFactor * spacingPx);
          Vector2 dims = new Vector2(adv, fontHeight * _cfg.Scale * partScale * _cfg.TextHeightScale);

          // Initialize letter (carry over previous scale if you later want persistence)
          var letter = new Letter
          {
            Grapheme = g,
            // Scale is a per-frame delta around 1; base scale is baked into Dims
            Scale = 1f,
            BaseScale = partScale,
            PopIn = (firstPass ? (_cfg.PopInStartAt.HasValue ? 0f : 1f) : 1f),
            Offset = Vector2.Zero,
            Dims = dims,
            Rotation = 0f,
            PrefixColor = prefixCol,
            SuffixColor = suffixCol,
            InnerColor = innerCol
          };

          // If multiple strings, force initial pop-in = 0 for all but focused
          if (p > 0) letter.PopIn = 0f;

          _built[p].Letters.Add(letter);

          widthAccum += dims.X;
          heightMax = MathF.Max(heightMax, dims.Y);
        }

        _built[p].WidthPx = widthAccum;
        _built[p].HeightPx = heightMax;
      }

      // Track overall bounds
      _boundsW = MathF.Max(_boundsW, _built[p].WidthPx);
      _boundsH = MathF.Max(_boundsH, _built[p].HeightPx);
    }

    // Center offsets so each line draws centered inside overall bounds (like original W/H_offset logic)
    for (int i = 0; i < _built.Count; i++)
    {
      var b = _built[i];
      b.WOffsetPx = 0.5f * (_boundsW - b.WidthPx);
      b.HOffsetPx = 0.5f * (_boundsH - b.HeightPx);
    }
  }

  private void EnsureBuiltListSize(int count)
  {
    while (_built.Count < count) _built.Add(new BuiltString());
    while (_built.Count > count) _built.RemoveAt(_built.Count - 1);
  }

  // ------------------------------
  // Pop in/out & per-letter animation (AlignLetters)
  // ------------------------------
  private void AlignLetters()
  {
    float now = TimeSinceStart;

    // Handle multi-string pop cycling
    if (_popCyclePending)
    {
      // Move focus to next or random string
      if (_cfg.RandomElement && _built.Count > 1)
        _focusedIndex = (int)(GD.Randi() % (uint)_built.Count);
      else
        _focusedIndex = (_focusedIndex + 1) % _built.Count;

      _popCyclePending = false;
      ResetLettersForPopIn(_focusedIndex);

      _cfg.PopInStartAt = 0.1f; // start pop-in slightly delayed (matches original behavior)
      _createdTime = now;
    }

    var line = _built[_focusedIndex];
    int lastIndex = line.Letters.Count - 1;
    int pulseStartIndex = 0;
    int pulseLastIndex = lastIndex;
    bool pulseActive = _cfg.Pulse != null && line.Letters.Count > 0;
    if (pulseActive)
    {
      var pulse = _cfg.Pulse!;
      if (pulse.StartGlyphIndex >= line.Letters.Count)
      {
        pulseActive = false;
      }
      else
      {
        pulseStartIndex = Math.Clamp(pulse.StartGlyphIndex, 0, lastIndex);
        if (pulse.GlyphCount < 0)
          pulseLastIndex = lastIndex;
        else
          pulseLastIndex = Math.Clamp(pulseStartIndex + Math.Max(0, pulse.GlyphCount) - 1, pulseStartIndex, lastIndex);
      }
    }

    for (int k = 0; k < line.Letters.Count; k++)
    {
      var letter = line.Letters[k];
      int k1 = k + 1; // Lua indexing compatibility
      // Clear any leftover offsets unless an effect writes them
      letter.Offset = Vector2.Zero;

      // POP-OUT: when configured, decay pop-in over time on every letter
      if (_cfg.PopInStartAt == null && _popOutActive)
      {
        // Pop-out in progress: exact Balatro formula
        float minCycle = (_cfg.MinCycleTime <= 0f ? 1f : _cfg.MinCycleTime);
        float decay = minCycle - (now - _popOutStartTime) * (_popOutRate / minCycle);
        letter.PopIn = MathF.Min(1f, MathF.Max(decay, 0f));
        letter.PopIn *= letter.PopIn; // square
        if (k == lastIndex && letter.PopIn <= 0f && _built.Count > 1)
        {
          _popCyclePending = true;
          _popOutActive = false;
        }
      }
      else if (_cfg.PopInStartAt.HasValue)
      {
        // POP-IN: ramp up per-letter with cascading delay
        float prev = letter.PopIn;
        float elapsed = (now - _cfg.PopInStartAt.Value - _createdTime);
        float head = elapsed * line.Letters.Count * _cfg.PopInRate - k1 + 1;
        letter.PopIn = (float)Math.Clamp(head, _cfg.MinCycleTime == 0f ? 1f : 0f, 1f);
        letter.PopIn *= letter.PopIn; // ease-in (square)

        // Optional paper tick callback on first reveal for sparse strings
        if (prev <= 0f && letter.PopIn > 0f && !_cfg.Silent && (line.Letters.Count < 10 || (k1 % 2 == 0)))
        {
          // Original uses play_sound('paper1', pitch). Expose callback so users can play SFX in-game.
          _cfg.OnPopInTickSfx?.Invoke(0.45f + 0.05f * GD.Randf() + (0.3f / MathF.Max(1, line.Letters.Count)) * k1 + _cfg.PitchShift);
        }

        if (k == lastIndex && letter.PopIn >= 1f)
        {
          if (_built.Count > 1)
          {
            // Start pop-out after dynamic delay: time since pop_in start + configured pop_delay
            float baseDelay = MathF.Max(0f, _cfg.PopDelay <= 0 ? 1.5f : _cfg.PopDelay);
            float elapsedPopIn = 0f;
            if (_cfg.PopInStartAt.HasValue)
              elapsedPopIn = MathF.Max(0f, now - _cfg.PopInStartAt.Value - _createdTime);
            _cfg.PopInStartAt = null;
            _popOutStartTime = now + (elapsedPopIn + baseDelay);
            _popOutRate = 4f;
            _popOutActive = true;
          }
          else
          {
            _cfg.PopInStartAt = null;
          }
        }
      }

      // Reset base per-frame transform like Lua: rotation=0, scale=1 (baseline baked into dims)
      letter.Rotation = 0f;
      letter.Scale = 1f;

      // Optional swaying rotation (RotateMode==2 flips sign)
      if (_cfg.Rotate)
      {
        float sgn = (_cfg.RotateMode == 2) ? -1f : 1f;
        float n = -line.Letters.Count / 2f - 0.5f + k1;
        letter.Rotation = sgn * (0.2f * (n / MathF.Max(1, line.Letters.Count)) + 0.02f * MathF.Sin(2f * now + k1));
      }

      // Pulse: piecewise-linear envelope identical to Lua
      if (pulseActive && _cfg.Pulse != null)
      {
        var P = _cfg.Pulse;
        if (P != null)
        {
          bool inRange = k >= pulseStartIndex && k <= pulseLastIndex;
          if (inRange)
          {
            float localIndex = (k - pulseStartIndex) + 1f; // 1-based within the range
            float t1 = (P.StartTime - now) * P.Speed + localIndex + P.Width;
            float t2 = (now - P.StartTime) * P.Speed - localIndex + P.Width + 2f;
            float env = MathF.Max(MathF.Min(t1, t2), 0f);
            float add = (1f / MathF.Max(0.0001f, P.Width)) * P.Amount * env;
            letter.Scale += add;
            float scaleDelta = letter.Scale - 1f;
            letter.Rotation += scaleDelta * (0.02f * (-line.Letters.Count / 2f - 0.5f + k1));
            if (k == pulseLastIndex)
            {
              float rangeLetters = MathF.Max(1f, pulseLastIndex - pulseStartIndex + 1);
              float clearAt = P.StartTime + (rangeLetters + P.Width + 2f) / MathF.Max(0.0001f, P.Speed);
              if (now >= clearAt)
              {
                _cfg.Pulse = null;
                pulseActive = false;
              }
            }
          }
        }
      }

      // Quiver: small extra scale + noisy rotation
      if (_cfg.Quiver != null)
      {
        var Q = _cfg.Quiver;
        letter.Scale += 0.1f * Q.Amount;
        letter.Rotation += 0.3f * Q.Amount * (
            MathF.Sin(41.12342f * now * Q.Speed + k1 * 1223.2f) +
            MathF.Cos(63.21231f * now * Q.Speed + k1 * 1112.2f) * MathF.Sin(36.1231f * now * Q.Speed) +
            MathF.Cos(95.123f * now * Q.Speed + k1 * 1233.2f) -
            MathF.Sin(30.133421f * now * Q.Speed + k1 * 123.2f)
        );
        if (_quiverStopAt > 0f && TimeSinceStart >= _quiverStopAt)
        {
          _cfg.Quiver = null;
          _quiverStopAt = 0f;
        }
      }

      // Vertical float (sinusoidal) — tuned to Balatro's subtle feel
      if (_cfg.Float)
      {
        int fsBase = Math.Max(1, _cfg.FontSizePx);
        float fontH = _cfg.Font.GetHeight(fsBase) * _cfg.Scale * (letter.BaseScale <= 0f ? 1f : letter.BaseScale);
        // Very subtle amplitude: ~3% of font height
        float amp = 0.03f * MathF.Max(1f, fontH);
        // Match Balatro phase: 2.666 * t + 200 * k
        float phase = 2.666f * now + 200f * k1;
        float scaleDelta = letter.Scale - 1f;
        letter.Offset.Y = MathF.Sqrt(Math.Max(0.0001f, _cfg.Scale)) * (2f + amp * MathF.Sin(phase)) + 60f * scaleDelta;
      }

      // Bump: envelope-based bounce
      if (_cfg.Bump)
      {
        float env = MathF.Max(0f, (5f + _cfg.BumpRate) * MathF.Sin(_cfg.BumpRate * now + 200f * k1) - 3f - _cfg.BumpRate);
        letter.Offset.Y = _cfg.BumpAmount * (float)Math.Sqrt(Math.Max(0.0001f, _cfg.Scale)) * 7f * env;
        if (_bumpStopAt > 0f && TimeSinceStart >= _bumpStopAt)
        {
          _cfg.Bump = false;
          _bumpStopAt = 0f;
        }
      }

      // Write back
      line.Letters[k] = letter;
    }
  }

  private void PopOut(float popOutTimer)
  {
    // Start pop-out after configured delay (Lua pop_delay), then decay handled in AlignLetters
    _cfg.PopInStartAt = null;
    float delay = MathF.Max(0f, _cfg.PopDelay <= 0 ? 1.5f : _cfg.PopDelay);
    _popOutStartTime = TimeSinceStart + delay;
    _popOutRate = MathF.Max(0.0001f, popOutTimer);
    _popOutActive = true;
  }

  private void ResetLettersForPopIn(int lineIndex)
  {
    if (lineIndex < 0 || lineIndex >= _built.Count) return;
    var line = _built[lineIndex];
    for (int i = 0; i < line.Letters.Count; i++)
    {
      var l = line.Letters[i];
      l.PopIn = 0f;
      line.Letters[i] = l;
    }
    _cfg.PopInStartAt = 0f;
  }

  // ------------------------------
  // Utilities
  // ------------------------------
  private static void CopyConfig(Config src, Config dst)
  {
    dst.Parts = new List<TextPart>(src.Parts);
    dst.Shadow = src.Shadow;
    dst.ShadowColor = src.ShadowColor;
    dst.ShadowOffsetPx = src.ShadowOffsetPx;
    dst.Scale = src.Scale;
    dst.PopInRate = src.PopInRate;
    dst.BumpRate = src.BumpRate;
    dst.BumpAmount = src.BumpAmount;
    dst.PopInStartAt = src.PopInStartAt;
    dst.MaxWidthPx = src.MaxWidthPx;
    dst.SpacingExtraPx = src.SpacingExtraPx;
    dst.TextRotationRad = src.TextRotationRad;
    dst.PopDelay = src.PopDelay;
    dst.RandomElement = src.RandomElement;
    dst.MinCycleTime = src.MinCycleTime;
    dst.TextPixelOffset = src.TextPixelOffset;
    dst.OffsetYExtraPx = src.OffsetYExtraPx;
    dst.Colours = new List<Color>(src.Colours);
    dst.Silent = src.Silent;
    dst.PitchShift = src.PitchShift;
    dst.Font = src.Font;
    dst.FontSizePx = Math.Max(1, src.FontSizePx);
    dst.TextHeightScale = MathF.Max(0.0001f, src.TextHeightScale);
    dst.Rotate = src.Rotate;
    dst.RotateMode = src.RotateMode;
    dst.Pulse = src.Pulse;
    dst.Quiver = src.Quiver;
    dst.Float = src.Float;
    dst.Bump = src.Bump;
    dst.OnPopInTickSfx = src.OnPopInTickSfx;
  }

  private static float TimeSinceStart => (float)Time.GetTicksMsec() / 1000f;

  // Public lightweight mutator to adjust global scale without resetting other state.
  public void SetScale(float scale)
  {
    _cfg.Scale = MathF.Max(0.0001f, scale);
    // Recalculate metrics with new scale
    Rebuild(firstPass: true);
    QueueRedraw();
  }

  // Public: transient tilt (Moveable:juice_up rotation component)
  public void TriggerTilt(float amount = 0.4f, float? rotAmt = null, float duration = 0.4f)
  {
    float dir = (GD.Randf() < 0.5f ? -1f : 1f);
    float ra = rotAmt.HasValue ? rotAmt.Value : (0.6f * amount * dir);
    _tiltStart = TimeSinceStart;
    _tiltEnd = _tiltStart + MathF.Max(0.0001f, duration);
    _tiltRAmt = ra;
    _tiltActive = true;
    QueueRedraw();
  }
}
