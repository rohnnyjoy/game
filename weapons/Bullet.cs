using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Bullet : Area3D
{
  public struct CollisionData
  {
    public Vector3 Position;
    public Vector3 Normal;
    public ulong ColliderId; // Using ulong to match GetInstanceId.
    public Node3D Collider;
    public Rid Rid;
  }

  [Export] public float LifeTime { get; set; } = 30.0f;
  [Export] public float Gravity { get; set; } = 0.0f;
  [Export] public Vector3 Direction { get; set; } = Vector3.Forward;
  [Export] public float Speed { get; set; } = 20.0f;
  [Export] public float Damage { get; set; } = 1.0f;
  // Bullet starting color
  [Export] public Color Color { get; set; } = Colors.White;
  [Export] public bool DestroyOnImpact { get; set; } = true;
  [Export] public Vector3 InitialPosition { get; private set; }
  [Export] public float TraveledDistance { get; private set; } = 0.0f;

  // Trail cleanup delay (in seconds)
  [Export] public float TrailCleanupDelay { get; set; } = 2.0f;

  // Overlap collision detection properties.
  [Export] public bool EnableOverlapCollision { get; set; } = true;
  [Export] public float OverlapCollisionDelay { get; set; } = 0.5f;
  [Export] public float ColorChangeFactor { get; set; } = 40f;

  // Timer tracking overlap collisions.
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

  // Custom velocity since Area3D does not have one.
  [Export] public Vector3 Velocity;

  public override void _Ready()
  {
    Scale = Vector3.One;
    GD.Randomize();
    RotationDegrees = new Vector3(
        GD.Randf() * 360,
        GD.Randf() * 360,
        GD.Randf() * 360
    );

    // Create the visual mesh.
    _mesh = new MeshInstance3D();
    var sphereMesh = new SphereMesh
    {
      Radius = Radius,
      Height = Radius * 2,
      RadialSegments = 4,
      Rings = 2
    };
    _mesh.Mesh = sphereMesh;
    _mesh.Scale = Vector3.One;
    _mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    AddChild(_mesh);

    var mat = new StandardMaterial3D
    {
      AlbedoColor = Color,
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
    _mesh.MaterialOverride = mat;

    // Store the spawn position.
    InitialPosition = GlobalPosition;

    // Create a collision shape for detection.
    var collisionShape = new CollisionShape3D();
    collisionShape.Shape = new SphereShape3D { Radius = Radius + 0.5f };
    AddChild(collisionShape);

    // Optionally add an Area3D for additional overlap detection.
    var detectionArea = new Area3D();
    var areaCollisionShape = new CollisionShape3D();
    var areaSphere = new SphereShape3D { Radius = Radius + 0.5f };
    areaCollisionShape.Shape = areaSphere;
    detectionArea.CollisionLayer = 0;
    detectionArea.CollisionMask = 1;
    detectionArea.AddChild(areaCollisionShape);
    AddChild(detectionArea);

    // Set initial velocity.
    Velocity = Direction.Normalized() * Speed;

    // Setup a default trail.
    var trailGradient = new Gradient();
    trailGradient.SetColor(0, Colors.Yellow);
    trailGradient.SetColor(1, Colors.White);
    var defaultTrail = new Trail
    {
      Lifetime = 0.1f,
      BaseWidth = Radius,
      Gradient = trailGradient
    };
    Trails.Add(defaultTrail);

    // Initialize and add each trail.
    foreach (var trail in Trails)
    {
      trail.Initialize(); // Ensure the trail sets TopLevel = true.
      AddChild(trail);
    }

    // Notify modules that the bullet has been fired.
    foreach (var module in Modules)
      module.OnFire(this);

    // After LifeTime seconds, clean up the bullet.
    GetTree().CreateTimer(LifeTime).Timeout += _Cleanup;
  }

  public override async void _PhysicsProcess(double delta)
  {
    float dt = (float)delta;
    Velocity += Vector3.Down * Gravity * dt;
    TraveledDistance = InitialPosition.DistanceTo(GlobalPosition);

    Vector3 currentPosition = GlobalTransform.Origin;
    Vector3 predictedMotion = Velocity * dt;
    Vector3 predictedPosition = currentPosition + predictedMotion;

    UpdateColorBasedOnSpeed(dt);

    // Create a ray query from current to predicted position.
    var query = new PhysicsRayQueryParameters3D
    {
      From = currentPosition,
      To = predictedPosition,
      Exclude = new Godot.Collections.Array<Rid> { GetRid() }
    };

    var collision = GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (collision.Count > 0 && collision.ContainsKey("collider"))
    {
      Node3D hit = (Node3D)collision["collider"];
      // If we hit an enemy and it's the same collider as last frame, simply update the position.
      if (hit.IsInGroup("enemies") && (ulong)collision["collider_id"] == _lastCollisionColliderId)
      {
        GlobalTransform = new Transform3D(GlobalTransform.Basis, predictedPosition);
      }
      else
      {
        // Snap the bullet to the collision position.
        GlobalTransform = new Transform3D(GlobalTransform.Basis, (Vector3)collision["position"]);
        _lastCollisionColliderId = (ulong)collision["collider_id"];

        CollisionData collisionData = new CollisionData
        {
          Position = (Vector3)collision["position"],
          Normal = (Vector3)collision["normal"],
          ColliderId = (ulong)collision["collider_id"],
          Collider = (Node3D)collision["collider"],
          Rid = (Rid)collision["rid"]
        };

        // Immediately spawn collision particles BEFORE any collision handlers run.
        SpawnCollisionParticles(collisionData);

        // Notify all modules about the collision.
        DestroyOnImpact = true;
        foreach (var module in Modules)
          await module.OnCollision(collisionData, this);

        // Bullet-specific collision logic.
        _OnBulletCollision(collisionData, this);

        if (DestroyOnImpact)
        {
          _Cleanup();
          return; // Exit immediately to stop further processing.
        }
      }
    }
    else
    {
      GlobalTransform = new Transform3D(GlobalTransform.Basis, predictedPosition);
    }

    foreach (var module in Modules)
      await module.OnPhysicsProcess(dt, this);

    // Process continuous overlap collisions.
    _ProcessOverlapCollisions(dt);
  }

  private async Task _ProcessOverlapCollisions(float dt)
  {
    if (!EnableOverlapCollision)
      return;

    bool overlapping = false;
    Node3D collidedBody = null;

    // Check all child Area3D nodes for overlapping bodies.
    foreach (Node child in GetChildren())
    {
      if (child is Area3D area)
      {
        var bodies = area.GetOverlappingBodies();
        foreach (var body in bodies)
        {
          GD.Print("Overlapping with: " + body);
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
          Position = GlobalPosition, // Use bullet's current position.
          Normal = Vector3.Up,       // Placeholder normal; adjust as needed.
          ColliderId = (ulong)collidedBody.GetInstanceId(),
          Collider = collidedBody,
          Rid = rid
        };

        // Spawn collision particles.
        SpawnCollisionParticles(collisionData);

        // Notify modules about the collision.
        DestroyOnImpact = true;
        foreach (var module in Modules)
          await module.OnCollision(collisionData, this);

        _OnBulletCollision(collisionData, this);

        if (DestroyOnImpact)
        {
          _Cleanup();
          return;
        }

        _overlapTimer = 0.0f; // Reset timer after triggering.
      }
    }
    else
    {
      _overlapTimer = 0.0f;
    }
  }

  // Bullet-specific collision logic.
  private void _OnBulletCollision(CollisionData collision, Bullet bullet)
  {
    try
    {
      Node3D hit = collision.Collider;
      if (!IsInstanceValid(hit))
        return;
      if (hit != null && hit.IsInGroup("enemies") && IsInstanceValid(hit))
      {
        if (hit.HasMethod("take_damage"))
          hit.CallDeferred("take_damage", Damage);
      }
    }
    catch (Exception e)
    {
      GD.PrintErr(e.Message);
    }
  }

  // Spawns collision particles immediately upon collision.
  // This method spawns original collision particles, action line particles, and low-poly physical particles.
  private void SpawnCollisionParticles(CollisionData collision)
  {
    // Spawn original collision particles.
    int countOld = 8;
    for (int i = 0; i < countOld; i++)
    {
      CollisionParticle particle = new CollisionParticle();
      particle.GlobalPosition = collision.Position;
      particle.InitialDirection = new Vector3(
          (float)GD.Randf() * 2 - 1,
          (float)GD.Randf() * 2 - 1,
          (float)GD.Randf() * 2 - 1
      );
      particle.Gravity = 20.0f;
      GetTree().CurrentScene.AddChild(particle);
    }

    // Spawn action line particles.
    int countLines = 6;
    for (int i = 0; i < countLines; i++)
    {
      ActionLineParticle lineParticle = new ActionLineParticle();
      lineParticle.GlobalPosition = collision.Position;
      GetTree().CurrentScene.AddChild(lineParticle);
    }

    // Spawn low-poly physical particles with a bias towards 0.
    float rand = GD.Randf();
    int countLowPoly = (rand > 0.9f) ? 3 : (rand > 0.8f) ? 2 : (rand > 0.6f) ? 1 : 0;
    for (int i = 0; i < countLowPoly; i++)
    {
      PhysicalParticle particle = new PhysicalParticle();

      // Offset the spawn position along the collision normal.
      float offsetDistance = 0.2f;
      Vector3 basePosition = collision.Position + collision.Normal * offsetDistance;

      // Calculate perpendicular vectors.
      Vector3 tangent = collision.Normal.Cross(Vector3.Up);
      if (tangent.Length() < 0.001f)
        tangent = collision.Normal.Cross(Vector3.Right);
      tangent = tangent.Normalized();
      Vector3 bitangent = collision.Normal.Cross(tangent).Normalized();

      // Generate a random offset in the perpendicular plane.
      float randomMagnitude = 0.1f;
      Vector3 randomOffset = tangent * ((float)GD.Randf() * randomMagnitude - randomMagnitude * 0.5f)
                            + bitangent * ((float)GD.Randf() * randomMagnitude - randomMagnitude * 0.5f);

      // Set particle's spawn position.
      particle.GlobalPosition = basePosition + randomOffset;

      // Compute the impulse direction with a slight random variation.
      Vector3 shootDir = collision.Normal;
      Vector3 impulseVariation = tangent * ((float)GD.Randf() * 0.2f - 0.1f)
                               + bitangent * ((float)GD.Randf() * 0.2f - 0.1f);
      shootDir = (shootDir + impulseVariation).Normalized();

      // Set the particle's impulse direction.
      particle.InitialImpulse = shootDir;

      // Optionally, assign a texture from the collider using our helper.
      StandardMaterial3D mat = GetTextureFromCollider(collision.Collider);
      if (mat != null)
        particle.ParticleMaterial = mat;

      GetTree().CurrentScene.AddChild(particle);
    }

  }

  // Helper method to recursively search for a texture from a collider.
  private StandardMaterial3D GetTextureFromCollider(Node3D collider)
  {
    GD.Print("Searching for texture in collider: ", collider);
    // Check if collider is a GeometryInstance3D (includes MeshInstance3D).
    if (collider is GeometryInstance3D geom)
    {
      // Try material override first.
      if (geom.MaterialOverride is StandardMaterial3D mat)
      {
        return mat;
      }
      // If no override, and collider is a MeshInstance3D, try the surface material.
      if (geom is MeshInstance3D mi && mi.Mesh != null)
      {
        GD.Print("Checking MeshInstance3DMeshInstance3Dsurface material...");
        // Use SurfaceGetMaterial(0) to retrieve the material.
        var surfMat = mi.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
        if (surfMat != null)
        {
          GD.Print("Found MeshInstance3D surface material: ", surfMat);
          return surfMat;
        }
      }
    }
    // Otherwise, search children recursively.
    foreach (Node child in collider.GetChildren())
    {
      if (child is Node3D childNode)
      {
        GD.Print("Checking child node: ", childNode);
        StandardMaterial3D found = GetTextureFromCollider(childNode);
        if (found != null)
          return found;
      }
    }
    return null;
  }

  // Utility method for spawning a random direction in the hemisphere defined by 'normal'.
  private Vector3 GetRandomHemisphereDirection(Vector3 normal)
  {
    Vector3 randomDir = new Vector3(
        (float)GD.Randf() * 2 - 1,
        (float)GD.Randf() * 2 - 1,
        (float)GD.Randf() * 2 - 1
    ).Normalized();
    if (randomDir.Dot(normal) < 0)
      randomDir = -randomDir;
    return randomDir;
  }

  private void UpdateColorBasedOnSpeed(float dt)
  {
    // Define your gunmetal gray.
    Color gunmetalGray = new Color(0.325f, 0.325f, 0.345f);

    // Calculate the ratio of the current speed to the base Speed.
    // When at full speed, ratio will be 1 (resulting in white).
    // When slowed to 0, ratio will be 0 (resulting in gunmetal gray).
    float speedRatio = Mathf.Clamp(Velocity.Length() / Speed, 0, 1);

    // Interpolate from white to gunmetal gray. When speedRatio is 1, target is white.
    Color targetColor = Colors.White.Lerp(gunmetalGray, 1 - speedRatio);

    // Get the material from the mesh.
    if (_mesh.MaterialOverride is StandardMaterial3D mat)
    {
      // Smoothly tween the material's albedo color toward targetColor.
      // Adjust the factor (here dt * 10) to control the tweening speed.
      mat.AlbedoColor = mat.AlbedoColor.Lerp(targetColor, dt * ColorChangeFactor);
    }
  }


  private void _Cleanup()
  {
    if (!IsInstanceValid(this))
      return;
    QueueFree();
  }
}
