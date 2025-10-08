using Godot;
using System;
using System.Collections.Generic;

/// Generic host control for DynaText with pluggable DynaTextStyle.
/// Use this instead of ad-hoc DynaTextControl when you want consistent styling across UI.
public partial class DynaTextHost : Control
{
  [Export] public DynaTextStyle Style; // If null, a default style is used
  [Export] public bool CenterInRect = true;
  // When not centered, alignment within this control's rect
  [Export(PropertyHint.Range, "0,1,0.01")] public float AlignX = 0.5f;
  [Export(PropertyHint.Range, "0,1,0.01")] public float AlignY = 0.5f;

  public DynaText Inner { get; private set; }
  public DynaText.Config Config { get; private set; }

  private string _text = string.Empty;
  private List<Color> _overrideColours = null;
  private List<DynaText.TextPart> _segments = null; // optional: if set, used instead of single text provider

  public override void _Ready()
  {
    if (Style == null)
      Style = new DynaTextStyle();

    Inner = new DynaText();
    Config = new DynaText.Config();
    Style.ApplyTo(Config);

    BuildParts();
    AddChild(Inner);
  }

  public override void _Process(double delta)
  {
    if (Inner == null) return;
    var b = Inner.GetBoundsPx();
    if (CenterInRect)
    {
      Inner.Position = new Vector2((Size.X - b.X) * 0.5f, (Size.Y - b.Y) * 0.5f);
    }
    else
    {
      float ax = Mathf.Clamp(AlignX, 0f, 1f);
      float ay = Mathf.Clamp(AlignY, 0f, 1f);
      Inner.Position = new Vector2((Size.X - b.X) * ax, (Size.Y - b.Y) * ay);
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
    int px = Style != null ? Style.FontPx : 24;
    return new Vector2(Mathf.Max(4f, px * 0.6f), Mathf.Max(4f, px * 1.0f));
  }

  public void SetText(string text)
  {
    _segments = null;
    _text = text ?? string.Empty;
    Rebuild();
  }

  public void SetColours(List<Color> colours)
  {
    _overrideColours = (colours != null && colours.Count > 0) ? new List<Color>(colours) : null;
    ReapplyColours();
  }

  public void SetTextSegments(List<DynaText.TextPart> parts)
  {
    _segments = (parts != null && parts.Count > 0) ? new List<DynaText.TextPart>(parts) : null;
    Rebuild();
  }

  public void SetScale(float scale)
  {
    Inner?.SetScale(scale);
  }

  public void SetTextHeightScale(float scale)
  {
    if (Config == null) return;
    Config.TextHeightScale = MathF.Max(0.5f, MathF.Min(2f, scale));
    Inner?.Init(Config);
  }

  public void Pulse(float amount = 0.22f)
  {
    Inner?.TriggerPulse(amount);
  }

  public void Pulse(float amount, float width, float speed)
  {
    Inner?.TriggerPulse(amount, width, speed);
  }

  public void Quiver(float amount, float speed, float duration)
  {
    Inner?.SetQuiver(amount, speed, duration);
  }

  public Vector2 GetTextBoundsPx()
  {
    return Inner != null ? Inner.GetBoundsPx() : Vector2.Zero;
  }

  private void BuildParts()
  {
    if (Config == null || Inner == null) return;
    Config.Parts.Clear();
    if (_segments != null)
    {
      // Use provided structured parts directly (supports dynamic providers, prefix/suffix, inner/outer colours)
      foreach (var p in _segments)
        Config.Parts.Add(p);
    }
    else
    {
      Config.Parts.Add(new DynaText.TextPart { Provider = () => _text });
    }
    ReapplyColours();
    Inner.Init(Config);
  }

  private void Rebuild()
  {
    if (Config == null || Inner == null) return;
    BuildParts();
  }

  private void ReapplyColours()
  {
    if (Config == null) return;
    if (_overrideColours != null && _overrideColours.Count > 0)
      Config.Colours = new List<Color>(_overrideColours);
    else if (Config.Colours == null || Config.Colours.Count == 0)
      Config.Colours = new List<Color> { Colors.White };
  }
}

