using Godot;
public partial class AimbotModule : WeaponModule, IAimbotProvider
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
  }

  public bool TryGetAimbotConfig(out AimbotProviderConfig config)
  {
    config = new AimbotProviderConfig(
      Mathf.Max(0.0f, aim_cone_angle),
      vertical_offset,
      1000.0f,
      Mathf.Max(0.0f, target_line_width),
      Mathf.Max(0.0f, target_line_duration)
    );
    return true;
  }
}
