using Godot;

public partial class WeightedGloveModule : WeaponModule
{
  [Export] public float DamagePerSpeedFactor { get; set; } = 1.0f; // 2x damage at 2x speed by default
  [Export] public float KnockbackPerSpeedFactor { get; set; } = 1.0f; // 2x knockback at 2x speed by default
  [Export] public bool UseInitialSpeedAsBaseline { get; set; } = true;

  public WeightedGloveModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(12); // glove sprite (shifted left by 1)
    ModuleName = "Weighted Glove";
    ModuleDescription = "Damage and knockback scale with projectile speed.";
    Rarity = Rarity.Uncommon;
    BulletModifiers.Add(new SpeedScaledImpactModifier
    {
      DamagePerSpeedFactor = DamagePerSpeedFactor,
      KnockbackPerSpeedFactor = KnockbackPerSpeedFactor,
      UseInitialSpeedAsBaseline = UseInitialSpeedAsBaseline,
    });
  }
}
