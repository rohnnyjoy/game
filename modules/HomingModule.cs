using System.Threading.Tasks;
using Godot;

public partial class HomingModule : WeaponModule, IHomingProvider
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
  }

  public bool TryGetHomingConfig(out HomingProviderConfig config)
  {
    config = new HomingProviderConfig(Mathf.Max(0.0f, HomingRadius), Mathf.Clamp(TrackingStrength, 0.0f, 1.0f));
    return config.Radius > 0.0f && config.Strength > 0.0f;
  }
}
