using Godot;

public partial class BulletManager
{
  // Effect pipeline (scoped to BulletManager to access private BulletData)
  private interface IBulletEffect
  {
    void OnCollision(ref BulletData b, in CollisionContext ctx, ref bool motionResolved);
    void OnTick(ref BulletData b, Archetype arch, float dt) { }
  }

  // Bounce adapter -> uses BulletCollisionProcessor with bounce-only config
  private sealed class BounceEffect : IBulletEffect
  {
    private readonly BounceConfig _cfg;
    public BounceEffect(BounceConfig cfg) { _cfg = cfg; }

    public void OnCollision(ref BulletData b, in CollisionContext ctx, ref bool motionResolved)
    {
      if (ctx.HitNormal.LengthSquared() < 0.0001f)
        return;
      var state = new BulletCollisionState
      {
        Position = b.Position,
        PrevPosition = b.PrevPosition,
        Velocity = b.Velocity,
        Damage = b.Damage,
        BounceCount = b.BounceCount,
        PenetrationCount = b.PenetrationCount,
        LastColliderId = b.LastColliderId,
        CollisionCooldown = b.CollisionCooldown,
      };
      var cfg = BulletBehaviorConfig.Create(_cfg, null);
      // Default deactivate = false here (we want to keep bullet alive on bounce)
      var ctxNoDeactivate = new CollisionContext(ctx.HitPosition, ctx.HitNormal, ctx.NextPosition, ctx.ColliderId, ctx.IsEnemy, ctx.Radius, false);
      _ = BulletCollisionProcessor.ProcessCollision(ref state, cfg, ctxNoDeactivate);

      // If bounce count increased, treat as resolved
      if (state.BounceCount > b.BounceCount)
      {
        b.Position = state.Position;
        b.PrevPosition = state.PrevPosition;
        b.Velocity = state.Velocity;
        b.Damage = state.Damage;
        b.BounceCount = state.BounceCount;
        b.PenetrationCount = state.PenetrationCount;
        b.LastColliderId = state.LastColliderId;
        b.CollisionCooldown = state.CollisionCooldown;
        motionResolved = true;
      }
    }
  }

  // Pierce adapter -> uses BulletCollisionProcessor with pierce-only config
  private sealed class PierceEffect : IBulletEffect
  {
    private readonly PierceConfig _cfg;
    public PierceEffect(PierceConfig cfg) { _cfg = cfg; }

    public void OnCollision(ref BulletData b, in CollisionContext ctx, ref bool motionResolved)
    {
      if (!ctx.IsEnemy)
        return;
      var state = new BulletCollisionState
      {
        Position = b.Position,
        PrevPosition = b.PrevPosition,
        Velocity = b.Velocity,
        Damage = b.Damage,
        BounceCount = b.BounceCount,
        PenetrationCount = b.PenetrationCount,
        LastColliderId = b.LastColliderId,
        CollisionCooldown = b.CollisionCooldown,
      };
      var cfg = BulletBehaviorConfig.Create(null, _cfg);
      // Default deactivate = false to allow continuation
      var ctxNoDeactivate = new CollisionContext(ctx.HitPosition, ctx.HitNormal, ctx.NextPosition, ctx.ColliderId, ctx.IsEnemy, ctx.Radius, false);
      _ = BulletCollisionProcessor.ProcessCollision(ref state, cfg, ctxNoDeactivate);

      if (state.PenetrationCount > b.PenetrationCount)
      {
        b.Position = state.Position;
        b.PrevPosition = state.PrevPosition;
        b.Velocity = state.Velocity;
        b.Damage = state.Damage;
        b.BounceCount = state.BounceCount;
        b.PenetrationCount = state.PenetrationCount;
        b.LastColliderId = state.LastColliderId;
        b.CollisionCooldown = state.CollisionCooldown;
        motionResolved = true;
      }
    }
  }

  // Explode adapter -> triggers AOE via manager helper (no motion resolution)
  private sealed class ExplodeEffect : IBulletEffect
  {
    private readonly ExplosiveConfig _cfg;
    private readonly BulletManager _owner;
    public ExplodeEffect(BulletManager owner, ExplosiveConfig cfg) { _owner = owner; _cfg = cfg; }
    public void OnCollision(ref BulletData b, in CollisionContext ctx, ref bool motionResolved)
    {
      _owner.ApplyExplosionAOE(ctx.HitPosition, _cfg, b.Damage);
    }
  }
}
