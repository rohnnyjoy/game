using Godot;
using System;
using Godot.Collections;

public partial class InteractionManager : Node3D
{
  [Export]
  public float InteractRadius = 2.0f;

  [Export]
  public int MaxResults = 32;

  /// <summary>
  /// Returns the best candidate interactable (if any) near the player.
  /// Uses Player.Instance for the origin and camera.
  /// </summary>
  public IInteractable DetectInteractable()
  {
    if (Player.Instance == null)
    {
      GD.PrintErr("Player.Instance is not set!");
      return null;
    }

    // Use the player's global position.
    Vector3 origin = Player.Instance.GlobalPosition;

    // Set up a sphere query centered at the player's position.
    PhysicsShapeQueryParameters3D query = new PhysicsShapeQueryParameters3D
    {
      Transform = new Transform3D(Basis.Identity, origin),
      Shape = new SphereShape3D { Radius = InteractRadius },
      CollideWithBodies = true
    };

    World3D world3d = Player.Instance.GetWorld3D();
    if (world3d == null)
    {
      GD.PrintErr("World3D not found!");
      return null;
    }



    PhysicsDirectSpaceState3D spaceState = world3d.DirectSpaceState;
    // IntersectShape returns an Array<Dictionary>
    Array<Dictionary> results = (Array<Dictionary>)spaceState.IntersectShape(query, MaxResults);

    IInteractable bestInteractable = null;
    float bestDistance = float.MaxValue;

    foreach (Dictionary result in results)
    {
      // Use the Variant extension method As<T>() to convert to Node.
      Node colliderNode = result["collider"].As<Node>();
      if (colliderNode != null)
      {
        Node3D collider = colliderNode as Node3D;
        if (collider != null && collider is IInteractable interactable)
        {
          Vector3 interactablePos = collider.GlobalPosition;
          float distance = origin.DistanceTo(interactablePos);

          // Use the player's camera if available.
          Camera3D camera = Player.Instance.CameraPivot.GetNode<Camera3D>("Camera");
          Vector3 rayOrigin = camera != null ? camera.GlobalPosition : origin;

          // Check for clear line-of-sight.
          if (distance < bestDistance)
          {
            bestDistance = distance;
            bestInteractable = interactable;
          }
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

  /// <summary>
  /// When called (e.g. from the Player), this method checks for an interactable
  /// and if the interact key is pressed, invokes its OnInteract().
  /// </summary>
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
