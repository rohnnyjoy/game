using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;

public partial class TrackingModule : WeaponModule
{
  [Export]
  public float tracking_strength { get; set; } = 0.1f; // How quickly the bullet turns; 0.0 to 1.0

  [Export]
  public float max_ray_distance { get; set; } = 1000.0f; // Maximum distance for the ray trace

  public TrackingModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/tracking.png");
    Rarity = Rarity.Rare;
    ModuleDescription = "Bullets track the mouse cursor, adjusting their trajectory to hit it.";
  }

  public override async Task OnBulletPhysicsProcess(float delta, Bullet bullet)
  {
    if (bullet == null || !bullet.IsInsideTree())
    {
      QueueFree();
      return;
    }

    var viewport = bullet.GetViewport();
    if (viewport == null)
      return;

    Camera3D camera = viewport.GetCamera3D();
    if (camera == null)
      return;

    Vector2 mousePos = viewport.GetMousePosition();
    Vector3 rayOrigin = camera.ProjectRayOrigin(mousePos);
    Vector3 rayDirection = camera.ProjectRayNormal(mousePos);
    Vector3 rayEnd = rayOrigin + rayDirection * max_ray_distance;

    PhysicsRayQueryParameters3D rayQuery = new PhysicsRayQueryParameters3D
    {
      From = rayOrigin,
      To = rayEnd,
      // Convert bullet to its RID since Exclude expects an Array<Rid>
      Exclude = new Array<Rid> { bullet.GetRid() }
    };

    var spaceState = bullet.GetWorld3D().DirectSpaceState;
    var collision = spaceState.IntersectRay(rayQuery);

    Vector3 targetPoint;
    // Use ContainsKey instead of Contains for dictionaries in C#
    if (collision.Count > 0 && collision.ContainsKey("position"))
    {
      targetPoint = (Vector3)collision["position"];
    }
    else
    {
      targetPoint = rayEnd;
    }

    Vector3 targetDirection = (targetPoint - bullet.GlobalPosition).Normalized();
    Vector3 desiredVelocity = targetDirection * bullet.Speed;
    // Since Vector3.LinearInterpolate isn't available, we perform the lerp manually
    bullet.Velocity = bullet.Velocity + (desiredVelocity - bullet.Velocity) * tracking_strength;

    await Task.CompletedTask;
  }
}
