using Godot;
using Godot.Collections;

public partial class SlotFrame : PanelContainer
{
  [Export] public Texture2D FrameTexture { get; set; } = GD.Load<Texture2D>("res://assets/ui/3x/ninepatch.png");
  [Export] public int PatchMargin { get; set; } = 18;
  [Export] public float InnerPadding { get; set; } = 6.0f;

  private MarginContainer _innerMargin;
  private CenterContainer _center;
  private bool _isPlaceholder = false;

  public override void _Ready()
  {
    // Apply stylebox as panel so PanelContainer gives us proper content margins
    var sb = new StyleBoxTexture
    {
      Texture = FrameTexture,
      AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
      AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
    };
    sb.TextureMarginLeft = PatchMargin;
    sb.TextureMarginTop = PatchMargin;
    sb.TextureMarginRight = PatchMargin;
    sb.TextureMarginBottom = PatchMargin;
    AddThemeStyleboxOverride("panel", sb);

    _innerMargin = new MarginContainer();
    _innerMargin.AddThemeConstantOverride("margin_left", (int)InnerPadding);
    _innerMargin.AddThemeConstantOverride("margin_top", (int)InnerPadding);
    _innerMargin.AddThemeConstantOverride("margin_right", (int)InnerPadding);
    _innerMargin.AddThemeConstantOverride("margin_bottom", (int)InnerPadding);
    AddChild(_innerMargin);
    _innerMargin.SetAnchorsPreset(LayoutPreset.FullRect);

    _center = new CenterContainer();
    _innerMargin.AddChild(_center);
    _center.SetAnchorsPreset(LayoutPreset.FullRect);
  }

  public void AdoptCard(Card2D card)
  {
    if (card == null) return;
    var current = GetCard();
    if (current == card)
    {
      // Already adopted; just normalize transforms
      card.Position = Vector2.Zero;
      card.RotationDegrees = 0f;
      card.Scale = Vector2.One;
      DebugTrace.Log($"SlotFrame.{Name}.AdoptCard (noop) card={card.Name}");
      return;
    }
    var parentBefore = card.GetParent();
    DebugTrace.Log($"SlotFrame.{Name}.AdoptCard BEGIN card={card.Name} parentBefore={(parentBefore!=null?parentBefore.GetPath():new NodePath("<null>")).ToString()} center={_center.GetPath()}");
    // Ensure only this card is present in the frame.
    if (card.GetParent() == _center)
    {
      // Remove any other Card2D children that might have accumulated
      foreach (Node child in _center.GetChildren())
      {
        if (child is Card2D c && c != card)
        {
          DebugTrace.Log($"SlotFrame.{Name}.AdoptCard remove-other child={c.Name}");
          _center.RemoveChild(c);
        }
      }
    }
    else
    {
      // Clear all existing Card2D children from this slot, then adopt the new card
      foreach (Node child in _center.GetChildren())
      {
        if (child is Card2D c)
        {
          DebugTrace.Log($"SlotFrame.{Name}.AdoptCard clear child={c.Name}");
          _center.RemoveChild(c);
        }
      }
      var parent = card.GetParent();
      if (parent != null)
      {
        DebugTrace.Log($"SlotFrame.{Name}.AdoptCard reparent from={(parent!=null?parent.GetPath():new NodePath("<null>")).ToString()} to={_center.GetPath()}");
        // Single atomic operation avoids remove/add re-entrancy.
        card.Reparent(_center);
        DebugTrace.Log($"SlotFrame.{Name}.AdoptCard reparented");
      }
      else
      {
        DebugTrace.Log($"SlotFrame.{Name}.AdoptCard adding orphan to center={_center.GetPath()}");
        _center.AddChild(card);
        DebugTrace.Log($"SlotFrame.{Name}.AdoptCard added orphan to center");
      }
    }
    // Let containers handle placement; reset manual transform bits used while dragging
    card.Position = Vector2.Zero;
    card.RotationDegrees = 0f;
    card.Scale = Vector2.One;
    DebugTrace.Log($"SlotFrame.{Name}.AdoptCard END card={card.Name}");
  }

  public Card2D GetCard()
  {
    foreach (Node child in _center.GetChildren())
    {
      if (child is Card2D c) return c;
    }
    return null;
  }

  public void SetPlaceholder(bool enabled)
  {
    _isPlaceholder = enabled;
    DebugTrace.Log($"SlotFrame.{Name}.SetPlaceholder {enabled}");
    // Visuals remain unchanged (conceptual placeholder only)
  }

  public bool IsPlaceholder() => _isPlaceholder;

  public void ClearCard()
  {
    // Remove all Card2D children from this frame
    if (GetCard() == null) return;
    DebugTrace.Log($"SlotFrame.{Name}.ClearCard");
    foreach (Node child in _center.GetChildren())
    {
      if (child is Card2D c)
      {
        var p = c.GetParent();
        if (p != null) p.RemoveChild(c);
      }
    }
  }

  public override bool _CanDropData(Vector2 atPosition, Variant data)
  {
    if (data.VariantType != Variant.Type.Dictionary)
      return false;
    var dict = (Dictionary)data;
    if (!dict.ContainsKey("card")) return false;
    Variant v = (Variant)dict["card"];
    var obj = v.AsGodotObject();
    return obj is Card2D;
  }

  public override void _DropData(Vector2 atPosition, Variant data)
  {
    if (!_CanDropData(atPosition, data)) return;
    var dict = (Dictionary)data;
    Variant v = (Variant)dict["card"];
    var obj = v.AsGodotObject();
    var card = obj as Card2D;
    if (card == null) return;
    GD.Print($"[SlotFrame:{Name}] _DropData at={atPosition} card={card?.Name}");

    // Find owning stack (InventoryStack or PrimaryWeaponStack)
    Node n = this;
    while (n != null && n is not InventoryStack && n is not PrimaryWeaponStack)
      n = n.GetParent();
    if (n == null) return;

    int index = GetIndex(); // index within HBoxContainer
    if (n is InventoryStack inv)
    {
      GD.Print($"[SlotFrame:{Name}] routing drop to InventoryStack index={index}");
      inv.HandleDrop(card, index);
    }
    else if (n is PrimaryWeaponStack pw)
    {
      GD.Print($"[SlotFrame:{Name}] routing drop to PrimaryWeaponStack index={index}");
      pw.HandleDrop(card, index);
    }
  }
}
