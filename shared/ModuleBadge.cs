using Godot;

public readonly struct ModuleBadge
{
  public readonly string Text;
  public readonly Color TextColor;
  public readonly Color BackgroundColor;

  public ModuleBadge(string text, Color? textColor = null, Color? backgroundColor = null)
  {
    Text = text ?? string.Empty;
    TextColor = textColor ?? Colors.White;
    BackgroundColor = backgroundColor ?? new Color(0, 0, 0, 0);
  }
}
