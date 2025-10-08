using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BulletWeapon : Weapon
{
  [Export]
  public bool UseBulletManager { get; set; } = true;

  // If using BulletManager, this archetype id will be registered on _Ready()
  [Export]
  public int BulletArchetypeId { get; set; } = -1;

  [Export]
  public float ManagerBulletLifetime { get; set; } = 3.0f;

  [Export]
  public float ManagerBulletRadius { get; set; } = 0.05f;

  [Export]
  public float ManagerVisualScale { get; set; } = 1.0f;

  [Export]
  public bool ManagerAlignToVelocity { get; set; } = false;

  // Trail configuration for BulletManager rendering
  [Export] public bool ManagerTrailEnabled { get; set; } = true;
  // Match RibbonTrailEmitter defaults closely
  [Export] public float ManagerTrailWidth { get; set; } = 0.05f;       // Ribbon BaseWidth = 0.05
  [Export] public int ManagerTrailMaxPoints { get; set; } = 12;        // Allow more segments if needed
  [Export] public float ManagerTrailMinDistance { get; set; } = 0.01f; // Ribbon Distance = 0.01
  [Export] public float ManagerTrailLifetime { get; set; } = 0.10f;    // Ribbon Lifetime = 0.1
  [Export] public bool ManagerTrailViewAligned { get; set; } = true;
  [Export]
  public PackedScene BulletScene { get; set; }
  [Export]
  public PackedScene MuzzleFlash { get; set; }
  [Export]
  public float GunRecoilRotation { get; set; } = 15.0f;
  [Export]
  public float GunRecoilKickback { get; set; } = 0.2f;
  [Export]
  public float RecoilRecoverySpeed { get; set; } = 4.0f;
  [Export]
  public int PoolSize { get; set; } = 200; // Configurable pool size

  private Node3D BulletOrigin;
  private Node3D Muzzle;
  private bool Firing = false;
  private float fireCooldownTimer = 0.0f;
  private Task reloadTask = Task.CompletedTask;
  private RandomNumberGenerator rng = new RandomNumberGenerator();

  // Store the original local transform of the gun.
  private Transform3D originalTransform;
  // Represents the current recoil intensity (0 to 1).
  private float currentRecoil = 0.0f;

  // Bullet object pool stored as a list with an index acting as a circular buffer.
  private List<Bullet> bulletPool = new List<Bullet>();
  private int nextBulletIndex = 0;

  // New field for single muzzle flash instance
  private GpuParticles3D muzzleFlashInstance;
  // Gunshot audio
  private AudioStreamPlayer3D _gunShot;
  [Export]
  public string SfxGunPath { get; set; } = "res://assets/sounds/gun.wav";

  public override void _Ready()
  {
    base._Ready();
    BulletOrigin = GetNode<Node3D>("BulletOrigin");
    Muzzle = GetNode<Node3D>("Muzzle");
    rng.Randomize();

    // Save the original local transform.
    originalTransform = Transform;

    ModulesChanged += HandleModulesChanged;

    // If we are not using BulletManager (or manager is missing), build the local pool.
    // Otherwise, defer to centralized manager and avoid pooling here.
    if (!(UseBulletManager && BulletManager.Instance != null))
    {
      Node sceneRoot = GetTree().CurrentScene;
      for (int i = 0; i < PoolSize; i++)
      {
        Bullet bullet = (Bullet)BulletScene.Instantiate();
        bullet.Visible = false;
        bullet.Name = "Bullet_" + i;
        sceneRoot.AddChild(bullet);
        bulletPool.Add(bullet);
      }
    }

    HandleModulesChanged();

    // Instantiate the muzzle flash once, if available.
    if (MuzzleFlash != null)
    {
      muzzleFlashInstance = (GpuParticles3D)MuzzleFlash.Instantiate();
      Muzzle.AddChild(muzzleFlashInstance);
      muzzleFlashInstance.Emitting = false;
      muzzleFlashInstance.OneShot = true; // ensure one-shot behavior
    }

    // Create a 3D audio player for gunshot SFX and attach to the muzzle if available
    _gunShot = new AudioStreamPlayer3D { Autoplay = false, Bus = "Master" };
    _gunShot.Stream = GD.Load<AudioStream>(SfxGunPath);
    if (Muzzle != null)
      Muzzle.AddChild(_gunShot);
    else
      AddChild(_gunShot);
  }

  public override void OnPress()
  {
    Firing = true;
    TryFireImmediate();
  }

  public override void OnRelease()
  {
    Firing = false;
  }
  public override void _ExitTree()
  {
    ModulesChanged -= HandleModulesChanged;
    base._ExitTree();
  }

  private void HandleModulesChanged()
  {
    if (UseBulletManager && BulletManager.Instance != null)
      BulletManager.Instance.EnsureArchetypeForWeapon(this);
  }

  private const float MinFireInterval = 0.001f;

  private void FireBullet()
  {
    // Centralized path: emit a MultiMesh bullet via BulletManager
    if (UseBulletManager && BulletManager.Instance != null && BulletArchetypeId >= 0)
    {
      Vector3 origin = BulletOrigin.GlobalTransform.Origin;
      Vector3 dir = ComputeBulletDirection();
      float speed = GetBulletSpeed();
      float damage = GetDamage();
      Vector3 velocity = dir * speed;

      if (TryGetScatter(out int duplicateCount, out float spreadAngle, out float damageFactor) && duplicateCount > 1)
      {
        // Spawn original bullet
        BulletManager.Instance.SpawnBullet(BulletArchetypeId, origin, velocity, damage, ManagerBulletLifetime);

        // Basis for yaw/pitch rotation around current direction
        Vector3 forward = dir;
        Vector3 right = GetOrthogonal(forward).Normalized();
        Vector3 up = forward.Cross(right).Normalized();

        for (int i = 1; i < duplicateCount; i++)
        {
          float yaw = rng.RandfRange(-spreadAngle / 2f, spreadAngle / 2f);
          float pitch = rng.RandfRange(-spreadAngle / 2f, spreadAngle / 2f);
          Vector3 newDir = forward.Rotated(up, yaw).Rotated(right, pitch).Normalized();
          Vector3 newVel = newDir * speed;
          float dupDamage = damage * damageFactor;
          BulletManager.Instance.SpawnBullet(BulletArchetypeId, origin, newVel, dupDamage, ManagerBulletLifetime);
        }
      }
      else
      {
        BulletManager.Instance.SpawnBullet(BulletArchetypeId, origin, velocity, damage, ManagerBulletLifetime);
      }

      // Use the single muzzle flash instance if available.
      if (muzzleFlashInstance != null)
      {
        muzzleFlashInstance.GlobalTransform = Muzzle.GlobalTransform;
        muzzleFlashInstance.Emitting = false;
        muzzleFlashInstance.Emitting = true;
      }

      // Play SFX and emit a global fired event for listeners (e.g., camera shake)
      PlayGunshot();
      GlobalEvents.Instance?.EmitWeaponFired(this);
      currentRecoil = 1.0f;
      return;
    }
    // Fallback path only when manager is not in use or missing
    if (!UseBulletManager || BulletManager.Instance == null)
    {
      // Always reuse the oldest bullet in the pool.
      Bullet bullet = bulletPool[nextBulletIndex];
      // Advance the circular buffer index.
      nextBulletIndex = (nextBulletIndex + 1) % bulletPool.Count;

      // Reset the bullet state so it can be reused even if still active.
      bullet.Reset();

      // Set bullet properties.
      bullet.GlobalTransform = BulletOrigin.GlobalTransform;
      bullet.Direction = ComputeBulletDirection();
      bullet.Speed = GetBulletSpeed();
      // FIX: reinitialize velocity based on the new direction and speed
      bullet.Velocity = bullet.Direction.Normalized() * bullet.Speed;
      bullet.Radius = 0.05f;
      bullet.Damage = GetDamage();
      bullet.Visible = true;

      // Legacy per-bullet modifications removed; runtime behavior is provided via manager pipelines/providers.

      // Ensure the bullet is parented to the scene root.
      if (bullet.GetParent() != GetTree().CurrentScene)
        GetTree().CurrentScene.AddChild(bullet);

      // Use the single muzzle flash instance.
      if (muzzleFlashInstance != null)
      {
        // Update the global transform in case the Muzzle moved.
        muzzleFlashInstance.GlobalTransform = Muzzle.GlobalTransform;
        // Restart the emission.
        muzzleFlashInstance.Emitting = false;
        muzzleFlashInstance.Emitting = true;
      }

      // Apply recoil effect.
      currentRecoil = 1.0f;
      PlayGunshot();
      GlobalEvents.Instance?.EmitWeaponFired(this);
    }
    else
    {
      // Manager present but archetype not ready; skip this shot to avoid fallback ribbons
      GD.Print("BulletManager archetype not ready for weapon ", Name, "; skipping shot this frame");
    }
  }

  private void PlayGunshot()
  {
    if (_gunShot == null || _gunShot.Stream == null) return;
    // Subtle pitch variance for texture
    _gunShot.PitchScale = 0.97f + 0.06f * rng.Randf();
    // Ensure position matches the muzzle at fire time
    if (_gunShot.GetParent() == this && Muzzle != null)
      _gunShot.GlobalTransform = Muzzle.GlobalTransform;
    _gunShot.Play();
  }

  private void TryFireImmediate()
  {
    if (Reloading)
      return;

    if (CurrentAmmo <= 0)
    {
      StartReloadIfNeeded();
      return;
    }

    if (fireCooldownTimer <= 0.0f)
    {
      FireAndConsumeAmmo();
    }
  }

  private void FireAndConsumeAmmo()
  {
    FireBullet();
    CurrentAmmo--;
    fireCooldownTimer = GetFireInterval();
  }

  private float GetFireInterval()
  {
    return Mathf.Max(GetFireRate(), MinFireInterval);
  }

  private void StartReloadIfNeeded()
  {
    if (Reloading)
      return;

    if (reloadTask != null && !reloadTask.IsCompleted)
      return;

    reloadTask = ReloadAsync();
  }

  private Vector3 ComputeBulletDirection()
  {
    Camera3D camera = GetViewport().GetCamera3D();
    if (camera == null)
      return Vector3.Forward;

    Vector2 screenCenter = GetViewport().GetVisibleRect().Size / 2;
    Vector3 rayOrigin = camera.ProjectRayOrigin(screenCenter);
    Vector3 rayDirection = camera.ProjectRayNormal(screenCenter);
    Vector3 rayEnd = rayOrigin + rayDirection * 1000;

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

    Vector3 finalDirection = (forward * Mathf.Cos(deviationAngle)) +
                             (right * Mathf.Sin(deviationAngle) * Mathf.Cos(randomAzimuth)) +
                             (up * Mathf.Sin(deviationAngle) * Mathf.Sin(randomAzimuth));
    return finalDirection.Normalized();
  }

  private async Task ReloadAsync()
  {
    Reloading = true;
    await WaitForSeconds(GetReloadSpeed());
    CurrentAmmo = GetAmmo();
    Reloading = false;
    fireCooldownTimer = 0.0f;
    foreach (var module in ImmutableModules + Modules)
    {
      await module.OnReload();
    }
    BulletManager.Instance?.NotifyWeaponReloaded(this);
  }

  private async Task WaitForSeconds(float seconds)
  {
    float clampedSeconds = Mathf.Max(seconds, 0.001f);
    if (IsInsideTree())
    {
      SceneTree tree = GetTree();
      if (tree != null)
      {
        SceneTreeTimer timer = tree.CreateTimer(clampedSeconds);
        await ToSignal(timer, "timeout");
        return;
      }
    }

    await Task.Delay((int)(clampedSeconds * 1000.0f));
  }

  public override async void _Process(double delta)
  {
    foreach (var module in ImmutableModules + Modules)
    {
      await module.OnWeaponProcess(delta);
    }

    if (currentRecoil > 0.001f)
    {
      currentRecoil = Mathf.Lerp(currentRecoil, 0, (float)(delta * RecoilRecoverySpeed));
      Basis rotationOffset = new Basis(Vector3.Right, Mathf.DegToRad(GunRecoilRotation * currentRecoil));
      Vector3 translationOffset = new Vector3(0, 0, GunRecoilKickback * currentRecoil);
      Transform3D recoilTransform = new Transform3D(rotationOffset, translationOffset);
      Transform = originalTransform * recoilTransform;
    }
    else
    {
      Transform = originalTransform;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    // Drive firing cadence from physics to avoid async _Process jitter.
    UpdateFiringState((float)delta);
  }

  private void UpdateFiringState(float dt)
  {
    fireCooldownTimer = Mathf.Max(0.0f, fireCooldownTimer - dt);

    if (CurrentAmmo <= 0)
    {
      StartReloadIfNeeded();
    }

    if (!Firing)
      return;

    if (CurrentAmmo <= 0 || Reloading)
      return;

    if (fireCooldownTimer <= 0.0f)
    {
      FireAndConsumeAmmo();
    }
  }

  private Vector3 GetOrthogonal(Vector3 v)
  {
    if (Mathf.Abs(v.X) < Mathf.Abs(v.Y) && Mathf.Abs(v.X) < Mathf.Abs(v.Z))
      return new Vector3(0, -v.Z, v.Y);
    else if (Mathf.Abs(v.Y) < Mathf.Abs(v.Z))
      return new Vector3(-v.Z, 0, v.X);
    else
      return new Vector3(-v.Y, v.X, 0);
  }
}

// Helpers for extracting mesh/material from a bullet scene
public partial class BulletWeapon
{
  private bool TryGetScatter(out int duplicationCount, out float spreadAngle, out float damageFactor)
  {
    int count = 0;
    float angle = 0f;
    float factor = 1f;
    bool found = false;

    void InspectModule(WeaponModule m)
    {
      if (m is IScatterProvider provider && provider.TryGetScatterConfig(out var cfg) && cfg.DuplicationCount > 1)
      {
        count = Math.Max(1, cfg.DuplicationCount);
        angle = cfg.SpreadAngle;
        factor = cfg.DamageFactor;
        found = true;
      }
    }

    if (ImmutableModules != null)
    {
      foreach (var m in ImmutableModules)
        InspectModule(m);
    }
    if (!found && Modules != null)
    {
      foreach (var m in Modules)
        InspectModule(m);
    }

    duplicationCount = count;
    spreadAngle = angle;
    damageFactor = factor;
    return found;
  }
  private void TryExtractMeshInfo(PackedScene scene, out Mesh mesh, out Material material, out Transform3D localTransform)
  {
    mesh = null;
    material = null;
    localTransform = Transform3D.Identity;
    if (scene == null)
      return;

    Node inst = null;
    try
    {
      inst = scene.Instantiate();
      Transform3D acc = Transform3D.Identity;
      FindFirstMeshRecursive(inst, acc, ref mesh, ref material, ref localTransform);
    }
    catch (Exception e)
    {
      GD.PrintErr($"BulletWeapon: Failed to extract mesh: {e.Message}");
    }
    finally
    {
      if (IsInstanceValid(inst))
        inst.Free();
    }
  }

  private void FindFirstMeshRecursive(Node node, Transform3D acc, ref Mesh mesh, ref Material material, ref Transform3D local)
  {
    Transform3D nextAcc = acc;
    if (node is Node3D n3)
      nextAcc = acc * n3.Transform;

    if (node is MeshInstance3D mi && mi.Mesh != null)
    {
      mesh = mi.Mesh;
      material = mi.MaterialOverride;
      local = nextAcc;
      return;
    }
    foreach (Node child in node.GetChildren())
    {
      if (!IsInstanceValid(child)) continue;
      if (mesh != null) return;
      FindFirstMeshRecursive(child, nextAcc, ref mesh, ref material, ref local);
      if (mesh != null) return;
    }
  }
}
