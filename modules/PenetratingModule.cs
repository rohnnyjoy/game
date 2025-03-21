using Godot;
using System;
using Godot.Collections;
using System.Threading.Tasks;

public partial class PiercingModule : WeaponModule
{
  public PiercingModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/penetrating.png");
    Rarity = Rarity.Uncommon;
    ModuleDescription = "Bullets can penetrate multiple enemies, reducing damage with each hit.";
    BulletModifiers.Add(new PiercingBulletModifier());
  }
}
