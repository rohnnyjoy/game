using Godot;
using System;
using System.Threading.Tasks;

public partial class BouncingModule : WeaponModule
{
  [Export]
  public float DamageReduction { get; set; } = 0.2f;
  [Export]
  public float Bounciness { get; set; } = 0.8f;
  [Export]
  public int MaxBounces { get; set; } = 3;

  public BouncingModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(0); // bounce
    ModuleName = "Richochet Coil";
    ModuleDescription = "Bullets bounce off surfaces, reducing damage with each bounce.";
    Rarity = Rarity.Rare;
    BulletModifiers.Add(new BouncingBulletModifier());
  }
}
