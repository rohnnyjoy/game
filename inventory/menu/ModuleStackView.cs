using Godot;
using System;
using System.Collections.Generic;

public partial class ModuleStackView : Panel
{
  [Export]
  public StackKind Kind { get; set; } = StackKind.Inventory;

  [Export]
  public StackLayoutConfig Layout { get; set; }

  [Export(PropertyHint.Range, "1,16,1")]
  public int VisibleSlotCount { get; set; } = 4;

  [Export]
  public Vector2 CardSize { get; set; } = new Vector2(100, 100);

  [Export]
  public Color PlaceholderColor { get; set; } = new Color(1, 1, 1, 0.65f);

  [Export]
  public float PlaceholderWidth { get; set; } = 0f;

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
    if (!TryParseDragData(data, out string moduleId, out StackKind source))
      return false;

    if (_store == null || !_store.TryGetModuleData(moduleId, out _))
    {
      HidePlaceholder();
      return false;
    }

    _isDragHovering = true;
    _hoverModuleId = moduleId;
    UpdatePlaceholderFromMouse();

    return true;
  }

  public override void _DropData(Vector2 atPosition, Variant data)
  {
    if (!TryParseDragData(data, out string moduleId, out StackKind source))
    {
      ClearHoverState();
      return;
    }
    string resolvedModuleId = moduleId;
    StackKind resolvedSource = source;
    ClearHoverState();

    if (_store == null)
      return;

    int targetIndex = ComputeDropIndex(GetGlobalMousePosition());
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

    _content = new MarginContainer
    {
      Name = "Content",
      MouseFilter = MouseFilterEnum.Ignore
    };
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

    _overlay = new Control
    {
      Name = "Overlay",
      MouseFilter = MouseFilterEnum.Ignore
    };
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
    if (modules == null)
      modules = Array.Empty<ModuleVm>();

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
      var slot = new SlotView
      {
        Kind = Kind
      };
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

  private int ComputeDropIndex(Vector2 globalMouse)
  {
    if (_slots.Count == 0)
      return 0;

    for (int i = 0; i < _slots.Count; i++)
    {
      Rect2 rect = _slots[i].GetGlobalRect();
      float mid = rect.Position.X + rect.Size.X * 0.5f;
      if (globalMouse.X < mid)
        return i;
    }
    return _slots.Count;
  }

  private bool TryParseDragData(Variant data, out string moduleId, out StackKind source)
  {
    moduleId = null;
    source = Kind;

    if (data.VariantType != Variant.Type.Dictionary)
      return false;

    var dict = (Godot.Collections.Dictionary)data;
    if (!dict.TryGetValue("module_id", out Variant moduleVariant) || moduleVariant.VariantType != Variant.Type.String)
      return false;

    moduleId = (string)moduleVariant;

    if (dict.TryGetValue("source_stack", out Variant sourceVariant) && sourceVariant.VariantType == Variant.Type.Int)
    {
      try
      {
        source = (StackKind)(int)sourceVariant;
      }
      catch
      {
        source = Kind;
      }
    }

    return true;
  }

  private void PositionPlaceholder(int index)
  {
    if (_placeholder == null || _slotsBox == null)
      return;

    Rect2 overlayRect = _overlay.GetGlobalRect();
    Rect2 boxRect = _slotsBox.GetGlobalRect();

    Vector2 frameSize = GetFrameSize();
    float separation = MathF.Max(0f, Layout.Offset - (CardSize.X + 2f * Layout.SlotPadding));

    float targetWidth = PlaceholderWidth > 0f ? PlaceholderWidth : frameSize.X;
    float targetHeight = frameSize.Y;
    Vector2 globalPos;

    if (_slots.Count == 0)
    {
      globalPos = boxRect.Position;
    }
    else
    {
      Rect2 lastRect = _slots[^1].GetGlobalRect();
      globalPos = new Vector2(lastRect.Position.X + lastRect.Size.X + separation, lastRect.Position.Y);
    }

    float localX = globalPos.X - overlayRect.Position.X;
    float localY = globalPos.Y - overlayRect.Position.Y;
    _placeholder.Position = new Vector2(localX, localY);
    _placeholder.Size = new Vector2(targetWidth, targetHeight);
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

    Vector2 globalMouse = GetViewport()?.GetMousePosition() ?? GetGlobalMousePosition();
    int index = ComputeDropIndex(globalMouse);
    ShowPlaceholder(index);
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
      PositionPlaceholder(0);
      ShowTrailingPlaceholder();
      return;
    }

    index = Math.Clamp(index, 0, _slots.Count);
    if (index >= _slots.Count)
    {
      SetHighlightedSlot(-1);
      _placeholderIndex = index;
      PositionPlaceholder(index);
      ShowTrailingPlaceholder();
    }
    else
    {
      SetHighlightedSlot(index);
      _placeholderIndex = index;
      HideTrailingPlaceholder();
    }
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

    // Build preview sequence: remove from same stack if applicable, insert placeholder at index
    var others = new System.Collections.Generic.List<ModuleVm>(baseModules);
    // If dragging within the same stack, remove the module being dragged so we create a gap
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
