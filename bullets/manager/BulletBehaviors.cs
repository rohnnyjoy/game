using Godot;

#nullable enable

internal sealed class BulletBehaviorConfig
{
  public static readonly BulletBehaviorConfig None = new BulletBehaviorConfig(null, null, null, null, null, null, null);

  public BounceConfig? Bounce { get; }
  public PierceConfig? Pierce { get; }
  public HomingConfig? Homing { get; }
  public TrackingConfig? Tracking { get; }
  public AimbotConfig? Aimbot { get; }
  public ExplosiveConfig? Explosive { get; }
  public StickyConfig? Sticky { get; }

  private BulletBehaviorConfig(
    BounceConfig? bounce,
    PierceConfig? pierce,
    HomingConfig? homing,
    TrackingConfig? tracking,
    AimbotConfig? aimbot,
    ExplosiveConfig? explosive,
    StickyConfig? sticky)
  {
    Bounce = bounce;
    Pierce = pierce;
    Homing = homing;
    Tracking = tracking;
    Aimbot = aimbot;
    Explosive = explosive;
    Sticky = sticky;
  }

  public static BulletBehaviorConfig Create(
    BounceConfig? bounce,
    PierceConfig? pierce,
    HomingConfig? homing = null,
    TrackingConfig? tracking = null,
    AimbotConfig? aimbot = null,
    ExplosiveConfig? explosive = null,
    StickyConfig? sticky = null)
  {
    if (bounce is null && pierce is null && homing is null && tracking is null && aimbot is null && explosive is null && sticky is null)
      return None;
    return new BulletBehaviorConfig(bounce, pierce, homing, tracking, aimbot, explosive, sticky);
  }
}

internal sealed class BounceConfig
{
  public float DamageReduction { get; }
  public float Bounciness { get; }
  public int MaxBounces { get; }

  public BounceConfig(float damageReduction, float bounciness, int maxBounces)
  {
    DamageReduction = damageReduction;
    Bounciness = bounciness;
    MaxBounces = Mathf.Max(0, maxBounces);
  }
}

internal sealed class PierceConfig
{
  public float DamageReduction { get; }
  public float VelocityFactor { get; }
  public int MaxPenetrations { get; }
  public float Cooldown { get; }

  public PierceConfig(float damageReduction, float velocityFactor, int maxPenetrations, float cooldown)
  {
    DamageReduction = damageReduction;
    VelocityFactor = velocityFactor;
    MaxPenetrations = Mathf.Max(0, maxPenetrations);
    Cooldown = Mathf.Max(0.0f, cooldown);
  }
}

internal sealed class HomingConfig
{
  public float Radius { get; }
  public float Strength { get; }

  public HomingConfig(float radius, float strength)
  {
    Radius = Mathf.Max(0.0f, radius);
    Strength = Mathf.Clamp(strength, 0.0f, 1.0f);
  }
}

internal sealed class TrackingConfig
{
  public float Strength { get; }
  public float MaxRayDistance { get; }

  public TrackingConfig(float strength, float maxRayDistance)
  {
    Strength = Mathf.Clamp(strength, 0.0f, 1.0f);
    MaxRayDistance = Mathf.Max(0.0f, maxRayDistance);
  }
}

internal sealed class AimbotConfig
{
  public float AimConeAngle { get; }
  public float VerticalOffset { get; }
  public float Radius { get; }
  public float LineWidth { get; }
  public float LineDuration { get; }

  public AimbotConfig(float aimConeAngle, float verticalOffset, float radius, float lineWidth, float lineDuration)
  {
    AimConeAngle = Mathf.Max(0.0f, aimConeAngle);
    VerticalOffset = verticalOffset;
    Radius = Mathf.Max(0.0f, radius);
    LineWidth = Mathf.Max(0.0f, lineWidth);
    LineDuration = Mathf.Max(0.0f, lineDuration);
  }
}

internal sealed class ExplosiveConfig
{
  public float Radius { get; }
  public float DamageMultiplier { get; }

  public ExplosiveConfig(float radius, float damageMultiplier)
  {
    Radius = Mathf.Max(0.0f, radius);
    DamageMultiplier = Mathf.Max(0.0f, damageMultiplier);
  }
}

internal sealed class StickyConfig
{
  public float Duration { get; }
  public float CollisionDamage { get; }

  public StickyConfig(float duration, float collisionDamage)
  {
    Duration = Mathf.Max(0.0f, duration);
    CollisionDamage = Mathf.Max(0.0f, collisionDamage);
  }
}

internal struct BulletCollisionState
{
  public Vector3 Position;
  public Vector3 PrevPosition;
  public Vector3 Velocity;
  public float Damage;
  public int BounceCount;
  public int PenetrationCount;
  public ulong LastColliderId;
  public float CollisionCooldown;
}

internal readonly struct CollisionContext
{
  public Vector3 HitPosition { get; }
  public Vector3 HitNormal { get; }
  public Vector3 NextPosition { get; }
  public ulong ColliderId { get; }
  public bool IsEnemy { get; }
  public float Radius { get; }
  public bool DefaultDeactivate { get; }

  public CollisionContext(Vector3 hitPosition, Vector3 hitNormal, Vector3 nextPosition, ulong colliderId, bool isEnemy, float radius, bool defaultDeactivate)
  {
    HitPosition = hitPosition;
    HitNormal = hitNormal;
    NextPosition = nextPosition;
    ColliderId = colliderId;
    IsEnemy = isEnemy;
    Radius = radius;
    DefaultDeactivate = defaultDeactivate;
  }
}

internal static class BulletCollisionProcessor
{
  public static bool ProcessCollision(ref BulletCollisionState bullet, BulletBehaviorConfig behavior, in CollisionContext context)
  {
    bullet.LastColliderId = context.ColliderId;
    bullet.CollisionCooldown = 0.0f;

    bool deactivate = context.DefaultDeactivate;

    if (behavior != null)
    {
      if (behavior.Pierce is PierceConfig pierce && context.IsEnemy && bullet.PenetrationCount < pierce.MaxPenetrations)
      {
        bullet.PenetrationCount++;
        bullet.Damage *= (1.0f - pierce.DamageReduction);
        bullet.Velocity *= pierce.VelocityFactor;
        Vector3 forward = bullet.Velocity.LengthSquared() > 0.0001f
          ? bullet.Velocity.Normalized()
          : (context.NextPosition - bullet.Position).Normalized();
        if (forward.LengthSquared() < 0.0001f)
          forward = Vector3.Forward;
        float epsilon = Mathf.Max(0.01f, context.Radius);
        bullet.Position = context.HitPosition + forward * epsilon;
        bullet.PrevPosition = bullet.Position;
        bullet.CollisionCooldown = pierce.Cooldown;
        deactivate = false;
        return deactivate;
      }

      if (behavior.Bounce is BounceConfig bounce && bullet.BounceCount < bounce.MaxBounces && context.HitNormal.LengthSquared() > 0.0001f)
      {
        Vector3 normal = context.HitNormal.Normalized();
        bullet.BounceCount++;
        bullet.Velocity = bullet.Velocity.Bounce(normal) * bounce.Bounciness;
        bullet.Damage *= (1.0f - bounce.DamageReduction);
        float epsilon = Mathf.Max(0.01f, context.Radius);
        bullet.Position = context.HitPosition + normal * epsilon;
        bullet.PrevPosition = bullet.Position;
        deactivate = false;
        return deactivate;
      }
    }

    bullet.Position = context.HitPosition;
    return deactivate;
  }
}
