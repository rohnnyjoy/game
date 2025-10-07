public readonly struct ScatterConfig
{
  public ScatterConfig(int duplicationCount, float spreadAngle, float damageFactor)
  {
    DuplicationCount = duplicationCount;
    SpreadAngle = spreadAngle;
    DamageFactor = damageFactor;
  }

  public int DuplicationCount { get; }
  public float SpreadAngle { get; }
  public float DamageFactor { get; }
}

public readonly struct BounceProviderConfig
{
  public BounceProviderConfig(float damageReduction, float bounciness, int maxBounces)
  {
    DamageReduction = damageReduction;
    Bounciness = bounciness;
    MaxBounces = maxBounces;
  }

  public float DamageReduction { get; }
  public float Bounciness { get; }
  public int MaxBounces { get; }
}

public readonly struct PierceProviderConfig
{
  public PierceProviderConfig(float damageReduction, float velocityFactor, int maxPenetrations, float cooldown)
  {
    DamageReduction = damageReduction;
    VelocityFactor = velocityFactor;
    MaxPenetrations = maxPenetrations;
    Cooldown = cooldown;
  }

  public float DamageReduction { get; }
  public float VelocityFactor { get; }
  public int MaxPenetrations { get; }
  public float Cooldown { get; }
}

public readonly struct ExplosiveProviderConfig
{
  public ExplosiveProviderConfig(float radius, float damageMultiplier)
  {
    Radius = radius;
    DamageMultiplier = damageMultiplier;
  }

  public float Radius { get; }
  public float DamageMultiplier { get; }
}

public readonly struct StickyProviderConfig
{
  public StickyProviderConfig(float duration, float collisionDamage)
  {
    Duration = duration;
    CollisionDamage = collisionDamage;
  }

  public float Duration { get; }
  public float CollisionDamage { get; }
}

public readonly struct HomingProviderConfig
{
  public HomingProviderConfig(float radius, float strength)
  {
    Radius = radius;
    Strength = strength;
  }

  public float Radius { get; }
  public float Strength { get; }
}

public readonly struct TrackingProviderConfig
{
  public TrackingProviderConfig(float strength, float maxRayDistance)
  {
    Strength = strength;
    MaxRayDistance = maxRayDistance;
  }

  public float Strength { get; }
  public float MaxRayDistance { get; }
}

public readonly struct AimbotProviderConfig
{
  public AimbotProviderConfig(float coneAngle, float verticalOffset, float radius, float lineWidth, float lineDuration)
  {
    ConeAngle = coneAngle;
    VerticalOffset = verticalOffset;
    Radius = radius;
    LineWidth = lineWidth;
    LineDuration = lineDuration;
  }

  public float ConeAngle { get; }
  public float VerticalOffset { get; }
  public float Radius { get; }
  public float LineWidth { get; }
  public float LineDuration { get; }
}

public interface IBounceProvider
{
  bool TryGetBounceConfig(out BounceProviderConfig config);
}

public interface IPierceProvider
{
  bool TryGetPierceConfig(out PierceProviderConfig config);
}

public interface IExplosiveProvider
{
  bool TryGetExplosiveConfig(out ExplosiveProviderConfig config);
}

public interface IStickyProvider
{
  bool TryGetStickyConfig(out StickyProviderConfig config);
}

public interface IHomingProvider
{
  bool TryGetHomingConfig(out HomingProviderConfig config);
}

public interface ITrackingProvider
{
  bool TryGetTrackingConfig(out TrackingProviderConfig config);
}

public interface IAimbotProvider
{
  bool TryGetAimbotConfig(out AimbotProviderConfig config);
}

public interface IScatterProvider
{
  bool TryGetScatterConfig(out ScatterConfig config);
}

public struct WeaponStats
{
  public float Damage;
  public float FireRate;      // Seconds between shots
  public float BulletSpeed;
  public float Accuracy;      // 0..1
  public float ReloadSpeed;   // Seconds to reload
  public int Ammo;
}

public interface IStatModifier
{
  void Modify(ref WeaponStats stats);
}
