using Godot;
using System;

public partial class ActionLineParticle : Node3D
{
  [Export] public float Lifetime { get; set; } = 0.2f;
  [Export] public float Speed { get; set; } = 20.0f;
  [Export] public float DecelerationFactor { get; set; } = 5.0f;
  [Export] public float ThicknessDecayExponent { get; set; } = 1.0f;
  // How far from the sphere (origin) the particle starts.
  [Export] public float StartDistance { get; set; } = 2.0f;
  // Configurable crystal color.
  [Export]
  public Color CrystalColor { get; set; } = Colors.Orange;

  private float _timeElapsed = 0.0f;
  private Vector3 _blastDirection;
  private MeshInstance3D _lineMeshInstance;
  private Vector3 _baseScale;

  // Constants for the crystal geometry.
  private const float _L = 0.5f;  // Half the length of the central box.
  private const float _T = 0.2f;  // Length added for each tapered tip.
  private const float _W = 0.1f; // Half width.
  private const float _H = 0.05f; // Half height.

  // CollisionNormal remains available if needed externally.
  public Vector3 CollisionNormal { get; set; } = Vector3.Forward;

  public override void _Ready()
  {
    SetProcessPriority(-1);

    // Pick a random direction over the full sphere.
    _blastDirection = GetRandomDirection();

    // Move the entire particle node along the blast direction.
    // This effectively spawns it at a configurable distance from the sphere center.
    Translate(_blastDirection * StartDistance);

    // Create the mesh instance using a custom "crystal" mesh.
    _lineMeshInstance = new MeshInstance3D();
    ArrayMesh crystalMesh = CreateCrystalMesh();
    _lineMeshInstance.Mesh = crystalMesh;

    // Rotate the mesh instance so that its local +Z axis aligns with _blastDirection.
    if (_blastDirection != Vector3.Zero)
    {
      Quaternion rotation = GetRotationFromForwardToDirection(_blastDirection);
      _lineMeshInstance.Rotation = rotation.GetEuler();
    }

    // Apply a slight random scale variation.
    float scaleVariation = 0.8f + (float)GD.Randf() * 0.4f;
    _lineMeshInstance.Scale = new Vector3(scaleVariation, scaleVariation, scaleVariation);
    _baseScale = _lineMeshInstance.Scale;

    // Create an unshaded material that is translucent and uses the configurable crystal color.
    // Backface culling is disabled so that both sides are visible.
    var mat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      AlbedoColor = CrystalColor,
      CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      // Optional emission for a glow effect.
      EmissionEnabled = true,
      Emission = CrystalColor,
      EmissionEnergyMultiplier = 1.0f
    };
    _lineMeshInstance.MaterialOverride = mat;

    // Disable shadow casting.
    _lineMeshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

    // Add the mesh instance as a child of the particle node.
    AddChild(_lineMeshInstance);
  }

  public override void _PhysicsProcess(double delta)
  {
    float dt = (float)delta;
    _timeElapsed += dt;

    // Exponential deceleration.
    float currentSpeed = Speed * Mathf.Exp(-DecelerationFactor * _timeElapsed);

    // Move the entire particle node along the blast direction.
    Translate(_blastDirection * currentSpeed * dt);

    // Shrink the X/Y thickness of the mesh over time.
    float t = Mathf.Clamp(_timeElapsed / Lifetime, 0f, 1f);
    float thicknessFactor = 1.0f - Mathf.Pow(t, ThicknessDecayExponent);
    Vector3 sc = _lineMeshInstance.Scale;
    sc.X = _baseScale.X * thicknessFactor;
    sc.Y = _baseScale.Y * thicknessFactor;
    _lineMeshInstance.Scale = sc;

    // Remove the particle after its lifetime.
    if (_timeElapsed >= Lifetime)
      QueueFree();
  }

  // Returns a random direction uniformly distributed over the sphere.
  private Vector3 GetRandomDirection()
  {
    float u = (float)GD.Randf();
    float v = (float)GD.Randf();
    float theta = u * Mathf.Tau;
    float phi = Mathf.Acos(2.0f * v - 1.0f);
    float x = Mathf.Sin(phi) * Mathf.Cos(theta);
    float y = Mathf.Sin(phi) * Mathf.Sin(theta);
    float z = Mathf.Cos(phi);
    return new Vector3(x, y, z).Normalized();
  }

  // Computes a quaternion to rotate the default forward (0,0,1) to the given direction.
  private Quaternion GetRotationFromForwardToDirection(Vector3 direction)
  {
    Vector3 forward = new Vector3(0, 0, 1);
    Vector3 dirNormalized = direction.Normalized();
    float dot = forward.Dot(dirNormalized);
    if (dot < -0.9999f)
      return new Quaternion(Vector3.Up, Mathf.Pi);
    Vector3 cross = forward.Cross(dirNormalized);
    float s = Mathf.Sqrt((1 + dot) * 2);
    float invS = 1 / s;
    return new Quaternion(cross.X * invS, cross.Y * invS, cross.Z * invS, s * 0.5f);
  }

  // Creates a custom ArrayMesh that resembles a double-terminated crystal.
  private ArrayMesh CreateCrystalMesh()
  {
    // Define the eight corners of the central box.
    Vector3 v0 = new Vector3(-_W, -_H, -_L);
    Vector3 v1 = new Vector3(_W, -_H, -_L);
    Vector3 v2 = new Vector3(_W, _H, -_L);
    Vector3 v3 = new Vector3(-_W, _H, -_L);

    Vector3 v4 = new Vector3(-_W, -_H, _L);
    Vector3 v5 = new Vector3(_W, -_H, _L);
    Vector3 v6 = new Vector3(_W, _H, _L);
    Vector3 v7 = new Vector3(-_W, _H, _L);

    // Define the tip vertices.
    Vector3 tipFront = new Vector3(0, 0, _L + _T);
    Vector3 tipBack = new Vector3(0, 0, -_L - _T);

    SurfaceTool st = new SurfaceTool();
    st.Begin(Mesh.PrimitiveType.Triangles);

    // Helper function to add a triangle with a flat normal.
    void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
      Vector3 normal = (b - a).Cross(c - a).Normalized();
      st.SetNormal(normal);
      st.AddVertex(a);
      st.SetNormal(normal);
      st.AddVertex(b);
      st.SetNormal(normal);
      st.AddVertex(c);
    }

    // Right face.
    AddTriangle(v1, v5, v6);
    AddTriangle(v1, v6, v2);

    // Left face.
    AddTriangle(v0, v4, v7);
    AddTriangle(v0, v7, v3);

    // Top face.
    AddTriangle(v3, v7, v6);
    AddTriangle(v3, v6, v2);

    // Bottom face.
    AddTriangle(v0, v4, v5);
    AddTriangle(v0, v5, v1);

    // Front pyramid (tapered tip).
    AddTriangle(v4, v5, tipFront);
    AddTriangle(v5, v6, tipFront);
    AddTriangle(v6, v7, tipFront);
    AddTriangle(v7, v4, tipFront);

    // Back pyramid (tapered tip).
    AddTriangle(v0, v3, tipBack);
    AddTriangle(v3, v2, tipBack);
    AddTriangle(v2, v1, tipBack);
    AddTriangle(v1, v0, tipBack);

    ArrayMesh mesh = st.Commit();
    return mesh;
  }
}
