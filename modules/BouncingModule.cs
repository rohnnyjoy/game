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
    CardTexture = GD.Load<Texture2D>("res://icons/bouncing.png");
    ModuleDescription = "Bullets bounce off surfaces, reducing damage with each bounce.";
    Rarity = Rarity.Rare;
    BulletModifiers.Add(new BouncingBulletModifier());
  }
}
