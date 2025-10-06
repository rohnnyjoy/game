using Godot;

public partial class TopLeftHud : VBoxContainer
{
  public override void _Ready()
  {
    // Dynamically align the health text with the visual content area of the slot frames.
    // The slot frames reserve a border via nine-patch margin; the icon content starts at
    // (SlotNinePatchMargin + SlotPadding). Mirror that as a left margin on the health row.
    var stack = GetNodeOrNull<Node>("PrimaryWeaponStack") as Node;
    var inset = GetNodeOrNull<MarginContainer>("HealthInset");
    if (stack == null || inset == null)
      return;

    // PrimaryWeaponStack derives from ModuleStackView and exposes a Layout with these fields.
    float leftInset = 0f;
    if (stack is ModuleStackView msv && msv.Layout != null)
    {
      leftInset = msv.Layout.SlotNinePatchMargin + msv.Layout.SlotPadding;
    }

    int px = (int)Mathf.Round(leftInset);
    inset.AddThemeConstantOverride("margin_left", px);
  }
}

