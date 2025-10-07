using Godot;
using System;
using System.Collections.Generic;

#nullable enable

// Batched per-letter 3D text that mirrors AttentionText3D's look/feel but renders via Text3DBatcher MultiMeshes.
public partial class AttentionText3DBatched : Text3DString
{
  [ExportGroup("Content")] public string Text { get; set; } = "";

  [ExportGroup("Font & Visual")]
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public FontFile? Font { get; set; }
  [Export] public int FontSize { get; set; } = 56;
  [Export] public float PixelSizeWorld { get; set; } = 0.01f;
  [Export] public bool Shaded { get; set; } = false;
  [Export] public Color DefaultColor { get; set; } = Colors.White;
  [Export] public int OutlineSize { get; set; } = 14;
  [Export] public Color OutlineColor { get; set; } = Colors.Black;

  [ExportGroup("Layout")]
  [Export] public float AdvanceFactor { get; set; } = 0.45f; // matches AttentionText3D default for numbers
  [Export] public float Spacing { get; set; } = 0f;
  [Export] public float SpacingFactor { get; set; } = 2.7f;
  [Export] public Vector2 TextOffset { get; set; } = Vector2.Zero;

  [ExportGroup("Facing")]
  [Export] public bool FaceCamera { get; set; } = true;
  [Export(PropertyHint.Range, "0,360,1")] public float TextYawDegrees { get; set; } = 0f;

  [ExportGroup("Effects")]
  [Export] public bool EnableRotate { get; set; } = true;
  [Export] public int RotateMode { get; set; } = 1; // 0 none, 1 clockwise, 2 ccw
  [Export] public bool SequentialPopIn { get; set; } = true;
  [Export] public float PopInRate { get; set; } = 6.0f;
  [Export] public float MinCycleTime { get; set; } = 1.0f; // use 1 for damage numbers
  [Export] public float PopDelay { get; set; } = 0.25f;
  [Export] public float PopOutRate { get; set; } = 4.0f;
  [Export] public bool AutoPopOut { get; set; } = true;
  [Export] public bool EnableFloat { get; set; } = false;
  [Export] public float FloatFrequency { get; set; } = 2.666f;
  [Export] public float FloatAmplitude { get; set; } = 0.08f;
  [Export] public float FloatBase { get; set; } = 0f;
  [Export] public bool EnableQuiver { get; set; } = false;
  [Export] public float QuiverAmount { get; set; } = 0.15f;
  [Export] public float QuiverSpeed { get; set; } = 0.5f;
  [Export] public float PulseSpeed { get; set; } = 8.0f;
  [Export] public float PulseAmount { get; set; } = 0.25f;

  [ExportGroup("Lifetime")]
  [Export] public bool AutoFreeAfter { get; set; } = true;
  [Export] public float HoldSeconds { get; set; } = 0.45f;
  [Export] public float FadeOutSeconds { get; set; } = 0.33f;

  private struct Letter
  {
    public Vector3 BaseLocalPos;
    public float Width;
    public float PopIn;
  }
  private readonly List<Letter> _letters = new();
  private float _age = 0f;
  private float _createdTime = 0f;
  private float _popInStart = 0f;
  private bool _popOutActive = false;
  private float _popOutStart = 0f;
  private float _lastFullInAge = -999f;
  private bool _pulseActive = true;
  private float _pulseStartAge = 0f;

  public override void _Ready()
  {
    if (Font == null && !string.IsNullOrEmpty(FontPath))
    {
      Font = GD.Load<FontFile>(FontPath);
    }
    _createdTime = _age;
    BuildLetters();
    // Register with batcher after initial build
    base._Ready();
  }

  public override void _Process(double delta)
  {
    _age += (float)delta;
    if (AutoFreeAfter && _age > HoldSeconds + FadeOutSeconds) QueueFree();
  }

  public override Text3DBatcher.FontConfig GetFontConfig()
  {
    var font = Font ?? GD.Load<FontFile>(FontPath) ?? throw new InvalidOperationException("Font missing");
    return new Text3DBatcher.FontConfig
    {
      Font = font,
      FontSize = FontSize,
      PixelSize = PixelSizeWorld,
      Shaded = Shaded,
      OutlineSize = OutlineSize,
      OutlineColor = OutlineColor
    };
  }

  public override void EmitLetters(Action<char, Transform3D, Color> emit)
  {
    if (string.IsNullOrEmpty(Text) || _letters.Count != Text.Length)
    {
      BuildLetters();
    }

    // Facing basis (camera billboard + yaw)
    Basis faceBasis = GlobalBasisFacing();

    // Alpha for lifetime fade
    float alphaMul = 1f;
    if (AutoFreeAfter && _age > HoldSeconds)
    {
      float k = Mathf.Clamp((_age - HoldSeconds) / Mathf.Max(0.0001f, FadeOutSeconds), 0f, 1f);
      alphaMul = 1f - k;
    }

    bool allFullyIn = true;
    for (int i = 0; i < _letters.Count; i++)
    {
      var ld = _letters[i];

      float pop = ld.PopIn;
      if (!_popOutActive)
      {
        float baseT = Mathf.Max(0f, (_age - _createdTime - _popInStart));
        float raw = SequentialPopIn ? baseT * _letters.Count * Mathf.Max(0.01f, PopInRate) - i + 1f : baseT * Mathf.Max(0.01f, PopInRate);
        float clampMin = (MinCycleTime <= 0f) ? 1f : 0f;
        pop = Mathf.Clamp(raw, clampMin, 1f);
        pop *= pop;
        ld.PopIn = pop; _letters[i] = ld;
      }
      if (pop < 1f - 1e-4f) allFullyIn = false;

      float angle = 0f;
      if (EnableRotate && RotateMode != 0)
      {
        float dir = (RotateMode == 2) ? -1f : 1f;
        float idxTerm = 0.2f * (-_letters.Count / 2f - 0.5f + i) / Mathf.Max(1f, _letters.Count);
        float timeTerm = 0.02f * Mathf.Sin(2f * _age + i);
        angle = dir * (idxTerm + timeTerm);
      }

      float quiverAngle = 0f;
      if (EnableQuiver)
      {
        float q = Mathf.Sin(41.12f * _age * QuiverSpeed + i * 1223.2f) + Mathf.Cos(63.21f * _age * QuiverSpeed + i * 1112.2f);
        quiverAngle = 0.3f * QuiverAmount * q;
      }

      float pulseAdd = 0f;
      if (_pulseActive)
      {
        int idx = i + 1;
        float s1 = (_pulseStartAge - _age) * PulseSpeed + idx + 2.5f;
        float s2 = (_age - _pulseStartAge) * PulseSpeed - idx + 4.5f;
        float envelope = Mathf.Max(0f, Mathf.Min(s1, s2));
        pulseAdd = 0.2f * (envelope / 2.5f);
      }

      float realPopIn = (MinCycleTime <= 0f) ? 1f : pop;
      float scale = Mathf.Max(0.0001f, realPopIn * (1f + pulseAdd));

      float offY = 0f;
      if (EnableFloat)
      {
        offY = FloatBase + FloatAmplitude * Mathf.Sin(FloatFrequency * _age + i * 0.8f);
      }

      // World position for this letter center
      Vector3 worldOrigin = GlobalTransform.Origin;
      Vector3 local = ld.BaseLocalPos + new Vector3(0, offY, 0);
      Vector3 worldPos = worldOrigin + (faceBasis * local);

      // Basis for this letter: face + per-letter rotation around forward + scale
      Basis basis = faceBasis.Rotated(faceBasis.Z, angle + quiverAngle).Scaled(new Vector3(scale, scale, scale));

      // Emit
      var xform = new Transform3D(basis, worldPos);
      var col = DefaultColor; col.A *= alphaMul;
      emit(Text[i], xform, col);
    }

    if (!_popOutActive && AutoPopOut)
    {
      if (allFullyIn)
      {
        if (_lastFullInAge < 0f) _lastFullInAge = _age;
        if (_age - _lastFullInAge >= Mathf.Max(0f, PopDelay))
        {
          _popOutActive = true;
          _popOutStart = _age;
        }
      }
    }

    if (_popOutActive)
    {
      float mct = (MinCycleTime <= 0f) ? 1f : MinCycleTime;
      float pop = Mathf.Clamp(mct - (_age - _popOutStart) * PopOutRate / mct, 0f, 1f);
      pop *= pop;
      for (int i = 0; i < _letters.Count; i++)
      {
        var ld = _letters[i]; ld.PopIn = pop; _letters[i] = ld;
      }
      if (_letters.Count > 0 && _letters[_letters.Count - 1].PopIn <= 0f)
      {
        QueueFree();
      }
    }
  }

  private void BuildLetters()
  {
    if (Font == null && !string.IsNullOrEmpty(FontPath)) Font = GD.Load<FontFile>(FontPath);
    _letters.Clear();
    if (string.IsNullOrEmpty(Text)) return;

    float x = 0f;
    for (int i = 0; i < Text.Length; i++)
    {
      char ch = Text[i];
      // Approximate advance like AttentionText3D
      float wPx = FontSize * Mathf.Clamp(AdvanceFactor, 0.3f, 1.2f);
      float spacingW = SpacingFactor * Spacing * PixelSizeWorld * FontSize * Mathf.Clamp(AdvanceFactor, 0.3f, 1.2f);
      float baseW = wPx * PixelSizeWorld;
      float step = baseW + spacingW;

      var ld = new Letter
      {
        BaseLocalPos = new Vector3(0, 0, 0),
        Width = step,
        PopIn = 0f
      };
      float centerX = x + 0.5f * step;
      ld.BaseLocalPos = new Vector3(centerX, 0, 0);
      _letters.Add(ld);
      x += step;
    }

    // Center horizontally and apply text offset
    float halfTotal = x * 0.5f;
    for (int i = 0; i < _letters.Count; i++)
    {
      var ld = _letters[i];
      ld.BaseLocalPos -= new Vector3(halfTotal, 0, 0);
      ld.BaseLocalPos += new Vector3(TextOffset.X, TextOffset.Y, 0);
      _letters[i] = ld;
    }
    _createdTime = _age;
    _lastFullInAge = -999f;
    _pulseStartAge = _age;
  }

  private Basis GlobalBasisFacing()
  {
    if (!FaceCamera)
    {
      return GlobalTransform.Basis.Rotated(Vector3.Up, Mathf.DegToRad(TextYawDegrees));
    }
    var cam = GetViewport()?.GetCamera3D();
    if (cam == null) return GlobalTransform.Basis;
    Vector3 toCam = cam.GlobalTransform.Origin - GlobalTransform.Origin;
    Vector3 planar = new Vector3(toCam.X, 0, toCam.Z);
    if (planar.LengthSquared() <= 0.000001f)
      return GlobalTransform.Basis;
    var basis = Basis.LookingAt((-planar).Normalized(), Vector3.Up);
    basis = basis.Rotated(Vector3.Up, Mathf.DegToRad(TextYawDegrees));
    return basis;
  }
}
