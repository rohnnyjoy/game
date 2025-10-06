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
    CardTexture = IconAtlas.MakeItemsIcon(6); // homing
    ModuleName = "Neuron Capsule";
    ModuleDescription = "Attacks home in on the nearest enemy within 10m.";
    Rarity = Rarity.Legendary;
    BulletModifiers.Add(new HomingBulletModifier());
  }
}
