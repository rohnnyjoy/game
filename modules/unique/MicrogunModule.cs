using System.Threading.Tasks;
using Godot;

public partial class MicrogunModule : WeaponModule, IStatModifier
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

  public MicrogunModule()
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

  public override async Task OnReload()
  {
    _currentAccuracy = BaselineAccuracy;
    await Task.CompletedTask;
  }

  public void Modify(ref WeaponStats stats)
  {
    // Static baseline for snapshot. Dynamic accuracy changes still occur via _currentAccuracy,
    // but snapshot returns the configured baseline value.
    stats.Damage = 2.0f;
    stats.Accuracy = BaselineAccuracy;
    stats.FireRate = 0.0015f;
    stats.BulletSpeed = 200.0f;
    stats.Ammo = 800;
    stats.ReloadSpeed = 1.0f;
  }
}
