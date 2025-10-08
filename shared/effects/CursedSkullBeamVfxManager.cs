using Godot;
#nullable enable
using System;
using System.Collections.Generic;

public sealed partial class CursedSkullBeamVfxManager : Node3D
{
  public static CursedSkullBeamVfxManager? Instance { get; private set; }

  [Export] public float DefaultLifetime { get; set; } = 0.32f;
  [Export] public float DefaultWidth { get; set; } = 0.5f;
  [Export] public Color BeamColor { get; set; } = new Color(0.85f, 0.25f, 1.0f, 1.0f);

  private readonly List<Beam> _beams = new();
  private MultiMesh _multiMesh = null!;
  private MultiMeshInstance3D _multiMeshInstance = null!;
  private StandardMaterial3D _material = null!;

  private sealed class Beam
  {
    public Node3D? OriginNode;
    public Node3D? TargetNode;
    public Vector3 StartPos;
    public Vector3 EndPos;
    public Vector3 OriginOffset;
    public Vector3 TargetOffset;
    public float SpawnTime;
    public float Lifetime;
    public float Width;

    public Vector3 GetStart()
    {
      if (OriginNode != null && GodotObject.IsInstanceValid(OriginNode))
        StartPos = OriginNode.GlobalTransform.Origin + OriginOffset;
      return StartPos;
    }

    public Vector3 GetEnd()
    {
      if (TargetNode != null && GodotObject.IsInstanceValid(TargetNode))
        EndPos = TargetNode.GlobalTransform.Origin + TargetOffset;
      return EndPos;
    }
  }

  public override void _EnterTree()
  {
    Instance = this;
    ProcessMode = ProcessModeEnum.Always;
  }

  public override void _ExitTree()
  {
    if (Instance == this)
      Instance = null;
  }

  public override void _Ready()
  {
    InitializeRenderer();
  }

  public override void _Process(double delta)
  {
    if (_multiMesh == null)
      return;

    float now = GetTimeSeconds();
    int write = 0;
    for (int i = 0; i < _beams.Count; i++)
    {
      var beam = _beams[i];
      if (now - beam.SpawnTime >= beam.Lifetime)
        continue;
      _beams[write++] = beam;
    }
    if (write < _beams.Count)
      _beams.RemoveRange(write, _beams.Count - write);

    int active = _beams.Count;
    _multiMesh.InstanceCount = active;
    if (active == 0)
      return;

    var cam = GetViewport()?.GetCamera3D();

    for (int i = 0; i < active; i++)
    {
      var beam = _beams[i];
      Vector3 start = beam.GetStart();
      Vector3 end = beam.GetEnd();
      Vector3 dir = end - start;
      float len = dir.Length();
      if (len < 0.001f)
        len = 0.001f;

      Vector3 center = (start + end) * 0.5f;
      Vector3 forward = dir / len;

      Vector3 lookDir = Vector3.Up;
      if (cam != null)
      {
        lookDir = (cam.GlobalPosition - center).Normalized();
        if (lookDir.LengthSquared() < 1e-6f)
          lookDir = Vector3.Up;
      }

      Vector3 axisX = lookDir.Cross(forward);
      if (axisX.LengthSquared() < 1e-6f)
        axisX = forward.Cross(Vector3.Up);
      if (axisX.LengthSquared() < 1e-6f)
        axisX = Vector3.Right;
      axisX = axisX.Normalized();
      Vector3 axisZ = forward.Cross(axisX).Normalized();

      float radius = beam.Width * 0.5f;
      var basis = new Basis(axisX * radius, forward * len, axisZ * radius);
      _multiMesh.SetInstanceTransform(i, new Transform3D(basis, center));
    }
  }

  public static void Spawn(Vector3 start, Vector3 end, float strength = 1f, float? lifetimeOverride = null, float? widthOverride = null)
  {
    Instance?.SpawnInternal(start, end, null, Vector3.Zero, null, Vector3.Zero, strength, lifetimeOverride, widthOverride);
  }

  public static void Spawn(Node3D? originNode, Node3D? targetNode, float strength = 1f, float? lifetimeOverride = null, float? widthOverride = null)
  {
    if (Instance == null)
      return;
    Vector3 start = originNode != null && GodotObject.IsInstanceValid(originNode) ? originNode.GlobalTransform.Origin : Vector3.Zero;
    Vector3 end = targetNode != null && GodotObject.IsInstanceValid(targetNode) ? targetNode.GlobalTransform.Origin : Vector3.Zero;
    Instance.SpawnInternal(start, end, originNode, Vector3.Zero, targetNode, Vector3.Zero, strength, lifetimeOverride, widthOverride);
  }

  public static void Spawn(Node3D? originNode, Vector3 originOffset, Node3D? targetNode, Vector3 targetOffset, float strength = 1f, float? lifetimeOverride = null, float? widthOverride = null)
  {
    if (Instance == null)
      return;
    Vector3 start = originNode != null && GodotObject.IsInstanceValid(originNode) ? originNode.GlobalTransform.Origin + originOffset : Vector3.Zero;
    Vector3 end = targetNode != null && GodotObject.IsInstanceValid(targetNode) ? targetNode.GlobalTransform.Origin + targetOffset : Vector3.Zero;
    Instance.SpawnInternal(start, end, originNode, originOffset, targetNode, targetOffset, strength, lifetimeOverride, widthOverride);
  }

  private void SpawnInternal(Vector3 start, Vector3 end, Node3D? originNode, Vector3 originOffset, Node3D? targetNode, Vector3 targetOffset, float strength, float? lifetimeOverride, float? widthOverride)
  {
    if (!IsInsideTree())
      return;

    Vector3 dir = end - start;
    if (dir.LengthSquared() < 1e-6f)
      return;

    var beam = new Beam
    {
      OriginNode = originNode,
      TargetNode = targetNode,
      StartPos = start,
      EndPos = end,
      OriginOffset = originOffset,
      TargetOffset = targetOffset,
      SpawnTime = GetTimeSeconds(),
      Lifetime = Mathf.Max(0.05f, lifetimeOverride ?? DefaultLifetime),
      Width = Mathf.Max(0.05f, widthOverride ?? DefaultWidth),
    };

    _beams.Add(beam);
  }

  private void InitializeRenderer()
  {
    var mesh = new CylinderMesh
    {
      Height = 1f,
      TopRadius = 0.5f,
      BottomRadius = 0.5f,
      RadialSegments = 16,
    };

    _multiMesh = new MultiMesh
    {
      TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
      Mesh = mesh,
      UseCustomData = false,
    };

    _material = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      AlbedoColor = BeamColor,
      Emission = BeamColor,
      EmissionEnabled = true,
      Roughness = 0f,
    };

    _multiMeshInstance = new MultiMeshInstance3D
    {
      Multimesh = _multiMesh,
      Visible = true,
      CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
      MaterialOverride = _material,
      TopLevel = true,
      Layers = 1,
    };

    AddChild(_multiMeshInstance);
  }

  private static float GetTimeSeconds() => (float)Time.GetTicksMsec() / 1000f;
}
