using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;

public partial class StickyBulletModifier : BulletModifier
{
  [Export]
  private StandardMaterial3D BlobMaterial;

  [Export]
  public float StickDuration { get; set; } = 1.0f;

  [Export]
  public float CollisionDamage { get; set; } = 1.0f;

  [Export]
  public float tracking_strength { get; set; } = 0.1f; // How quickly the bullet turns; 0.0 to 1.0

  [Export]
  public float max_ray_distance { get; set; } = 1000.0f; // Maximum distance for the ray trace


  private static RandomNumberGenerator _rng = new RandomNumberGenerator();


  public override Task OnFire(Bullet bullet)
  {
    // Use a dedicated property instead of meta if possible.
    bullet.SetMeta("is_sticky", false);

    // Create a goopy green translucent blob overlay.
    MeshInstance3D blob = new MeshInstance3D();

    // Create a low-poly sphere mesh for the blob.
    SphereMesh sphere = new SphereMesh
    {
      RadialSegments = 4,
      Rings = 4
    };

    // Reuse the shared RandomNumberGenerator instance.
    float slimeThickness = bullet.Radius / 1.5f;
    float randomOffset = _rng.RandfRange(0f, slimeThickness / 2);
    sphere.Radius = bullet.Radius + slimeThickness + randomOffset;
    sphere.Height = sphere.Radius * 2; // Ensure height matches the diameter.
    blob.Mesh = sphere;

    // Use the cached blob material.
    blob.MaterialOverride = BlobMaterial;

    // Center the blob over the bullet.
    blob.Position = Vector3.Zero;
    bullet.AddChild(blob);

    return Task.CompletedTask;
  }

  public override async Task OnCollision(Bullet bullet, Bullet.CollisionData collision)
  {
    collision.TotalDamageDealt += CollisionDamage;
    bool initialOverlapEnabled = bullet.EnableOverlapCollision;
    bullet.EnableOverlapCollision = false;
    float originalGravity = bullet.Gravity;
    bullet.Gravity = 0;

    try
    {
      // If the collider is an enemy, apply damage.
      if (collision.Collider != null && collision.Collider.IsInGroup("enemies"))
      {
        if (IsInstanceValid(collision.Collider))
          collision.Collider.Call("take_damage", CollisionDamage);
      }

      // Save the bullet's original velocity.
      Vector3 initialVelocity = bullet.Velocity;

      // If the collider is another bullet, perform a secondary raycast.
      if (collision.Collider is Bullet)
      {
        Transform3D bulletTransform = bullet.GlobalTransform;
        Vector3 bulletOrigin = bulletTransform.Origin;
        Vector3 bulletDir = bullet.Velocity.Normalized();
        PhysicsRayQueryParameters3D secondaryQuery = new PhysicsRayQueryParameters3D
        {
          From = bulletOrigin,
          To = bulletOrigin + bulletDir * 100.0f,
          CollisionMask = 1,
          Exclude = bullet.GetChildCollisionRIDs()
        };

        if (bullet is Node3D bulletNode)
        {
          var spaceState = bulletNode.GetWorld3D().DirectSpaceState;
          var newCollision = spaceState.IntersectRay(secondaryQuery);
          if (newCollision != null && newCollision.Count > 0)
          {
            if (newCollision.ContainsKey("collider"))
            {
              object colliderObj = newCollision["collider"];
              if (colliderObj is Node newCollider)
                collision.Collider = newCollider as Node3D;
            }
            if (newCollision.ContainsKey("position"))
              collision.Position = (Vector3)newCollision["position"];
            if (newCollision.ContainsKey("normal"))
              collision.Normal = (Vector3)newCollision["normal"];
          }
          else
            return;
        }
        else
          return;
      }

      // Do not proceed if bullet is already sticking.
      if ((bool)bullet.GetMeta("is_sticky"))
        return;

      bullet.SetMeta("is_sticky", true);
      bullet.Velocity = Vector3.Zero;

      // Use collision normal if available; default to Vector3.Up.
      Vector3 normal = (collision.Normal != Vector3.Zero) ? collision.Normal : Vector3.Up;
      // Offset slightly along the normal.
      bullet.GlobalTransform = new Transform3D(bullet.GlobalTransform.Basis, collision.Position + normal * 0.01f);

      // Save the original parent for later reparenting.
      Node originalParent = bullet.GetParent();

      // Reparent bullet to the collider (if applicable).
      if (collision.Collider != null)
      {
        if (collision.Collider is Node3D colliderNode)
        {
          Vector3 localPosition = colliderNode.ToLocal(bullet.GlobalTransform.Origin);
          bullet.Reparent(colliderNode);
          bullet.Transform = new Transform3D(bullet.Transform.Basis, localPosition);
        }
        await PushBulletOutside(bullet, normal);
      }

      // Wait for the stick duration.
      SceneTree tree = bullet.GetTree();
      if (tree != null)
      {
        var timer = tree.CreateTimer(StickDuration);
        await ToSignal(timer, "timeout");
      }
      else
      {
        GD.Print("Warning: bullet is not in the scene tree!");
      }

      // Unstick the bullet if it's still valid.
      if (IsInstanceValid(bullet))
      {
        bullet.SetMeta("is_sticky", false);
        if (originalParent != null && IsInstanceValid(originalParent))
        {
          Transform3D currentGlobal = bullet.GlobalTransform;
          bullet.GetParent().RemoveChild(bullet);
          originalParent.AddChild(bullet);
          bullet.GlobalTransform = currentGlobal;
          bullet.Velocity = initialVelocity;
        }
      }
    }
    finally
    {
      // Reset gravity and overlap collision.
      bullet.Gravity = originalGravity;
      bullet.EnableOverlapCollision = initialOverlapEnabled;
    }
  }

  private async Task PushBulletOutside(Bullet bullet, Vector3 normal)
  {
    var spaceState = bullet.GetWorld3D().DirectSpaceState;
    // Create a sphere shape and update its radius based on bullet properties.
    SphereShape3D sphere = new SphereShape3D { Radius = bullet.Radius };

    var query = new PhysicsShapeQueryParameters3D
    {
      Shape = sphere,
      Transform = bullet.GlobalTransform,
      Exclude = bullet.GetChildCollisionRIDs()
    };

    const int maxIterations = 10;
    const float pushStep = 0.05f;
    int iterations = 0;

    while (iterations < maxIterations)
    {
      var collisions = spaceState.IntersectShape(query);
      if (collisions.Count == 0)
        break;

      Transform3D currentTransform = bullet.GlobalTransform;
      currentTransform.Origin += normal * pushStep;
      bullet.GlobalTransform = currentTransform;
      query.Transform = currentTransform;
      iterations++;

      // Yield to allow physics to update.
      await Task.Yield();
    }
  }
}
