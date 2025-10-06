using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;

public partial class AimbotModule : WeaponModule
{
  [Export]
  public float aim_cone_angle { get; set; } = (float)(120 * Math.PI / 180.0); // 120° in radians

  [Export]
  public float vertical_offset { get; set; } = 0.0f;

  [Export]
  public float target_line_width { get; set; } = 0.1f;

  [Export]
  public float target_line_duration { get; set; } = 0.05f;

  public AimbotModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(1); // aimbot
    ModuleName = "Mechanical Lens";
    Rarity = Rarity.Epic;
    ModuleDescription = "Attacks reorient mid-flight toward the nearest enemy across a large range (within a wide 120° cone).";
    BulletModifiers.Add(new AimbotBulletModifier());
  }
}
