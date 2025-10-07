UI Layout Contract
===================

Goals
- Predictable sizing with no hidden padding
- Single source of truth for inter-slot gap vs. inner padding
- Menu/HUD differences expressed in scenes/resources, not ad‑hoc code

Definitions
- CardSize: Content area inside a slot frame that the icon scales to fill.
- FrameBorderPx: Visual frame thickness from the nine‑patch (StackLayoutConfig.SlotNinePatchMargin).
- SlotPadding: Inner breathing room inside the frame border.
- Gap: Inter-slot separation (StackLayoutConfig.Gap). If set, it overrides legacy Offset math.

Formulas
- Inner content rect per slot: CardSize
- Outer slot min size: CardSize + 2 * (FrameBorderPx + SlotPadding)
- HBox separation:
  - If Gap > 0: separation = Gap
  - Else: separation = Offset - (CardSize + 2 * (SlotPadding + FrameBorderPx))

Practices
- Do not add nine‑patch margins to UI padding; they are draw parameters, not spacing.
- Keep alignment explicit (Begin/Center) per use case; HUD rows typically Begin.
- Avoid artificial ‘visible floors’ in HUD (use visible_slot_count = 0) so no phantom frames affect layout.

HUD vs Menu
- Prefer separate StackLayoutConfig resources per context (e.g., hud_primary_stack.tres) to set CardSize, SlotPadding, Gap.
- Keep container padding in parent scenes (TopLeftHud) rather than in the slots.

