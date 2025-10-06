using System;
using Godot;

internal static class Program
{
  private static int _failed;

  private static void Assert(bool condition, string message)
  {
    if (condition)
      return;
    _failed++;
    Console.Error.WriteLine($"[FAIL] {message}");
  }

  private static void TestBounceReflection()
  {
    var state = new BulletCollisionState
    {
      Position = Vector3.Zero,
      PrevPosition = Vector3.Zero,
      Velocity = new Vector3(0, -5, 0),
      Damage = 10f,
      BounceCount = 0,
      PenetrationCount = 0,
      LastColliderId = 0,
      CollisionCooldown = 0,
    };

    var config = BulletBehaviorConfig.Create(
      new BounceConfig(damageReduction: 0.2f, bounciness: 0.5f, maxBounces: 3),
      null);

    var ctx = new CollisionContext(
      hitPosition: Vector3.Zero,
      hitNormal: Vector3.Up,
      nextPosition: new Vector3(0, -1, 0),
      colliderId: 42,
      isEnemy: false,
      radius: 0.1f,
      defaultDeactivate: true);

    bool deactivate = BulletCollisionProcessor.ProcessCollision(ref state, config, ctx);

    Assert(!deactivate, "Bounce should keep bullet alive");
    Assert(state.BounceCount == 1, "Bounce count should increment");
    Assert(Mathf.IsEqualApprox(state.Damage, 8f), "Damage should reduce by 20%");
    Assert(state.Velocity.Y > 0, "Velocity should reflect upwards");
    Assert(state.Position.Y > 0, "Position should be nudged along normal");
  }

  private static void TestBounceMaxBounces()
  {
    var state = new BulletCollisionState
    {
      Position = Vector3.Zero,
      PrevPosition = Vector3.Zero,
      Velocity = new Vector3(1, -1, 0),
      Damage = 5f,
      BounceCount = 3,
      PenetrationCount = 0,
      LastColliderId = 0,
      CollisionCooldown = 0,
    };

    var config = BulletBehaviorConfig.Create(
      new BounceConfig(damageReduction: 0.1f, bounciness: 0.8f, maxBounces: 3),
      null);

    var ctx = new CollisionContext(Vector3.Zero, Vector3.Up, Vector3.Zero, 0, false, 0.1f, defaultDeactivate: true);

    bool deactivate = BulletCollisionProcessor.ProcessCollision(ref state, config, ctx);
    Assert(deactivate, "Bullet should deactivate once max bounces reached");
  }

  private static void TestPierceContinuation()
  {
    var state = new BulletCollisionState
    {
      Position = Vector3.Zero,
      PrevPosition = Vector3.Zero,
      Velocity = new Vector3(0, 0, 10),
      Damage = 20f,
      BounceCount = 0,
      PenetrationCount = 0,
      LastColliderId = 0,
      CollisionCooldown = 0,
    };

    var config = BulletBehaviorConfig.Create(
      null,
      new PierceConfig(damageReduction: 0.25f, velocityFactor: 0.5f, maxPenetrations: 2, cooldown: 0.1f));

    var ctx = new CollisionContext(Vector3.Zero, Vector3.Zero, new Vector3(0, 0, 1), 99, true, 0.1f, defaultDeactivate: true);

    bool deactivate = BulletCollisionProcessor.ProcessCollision(ref state, config, ctx);

    Assert(!deactivate, "Piercing enemy should keep bullet alive");
    Assert(state.PenetrationCount == 1, "Penetration count should increment");
    Assert(Mathf.IsEqualApprox(state.Damage, 15f), "Damage should reduce by 25%");
    Assert(state.Velocity.Z > 0 && state.Velocity.Z < 10f, "Velocity should be scaled down");
    Assert(state.Position.Z > 0f, "Bullet should advance past hit point");
    Assert(Mathf.IsEqualApprox(state.CollisionCooldown, 0.1f), "Cooldown should be applied");
  }

  private static void TestPierceExhausted()
  {
    var state = new BulletCollisionState
    {
      Position = Vector3.Zero,
      PrevPosition = Vector3.Zero,
      Velocity = new Vector3(0, 0, 5),
      Damage = 8f,
      BounceCount = 0,
      PenetrationCount = 2,
      LastColliderId = 0,
      CollisionCooldown = 0,
    };

    var config = BulletBehaviorConfig.Create(
      null,
      new PierceConfig(damageReduction: 0.2f, velocityFactor: 0.8f, maxPenetrations: 2, cooldown: 0.1f));

    var ctx = new CollisionContext(Vector3.Forward, Vector3.Zero, Vector3.Forward, 10, true, 0.1f, defaultDeactivate: true);

    bool deactivate = BulletCollisionProcessor.ProcessCollision(ref state, config, ctx);
    Assert(deactivate, "Bullet should deactivate once piercing limit reached");
    Assert(Mathf.IsEqualApprox(state.Position.Z, Vector3.Forward.Z), "Position should snap to hit point");
  }

  private static void TestDefaultBehavior()
  {
    var state = new BulletCollisionState
    {
      Position = new Vector3(1, 1, 1),
      PrevPosition = Vector3.Zero,
      Velocity = new Vector3(1, 0, 0),
      Damage = 5f,
    };

    var ctx = new CollisionContext(Vector3.Zero, Vector3.Up, Vector3.Right, 0, false, 0.1f, defaultDeactivate: true);
    bool deactivate = BulletCollisionProcessor.ProcessCollision(ref state, BulletBehaviorConfig.None, ctx);
    Assert(deactivate, "Without modifiers bullet should deactivate");
    Assert(state.Position == Vector3.Zero, "Position should snap to collision point");
  }

  private static void RunAll()
  {
    TestBounceReflection();
    TestBounceMaxBounces();
    TestPierceContinuation();
    TestPierceExhausted();
    TestDefaultBehavior();
  }

  public static int Main()
  {
    RunAll();
    if (_failed == 0)
    {
      Console.WriteLine("All BulletCollisionProcessor tests passed.");
      return 0;
    }

    Console.Error.WriteLine($"{_failed} test(s) failed.");
    return 1;
  }
}
