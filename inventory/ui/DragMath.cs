using System;

/// <summary>
/// Small helper for drag/drop spatial math that can be unit-tested
/// without requiring Godot scene tree state.
/// </summary>
public static class DragMath
{
  /// <summary>
  /// Computes the visual center of the dragged sprite given the raw mouse X,
  /// the grab offset within the sprite's draw region, and the draw width.
  /// </summary>
  public static float ComputeVisualCenterX(float mouseX, float grabWithinDrawX, float drawWidth)
  {
    return mouseX - grabWithinDrawX + 0.5f * drawWidth;
  }

  /// <summary>
  /// Given slot midpoints (in global X space) and a visual center X, returns
  /// the insertion index: the first slot whose midpoint is to the right of
  /// the visual center. If none, returns slot count (append).
  /// </summary>
  public static int ComputeInsertIndex(ReadOnlySpan<float> slotMidpoints, float visualCenterX)
  {
    for (int i = 0; i < slotMidpoints.Length; i++)
    {
      if (visualCenterX < slotMidpoints[i])
        return i;
    }
    return slotMidpoints.Length;
  }
}

