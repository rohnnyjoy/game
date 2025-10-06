using Godot;

public partial class Crosshair : Control
{
  public override void _Ready()
  {
    // Schedule a redraw of the control.
    QueueRedraw();
  }

  // No per-frame logic needed

  public override void _Draw()
  {
    // Draw centered relative to this control's rect.
    Vector2 center = Size / 2;

    // Define crosshair properties.
    int lineLength = 10;
    int lineThickness = 2;
    Color lineColor = new Color(1, 1, 1); // white

    // Draw horizontal line.
    DrawLine(
        new Vector2(center.X - lineLength, center.Y),
        new Vector2(center.X + lineLength, center.Y),
        lineColor,
        lineThickness
    );

    // Draw vertical line.
    DrawLine(
        new Vector2(center.X, center.Y - lineLength),
        new Vector2(center.X, center.Y + lineLength),
        lineColor,
        lineThickness
    );
  }
}
