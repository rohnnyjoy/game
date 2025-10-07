using Godot;

public partial class CursedSkullModule : WeaponModule
{
  [Export] public float TransferRadius { get; set; } = 8.0f;

  public CursedSkullModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(11); // skull sprite (shifted left by 1)
    ModuleName = "Cursed Skull";
    ModuleDescription = "Excess damage on kill chains to the closest enemy.";
    Rarity = Rarity.Legendary;

    BulletModifiers.Add(new CursedSkullBulletModifier
    {
      TransferRadius = TransferRadius
    });
  }
}
