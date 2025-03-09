using Godot;

public partial class FastModule : WeaponModule
{
  [Export]
  public float default_trail_width = 0.5f; // Fallback width if bullet.trail is null.

  public FastModule()
  {
    // Initialize the module description when the node is ready.
    CardTexture = GD.Load<Texture2D>("res://icons/fast.png");
    Rarity = Rarity.Common;
    ModuleDescription = "Bullets move faster";
  }

  public override float GetModifiedBulletSpeed(float bulletSpeed)
  {
    return bulletSpeed * 2.0f;
  }
}
