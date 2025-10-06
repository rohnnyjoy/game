using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Bullet : Node3D
{
  public class CollisionData
  {
    public Vector3 Position;
    public Vector3 Normal;
    public ulong ColliderId;
    public Node3D Collider;
    public Rid Rid;
    public float TotalDamageDealt;
  }

  [Export] public float LifeTime { get; set; } = 30.0f;
  [Export] public float Gravity { get; set; } = 0.0f;
  [Export] public Vector3 Direction { get; set; } = Vector3.Forward;
  [Export] public float Speed { get; set; } = 20.0f;
  [Export] public float Damage { get; set; } = 1.0f;
  [Export] public float KnockbackStrength { get; set; } = 3.5f;
  [Export] public Color Color { get; set; } = Colors.White;
  [Export] public bool DestroyOnImpact { get; set; } = true;
  [Export] public Vector3 InitialPosition { get; private set; }
  [Export] public float TraveledDistance { get; private set; } = 0.0f;
  [Export] public float TrailCleanupDelay { get; set; } = 2.0f;
  [Export] public bool EnableOverlapCollision { get; set; } = true;
  [Export] public float OverlapCollisionDelay { get; set; } = 0.5f;
  [Export] public float ColorChangeFactor { get; set; } = 40f;

  // New exported property to set max allowed distance from the player.
  [Export] public float MaxDistanceFromPlayer { get; set; } = 100.0f;

  // Note: The Rubble property was previously used to spawn rubble directly.
  // Now we are delegating rubble spawning to environment child nodes (RubbleSpawner).
  [Export] public PackedScene Rubble { get; set; }

  private float _overlapTimer = 0.0f;
  private float _radius = 0.5f;
  [Export]
  public float Radius
  {
    get => _radius;
    set
    {
      _radius = value;
      foreach (var trail in Trails)
        trail.BaseWidth = value;
    }
  }

  [Export] public Array<Trail> Trails { get; set; } = new Array<Trail>();
  [Export] public Gradient TrailGradient { get; set; } = new();
  [Export] public Array<BulletModifier> Modifiers { get; set; } = new Array<BulletModifier>();

  private MeshInstance3D _mesh;
  private ulong _lastCollisionColliderId = 0;
  [Export] public Vector3 Velocity;

  // Reference to the player found by group.
  private Node3D player;

  // New flag to track inert state.
  private bool isInert = false;
  [Export] public Array<PackedScene> CollisionParticles { get; set; } = new Array<PackedScene>();

  public override async void _Ready()
  {
    Scale = Vector3.One;
    GD.Randomize();

    // Attempt to locate the player automatically via the "players" group.
    var players = GetTree().GetNodesInGroup("players");
    if (players.Count > 0)
    {
      player = players[0] as Node3D;
    }

    InitialPosition = GlobalPosition;

    // Setup collision shape.
    var collisionShape = new CollisionShape3D
    {
      Shape = new SphereShape3D { Radius = Radius + 0.5f }
    };
    AddChild(collisionShape);

    Velocity = Direction.Normalized() * Speed;

    foreach (var modifier in Modifiers)
    {
      await modifier.OnFire(this);
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    // Skip processing if bullet is inert.
    if (isInert)
      return;

    float dt = (float)delta;

    Transform3D currentTransform = GlobalTransform;
    Vector3 currentPosition = currentTransform.Origin;

    Velocity += Vector3.Down * Gravity * dt;
    TraveledDistance = InitialPosition.DistanceTo(currentPosition);

    if (player != null)
    {
      float distanceFromPlayer = GlobalPosition.DistanceTo(player.GlobalPosition);
      if (distanceFromPlayer > MaxDistanceFromPlayer)
      {
        Deactivate();
        return;
      }
    }

    Vector3 predictedMotion = Velocity * dt;
    Vector3 predictedPosition = currentPosition + predictedMotion;

    var query = new PhysicsRayQueryParameters3D
    {
      From = currentPosition,
      To = predictedPosition,
      Exclude = GetChildCollisionRIDs()
    };

    var collision = GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (collision.Count > 0 && collision.ContainsKey("collider"))
    {
      // Convert the collision variant to Node3D.
      Node3D hit = collision["collider"].As<Node3D>();
      if (hit != null && hit.IsInGroup("enemies") && ((ulong)collision["collider_id"]) == _lastCollisionColliderId)
      {
        GlobalTransform = new Transform3D(currentTransform.Basis, predictedPosition);
      }
      else
      {
        GlobalTransform = new Transform3D(currentTransform.Basis, (Vector3)collision["position"]);
        _lastCollisionColliderId = (ulong)collision["collider_id"];

        CollisionData collisionData = new CollisionData
        {
          Position = (Vector3)collision["position"],
          Normal = (Vector3)collision["normal"],
          ColliderId = (ulong)collision["collider_id"],
          Collider = collision["collider"].As<Node3D>(),
          Rid = (Rid)collision["rid"]
        };

        // Delegate to modifiers.
        DestroyOnImpact = true;
        foreach (var modifier in Modifiers)
          _ = modifier.OnCollision(this, collisionData);

        // ★ Integration: Let the hit object’s RubbleSpawner handle rubble emission.
        if (collisionData.Collider != null)
        {
          RubbleSpawner rubbleSpawner = FindRubbleSpawner(collisionData.Collider);
          if (rubbleSpawner != null)
          {
            // Use the bullet's Damage and the collision normal as impact direction.
            rubbleSpawner.EmitRubbleAt(collisionData.Position, Velocity, collisionData.Normal, Damage);
          }
        }

        // Broadcast impact for consistent FX handling.
        GD.Print($"[Bullet] impact hit collider={collisionData.Collider?.Name} at ({collisionData.Position.X:0.00},{collisionData.Position.Y:0.00},{collisionData.Position.Z:0.00}) normal=({collisionData.Normal.X:0.00},{collisionData.Normal.Y:0.00},{collisionData.Normal.Z:0.00})");
        Vector3 travelDir = Velocity.LengthSquared() > 0.000001f ? Velocity.Normalized() : (collisionData.Normal != Vector3.Zero ? -collisionData.Normal.Normalized() : Vector3.Forward);
        GlobalEvents.Instance?.EmitImpactOccurred(collisionData.Position, collisionData.Normal, travelDir);

        // Call any additional collision response.
        DefaultBulletCollision(collisionData, this);
        // SpawnCollisionParticles(collisionData);

        if (DestroyOnImpact)
        {
          Deactivate();
          return;
        }
      }
    }
    else
    {
      GlobalTransform = new Transform3D(currentTransform.Basis, predictedPosition);
    }

    foreach (var modifier in Modifiers)
      _ = modifier.OnUpdate(this, dt);

    // await _ProcessOverlapCollisions(dt);
  }

  private async Task _ProcessOverlapCollisions(float dt)
  {
    if (!EnableOverlapCollision)
      return;

    bool overlapping = false;
    Node3D collidedBody = null;

    foreach (Node child in GetChildren())
    {
      if (child is Area3D area)
      {
        var bodies = area.GetOverlappingBodies();
        foreach (var body in bodies)
        {
          if (body is Node3D n)
          {
            overlapping = true;
            collidedBody = n;
            break;
          }
        }
      }
      if (overlapping)
        break;
    }

    if (overlapping)
    {
      _overlapTimer += dt;
      if (_overlapTimer >= OverlapCollisionDelay)
      {
        Rid rid = default;
        if (collidedBody is CollisionObject3D collisionObj)
          rid = collisionObj.GetRid();

        CollisionData collisionData = new CollisionData
        {
          Position = GlobalPosition,
          Normal = Vector3.Up,
          ColliderId = (ulong)collidedBody.GetInstanceId(),
          Collider = collidedBody,
          Rid = rid,
          TotalDamageDealt = 0
        };

        DestroyOnImpact = true;
        foreach (var modifier in Modifiers)
          await modifier.OnCollision(this, collisionData);

        DefaultBulletCollision(collisionData, this);
        SpawnCollisionParticles(collisionData);
        Vector3 travelDir2 = Velocity.LengthSquared() > 0.000001f ? Velocity.Normalized() : (collisionData.Normal != Vector3.Zero ? -collisionData.Normal.Normalized() : Vector3.Forward);
        GlobalEvents.Instance?.EmitImpactOccurred(collisionData.Position, collisionData.Normal, travelDir2);

        if (DestroyOnImpact)
        {
          Deactivate();
          return;
        }

        _overlapTimer = 0.0f;
      }
    }
    else
    {
      _overlapTimer = 0.0f;
    }
  }

  private void DefaultBulletCollision(CollisionData collision, Bullet bullet)
  {
    collision.TotalDamageDealt += Damage;
    try
    {
      Node3D hit = collision.Collider;
      if (!IsInstanceValid(hit))
        return;
      if (hit != null && hit is Enemy && IsInstanceValid(hit))
      {
        Enemy enemy = hit as Enemy;
        enemy.TakeDamage(collision.TotalDamageDealt);

        // Emit damage for global knockback handling
        Vector3 dir = Velocity.LengthSquared() > 0.000001f ? Velocity.Normalized() : (enemy.GlobalTransform.Origin - GlobalTransform.Origin).Normalized();
        dir = new Vector3(dir.X, 0.15f * dir.Y, dir.Z).Normalized();
        GlobalEvents.Instance?.EmitDamageDealt(enemy, collision.TotalDamageDealt, dir * Mathf.Max(0.0f, KnockbackStrength));

        // Compute UI screen shake based on total damage (pixels).
        GameUi.Instance?.AddJiggle(Mathf.Clamp(collision.TotalDamageDealt * 0.01f, 0.2f, 2.0f));
        Player.Instance.CameraShake.TriggerShake(0.05f, Mathf.Clamp(collision.TotalDamageDealt * 0.05f, 0.1f, 0.3f));

        // Spawn damage number above the enemy
        DamageNumber3D.Spawn(this, enemy, collision.TotalDamageDealt);
      }
    }
    catch (Exception e)
    {
      GD.PrintErr(e.Message);
    }
  }

  private void SpawnCollisionParticles(CollisionData collision)
  {
    foreach (var particleScene in CollisionParticles)
    {
      GpuParticles3D particles = particleScene.Instantiate<GpuParticles3D>();
      GetTree().CurrentScene.AddChild(particles);
      particles.GlobalPosition = collision.Position;
      if (Velocity.Length() > 0.001f)
      {
        Vector3 desiredDirection = collision.Normal.Normalized();
        particles.LookAt(particles.GlobalPosition + desiredDirection, Vector3.Up);
        particles.Rotate(Vector3.Up, Mathf.Pi / 2);
      }
      particles.Emitting = true;
    }

    float rand = GD.Randf();
    int baseLowPolyCount = (rand > 0.9f) ? 3 : (rand > 0.8f) ? 2 : (rand > 0.6f) ? 1 : 0;
    int countLowPoly = (int)TriangularScaleDamage(0, baseLowPolyCount, collision.TotalDamageDealt, 0, 200);
    for (int i = 0; i < countLowPoly; i++)
    {
      // Direct rubble spawn as a fallback.
      // RigidBody3D rubble = Rubble.Instantiate() as RigidBody3D;
      // float offsetDistance = 0.2f;
      // Vector3 basePosition = collision.Position + collision.Normal * offsetDistance;
      // Vector3 tangent = collision.Normal.Cross(Vector3.Up);
      // if (tangent.Length() < 0.001f)
      //   tangent = collision.Normal.Cross(Vector3.Right);
      // tangent = tangent.Normalized();
      // Vector3 bitangent = collision.Normal.Cross(tangent).Normalized();
      // float randomMagnitude = 0.1f;
      // Vector3 randomOffset = tangent * (GD.Randf() * randomMagnitude - randomMagnitude * 0.5f)
      //                     + bitangent * (GD.Randf() * randomMagnitude - randomMagnitude * 0.5f);
      // rubble.GlobalPosition = basePosition + randomOffset;
      // Vector3 shootDir = collision.Normal;
      // Vector3 impulseVariation = tangent * (GD.Randf() * 0.2f - 0.1f)
      //                        + bitangent * (GD.Randf() * 0.2f - 0.1f);
      // shootDir = (shootDir + impulseVariation).Normalized();
      // rubble.LinearVelocity = shootDir * 5;
      // GetTree().CurrentScene.AddChild(rubble);
    }
  }

  private float TriangularScaleDamage(float minValue, float maxValue, float damage, float damageThreshold, float damageCap)
  {
    if (damage < damageThreshold)
    {
      return minValue;
    }

    // Normalize the damage relative to the threshold and cap.
    float normalized = Mathf.Clamp((damage - damageThreshold) / (damageCap - damageThreshold), 0, 1);

    // Compute the range between max and min.
    float range = maxValue - minValue;

    // Set the mode for the triangular distribution within the new range.
    float mode = range * normalized;

    // Generate a triangular random value within [0, range] and then add minValue to shift it.
    return minValue + (float)TriangularRandom(0, range, mode);
  }

  public static double TriangularRandom(double low, double high, double mode)
  {
    double u = GD.Randf();
    double c = (mode - low) / (high - low);
    if (u < c)
    {
      return low + Math.Sqrt(u * (high - low) * (mode - low));
    }
    else
    {
      return high - Math.Sqrt((1 - u) * (high - low) * (high - mode));
    }
  }

  private StandardMaterial3D GetTextureFromCollider(Node3D collider)
  {
    if (collider is Bullet)
      return null;

    if (collider is GeometryInstance3D geom)
    {
      if (geom.MaterialOverride is StandardMaterial3D mat)
        return mat;
      if (geom is MeshInstance3D mi && mi.Mesh != null)
      {
        var surfMat = mi.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
        if (surfMat != null)
        {
          return surfMat;
        }
      }
    }
    foreach (Node child in collider.GetChildren())
    {
      if (child is Node3D childNode)
      {
        StandardMaterial3D found = GetTextureFromCollider(childNode);
        if (found != null)
          return found;
      }
    }
    return null;
  }

  private Vector3 GetRandomHemisphereDirection(Vector3 normal)
  {
    Vector3 randomDir = new Vector3(GD.Randf() * 2 - 1, GD.Randf() * 2 - 1, GD.Randf() * 2 - 1).Normalized();
    if (randomDir.Dot(normal) < 0)
      randomDir = -randomDir;
    return randomDir;
  }

  public Array<Rid> GetChildCollisionRIDs()
  {
    var excludeArray = new Array<Rid>();
    foreach (Node child in GetChildren())
    {
      if (child is CollisionObject3D collisionObj)
      {
        excludeArray.Add(collisionObj.GetRid());
      }
    }
    return excludeArray;
  }

  /// <summary>
  /// Recursively searches for a RubbleSpawner node within the given parent.
  /// </summary>
  private RubbleSpawner FindRubbleSpawner(Node parent)
  {
    foreach (Node child in parent.GetChildren())
    {
      if (child is RubbleSpawner spawner)
        return spawner;

      RubbleSpawner recursive = FindRubbleSpawner(child);
      if (recursive != null)
        return recursive;
    }
    return null;
  }

  public void Deactivate()
  {
    Velocity = Vector3.Zero;
    foreach (Node child in GetChildren())
    {
      if (child is CollisionShape3D cs)
        cs.Disabled = true;
      else if (child is Area3D area)
        area.Monitoring = false;
    }
    isInert = true;
  }

  public void Reset()
  {
    // Reset runtime state
    Velocity = Vector3.Zero;
    TraveledDistance = 0.0f;
    _overlapTimer = 0.0f;
    isInert = false;
    _lastCollisionColliderId = 0;

    // Clear any runtime modifiers.
    Modifiers.Clear();
    Position = InitialPosition;

    // Re-enable collision shapes and areas in case they were disabled.
    foreach (Node child in GetChildren())
    {
      if (child is CollisionShape3D cs)
        cs.Disabled = false;
      else if (child is Area3D area)
        area.Monitoring = true;
      // If the bullet has an attached trail, reset it to avoid snapping.
      else if (child is RibbonTrailEmitter trail)
      {
        trail.Reset();
      }
    }

    foreach (var key in GetMetaList())
    {
      RemoveMeta(key.ToString());
    }
  }
}
