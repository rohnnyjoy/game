using System.Threading.Tasks;
using Godot;

public partial class HomingModule : WeaponModule
{
  [Export]
  public float HomingRadius { get; set; } = 10.0f;

  [Export]
  public float TrackingStrength { get; set; } = 0.04f; // How quickly the bullet turns; 0.0 to 1.0

  public HomingModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/homing.png");
    ModuleDescription = "Attacks home in on the nearest enemy within 10m.";
    Rarity = Rarity.Epic;
    BulletModifiers.Add(new HomingBulletModifier());
  }
}
