using Godot;
using System;
using System.Collections.Generic;

public partial class Bullet : Area3D
{
  public struct CollisionData
  {
    public Vector3 Position;
    public Vector3 Normal;
    public int ColliderId;
    public Node3D Collider;
    public Rid Rid;
  }

  [Export] public float LifeTime { get; set; } = 30.0f;
  [Export] public float Gravity { get; set; } = 0.0f;
  [Export] public Vector3 Direction { get; set; } = Vector3.Forward;
  [Export] public float Speed { get; set; } = 20.0f;
  [Export] public float Damage { get; set; } = 1.0f;
  [Export] public Color Color { get; set; } = new Color(1, 1, 1);
  [Export] public bool DestroyOnImpact { get; set; } = true;
  [Export] public Vector3 InitialPosition { get; private set; }
  [Export] public float TraveledDistance { get; private set; } = 0.0f;

  // New export property for trail cleanup delay (in seconds)
  [Export] public float TrailCleanupDelay { get; set; } = 2.0f;

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
  private int _lastCollisionColliderId = -1;
  private List<Action<CollisionData, Bullet>> CollisionHandlers = new List<Action<CollisionData, Bullet>>();

  // Use a custom velocity since Area3D does not have one.
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

    // Optionally, if you need additional detection (for overlap queries), you can add a child Area3D.
    // (If not required, you may remove this extra area.)
    var detectionArea = new Area3D();
    var areaCollisionShape = new CollisionShape3D();
    var areaSphere = new SphereShape3D { Radius = Radius + 0.5f };
    areaCollisionShape.Shape = areaSphere;
    detectionArea.AddChild(areaCollisionShape);
    // Set layers/masks as needed.
    detectionArea.CollisionLayer = 0;
    detectionArea.CollisionMask = 1;
    AddChild(detectionArea);

    // Set initial velocity.
    Velocity = Direction.Normalized() * Speed;

    // Setup a default trail.
    var trailGradient = new Gradient();
    trailGradient.SetColor(0, Colors.Yellow);
    trailGradient.SetColor(1, Colors.White);
    var defaultTrail = new Trail();
    defaultTrail.Lifetime = 0.1f;
    defaultTrail.BaseWidth = Radius;
    defaultTrail.Gradient = trailGradient;
    Trails.Add(defaultTrail);

    // Initialize and add each trail.
    foreach (var trail in Trails)
    {
      trail.Initialize(); // Ensure the trail sets TopLevel = true.
      AddChild(trail);
    }

    // Notify any modules that the bullet has been fired.
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

    // Create a ray query from the current to predicted position.
    var query = new PhysicsRayQueryParameters3D();
    query.From = currentPosition;
    query.To = predictedPosition;
    query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

    var collision = GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (collision.Count > 0 && collision.ContainsKey("collider"))
    {
      Node3D hit = (Node3D)collision["collider"];
      // If we hit an enemy and it's the same collider as last frame, simply update the position.
      if (hit.IsInGroup("enemies") && (int)collision["collider_id"] == _lastCollisionColliderId)
      {
        GlobalTransform = new Transform3D(GlobalTransform.Basis, predictedPosition);
      }
      else
      {
        // Snap the bullet to the collision position.
        GlobalTransform = new Transform3D(GlobalTransform.Basis, (Vector3)collision["position"]);
        _lastCollisionColliderId = (int)collision["collider_id"];

        CollisionData collisionData = new CollisionData
        {
          Position = (Vector3)collision["position"],
          Normal = (Vector3)collision["normal"],
          ColliderId = (int)collision["collider_id"],
          Collider = (Node3D)collision["collider"],
          Rid = (Rid)collision["rid"]
        };
        // Must set on collision pre-modules
        DestroyOnImpact = true;
        foreach (var module in Modules)
          await module.OnCollision(collisionData, this);

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

    _ProcessEnemiesInside();
  }

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

  private void _ProcessEnemiesInside()
  {
    // Iterate through children; if an Area3D, check overlapping bodies.
    foreach (Node child in GetChildren())
    {
      if (child is Area3D area)
      {
        var bodies = area.GetOverlappingBodies();
        foreach (var body in bodies)
        {
          if (body is Node3D n && n.IsInGroup("enemies"))
          {
            // Process enemy overlap here if needed.
          }
        }
      }
    }
  }

  private void _Cleanup()
  {
    // Check if this bullet instance is still valid.
    if (!IsInstanceValid(this))
      return;

    // Optional: Reparent the trails so they aren’t immediately destroyed with the bullet.
    // foreach (Trail t in Trails)
    // {
    //   if (!IsInstanceValid(t))
    //     continue;
    //   RemoveChild(t);
    //   GetTree().CurrentScene.AddChild(t);
    //   t.StopTrail();
    // }

    // // Create a timer to free the trails after TrailCleanupDelay seconds.
    // GetTree().CreateTimer(TrailCleanupDelay).Timeout += () =>
    // {
    //   foreach (Trail t in Trails)
    //   {
    //     if (IsInstanceValid(t))
    //       continue;
    //     t.QueueFree();
    //   }
    // };

    QueueFree();
  }
}
