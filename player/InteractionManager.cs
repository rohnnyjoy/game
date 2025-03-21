using Godot;
using System;
using Godot.Collections;

public partial class InteractionManager : Node
{
  [Export] public float InteractRadius = 2.0f;
  [Export] public int MaxResults = 32;
  [Export] public NodePath PlayerPath;
  [Export] public NodePath CameraPath;


  private Player _player;
  private Camera3D _camera;

  public override void _Ready()
  {
    _player = GetNode<Player>(PlayerPath);
    _camera = GetNode<Camera3D>(CameraPath);
  }

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventKey keyEvent && !keyEvent.Echo && Input.IsActionJustPressed("interact"))
    {
      GD.Print("Interacting with: ", DetectInteractable());
      ProcessInteraction();
    }
  }


  public override void _PhysicsProcess(double delta)
  {
    DetectInteractable();
  }


  public IInteractable DetectInteractable()
  {
    if (_player == null)
    {
      GD.PrintErr("Player is not set!");
      return null;
    }

    Vector3 origin = _player.GlobalPosition;

    PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
    {
      Transform = new Transform3D(Basis.Identity, origin),
      Shape = new SphereShape3D { Radius = InteractRadius },
      CollideWithBodies = true
    };

    World3D world3d = _player.GetWorld3D();
    if (world3d == null)
    {
      GD.PrintErr("World3D not found!");
      return null;
    }

    PhysicsDirectSpaceState3D spaceState = world3d.DirectSpaceState;
    Array<Dictionary> results = (Array<Dictionary>)spaceState.IntersectShape(query, MaxResults);

    IInteractable bestInteractable = null;
    float bestDistance = float.MaxValue;

    foreach (Dictionary result in results)
    {
      Node colliderNode = result["collider"].As<Node>();
      if (colliderNode is Node3D collider && collider is IInteractable interactable)
      {
        float distance = origin.DistanceTo(collider.GlobalPosition);
        Vector3 rayOrigin = _camera != null ? _camera.GlobalPosition : origin;
        if (distance < bestDistance)
        {
          bestDistance = distance;
          bestInteractable = interactable;
        }
      }
    }

    GameUi.Instance.InteractionLabel.Visible = bestInteractable != null;
    if (bestInteractable != null)
    {
      GameUi.Instance.InteractionLabel.Text = bestInteractable.GetInteractionText();
      GameUi.Instance.InteractionLabel.QueueRedraw();
    }

    return bestInteractable;
  }

  public void ProcessInteraction()
  {
    IInteractable interactable = DetectInteractable();
    if (interactable != null)
    {
      GameUi.Instance.InteractionLabel.Text = interactable.GetInteractionText();
      GameUi.Instance.InteractionLabel.Visible = true;
      GameUi.Instance.InteractionLabel.QueueRedraw();
      interactable.OnInteract();
    }
    else
    {
      GameUi.Instance.InteractionLabel.Visible = false;
    }
  }
}
