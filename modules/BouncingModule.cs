using Godot;
public partial class BouncingModule : WeaponModule, IBounceProvider
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
  }

  public bool TryGetBounceConfig(out BounceProviderConfig config)
  {
    config = new BounceProviderConfig(
      Mathf.Clamp(DamageReduction, 0.0f, 1.0f),
      Mathf.Clamp(Bounciness, 0.0f, 1.0f),
      Math.Max(0, MaxBounces)
    );
    return config.MaxBounces > 0;
  }
}
