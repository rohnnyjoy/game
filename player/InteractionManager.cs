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
      return null;

    Vector3 origin = _player.GlobalPosition;

    PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
    {
      Transform = new Transform3D(Basis.Identity, origin),
      Shape = new SphereShape3D { Radius = InteractRadius },
      CollideWithBodies = true
    };

    World3D world3d = _player.GetWorld3D();
    if (world3d == null)
      return null;

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

    // UI may not be ready yet at startup; guard the singleton.
    if (GameUI.Instance != null)
    {
      if (bestInteractable != null)
        GameUI.Instance.ShowInteractionText(bestInteractable.GetInteractionText());
      else
        GameUI.Instance.HideInteractionText();
    }

    return bestInteractable;
  }

  public void ProcessInteraction()
  {
    IInteractable interactable = DetectInteractable();
    if (interactable != null)
    {
      if (GameUI.Instance != null)
        GameUI.Instance.ShowInteractionText(interactable.GetInteractionText());
      interactable.OnInteract();
    }
    else
    {
      if (GameUI.Instance != null)
        GameUI.Instance.HideInteractionText();
    }
  }
}
