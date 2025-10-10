#nullable enable

using System;
using Godot;

namespace Shared.Runtime
{
  public static class DamageSystem
  {
    public static DamageResult Apply(in DamageRequest request)
    {
      if (request.Target == null || !GodotObject.IsInstanceValid(request.Target))
        return DamageResult.Blocked;

      Vector3 originPos = ResolveOriginPosition(request);

      DamageResult result = InvokeReceiver(request);
      if (!result.Applied)
        return result;

      if (request.EmitGlobalEvent && GlobalEvents.Instance != null)
      {
        Vector3 dir = request.KnockbackDirection ?? (request.Target.GlobalTransform.Origin - originPos);
        if (dir.LengthSquared() > 0.000001f)
          dir = dir.Normalized();
        else
          dir = Vector3.Forward;

        float knockback = MathF.Max(0f, request.KnockbackStrength) * MathF.Max(0f, request.KnockbackScale);
        var snapshot = new global::BulletManager.ImpactSnapshot(
          damage: result.DamageDealt,
          knockbackScale: request.KnockbackScale,
          enemyHit: request.Target.IsInGroup("enemies"),
          enemyId: (ulong)request.Target.GetInstanceId(),
          hitPosition: request.Target.GlobalTransform.Origin,
          hitNormal: -dir,
          isCrit: false,
          critMultiplier: 1.0f
        );
        GlobalEvents.Instance.EmitDamageDealt(request.Target, snapshot, dir, knockback);
      }

      return result;
    }

    private static Vector3 ResolveOriginPosition(in DamageRequest request)
    {
      if (request.OriginPosition.HasValue)
        return request.OriginPosition.Value;
      if (request.SourcePosition.HasValue)
        return request.SourcePosition.Value;

      if (request.Source != null && GodotObject.IsInstanceValid(request.Source))
        return request.Source.GlobalTransform.Origin;

      return request.Target.GlobalTransform.Origin;
    }

    private static DamageResult InvokeReceiver(in DamageRequest request)
    {
      if (request.Target is IDamageReceiver receiver)
        return receiver.ReceiveDamage(request);

      if (request.Target.HasMethod("take_damage"))
      {
        request.Target.CallDeferred("take_damage", request.Amount);
        return new DamageResult(true, request.Amount, 0f, 0f);
      }

      return DamageResult.Blocked;
    }
  }
}
