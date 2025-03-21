using Godot;
using System.Threading.Tasks;

public partial class PiercingBulletModifier : BulletModifier
{
  [Export]
  public float DamageReduction { get; set; } = 0.2f;
  [Export]
  public float VelocityFactor { get; set; } = 0.9f;
  [Export]
  public int MaxPenetrations { get; set; } = 5;
  [Export]
  public float CollisionCooldown { get; set; } = 0.2f;

  public override Task OnFire(Bullet bullet)
  {
    if (!bullet.HasMeta("penetration_count"))
    {
      bullet.SetMeta("penetration_count", 0);
    }
    return Task.CompletedTask;
  }

  public override async Task OnCollision(Bullet bullet, Bullet.CollisionData collisionData)
  {
    // Save the original setting for whether the bullet should be destroyed on impact.
    bool originalDestroyOnImpact = bullet.DestroyOnImpact;
    bullet.DestroyOnImpact = false;

    if (collisionData.Collider.IsInGroup("enemies"))
    {
      GD.Print("pen count ", bullet.GetMeta("penetration_count"));
      int penetrationCount = (int)bullet.GetMeta("penetration_count");
      penetrationCount++;
      bullet.SetMeta("penetration_count", penetrationCount);

      bullet.Damage *= (1.0f - DamageReduction);
      bullet.Velocity *= VelocityFactor;

      if (penetrationCount >= MaxPenetrations)
      {
        bullet.DestroyOnImpact = originalDestroyOnImpact;
      }
    }
    else
    {
      bullet.DestroyOnImpact = originalDestroyOnImpact;
    }

    await Task.CompletedTask;
  }
}
