using Godot;
using System;
using Godot.Collections;
using System.Threading.Tasks;

public partial class PenetratingModule : WeaponModule
{
  [Export]
  public float DamageReduction { get; set; } = 0.2f;
  [Export]
  public float VelocityFactor { get; set; } = 0.9f;
  [Export]
  public int MaxPenetrations { get; set; } = 5;
  [Export]
  public float CollisionCooldown { get; set; } = 0.2f;

  public PenetratingModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/penetrating.png");
    Rarity = Rarity.Uncommon;
    ModuleDescription = "Bullets can penetrate multiple enemies, reducing damage with each hit.";
  }

  public override Bullet ModifyBullet(Bullet bullet)
  {
    if (!bullet.HasMeta("penetration_count"))
    {
      bullet.SetMeta("penetration_count", 0);
    }
    return bullet;
  }

  public override async Task OnCollision(Bullet.CollisionData collisionData, Bullet bullet)
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
