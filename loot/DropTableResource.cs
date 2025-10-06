using Godot;
using Godot.Collections;
using System;

[GlobalClass]
public partial class DropTableResource : Resource
{
  public enum DropRollMode
  {
    Independent, // roll each entry independently
    ChooseOne    // choose exactly one entry by weight among those that pass DropChance
  }

  [Export] public DropRollMode RollMode { get; set; } = DropRollMode.Independent;
  [Export] public Array<DropEntryResource> Entries { get; set; } = new();

  public void SpawnDrops(Vector3 origin, Node parent)
  {
    if (parent == null || Entries == null || Entries.Count == 0)
      return;

    var rng = new RandomNumberGenerator();
    rng.Randomize();

    switch (RollMode)
    {
      case DropRollMode.Independent:
        foreach (var entry in Entries)
        {
          if (entry == null) continue;
          if (rng.Randf() <= Mathf.Clamp(entry.DropChance, 0f, 1f))
          {
            int qty = Math.Max(0, entry.SampleQuantity(rng));
            if (qty > 0)
              LootSpawner.Spawn(entry.Kind, qty, origin, parent);
          }
        }
        break;
      case DropRollMode.ChooseOne:
        // Build candidate list with weights
        float total = 0f;
        foreach (var e in Entries)
        {
          if (e == null) continue;
          if (rng.Randf() <= Mathf.Clamp(e.DropChance, 0f, 1f))
            total += Math.Max(0f, e.Weight);
        }
        if (total <= 0f)
          return;
        float pick = rng.RandfRange(0, total);
        float accum = 0f;
        foreach (var e in Entries)
        {
          if (e == null) continue;
          if (rng.Randf() > Mathf.Clamp(e.DropChance, 0f, 1f))
            continue;
          float w = Math.Max(0f, e.Weight);
          if (w <= 0f) continue;
          accum += w;
          if (pick <= accum)
          {
            int qty = Math.Max(0, e.SampleQuantity(rng));
            if (qty > 0)
              LootSpawner.Spawn(e.Kind, qty, origin, parent);
            break;
          }
        }
        break;
    }
  }
}

