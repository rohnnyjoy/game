using Godot;

public partial class RapidFireModule : WeaponModule, IStatModifier
{
  private const float FireRateMultiplier = 0.35f;

  public RapidFireModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(13); // rapid-fire sprite slot
    ModuleName = "Overclocked Servo";
    ModuleDescription = "Massively boosts fire rate.";
    Rarity = Rarity.Rare;
  }

  public void Modify(ref WeaponStats stats)
  {
    stats.FireRate *= FireRateMultiplier;
  }
}
