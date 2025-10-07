using Godot;

public partial class SlowModule : WeaponModule, IStatModifier
{
  [Export]
  public float default_trail_width = 0.5f; // Fallback width if bullet.trail is null.

  public SlowModule()
  {
    // Initialize the module description when the node is ready.
    CardTexture = IconAtlas.MakeItemsIcon(8); // slow
    ModuleName = "Chrono Ampule";
    Rarity = Rarity.Common;
    ModuleDescription = "Bullets move slower, but do more damage.";
  }


  public void Modify(ref WeaponStats stats)
  {
    stats.BulletSpeed *= 0.2f;
    stats.Damage *= 1.5f;
  }
}
