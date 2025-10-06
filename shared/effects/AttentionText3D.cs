using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

#nullable enable

// ─────────────────────────────────────────────────────────────────────────────
// AttentionTextSegment: mirrors Lua's segment tables (prefix/suffix, inner/outer colors, part scale, dynamic source)
// ─────────────────────────────────────────────────────────────────────────────
[GlobalClass]
public partial class AttentionTextSegment : Resource
{
  [Export] public string Prefix { get; set; } = "";
  [Export] public string Text { get; set; } = ""; // literal when Source* not used
  [Export] public string Suffix { get; set; } = "";

  // Dynamic value source ≈ {ref_table=..., ref_value=...}
  [Export] public NodePath? SourceNode { get; set; }
  [Export] public string SourceProperty { get; set; } = "";

  // Per-part scale (Lua: v.scale)
  [Export] public float PartScale { get; set; } = 1f;

  // Colors (Lua: v.colour, v.outer_colour)
  [Export] public bool UseInnerColor { get; set; } = false;
  [Export] public Color InnerColor { get; set; } = Colors.White;

  [Export] public bool UseOuterColor { get; set; } = false;
  [Export] public Color OuterColor { get; set; } = Colors.White;

  // Compose: returns (text, prefixLen, suffixLen, scale, inner?, outer?)
  public (string text, int prefixLen, int suffixLen, float partScale, Color? inner, Color? outer)
      Compose(Node context)
  {
    string core = Text;
    if (SourceNode != null && SourceNode.ToString() != string.Empty && !string.IsNullOrEmpty(SourceProperty))
    {
      var node = context.GetNodeOrNull(SourceNode);
      if (node != null)
      {
        var v = node.Get(SourceProperty);
        core = v.VariantType == Variant.Type.Nil ? "" : v.ToString();
      }
    }

    string final = (Prefix ?? "") + core + (Suffix ?? "");
    int pLen = (Prefix ?? "").Length;
    int sLen = (Suffix ?? "").Length;
    Color? inner = UseInnerColor ? InnerColor : null;
    Color? outer = UseOuterColor ? OuterColor : null;
    return (final, pLen, sLen, Mathf.Max(0.001f, PartScale), inner, outer);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// AttentionText3D: behavior‑level port of DynaText (3D Label3D glyphs, per-letter transforms)
// ─────────────────────────────────────────────────────────────────────────────
public partial class AttentionText3D : Node3D
{
  // ── Content ───────────────────────────────────────────────────────────────
  [ExportGroup("Content")]
  [Export] public string Text { get; set; } = "HELLO WORLD";
  [Export] public Godot.Collections.Array<AttentionTextSegment> Segments { get; set; } = new();
  [Export] public Godot.Collections.Array<string> Variants { get; set; } = new();
  [Export(PropertyHint.Range, "0,360,1")] public float TextYawDegrees { get; set; } = 0f;

  // Palette when a glyph has no explicit color from segments (Lua: colours[k%#colours+1])
  [Export]
  public Godot.Collections.Array<Color> Palette { get; set; } =
      new() { new Color(1f, 0.25f, 0.25f, 1f) };

  // Global scale (Lua: self.scale)
  [Export] public new float GlobalScale { get; set; } = 1f;

  // Optional auto downscale to fit width (Lua: maxw)
  [Export] public float MaxWorldWidth { get; set; } = 0f;

  // Text offset (Lua: font.TEXT_OFFSET * scale + config offset)
  [Export] public Vector2 TextOffset { get; set; } = Vector2.Zero;

  // ── Font & Visual ─────────────────────────────────────────────────────────
  [ExportGroup("Font & Visual")]
  [Export] public FontFile? Font { get; set; } = null;
  [Export] public int FontSize { get; set; } = 32;
  [Export] public int OutlineSize { get; set; } = 6;
  [Export] public Color OutlineColor { get; set; } = Colors.White;
  [Export] public Color DefaultTextColor { get; set; } = new Color(1f, 0.25f, 0.25f, 1f);
  [Export] public float PixelSizeWorld { get; set; } = 0.01f;
  [Export] public bool Shaded { get; set; } = false;

  // Spacing (Lua: + 2.7 * spacing * .. per glyph)
  [Export] public float Spacing { get; set; } = 0f;
  [Export] public float SpacingFactor { get; set; } = 2.7f;

  // Advance approximation fallback if we cannot measure glyphs from font
  [Export(PropertyHint.Range, "0.3,1.2,0.01")] public float AdvanceFactor { get; set; } = 0.6f;

  // ── Shadow ────────────────────────────────────────────────────────────────
  [ExportGroup("Shadow")]
  [Export] public bool EnableShadow { get; set; } = true;
  [Export] public Color ShadowColor { get; set; } = new Color(0, 0, 0, 0.35f);
  [Export] public float ShadowOffset { get; set; } = 0.0075f; // world units
  [Export] public Vector2 ShadowParallax { get; set; } = new Vector2(1, 1); // normalized internally

  // ── Timing & Cycling ──────────────────────────────────────────────────────
  [ExportGroup("Timing")]
  [Export] public float PopInRate { get; set; } = 3.0f;             // Lua default ~3
  [Export] public bool SequentialPopIn { get; set; } = true;
  [Export] public float PopDelay { get; set; } = 1.5f;               // Lua: pop_delay
  [Export] public float PopOutRate { get; set; } = 4.0f;             // Lua: pop_out(4)
  [Export] public float MinCycleTime { get; set; } = 1.0f;           // Lua: config.min_cycle_time (0 => instant)
  [Export] public bool RandomVariant { get; set; } = false;

  [ExportGroup("Auto Free (optional)")]
  [Export] public bool AutoFreeAfter { get; set; } = false;
  [Export] public float HoldSeconds { get; set; } = 0.45f;
  [Export] public float FadeOutSeconds { get; set; } = 0.33f;

  // ── Auto Pop Out (single string) ──────────────────────────────────────────
  [ExportGroup("Auto Pop Out")]
  [Export] public bool AutoPopOut { get; set; } = false; // pop out after fully in, even with a single string

  // ── Effects (parity with Lua) ─────────────────────────────────────────────
  [ExportGroup("Effects")]
  [Export] public bool EnableRotate { get; set; } = false;
  [Export(PropertyHint.Enum, "Off,Clockwise,CounterClockwise")] public int RotateMode { get; set; } = 0;

  [Export] public bool EnableFloat { get; set; } = false;
  [Export] public float FloatFrequency { get; set; } = 2.666f;
  [Export] public float FloatPhasePerLetter { get; set; } = 5.227f; // ≈ 200 rad mod 2π
  [Export] public float FloatBase { get; set; } = 0f;
  [Export] public float FloatAmplitude { get; set; } = 0.08f;
  [Export] public float FloatKick { get; set; } = 0.06f;

  [Export] public bool EnableBump { get; set; } = false;
  [Export] public float BumpRate { get; set; } = 2.666f;
  [Export] public float BumpAmount { get; set; } = 1.0f;

  [Export] public bool EnableQuiver { get; set; } = false;
  [Export] public float QuiverAmount { get; set; } = 0.15f;
  [Export] public float QuiverSpeed { get; set; } = 0.5f;

  [Export] public float PulseSpeed { get; set; } = 40.0f; // Lua: pulse.speed
  [Export] public float PulseWidth { get; set; } = 2.5f;  // Lua: pulse.width
  [Export] public float ScalePulseAmount { get; set; } = 0.2f; // Lua: pulse.amount

  // ── Internals ─────────────────────────────────────────────────────────────
  private struct Letter
  {
    public Label3D Label;
    public Label3D? Shadow;
    public float PopIn;             // 0..1 (squared easing applied)
    public float Scale;             // final node scale (computed each frame)
    public float Width;             // world advance including spacing and part scale and global scale
    public Vector2 DimsWorld;       // glyph width/height in world units (pre node-scale)
    public Vector2 Offset;          // animated offset (y used by float/bump)
    public float Angle;             // Z rotation in radians
    public Vector3 BaseLocalPos;    // per-letter baseline center
    public float PartScale;         // base scale from segment (Lua: part_scale)
    public Color? ExplicitColor;    // inner/outer color from segment tagging
  }

  private readonly List<Letter> _letters = new();
  private float _age = 0f;
  private float _popInStart = 0f;       // Lua: config.pop_in
  private float _createdTime = 0f;      // Lua: created_time
  private bool _popOutActive = false;
  private float _popOutStart = 0f;      // Lua: pop_out_time
  private float _lastFullInAge = -999f;
  private bool _pulseActive = false;
  private float _pulseStartAge = 0f;
  private int _focusedVariant = 0;
  private readonly RandomNumberGenerator _rng = new();
  [Export] public bool DebugLogOnce { get; set; } = false;
  private bool _didDebugLog = false;

  [ExportGroup("Debug")]
  [Export] public bool DebugLog { get; set; } = false;
  [Export(PropertyHint.Range, "0.01,1.0,0.01")] public float DebugFrameInterval { get; set; } = 0.2f;
  private float _debugTimer = 0f;

  private string _currentComposed = "";
  private List<(int start, int end, float scale, Color? inner, Color? outer, int prefixLen, int suffixLen)> _parts = new();

  public override void _Ready()
  {
    _rng.Randomize();
    // Align created time with local age timeline so timing matches Lua behaviour
    _createdTime = _age;
    ComposeContent();
    BuildLetters();
    if (Variants.Count > 1) _popInStart = 0f;
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;
    _age += dt;
    if (DebugLog)
    {
      _debugTimer += dt;
    }

    FaceCameraAndApplyYaw();
    ComposeContentIfChanged();
    UpdateLetters(dt);

    if (AutoFreeAfter && _age > HoldSeconds + FadeOutSeconds)
      QueueFree();
  }

  // ── Public API to mirror Lua ──────────────────────────────────────────────
  public void PopIn(float startDelay = 0f)
  {
    _popOutActive = false;
    _popInStart = startDelay;
    _createdTime = _age;
    for (int i = 0; i < _letters.Count; i++)
    {
      var ld = _letters[i]; ld.PopIn = 0f; _letters[i] = ld;
    }
  }

  public void PopOut(float rate = 4f)
  {
    PopOutRate = rate;
    _popOutActive = true;
    _popOutStart = _age + Mathf.Max(0f, PopDelay);
  }

  public void TriggerPulse(float amount = 0.2f)
  {
    ScalePulseAmount = amount;
    _pulseActive = true;
    _pulseStartAge = _age;
  }

  public void SetQuiver(float amount = 0.7f, float speed = 0.5f)
  {
    EnableQuiver = true;
    QuiverAmount = amount;
    QuiverSpeed = speed;
  }

  public void ClearQuiver() => EnableQuiver = false;

  public void RequestRebuild()
  {
    ComposeContent();
    BuildLetters();
    _createdTime = _age;
  }

  // Quick preset to mimic Balatro scoring text feel
  public void ConfigureScoringStyle(float popDelay = 0.25f, float popInRate = 3f, float popOutRate = 4f, float pulseAmount = 0.2f)
  {
    EnableRotate = true;
    RotateMode = 1; // Clockwise
    SequentialPopIn = true;
    PopInRate = popInRate;
    MinCycleTime = 1f;
    PopDelay = popDelay;
    AutoPopOut = true;
    PopOutRate = popOutRate;
    EnableQuiver = false;
    EnableFloat = false;
    TriggerPulse(pulseAmount);
  }

  // ── Composition ───────────────────────────────────────────────────────────
  private void ComposeContentIfChanged()
  {
    var before = _currentComposed;
    var beforeCount = _letters.Count;
    ComposeContent();
    if (_currentComposed != before || _letters.Count == 0 || beforeCount != _currentComposed.Length)
    {
      BuildLetters();
      _popInStart = 0f;
      _createdTime = _age;
      if (DebugLog)
      {
        GD.Print($"[AT] Rebuild: text='{_currentComposed}' len={_currentComposed.Length} time={_age:F2}");
      }
    }
  }

  private void ComposeContent()
  {
    if (Variants.Count > 0)
    {
      Text = Variants[Mathf.Clamp(_focusedVariant, 0, Variants.Count - 1)];
      Segments.Clear();
    }

    _parts.Clear();

    if (Segments.Count == 0)
    {
      _currentComposed = Text ?? "";
    }
    else
    {
      var pieces = new List<string>();
      int cursor = 0;

      foreach (var seg in Segments)
      {
        var (txt, pLen, sLen, pScale, inner, outer) = seg.Compose(this);
        if (string.IsNullOrEmpty(txt)) continue;

        pieces.Add(txt);
        int start = cursor;
        int end = cursor + txt.Length - 1;
        cursor += txt.Length;

        _parts.Add((start, end, pScale, inner, outer, pLen, sLen));
      }

      _currentComposed = string.Join("", pieces);
    }
  }

  // ── Building glyph nodes ──────────────────────────────────────────────────
  private void BuildLetters()
  {
    foreach (var child in GetChildren())
    {
      if (child is Label3D lb) { RemoveChild(lb); lb.QueueFree(); }
    }
    _letters.Clear();

    // Determine effective global scale if constrained by MaxWorldWidth
    // We first measure advances WITHOUT applying node scale, then compute fitting scale.
    float totalWidthUnscaled = 0f;
    for (int i = 0; i < _currentComposed.Length; i++)
    {
      var dimsPx = MeasureCharPx(_currentComposed[i]);
      float baseW = dimsPx.X * PixelSizeWorld;                // world units at node scale = 1
      float spacingW = SpacingFactor * Spacing * PixelSizeWorld * FontSize * Mathf.Clamp(AdvanceFactor, 0.3f, 1.2f);
      float partScale = GetPartScaleForIndex(i);
      float step = (baseW * partScale) + spacingW;            // include part scale in step (Lua)
      totalWidthUnscaled += step;
    }

    float effectiveScale = GlobalScale;
    if (MaxWorldWidth > 0f && totalWidthUnscaled * GlobalScale > MaxWorldWidth)
      effectiveScale = Mathf.Max(0.001f, GlobalScale * (MaxWorldWidth / (totalWidthUnscaled * GlobalScale)));

    // Now build each letter; pivot at glyph center (Center/Center) to match Lua draw ox/oy
    float x = 0f;
    for (int i = 0; i < _currentComposed.Length; i++)
    {
      char ch = _currentComposed[i];
      var label = new Label3D
      {
        Text = ch.ToString(),
        Modulate = DefaultTextColor,
        FontSize = FontSize,
        OutlineSize = OutlineSize,
        OutlineModulate = OutlineColor,
        PixelSize = PixelSizeWorld,
        Shaded = Shaded,
        HorizontalAlignment = Godot.HorizontalAlignment.Center,
        VerticalAlignment = Godot.VerticalAlignment.Center
      };
      if (Font != null) label.Font = Font;
      AddChild(label);

      Label3D? shadow = null;
      if (EnableShadow)
      {
        shadow = new Label3D
        {
          Text = ch.ToString(),
          Modulate = ShadowColor,
          FontSize = FontSize,
          OutlineSize = 0,
          PixelSize = PixelSizeWorld,
          Shaded = Shaded,
          HorizontalAlignment = Godot.HorizontalAlignment.Center,
          VerticalAlignment = Godot.VerticalAlignment.Center
        };
        if (Font != null) shadow.Font = Font;
        AddChild(shadow);
      }

      // Per-part attribution for this glyph
      float partScale = 1f;
      Color? explicitColor = null;
      if (_parts.Count > 0)
      {
        foreach (var p in _parts)
        {
          if (i >= p.start && i <= p.end)
          {
            if (p.outer.HasValue)
            {
              bool inPrefix = (i - p.start) < p.prefixLen;
              bool inSuffix = (p.end - i) < p.suffixLen;
              if (inPrefix || inSuffix) explicitColor = p.outer.Value;
            }
            if (!explicitColor.HasValue && p.inner.HasValue) explicitColor = p.inner.Value;
            partScale = p.scale;
            break;
          }
        }
      }

      // Measure pixel dims and convert to world units (pre node-scale)
      var dimsPx = MeasureCharPx(ch);
      Vector2 dimsWorld = new Vector2(dimsPx.X * PixelSizeWorld, dimsPx.Y * PixelSizeWorld);
      float spacingW = SpacingFactor * Spacing * PixelSizeWorld * FontSize * Mathf.Clamp(AdvanceFactor, 0.3f, 1.2f);

      // Like Lua: step uses self.scale * part_scale (we apply GlobalScale later to node scale; for step we insert partScale now)
      float step = (dimsWorld.X * partScale) + spacingW;

      // Build letter record
      var ld = new Letter
      {
        Label = label,
        Shadow = shadow,
        PopIn = 0f,
        Scale = effectiveScale, // base node scale (will be multiplied further each frame)
        Width = step * effectiveScale, // step scales with global scale like Lua stepping
        DimsWorld = dimsWorld,
        Offset = Vector2.Zero,
        Angle = 0f,
        BaseLocalPos = new Vector3(0f, 0f, 0f), // set below
        PartScale = partScale,
        ExplicitColor = explicitColor
      };

      // Place letter center at current x + 0.5*step (center pivot behavior)
      float centerX = x + 0.5f * ld.Width;
      ld.BaseLocalPos = new Vector3(centerX, 0, 0);
      _letters.Add(ld);

      x += ld.Width; // advance
    }

    // Center the whole string around origin horizontally, apply user offset
    float halfTotal = x * 0.5f;
    for (int i = 0; i < _letters.Count; i++)
    {
      var ld = _letters[i];
      ld.BaseLocalPos -= new Vector3(halfTotal, 0, 0);
      ld.BaseLocalPos += new Vector3(TextOffset.X, TextOffset.Y, 0);
      _letters[i] = ld;
    }

    Rotation = new Vector3(Rotation.X, Mathf.DegToRad(TextYawDegrees), Rotation.Z);

    if (DebugLogOnce && !_didDebugLog)
    {
      float total = 0f;
      foreach (var l in _letters) total += l.Width;
      GD.Print($"[AttentionText3D] text='{_currentComposed}' len={_letters.Count} totalW={total:F3} step0={( _letters.Count>0 ? _letters[0].Width:0):F3}");
      _didDebugLog = true;
    }
  }

  // Per-character pixel size (width, height). Use a robust approximation to avoid API differences across Godot versions.
  private Vector2 MeasureCharPx(char ch)
  {
    // Approximate monospace-like advance for pixel fonts; this avoids Godot API overload issues
    float w = FontSize * Mathf.Clamp(AdvanceFactor, 0.3f, 1.2f);
    float h = FontSize;
    return new Vector2(w, h);
  }

  private float GetPartScaleForIndex(int i)
  {
    if (_parts.Count == 0) return 1f;
    foreach (var p in _parts)
    {
      if (i >= p.start && i <= p.end) return p.scale;
    }
    return 1f;
  }

  // ── Frame simulation ───────────────────────────────────────────────────────
  private void FaceCameraAndApplyYaw()
  {
    var cam = GetViewport()?.GetCamera3D();
    if (cam == null) return;

    Vector3 toCam = cam.GlobalTransform.Origin - GlobalTransform.Origin;
    Vector3 planar = new Vector3(toCam.X, 0, toCam.Z);
    if (planar.LengthSquared() <= 0.000001f) return;

    var basis = Basis.LookingAt((-planar).Normalized(), Vector3.Up);
    GlobalTransform = new Transform3D(basis, GlobalTransform.Origin);
    RotateObjectLocal(Vector3.Up, Mathf.DegToRad(TextYawDegrees));
  }

  private void UpdateLetters(float dt)
  {
    int count = _letters.Count;
    if (count == 0) return;

    // Optional auto-fade for "damage numbers"
    float alphaMul = 1f;
    if (AutoFreeAfter && _age > HoldSeconds)
    {
      float k = Mathf.Clamp((_age - HoldSeconds) / Mathf.Max(0.0001f, FadeOutSeconds), 0f, 1f);
      alphaMul = 1f - k;
    }

    // Shadow parallax normalization (Lua: _shadow_norm)
    Vector2 sp = ShadowParallax;
    float plen = Mathf.Max(0.00001f, Mathf.Sqrt(sp.X * sp.X + sp.Y * sp.Y));
    Vector2 shadowNorm = sp / plen;
    Vector2 shadowWorld = shadowNorm * PixelSizeWorld; // 1 px nudge, like Lua used font scale

    // Active pop-out: apply exact Lua formula per frame
    if (_popOutActive && _age >= _popOutStart)
    {
      float mct = (MinCycleTime <= 0f) ? 1f : MinCycleTime; // Lua: (self.config.min_cycle_time or 1)
      float pop = Mathf.Clamp(mct - (_age - _popOutStart) * PopOutRate / mct, 0f, 1f);
      pop *= pop;

      for (int i = 0; i < count; i++)
      {
        var ld = _letters[i]; ld.PopIn = pop; _letters[i] = ld;
      }

      // When the last glyph is fully popped-out, cycle variants if any
      if (_letters[count - 1].PopIn <= 0f)
      {
        if (Variants.Count > 1)
        {
          if (RandomVariant)
          {
            int next;
            do next = _rng.RandiRange(0, Variants.Count - 1);
            while (next == _focusedVariant && Variants.Count > 1);
            _focusedVariant = next;
          }
          else
          {
            _focusedVariant = (_focusedVariant + 1) % Variants.Count;
          }
          ComposeContent();
          BuildLetters();
          PopIn(0.1f);
          _popOutActive = false;
          _lastFullInAge = -999f;
        }
        else
        {
          if (AutoPopOut)
          {
            // For single-string auto pop-out, remove the node once fully popped out
            _popOutActive = false;
            QueueFree();
            return;
          }
          _popOutActive = false;
        }
      }
    }

    // Per-letter simulation & draw state
    bool allFullyIn = true;
    bool emitFrameLog = DebugLog && _debugTimer >= DebugFrameInterval;
    for (int i = 0; i < count; i++)
    {
      var ld = _letters[i];

      // Pop-in (Lua exact): pop = clamp( (age - pop_in_start - created_time) * #string * rate - i + 1, (min_cycle_time==0)?1:0, 1 )^2
      float pop = ld.PopIn;
      if (!_popOutActive)
      {
        float baseT = Mathf.Max(0f, (_age - _createdTime - _popInStart));
        float raw = SequentialPopIn
            ? baseT * count * Mathf.Max(0.01f, PopInRate) - i + 1f
            : baseT * Mathf.Max(0.01f, PopInRate);
        float clampMin = (MinCycleTime <= 0f) ? 1f : 0f; // Lua: instant full when min_cycle_time==0
        pop = Mathf.Clamp(raw, clampMin, 1f);
        pop *= pop;
      }
      if (pop < 1f - 1e-4f) allFullyIn = false;

      // Rotate (Lua curve)
      float angle = 0f;
      if (EnableRotate && RotateMode != 0)
      {
        float dir = (RotateMode == 2) ? -1f : 1f;
        float idxTerm = 0.2f * (-count / 2f - 0.5f + i) / Mathf.Max(1f, count);
        float timeTerm = 0.02f * Mathf.Sin(2f * _age + i);
        angle = dir * (idxTerm + timeTerm);
      }

      // Quiver additions
      float quiverAngle = 0f;
      float quiverScaleAdd = 0f;
      if (EnableQuiver)
      {
        float q = Mathf.Sin(41.12342f * _age * QuiverSpeed + i * 1223.2f)
                + Mathf.Cos(63.21231f * _age * QuiverSpeed + i * 1112.2f) * Mathf.Sin(36.1231f * _age * QuiverSpeed)
                + Mathf.Cos(95.123f * _age * QuiverSpeed + i * 1233.2f)
                - Mathf.Sin(30.133421f * _age * QuiverSpeed + i * 123.2f);
        quiverAngle = 0.3f * QuiverAmount * q;
        quiverScaleAdd = 0.1f * QuiverAmount;
      }

      // Pulse (traveling envelope; additive to letter.scale)
      float pulseAdd = 0f;
      if (_pulseActive)
      {
        // Lua is 1-based for k indices; mirror with idx = i+1
        int idx = i + 1;
        float s1 = (_pulseStartAge - _age) * PulseSpeed + idx + PulseWidth;
        float s2 = (_age - _pulseStartAge) * PulseSpeed - idx + PulseWidth + 2.0f;
        float envelope = Mathf.Max(0f, Mathf.Min(s1, s2));
        pulseAdd = (PulseWidth > 0f) ? (ScalePulseAmount * (envelope / PulseWidth)) : 0f;

        float waveSpan = (2f * count) / Mathf.Max(0.001f, PulseSpeed);
        if (_age - _pulseStartAge > waveSpan + 0.1f) _pulseActive = false;
      }

      // Final per-letter node scale: real_pop_in * (effect_scale) * global
      // Lua: letter.scale starts at 1 and only pulse/quiver add to it. PartScale affects advance/dims, not draw scale.
      float letterScaleCore = 1f + quiverScaleAdd + pulseAdd;
      float realPopIn = (MinCycleTime <= 0f) ? 1f : pop;
      float finalNodeScale = Mathf.Max(0.0001f, realPopIn * letterScaleCore * GlobalScale);

      // Float/Bump offsets (order after pulse/quiver to use effect scale)
      float offY = 0f;
      if (EnableFloat)
      {
        float wobble = FloatAmplitude * Mathf.Sin(FloatFrequency * _age + FloatPhasePerLetter * i);
        float kick = FloatKick * ((letterScaleCore) - 1f); // Lua: +60*(letter.scale-1)
        offY = FloatBase + wobble + kick;
      }
      if (EnableBump)
      {
        float bump = (5f + BumpRate) * Mathf.Sin(BumpRate * _age + 200f * i) - 3f - BumpRate;
        bump = Mathf.Max(0f, bump);
        offY += BumpAmount * Mathf.Sqrt(Mathf.Max(0.001f, GlobalScale)) * 7f * bump;
      }

      ld.PopIn = pop;
      ld.Scale = finalNodeScale;
      ld.Angle = angle + quiverAngle;
      ld.Offset = new Vector2(0f, offY);

      // Apply transforms: position is base center plus offsets; rotation around center (Center alignment)
      Vector3 letterPos = ld.BaseLocalPos + new Vector3(0, ld.Offset.Y, 0);
      ld.Label.Position = letterPos;
      ld.Label.Scale = new Vector3(ld.Scale, ld.Scale, ld.Scale);
      // Add slight rotation coupling during pulse, like Lua does: r += (letter.scale-1)*0.02*(index term)
      float pulseRot = 0f;
      if (_pulseActive)
      {
        float idxTerm = 0.02f * (-count / 2f - 0.5f + (i + 1));
        pulseRot = (letterScaleCore - 1f) * idxTerm;
      }
      ld.Label.Rotation = new Vector3(0, 0, ld.Angle + pulseRot);

      // Colors: explicit → palette → default
      Color col = ld.ExplicitColor ?? (Palette.Count > 0 ? Palette[(i % Palette.Count)] : DefaultTextColor);
      col.A *= alphaMul;
      ld.Label.Modulate = col;

      // Shadow: center-aligned and offset by parallax + ShadowOffset
      if (ld.Shadow != null)
      {
        ld.Shadow.Position = letterPos + new Vector3(shadowWorld.X, -shadowWorld.Y, 0) + new Vector3(ShadowOffset, -ShadowOffset, 0);
        ld.Shadow.Scale = ld.Label.Scale;
        ld.Shadow.Rotation = ld.Label.Rotation;
        var sc = ShadowColor; sc.A *= alphaMul; ld.Shadow.Modulate = sc;
      }

      _letters[i] = ld;

      if (emitFrameLog && (i == 0 || i == count - 1))
      {
        GD.Print($"[AT] i={(i+1)}/{count} pop={pop:F2} ang={(ld.Angle+pulseRot):F2} scale={ld.Scale:F2} offY={ld.Offset.Y:F2} pulse={_pulseActive}");
      }
    }
    if (emitFrameLog)
    {
      GD.Print($"[AT] age={_age:F2} popOut={_popOutActive} minCycle={MinCycleTime:F2} popDelay={PopDelay:F2}");
      _debugTimer = 0f;
    }

    // Cycle after fully popped-in, honoring PopDelay, like Lua
    if (!_popOutActive && (Variants.Count > 1 || AutoPopOut))
    {
      if (allFullyIn)
      {
        if (_lastFullInAge < 0f) _lastFullInAge = _age;
        if (_age - _lastFullInAge >= Mathf.Max(0f, PopDelay))
          PopOut(PopOutRate);
      }
    }
  }

  // ── Convenience factory (legacy) ───────────────────────────────────────────
  [Obsolete("Use DamageNumber3D.Spawn(context, target, amount, color)")]
  public static void SpawnForDamage(Node context, Node3D target, float amount, Color? color = null)
  {
    DamageNumber3D.Spawn(context, target, amount, color);
  }
}
