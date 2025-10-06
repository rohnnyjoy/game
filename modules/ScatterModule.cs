using Godot;
using Godot.Collections;
using System;
using System.Threading.Tasks;

[Tool]
public partial class ScatterModule : WeaponModule
{
  [Export]
  public int DuplicationCount { get; set; } = 5; // Total bullets (original + duplicates)

  [Export]
  public float BulletDamageFactor { get; set; } = 1 / 5;

  // Spread angle in radians. Bullets will scatter randomly within ±SpreadAngle/2 both horizontally and vertically.
  [Export]
  public float SpreadAngle { get; set; } = Mathf.DegToRad(15.0f);

  [Export]
  public override Array<BulletModifier> BulletModifiers { get; set; } = new Array<BulletModifier>(){
    new ScatterBulletModifier()
  };

  [Export]
  public override string ModuleName { get; set; } = "Scatter Module";

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
}
