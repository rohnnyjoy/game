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

  // Expose a test-friendly assert for other files in this assembly.
  public static void TAssert(bool condition, string message) => Assert(condition, message);

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

  private static WeaponModule CreateModule(string name)
  {
    return new WeaponModule
    {
      ModuleName = name,
      ModuleDescription = $"Module {name}"
    };
  }

  private static Weapon CreateWeapon()
  {
    var weapon = new Weapon
    {
      Modules = new Godot.Collections.Array<WeaponModule>()
    };
    return weapon;
  }

  private static void WithStore(Action<InventoryStore> action)
  {
    var store = new InventoryStore();
    store._EnterTree();
    try
    {
      action(store);
    }
    finally
    {
      store._ExitTree();
    }
  }

  private static void TestInventoryStoreInitialization()
  {
    WithStore(store =>
    {
      var weapon = CreateWeapon();
      var invModules = new[] { CreateModule("InvA"), CreateModule("InvB") };
      var primaryModules = new[] { CreateModule("PrimA") };

      int events = 0;
      store.StateChanged += (_, origin) =>
      {
        events++;
        Assert(origin == ChangeOrigin.System, "Initialization should report system origin");
      };

      store.Initialize(weapon, invModules, primaryModules, ChangeOrigin.System);

      Assert(events == 1, "Initialize should emit exactly one StateChanged event");
      Assert(store.State.InventoryModuleIds.Count == 2, "Inventory state should include both modules");
      Assert(store.State.PrimaryWeaponModuleIds.Count == 1, "Primary weapon state should include the provided module");
      Assert(weapon.Modules.Count == 1 && weapon.Modules[0] == primaryModules[0], "Weapon modules should match store state");
    });
  }

  private static void TestInventoryStoreMoveModule()
  {
    WithStore(store =>
    {
      var weapon = CreateWeapon();
      var invModules = new[] { CreateModule("InvA") };
      var primaryModules = new[] { CreateModule("PrimA") };
      store.Initialize(weapon, invModules, primaryModules, ChangeOrigin.System);

      bool hasInvId = store.TryGetModuleId(invModules[0], out string invId);
      bool hasPrimId = store.TryGetModuleId(primaryModules[0], out string primId);
      Assert(hasInvId && hasPrimId, "Module IDs should be registered after initialization");

      int events = 0;
      ChangeOrigin lastOrigin = ChangeOrigin.Unknown;
      store.StateChanged += (_, origin) =>
      {
        events++;
        lastOrigin = origin;
      };

      store.MoveModule(invId, StackKind.Inventory, StackKind.PrimaryWeapon, 1, ChangeOrigin.UI);

      Assert(events == 1, "MoveModule should emit exactly one StateChanged event");
      Assert(lastOrigin == ChangeOrigin.UI, "MoveModule should propagate UI origin");
      Assert(store.State.InventoryModuleIds.Count == 0, "Inventory should be empty after moving the module");
      Assert(store.State.PrimaryWeaponModuleIds.Count == 2, "Primary weapon should contain two modules");
      Assert(store.State.PrimaryWeaponModuleIds[0] == primId && store.State.PrimaryWeaponModuleIds[1] == invId, "Module order should reflect insertion index");
      Assert(weapon.Modules.Count == 2 && weapon.Modules[1] == invModules[0], "Weapon modules should stay in sync with store state");
    });
  }

  private static void TestInventoryStoreRemoveModule()
  {
    WithStore(store =>
    {
      var weapon = CreateWeapon();
      var module = CreateModule("Solo");
      store.Initialize(weapon, new[] { module }, Array.Empty<WeaponModule>(), ChangeOrigin.System);

      bool hasId = store.TryGetModuleId(module, out string moduleId);
      Assert(hasId, "Module should be registered before removal");

      int events = 0;
      store.StateChanged += (_, __) => events++;

      store.RemoveModule(moduleId, ChangeOrigin.Gameplay);

      Assert(events == 1, "RemoveModule should emit exactly one StateChanged event");
      Assert(store.State.InventoryModuleIds.Count == 0, "Inventory should be empty after removal");
      Assert(!store.TryGetModuleId(module, out _), "Catalog should prune removed module IDs");
    });
  }

  private static void RunAll()
  {
    TestBounceReflection();
    TestBounceMaxBounces();
    TestPierceContinuation();
    TestPierceExhausted();
    TestDefaultBehavior();
    TestInventoryStoreInitialization();
    TestInventoryStoreMoveModule();
    TestInventoryStoreRemoveModule();
    DragMathTests.TestDragMathAlignment();
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

internal static class DragMathTests
{
  public static void TestDragMathAlignment()
  {
    // Two slots of width 120 at X ranges [0..120] and [140..260], midpoints 60 and 200.
    var mids = new float[] { 60f, 200f };

    // Case: User grabbed near right edge of a wide icon (drawWidth=160, grab=144).
    // Mouse pointer is at X=244; visual center should be mouse - 144 + 80 = 180.
    float mouseX = 244f;
    float grabWithinDraw = 144f;
    float drawWidth = 160f;
    float visualCenter = DragMath.ComputeVisualCenterX(mouseX, grabWithinDraw, drawWidth);

    // Sanity: visual center should be offset left of the raw mouse X.
    Program.TAssert(Mathf.IsEqualApprox(visualCenter, 180f), "Visual center should be 180 for given inputs");

    int idxByVisual = DragMath.ComputeInsertIndex(mids, visualCenter);
    int idxByMouse = DragMath.ComputeInsertIndex(mids, mouseX);

    // Expected: based on sprite center at 180, insert before second slot (index 1).
    Program.TAssert(idxByVisual == 1, "Insert index by visual center should be 1");

    // Buggy behavior: using raw mouse X (244) would incorrectly insert after the second slot (index 2).
    Program.TAssert(idxByMouse == 2, "Insert index by raw mouse should be 2 (buggy reference)");
  }

  // No local assert; use Program.TAssert so failures increment the shared counter.
}
