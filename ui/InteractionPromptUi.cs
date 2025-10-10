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
  private readonly List<LineSlot> _slots = new();
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
  [Export] public float FadeDuration = 0.12f;
  [Export] public float LineSpacingPx = 8f;
  [Export] public float PopInRate = 3f;
  [Export] public float PopDelay = 1.5f;
  [Export] public float PopOutRate = 4f;
  [Export] public bool DebugSlowPopIn = false;
  [Export(PropertyHint.Range, "0.05,3,0.05")] public float DebugPopInRate = 0.4f;
  [Export(PropertyHint.Range, "0,1,0.01")] public float PulseAmount = 0.3f;
  [Export(PropertyHint.Range, "0.5,4,0.1")] public float PulseWidth = 2.5f;
  [Export(PropertyHint.Range, "5,80,5")] public float PulseSpeed = 40f;

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
      var slot = _slots[i];
      string incoming = lines![desired - 1 - i] ?? string.Empty;
      string previous = slot.Text;

      CancelExit(slot);

      if (!string.Equals(previous, incoming, StringComparison.Ordinal))
      {
        ApplyLineConfig(slot, incoming);
        AnimateLine(slot);
      }

      slot.Text = incoming;
      slot.Node.Visible = true;
    }

    for (int i = desired; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      if (!string.IsNullOrEmpty(slot.Text))
        BeginLineExit(slot);
      else
        FinalizeImmediate(slot);
      slot.Text = string.Empty;
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
    if (_slots.Count == 0)
      return;

    bool restartPop = !Visible || Modulate.A <= 0.001f;

    _fadeTween?.Kill();
    Visible = true;
    Modulate = Modulate with { A = 0f };

    _fadeTween = GetTree().CreateTween();
    _fadeTween.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.In);
    _fadeTween.TweenProperty(this, "modulate:a", 1f, MathF.Max(0.0001f, FadeDuration));

    if (restartPop)
      JuiceVisibleLines();
  }

  public void HidePrompt()
  {
    TriggerPopOutOnVisibleLines();

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

    for (int i = 0; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      if (slot.IsExiting && IsInstanceValid(slot.Node))
        slot.Node.Position = slot.ExitPosition;
    }

    float currentY = 0f;
    bool anyVisible = false;

    for (int i = 0; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      if (!IsSlotActive(slot))
        continue;

      var node = slot.Node;
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
    while (_slots.Count < desired)
    {
      var node = new DynaText();
      var slot = new LineSlot(node);
      ApplyLineConfig(slot, string.Empty);
      node.Visible = false;
      AddChild(node);
      _slots.Insert(0, slot);
    }
  }

  private void ApplyLineConfig(LineSlot slot, string text)
  {
    CancelExit(slot);
    var cfg = BuildConfig(text);
    cfg.Parts.Clear();
    cfg.Parts.Add(new DynaText.TextPart { Literal = text });
    slot.Node.Init(cfg);
  }

  private void AnimateLine(LineSlot slot)
  {
    CancelExit(slot);
    slot.Node.CancelPopOut(true);
    slot.Node.TriggerPulse(PulseAmount, PulseWidth, PulseSpeed);
  }

  private void RefreshLineFonts()
  {
    for (int i = 0; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      ApplyLineConfig(slot, slot.Text);
      slot.Node.Visible = !string.IsNullOrEmpty(slot.Text);
    }
  }

  private bool HasVisibleLineAbove(int index)
  {
    for (int i = index + 1; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      if (!IsSlotActive(slot))
        continue;
      if (slot.Node.GetBoundsPx().LengthSquared() > 0.000001f)
        return true;
    }
    return false;
  }

  private void JuiceVisibleLines()
  {
    for (int i = 0; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      if (string.IsNullOrEmpty(slot.Text))
        continue;

      if (!slot.Node.Visible)
        continue;

      CancelExit(slot);
      slot.Node.CancelPopOut(true);
      slot.Node.TriggerPulse(PulseAmount, PulseWidth, PulseSpeed);
    }
  }

  private void TriggerPopOutOnVisibleLines()
  {
    float rate = MathF.Max(0.0001f, PopOutRate);
    for (int i = 0; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      if (string.IsNullOrEmpty(slot.Text))
        continue;

      if (!slot.Node.Visible)
        continue;

      CancelExit(slot);
      slot.Node.StartPopOut(rate, delayOverride: 0f);
    }
  }

  private static bool IsSlotActive(LineSlot slot) => slot.Node.Visible && !slot.IsExiting;

  private void CancelExit(LineSlot slot)
  {
    if (!slot.IsExiting)
      return;

    if (slot.HideTween != null && IsInstanceValid(slot.HideTween))
      slot.HideTween.Kill();

    slot.HideTween = null;
    slot.IsExiting = false;
    slot.Node.Visible = true;
    _layoutDirty = true;
  }

  private void BeginLineExit(LineSlot slot)
  {
    CancelExit(slot);

    slot.IsExiting = true;
    slot.ExitPosition = slot.Node.Position;
    slot.Node.Visible = true;
    slot.Node.StartPopOut(MathF.Max(0.0001f, PopOutRate), delayOverride: 0f);

    float rate = MathF.Max(0.0001f, PopOutRate);
    float hideDelay = MathF.Max(0.05f, 1f / rate);

    Tween tween = GetTree().CreateTween();
    slot.HideTween = tween;
    tween.TweenInterval(hideDelay);
    tween.Finished += () =>
    {
      if (slot.HideTween == tween)
        FinalizeLineExit(slot);
    };
    _layoutDirty = true;
  }

  private void FinalizeLineExit(LineSlot slot)
  {
    slot.HideTween = null;
    slot.IsExiting = false;

    if (IsInstanceValid(slot.Node))
      slot.Node.Visible = false;

    _layoutDirty = true;
  }

  private void FinalizeImmediate(LineSlot slot)
  {
    CancelExit(slot);
    if (IsInstanceValid(slot.Node))
      slot.Node.Visible = false;
  }

  private sealed class LineSlot
  {
    public LineSlot(DynaText node) => Node = node;

    public DynaText Node { get; }
    public string Text { get; set; } = string.Empty;
    public bool IsExiting { get; set; }
    public Vector2 ExitPosition { get; set; }
    public Tween? HideTween { get; set; }
  }

  private DynaText.Config BuildConfig(string text)
  {
    EnsureFontLoaded();

    bool hasText = !string.IsNullOrEmpty(text);

    float popInRate = DebugSlowPopIn ? DebugPopInRate : PopInRate;

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
      Lean = false,
      // PopInRate = MathF.Max(0.0001f, popInRate),
      // PopDelay = hasText ? MathF.Max(0f, PopDelay) : 0f,
      BumpRate = 2.666f,
      BumpAmount = 0f,
      Float = false,
      Bump = false,
      Rotate = false,
      PulseAffectsRotation = false,
      Silent = false,
      PixelSnap = false,
      SpacingExtraPx = 1.0f,
      TextHeightScale = 1f,
      PopInStartAt = null
    };
  }
}
