#nullable enable

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Bottom-aligned interaction prompt stack that renders each line with DynaText.
/// New/changed lines animate, but existing ones stay anchored so the stack does not jump.
/// </summary>
public partial class InteractionPromptUi : Control
{
  private readonly List<DynaText> _lineNodes = new();
  private readonly List<string> _currentLines = new();
  private Tween? _fadeTween;
  private Control? _anchor;
  private bool _layoutDirty;
  private int _appliedFontPx = -1;
  private FontFile? _font;

  [Export] public string FontPath = "res://assets/fonts/Born2bSportyV2.ttf";
  [Export(PropertyHint.Range, "24,96,2")] public int FontPx = 64;
  [Export] public Color TextColor = new(1f, 1f, 1f);
  [Export] public bool Shadow = true;
  [Export] public float ShadowAlpha = 0.35f;
  [Export] public Vector2 ShadowOffset = Vector2.Zero;
  [Export] public bool UseShadowParallax = true;
  [Export] public float ParallaxPixelScale = 0f;
  [Export] public float TextRotation = 0f;
  [Export] public float PulseAmount = 0.35f;
  [Export] public float PulseDuration = 0.12f;
  [Export] public float QuiverAmount = 0.12f;
  [Export] public float QuiverSpeed = 0.6f;
  [Export] public float FadeDuration = 0.12f;
  [Export] public float LineSpacingPx = 8f;

  public override void _Ready()
  {
    Visible = false;
    Modulate = Modulate with { A = 0f };
    EnsureFontLoaded(true);
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
    EnsureFontLoaded();

    int desired = lines?.Count ?? 0;
    EnsureLineCapacity(desired);

    for (int i = 0; i < desired; i++)
    {
      string incoming = lines![desired - 1 - i] ?? string.Empty;
      string previous = _currentLines[i];
      DynaText node = _lineNodes[i];

      if (!string.Equals(previous, incoming, StringComparison.Ordinal))
      {
        ApplyLineConfig(node, incoming);
        AnimateLine(node);
        _currentLines[i] = incoming;
      }

      node.Visible = true;
    }

    for (int i = desired; i < _lineNodes.Count; i++)
    {
      _lineNodes[i].Visible = false;
      _currentLines[i] = string.Empty;
    }

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

    _fadeTween?.Kill();
    Visible = true;
    Modulate = Modulate with { A = 0f };

    _fadeTween = GetTree().CreateTween();
    _fadeTween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);
    _fadeTween.TweenProperty(this, "modulate:a", 1f, MathF.Max(0.0001f, FadeDuration));
  }

  public void HidePrompt()
  {
    _fadeTween?.Kill();
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
      EnsureFontLoaded(true);
      RefreshLineFonts();
      _layoutDirty = true;
    }

    if (_layoutDirty && UpdateLayout())
      _layoutDirty = false;
  }

  private void OnAnchorResized()
  {
    _layoutDirty = true;
  }

  private bool UpdateLayout()
  {
    if (_anchor == null || !IsInstanceValid(_anchor))
      return false;

    Rect2 rect = _anchor.GetGlobalRect();
    GlobalPosition = rect.Position + rect.Size * 0.5f;

    float currentY = 0f;
    bool anyVisible = false;

    for (int i = 0; i < _lineNodes.Count; i++)
    {
      DynaText node = _lineNodes[i];
      if (!node.Visible)
        continue;

      Vector2 size = node.GetBoundsPx();
      if (size.LengthSquared() < 0.000001f)
        continue;

      currentY -= size.Y;
      node.Position = new Vector2(-size.X * 0.5f, currentY);

      if (HasVisibleLineAbove(i))
        currentY -= LineSpacingPx;

      anyVisible = true;
    }

    return anyVisible;
  }

  private void EnsureFontLoaded(bool force = false)
  {
    if (_font == null || force)
    {
      _font = GD.Load<FontFile>(FontPath);
      _appliedFontPx = FontPx;
    }
  }

  private void EnsureLineCapacity(int desired)
  {
    while (_lineNodes.Count < desired)
    {
      var node = new DynaText();
      ApplyLineConfig(node, string.Empty);
      node.Visible = false;
      AddChild(node);
      _lineNodes.Insert(0, node);
      _currentLines.Insert(0, string.Empty);
    }

  }

  private void ApplyLineConfig(DynaText node, string text)
  {
    var cfg = BuildConfig();
    cfg.Parts.Clear();
    cfg.Parts.Add(new DynaText.TextPart { Literal = text });
    node.Init(cfg);
  }

  private void AnimateLine(DynaText node)
  {
    node.TriggerPulse(PulseAmount, 2.5f, 40f);
    node.SetQuiver(QuiverAmount, QuiverSpeed, 0.3f);
    node.TriggerTilt(0.25f);
  }

  private void RefreshLineFonts()
  {
    for (int i = 0; i < _lineNodes.Count; i++)
    {
      string text = _currentLines[i];
      DynaText node = _lineNodes[i];
      ApplyLineConfig(node, text);
      node.Visible = !string.IsNullOrEmpty(text);
    }
  }

  private bool HasVisibleLineAbove(int index)
  {
    for (int i = index + 1; i < _lineNodes.Count; i++)
    {
      var node = _lineNodes[i];
      if (!node.Visible)
        continue;
      if (node.GetBoundsPx().LengthSquared() > 0.000001f)
        return true;
    }
    return false;
  }

  private DynaText.Config BuildConfig()
  {
    EnsureFontLoaded();

    return new DynaText.Config
    {
      Font = _font!,
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
