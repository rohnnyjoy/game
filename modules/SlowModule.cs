using Godot;

public partial class SlowModule : WeaponModule
{
  [Export]
  public float default_trail_width = 0.5f; // Fallback width if bullet.trail is null.

  public SlowModule()
  {
    // Initialize the module description when the node is ready.
    CardTexture = GD.Load<Texture2D>("res://icons/slow.png");
    Rarity = Rarity.Common;
    ModuleDescription = "Bullets move slower, but do more damage.";
  }


  public override float GetModifiedBulletSpeed(float bulletSpeed)
  {
    return bulletSpeed * 0.2f;
  }

  public override float GetModifiedDamage(float damage)
  {
    return damage * 1.5f;
  }
}
