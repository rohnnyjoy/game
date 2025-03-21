using Godot;
using System;
using System.Threading.Tasks;

public partial class BouncingBulletModifier : BulletModifier
{
  [Export]
  public float DamageReduction { get; set; } = 0.2f;
  [Export]
  public float Bounciness { get; set; } = 0.8f;
  [Export]
  public int MaxBounces { get; set; } = 3;


  public override Task OnFire(Bullet bullet)
  {
    // Ensure the bullet has a meta value for bounce count.
    if (!bullet.HasMeta("bounce_count"))
    {
      bullet.SetMeta("bounce_count", 0);
    }
    return Task.CompletedTask;
  }

  public override async Task OnCollision(Bullet bullet, Bullet.CollisionData collision)
  {
    // Temporarily disable destruction so the bullet can bounce.
    bool originalDestroyOnImpact = bullet.DestroyOnImpact;
    bullet.DestroyOnImpact = false;

    // Retrieve the collision normal.
    Vector3 normal = collision.Normal;

    // Update and cache the bounce count.
    int bounceCount = (int)bullet.GetMeta("bounce_count");
    bounceCount++;
    bullet.SetMeta("bounce_count", bounceCount);

    // Apply damage reduction.
    bullet.Damage *= (1.0f - DamageReduction);

    // Reflect the velocity along the collision normal and apply bounciness.
    bullet.Velocity = bullet.Velocity.Bounce(normal) * Bounciness;

    // If the maximum number of bounces is reached, restore the bullet's destroy behavior.
    if (bounceCount >= MaxBounces)
    {
      bullet.DestroyOnImpact = originalDestroyOnImpact;
    }

    await Task.CompletedTask;
  }
}
