using Godot;
#nullable enable
using System;
using System.Collections.Generic;

public sealed partial class CursedSkullBeamVfxManager : Node3D
{
  public static CursedSkullBeamVfxManager? Instance { get; private set; }

  [Export] public float DefaultLifetime { get; set; } = 6.32f;
  [Export] public float DefaultWidth { get; set; } = 0.5f;
  [Export] public Color BeamColor { get; set; } = new Color(0.85f, 0.25f, 1.0f, 1.0f);

  private const float FramePixelWidth = 32f;
  private const float TopPixelHeight = 48f;
  private const float MidPixelHeight = 48f;
  private const float BottomPixelHeight = 48f;

  private readonly List<Beam> _beams = new();
  private MultiMesh _multiMesh = null!;
  private MultiMeshInstance3D _multiMeshInstance = null!;
  private ShaderMaterial _material = null!;

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
    public float Strength;

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

      float width = beam.Width;
      var basis = new Basis(axisX * width, forward * len, axisZ);
      _multiMesh.SetInstanceTransform(i, new Transform3D(basis, center));
      float progress = beam.Lifetime <= 0.0001f ? 1f : Mathf.Clamp((now - beam.SpawnTime) / beam.Lifetime, 0f, 1f);
      float repeatFactor = CalculateRepeatFactor(len, width);
      float pixelScale = Mathf.Max(0.0001f, width / FramePixelWidth);
      _multiMesh.SetInstanceCustomData(i, new Color(progress, repeatFactor, pixelScale, beam.Strength));
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
      Strength = Mathf.Max(0.05f, strength),
    };

    _beams.Add(beam);
  }

  private void InitializeRenderer()
  {
    _multiMesh = new MultiMesh
    {
      TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
      UseCustomData = true,
    };

    var quad = new QuadMesh
    {
      Size = new Vector2(1f, 1f),
    };

    _multiMesh.Mesh = quad;

    var shader = GD.Load<Shader>("res://shared/shaders/cursed_skull_beam.gdshader");
    var texture = GD.Load<Texture2D>("res://assets/sprites/effects/curse_32x96.png");

    _material = new ShaderMaterial
    {
      Shader = shader,
      ResourceLocalToScene = true,
    };

    float textureWidth = texture.GetWidth();
    float textureHeight = texture.GetHeight();
    const float framePixelWidth = 32.0f;
    float frameCount = textureWidth > 0.0f ? textureWidth / framePixelWidth : 1.0f;

    _material.SetShaderParameter("effect_texture", texture);
    _material.SetShaderParameter("frame_count", frameCount);
    _material.SetShaderParameter("frame_pixel_width", framePixelWidth);
    _material.SetShaderParameter("frame_index", 1.0f);
    _material.SetShaderParameter("texture_pixel_size", new Vector2(textureWidth, textureHeight));
    _material.SetShaderParameter("top_pixels", 48.0f);
    _material.SetShaderParameter("mid_pixels", 48.0f);
    _material.SetShaderParameter("bottom_pixels", 48.0f);

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

  private static float CalculateRepeatFactor(float length, float width)
  {
    float pixelScale = Mathf.Max(0.0001f, width / FramePixelWidth);
    float baseline = Mathf.Max(0.0001f, MidPixelHeight * pixelScale);
    return Mathf.Max(1f, length / baseline);
  }
}
