using Godot;
using System;
using System.Collections.Generic;

public partial class Trail : Node3D
{
  [Export] public bool Emit { get; set; } = true;
  [Export] public float Distance { get; set; } = 0.01f; // Minimum distance between sampled points
  [Export] public int Segments { get; set; } = 20;
  [Export] public float Lifetime { get; set; } = 0.5f;
  [Export] public float BaseWidth { get; set; } = 0.5f;
  [Export] public bool TiledTexture { get; set; } = false;
  [Export] public int Tiling { get; set; } = 0;
  [Export] public Curve WidthProfile { get; set; }
  [Export] public Gradient Gradient { get; set; }
  [Export] public int SmoothingIterations { get; set; } = 0;
  [Export] public float SmoothingRatio { get; set; } = 0.25f;
  [Export] public string Alignment { get; set; } = "View"; // "View", "Normal", or "Object"
  [Export] public string Axe { get; set; } = "Y"; // "X", "Y", or "Z"
  [Export] public bool ShowWireframe { get; set; } = false;
  [Export] public Color WireframeColor { get; set; } = new Color(1, 1, 1, 1);
  [Export] public float WireLineWidth { get; set; } = 1.0f;
  [Export] public int TransparencyMode { get; set; } = (int)BaseMaterial3D.TransparencyEnum.Disabled;
  // NEW: how long the trail should remain visible after stopping emission.
  [Export] public float FadeOutTime { get; set; } = 1.0f;

  private List<TrailPoint> _points = new List<TrailPoint>();
  private MeshInstance3D _meshInstance;
  private MeshInstance3D _wireInstance;
  // Timestamp (in seconds) when StopTrail() was called.
  private float _stopTime = -1f;

  // Internal class representing an individual trail point.
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
    // Ensure a parent exists (typically the bullet).
    Node3D parent = GetParent<Node3D>();
    if (parent == null)
    {
      GD.PrintErr("Trail requires a parent Node3D (typically the bullet)!");
      return;
    }

    // Create and configure the MeshInstance3D for the trail.
    _meshInstance = new MeshInstance3D();
    var mat = new StandardMaterial3D
    {
      VertexColorUseAsAlbedo = true,
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = (BaseMaterial3D.TransparencyEnum)TransparencyMode
    };
    _meshInstance.MaterialOverride = mat;
    AddChild(_meshInstance);

    // Optionally create a MeshInstance3D for the wireframe.
    if (ShowWireframe)
    {
      _wireInstance = new MeshInstance3D();
      AddChild(_wireInstance);
    }

    // Add initial points so the trail starts rendering immediately.
    AddPoint(parent.GlobalTransform);
    AddPoint(parent.GlobalTransform);

    // Connect to parent's TreeExiting signal so we can stop emitting.
    ((Node)parent).TreeExiting += OnParentExiting;
  }

  public void Initialize()
  {
    // Set the trail as top-level so its transform is independent.
    TopLevel = true;

    // Connect to the parent's TreeExiting signal if not already done.
    Node parent = GetParent();
    if (parent != null)
    {
      parent.TreeExiting += OnParentExiting;
    }
  }

  private void OnParentExiting()
  {
    StopTrail();
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
      // If no points remain and we've waited longer than FadeOutTime, then free.
      if (_points.Count == 0 && _stopTime > 0)
      {
        float currentTime = Time.GetTicksMsec() / 1000.0f;
        if (currentTime - _stopTime > FadeOutTime)
        {
          QueueFree();
        }
      }
    }
  }

  // Adds a new point if the parent has moved far enough.
  private void EmitPoint(float delta)
  {
    Node3D parent = GetParent<Node3D>();
    if (parent == null)
      return;

    if (_points.Count == 0 ||
        parent.GlobalTransform.Origin.DistanceSquaredTo(_points[_points.Count - 1].Transform.Origin) >= Distance * Distance)
    {
      AddPoint(parent.GlobalTransform);
    }
    UpdateMesh();
  }

  private void AddPoint(Transform3D transform)
  {
    _points.Add(new TrailPoint(transform, Lifetime));
  }

  private void UpdatePoints(float delta)
  {
    // Update the age of each point.
    for (int i = 0; i < _points.Count; i++)
    {
      _points[i].Update(delta);
    }
    // Remove points that have expired.
    _points.RemoveAll(pt => pt.Age <= 0);
  }

  // Optionally smooth the points.
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

  // Computes two vertices (left and right) for a trail segment based on the alignment.
  private (Vector3 left, Vector3 right) PrepareGeometry(TrailPoint prevPt, TrailPoint pt, float factor)
  {
    Vector3 normal = Vector3.Zero;
    if (Alignment == "View")
    {
      Camera3D cam = GetViewport().GetCamera3D();
      if (cam != null)
      {
        Vector3 camPos = cam.GlobalTransform.Origin;
        Vector3 pathDir = (pt.Transform.Origin - prevPt.Transform.Origin).Normalized();
        Vector3 midPoint = (pt.Transform.Origin + prevPt.Transform.Origin) * 0.5f;
        normal = (camPos - midPoint).Cross(pathDir).Normalized();
      }
      else
      {
        normal = Vector3.Up;
      }
    }
    else if (Alignment == "Normal")
    {
      if (Axe == "X")
        normal = pt.Transform.Basis.X.Normalized();
      else if (Axe == "Y")
        normal = pt.Transform.Basis.Y.Normalized();
      else if (Axe == "Z")
        normal = pt.Transform.Basis.Z.Normalized();
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

    // Taper the width from tail (0) to head (1) based on factor.
    float currentWidth = BaseWidth * factor;
    if (WidthProfile != null)
      currentWidth *= WidthProfile.Sample(factor);

    Vector3 p1 = pt.Transform.Origin - normal * currentWidth;
    Vector3 p2 = pt.Transform.Origin + normal * currentWidth;
    return (p1, p2);
  }

  // Rebuilds the mesh for the trail using a triangle strip.
  private void UpdateMesh()
  {
    if (_points.Count < 2)
    {
      _meshInstance.Mesh = null;
      if (_wireInstance != null)
        _wireInstance.Mesh = null;
      return;
    }

    var st = new SurfaceTool();
    st.Begin(Mesh.PrimitiveType.TriangleStrip);

    List<TrailPoint> pts = SmoothPoints(_points);
    float u = 0.0f;
    for (int i = 1; i < pts.Count; i++)
    {
      float factor = (float)i / (pts.Count - 1);
      Color col = new Color(1, 0, 0, 1);
      if (Gradient != null)
        col = Gradient.Sample(factor);
      st.SetColor(col);

      var (leftVertex, rightVertex) = PrepareGeometry(pts[i - 1], pts[i], factor);

      if (TiledTexture)
      {
        if (Tiling > 0)
        {
          factor *= Tiling;
        }
        else
        {
          float travel = pts[i - 1].Transform.Origin.DistanceTo(pts[i].Transform.Origin);
          u += travel / BaseWidth;
          factor = u;
        }
      }

      // Set UVs via Call since direct SetUv is unavailable.
      st.Call("set_uv", new Vector2(factor, 0));
      st.AddVertex(leftVertex);
      st.Call("set_uv", new Vector2(factor, 1));
      st.AddVertex(rightVertex);
    }
    Mesh mesh = st.Commit();
    if (mesh != null)
      _meshInstance.Mesh = mesh;

    if (ShowWireframe && _wireInstance != null)
    {
      var stWire = new SurfaceTool();
      stWire.Begin(Mesh.PrimitiveType.LineStrip);
      stWire.SetColor(WireframeColor);
      foreach (var pt in pts)
      {
        stWire.AddVertex(pt.Transform.Origin);
      }
      Mesh wireMesh = stWire.Commit();
      if (wireMesh != null)
        _wireInstance.Mesh = wireMesh;
    }
  }

  public void StopTrail()
  {
    Emit = false;
    // Record when we stopped emitting.
    _stopTime = Time.GetTicksMsec() / 1000.0f;
  }
}
