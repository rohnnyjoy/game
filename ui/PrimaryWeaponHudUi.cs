using Godot;

public partial class PrimaryWeaponHudUi : PrimaryWeaponStack
{
  [Export] public Vector2 HudCardSize { get; set; } = new Vector2(72, 72);
  [Export(PropertyHint.Range, "1,16,1")] public int HudVisibleSlotCount { get; set; } = 4;

  public override void _Ready()
  {
    EnableInteractions = false;
    DrawBackground = false;
    CardSize = HudCardSize;
    VisibleSlotCount = HudVisibleSlotCount;

    // Use a layout with no outer padding; only inter-slot gap.
    var hudLayout = new StackLayoutConfig();
    hudLayout.Padding = 0f;
    hudLayout.VerticalPadding = 0f;
    hudLayout.SlotPadding = 6f; // keep icon inset inside the frame
    // Set offset so computed separation equals our desired gap (8px).
    float desiredGap = 8f;
    hudLayout.Offset = CardSize.X + 2f * hudLayout.SlotPadding + desiredGap;
    Layout = hudLayout;

    base._Ready();

    MouseFilter = MouseFilterEnum.Ignore;
    AnchorLeft = 0; AnchorRight = 0; AnchorTop = 0; AnchorBottom = 0;
    OffsetLeft = 0; OffsetTop = 0; OffsetRight = 0; OffsetBottom = 0;
    SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    SizeFlagsVertical = SizeFlags.ShrinkBegin;
  }
}
