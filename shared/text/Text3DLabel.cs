using Godot;
using System;

#nullable enable

// General-purpose 3D text renderer that uses a single Label3D per string.
// Provides optional camera-facing billboarding, shadow, simple pulse/quiver/float effects,
// and auto-fade with free. Intended as a fast, batched alternative to per-glyph Label3Ds.
[GlobalClass]
public partial class Text3DLabel : Node3D
{
  // Content
  [ExportGroup("Content")]
  [Export] public string Text { get; set; } = "";
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export] public FontFile? Font { get; set; }
  [Export] public int FontSize { get; set; } = 40;
  [Export] public Color Color { get; set; } = Colors.White;

  // Visual
  [ExportGroup("Visual")]
  [Export] public int OutlineSize { get; set; } = 8;
  [Export] public Color OutlineColor { get; set; } = new Color(0, 0, 0, 1);
  [Export] public float PixelSize { get; set; } = 0.01f; // world units per font pixel
  [Export] public bool Shaded { get; set; } = false;

  // Shadow (second label offset behind text)
  [ExportGroup("Shadow")]
  [Export] public bool EnableShadow { get; set; } = true;
  [Export] public Color ShadowColor { get; set; } = new Color(0, 0, 0, 0.35f);
  [Export] public Vector2 ShadowParallax { get; set; } = new Vector2(1, 1);
  [Export] public float ShadowOffset { get; set; } = 0.0075f;

  // Behaviour
  [ExportGroup("Behaviour")]
  [Export] public bool FaceCamera { get; set; } = true;
  [Export(PropertyHint.Range, "0,360,1")] public float TextYawDegrees { get; set; } = 0f;

  // Effects (string-level)
  [ExportGroup("Effects")]
  [Export] public bool EnableFloat { get; set; } = false;
  [Export] public float FloatFrequency { get; set; } = 2.666f;
  [Export] public float FloatAmplitude { get; set; } = 0.08f; // world units
  [Export] public float FloatBase { get; set; } = 0f;

  [Export] public bool EnableQuiver { get; set; } = false;
  [Export] public float QuiverAmount { get; set; } = 0.15f; // radians
  [Export] public float QuiverSpeed { get; set; } = 0.5f;

  [Export] public bool EnablePulse { get; set; } = false;
  [Export] public float PulseAmount { get; set; } = 0.2f; // additive to scale (0.2 = +20%)
  [Export] public float PulseSpeed { get; set; } = 6.0f;

  // Lifetime
  [ExportGroup("Lifetime")]
  [Export] public bool AutoFade { get; set; } = false;
  [Export] public float HoldSeconds { get; set; } = 0.5f;
  [Export] public float FadeOutSeconds { get; set; } = 0.3f;

  private Label3D _label = null!;
  private Label3D? _shadow;
  private float _age = 0f;

  public override void _Ready()
  {
    // Load font if specified by path and not already provided
    if (Font == null && !string.IsNullOrEmpty(FontPath))
    {
      var f = GD.Load<FontFile>(FontPath);
      if (f != null) Font = f;
    }

    // Create label
    _label = new Label3D
    {
      Text = Text,
      Modulate = Color,
      FontSize = FontSize,
      OutlineSize = OutlineSize,
      OutlineModulate = OutlineColor,
      PixelSize = PixelSize,
      Shaded = Shaded,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
    };
    if (Font != null) _label.Font = Font;
    AddChild(_label);

    if (EnableShadow)
    {
      _shadow = new Label3D
      {
        Text = Text,
        Modulate = ShadowColor,
        FontSize = FontSize,
        OutlineSize = OutlineSize,
        OutlineModulate = OutlineColor,
        PixelSize = PixelSize,
        Shaded = Shaded,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
      };
      if (Font != null) _shadow.Font = Font;
      AddChild(_shadow);
    }

    // Apply orientation
    Rotation = new Vector3(Rotation.X, Mathf.DegToRad(TextYawDegrees), Rotation.Z);
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;
    _age += dt;

    if (FaceCamera)
    {
      var cam = GetViewport()?.GetCamera3D();
      if (cam != null)
      {
        Vector3 toCam = cam.GlobalTransform.Origin - GlobalTransform.Origin;
        Vector3 planar = new Vector3(toCam.X, 0, toCam.Z);
        if (planar.LengthSquared() > 0.000001f)
        {
          var basis = Basis.LookingAt((-planar).Normalized(), Vector3.Up);
          GlobalTransform = new Transform3D(basis, GlobalTransform.Origin);
          RotateObjectLocal(Vector3.Up, Mathf.DegToRad(TextYawDegrees));
        }
      }
    }

    // Effects
    float scaleMul = 1f;
    if (EnablePulse)
    {
      scaleMul += Mathf.Max(0f, PulseAmount) * (0.5f * (1f + Mathf.Sin(_age * PulseSpeed)));
    }
    if (EnableQuiver)
    {
      float q = QuiverAmount * Mathf.Sin(_age * QuiverSpeed * 7.1231f);
      Rotation = new Vector3(Rotation.X, Rotation.Y, q);
    }

    Vector3 basePos = Position;
    float yOff = 0f;
    if (EnableFloat)
    {
      yOff = FloatBase + FloatAmplitude * Mathf.Sin(FloatFrequency * _age);
    }

    // Apply transforms
    _label.Scale = new Vector3(scaleMul, scaleMul, scaleMul);
    _label.Position = new Vector3(0, yOff, 0);

    if (_shadow != null)
    {
      // Shadow parallax normalization: use 1px in world units based on PixelSize, plus ShadowOffset bias
      Vector2 sp = ShadowParallax;
      float plen = Mathf.Max(0.00001f, Mathf.Sqrt(sp.X * sp.X + sp.Y * sp.Y));
      Vector2 shadowNorm = sp / plen;
      Vector2 shadowWorld = shadowNorm * PixelSize;
      _shadow.Scale = _label.Scale;
      _shadow.Rotation = _label.Rotation;
      _shadow.Position = _label.Position + new Vector3(shadowWorld.X, -shadowWorld.Y, 0) + new Vector3(ShadowOffset, -ShadowOffset, 0);
      var sc = ShadowColor;
      // Auto fade also applies to shadow via alpha multiplication below
      _shadow.Modulate = sc;
    }

    // Lifetime fade
    if (AutoFade)
    {
      float alphaMul = 1f;
      if (_age > HoldSeconds)
      {
        float k = Mathf.Clamp((_age - HoldSeconds) / Mathf.Max(0.0001f, FadeOutSeconds), 0f, 1f);
        alphaMul = 1f - k;
      }

      var c = Color; c.A *= alphaMul; _label.Modulate = c;
      if (_shadow != null) { var sc = ShadowColor; sc.A *= alphaMul; _shadow.Modulate = sc; }

      if (_age > HoldSeconds + FadeOutSeconds)
      {
        QueueFree();
      }
    }
  }

  public void SetText(string t)
  {
    Text = t ?? "";
    if (_label != null) _label.Text = Text;
    if (_shadow != null) _shadow.Text = Text;
  }

  public void RebuildVisual()
  {
    if (_label == null) return;
    if (Font == null && !string.IsNullOrEmpty(FontPath)) Font = GD.Load<FontFile>(FontPath);

    _label.FontSize = FontSize;
    _label.PixelSize = PixelSize;
    _label.Shaded = Shaded;
    _label.OutlineSize = OutlineSize;
    _label.OutlineModulate = OutlineColor;
    _label.Modulate = Color;
    if (Font != null) _label.Font = Font;
    _label.Text = Text;

    if (_shadow != null)
    {
      _shadow.FontSize = FontSize;
      _shadow.PixelSize = PixelSize;
      _shadow.Shaded = Shaded;
      _shadow.OutlineSize = OutlineSize;
      _shadow.OutlineModulate = OutlineColor;
      _shadow.Modulate = ShadowColor;
      if (Font != null) _shadow.Font = Font;
      _shadow.Text = Text;
    }
  }
}
