using Godot;
using System;
using System.Collections.Generic;

public partial class ArcEmitter : Node3D
{
  // Main arc properties:
  [Export] public float ArcRadius = 10.0f;
  [Export] public Vector3 DefaultArcDirection = Vector3.Up;
  [Export] public Vector3 SpawnOffset = Vector3.Zero;
  [Export] public int Segments = 8;
  [Export] public float Jitter = 1.0f;
  [Export] public NodePath TargetPath;
  [Export] public bool AnimateArc = false;
  [Export] public float ArcUpdateInterval = 0.1f;
  [Export] public int ArcCount = 1;
  [Export] public float EmissionEnergy = 2.0f;
  [Export] public float StartWidth = 0.2f;
  [Export] public float EndWidth = 0.05f;

  // Branch arc (vestigial decoration) properties:
  [Export] public bool EnableBranching = true;
  [Export] public float BranchChance = 1.0f;          // For testing, force branches.
  [Export] public float BranchLengthFactor = 0.5f;    // Adjust to control branch length.
  [Export] public int BranchSegments = 6;             // Number of segments in the branch arc.
  [Export] public float BranchJitter = 0.0f;            // No jitter for testing.
  [Export] public float BranchWidthFactor = 5.0f;       // Increase branch width for testing.

  // Lists to store main arc MeshInstances and ImmediateMesh.
  private List<MeshInstance3D> arcMeshInstances = new List<MeshInstance3D>();
  private List<ImmediateMesh> arcMeshes = new List<ImmediateMesh>();

  // Accumulator for updating arcs.
  private float timeAccumulator = 0.0f;

  public override void _Ready()
  {
    for (int i = 0; i < ArcCount; i++)
    {
      MeshInstance3D meshInstance = new MeshInstance3D();
      ImmediateMesh mesh = new ImmediateMesh();
      meshInstance.Mesh = mesh;

      // Main arc material: white glow, no culling.
      StandardMaterial3D material = new StandardMaterial3D();
      material.EmissionEnabled = true;
      material.Emission = new Color(1, 1, 1);
      material.EmissionEnergyMultiplier = EmissionEnergy;
      material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
      meshInstance.MaterialOverride = material;

      AddChild(meshInstance);
      arcMeshInstances.Add(meshInstance);
      arcMeshes.Add(mesh);

      GenerateArcForMesh(mesh);
    }
  }

  public override void _Process(double delta)
  {
    if (AnimateArc)
    {
      timeAccumulator += (float)delta;
      if (timeAccumulator >= ArcUpdateInterval)
      {
        // First, clear existing branch arcs.
        foreach (Node node in GetTree().GetNodesInGroup("BranchArc"))
        {
          node.QueueFree();
        }

        // Then update the main arc (which spawns branch arcs).
        foreach (ImmediateMesh mesh in arcMeshes)
        {
          GenerateArcForMesh(mesh);
        }
        timeAccumulator = 0.0f;
      }
    }
  }


  /// <summary>
  /// Returns a random endpoint on a sphere defined by ArcRadius.
  /// </summary>
  private Vector3 GetRandomEndpoint()
  {
    Vector3 randomDir = new Vector3(
        (float)GD.RandRange(-1, 1),
        (float)GD.RandRange(-1, 1),
        (float)GD.RandRange(-1, 1)
    );
    if (randomDir.Length() == 0)
      randomDir = DefaultArcDirection.Normalized();
    else
      randomDir = randomDir.Normalized();
    return randomDir * ArcRadius;
  }

  /// <summary>
  /// Generates the main arc as a continuous, tapered, billboarded triangle strip.
  /// Also spawns vestigial branch arcs for decoration.
  /// </summary>
  private void GenerateArcForMesh(ImmediateMesh mesh)
  {
    // Main arc: start and end points in local space.
    Vector3 localStartPos = SpawnOffset;
    Vector3 localEndPos;
    if (!string.IsNullOrEmpty(TargetPath))
    {
      Node targetNode = GetNode(TargetPath);
      if (targetNode is Node3D target3D)
        localEndPos = ToLocal(target3D.GlobalTransform.Origin);
      else
        localEndPos = SpawnOffset + DefaultArcDirection.Normalized() * ArcRadius;
    }
    else
    {
      localEndPos = SpawnOffset + (AnimateArc ? GetRandomEndpoint() : DefaultArcDirection.Normalized() * ArcRadius);
    }

    // Compute main arc points with optional jitter.
    int numPoints = Segments + 1;
    Vector3[] arcPoints = new Vector3[numPoints];
    for (int i = 0; i < numPoints; i++)
    {
      float t = (float)i / Segments;
      Vector3 point = localStartPos.Lerp(localEndPos, t);
      if (i != 0)
      {
        point.X += (float)GD.RandRange(-Jitter, Jitter);
        point.Y += (float)GD.RandRange(-Jitter, Jitter);
        point.Z += (float)GD.RandRange(-Jitter, Jitter);
      }
      arcPoints[i] = point;
    }

    // Get the active camera and its up vector in local space.
    Camera3D camera = GetViewport().GetCamera3D();
    Vector3 localCameraUp = Vector3.Up;
    if (camera != null)
    {
      Vector3 camGlobalUp = camera.GlobalTransform.Basis.Y;
      localCameraUp = (ToLocal(camera.GlobalTransform.Origin + camGlobalUp) - ToLocal(camera.GlobalTransform.Origin)).Normalized();
    }

    // Prepare per-vertex widths.
    float[] widths = new float[numPoints];
    for (int i = 0; i < numPoints; i++)
    {
      widths[i] = Mathf.Lerp(StartWidth, EndWidth, (float)i / (numPoints - 1));
    }

    // Build the main arc as a triangle strip.
    mesh.ClearSurfaces();
    mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
    for (int i = 0; i < numPoints; i++)
    {
      // Compute tangent at this point.
      Vector3 tangent;
      if (i == 0)
        tangent = arcPoints[1] - arcPoints[0];
      else if (i == numPoints - 1)
        tangent = arcPoints[numPoints - 1] - arcPoints[numPoints - 2];
      else
        tangent = arcPoints[i + 1] - arcPoints[i - 1];
      tangent = tangent.Normalized();

      // Use the camera up vector projected onto the plane perpendicular to the tangent.
      Vector3 billboard = localCameraUp - tangent * (localCameraUp.Dot(tangent));
      if (billboard.Length() < 0.001f)
      {
        billboard = tangent.Cross(Vector3.Right);
      }
      billboard = billboard.Normalized();

      // Compute left/right vertices.
      Vector3 left = arcPoints[i] + billboard * (widths[i] / 2);
      Vector3 right = arcPoints[i] - billboard * (widths[i] / 2);
      mesh.SurfaceAddVertex(left);
      mesh.SurfaceAddVertex(right);
    }
    mesh.SurfaceEnd();

    // --- Branching Arcs ---
    if (EnableBranching)
    {
      // When animating, clear previous branch arcs.
      if (AnimateArc)
      {
        foreach (Node node in GetTree().GetNodesInGroup("BranchArc"))
        {
          node.QueueFree();
        }
      }

      // For each candidate point along the main arc (skip endpoints), try to spawn a branch.
      for (int i = 0; i < numPoints - 1; i++)
      {
        if (GD.RandRange(0, 1) < BranchChance)
        {
          // Compute tangent at candidate point.
          Vector3 tangent;
          if (i == 0)
            tangent = arcPoints[1] - arcPoints[0];
          else if (i == numPoints - 1)
            tangent = arcPoints[numPoints - 1] - arcPoints[numPoints - 2];
          else
            tangent = arcPoints[i + 1] - arcPoints[i - 1];
          tangent = tangent.Normalized();

          // Build a perpendicular basis.
          Vector3 branchBasis1 = tangent.Cross(Vector3.Up);
          if (branchBasis1.Length() < 0.001f)
            branchBasis1 = tangent.Cross(Vector3.Right);
          branchBasis1 = branchBasis1.Normalized();
          Vector3 branchBasis2 = tangent.Cross(branchBasis1).Normalized();

          // Choose a random angle for branch direction.
          float angle = (float)GD.RandRange(0, Math.PI * 2);
          Vector3 branchDir = Mathf.Cos(angle) * branchBasis1 + Mathf.Sin(angle) * branchBasis2;

          Vector3 branchStart = arcPoints[i];
          Vector3 branchEnd = branchStart + branchDir * (ArcRadius * BranchLengthFactor);

          GenerateBranchArc(branchStart, branchEnd);
        }
      }
    }
  }

  /// <summary>
  /// Generates a branch arc as a separate ImmediateMesh from branchStart to branchEnd.
  /// This version builds the branch as a triangle list and forces it to render on top.
  /// </summary>
  private void GenerateBranchArc(Vector3 branchStart, Vector3 branchEnd)
  {
    // Create a new ImmediateMesh and MeshInstance3D for the branch.
    ImmediateMesh branchMesh = new ImmediateMesh();
    MeshInstance3D branchMeshInstance = new MeshInstance3D();
    branchMeshInstance.Mesh = branchMesh;

    // Use the same material as the main arc (assuming at least one exists).
    if (arcMeshInstances.Count > 0)
    {
      branchMeshInstance.MaterialOverride = arcMeshInstances[0].MaterialOverride;
    }

    // Add to group for cleanup.
    branchMeshInstance.AddToGroup("BranchArc");
    AddChild(branchMeshInstance);

    // Number of points along the branch.
    int numPoints = BranchSegments + 1;
    Vector3[] branchPoints = new Vector3[numPoints];

    // Generate branch arc points with optional jitter (except endpoints).
    for (int i = 0; i < numPoints; i++)
    {
      float t = (float)i / (numPoints - 1);
      Vector3 point = branchStart.Lerp(branchEnd, t);
      if (i != 0 && i != numPoints - 1 && BranchJitter > 0)
      {
        point.X += (float)GD.RandRange(-BranchJitter, BranchJitter);
        point.Y += (float)GD.RandRange(-BranchJitter, BranchJitter);
        point.Z += (float)GD.RandRange(-BranchJitter, BranchJitter);
      }
      branchPoints[i] = point;
    }

    // Compute per-vertex widths, tapering from a wider start to a thinner end.
    float[] widths = new float[numPoints];
    float branchStartWidth = StartWidth * BranchWidthFactor;
    float branchEndWidth = EndWidth * BranchWidthFactor;
    for (int i = 0; i < numPoints; i++)
    {
      widths[i] = Mathf.Lerp(branchStartWidth, branchEndWidth, (float)i / (numPoints - 1));
    }

    // Get the active camera and its up vector in local space.
    Camera3D camera = GetViewport().GetCamera3D();
    Vector3 localCameraUp = Vector3.Up;
    if (camera != null)
    {
      Vector3 camGlobalUp = camera.GlobalTransform.Basis.Y;
      localCameraUp = (ToLocal(camera.GlobalTransform.Origin + camGlobalUp) - ToLocal(camera.GlobalTransform.Origin)).Normalized();
    }

    // Build the branch arc as a triangle strip.
    branchMesh.ClearSurfaces();
    branchMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
    for (int i = 0; i < numPoints; i++)
    {
      // Compute tangent at this branch point.
      Vector3 tangent;
      if (i == 0)
        tangent = branchPoints[1] - branchPoints[0];
      else if (i == numPoints - 1)
        tangent = branchPoints[numPoints - 1] - branchPoints[numPoints - 2];
      else
        tangent = branchPoints[i + 1] - branchPoints[i - 1];
      tangent = tangent.Normalized();

      // Compute billboard vector using the camera's up vector.
      Vector3 billboard = localCameraUp - tangent * (localCameraUp.Dot(tangent));
      if (billboard.Length() < 0.001f)
        billboard = tangent.Cross(Vector3.Right);
      billboard = billboard.Normalized();

      // Determine left/right vertices.
      Vector3 left = branchPoints[i] + billboard * (widths[i] / 2);
      Vector3 right = branchPoints[i] - billboard * (widths[i] / 2);
      branchMesh.SurfaceAddVertex(left);
      branchMesh.SurfaceAddVertex(right);
    }
    branchMesh.SurfaceEnd();
  }

}
