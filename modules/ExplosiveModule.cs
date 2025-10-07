using Godot;
public partial class ExplosiveModule : WeaponModule, IExplosiveProvider
{
  [Export]
  public float ExplosionRadius { get; set; } = 2.5f;

  [Export]
  public float ExplosionDamageMultiplier { get; set; } = 0.25f;

  public ExplosiveModule()
  {
    // Set the module’s card texture and description.
    CardTexture = IconAtlas.MakeItemsIcon(5); // explosive
    ModuleName = "Boom Juice";
    ModuleDescription = "Attacks explode on impact, dealing 25% damage in a 2-meter radius.";
    Rarity = Rarity.Uncommon;
  }

  public bool TryGetExplosiveConfig(out ExplosiveProviderConfig config)
  {
    config = new ExplosiveProviderConfig(
      Mathf.Max(0.0f, ExplosionRadius),
      Mathf.Max(0.0f, ExplosionDamageMultiplier)
    );
    return config.Radius > 0.0f && config.DamageMultiplier > 0.0f;
  }
}
