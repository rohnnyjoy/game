using Godot;
using System;
using System.Threading.Tasks;

public partial class Card2D : Button
{
  // A resource holding common card properties.
  [Export]
  public CardCore CardCore { get; set; }

  // 2D-specific exported properties.
  [Export]
  public float RotationSensitivity { get; set; } = 1.0f;
  [Export]
  public float RotationLerpFactor { get; set; } = 0.5f;
  [Export]
  public float RotationFollowSpeed { get; set; } = 10.0f;
  [Export]
  public float ReturnSpeed { get; set; } = 3.0f;
  [Export]
  public float MaxAngle { get; set; } = 15.0f;
  [Export]
  public float DragSpeed { get; set; } = 0.3f;
  [Export]
  public bool UseDnD { get; set; } = true;

  // Internal variables.
  protected bool _pickedUp = false;
  private Vector2 _offset = Vector2.Zero;
  private Vector2 _lastPos;
  private float _targetRotation = 0.0f;
  private IFramedCardStack _framedOwner; // Optional: owning framed stack managing placeholder logic
  private Vector2 _localDragOffset = Vector2.Zero;
  private bool _hasLocalDragOffset = false;

  // Oscillator variables.
  private float _oscillatorVelocity = 0.0f;
  private float _displacement = 0.0f;

  [Signal]
  public delegate void DropEventHandler();

  // Static reference to the currently dragged card.
  public static Card2D CurrentlyDragged;

  public bool IsDragged => _pickedUp;

  // Expose the drag offset for use during drop
  public Vector2 DragOffset => _offset;

  public override void _Ready()
  {
    FocusMode = FocusModeEnum.None;

    if (CardCore == null)
      CardCore = new CardCore();

    CustomMinimumSize = CardCore.CardSize;
    PivotOffset = CardCore.CardSize * 0.5f;
    MouseFilter = MouseFilterEnum.Stop;
    _lastPos = Position;

    // Make the card background fully transparent for all states.
    var empty = new StyleBoxEmpty();
    AddThemeStyleboxOverride("normal", empty);
    AddThemeStyleboxOverride("pressed", empty);
    AddThemeStyleboxOverride("hover", empty);

    // If a texture is provided, render it via a TextureRect to preserve transparency.
    // Standardize drawing so icons are centered with even padding across stacks.
    // Always center and keep aspect; rely on SlotFrame.InnerPadding for the frame gap.
    if (CardCore.CardTexture != null)
    {
      var icon = new TextureRect();
      icon.Name = "CardIcon";
      icon.Texture = CardCore.CardTexture;
      icon.MouseFilter = MouseFilterEnum.Ignore;
      icon.SetAnchorsPreset(LayoutPreset.FullRect);
      icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
      AddChild(icon);
    }

    MouseEntered += OnMouseEntered;
    MouseExited += OnMouseExited;
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;
    // Safety: if dragging but the left mouse button is no longer pressed (release consumed by other UI),
    // finalize the drag here to avoid getting stuck in a "picked up" state.
    if (_pickedUp && !Input.IsMouseButtonPressed(MouseButton.Left))
    {
      GD.Print($"[Card2D:{Name}] _Process auto-finalize drag (mouse released)");
      OnDragEnd();
      // Continue with non-picked-up branch below
    }
    if (_pickedUp)
    {
      // Ensure dragged card follows the cursor even if GUI input routing changes due to reparenting.
      if (TopLevel)
      {
        Vector2 targetPos = GetGlobalMousePosition() + _offset;
        GlobalPosition = GlobalPosition.Lerp(targetPos, DragSpeed);
      }
      else if (_hasLocalDragOffset && GetParent() is Control pc)
      {
        Vector2 targetPosLocal = pc.GetLocalMousePosition() + _localDragOffset;
        Position = Position.Lerp(targetPosLocal, DragSpeed);
      }
      else
      {
        Vector2 targetPos = GetGlobalMousePosition() + _offset;
        GlobalPosition = GlobalPosition.Lerp(targetPos, DragSpeed);
      }
      UpdateOscillator(dt);
      RotationDegrees = Mathf.Lerp(RotationDegrees, _displacement, RotationFollowSpeed * dt);
    }
    else
    {
      _targetRotation = Mathf.Lerp(_targetRotation, 0.0f, ReturnSpeed * dt);
      RotationDegrees = Mathf.Lerp(RotationDegrees, _targetRotation, ReturnSpeed * dt);
    }
  }

  private void UpdateOscillator(float delta)
  {
    Vector2 vel = (Position - _lastPos) / delta;
    _lastPos = Position;
    _oscillatorVelocity += vel.Normalized().X;
    float spring = 150.0f;
    float damp = 10.0f;
    float force = -spring * _displacement - damp * _oscillatorVelocity;
    _oscillatorVelocity += force * delta;
    _displacement += _oscillatorVelocity * delta;
  }

  public override void _GuiInput(InputEvent @event)
  {
    if (UseDnD)
    {
      // With DnD enabled, let Godot handle drag; keep hover rotation only
      if (@event is InputEventMouseMotion mouseMotion)
      {
        Vector2 localMouse = GetLocalMousePosition();
        _targetRotation = Remap(localMouse.X, 0.0f, CardCore.CardSize.X, -MaxAngle, MaxAngle);
        _targetRotation = Mathf.Lerp(_targetRotation, mouseMotion.Relative.X * RotationSensitivity, RotationLerpFactor);
        _targetRotation = Mathf.Clamp(_targetRotation, -MaxAngle, MaxAngle);
      }
      return;
    }

    if (@event is InputEventMouseButton mouseButton)
    {
      if (mouseButton.ButtonIndex == MouseButton.Left)
      {
        if (mouseButton.Pressed)
          OnDragStart();
        else
          OnDragEnd();
      }
    }
    else if (@event is InputEventMouseMotion mouseMotion)
    {
      if (_pickedUp)
      {
        if (TopLevel)
        {
          Vector2 targetPos = GetGlobalMousePosition() + _offset;
          GlobalPosition = GlobalPosition.Lerp(targetPos, DragSpeed);
        }
        else if (_hasLocalDragOffset && GetParent() is Control pc)
        {
          Vector2 targetPosLocal = pc.GetLocalMousePosition() + _localDragOffset;
          Position = Position.Lerp(targetPosLocal, DragSpeed);
        }
        else
        {
          Vector2 targetPos = GetGlobalMousePosition() + _offset;
          GlobalPosition = GlobalPosition.Lerp(targetPos, DragSpeed);
        }
      }
      else
      {
        Vector2 localMouse = GetLocalMousePosition();
        _targetRotation = Remap(localMouse.X, 0.0f, CardCore.CardSize.X, -MaxAngle, MaxAngle);
        _targetRotation = Mathf.Lerp(_targetRotation, mouseMotion.Relative.X * RotationSensitivity, RotationLerpFactor);
        _targetRotation = Mathf.Clamp(_targetRotation, -MaxAngle, MaxAngle);
      }
    }
  }

  private void OnDragStart()
  {
    _offset = GlobalPosition - GetGlobalMousePosition();
    _pickedUp = true;
    Card2D.CurrentlyDragged = this;
    _oscillatorVelocity = 0.0f;
    _displacement = 0.0f;
    _lastPos = Position;
    ZIndex = 999;
    MoveToFront();
    Tween tween = CreateTween();
    tween.TweenProperty(this, "scale", new Vector2(1.1f, 1.1f), 0.1f);
    TooltipText = "";
    if (GameUI.Instance != null)
      GameUI.Instance.HideTooltip();

    // Notify containing framed stack (if any) to begin placeholder-based drag handling.
    _framedOwner = FindFramedOwner();
    if (_framedOwner != null)
    {
      var fromFrame = FindParentSlotFrame();
      if (fromFrame != null)
      {
        _framedOwner.BeginCardDrag(this, fromFrame, GetGlobalMousePosition());
        // After the stack has reparented/top-leveled us, recompute drag offset
        RecomputeDragOffset();
      }
    }
  }

  protected void OnDragEnd()
  {
    if (!_pickedUp)
      return;
    GD.Print($"[Card2D:{Name}] OnDragEnd at={GetGlobalMousePosition()} parent={(GetParent()!=null?GetParent().GetPath():new NodePath("<null>")).ToString()} topLevel={TopLevel}");
    _pickedUp = false;
    ZIndex = 0;
    Card2D.CurrentlyDragged = null;

    // If a framed owner is managing this drag, let it finalize the drop logic.
    bool handledByFramed = false;
    if (_framedOwner != null)
    {
      handledByFramed = _framedOwner.EndCardDrag(this, GetGlobalMousePosition());
      GD.Print($"[Card2D:{Name}] EndCardDrag by framedOwner={_framedOwner.GetType().Name} handled={handledByFramed}");
      _framedOwner = null;
    }
    if (!handledByFramed)
    {
      // Try framed stacks first for cross-stack drops
      Node framedTarget = null;
      foreach (Node stack in GetTree().GetNodesInGroup("CardStacks"))
      {
        if (stack is Control control && control.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
        {
          framedTarget = stack;
          break;
        }
      }

      bool framedHandled = false;
      if (framedTarget is IFramedCardStack fstack)
      {
        GD.Print($"[Card2D:{Name}] AcceptExternalDrop on target={framedTarget.GetType().Name}");
        framedHandled = fstack.AcceptExternalDrop(this, GetGlobalMousePosition());
        GD.Print($"[Card2D:{Name}] AcceptExternalDrop handled={framedHandled}");
      }

      if (!framedHandled)
      {
        // If a framed stack has already restored/adopted this card during its
        // EndCardDrag (e.g., cancel/snap-back), avoid treating this as an outside
        // drop. In that case, we are already back under a framed owner and should
        // not convert to 3D.
        if (FindFramedOwner() is IFramedCardStack)
        {
          return;
        }

        // Fallback: legacy drop scanning for plain CardStack parents.
        if (framedTarget is CardStack newStack)
        {
          GD.Print($"[Card2D:{Name}] Legacy OnCardDrop to stack={newStack.Name}");
          Vector2 targetGlobalPos = GetGlobalMousePosition() + _offset;
          Vector2 dropLocalPos = targetGlobalPos - newStack.GetGlobalRect().Position;
          newStack.CallDeferred("OnCardDrop", this, dropLocalPos);
        }
        else if (GetParent() is CardStack parentStack)
        {
          GD.Print($"[Card2D:{Name}] Dropped outside stacks; calling OnDroppedOutsideStacks");
          OnDroppedOutsideStacks();
        }
      }
    }

    // Return to normal hierarchy mode after drag completes
    TopLevel = false;
    GD.Print($"[Card2D:{Name}] DragEnd complete; topLevel reset");

    ResetScale();
    if (GetGlobalRect().HasPoint(GetGlobalMousePosition()))
      OnMouseEntered();
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    // Safety net: if the release doesn't reach _GuiInput due to reparenting/top-level changes,
    // capture it here and end the drag cleanly.
    if (@event is InputEventMouseButton mouseButton)
    {
      if (mouseButton.ButtonIndex == MouseButton.Left && !mouseButton.Pressed && _pickedUp)
      {
        OnDragEnd();
      }
    }
  }

  // Global safety net: capture releases even if GUI consumes the event before it reaches _UnhandledInput
  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventMouseButton mouseButton)
    {
      if (mouseButton.ButtonIndex == MouseButton.Left && !mouseButton.Pressed && _pickedUp)
      {
        GD.Print($"[Card2D:{Name}] _Input detected release while dragging; finalizing");
        OnDragEnd();
      }
    }
  }

  protected virtual void OnDroppedOutsideStacks() { }

  protected void ResetScale()
  {
    Tween tween = CreateTween();
    tween.TweenProperty(this, "scale", Vector2.One, 0.3f);
    tween.TweenProperty(this, "rotation_degrees", 0.0f, 0.3f);
  }

  protected void OnMouseEntered()
  {
    if (!_pickedUp)
    {
      // Show custom tooltip immediately on hover, anchored to this control
      if (GameUI.Instance != null)
        GameUI.Instance.ShowTooltip(this, CardCore.CardDescription);
    }
    Tween tween = CreateTween().SetEase(Tween.EaseType.Out)
                                 .SetTrans(Tween.TransitionType.Elastic);
    tween.TweenProperty(this, "scale", new Vector2(1.2f, 1.2f), 0.5f);
  }

  private void OnMouseExited()
  {
    if (_pickedUp)
      return;
    if (GameUI.Instance != null)
      GameUI.Instance.HideTooltip();
    Tween tween = CreateTween().SetEase(Tween.EaseType.Out)
                                 .SetTrans(Tween.TransitionType.Back)
                                 .SetParallel(true);
    tween.TweenProperty(this, "rotation_degrees", 0.0f, 0.5f);
    tween = CreateTween().SetEase(Tween.EaseType.Out)
                          .SetTrans(Tween.TransitionType.Elastic);
    tween.TweenProperty(this, "scale", Vector2.One, 0.55f);
  }

  private float Remap(float value, float from1, float to1, float from2, float to2)
  {
    return from2 + (value - from1) * (to2 - from2) / (to1 - from1);
  }

  public override Variant _GetDragData(Vector2 atPosition)
  {
    if (!UseDnD) return new Variant();

    var data = new Godot.Collections.Dictionary
    {
      { "card", this }
    };

    // Simple preview using the card's texture if present
    Control preview = new Control();
    preview.CustomMinimumSize = CardCore != null ? CardCore.CardSize : new Vector2(100, 100);
    if (CardCore?.CardTexture != null)
    {
      var tex = new TextureRect
      {
        Texture = CardCore.CardTexture,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
      };
      tex.SetAnchorsPreset(LayoutPreset.FullRect);
      preview.AddChild(tex);
    }
    SetDragPreview(preview);
    return data;
  }

  private IFramedCardStack FindFramedOwner()
  {
    Node n = this;
    while (n != null)
    {
      if (n is IFramedCardStack fcs)
        return fcs;
      n = n.GetParent();
    }
    return null;
  }

  private SlotFrame FindParentSlotFrame()
  {
    Node n = this;
    while (n != null)
    {
      if (n is SlotFrame sf)
        return sf;
      n = n.GetParent();
    }
    return null;
  }

  public void RecomputeDragOffset()
  {
    _offset = GlobalPosition - GetGlobalMousePosition();
    _hasLocalDragOffset = false;
    if (!TopLevel && GetParent() is Control pc)
    {
      _localDragOffset = Position - pc.GetLocalMousePosition();
      _hasLocalDragOffset = true;
    }
  }
}
