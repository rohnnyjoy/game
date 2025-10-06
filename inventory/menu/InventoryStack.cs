using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class InventoryStack : CardStack, IFramedCardStack
{
  private readonly System.Collections.Generic.Dictionary<WeaponModule, WeaponModuleCard2D> _cardMap = new();
  private bool _layoutDirty = false;
  private bool _populateQueued = false;
  private MarginContainer _content;
  private HBoxContainer _slotsBox;
  // Independent vertical padding (top/bottom) inside the framed panel
  [Export]
  public float VerticalPadding { get; set; } = 12.0f;
  [Export]
  public StackLayoutConfig Layout { get; set; }
  [Export]
  public int SlotCount { get; set; } = 8; // Visible containers for inventory slots

  [Export]
  public Color SlotFillColor { get; set; } = new Color(0.95f, 0.95f, 0.95f, 0.35f);

  [Export]
  public Color SlotBorderColor { get; set; } = new Color(0.1f, 0.1f, 0.1f, 0.6f);

  [Export]
  public float SlotPadding { get; set; } = 6.0f; // Inner padding within each slot

  [Export]
  public Texture2D SlotNinePatchTexture { get; set; } = GD.Load<Texture2D>("res://assets/ui/3x/ninepatch.png");

  [Export]
  public int SlotNinePatchMargin { get; set; } = 18;

  private bool _suppressPopulate = false;
  private Control _slotLayer;
  // Drag/placeholder state
  private Card2D _dragCard;
  private SlotFrame _dragFrame;
  private int _originIndex;
  private int _lastPlaceholderIndex;
  private int _internalPreviewIndex = -1;
  private Vector2 _dragStartGlobal;
  [Export] public float HorizontalThresholdPx { get; set; } = 10f;
  private Array<Card2D> _dragSnapshot;


  public override void _Ready()
  {
    base._Ready();
    DebugLogs = true;

    Inventory inventory = Player.Instance.Inventory;
    inventory.InventoryChanged += OnInventoryChanged;

    // Container-driven slot layout with inner content margin for Padding
    _content = new MarginContainer { Name = "Content" };
    AddChild(_content);
    _content.SetAnchorsPreset(LayoutPreset.FullRect);
    ApplySharedLayout();
    _content.AddThemeConstantOverride("margin_left", (int)Padding);
    _content.AddThemeConstantOverride("margin_right", (int)Padding);
    _content.AddThemeConstantOverride("margin_top", (int)VerticalPadding);
    _content.AddThemeConstantOverride("margin_bottom", (int)VerticalPadding);

    _slotsBox = new HBoxContainer { Name = "SlotsBox" };
    _content.AddChild(_slotsBox);
    _slotsBox.SetAnchorsPreset(LayoutPreset.FullRect);
    // Center the slot frames within available width
    _slotsBox.Alignment = BoxContainer.AlignmentMode.Center;

    AutoSizeToFitSlots();
    UpdateSlotBackgrounds();
    PopulateCards();
  }

  public override void _ExitTree()
  {
    // Avoid duplicate handlers after scene reloads
    try { if (Player.Instance?.Inventory != null) Player.Instance.Inventory.InventoryChanged -= OnInventoryChanged; } catch { }
    base._ExitTree();
  }

  private void PopulateCards()
  {
    DebugTrace.Log($"InventoryStack.PopulateCards start");
    var modules = Player.Instance.Inventory.WeaponModules;

    // Remove stale cards
    var keys = new System.Collections.Generic.List<WeaponModule>(_cardMap.Keys);
    foreach (var m in keys)
    {
      if (!modules.Contains(m))
      {
        var card = _cardMap[m];
        if (card != Card2D.CurrentlyDragged)
        {
          var parent = card.GetParent();
          if (parent != null) parent.RemoveChild(card);
          card.QueueFree();
        }
        _cardMap.Remove(m);
      }
    }

    // Assemble ordered list
    var ordered = new Array<Card2D>();
    foreach (WeaponModule module in modules)
    {
      if (!_cardMap.TryGetValue(module, out var card))
      {
        card = new WeaponModuleCard2D { Module = module };
        _cardMap[module] = card;
      }
      ordered.Add(card);
    }

    UpdateSlotBackgrounds();
    int idx = 0;
    foreach (Card2D c in ordered)
    {
      var frame = _slotsBox.GetChild(idx) as SlotFrame;
      if (c == Card2D.CurrentlyDragged)
        frame?.ClearCard();
      else
        frame?.AdoptCard(c);
      idx++;
    }
    DebugTrace.Log($"InventoryStack.PopulateCards done count={ordered.Count}");
  }

  public override void _Process(double delta)
  {
    // Update live preview during drag.
    if (_dragCard != null && _slotsBox != null && _dragFrame != null)
    {
      // Safety: if mouse released but drag state still present, finalize here.
      if (!Input.IsMouseButtonPressed(MouseButton.Left))
      {
        DebugTrace.Log($"InventoryStack._Process auto-finalize drag");
        EndCardDrag(_dragCard, GetGlobalMousePosition());
        return;
      }
      float dx = Mathf.Abs(GetGlobalMousePosition().X - _dragStartGlobal.X);
      int targetIndex = _originIndex;
      if (dx >= HorizontalThresholdPx)
      {
        targetIndex = ComputeNearestIndexLocal(GetLocalMousePosition());
        targetIndex = Mathf.Clamp(targetIndex, 0, _slotsBox.GetChildCount() - 1);
      }
      if (targetIndex != _lastPlaceholderIndex)
      {
        _lastPlaceholderIndex = targetIndex;
        // Live preview disabled to avoid scene churn during drag.
      }
    }
    else
    {
      // Support external drag preview when mouse is over this stack but drag started elsewhere
      var dragging = Card2D.CurrentlyDragged;
      if (dragging != null && _slotsBox != null)
      {
        bool inside = GetGlobalRect().HasPoint(dragging.GetGlobalMousePosition());
        if (inside)
        {
          int targetIndex = ComputeNearestIndexLocal(GetLocalMousePosition());
          targetIndex = Mathf.Clamp(targetIndex, 0, _slotsBox.GetChildCount() - 1);
          if (targetIndex != _lastPlaceholderIndex)
          {
            _lastPlaceholderIndex = targetIndex;
            // External preview disabled to avoid scene churn.
          }
        }
        else if (_lastPlaceholderIndex != -1)
        {
          // Restore layout when cursor leaves
          PopulateCards();
          _lastPlaceholderIndex = -1;
        }
      }
      else if (_lastPlaceholderIndex != -1)
      {
        // No layout changes on leave; preview was visual-only
        _lastPlaceholderIndex = -1;
      }
    }
  }

  private void UpdateSlotBackgrounds()
  {
    if (_slotsBox == null) return;
    Vector2 cardSize = new Vector2(100, 100);
    var currentCards = GetAllCardsInFrames();
    if (currentCards.Count > 0)
      cardSize = currentCards[0].CardCore.CardSize;
    int moduleCount = 0;
    try { moduleCount = Player.Instance?.Inventory?.WeaponModules?.Count ?? 0; } catch { moduleCount = 0; }
    int containers = Mathf.Max(SlotCount, Mathf.Max(currentCards.Count, moduleCount));

    float frameW = cardSize.X + 2.0f * (SlotPadding + SlotNinePatchMargin);
    float frameH = cardSize.Y + 2.0f * (SlotPadding + SlotNinePatchMargin);
    float separation = Mathf.Max(0, Offset - (cardSize.X + 2.0f * SlotPadding));
    _slotsBox.AddThemeConstantOverride("separation", (int)separation);
    // Keep outer padding equal to the internal separation for visual balance
    if (!Mathf.IsEqualApprox(Padding, separation))
    {
      Padding = separation;
    }
    if (_content != null)
    {
      _content.AddThemeConstantOverride("margin_left", (int)Padding);
      _content.AddThemeConstantOverride("margin_right", (int)Padding);
      // Apply same padding vertically to keep the slots centered with equal top/bottom spacing
      _content.AddThemeConstantOverride("margin_top", (int)VerticalPadding);
      _content.AddThemeConstantOverride("margin_bottom", (int)VerticalPadding);
    }

    // One-time layout log to debug mismatches between stacks
    if (DebugLogs && !_layoutDirty)
    {
      GD.Print($"[InventoryStack:{Name}] layout cardSize={cardSize} frame=({frameW},{frameH}) sep={separation} padding={Padding} slotPad={SlotPadding} patch={SlotNinePatchMargin} offset={Offset} containers={containers}");
      _layoutDirty = true;
    }

    while (_slotsBox.GetChildCount() < containers)
    {
      var frame = new SlotFrame
      {
        Name = $"SlotFrame{_slotsBox.GetChildCount()}",
        FrameTexture = SlotNinePatchTexture,
        PatchMargin = SlotNinePatchMargin,
        InnerPadding = SlotPadding,
        CustomMinimumSize = new Vector2(frameW, frameH)
      };
      _slotsBox.AddChild(frame);
    }
    while (_slotsBox.GetChildCount() > containers)
    {
      _slotsBox.GetChild(_slotsBox.GetChildCount() - 1).QueueFree();
    }
  }

  private void AutoSizeToFitSlots()
  {
    Vector2 cardSize = new Vector2(100, 100);
    var currentCards = GetAllCardsInFrames();
    if (currentCards.Count > 0)
      cardSize = currentCards[0].CardCore.CardSize;
    int moduleCount = 0;
    try { moduleCount = Player.Instance?.Inventory?.WeaponModules?.Count ?? 0; } catch { moduleCount = 0; }
    int containers = Mathf.Max(SlotCount, Mathf.Max(currentCards.Count, moduleCount));
    // Padding and Offset come from shared layout; do not auto-override them here.
    float frameW = cardSize.X + 2.0f * (SlotPadding + SlotNinePatchMargin);
    float separation = Mathf.Max(0, Offset - (cardSize.X + 2.0f * SlotPadding));
    float width = Padding * 2 + (containers > 0 ? (containers - 1) * separation + containers * frameW : 0);
    float height = VerticalPadding * 2 + cardSize.Y + 2.0f * (SlotPadding + SlotNinePatchMargin);
    CustomMinimumSize = new Vector2(width, height);

    if (_content != null)
    {
      _content.AddThemeConstantOverride("margin_left", (int)Padding);
      _content.AddThemeConstantOverride("margin_right", (int)Padding);
      _content.AddThemeConstantOverride("margin_top", (int)VerticalPadding);
      _content.AddThemeConstantOverride("margin_bottom", (int)VerticalPadding);
    }
  }

  private WeaponModuleCard2D findCard(WeaponModule module)
  {
    // Prefer globally registered card to avoid duplicate instances across stacks
    if (WeaponModuleCard2D.TryGetForModule(module, out var existing))
      return existing;
    foreach (Card2D card in GetAllCardsInFrames())
    {
      if (card is WeaponModuleCard2D moduleCard && moduleCard.Module == module)
        return moduleCard;
    }
    return null;
  }

  private Array<Card2D> GetAllCardsInFrames()
  {
    var list = new Array<Card2D>();
    if (_slotsBox == null) return list;
    foreach (Node child in _slotsBox.GetChildren())
    {
      if (child is SlotFrame frame)
      {
        var c = frame.GetCard();
        if (c != null) list.Add(c);
      }
    }
    return list;
  }

  private void OnInventoryChanged()
  {
    DebugTrace.Log($"InventoryStack.OnInventoryChanged");
    if (_suppressPopulate) return;
    if (DebugLogs) GD.Print($"[InventoryStack:{Name}] InventoryChanged (queued)");
    // Defer populate to avoid re-entrant scene graph mutations during drops
    if (!_populateQueued)
    {
      _populateQueued = true;
      CallDeferred(nameof(DeferredPopulate));
    }
  }

  private void DeferredPopulate()
  {
    DebugTrace.Log($"InventoryStack.DeferredPopulate");
    _populateQueued = false;
    PopulateCards();
    UpdateSlotBackgrounds();
    AutoSizeToFitSlots();
  }

  public override void OnCardsChanged(Array<Card2D> newCards)
  {
    // No muting: treat as model change only; UI repopulates deferred
    if (_dragCard != null) { DebugTrace.Log($"InventoryStack.OnCardsChanged ignored (active drag)"); return; }
    if (DebugLogs) GD.Print($"[InventoryStack:{Name}] OnCardsChanged count={newCards.Count}");
    // Defensive: ignore empty lists from legacy CardStack drop paths to avoid
    // wiping inventory modules on canceled/invalid drags.
    if (newCards == null || newCards.Count == 0)
    {
      DebugTrace.Log($"InventoryStack.OnCardsChanged ignored (empty)");
      return;
    }
    // Update the underlying data structure based on the new card order.
    var newModules = new Array<WeaponModule>();
    foreach (Card2D card in newCards)
    {
      if (card is WeaponModuleCard2D moduleCard)
        newModules.Add(moduleCard.Module);
    }
    DebugTrace.Log($"InventoryStack.OnCardsChanged commit modules={newModules.Count}");
    _suppressPopulate = true;
    Player.Instance.Inventory.WeaponModules = newModules;
    _suppressPopulate = false;
    if (!_populateQueued)
    {
      _populateQueued = true;
      CallDeferred(nameof(DeferredPopulate));
    }
  }

  // IFramedCardStack: Begin drag with placeholder behavior
  public void BeginCardDrag(Card2D card, SlotFrame fromFrame, Vector2 startGlobalMouse)
  {
    DebugTrace.Log($"InventoryStack.BeginCardDrag card={card.Name} frame={fromFrame?.Name}");
    _dragCard = card;
    _dragFrame = fromFrame;
    _originIndex = _dragFrame.GetIndex();
    _lastPlaceholderIndex = _originIndex;
    _dragStartGlobal = startGlobalMouse;

    // Snapshot original card order
    _dragSnapshot = GetAllCardsInFrames();

    // Preserve global position before detaching to avoid jumps
    var gp = card.GlobalPosition;
    var oldParent = card.GetParent();
    if (DebugLogs)
      GD.Print($"[InventoryStack:{Name}] BeginDrag card={card.Name} oldParent={(oldParent != null ? oldParent.GetPath() : new NodePath("<null>")).ToString()} gp(before)={gp}");

    // Do not reparent during drag; just switch to top-level for global coords.
    card.TopLevel = true; // use global coordinates during drag for consistency
    card.GlobalPosition = gp;
    // Keep on top within the canvas layer while dragging
    card.ZIndex = 4095;
    card.MoveToFront();
    card.RecomputeDragOffset();
    if (DebugLogs)
      GD.Print($"[InventoryStack:{Name}] AfterBeginDrag card={card.Name} gp(after)={card.GlobalPosition} toplevel={card.TopLevel} parent={card.GetParent().GetPath()}");

    // Leave origin slot empty; no live reflow/placeholder to reduce churn.
    DebugTrace.Log($"InventoryStack.BeginCardDrag live preview disabled");
  }

  // Choose a drag parent within the nearest CanvasLayer so draw order stays above UI.
  private Node GetDragLayer()
  {
    Node n = this;
    while (n != null)
    {
      if (n is CanvasLayer)
        return n;
      n = n.GetParent();
    }
    // Fallback: root window
    return GetTree().Root;
  }

  // IFramedCardStack: End drag, decide reorder or cancel
  public bool EndCardDrag(Card2D card, Vector2 endGlobalMouse)
  {
    if (_dragCard != card || _dragFrame == null)
    {
      return false;
    }

    bool insideThis = GetGlobalRect().HasPoint(endGlobalMouse);
    float dx = Mathf.Abs(endGlobalMouse.X - _dragStartGlobal.X);
    // Recompute target index at drop time to avoid stale placeholder index
    Vector2 endLocal = endGlobalMouse - GetGlobalRect().Position;
    int destIndex = ComputeNearestIndexLocal(endLocal);
    destIndex = Mathf.Clamp(destIndex, 0, _slotsBox.GetChildCount() - 1);
    GD.Print($"[InventoryStack:{Name}] EndDrag dx={dx} origin={_originIndex} placeholder={_lastPlaceholderIndex} destIndex(computed)={destIndex}");

    // If not inside this stack, cancel and snap back
    if (!insideThis)
    {
      // Let Card2D handle outside-of-stacks drop (e.g., convert to 3D)
      _dragFrame.SetPlaceholder(false);
      // Restore layout to data state
      PopulateCards();
      card.TopLevel = false;
      // Keep info UI consistent in cancel cases
      var weapon = Player.Instance?.Inventory?.PrimaryWeapon;
      if (weapon != null)
      {
        weapon.Modules = weapon.Modules;
      }
      ClearDragState();
      GD.Print($"[InventoryStack:{Name}] EndCardDrag leaving; returning false");
      return false;
    }

    bool cancel = dx < HorizontalThresholdPx && destIndex == _originIndex;
    if (cancel)
    {
      // Tween back to the origin frame center, then adopt back
      var frameRect = _dragFrame.GetGlobalRect();
      var to = frameRect.Position + frameRect.Size * 0.5f - card.CardCore.CardSize * 0.5f;
      var tween = card.CreateTween();
      tween.TweenProperty(card, "global_position", to, 0.18f)
           .SetTrans(Tween.TransitionType.Quad)
           .SetEase(Tween.EaseType.Out);
      tween.Finished += () =>
      {
        card.TopLevel = false;
        _dragFrame.AdoptCard(card);
      };
      _dragFrame.SetPlaceholder(false);
      // Restore full layout
      PopulateCards();
      ClearDragState();
      GD.Print($"[InventoryStack:{Name}] EndCardDrag cancel -> snap back");
      return true;
    }

    // Avoid reparenting/adopting here while still in release handling; defer to data update + repopulate
    card.TopLevel = false;

    // Commit the reorder into the Inventory data model and refresh (deferred)
    HandleDrop(card, destIndex);
    _dragFrame.SetPlaceholder(false);
    ClearDragState();
    GD.Print($"[InventoryStack:{Name}] EndCardDrag committed");
    return true;
  }

  private void ClearDragState()
  {
    _dragCard = null;
    _dragFrame = null;
    _lastPlaceholderIndex = -1;
    if (_internalPreviewIndex != -1 && _slotsBox != null && _slotsBox.GetChildCount() > _internalPreviewIndex && _slotsBox.GetChild(_internalPreviewIndex) is SlotFrame prev)
    {
      prev.SetPlaceholder(false);
    }
    _internalPreviewIndex = -1;
  }

  private int ComputeNearestIndexLocal(Vector2 localMouse)
  {
    if (_slotsBox == null || _slotsBox.GetChildCount() == 0)
      return 0;

    // Compare mouse X to frame midpoints (global -> local)
    float lx = localMouse.X;
    int n = _slotsBox.GetChildCount();
    int best = 0;
    float bestDist = float.MaxValue;
    for (int i = 0; i < n; i++)
    {
      if (_slotsBox.GetChild(i) is Control frame)
      {
        var r = frame.GetGlobalRect();
        float mid = (r.Position.X + r.End.X) * 0.5f;
        float localMid = mid - GetGlobalRect().Position.X;
        float d = Mathf.Abs(lx - localMid);
        if (d < bestDist)
        {
          bestDist = d;
          best = i;
        }
      }
    }
    return best;
  }

  private void PreviewArrange(int placeholderIndex)
  {
    // No-op: live preview disabled for stability.
    DebugTrace.Log($"InventoryStack.PreviewArrange (noop) idx={placeholderIndex}");
  }

  private void PreviewArrangeExternal(int placeholderIndex)
  {
    // No-op: external preview disabled for stability.
    DebugTrace.Log($"InventoryStack.PreviewArrangeExternal (noop) idx={placeholderIndex}");
  }

  public void HandleDrop(Card2D card, int destIndex)
  {
    if (card is not WeaponModuleCard2D wm) return;
    DebugTrace.Log($"InventoryStack.HandleDrop module={wm.Module?.GetType().Name} destIndex={destIndex}");
    var inv = Player.Instance.Inventory;
    var invMods = new Array<WeaponModule>(inv.WeaponModules);
    var weapon = inv.PrimaryWeapon;
    var weapMods = new Array<WeaponModule>(weapon.Modules);

    // Remove from both lists if present
    invMods.Remove(wm.Module);
    weapMods.Remove(wm.Module);

    destIndex = Mathf.Clamp(destIndex, 0, invMods.Count);
    invMods.Insert(destIndex, wm.Module);

    _suppressPopulate = true;
    inv.SetModulesBoth(invMods, weapMods);
    _suppressPopulate = false;
    DebugTrace.Log($"InventoryStack.HandleDrop -> SetModulesBoth inv={invMods.Count} weap={weapMods.Count}");
    // Defer UI repopulate to avoid re-entrant scene graph mutation during drop release
    if (!_populateQueued)
    {
      _populateQueued = true;
      CallDeferred(nameof(DeferredPopulate));
    }
  }

  private void ApplySharedLayout()
  {
    if (Layout == null)
      Layout = new StackLayoutConfig();
    // Apply shared values
    Offset = Layout.Offset;
    Padding = Layout.Padding;
    VerticalPadding = Layout.VerticalPadding;
    SlotPadding = Layout.SlotPadding;
    SlotNinePatchMargin = Layout.SlotNinePatchMargin;
    SlotNinePatchTexture = Layout.SlotNinePatchTexture;
  }

  // Accept an external drop from another framed stack
  public bool AcceptExternalDrop(Card2D card, Vector2 endGlobalMouse)
  {
    // if (card is not WeaponModuleCard2D) return false;
    // if (_slotsBox == null) return false;
    // int destIndex = ComputeNearestIndexLocal(endGlobalMouse - GetGlobalRect().Position);
    // destIndex = Mathf.Clamp(destIndex, 0, _slotsBox.GetChildCount() - 1);
    // if (DebugLogs)
    //   GD.Print($"[InventoryStack:{Name}] AcceptExternalDrop destIndex={destIndex}");
    // // Defer all reparent/adopt to HandleDrop->DeferredPopulate to avoid scene churn here
    // // HandleDrop(card, destIndex);
    // _lastPlaceholderIndex = -1;
    return true;
  }
}
