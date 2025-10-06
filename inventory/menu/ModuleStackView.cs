using Godot;
using System;
using System.Collections.Generic;

public partial class ModuleStackView : Panel
{
  [Export] public StackKind Kind { get; set; } = StackKind.Inventory;
  [Export] public StackLayoutConfig Layout { get; set; }
  [Export(PropertyHint.Range, "1,16,1")] public int VisibleSlotCount { get; set; } = 4;
  [Export] public Vector2 CardSize { get; set; } = new Vector2(100, 100);
  [Export] public Color PlaceholderColor { get; set; } = new Color(1, 1, 1, 0.65f);
  [Export] public float PlaceholderWidth { get; set; } = 0f;

  private readonly List<SlotView> _slots = new();
  private MarginContainer _content;
  private HBoxContainer _slotsBox;
  private Control _overlay;
  private ColorRect _placeholder;
  private InventoryStore _store;
  private int _placeholderIndex = -1;
  private bool _isDragHovering;
  private string _hoverModuleId = string.Empty;
  private int _highlightSlotIndex = -1;
  private float _dragGrabWithinDrawX = 0f;
  private float _dragDrawWidth = 0f;

  public override void _Ready()
  {
    Layout ??= new StackLayoutConfig();
    BuildUi();
    SetProcess(true);

    _store = InventoryStore.Instance;
    if (_store != null)
    {
      _store.StateChanged += OnStoreStateChanged;
      RenderCurrent();
    }
  }

  public override void _ExitTree()
  {
    if (_store != null)
      _store.StateChanged -= OnStoreStateChanged;
    base._ExitTree();
  }

  public override bool _CanDropData(Vector2 atPosition, Variant data)
  {
    if (!TryParseDragData(data, out string moduleId, out StackKind source, out float grabWithinDrawX, out float drawWidth))
      return false;

    if (_store == null || !_store.TryGetModuleData(moduleId, out _))
    {
      HidePlaceholder();
      return false;
    }

    _isDragHovering = true;
    _hoverModuleId = moduleId;
    _dragGrabWithinDrawX = grabWithinDrawX;
    _dragDrawWidth = drawWidth;
    UpdatePlaceholderFromMouse();
    return true;
  }

  public override void _DropData(Vector2 atPosition, Variant data)
  {
    if (!TryParseDragData(data, out string moduleId, out StackKind source, out float grabWithinDrawX, out float drawWidth))
    {
      ClearHoverState();
      return;
    }

    string resolvedModuleId = moduleId;
    StackKind resolvedSource = source;

    if (_store == null)
    {
      ClearHoverState();
      return;
    }

    // Single source of truth: prefer the live placeholder index captured during hover.
    int dropIndex = _placeholderIndex >= 0 ? _placeholderIndex : 0;
    // Fallback: if placeholder index is unavailable (edge cases), compute from mouse X.
    if (_placeholderIndex < 0)
    {
      float x = GetGlobalMousePosition().X;
      if (drawWidth > 0f)
        x = DragMath.ComputeVisualCenterX(x, grabWithinDrawX, drawWidth);
      dropIndex = ComputeDropIndexForX(x);
    }

    // Convert to InventoryStore.MoveModule index space and clamp.
    int listCount = _store.GetModules(Kind).Length; // destination list size pre-move
    int targetIndex = dropIndex;
    if (resolvedSource == Kind)
    {
      int currentIndex = GetModuleIndexIn(Kind, resolvedModuleId);
      if (currentIndex >= 0 && targetIndex > currentIndex)
        targetIndex += 1; // convert from post-removal to pre-removal index
      targetIndex = Math.Clamp(targetIndex, 0, listCount);
    }
    else
    {
      targetIndex = Math.Clamp(targetIndex, 0, listCount);
    }

    // Clear hover state BEFORE committing so the re-render uses final state (no preview gap).
    ClearHoverState();
    _store.MoveModule(resolvedModuleId, resolvedSource, Kind, targetIndex, ChangeOrigin.UI);
  }

  public override void _Notification(int what)
  {
    if (what == (int)Control.NotificationDragEnd || what == (int)Control.NotificationMouseExit)
      ClearHoverState();
    base._Notification(what);
  }

  public override void _Process(double delta)
  {
    if (_isDragHovering)
      UpdatePlaceholderFromMouse();
  }

  private void BuildUi()
  {
    MouseFilter = MouseFilterEnum.Stop;

    _content = new MarginContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
    _content.SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(_content);

    _slotsBox = new HBoxContainer
    {
      Name = "SlotsBox",
      Alignment = BoxContainer.AlignmentMode.Center,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _slotsBox.SetAnchorsPreset(LayoutPreset.FullRect);
    _content.AddChild(_slotsBox);

    _overlay = new Control { Name = "Overlay", MouseFilter = MouseFilterEnum.Ignore };
    _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(_overlay);

    _placeholder = new ColorRect
    {
      Name = "Placeholder",
      Color = PlaceholderColor,
      Visible = false,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _overlay.AddChild(_placeholder);
  }

  private void RenderModules(ModuleVm[] modules)
  {
    modules ??= Array.Empty<ModuleVm>();

    int slotCount = Math.Max(VisibleSlotCount, modules.Length);
    EnsureSlotCount(slotCount);

    for (int i = 0; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      slot.ConfigureVisuals(Layout.SlotNinePatchTexture, Layout.SlotNinePatchMargin, Layout.SlotPadding, CardSize);
      slot.Kind = Kind;
      if (i < modules.Length)
        slot.SetContent(modules[i]);
      else
        slot.Clear();
    }

    ApplyLayout(slotCount);
    HidePlaceholder();
  }

  private void ApplyLayout(int slotCount)
  {
    // Keep HBox separation in sync with the visual size of a frame.
    float separation = MathF.Max(0f, Layout.Offset - (CardSize.X + 2f * Layout.SlotPadding));
    _slotsBox.AddThemeConstantOverride("separation", (int)MathF.Round(separation));

    int horizontalMargin = (int)MathF.Round(Layout.Padding);
    int verticalMargin = (int)MathF.Round(Layout.VerticalPadding);
    _content.AddThemeConstantOverride("margin_left", horizontalMargin);
    _content.AddThemeConstantOverride("margin_right", horizontalMargin);
    _content.AddThemeConstantOverride("margin_top", verticalMargin);
    _content.AddThemeConstantOverride("margin_bottom", verticalMargin);

    float frameWidth = CardSize.X + 2f * (Layout.SlotPadding + Layout.SlotNinePatchMargin);
    float frameHeight = CardSize.Y + 2f * (Layout.SlotPadding + Layout.SlotNinePatchMargin);
    float totalWidth = slotCount > 0 ? frameWidth * slotCount + separation * (slotCount - 1) : 0f;
    totalWidth += horizontalMargin * 2f;
    float totalHeight = frameHeight + verticalMargin * 2f;
    CustomMinimumSize = new Vector2(totalWidth, totalHeight);
  }

  private void EnsureSlotCount(int desired)
  {
    while (_slots.Count < desired)
    {
      var slot = new SlotView { Kind = Kind };
      _slots.Add(slot);
      _slotsBox.AddChild(slot);
    }
    while (_slots.Count > desired)
    {
      var last = _slots[_slots.Count - 1];
      _slots.RemoveAt(_slots.Count - 1);
      last.QueueFree();
    }
  }

  private int ComputeDropIndexForX(float x)
  {
    if (_slots.Count == 0)
      return 0;

    // Border-based logic: change index only when crossing the border between slots
    // (i.e., the left edge of the next slot), not at midpoints.
    for (int i = 0; i < _slots.Count; i++)
    {
      Rect2 rect = _slots[i].GetGlobalRect();
      float left = rect.Position.X;
      float right = left + rect.Size.X;
      if (x < left)
        return i;           // before this slot
      if (x <= right)
        return i;           // inside this slot
    }
    return _slots.Count;     // after the last slot
  }

  private bool TryParseDragData(Variant data, out string moduleId, out StackKind source, out float grabWithinDrawX, out float drawWidth)
  {
    moduleId = null;
    source = Kind;
    grabWithinDrawX = 0f;
    drawWidth = 0f;

    if (data.VariantType != Variant.Type.Dictionary)
      return false;

    var dict = (Godot.Collections.Dictionary)data;
    if (!dict.TryGetValue("module_id", out Variant moduleVariant) || moduleVariant.VariantType != Variant.Type.String)
      return false;

    moduleId = (string)moduleVariant;

    if (dict.TryGetValue("source_stack", out Variant sourceVariant) && sourceVariant.VariantType == Variant.Type.Int)
    {
      try { source = (StackKind)(int)sourceVariant; }
      catch { source = Kind; }
    }
    if (dict.TryGetValue("grab_within_draw_x", out Variant grabVar) && grabVar.VariantType == Variant.Type.Float)
      grabWithinDrawX = (float)grabVar;
    if (dict.TryGetValue("draw_width", out Variant drawVar) && drawVar.VariantType == Variant.Type.Float)
      drawWidth = (float)drawVar;
    return true;
  }

  /// <summary>
  /// Places the placeholder BEFORE the slot at 'index'. If index == _slots.Count,
  /// it places it AFTER the last slot. All math is done in GLOBAL space and then
  /// converted into the overlay’s LOCAL coordinates.
  /// </summary>
  private void PositionPlaceholder(int index)
  {
    if (_placeholder == null || _overlay == null)
      return;

    Rect2 overlayRect = _overlay.GetGlobalRect();

    // Dimensions: use the actual slot frame rect height/width for consistency with visuals.
    Vector2 size;
    Vector2 globalPos;

    if (_slots.Count == 0)
    {
      // No slots yet: anchor to the slotsBox top-left.
      Rect2 boxRect = _slotsBox.GetGlobalRect();
      float width = PlaceholderWidth > 0f ? PlaceholderWidth : GetFrameSize().X;
      size = new Vector2(width, GetFrameSize().Y);
      globalPos = boxRect.Position;
    }
    else
    {
      index = Math.Clamp(index, 0, _slots.Count);

      // Determine anchor rect at the insertion edge.
      // If inserting before slot k, anchor is slot k’s left edge.
      // If inserting at the end, anchor is the right edge of the last slot.
      Rect2 refRect;
      float anchorX;
      if (index == 0)
      {
        refRect = _slots[0].GetGlobalRect();
        anchorX = refRect.Position.X;
      }
      else
      {
        refRect = _slots[index - 1].GetGlobalRect();
        anchorX = refRect.Position.X + refRect.Size.X;
      }

      float width = PlaceholderWidth > 0f ? PlaceholderWidth : refRect.Size.X; // full-slot width by default
      float height = refRect.Size.Y;

      size = new Vector2(width, height);
      globalPos = new Vector2(anchorX, refRect.Position.Y);
    }

    // Convert global -> overlay-local
    _placeholder.Position = globalPos - overlayRect.Position;
    _placeholder.Size = size;
  }

  private void HidePlaceholder()
  {
    SetHighlightedSlot(-1);
    HideTrailingPlaceholder();
    _placeholderIndex = -1;
  }

  private void UpdatePlaceholderFromMouse()
  {
    if (!_isDragHovering)
      return;

    if (_store == null || string.IsNullOrEmpty(_hoverModuleId) || !_store.TryGetModuleData(_hoverModuleId, out _))
    {
      HidePlaceholder();
      return;
    }

    // Use the same global mouse space as ComputeDropIndex/GetGlobalRect().
    float mouseX = GetGlobalMousePosition().X;
    // Adjust the anchor X to the visual center if we have geometry from the drag source.
    float anchorX = (_dragDrawWidth > 0f)
      ? DragMath.ComputeVisualCenterX(mouseX, _dragGrabWithinDrawX, _dragDrawWidth)
      : mouseX;

    int index = ComputeDropIndexForX(anchorX);

    ShowPlaceholder(index);

    // Re-render to show the gap in the logical sequence.
    RenderCurrent();
  }

  private void ClearHoverState()
  {
    _isDragHovering = false;
    _hoverModuleId = string.Empty;
    HidePlaceholder();
  }

  private Vector2 GetFrameSize()
  {
    float width = CardSize.X + 2f * (Layout.SlotPadding + Layout.SlotNinePatchMargin);
    float height = CardSize.Y + 2f * (Layout.SlotPadding + Layout.SlotNinePatchMargin);
    return new Vector2(width, height);
  }

  private void ShowPlaceholder(int index)
  {
    if (_slots.Count == 0)
    {
      SetHighlightedSlot(-1);
      _placeholderIndex = 0;
      PositionPlaceholder(0);      // FIX: Respect index at 0
      ShowTrailingPlaceholder();
      return;
    }

    index = Math.Clamp(index, 0, _slots.Count);
    _placeholderIndex = index;

    if (index >= _slots.Count)
    {
      // Trailing placeholder (after the last slot)
      SetHighlightedSlot(-1);
      PositionPlaceholder(index);  // FIX: Place at the correct trailing anchor
      ShowTrailingPlaceholder();
    }
    else
    {
      // Highlight a concrete slot and hide the trailing bar
      SetHighlightedSlot(index);
      HideTrailingPlaceholder();
    }
  }

  private int GetModuleIndexIn(StackKind kind, string moduleId)
  {
    if (_store == null || string.IsNullOrEmpty(moduleId)) return -1;
    var modules = _store.GetModules(kind);
    for (int i = 0; i < modules.Length; i++)
    {
      if (modules[i]?.ModuleId == moduleId)
        return i;
    }
    return -1;
  }

  private void ShowTrailingPlaceholder()
  {
    if (_placeholder != null)
      _placeholder.Visible = true;
  }

  private void HideTrailingPlaceholder()
  {
    if (_placeholder != null)
      _placeholder.Visible = false;
  }

  private void SetHighlightedSlot(int index)
  {
    if (_highlightSlotIndex == index)
      return;

    if (_highlightSlotIndex >= 0 && _highlightSlotIndex < _slots.Count)
      _slots[_highlightSlotIndex].SetPlaceholderHighlight(false);

    _highlightSlotIndex = index;

    if (_highlightSlotIndex >= 0 && _highlightSlotIndex < _slots.Count)
      _slots[_highlightSlotIndex].SetPlaceholderHighlight(true);
  }

  private void OnStoreStateChanged(InventoryState state, ChangeOrigin origin)
  {
    RenderCurrent();
  }

  private void RenderCurrent()
  {
    if (_store == null)
      return;

    var baseModules = _store.GetModules(Kind);

    if (!_isDragHovering || string.IsNullOrEmpty(_hoverModuleId))
    {
      RenderModules(baseModules);
      return;
    }

    // Build preview list with a gap at _placeholderIndex.
    var others = new List<ModuleVm>(baseModules);

    // If dragging within this same stack, remove the dragged entry to create the gap.
    int removeIdx = others.FindIndex(vm => vm.ModuleId == _hoverModuleId);
    if (removeIdx >= 0)
      others.RemoveAt(removeIdx);

    int placeholderIndex = Math.Clamp(_placeholderIndex >= 0 ? _placeholderIndex : others.Count, 0, others.Count);
    int slotCount = Math.Max(VisibleSlotCount, others.Count + 1);
    EnsureSlotCount(slotCount);

    int j = 0;
    for (int i = 0; i < _slots.Count; i++)
    {
      var slot = _slots[i];
      slot.ConfigureVisuals(Layout.SlotNinePatchTexture, Layout.SlotNinePatchMargin, Layout.SlotPadding, CardSize);
      slot.Kind = Kind;

      if (i == placeholderIndex)
      {
        slot.SetContent(null);
        slot.SetPlaceholderHighlight(true);
      }
      else if (j < others.Count)
      {
        slot.SetContent(others[j]);
        slot.SetPlaceholderHighlight(false);
        j++;
      }
      else
      {
        slot.SetContent(null);
        slot.SetPlaceholderHighlight(false);
      }
    }

    ApplyLayout(slotCount);
  }
}
