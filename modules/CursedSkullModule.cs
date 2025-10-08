using Godot;
using System.Collections.Generic;

public partial class CursedSkullModule : WeaponModule, IDamagePostStepProvider
{
  [Export] public float TransferRadius { get; set; } = 0.0f; // 0 means no range cap; find nearest anywhere

  public CursedSkullModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(11); // skull sprite (shifted left by 1)
    ModuleName = "Cursed Skull";
    ModuleDescription = "Excess damage on kill chains to the closest enemy.";
    Rarity = Rarity.Legendary;
  }

  public IEnumerable<DamagePostStepConfig> GetDamagePostSteps()
  {
    yield return new DamagePostStepConfig(
      DamagePostStepKind.OverkillTransfer,
      priority: 0,
      paramA: TransferRadius,
      paramB: 0f,
      paramC: 0f
    );
  }
}
