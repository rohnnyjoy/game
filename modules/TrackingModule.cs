using Godot;

public partial class TrackingModule : WeaponModule, ITrackingProvider
{
  [Export]
  public float TrackingStrength { get; set; } = 0.1f;

  [Export]
  public float MaxRayDistance { get; set; } = 1000.0f;

  public TrackingModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(9); // tracking
    ModuleName = "RC Controller";
    Rarity = Rarity.Uncommon;
    ModuleDescription = "Bullets track the mouse cursor, adjusting their trajectory to hit it.";
  }

  public bool TryGetTrackingConfig(out TrackingProviderConfig config)
  {
    config = new TrackingProviderConfig(Mathf.Clamp(TrackingStrength, 0.0f, 1.0f), Math.Max(0.0f, MaxRayDistance));
    return config.Strength > 0.0f;
  }
}
