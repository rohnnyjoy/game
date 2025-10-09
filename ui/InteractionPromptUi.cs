#nullable enable

using Godot;
using System;
using System.Collections.Generic;

public partial class InteractionPromptUi : Control
{
  private readonly List<DynaText> _lineNodes = new();
  private readonly List<Vector2> _lineSizes = new();
  private readonly List<string> _cachedLines = new();

  private Tween? _fadeTween;
  private Control? _anchor;
  private bool _layoutDirty;
  private int _appliedFontPx;

  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export(PropertyHint.Range, "24,96,2")] public int FontPx = 64;
  [Export] public Color TextColor = new Color(1f, 1f, 1f);
  [Export] public bool Shadow = true;
  [Export] public bool UseShadowParallax = true;
  [Export] public float ShadowAlpha = 0.35f;
  [Export] public Vector2 ShadowOffset = new Vector2(0, 0);
  [Export] public float ParallaxPixelScale = 0f;
  [Export] public float TextRotation = 0.0f;
  [Export] public float PulseAmount = 0.35f;
  [Export] public float QuiverAmount = 0.12f;
  [Export] public float QuiverSpeed = 0.6f;
  [Export] public float FadeDuration = 0.12f;
  [Export] public float LineSpacingPx = 8f;

  public override void _Ready()
  {
    Visible = false;
    Modulate = new Color(Modulate.R, Modulate.G, Modulate.B, 0f);
    _appliedFontPx = FontPx;
  }

  public void AttachTo(Control container)
  {
    var callable = new Callable(this, nameof(OnAnchorResized));
    if (_anchor != null && IsInstanceValid(_anchor) && _anchor.IsConnected("resized", callable))
      _anchor.Disconnect("resized", callable);

    _anchor = container;
    if (_anchor != null)
      _anchor.Connect("resized", callable);

    _layoutDirty = true;
  }

  public void SetLines(IReadOnlyList<string>? lines)
  {
    _cachedLines.Clear();
    if (lines != null)
      _cachedLines.AddRange(lines);

    RebuildLines();
    _layoutDirty = true;
  }

  public void SetText(string text)
  {
    if (string.IsNullOrEmpty(text))
      SetLines(Array.Empty<string>());
    else
      SetLines(new[] { text });
  }

  public void ShowPrompt()
  {
    if (_lineNodes.Count == 0)
      return;

    if (_fadeTween != null && _fadeTween.IsRunning())
      _fadeTween.Kill();

    Visible = true;
    var c = Modulate; c.A = 0f; Modulate = c;
    _fadeTween = GetTree().CreateTween();
    _fadeTween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);
    _fadeTween.TweenProperty(this, "modulate:a", 1f, MathF.Max(0.0001f, FadeDuration));
  }

  public void HidePrompt()
  {
    if (_fadeTween != null && _fadeTween.IsRunning())
      _fadeTween.Kill();

    _fadeTween = GetTree().CreateTween();
    _fadeTween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);
    _fadeTween.TweenProperty(this, "modulate:a", 0f, MathF.Max(0.0001f, FadeDuration));
    _fadeTween.Finished += () => Visible = false;
  }

  public override void _Process(double delta)
  {
    base._Process(delta);

    if (_appliedFontPx != FontPx)
    {
      _appliedFontPx = FontPx;
      RebuildLines();
      _layoutDirty = true;
    }

    if (_layoutDirty && UpdatePlacement())
      _layoutDirty = false;
  }

  private void OnAnchorResized()
  {
    _layoutDirty = true;
  }

  private void RebuildLines()
  {
    foreach (var node in _lineNodes)
    {
      if (node != null)
      {
        if (node.IsInsideTree())
          RemoveChild(node);
        node.QueueFree();
      }
    }
    _lineNodes.Clear();

    foreach (string line in _cachedLines)
    {
      var node = CreateLineNode(line ?? string.Empty);
      AddChild(node);
      _lineNodes.Add(node);
    }
  }

  private DynaText CreateLineNode(string text)
  {
    var cfg = BuildConfig();
    cfg.Parts.Clear();
    cfg.Parts.Add(new DynaText.TextPart { Literal = text });

    var dyn = new DynaText();
    dyn.Init(cfg);
    dyn.Visible = true;
    dyn.TriggerPulse(PulseAmount);
    dyn.SetQuiver(QuiverAmount, QuiverSpeed, 0.3f);
    dyn.TriggerTilt(0.25f);
    return dyn;
  }

  private bool UpdatePlacement()
  {
    if (_anchor == null || !IsInstanceValid(_anchor))
      return false;

    Rect2 rect = _anchor.GetGlobalRect();
    GlobalPosition = rect.Position + rect.Size * 0.5f;

    _lineSizes.Clear();
    float totalHeight = 0f;

    for (int i = 0; i < _lineNodes.Count; i++)
    {
      Vector2 bounds = _lineNodes[i].GetBoundsPx();
      _lineSizes.Add(bounds);
      if (bounds.LengthSquared() < 0.0001f)
        continue;
      totalHeight += bounds.Y;
      if (i < _lineNodes.Count - 1)
        totalHeight += LineSpacingPx;
    }

    float currentY = -totalHeight;
    for (int i = 0; i < _lineNodes.Count; i++)
    {
      DynaText node = _lineNodes[i];
      Vector2 bounds = _lineSizes[i];
      if (bounds.LengthSquared() < 0.0001f)
      {
        node.Visible = false;
        continue;
      }

      node.Visible = true;
      node.Position = new Vector2(-bounds.X * 0.5f, currentY);
      currentY += bounds.Y;
      if (i < _lineNodes.Count - 1)
        currentY += LineSpacingPx;
    }

    return true;
  }

  private DynaText.Config BuildConfig()
  {
    Font font = GD.Load<FontFile>(FontPath);

    return new DynaText.Config
    {
      Font = font,
      FontSizePx = FontPx,
      Colours = new() { TextColor },
      Shadow = Shadow,
      ShadowColor = new Color(0, 0, 0, Mathf.Clamp(ShadowAlpha, 0f, 1f)),
      ShadowOffsetPx = ShadowOffset,
      ShadowUseParallax = UseShadowParallax,
      ParallaxPixelScale = ParallaxPixelScale,
      TextRotationRad = TextRotation,
      PopInRate = 3f,
      BumpRate = 2.666f,
      BumpAmount = 0f,
      Float = true,
      Bump = true,
      Rotate = true,
      Silent = true,
      PixelSnap = true,
      SpacingExtraPx = 1.0f,
      TextHeightScale = 1f
    };
  }
}
