using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BulletWeapon : Weapon
{
  private Node3D BulletOrigin;
  private Node MuzzleFlash;
  private bool Firing = false;
  private RandomNumberGenerator rng = new RandomNumberGenerator();

  public override void _Ready()
  {
    base._Ready();
    BulletOrigin = GetNode<Node3D>("BulletOrigin");
    MuzzleFlash = GetNode<Node>("MuzzleFlash");
    rng.Randomize();
  }

  public override void OnPress()
  {
    if (Firing)
      return;
    Firing = true;
    _ = FireLoop();
  }

  public override void OnRelease()
  {
    Firing = false;
  }

  private async Task FireLoop()
  {
    while (Firing)
    {
      if (CurrentAmmo <= 0)
      {
        await Reload();
      }
      else
      {
        FireBullet();
        CurrentAmmo--;
        await Task.Delay((int)(GetFireRate() * 1000));
      }
    }
  }

  private void FireBullet()
  {
    Bullet bullet = new Bullet();

    // Get the camera.
    Camera3D camera = GetViewport().GetCamera3D();
    if (camera == null)
      return;

    // Determine the center of the screen (assuming your crosshair is centered).
    Vector2 screenCenter = GetViewport().GetVisibleRect().Size / 2;
    Vector3 rayOrigin = camera.ProjectRayOrigin(screenCenter);
    Vector3 rayDirection = camera.ProjectRayNormal(screenCenter);
    Vector3 rayEnd = rayOrigin + rayDirection * 1000; // Arbitrary long distance

    // Cast a ray into the scene from the camera's crosshair using PhysicsRayQueryParameters3D.
    var spaceState = GetWorld3D().DirectSpaceState;
    var query = new PhysicsRayQueryParameters3D
    {
      From = rayOrigin,
      To = rayEnd
    };
    Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

    // Determine the target using the hit position if available.
    Vector3 target;
    if (result != null && result.ContainsKey("position"))
    {
      target = (Vector3)result["position"];
    }
    else
    {
      target = rayEnd;
    }

    // Compute the bullet direction from its spawn point to the target.
    Vector3 baseDirection = (target - BulletOrigin.GlobalTransform.Origin).Normalized();

    // Apply accuracy-based spread.
    float accuracy = GetAccuracy();

    // Compute maximum deviation: 90° for 0 accuracy (full hemisphere) to 0° for perfect accuracy.
    float maxSpreadAngleDeg = Mathf.Lerp(90.0f, 0.0f, accuracy);
    float maxSpreadAngleRad = Mathf.DegToRad(maxSpreadAngleDeg);

    // Generate a random deviation using a cosine distribution for uniformity on the spherical cap.
    float cosMax = Mathf.Cos(maxSpreadAngleRad);
    float cosTheta = Mathf.Lerp(cosMax, 1.0f, rng.Randf());
    float deviationAngle = Mathf.Acos(cosTheta);

    // A random azimuth angle between 0 and 2π.
    float randomAzimuth = rng.Randf() * Mathf.Tau;

    // Build an orthonormal basis with baseDirection as the 'forward' vector.
    Vector3 forward = baseDirection;
    Vector3 right = GetOrthogonal(forward).Normalized();
    Vector3 up = forward.Cross(right).Normalized();

    // Compute the new direction using spherical coordinates:
    // baseDirection is tilted by deviationAngle away from forward, with rotation defined by randomAzimuth.
    baseDirection = (forward * Mathf.Cos(deviationAngle)) +
                    (right * Mathf.Sin(deviationAngle) * Mathf.Cos(randomAzimuth)) +
                    (up * Mathf.Sin(deviationAngle) * Mathf.Sin(randomAzimuth));

    bullet.Direction = baseDirection;
    bullet.Speed = GetBulletSpeed();
    bullet.Radius = 0.05f;
    bullet.Damage = GetDamage();
    bullet.GlobalTransform = BulletOrigin.GlobalTransform;

    foreach (var module in [UniqueModule] + Modules)
    {
      bullet = module.ModifyBullet(bullet);
    }

    bullet.Modules = [UniqueModule] + Modules;
    GetTree().CurrentScene.AddChild(bullet);

    if (MuzzleFlash != null && MuzzleFlash.HasMethod("trigger_flash"))
    {
      MuzzleFlash.Call("trigger_flash");
    }

    Camera cam = camera as Camera;
    if (cam != null)
    {
      cam.TriggerShake(0.04f, Mathf.Lerp(0.03f, 0.15f, GetDamage() / 100f));
    }
  }

  private async Task Reload()
  {
    Reloading = true;
    await Task.Delay((int)(GetReloadSpeed() * 1000));
    CurrentAmmo = GetAmmo();
    Reloading = false;
    foreach (var module in [UniqueModule] + Modules)
    {
      await module.OnReload();
    }
  }

  public override async void _Process(double delta)
  {
    foreach (var module in [UniqueModule] + Modules)
    {
      await module.OnWeaponProcess(delta);
    }
  }

  // Helper method to compute an orthogonal vector for the given vector.
  private Vector3 GetOrthogonal(Vector3 v)
  {
    // Choose the smallest absolute component to avoid degeneracy.
    if (Mathf.Abs(v.X) < Mathf.Abs(v.Y) && Mathf.Abs(v.X) < Mathf.Abs(v.Z))
    {
      return new Vector3(0, -v.Z, v.Y);
    }
    else if (Mathf.Abs(v.Y) < Mathf.Abs(v.Z))
    {
      return new Vector3(-v.Z, 0, v.X);
    }
    else
    {
      return new Vector3(-v.Y, v.X, 0);
    }
  }
}
