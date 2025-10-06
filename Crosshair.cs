using Godot;

public partial class Crosshair : Control
{
  public override void _Ready()
  {
    // Schedule a redraw of the control.
    SetProcess(true);
    QueueRedraw();
  }

  public override void _Process(double delta)
  {
    QueueRedraw();
  }

  public override void _Draw()
  {
    // Draw centered; when not using full-frame overlay, cancel UI offset so the crosshair remains fixed.
    bool fullFrame = GameUi.Instance != null && GameUi.Instance.UseFullFrameShake;
    Vector2 shake = (!fullFrame && GameUi.Instance != null) ? GameUi.Instance.GetScreenShakeOffset() : Vector2.Zero;
    Vector2 center = (Size / 2) - shake;

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
