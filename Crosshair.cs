using Godot;

public partial class Crosshair : Control
{
  public override void _Ready()
  {
    // Schedule a redraw of the control.
    QueueRedraw();
  }

  public override void _Draw()
  {
    // Get the viewport's visible rectangle to determine the center.
    Rect2 viewportRect = GetViewport().GetVisibleRect();
    Vector2 center = viewportRect.Size / 2;

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
