using Godot;
public partial class StickyModule : WeaponModule, IStickyProvider
{
  [Export]
  public float StickDuration { get; set; } = 1.0f;

  [Export]
  public float CollisionDamage { get; set; } = 1.0f;

  public StickyModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(4); // sticky
    ModuleName = "Gelmantle Core";
    ModuleDescription = "Bullets stick to surfaces and enemies, detonating after a short delay.";
    Rarity = Rarity.Common;
  }

  public bool TryGetStickyConfig(out StickyProviderConfig config)
  {
    config = new StickyProviderConfig(Mathf.Max(0.0f, StickDuration), Math.Max(0.0f, CollisionDamage));
    return config.Duration > 0.0f;
  }
}
