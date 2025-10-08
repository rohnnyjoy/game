using Godot;
using System.Collections.Generic;

// Lightweight badge control that hosts a DynaTextHost aligned to a chosen corner.
public partial class DynaBadge : Control
{
  public enum Corner
  {
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
  }

  [Export] public DynaTextStyle Style; // optional shared style
  [Export] public Corner AnchorCorner = Corner.BottomRight;
  [Export] public int Padding = 2; // outer control padding for comfortable inset

  private DynaTextHost _host;

  public override void _Ready()
  {
    _host = new DynaTextHost
    {
      Name = "InnerLabel",
      Style = Style,
      CenterInRect = false,
      AlignX = CornerIsRight() ? 1f : 0f,
      AlignY = CornerIsBottom() ? 1f : 0f,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _host.SetAnchorsPreset(LayoutPreset.FullRect);

    AddChild(_host);

    // Apply padding as theme constants so layout remains responsive
    AddThemeConstantOverride("margin_left", Padding);
    AddThemeConstantOverride("margin_top", Padding);
    AddThemeConstantOverride("margin_right", Padding);
    AddThemeConstantOverride("margin_bottom", Padding);
  }

  public void SetText(string text)
  {
    _host?.SetText(text);
  }

  public void SetColours(List<Color> colours)
  {
    _host?.SetColours(colours);
  }

  public void SetTextSegments(List<DynaText.TextPart> parts)
  {
    _host?.SetTextSegments(parts);
  }

  public void Pulse(float amount = 0.22f)
  {
    _host?.Pulse(amount);
  }

  private bool CornerIsRight() => AnchorCorner == Corner.TopRight || AnchorCorner == Corner.BottomRight;
  private bool CornerIsBottom() => AnchorCorner == Corner.BottomLeft || AnchorCorner == Corner.BottomRight;
}

