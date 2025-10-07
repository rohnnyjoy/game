using Godot;
using System.Threading.Tasks;

public partial class SpeedScaledImpactModifier : BulletModifier
{
  [Export] public float DamagePerSpeedFactor { get; set; } = 1.0f;
  [Export] public float KnockbackPerSpeedFactor { get; set; } = 1.0f;
  [Export] public bool UseInitialSpeedAsBaseline { get; set; } = true;

  private const string InitialSpeedMeta = "initial_speed";

  public override Task OnFire(Bullet bullet)
  {
    if (bullet != null && bullet.IsInsideTree())
    {
      float init = bullet.Velocity.Length();
      bullet.SetMeta(InitialSpeedMeta, init);
    }
    return Task.CompletedTask;
  }

  public override Task OnCollision(Bullet bullet, Bullet.CollisionData collisionData)
  {
    if (BulletManager.Instance != null)
      return Task.CompletedTask;

    if (bullet == null || !bullet.IsInsideTree() || !GodotObject.IsInstanceValid(bullet))
      return Task.CompletedTask;

    float currentSpeed = bullet.Velocity.Length();
    float baseline = UseInitialSpeedAsBaseline && bullet.HasMeta(InitialSpeedMeta)
      ? (float)bullet.GetMeta(InitialSpeedMeta)
      : (bullet.Speed > 0.0001f ? bullet.Speed : (currentSpeed > 0.0001f ? currentSpeed : 1.0f));

    float ratio = baseline > 0.0001f ? currentSpeed / baseline : 1.0f;
    // Linear scale: 1 + (ratio-1) * factor
    float damageScale = 1.0f + (ratio - 1.0f) * Mathf.Max(0.0f, DamagePerSpeedFactor);
    float knockScale = 1.0f + (ratio - 1.0f) * Mathf.Max(0.0f, KnockbackPerSpeedFactor);

    // Apply immediately so downstream default collision reads updated values
    bullet.Damage *= damageScale;
    bullet.KnockbackStrength *= knockScale;

    return Task.CompletedTask;
  }
}
