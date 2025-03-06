using Godot;
using System;
using System.Linq;

public partial class WeaponModuleCard2D : Card2D
{
  [Export]
  public WeaponModule Module { get; set; }

  public override void _Ready()
  {
    CardCore = new CardCore();
    CardCore.CardTexture = Module.CardTexture;
    CardCore.CardDescription = Module.ModuleDescription;
    GD.Print(Module.CardTexture);
    base._Ready();
  }

  protected override void OnDroppedOutsideStacks()
  {
    ConvertTo3D();
  }

  private void ConvertTo3D()
  {
    // Create an instance of the 3D card.
    // It is assumed that WeaponModuleCard3D has a constructor that accepts a WeaponModule.
    WeaponModuleCard3D card3d = new WeaponModuleCard3D();
    card3d.Initialize(Module);
    // Transfer the common data.
    card3d.CardCore = CardCore;

    Camera3D camera = GetViewport().GetCamera3D();
    if (camera != null)
    {
      float dropDistance = 2.0f; // Closer spawn point.
                                 // In Godot 4, the camera faces along its negative Z axis.
      Vector3 forward = -camera.GlobalTransform.Basis.Z.Normalized();
      Vector3 dropPosition = camera.GlobalTransform.Origin + forward * dropDistance;

      // Raycast from the camera to the drop position to avoid spawning inside walls.
      var spaceState = camera.GetWorld3D().DirectSpaceState;
      PhysicsRayQueryParameters3D query = new PhysicsRayQueryParameters3D
      {
        From = camera.GlobalTransform.Origin,
        To = dropPosition,
        Exclude = new Godot.Collections.Array<Rid>(),
        CollisionMask = 0xFFFFFFFF
      };
      var collision = spaceState.IntersectRay(query);
      if (collision.Count() > 0)
      {
        // Adjust drop position to be a safe offset away from the collision point.
        float safeOffset = 0.5f;
        if (collision.ContainsKey("position") && collision.ContainsKey("normal"))
        {
          Vector3 collisionPos = (Vector3)collision["position"];
          Vector3 collisionNormal = (Vector3)collision["normal"];
          dropPosition = collisionPos + collisionNormal * safeOffset;
        }
      }

      Transform3D transform = card3d.GlobalTransform;
      transform.Origin = dropPosition;
      card3d.GlobalTransform = transform;

      // Add the card to the scene tree.
      Node ground = GetNodeOrNull("/root/World/Ground");
      if (ground != null)
        ground.AddChild(card3d);
      else
        GetTree().Root.AddChild(card3d);

      // Immediately orient the card to face the camera.
      Vector3 toCamera = camera.GlobalTransform.Origin - dropPosition;
      if (toCamera.Length() > 0.001f)
      {
        Vector3 upDir = Vector3.Up;
        if (Math.Abs(toCamera.Normalized().Dot(upDir)) > 0.99f)
          upDir = Vector3.Forward;
        transform = card3d.GlobalTransform;
        transform.Basis = Basis.LookingAt(-toCamera, upDir);
        card3d.GlobalTransform = transform;
      }

      // Instead of teleporting, apply an initial toss velocity (weaker toss).
      float tossStrength = 3.0f;
      float tossUpStrength = 1.0f;
      card3d.LinearVelocity = forward * tossStrength + Vector3.Up * tossUpStrength;
    }
    else
    {
      // Fallback: place using the 2D global position (converted to 3D).
      Transform3D transform = card3d.GlobalTransform;
      transform.Origin = new Vector3(GlobalPosition.X, GlobalPosition.Y, 0);
      card3d.GlobalTransform = transform;
      Node ground = GetNodeOrNull("/root/World/Ground");
      if (ground != null)
        ground.AddChild(card3d);
      else
        GetTree().Root.AddChild(card3d);
    }

    QueueFree();
  }
}
