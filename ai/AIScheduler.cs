using System;

namespace AI
{
  public static class AIScheduler
  {
    // Computes a contiguous slice of work for this frame.
    // total: total items
    // maxPerFrame: 0 or less means process all per frame
    // cursor: current rotating start index
    public static (int start, int count) ComputeSlice(int total, int maxPerFrame, int cursor)
    {
      if (total <= 0)
        return (0, 0);

      int count = maxPerFrame <= 0 ? total : Math.Min(maxPerFrame, total);
      int start = (total == 0) ? 0 : Math.Abs(cursor % total);
      return (start, count);
    }

    // Advances the cursor by the count returned from ComputeSlice.
    public static int AdvanceCursor(int total, int maxPerFrame, int cursor)
    {
      if (total <= 0)
        return 0;
      int count = maxPerFrame <= 0 ? total : Math.Min(maxPerFrame, total);
      return (cursor + count) % total;
    }
  }
}

