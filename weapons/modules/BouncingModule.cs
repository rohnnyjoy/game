using Godot;
using System;
using System.Threading.Tasks;

public partial class BouncingModule : WeaponModule
{
  [Export]
  public float DamageReduction { get; set; } = 0.2f;
  [Export]
  public float Bounciness { get; set; } = 0.8f;
  [Export]
  public int MaxBounces { get; set; } = 3;

  public BouncingModule()
  {
    // Set the card texture and module description.
    CardTexture = GD.Load<Texture2D>("res://icons/bouncing.png");
    ModuleDescription = "Bullets bounce off surfaces, reducing damage with each bounce.";
    Rarity = Rarity.Rare;
  }

  public override Bullet ModifyBullet(Bullet bullet)
  {
    // Ensure the bullet has a meta value for bounce count.
    if (!bullet.HasMeta("bounce_count"))
    {
      bullet.SetMeta("bounce_count", 0);
    }
    return bullet;
  }

  public override async Task OnCollision(Bullet.CollisionData collision, Bullet bullet)
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
