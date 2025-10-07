using System;
using System.Collections.Generic;
using AI;

internal static class AISchedulerTests
{
  public static void TestSchedulingSlices()
  {
    // Case 1: budget 0 => process all, stable cursor
    {
      int total = 10;
      int cursor = 0;
      var (start, count) = AIScheduler.ComputeSlice(total, 0, cursor);
      Program.TAssert(start == 0 && count == total, "Budget 0 should process all items");
      int next = AIScheduler.AdvanceCursor(total, 0, cursor);
      Program.TAssert(next == 0, "Cursor should not advance when processing all");
    }

    // Case 2: rotating coverage with budget 3
    {
      int total = 10;
      int budget = 3;
      int cursor = 0;
      var seen = new bool[total];
      int frames = (int)Math.Ceiling(total / (float)budget);
      for (int f = 0; f < frames; f++)
      {
        var (start, count) = AIScheduler.ComputeSlice(total, budget, cursor);
        for (int i = 0; i < count; i++)
        {
          int idx = (start + i) % total;
          seen[idx] = true;
        }
        cursor = AIScheduler.AdvanceCursor(total, budget, cursor);
      }
      // All indices should be touched within ceil(total/budget) frames
      for (int i = 0; i < total; i++)
        Program.TAssert(seen[i], $"Index {i} should be covered within budgeted frames");
    }
  }
}

