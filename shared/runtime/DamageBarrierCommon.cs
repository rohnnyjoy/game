#nullable enable

using Godot;

namespace Shared.Runtime
{
  public enum DamageBarrierDirectionality
  {
    Both,
    PositiveToNegative,
    NegativeToPositive,
  }

  public static class DamageBarrierUtilities
  {
    public const string GroupName = "damage_barriers";

    public static uint BarrierCollisionMask => PhysicsLayers.Mask(
      PhysicsLayers.Layer.SafeZone,
      PhysicsLayers.Layer.DamageBarrier
    );

    public static bool BlocksKind(DamageKind kind, bool blocksDirectProjectiles, bool blocksIndirectDamage)
    {
      return kind switch
      {
        DamageKind.Projectile => blocksDirectProjectiles,
        DamageKind.Contact => blocksDirectProjectiles,
        DamageKind.Explosion => blocksIndirectDamage,
        DamageKind.Chain => blocksIndirectDamage,
        _ => blocksIndirectDamage || blocksDirectProjectiles,
      };
    }

    public static bool PassesDirection(DamageBarrierDirectionality directionality, Vector3 from, Vector3 to, Vector3 normal)
    {
      float dot = normal.Dot(to - from);
      return directionality switch
      {
        DamageBarrierDirectionality.PositiveToNegative => dot <= 0.0f,
        DamageBarrierDirectionality.NegativeToPositive => dot >= 0.0f,
        _ => true,
      };
    }
  }
}
