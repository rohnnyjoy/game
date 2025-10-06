using Godot;

[GlobalClass]
public partial class DropEntryResource : Resource
{
  [Export] public LootKind Kind { get; set; } = LootKind.HealthPotion;
  // Probability that this entry is considered at all (0..1).
  [Export(PropertyHint.Range, "0,1,0.001")] public float DropChance { get; set; } = 1.0f;
  // Relative weight when selecting a single entry (ChooseOne roll mode).
  [Export] public float Weight { get; set; } = 1.0f;
  // Quantity range when this entry is selected/triggered.
  [Export] public int MinQuantity { get; set; } = 1;
  [Export] public int MaxQuantity { get; set; } = 1;

  public int SampleQuantity(RandomNumberGenerator rng)
  {
    int minQ = Mathf.Max(0, MinQuantity);
    int maxQ = Mathf.Max(minQ, MaxQuantity);
    return rng.RandiRange(minQ, maxQ);
  }
}

