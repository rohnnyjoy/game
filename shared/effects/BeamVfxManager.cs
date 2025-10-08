using Godot;
#nullable enable
using System;
using System.Collections.Generic;

public sealed partial class BeamVfxManager : Node3D
{
  public static BeamVfxManager? Instance { get; private set; }

  [Export] public float DefaultLifetime { get; set; } = 6.32f;
  [Export] public float DefaultWidth { get; set; } = 0.5f;
  [Export] public Color BeamColor { get; set; } = new Color(0.85f, 0.25f, 1.0f, 1.0f);
  [Export] public BeamStyleResource? Style { get; set; }
  [Export(PropertyHint.Range, "0.0,2.0,0.01")] public float BaseTrimMinimum { get; set; } = 0.2f;
  [Export(PropertyHint.Range, "0.0,5.0,0.01")] public float WidthTrimScale { get; set; } = 0.35f;
  [Export(PropertyHint.Range, "0.0,2.0,0.01")] public float WidthTrimBias { get; set; } = 0.1f;
  [Export(PropertyHint.Range, "0.0,2.0,0.01")] public float CollisionRadiusScale { get; set; } = 0.6f;
  [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float MinimumSpanSlack { get; set; } = 0.1f;

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
    public float OriginTrim;
    public float TargetTrim;

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
      ApplyEndpointPadding(ref start, ref end, beam.OriginTrim, beam.TargetTrim);
      beam.StartPos = start;
      beam.EndPos = end;

      Vector3 dir = end - start;
      float len = dir.Length();
      if (len < 0.001f)
      {
        len = 0.001f;
        dir = Vector3.Forward * len;
      }

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
      var basis = new Basis(axisX * width, -forward * len, axisZ);
      _multiMesh.SetInstanceTransform(i, new Transform3D(basis, center));
      float repeatFactor = CalculateRepeatFactor(len, width);
      float pixelScale = Mathf.Max(0.0001f, width / FramePixelWidth);
      float age = Mathf.Clamp(now - beam.SpawnTime, 0f, beam.Lifetime);
      _multiMesh.SetInstanceCustomData(i, new Color(age, beam.Lifetime, repeatFactor, pixelScale));
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

    beam.OriginTrim = CalculateEndpointPadding(originNode, beam.Width);
    beam.TargetTrim = CalculateEndpointPadding(targetNode, beam.Width);

    ApplyEndpointPadding(ref beam.StartPos, ref beam.EndPos, beam.OriginTrim, beam.TargetTrim);

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

    var shader = Style?.Shader ?? GD.Load<Shader>("res://shared/shaders/cursed_skull_beam.gdshader");
    var texture = Style?.Texture ?? GD.Load<Texture2D>("res://assets/sprites/effects/curse_32x96.png");

    _material = new ShaderMaterial
    {
      Shader = shader,
      ResourceLocalToScene = true,
    };

    float textureWidth = texture.GetWidth();
    float textureHeight = texture.GetHeight();
    float framePixelWidth = Style?.FramePixelWidth ?? 32.0f;
    float frameCount = textureWidth > 0.0f ? textureWidth / Mathf.Max(1.0f, framePixelWidth) : 1.0f;

    _material.SetShaderParameter("effect_texture", texture);
    _material.SetShaderParameter("frame_count", frameCount);
    _material.SetShaderParameter("frame_pixel_width", framePixelWidth);
    _material.SetShaderParameter("frame_index", -1.0f);
    _material.SetShaderParameter("animation_fps", Style?.AnimationFps ?? 22.0f);
    _material.SetShaderParameter("frame_offset", Style?.FrameOffset ?? 0.0f);
    _material.SetShaderParameter("animate_once", Style?.AnimateOnce ?? true);
    _material.SetShaderParameter("texture_pixel_size", new Vector2(textureWidth, textureHeight));
    _material.SetShaderParameter("top_pixels", Style?.TopPixels ?? 48.0f);
    _material.SetShaderParameter("mid_pixels", Style?.MidPixels ?? 48.0f);
    _material.SetShaderParameter("bottom_pixels", Style?.BottomPixels ?? 48.0f);

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

  private void ApplyEndpointPadding(ref Vector3 start, ref Vector3 end, float originTrim, float targetTrim)
  {
    float totalTrim = originTrim + targetTrim;
    if (totalTrim <= 0f)
      return;

    Vector3 span = end - start;
    float length = span.Length();
    if (length < 0.001f)
      return;

    float slack = MathF.Max(0f, MinimumSpanSlack);
    float maxAllowedTrim = MathF.Max(0f, length - slack);
    if (maxAllowedTrim <= 0f)
      return;

    if (totalTrim > maxAllowedTrim)
    {
      float scale = maxAllowedTrim / totalTrim;
      originTrim *= scale;
      targetTrim *= scale;
      totalTrim = originTrim + targetTrim;
      if (totalTrim <= 0f)
        return;
    }

    Vector3 direction = span / length;
    start += direction * originTrim;
    end -= direction * targetTrim;
  }

  private float CalculateEndpointPadding(Node3D? node, float beamWidth)
  {
    float baseTrim = MathF.Max(BaseTrimMinimum, beamWidth * WidthTrimScale + WidthTrimBias);
    if (node == null)
      return baseTrim;

    float radius = EstimateNodeRadius(node);
    if (radius <= 0f)
      return baseTrim;

    return MathF.Max(baseTrim, radius * CollisionRadiusScale);
  }

  private static float EstimateNodeRadius(Node3D node)
  {
    float best = 0f;

    AccumulateCollisionExtents(node, node, ref best);

    if (best <= 0f && node is VisualInstance3D visual)
    {
      Aabb aabb = visual.GetAabb();
      Vector3 scale = ExtractScale(visual.GlobalTransform.Basis);
      Vector3 scaledSize = new Vector3(aabb.Size.X * scale.X, aabb.Size.Y * scale.Y, aabb.Size.Z * scale.Z);
      best = MathF.Max(best, scaledSize.Length() * 0.5f);
    }

    return best;
  }

  private static void AccumulateCollisionExtents(Node current, Node3D reference, ref float best)
  {
    if (current is CollisionShape3D collisionShape)
    {
      float extent = EstimateCollisionShapeExtent(collisionShape, reference);
      if (extent > best)
        best = extent;
    }

    foreach (Node child in current.GetChildren())
      AccumulateCollisionExtents(child, reference, ref best);
  }

  private static float EstimateCollisionShapeExtent(CollisionShape3D collisionShape, Node3D reference)
  {
    var shape = collisionShape.Shape;
    if (shape == null)
      return 0f;

    float localExtent = shape switch
    {
      SphereShape3D sphere => sphere.Radius,
      BoxShape3D box => box.Size.Length() * 0.5f,
      CapsuleShape3D capsule => capsule.Radius + capsule.Height * 0.5f,
      CylinderShape3D cylinder => cylinder.Radius + cylinder.Height * 0.5f,
      ConvexPolygonShape3D convex => EstimateConvexExtent(convex),
      SeparationRayShape3D ray => ray.Length,
      _ => 0.5f,
    };

    Vector3 scale = ExtractScale(collisionShape.GlobalTransform.Basis);
    float scaleFactor = MathF.Max(1f, MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z)));
    Vector3 offset = collisionShape.GlobalTransform.Origin - reference.GlobalTransform.Origin;
    return localExtent * scaleFactor + offset.Length();
  }

  private static float EstimateConvexExtent(ConvexPolygonShape3D convex)
  {
    var points = convex.Points;
    if (points == null || points.Length == 0)
      return 0.5f;

    float best = 0f;
    for (int i = 0; i < points.Length; i++)
    {
      float length = points[i].Length();
      if (length > best)
        best = length;
    }
    return MathF.Max(0.5f, best);
  }

  private static Vector3 ExtractScale(Basis basis)
  {
    Vector3 column0 = basis * Vector3.Right;
    Vector3 column1 = basis * Vector3.Up;
    Vector3 column2 = basis * Vector3.Forward;
    return new Vector3(column0.Length(), column1.Length(), column2.Length());
  }
}
