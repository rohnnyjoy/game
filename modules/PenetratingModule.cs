using Godot;
using System;
public partial class PiercingModule : WeaponModule, IPierceProvider
{
  [Export]
  public float DamageReduction { get; set; } = 0.2f;
  [Export]
  public float VelocityFactor { get; set; } = 0.9f;
  [Export]
  public int MaxPenetrations { get; set; } = 5;
  [Export]
  public float CollisionCooldown { get; set; } = 0.2f;

  public PiercingModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(2); // penetrating
    ModuleName = "Drillgun";
    Rarity = Rarity.Uncommon;
    ModuleDescription = "Bullets can penetrate multiple enemies, reducing damage with each hit.";
  }

  public bool TryGetPierceConfig(out PierceProviderConfig config)
  {
    config = new PierceProviderConfig(
      Mathf.Clamp(DamageReduction, 0.0f, 1.0f),
      Mathf.Clamp(VelocityFactor, 0.0f, 1.0f),
      Math.Max(0, MaxPenetrations),
      Math.Max(0.0f, CollisionCooldown)
    );
    return config.MaxPenetrations > 0;
  }
}
