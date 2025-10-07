using Godot;
using Godot.Collections;
using System;
[Tool]
public partial class ScatterModule : WeaponModule, IScatterProvider
{
  [Export]
  public int DuplicationCount { get; set; } = 5; // Total bullets (original + duplicates)

  [Export]
  public float BulletDamageFactor { get; set; } = 1 / 5;

  // Spread angle in radians. Bullets will scatter randomly within ±SpreadAngle/2 both horizontally and vertically.
  [Export]
  public float SpreadAngle { get; set; } = Mathf.DegToRad(15.0f);

  [Export]
  public override string ModuleName { get; set; } = "Shrapnel Chamber";

  // [Export]
  // public new Texture2D CardTexture { get; set; } = GD.Load<Texture2D>("res://icons/shotgun.png");

  [Export]
  public override string ModuleDescription { get; set; } = "Duplicates bullet in a scatter pattern with configurable horizontal and vertical spread.";

  [Export]
  public override Rarity Rarity { get; set; } = Rarity.Epic;

  public ScatterModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(3); // scatter
  }

  public bool TryGetScatterConfig(out ScatterConfig config)
  {
    int count = Math.Max(1, DuplicationCount);
    float damageFactor = Mathf.Max(0.0f, BulletDamageFactor);
    config = new ScatterConfig(count, SpreadAngle, damageFactor);
    return count > 1;
  }
}
