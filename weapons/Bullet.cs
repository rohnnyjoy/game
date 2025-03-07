using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Bullet : Area3D
{
  // Shared resources loaded only once per class
  private static Mesh sharedSphereMesh;
  private static StandardMaterial3D sharedMaterial;

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

  [Export] public Godot.Collections.Array<Trail> Trails { get; set; } = new Godot.Collections.Array<Trail>();
  [Export] public Gradient TrailGradient { get; set; } = new();
  [Export] public Godot.Collections.Array<WeaponModule> Modules { get; set; } = new Godot.Collections.Array<WeaponModule>();

  private MeshInstance3D _mesh;
  private ulong _lastCollisionColliderId = 0;
  private List<Action<CollisionData, Bullet>> CollisionHandlers = new List<Action<CollisionData, Bullet>>();
  [Export] public Vector3 Velocity;

  // Reference to the player found by group.
  private Node3D player;

  public override void _Ready()
  {
    Scale = Vector3.One;
    GD.Randomize();
    RotationDegrees = new Vector3(GD.Randf() * 360, GD.Randf() * 360, GD.Randf() * 360);

    // Attempt to locate the player automatically via the "players" group.
    var players = GetTree().GetNodesInGroup("players");
    if (players.Count > 0)
    {
      player = players[0] as Node3D;
    }

    // Load shared mesh and material if not already loaded
    if (sharedSphereMesh == null)
    {
      SphereMesh sphereMesh = new SphereMesh
      {
        Radius = Radius,
        Height = Radius * 2,
        RadialSegments = 4,
        Rings = 2
      };
      sharedSphereMesh = sphereMesh;
    }
    if (sharedMaterial == null)
    {
      sharedMaterial = new StandardMaterial3D
      {
        AlbedoColor = Color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
      };
    }

    // Create and add the mesh instance using the shared resources.
    _mesh = new MeshInstance3D
    {
      Mesh = sharedSphereMesh,
      Scale = Vector3.One,
      MaterialOverride = sharedMaterial,
      CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    };
    AddChild(_mesh);

    InitialPosition = GlobalPosition;

    // Setup collision shape.
    var collisionShape = new CollisionShape3D
    {
      Shape = new SphereShape3D { Radius = Radius + 0.5f }
    };
    AddChild(collisionShape);

    // Create a detection area for overlap collisions.
    var detectionArea = new Area3D();
    var areaCollisionShape = new CollisionShape3D
    {
      Shape = new SphereShape3D { Radius = Radius + 0.5f }
    };
    detectionArea.CollisionLayer = 0;
    detectionArea.CollisionMask = 1;
    detectionArea.AddChild(areaCollisionShape);
    AddChild(detectionArea);

    Velocity = Direction.Normalized() * Speed;

    // Setup default trail.
    Gradient defaultGradient = new Gradient();
    defaultGradient.SetColor(0, Colors.Yellow);
    defaultGradient.SetColor(1, Colors.White);
    var defaultTrail = new Trail
    {
      Lifetime = 0.05f,
      BaseWidth = Radius,
      Gradient = defaultGradient
    };
    Trails.Add(defaultTrail);

    foreach (var trail in Trails)
    {
      trail.Initialize();
      AddChild(trail);
    }

    foreach (var module in Modules)
      module.OnFire(this);

    // Create a timer for bullet lifetime cleanup.
    GetTree().CreateTimer(LifeTime).Timeout += _Cleanup;
  }

  public override async void _PhysicsProcess(double delta)
  {
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
        _Cleanup();
        return;
      }
    }

    Vector3 predictedMotion = Velocity * dt;
    Vector3 predictedPosition = currentPosition + predictedMotion;

    UpdateColorBasedOnSpeed(dt);

    var query = new PhysicsRayQueryParameters3D
    {
      From = currentPosition,
      To = predictedPosition,
      Exclude = new Godot.Collections.Array<Rid> { GetRid() }
    };

    var collision = GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (collision.Count > 0 && collision.ContainsKey("collider"))
    {
      // Convert the collision variant to Node3D using As<Node3D>().
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

        DestroyOnImpact = true;
        foreach (var module in Modules)
          await module.OnCollision(collisionData, this);

        DefaultBulletCollision(collisionData, this);
        SpawnCollisionParticles(collisionData);

        if (DestroyOnImpact)
        {
          _Cleanup();
          return;
        }
      }
    }
    else
    {
      GlobalTransform = new Transform3D(currentTransform.Basis, predictedPosition);
    }

    foreach (var module in Modules)
      await module.OnBulletPhysicsProcess(dt, this);

    await _ProcessOverlapCollisions(dt);
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
        foreach (var module in Modules)
          await module.OnCollision(collisionData, this);

        DefaultBulletCollision(collisionData, this);
        SpawnCollisionParticles(collisionData);

        if (DestroyOnImpact)
        {
          _Cleanup();
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
      if (hit != null && hit.IsInGroup("enemies") && IsInstanceValid(hit))
      {
        if (hit.HasMethod("take_damage"))
          hit.CallDeferred("take_damage", Damage);

        // Compute shake parameters based on total damage.
        // These values can be adjusted to get the desired "snappy" feel.
        float shakeDuration = 0.05f;
        float shakeIntensity = Mathf.Clamp(collision.TotalDamageDealt * 0.05f, 0.1f, 0.3f);

        Camera3D camera3D = GetViewport().GetCamera3D();
        if (camera3D != null)
        {
          Camera cam = camera3D as Camera;
          if (cam != null)
          {
            cam.TriggerShake(shakeDuration, shakeIntensity);
          }
        }
      }
    }
    catch (Exception e)
    {
      GD.PrintErr(e.Message);
    }
  }

  private void SpawnCollisionParticles(CollisionData collision)
  {
    int countOld = (int)TriangularScaleDamage(0, 8, collision.TotalDamageDealt, 0, 10);
    for (int i = 0; i < countOld; i++)
    {
      CollisionParticle particle = new CollisionParticle();
      particle.GlobalPosition = collision.Position;
      particle.InitialDirection = new Vector3(GD.Randf() * 2 - 1, GD.Randf() * 2 - 1, GD.Randf() * 2 - 1);
      particle.Gravity = 30.0f;
      GetTree().CurrentScene.AddChild(particle);
    }

    if (collision.TotalDamageDealt > 30)
    {
      for (int i = 0; i < 6; i++)
      {
        ActionLineParticle lineParticle = new ActionLineParticle();
        lineParticle.GlobalPosition = collision.Position;
        GetTree().CurrentScene.AddChild(lineParticle);
      }
    }

    float rand = GD.Randf();
    int baseLowPolyCount = (rand > 0.9f) ? 3 : (rand > 0.8f) ? 2 : (rand > 0.6f) ? 1 : 0;
    int countLowPoly = (int)TriangularScaleDamage(0, baseLowPolyCount, collision.TotalDamageDealt, 0, 200);
    for (int i = 0; i < countLowPoly; i++)
    {
      PhysicalParticle particle = new PhysicalParticle();
      float offsetDistance = 0.2f;
      Vector3 basePosition = collision.Position + collision.Normal * offsetDistance;
      Vector3 tangent = collision.Normal.Cross(Vector3.Up);
      if (tangent.Length() < 0.001f)
        tangent = collision.Normal.Cross(Vector3.Right);
      tangent = tangent.Normalized();
      Vector3 bitangent = collision.Normal.Cross(tangent).Normalized();
      float randomMagnitude = 0.1f;
      Vector3 randomOffset = tangent * (GD.Randf() * randomMagnitude - randomMagnitude * 0.5f)
                          + bitangent * (GD.Randf() * randomMagnitude - randomMagnitude * 0.5f);
      particle.GlobalPosition = basePosition + randomOffset;
      Vector3 shootDir = collision.Normal;
      Vector3 impulseVariation = tangent * (GD.Randf() * 0.2f - 0.1f)
                             + bitangent * (GD.Randf() * 0.2f - 0.1f);
      shootDir = (shootDir + impulseVariation).Normalized();
      particle.InitialImpulse = shootDir;
      StandardMaterial3D mat = GetTextureFromCollider(collision.Collider);
      if (mat != null)
        particle.ParticleMaterial = mat;
      GetTree().CurrentScene.AddChild(particle);
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

  private void UpdateColorBasedOnSpeed(float dt)
  {
    Color gunmetalGray = new Color(0.325f, 0.325f, 0.345f);
    float currentSpeed = Velocity.Length();
    float speedRatio = Mathf.Clamp(currentSpeed / 200, 0, 1);
    Color targetColor = gunmetalGray.Lerp(Colors.White, speedRatio);
    if (_mesh.MaterialOverride is StandardMaterial3D mat)
      mat.AlbedoColor = mat.AlbedoColor.Lerp(targetColor, dt * ColorChangeFactor);
  }

  private void _Cleanup()
  {
    if (!IsInstanceValid(this))
      return;
    QueueFree();
  }
}
