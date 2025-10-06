using Godot;

public partial class TrackingModule : WeaponModule
{
  public TrackingModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(9); // tracking
    Rarity = Rarity.Uncommon;
    ModuleDescription = "Bullets track the mouse cursor, adjusting their trajectory to hit it.";
    BulletModifiers.Add(new TrackingBulletModifier());
  }
}
