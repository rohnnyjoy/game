using Godot;

public static class DebugTrace
{
  // Toggle to enable/disable verbose tracing without removing prints.
  public static bool Enabled = true;

  public static void Log(string message)
  {
    if (!Enabled) return;
    ulong ms = Time.GetTicksMsec();
    GD.Print($"[TRACE {ms}ms] {message}");
  }
}

