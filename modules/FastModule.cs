using Godot;

public partial class FastModule : WeaponModule, IStatModifier
{
  [Export]
  public float default_trail_width = 0.5f; // Fallback width if bullet.trail is null.

  public FastModule()
  {
    // Initialize the module description when the node is ready.
    CardTexture = IconAtlas.MakeItemsIcon(7); // speed
    ModuleName = "Kinetic Booster";
    Rarity = Rarity.Common;
    ModuleDescription = "Bullets move faster";
  }

  public void Modify(ref WeaponStats stats)
  {
    stats.BulletSpeed *= 2.0f;
  }
}
