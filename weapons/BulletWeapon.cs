using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BulletWeapon : Weapon
{
  [Export]
  public PackedScene BulletScene { get; set; }
  [Export]
  public PackedScene MuzzleFlash { get; set; }

  [Export]
  public float GunRecoilRotation { get; set; } = 15.0f; // Degrees of upward rotation.
  [Export]
  public float GunRecoilKickback { get; set; } = 0.2f;   // Units to shift backward.
  [Export]
  public float RecoilRecoverySpeed { get; set; } = 4.0f;  // Recovery speed from recoil.


  private Node3D BulletOrigin;
  private Node3D Muzzle;
  private bool Firing = false;
  private RandomNumberGenerator rng = new RandomNumberGenerator();

  // Store the original local transform of the gun (so it moves with its parent).
  private Transform3D originalTransform;
  // Represents the current recoil intensity (0 to 1).
  private float currentRecoil = 0.0f;

  public override void _Ready()
  {
    base._Ready();
    BulletOrigin = GetNode<Node3D>("BulletOrigin");
    Muzzle = GetNode<Node3D>("Muzzle");
    rng.Randomize();

    // Save the original local transform.
    originalTransform = Transform;
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
    Bullet bullet = (Bullet)BulletScene.Instantiate();

    // Get the camera.
    Camera3D camera = GetViewport().GetCamera3D();
    if (camera == null)
      return;

    // Determine the center of the screen.
    Vector2 screenCenter = GetViewport().GetVisibleRect().Size / 2;
    Vector3 rayOrigin = camera.ProjectRayOrigin(screenCenter);
    Vector3 rayDirection = camera.ProjectRayNormal(screenCenter);
    Vector3 rayEnd = rayOrigin + rayDirection * 1000;

    // Cast a ray into the scene.
    var spaceState = GetWorld3D().DirectSpaceState;
    var query = new PhysicsRayQueryParameters3D
    {
      From = rayOrigin,
      To = rayEnd
    };
    Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

    Vector3 target = (result != null && result.ContainsKey("position"))
                     ? (Vector3)result["position"]
                     : rayEnd;

    // Compute bullet direction.
    Vector3 baseDirection = (target - BulletOrigin.GlobalTransform.Origin).Normalized();

    // Apply accuracy-based spread.
    float accuracy = GetAccuracy();
    float maxSpreadAngleDeg = Mathf.Lerp(90.0f, 0.0f, accuracy);
    float maxSpreadAngleRad = Mathf.DegToRad(maxSpreadAngleDeg);

    float cosMax = Mathf.Cos(maxSpreadAngleRad);
    float cosTheta = Mathf.Lerp(cosMax, 1.0f, rng.Randf());
    float deviationAngle = Mathf.Acos(cosTheta);

    float randomAzimuth = rng.Randf() * Mathf.Tau;

    Vector3 forward = baseDirection;
    Vector3 right = GetOrthogonal(forward).Normalized();
    Vector3 up = forward.Cross(right).Normalized();

    baseDirection = (forward * Mathf.Cos(deviationAngle)) +
                    (right * Mathf.Sin(deviationAngle) * Mathf.Cos(randomAzimuth)) +
                    (up * Mathf.Sin(deviationAngle) * Mathf.Sin(randomAzimuth));

    bullet.Direction = baseDirection;
    bullet.Speed = GetBulletSpeed();
    bullet.Radius = 0.05f;
    bullet.Damage = GetDamage();
    bullet.GlobalTransform = BulletOrigin.GlobalTransform;

    foreach (var module in ImmutableModules + Modules)
    {
      bullet = module.ModifyBullet(bullet);
    }

    foreach (var module in ImmutableModules + Modules)
    {
      foreach (var modifier in module.BulletModifiers)
      {
        bullet.Modifiers.Add(modifier);
      }
    }
    GetTree().CurrentScene.AddChild(bullet);
    GD.Print("Firing bullet");

    if (MuzzleFlash != null)
    {
      GpuParticles3D flash = (GpuParticles3D)MuzzleFlash.Instantiate();
      Muzzle.AddChild(flash);
      // Double-check that the muzzle flash has correct settings.
      flash.Emitting = true;
      flash.OneShot = true;
      flash.GlobalTransform = Muzzle.GlobalTransform;
    }

    Player.Instance.CameraShake.TriggerShake(0.04f, Mathf.Lerp(0.03f, 0.15f, GetDamage() / 100f));
    // --- Apply recoil effect to the gun model ---
    currentRecoil = 1.0f;  // Set recoil intensity to maximum on fire.
  }

  private async Task Reload()
  {
    Reloading = true;
    await Task.Delay((int)(GetReloadSpeed() * 1000));
    CurrentAmmo = GetAmmo();
    Reloading = false;
    foreach (var module in ImmutableModules + Modules)
    {
      await module.OnReload();
    }
  }

  public override async void _Process(double delta)
  {
    // Process weapon modules.
    foreach (var module in ImmutableModules + Modules)
    {
      await module.OnWeaponProcess(delta);
    }

    // Update recoil effect.
    if (currentRecoil > 0.001f)
    {
      GD.Print("Recoil: ", currentRecoil);
      // Gradually recover from recoil.
      currentRecoil = Mathf.Lerp(currentRecoil, 0, (float)(delta * RecoilRecoverySpeed));

      // Create a rotation offset around the local X-axis (upward rotation).
      Basis rotationOffset = new Basis(Vector3.Right, Mathf.DegToRad(GunRecoilRotation * currentRecoil));
      // Create a translation offset along the local Z-axis (backward kick).
      Vector3 translationOffset = new Vector3(0, 0, GunRecoilKickback * currentRecoil);

      // Combine rotation and translation.
      Transform3D recoilTransform = new Transform3D(rotationOffset, translationOffset);

      // Apply recoil offset relative to the original local transform.
      Transform = originalTransform * recoilTransform;
    }
    else
    {
      // Ensure we reset to the original local transform.
      Transform = originalTransform;
    }
  }

  // Helper method: returns a vector orthogonal to the given vector.
  private Vector3 GetOrthogonal(Vector3 v)
  {
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
