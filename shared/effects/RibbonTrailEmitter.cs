using Godot;
using System;
using System.Collections.Generic;

public partial class RibbonTrailEmitter : Node3D
{
  [Export]
  public bool Emit { get; set; } = true;

  [Export]
  public float Distance { get; set; } = 0.01f;

  [Export]
  public float Lifetime { get; set; } = 0.5f;

  [Export]
  public float BaseWidth { get; set; } = 0.5f;

  [Export]
  public bool TiledTexture { get; set; } = false;

  [Export]
  public int Tiling { get; set; } = 0;

  [Export]
  public Curve WidthProfile { get; set; }

  [Export]
  public int SmoothingIterations { get; set; } = 0;

  [Export]
  public string Alignment { get; set; } = "View";

  [Export]
  public string Axe { get; set; } = "Y";


  [Export]
  public StandardMaterial3D Material { get; set; }

  // Renamed property to clarify that width is computed based on point index.
  [Export]
  public bool IndexBasedWidth { get; set; } = true;

  private List<TrailPoint> _points = new List<TrailPoint>();
  private MeshInstance3D _meshInstance;
  private float _stopTime = -1f;

  private class TrailPoint
  {
    public Transform3D Transform;
    public float Age;

    public TrailPoint(Transform3D transform, float age)
    {
      Transform = transform;
      Age = age;
    }

    public void Update(float delta)
    {
      Age -= delta;
    }
  }

  public override void _Ready()
  {
    Node3D parent = GetParent<Node3D>();
    if (parent == null)
    {
      GD.PrintErr("Trail requires a parent Node3D (typically the bullet)!");
      return;
    }

    _meshInstance = new MeshInstance3D();
    _meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    _meshInstance.MaterialOverride = Material;
    AddChild(_meshInstance);

    // Add only one point initially.
    AddPoint(parent.GlobalTransform);
  }

  public void Initialize()
  {
    TopLevel = true;
  }

  public override void _Process(double delta)
  {
    float dt = (float)delta;
    UpdatePoints(dt);
    if (Emit)
    {
      EmitPoint(dt);
    }
    else
    {
      UpdateMesh();
    }
  }

  private void EmitPoint(float delta)
  {
    Node3D parent = GetParent<Node3D>();
    if (parent == null)
      return;

    // Only add a new point if the parent has moved sufficiently.
    if (_points.Count == 0 ||
        parent.GlobalTransform.Origin.DistanceSquaredTo(_points[_points.Count - 1].Transform.Origin) >= Distance * Distance)
    {
      AddPoint(parent.GlobalTransform);
    }
    UpdateMesh();
  }

  public void AddPoint(Transform3D transform)
  {
    _points.Add(new TrailPoint(transform, Lifetime));
  }

  private void UpdatePoints(float delta)
  {
    for (int i = 0; i < _points.Count; i++)
    {
      _points[i].Update(delta);
    }
    _points.RemoveAll(pt => pt.Age <= 0);
  }

  private List<TrailPoint> SmoothPoints(List<TrailPoint> inputPoints)
  {
    if (inputPoints.Count < 3 || SmoothingIterations <= 0)
      return inputPoints;

    List<TrailPoint> smoothed = new List<TrailPoint>(inputPoints);
    for (int i = 0; i < SmoothingIterations; i++)
    {
      List<TrailPoint> newPoints = new List<TrailPoint>();
      newPoints.Add(smoothed[0]);
      for (int j = 1; j < smoothed.Count - 1; j++)
      {
        TrailPoint A = smoothed[j - 1];
        TrailPoint B = smoothed[j];
        TrailPoint C = smoothed[j + 1];

        Transform3D t1 = A.Transform.InterpolateWith(B.Transform, 0.75f);
        Transform3D t2 = B.Transform.InterpolateWith(C.Transform, 0.25f);
        float a1 = Mathf.Lerp(A.Age, B.Age, 0.75f);
        float a2 = Mathf.Lerp(B.Age, C.Age, 0.25f);
        newPoints.Add(new TrailPoint(t1, a1));
        newPoints.Add(new TrailPoint(t2, a2));
      }
      newPoints.Add(smoothed[smoothed.Count - 1]);
      smoothed = newPoints;
    }
    return smoothed;
  }

  private void UpdateMesh()
  {
    // Only build a mesh if we have at least two distinct points.
    if (_points.Count < 2)
    {
      _meshInstance.Mesh = null;
      return;
    }

    var st = new SurfaceTool();
    st.Begin(Mesh.PrimitiveType.TriangleStrip);

    List<TrailPoint> pts = SmoothPoints(_points);

    Vector3[] leftVertices = new Vector3[pts.Count];
    Vector3[] rightVertices = new Vector3[pts.Count];

    for (int i = 0; i < pts.Count; i++)
    {
      float widthMultiplier;
      if (IndexBasedWidth)
      {
        // Use normalized index (0 at start, 1 at end).
        float t = (float)i / (pts.Count - 1);
        widthMultiplier = (WidthProfile != null) ? WidthProfile.Sample(t) : t;
      }
      else
      {
        // Use lifetime-based sampling.
        float normalizedLife = pts[i].Age / Lifetime;
        widthMultiplier = (WidthProfile != null) ? WidthProfile.Sample(normalizedLife) : normalizedLife;
      }
      float currentWidth = BaseWidth * widthMultiplier;
      Vector3 localPos = ToLocal(pts[i].Transform.Origin);

      Vector3 tangent;
      if (i == 0)
      {
        tangent = (ToLocal(pts[1].Transform.Origin) - localPos).Normalized();
      }
      else if (i == pts.Count - 1)
      {
        tangent = (localPos - ToLocal(pts[i - 1].Transform.Origin)).Normalized();
      }
      else
      {
        tangent = (ToLocal(pts[i + 1].Transform.Origin) - ToLocal(pts[i - 1].Transform.Origin)).Normalized();
      }

      Vector3 normal = Vector3.Zero;
      if (Alignment == "View")
      {
        Camera3D cam = GetViewport().GetCamera3D();
        if (cam != null)
        {
          Vector3 camPos = cam.GlobalTransform.Origin;
          Vector3 globalPos = pts[i].Transform.Origin;
          normal = (camPos - globalPos).Cross(tangent).Normalized();
        }
        else
        {
          normal = Vector3.Up;
        }
      }
      else if (Alignment == "Normal")
      {
        if (Axe == "X")
          normal = pts[i].Transform.Basis.X.Normalized();
        else if (Axe == "Y")
          normal = pts[i].Transform.Basis.Y.Normalized();
        else if (Axe == "Z")
          normal = pts[i].Transform.Basis.Z.Normalized();
      }
      else // "Object"
      {
        Node3D parent = GetParent<Node3D>();
        if (parent != null)
        {
          if (Axe == "X")
            normal = parent.GlobalTransform.Basis.X.Normalized();
          else if (Axe == "Y")
            normal = parent.GlobalTransform.Basis.Y.Normalized();
          else if (Axe == "Z")
            normal = parent.GlobalTransform.Basis.Z.Normalized();
        }
        else
        {
          normal = Vector3.Up;
        }
      }

      leftVertices[i] = localPos - normal * currentWidth;
      rightVertices[i] = localPos + normal * currentWidth;
    }

    for (int i = 0; i < pts.Count; i++)
    {
      float uvFactor = pts[i].Age / Lifetime;
      st.Call("set_uv", new Vector2(uvFactor, 0));
      st.AddVertex(leftVertices[i]);
      st.Call("set_uv", new Vector2(uvFactor, 1));
      st.AddVertex(rightVertices[i]);
    }

    Mesh mesh = st.Commit();
    if (mesh != null)
      _meshInstance.Mesh = mesh;
  }

  public void Reset()
  {
    _points.Clear();
  }
}
