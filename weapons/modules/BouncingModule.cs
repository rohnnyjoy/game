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
    // Store the original destroy_on_impact state.
    bool originalDestroyOnImpact = bullet.DestroyOnImpact;
    bullet.DestroyOnImpact = false;

    // Retrieve the collision normal.
    Vector3 normal = collision.Normal;

    // Update the bounce count.
    int bounceCount = (int)bullet.GetMeta("bounce_count");
    GD.Print("Bounce count: " + bounceCount);
    bounceCount++;
    bullet.SetMeta("bounce_count", bounceCount);

    // Apply damage reduction.
    bullet.Damage *= 1.0f - DamageReduction;

    bullet.Velocity = bullet.Velocity.Bounce(normal) * Bounciness;

    // Reset destroy on impact if maximum bounces are reached.
    if (bounceCount >= MaxBounces)
    {
      bullet.DestroyOnImpact = originalDestroyOnImpact;
    }

    await Task.CompletedTask;
  }
}
