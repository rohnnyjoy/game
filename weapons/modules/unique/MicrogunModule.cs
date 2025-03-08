using System.Threading.Tasks;
using Godot;

public partial class MicrogunModule : WeaponModule
{
  // Configurable properties:
  [Export]
  public float BaselineAccuracy { get; set; } = 0.7f; // Lower than max

  [Export]
  public float MaxAccuracy { get; set; } = 1.0f;

  // Increase accuracy very slowly per shot.
  [Export]
  public float AccuracyIncreasePerShot { get; set; } = 0.006f;

  // Decay accuracy rapidly (per tick).
  [Export]
  public float AccuracyDecayRate { get; set; } = 0.0015f;

  // Internal state:
  private float _currentAccuracy = 0.7f;

  public override void _Ready()
  {
    // Initialize current accuracy to the baseline.
    _currentAccuracy = BaselineAccuracy;
  }

  // This method is called on every game tick.
  public override async Task OnWeaponProcess(double _delta)
  {
    // Always decay accuracy toward the baseline, even during continuous fire.
    if (_currentAccuracy > BaselineAccuracy)
    {
      _currentAccuracy -= AccuracyDecayRate;
      if (_currentAccuracy < BaselineAccuracy)
        _currentAccuracy = BaselineAccuracy;
    }
    await Task.CompletedTask;
  }

  // This method is called when a shot is fired.
  private void OnShotFired()
  {
    // Increase accuracy (clamped to MaxAccuracy)
    _currentAccuracy = Mathf.Min(MaxAccuracy, _currentAccuracy + AccuracyIncreasePerShot);
  }

  public override Bullet ModifyBullet(Bullet bullet)
  {
    // Increase accuracy for each shot.
    OnShotFired();

    bullet.Gravity = 1;
    bullet.Radius = 0.05f;
    return bullet;
  }

  public override async Task OnReload()
  {
    _currentAccuracy = BaselineAccuracy;
    await Task.CompletedTask;
  }

  public override float GetModifiedDamage(float damage)
  {
    return 2;
  }

  // Return the current accuracy (modified by continuous fire and decay).
  public override float GetModifiedAccuracy(float accuracy)
  {
    return _currentAccuracy;
  }

  public override float GetModifiedFireRate(float fireRate)
  {
    return 0.0015f;
  }

  public override float GetModifiedBulletSpeed(float bulletSpeed)
  {
    return 200;
  }

  public override int GetModifiedAmmo(int ammo)
  {
    return 800;
  }

  public override float GetModifiedReloadSpeed(float reloadSpeed)
  {
    return 1;
  }
}
