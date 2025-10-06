using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class PrimaryWeaponStack : CardStack, IFramedCardStack
{
  private bool _suppressPopulate = false;
  private bool _populateQueued = false;
  private MarginContainer _content;
  private HBoxContainer _slotsBox;
  [Export]
  public StackLayoutConfig Layout { get; set; }
  [Export]
  public int SlotCount { get; set; } = 4; // Visible module slot containers

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

  public override void _Ready()
  {
    base._Ready();

    Inventory inventory = Player.Instance.Inventory;
    inventory.InventoryChanged += OnInventoryChanged;

    // Container-driven slot layout with inner content margin for Padding
    _content = new MarginContainer { Name = "Content" };
    AddChild(_content);
    _content.SetAnchorsPreset(LayoutPreset.FullRect);
    ApplySharedLayout();
    _content.AddThemeConstantOverride("margin_left", (int)Padding);
    _content.AddThemeConstantOverride("margin_right", (int)Padding);
    _content.AddThemeConstantOverride("margin_top", 0);
    _content.AddThemeConstantOverride("margin_bottom", 0);

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
      // External drag preview when mouse is over this stack
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
            PreviewArrangeExternal(_lastPlaceholderIndex);
          }
        }
        else if (_lastPlaceholderIndex != -1)
        {
          PopulateCards();
          _lastPlaceholderIndex = -1;
        }
      }
      else if (_lastPlaceholderIndex != -1)
      {
        PopulateCards();
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
      _content.AddThemeConstantOverride("margin_top", (int)Padding);
      _content.AddThemeConstantOverride("margin_bottom", (int)Padding);
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
    float height = Padding * 2 + cardSize.Y + 2.0f * (SlotPadding + SlotNinePatchMargin);
    CustomMinimumSize = new Vector2(width, height);
    if (_content != null)
    {
      _content.AddThemeConstantOverride("margin_left", (int)Padding);
      _content.AddThemeConstantOverride("margin_right", (int)Padding);
    }
  }

  private void PopulateCards()
  {
    var newCards = new Array<Card2D>();
    foreach (WeaponModule module in Player.Instance.Inventory.PrimaryWeapon.Modules)
    {
      var existingCard = findCard(module);
      if (existingCard == null)
      {
        WeaponModuleCard2D card = new WeaponModuleCard2D();
        card.Module = module;
        newCards.Add(card);
      }
      else
      {
        newCards.Add(existingCard);
      }
    }

    UpdateSlotBackgrounds();
    int idx = 0;
    foreach (Card2D c in newCards)
    {
      var frame = _slotsBox.GetChild(idx) as SlotFrame;
      frame?.AdoptCard(c);
      idx++;
    }
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
    _populateQueued = false;
    PopulateCards();
    // Refresh visuals once after data change; avoid per-frame churn.
    UpdateSlotBackgrounds();
    AutoSizeToFitSlots();
  }

  public override void OnCardsChanged(Array<Card2D> newCards)
  {
    base.OnCardsChanged(newCards);
    if (DebugLogs) GD.Print($"[PrimaryWeaponStack:{Name}] OnCardsChanged count={newCards.Count}");
    // Defensive: ignore empty lists (can occur from legacy CardStack fallback paths)
    // to avoid clearing the weapon modules when a drag is canceled or invalid.
    if (newCards == null || newCards.Count == 0)
    {
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
    UpdateSlotBackgrounds();
    int idx = 0;
    foreach (Card2D c in newCards)
    {
      var frame = _slotsBox.GetChild(idx) as SlotFrame;
      frame?.AdoptCard(c);
      idx++;
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
    inv.WeaponModules = invMods;
    weapon.Modules = weapMods;
    _suppressPopulate = false;
    PopulateCards();
    UpdateSlotBackgrounds();
  }

  // IFramedCardStack: Begin drag with placeholder behavior
  public void BeginCardDrag(Card2D card, SlotFrame fromFrame, Vector2 startGlobalMouse)
  {
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
      GD.Print($"[PrimaryWeaponStack:{Name}] BeginDrag card={card.Name} oldParent={(oldParent!=null?oldParent.GetPath():new NodePath("<null>")).ToString()} gp(before)={gp}");

    if (oldParent != null) oldParent.RemoveChild(card);

    Node uiLayer = GetDragLayer();
    uiLayer.AddChild(card);
    card.TopLevel = true;
    card.GlobalPosition = gp;
    // Keep on top within the canvas layer while dragging
    card.ZIndex = 4095;
    card.MoveToFront();
    card.RecomputeDragOffset();
    if (DebugLogs)
      GD.Print($"[PrimaryWeaponStack:{Name}] AfterReparent card={card.Name} gp(after)={card.GlobalPosition} toplevel={card.TopLevel} parent={card.GetParent().GetPath()}");

    _dragFrame.SetPlaceholder(true);
    PreviewArrange(_lastPlaceholderIndex);
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
    if (DebugLogs)
      GD.Print($"[PrimaryWeaponStack:{Name}] EndDrag dx={dx} origin={_originIndex} placeholder={_lastPlaceholderIndex} destIndex(computed)={destIndex}");

    if (!insideThis)
    {
      _dragFrame.SetPlaceholder(false);
      // Restore layout before exiting; Card2D handles outside drops
      PopulateCards();
      card.TopLevel = false;
      // Proactively re-emit modules state so info UI doesn't briefly clear
      var weapon = Player.Instance?.Inventory?.PrimaryWeapon;
      if (weapon != null)
      {
        weapon.Modules = weapon.Modules;
      }
      ClearDragState();
      return false;
    }

    bool cancel = dx < HorizontalThresholdPx && destIndex == _originIndex;
    if (cancel)
    {
      var frameRect = _dragFrame.GetGlobalRect();
      var to = frameRect.Position + frameRect.Size * 0.5f - card.CardCore.CardSize * 0.5f;
      var tween = card.CreateTween();
      tween.TweenProperty(card, "global_position", to, 0.18f)
           .SetTrans(Tween.TransitionType.Quad)
           .SetEase(Tween.EaseType.Out);
      tween.Finished += () => {
        card.TopLevel = false;
        _dragFrame.AdoptCard(card);
      };
      _dragFrame.SetPlaceholder(false);
      // Restore layout
      PopulateCards();
      ClearDragState();
      return true;
    }

    // Ensure the dragged card is attached to the destination slot to prevent any ghost overlay
    if (_slotsBox != null && destIndex >= 0 && destIndex < _slotsBox.GetChildCount())
    {
      if (_slotsBox.GetChild(destIndex) is SlotFrame destFrame)
      {
        card.TopLevel = false;
        destFrame.AdoptCard(card);
      }
    }

    HandleDrop(card, destIndex);
    _dragFrame.SetPlaceholder(false);
    ClearDragState();
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
    var current = GetAllCardsInFrames();
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
        if (k < current.Count)
        {
          frame.AdoptCard(current[k]);
          k++;
        }
        else
        {
          frame.ClearCard();
        }
      }
    }
  }

  private void ApplySharedLayout()
  {
    if (Layout == null)
      Layout = new StackLayoutConfig();
    Offset = Layout.Offset;
    Padding = Layout.Padding;
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
    if (_slotsBox.GetChild(destIndex) is SlotFrame destFrame)
    {
      card.TopLevel = false;
      destFrame.AdoptCard(card);
    }
    HandleDrop(card, destIndex);
    _lastPlaceholderIndex = -1;
    return true;
  }
}
