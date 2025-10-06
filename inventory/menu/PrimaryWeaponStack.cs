using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class PrimaryWeaponStack : CardStack, IFramedCardStack
{
  private readonly System.Collections.Generic.Dictionary<WeaponModule, WeaponModuleCard2D> _cardMap = new();
  private bool _suppressPopulate = false;
  private bool _populateQueued = false;
  private MarginContainer _content;
  private HBoxContainer _slotsBox;
  [Export]
  public StackLayoutConfig Layout { get; set; }
  [Export]
  public int SlotCount { get; set; } = 4; // Visible module slot containers

  // Independent vertical padding (top/bottom) inside the framed panel
  [Export]
  public float VerticalPadding { get; set; } = 12.0f; // default visual breathing room

  [Export]
  public Color SlotFillColor { get; set; } = new Color(0.95f, 0.95f, 0.95f, 0.35f);

  [Export]
  public Color SlotBorderColor { get; set; } = new Color(0.1f, 0.1f, 0.1f, 0.6f);

  [Export]
  public float SlotPadding { get; set; } = 6.0f; // Inner padding within each slot

  [Export]
  public Texture2D SlotNinePatchTexture { get; set; } = GD.Load<Texture2D>("res://assets/ui/3x/ninepatch.png");

  [Export]
  public int SlotNinePatchMargin { get; set; } = 18; // matches world theme margins

  private Control _slotLayer;
  // Drag/placeholder state
  private Card2D _dragCard;
  private SlotFrame _dragFrame;
  private int _originIndex;
  private int _lastPlaceholderIndex;
  private Vector2 _dragStartGlobal;
  [Export] public float HorizontalThresholdPx { get; set; } = 10f;
  private Array<Card2D> _dragSnapshot;
  private int _externalPreviewIndex = -1;


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

    // Make the container padding equal to the visual gap between slots once at startup.
    // gap = Offset - (cardWidth + 2*SlotPadding) (never negative) so outer padding matches inter-slot separation
    Vector2 cs0 = new Vector2(100, 100);
    var gcards = GetCards();
    if (gcards.Count > 0) cs0 = gcards[0].CardCore.CardSize;
    float desiredGap = Mathf.Max(0, Offset - (cs0.X + 2.0f * SlotPadding));
    if (!Mathf.IsEqualApprox(Padding, desiredGap))
    {
      Padding = desiredGap;
      UpdateCards(GetCards(), false);
    }
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

  public override void _Process(double delta)
  {
    // Update live preview during manual drag; leave frames fixed.
    if (_dragCard != null && _slotsBox != null && _dragFrame != null)
    {
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
        PreviewArrange(_lastPlaceholderIndex);
      }
    }
    else
    {
      // External preview fully disabled for stability; only clear any lingering placeholder.
      var dragging = Card2D.CurrentlyDragged;
      if ((!IsInstanceValid(dragging) || dragging == null) && _lastPlaceholderIndex != -1)
      {
        if (_externalPreviewIndex != -1 && _slotsBox != null && _slotsBox.GetChildCount() > _externalPreviewIndex && _slotsBox.GetChild(_externalPreviewIndex) is SlotFrame clearPrev)
        {
          clearPrev.SetPlaceholder(false);
        }
        _externalPreviewIndex = -1;
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
    int containers = Mathf.Max(SlotCount, currentCards.Count);

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

    if (DebugLogs && !_populateQueued)
    {
      GD.Print($"[PrimaryWeaponStack:{Name}] layout cardSize={cardSize} frame=({frameW},{frameH}) sep={separation} padding={Padding} slotPad={SlotPadding} patch={SlotNinePatchMargin} offset={Offset} containers={containers}");
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
    int containers = Mathf.Max(SlotCount, currentCards.Count);
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

  private void PopulateCards()
  {
    DebugTrace.Log($"PrimaryWeaponStack.PopulateCards start");
    var modules = Player.Instance.Inventory.PrimaryWeapon.Modules;

    // Remove stale
    var keys = new System.Collections.Generic.List<WeaponModule>(_cardMap.Keys);
    foreach (var m in keys)
    {
      if (!modules.Contains(m))
      {
        var card = _cardMap[m];
        var parent = card.GetParent();
        if (parent != null) parent.RemoveChild(card);
        card.QueueFree();
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
      frame?.AdoptCard(c);
      idx++;
    }
    DebugTrace.Log($"PrimaryWeaponStack.PopulateCards done count={ordered.Count}");
  }


  private WeaponModuleCard2D findCard(WeaponModule module)
  {
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
    DebugTrace.Log($"PrimaryWeaponStack.OnInventoryChanged");
    if (_suppressPopulate) return;
    if (DebugLogs) GD.Print($"[PrimaryWeaponStack:{Name}] InventoryChanged (queued)");
    // Defer populate to avoid re-entrant scene graph mutations during drops
    if (!_populateQueued)
    {
      _populateQueued = true;
      CallDeferred(nameof(DeferredPopulate));
    }
  }

  private void DeferredPopulate()
  {
    DebugTrace.Log($"PrimaryWeaponStack.DeferredPopulate");
    _populateQueued = false;
    PopulateCards();
    // Refresh visuals once after data change; avoid per-frame churn.
    UpdateSlotBackgrounds();
    AutoSizeToFitSlots();
  }

  public override void OnCardsChanged(Array<Card2D> newCards)
  {
    // Treat as model change only; UI repopulates deferred
    if (_dragCard != null) { DebugTrace.Log($"PrimaryWeaponStack.OnCardsChanged ignored (active drag)"); return; }
    if (DebugLogs) GD.Print($"[PrimaryWeaponStack:{Name}] OnCardsChanged count={newCards.Count}");
    // Defensive: ignore empty lists (can occur from legacy CardStack fallback paths)
    // to avoid clearing the weapon modules when a drag is canceled or invalid.
    if (newCards == null || newCards.Count == 0)
    {
      DebugTrace.Log($"PrimaryWeaponStack.OnCardsChanged ignored (empty)");
      return;
    }
    var newModules = new Array<WeaponModule>();
    foreach (Card2D card in newCards)
    {
      if (card is WeaponModuleCard2D moduleCard)
        newModules.Add(moduleCard.Module);
    }
    _suppressPopulate = true;
    // Update modules in-place; avoid reassigning the PrimaryWeapon property
    // to prevent redundant InventoryChanged emissions and re-entrant UI refresh.
    var weapon = Player.Instance.Inventory.PrimaryWeapon;
    if (weapon != null)
    {
      weapon.Modules = newModules;
    }
    _suppressPopulate = false;
    DebugTrace.Log($"PrimaryWeaponStack.OnCardsChanged committed modules={newModules.Count}");
    if (!_populateQueued)
    {
      _populateQueued = true;
      CallDeferred(nameof(DeferredPopulate));
    }
  }

  public void HandleDrop(Card2D card, int destIndex)
  {
    if (card is not WeaponModuleCard2D wm) return;
    var inv = Player.Instance.Inventory;
    var invMods = new Array<WeaponModule>(inv.WeaponModules);
    var weapon = inv.PrimaryWeapon;
    var weapMods = new Array<WeaponModule>(weapon.Modules);

    invMods.Remove(wm.Module);
    weapMods.Remove(wm.Module);

    destIndex = Mathf.Clamp(destIndex, 0, weapMods.Count);
    weapMods.Insert(destIndex, wm.Module);

    _suppressPopulate = true;
    inv.SetModulesBoth(invMods, weapMods);
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
    DebugTrace.Log($"PrimaryWeaponStack.BeginCardDrag card={card.Name} frame={fromFrame?.Name}");
    _dragCard = card;
    _dragFrame = fromFrame;
    _originIndex = _dragFrame.GetIndex();
    _lastPlaceholderIndex = _originIndex;
    _dragStartGlobal = startGlobalMouse;

    _dragSnapshot = GetAllCardsInFrames();

    // Preserve global position before detaching to avoid jumps
    var gp = card.GlobalPosition;
    var oldParent = card.GetParent();
    if (DebugLogs)
      GD.Print($"[PrimaryWeaponStack:{Name}] BeginDrag card={card.Name} oldParent={(oldParent != null ? oldParent.GetPath() : new NodePath("<null>")).ToString()} gp(before)={gp}");

    // Do not reparent during drag; just switch to top-level for global coordinates
    card.TopLevel = true;
    card.GlobalPosition = gp;
    // Keep on top within the canvas layer while dragging
    card.ZIndex = 4095;
    card.MoveToFront();
    card.RecomputeDragOffset();
    if (DebugLogs)
      GD.Print($"[PrimaryWeaponStack:{Name}] AfterBeginDrag card={card.Name} gp(after)={card.GlobalPosition} toplevel={card.TopLevel} parent={card.GetParent().GetPath()}");

    _dragFrame.SetPlaceholder(true);
    PreviewArrange(_lastPlaceholderIndex);
    DebugTrace.Log($"PrimaryWeaponStack.BeginCardDrag placeholder set + preview idx={_lastPlaceholderIndex}");
  }

  private Node GetDragLayer()
  {
    // Prefer the nearest CanvasLayer so we stay above menu UI during drags
    Node n = this;
    while (n != null)
    {
      if (n is CanvasLayer)
        return n;
      n = n.GetParent();
    }
    return GetTree().Root;
  }

  // IFramedCardStack: End drag, decide reorder or cancel
  public bool EndCardDrag(Card2D card, Vector2 endGlobalMouse)
  {
    // DebugTrace.Log($"PrimaryWeaponStack.EndCardDrag card={card.Name}");
    // if (_dragCard != card || _dragFrame == null)
    // {
    //   return false;
    // }

    // bool insideThis = GetGlobalRect().HasPoint(endGlobalMouse);
    // float dx = Mathf.Abs(endGlobalMouse.X - _dragStartGlobal.X);
    // // Recompute target index at drop time to avoid stale placeholder index
    // Vector2 endLocal = endGlobalMouse - GetGlobalRect().Position;
    // int destIndex = ComputeNearestIndexLocal(endLocal);
    // destIndex = Mathf.Clamp(destIndex, 0, _slotsBox.GetChildCount() - 1);
    // GD.Print($"[PrimaryWeaponStack:{Name}] EndDrag dx={dx} origin={_originIndex} placeholder={_lastPlaceholderIndex} destIndex(computed)={destIndex} inside={insideThis}");

    // if (!insideThis)
    // {
    //   DoSilently(() => _dragFrame.SetPlaceholder(false)); DebugTrace.Log($"PrimaryWeaponStack.EndCardDrag outside -> placeholder off");
    //   // Restore layout before exiting; Card2D handles outside drops
    //   PopulateCards();
    //   card.TopLevel = false;
    //   // Proactively re-emit modules state so info UI doesn't briefly clear
    //   var weapon = Player.Instance?.Inventory?.PrimaryWeapon;
    //   if (weapon != null)
    //   {
    //     weapon.Modules = weapon.Modules;
    //   }
    //   ClearDragState();
    //   return false;
    // }

    // bool cancel = dx < HorizontalThresholdPx && destIndex == _originIndex;
    // if (cancel)
    // {
    //   var frameRect = _dragFrame.GetGlobalRect();
    //   var to = frameRect.Position + frameRect.Size * 0.5f - card.CardCore.CardSize * 0.5f;
    //   var tween = card.CreateTween();
    //   tween.TweenProperty(card, "global_position", to, 0.18f)
    //        .SetTrans(Tween.TransitionType.Quad)
    //        .SetEase(Tween.EaseType.Out);
    //   tween.Finished += () => {
    //     card.TopLevel = false;
    //     DoSilently(() => _dragFrame.AdoptCard(card));
    //     DebugTrace.Log($"PrimaryWeaponStack.EndCardDrag cancel -> adopted back");
    //   };
    //   DoSilently(() => _dragFrame.SetPlaceholder(false)); DebugTrace.Log($"PrimaryWeaponStack.EndCardDrag cancel -> placeholder off");
    //   // Restore layout
    //   PopulateCards();
    //   ClearDragState();
    //   return true;
    // }

    // // Avoid reparenting/adopting here while still in release handling; defer to data update + repopulate
    // card.TopLevel = false;

    // HandleDrop(card, destIndex);
    // DoSilently(() => _dragFrame.SetPlaceholder(false)); DebugTrace.Log($"PrimaryWeaponStack.EndCardDrag commit -> placeholder off");
    // ClearDragState();
    return true;
  }

  private void ClearDragState()
  {
    _dragCard = null;
    _dragFrame = null;
    _lastPlaceholderIndex = -1;
  }

  private int ComputeNearestIndexLocal(Vector2 localMouse)
  {
    if (_slotsBox == null || _slotsBox.GetChildCount() == 0)
      return 0;
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
    if (_slotsBox == null || _dragSnapshot == null) return;
    DebugTrace.Log($"PrimaryWeaponStack.PreviewArrange idx={placeholderIndex} snapshot={_dragSnapshot.Count}");
    var others = new Array<Card2D>();
    foreach (var c in _dragSnapshot)
    {
      if (c != _dragCard) others.Add(c);
    }
    int k = 0;
    int frameCount = _slotsBox.GetChildCount();
    for (int i = 0; i < frameCount; i++)
    {
      if (_slotsBox.GetChild(i) is SlotFrame frame)
      {
        if (i == placeholderIndex)
        {
          frame.ClearCard();
          continue;
        }
        if (k < others.Count)
        {
          frame.AdoptCard(others[k]);
          k++;
        }
        else
        {
          frame.ClearCard();
        }
      }
    }
  }

  private void PreviewArrangeExternal(int placeholderIndex)
  {
    if (_slotsBox == null) return;
    DebugTrace.Log($"PrimaryWeaponStack.PreviewArrangeExternal idx={placeholderIndex}");
    // External preview disabled for stability: record index only.
    _externalPreviewIndex = placeholderIndex;
  }

  private void ApplySharedLayout()
  {
    if (Layout == null)
      Layout = new StackLayoutConfig();
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
    if (card is not WeaponModuleCard2D) return false;
    if (_slotsBox == null) return false;
    int destIndex = ComputeNearestIndexLocal(endGlobalMouse - GetGlobalRect().Position);
    destIndex = Mathf.Clamp(destIndex, 0, _slotsBox.GetChildCount() - 1);
    if (DebugLogs)
      GD.Print($"[PrimaryWeaponStack:{Name}] AcceptExternalDrop destIndex={destIndex}");
    // Defer all reparent/adopt to HandleDrop->PopulateCards to avoid scene churn here
    HandleDrop(card, destIndex);
    _lastPlaceholderIndex = -1;
    return true;
  }
}
