using Godot;
using System.Threading.Tasks;
using Godot.Collections;

public partial class StickyModule : WeaponModule
{
  [Export]
  public float StickDuration { get; set; } = 1.0f;

  [Export]
  public float CollisionDamage { get; set; } = 1.0f;

  public StickyModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/sticky.png");
    ModuleDescription = "Bullets stick to surfaces and enemies, detonating after a short delay.";
  }

  public override Bullet ModifyBullet(Bullet bullet)
  {
    bullet.SetMeta("is_sticky", false);
    bullet.Radius = 0.1f;
    return bullet;
  }

  public override async Task OnCollision(Bullet.CollisionData collision, Bullet bullet)
  {
    // 1. If the collider is in the "enemies" group, call its "take_damage" method.
    if (collision.Collider != null && collision.Collider.IsInGroup("enemies"))
    {
      if (IsInstanceValid(collision.Collider))
        collision.Collider.Call("take_damage", CollisionDamage);
    }

    // Save the bullet's original velocity.
    Vector3 initialVelocity = bullet.Velocity;

    // 2. If the collider is another bullet, perform a secondary raycast.
    if (collision.Collider is Bullet)
    {
      PhysicsRayQueryParameters3D secondaryQuery = new PhysicsRayQueryParameters3D
      {
        From = bullet.GlobalTransform.Origin,
        To = bullet.GlobalTransform.Origin + bullet.Velocity.Normalized() * 100.0f,
        CollisionMask = 1
      };

      // Exclude the bullet itself by its RID.
      var excludeArray = new Godot.Collections.Array<Rid> { bullet.GetRid() };
      secondaryQuery.Exclude = excludeArray;

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
            {
              collision.Collider = newCollider as Node3D;
            }
          }
          if (newCollision.ContainsKey("position"))
          {
            collision.Position = (Vector3)newCollision["position"];
          }
          if (newCollision.ContainsKey("normal"))
          {
            collision.Normal = (Vector3)newCollision["normal"];
          }
        }
        else
        {
          return;
        }
      }
      else
      {
        return;
      }
    }

    // 3. Check if the bullet is already sticking.
    if ((bool)bullet.GetMeta("is_sticky"))
    {
      return;
    }

    // Mark the bullet as sticky.
    bullet.SetMeta("is_sticky", true);

    // Stop the bullet.
    bullet.Velocity = Vector3.Zero;

    // Use the collision normal if provided; otherwise, default to Vector3.Up.
    Vector3 normal = (collision.Normal != Vector3.Zero) ? collision.Normal : Vector3.Up;
    bullet.GlobalTransform = new Transform3D(bullet.GlobalTransform.Basis, collision.Position + normal * 0.01f);

    // 4. Save the original parent for later reparenting.
    Node originalParent = bullet.GetParent();

    // If there is a collider, reparent the bullet to that collider.
    if (collision.Collider != null)
    {
      if (collision.Collider is Node3D colliderNode)
      {
        Vector3 localPosition = colliderNode.ToLocal(bullet.GlobalTransform.Origin);
        bullet.Reparent(colliderNode);
        // Preserve relative position by updating the bullet's transform.
        bullet.Transform = new Transform3D(bullet.Transform.Basis, localPosition);
      }
    }

    // 5. Wait for the stick duration.
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

    // 6. After the timer, if the bullet is still valid, "unstick" it.
    if (!IsInstanceValid(bullet))
      return;

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
