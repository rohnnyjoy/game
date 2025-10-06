using Godot;
using System;
using System.Threading.Tasks;
using static Godot.BaseMaterial3D;

public partial class ExplosiveModule : WeaponModule
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
    BulletModifiers.Add(new ExplosiveBulletModifier());
  }
}
