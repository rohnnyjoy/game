using Godot;
using System.Threading.Tasks;
using Godot.Collections;

public partial class StickyModule : WeaponModule
{
  [Export]
  public float StickDuration { get; set; } = 1.0f;

  [Export]
  public float CollisionDamage { get; set; } = 1.0f;

  // Cache a shared RandomNumberGenerator.
  private static RandomNumberGenerator _rng = new RandomNumberGenerator();

  public StickyModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(4); // sticky
    ModuleDescription = "Bullets stick to surfaces and enemies, detonating after a short delay.";
    Rarity = Rarity.Common;
    BulletModifiers.Add(new StickyBulletModifier());
  }
}
