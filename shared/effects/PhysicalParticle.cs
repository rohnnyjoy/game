using Godot;
using System;
using Godot.Collections; // For Godot.Collections.Array
#nullable enable

public partial class PhysicalParticle : RigidBody3D
{
  [Export]
  public float LifeTime { get; set; } = 3.0f;

  // The speed factor for the particle.
  [Export]
  public float ImpulseMagnitude { get; set; } = 8.0f;

  // A normalized direction that will be multiplied by ImpulseMagnitude to set the velocity.
  [Export]
  public Vector3 InitialImpulse { get; set; } = Vector3.Zero;

  // A factor to control the randomness added to the velocity.
  [Export]
  public float RandomVelocityFactor { get; set; } = 10.0f;

  // Particle material to apply to the particle (ideally sampled from the collided area).
  public StandardMaterial3D? ParticleMaterial { get; set; }

  // The UV coordinate (normalized 0–1) at which to sample from the texture.
  [Export]
  public Vector2 SampleUV { get; set; } = new Vector2(0.5f, 0.5f);

  // The crop size (in pixels) of the region to extract.
  [Export]
  public Vector2 SampleCropSize { get; set; } = new Vector2(64, 64);

  public override void _Ready()
  {
    // Activate the body.
    Sleeping = false;
    GravityScale = 3.0f;
    LinearDamp = 1.0f;

    // Set up a physics material with lower friction.
    var physicsMaterial = new PhysicsMaterial
    {
      Bounce = 0.8f,
      Friction = 0.1f
    };
    PhysicsMaterialOverride = physicsMaterial;
    ContinuousCd = true;

    // Create a MeshInstance3D with a low-poly sphere mesh.
    var meshInstance = new MeshInstance3D();
    SphereMesh sphereMesh = new SphereMesh();
    RandomNumberGenerator rng = new RandomNumberGenerator();
    rng.Randomize();
    sphereMesh.RadialSegments = rng.RandiRange(4, 8);
    sphereMesh.Rings = rng.RandiRange(2, 4);
    sphereMesh.Radius = 0.1f;
    sphereMesh.Height = 0.2f;
    meshInstance.Mesh = sphereMesh;
    AddChild(meshInstance);

    // Create a CollisionShape3D that attempts to match the mesh.
    var collisionShape = new CollisionShape3D();
    try
    {
      // Retrieve the vertex arrays from the mesh.
      Godot.Collections.Array arrays = (Godot.Collections.Array)sphereMesh.SurfaceGetArrays(0);
      Godot.Collections.Array vertexArray = (Godot.Collections.Array)arrays[(int)ArrayMesh.ArrayType.Vertex];

      if (vertexArray.Count > 0)
      {
        Vector3[] vertices = new Vector3[vertexArray.Count];
        for (int i = 0; i < vertexArray.Count; i++)
        {
          vertices[i] = (Vector3)vertexArray[i];
        }
        if (vertices.Length > 0)
        {
          var convexShape = new ConvexPolygonShape3D();
          convexShape.Points = vertices;
          collisionShape.Shape = convexShape;
        }
        else
        {
          collisionShape.Shape = new SphereShape3D { Radius = sphereMesh.Radius };
        }
      }
      else
      {
        collisionShape.Shape = new SphereShape3D { Radius = sphereMesh.Radius };
      }
    }
    catch (Exception ex)
    {
      GD.PrintErr("Error generating collision shape: ", ex);
      collisionShape.Shape = new SphereShape3D { Radius = sphereMesh.Radius };
    }
    AddChild(collisionShape);

    // Set up the visual material. Use ParticleMaterial if provided, else create a new one.
    StandardMaterial3D material;
    if (ParticleMaterial != null)
    {
      material = ParticleMaterial;
    }
    else
    {
      material = new StandardMaterial3D
      {
        AlbedoColor = Colors.Gray
      };
    }
    meshInstance.MaterialOverride = material;

    // Generate a random scale.
    float randomScale = (float)rng.RandfRange(0.5f, 1.5f);

    // Apply the scale to the MeshInstance3D and CollisionShape3D instead of the parent.
    meshInstance.Scale = new Vector3(randomScale, randomScale, randomScale);
    collisionShape.Scale = new Vector3(randomScale, randomScale, randomScale);

    // Set the initial velocity with added randomness.
    if (InitialImpulse != Vector3.Zero)
    {
      InitialImpulse = InitialImpulse.Normalized();
      Vector3 randomVariation = new Vector3(
          (float)rng.RandfRange(-RandomVelocityFactor, RandomVelocityFactor),
          (float)rng.RandfRange(-RandomVelocityFactor, RandomVelocityFactor),
          (float)rng.RandfRange(-RandomVelocityFactor, RandomVelocityFactor)
      );
      LinearVelocity = (InitialImpulse * ImpulseMagnitude) + randomVariation;
    }

    Gradient whiteGradient = new Gradient();
    whiteGradient.Colors = new Color[]
    {
            new Color(1, 1, 1, 0),
            new Color(1, 1, 1, 0.1f)
    };
    Trail particleTrail = new Trail();
    particleTrail.TransparencyMode = (int)BaseMaterial3D.TransparencyEnum.Alpha;
    particleTrail.Gradient = whiteGradient;
    particleTrail.BaseWidth = 0.1f;
    particleTrail.Initialize();
    AddChild(particleTrail);

    // Set up a timer to remove this particle after its lifetime expires.
    var timer = new Godot.Timer();
    timer.WaitTime = LifeTime;
    timer.OneShot = true;
    timer.Autostart = true;
    AddChild(timer);
    timer.Timeout += OnLifeTimeout;
  }

  private void OnLifeTimeout()
  {
    QueueFree();
  }
}
