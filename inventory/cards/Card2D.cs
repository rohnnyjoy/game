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

  // Internal variables.
  protected bool _pickedUp = false;
  private Vector2 _offset = Vector2.Zero;
  private Vector2 _lastPos;
  private float _targetRotation = 0.0f;

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

    if (CardCore.CardTexture != null)
    {
      var styleBox = new StyleBoxTexture();
      styleBox.Texture = CardCore.CardTexture;
      AddThemeStyleboxOverride("normal", styleBox);
      AddThemeStyleboxOverride("pressed", styleBox);
      AddThemeStyleboxOverride("hover", styleBox);
    }
    else
    {
      var styleBox = new StyleBoxFlat();
      styleBox.BgColor = CardCore.CardColor;
      AddThemeStyleboxOverride("normal", styleBox);
      AddThemeStyleboxOverride("pressed", styleBox);
      AddThemeStyleboxOverride("hover", styleBox);
    }

    MouseEntered += OnMouseEntered;
    MouseExited += OnMouseExited;
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;
    if (_pickedUp)
    {
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
        Vector2 targetPos = GetGlobalMousePosition() + _offset;
        GlobalPosition = GlobalPosition.Lerp(targetPos, DragSpeed);
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
  }

  protected async void OnDragEnd()
  {
    GD.Print("Card dropped.");
    _pickedUp = false;
    ZIndex = 0;
    Card2D.CurrentlyDragged = null;

    Node targetStack = null;
    foreach (Node stack in GetTree().GetNodesInGroup("CardStacks"))
    {
      if (stack is Control control && control.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
      {
        targetStack = stack;
        break;
      }
    }

    if (targetStack != null)
    {
      if (targetStack is CardStack newStack)
      {
        Vector2 targetGlobalPos = GetGlobalMousePosition() + _offset;
        Vector2 dropLocalPos = targetGlobalPos - newStack.GetGlobalRect().Position;
        newStack.OnCardDrop(this, dropLocalPos);
      }
    }
    else
    {
      if (GetParent() is CardStack parentStack)
        OnDroppedOutsideStacks();
    }

    await ToSignal(GetTree().CreateTimer(0.05f), "timeout");
    ResetScale();
    if (GetGlobalRect().HasPoint(GetGlobalMousePosition()))
      OnMouseEntered();
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
      TooltipText = CardCore.CardDescription;
    Tween tween = CreateTween().SetEase(Tween.EaseType.Out)
                                 .SetTrans(Tween.TransitionType.Elastic);
    tween.TweenProperty(this, "scale", new Vector2(1.2f, 1.2f), 0.5f);
  }

  private void OnMouseExited()
  {
    if (_pickedUp)
      return;
    TooltipText = "";
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
}
