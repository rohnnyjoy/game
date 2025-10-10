using Godot;

public partial class HyperTriggerModule : WeaponModule, IStatModifier
{
  private const float FireRateMultiplier = 0.2f; // 5x rate -> 20% of original delay

  public HyperTriggerModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(15);
    ModuleName = "Hyper Trigger Coil";
    ModuleDescription = "Attack speed boosted by fivefold.";
    Rarity = Rarity.Legendary;
  }

  public void Modify(ref WeaponStats stats)
  {
    stats.FireRate *= FireRateMultiplier;
  }
}
