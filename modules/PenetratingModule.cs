using Godot;
using System;
using Godot.Collections;
using System.Threading.Tasks;

public partial class PiercingModule : WeaponModule
{
  public PiercingModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(2); // penetrating
    Rarity = Rarity.Uncommon;
    ModuleDescription = "Bullets can penetrate multiple enemies, reducing damage with each hit.";
    BulletModifiers.Add(new PiercingBulletModifier());
  }
}
