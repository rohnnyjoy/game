#nullable enable

using Godot;
using System.Collections.Generic;
using Shared.Runtime;

/// <summary>
/// Static spherical barrier that prevents hostile actors and projectiles from
/// entering the shop area while remaining permeable to players. The node keeps
/// track of active instances so gameplay systems (e.g., enemies) can query and
/// push actors out of the restricted volume when needed.
/// </summary>
[Tool]
public partial class ShopSafeZone : StaticBody3D, IDamageBarrierSurface
{
  public const PhysicsLayers.Layer AssignedLayer = PhysicsLayers.Layer.SafeZone;
  public static readonly uint LayerMask = PhysicsLayers.Mask(AssignedLayer);
  public const string GroupName = "shop_safe_zones";

  private const string CollisionShapeName = nameof(CollisionShape3D);
  private const string MeshNodeName = "Visual";

  private static readonly List<ShopSafeZone> _activeZones = new();

  private CollisionShape3D? _collisionShape;
  private MeshInstance3D? _meshInstance;
  private float _radius = 4.0f;
  private StandardMaterial3D? _material;
  private bool _blocksDirectProjectiles = true;
  private bool _blocksIndirectDamage = true;

  [Export]
  public bool BlocksDirectProjectiles
  {
    get => _blocksDirectProjectiles;
    set
    {
      if (_blocksDirectProjectiles == value)
        return;
      _blocksDirectProjectiles = value;
      ApplyCollisionProfile();
    }
  }

  [Export]
  public bool BlocksIndirectDamage
  {
    get => _blocksIndirectDamage;
    set => _blocksIndirectDamage = value;
  }

  public DamageBarrierDirectionality Directionality => DamageBarrierDirectionality.Both;

  [Export(PropertyHint.Range, "0.5,50.0,0.1")] public float Radius
  {
    get => _radius;
    set
    {
      float clamped = Mathf.Max(0.5f, value);
      if (Mathf.IsEqualApprox(_radius, clamped))
        return;
      _radius = clamped;
      RefreshCollision();
      RefreshMesh();
    }
  }

  private int _radialSegments = 12;
  private int _rings = 8;

  [Export(PropertyHint.Range, "6,64,1")] public int RadialSegments
  {
    get => _radialSegments;
    set
    {
      int clamped = Mathf.Clamp(value, 6, 64);
      if (_radialSegments == clamped)
        return;
      _radialSegments = clamped;
      RefreshMesh();
    }
  }

  [Export(PropertyHint.Range, "4,32,1")] public int Rings
  {
    get => _rings;
    set
    {
      int clamped = Mathf.Clamp(value, 4, 32);
      if (_rings == clamped)
        return;
      _rings = clamped;
      RefreshMesh();
    }
  }

  [Export]
  public StandardMaterial3D? Material
  {
    get => _material;
    set
    {
      _material = value;
      ApplyMaterial();
    }
  }

  public override void _Ready()
  {
    ApplyCollisionProfile();
    CollisionMask = 0;
    ProcessMode = ProcessModeEnum.Disabled;

    EnsureCollisionShape();
    EnsureMeshInstance();
    RefreshCollision();
    RefreshMesh();
    ApplyMaterial();
  }

  public override void _EnterTree()
  {
    base._EnterTree();

    if (!IsInGroup(GroupName))
      AddToGroup(GroupName);

    if (!IsInGroup(DamageBarrierUtilities.GroupName))
      AddToGroup(DamageBarrierUtilities.GroupName);

    if (!_activeZones.Contains(this))
      _activeZones.Add(this);

    DamageBarrierRegistry.Register(this);
  }

  public override void _ExitTree()
  {
    _activeZones.Remove(this);
    DamageBarrierRegistry.Unregister(this);
    base._ExitTree();
  }

  private void EnsureCollisionShape()
  {
    _collisionShape = GetNodeOrNull<CollisionShape3D>(CollisionShapeName);
    if (_collisionShape != null)
      return;

    _collisionShape = new CollisionShape3D
    {
      Name = CollisionShapeName
    };
    AddChild(_collisionShape);
  }

  private void EnsureMeshInstance()
  {
    _meshInstance = GetNodeOrNull<MeshInstance3D>(MeshNodeName);
    if (_meshInstance != null)
      return;

    _meshInstance = new MeshInstance3D
    {
      Name = MeshNodeName,
      CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    };
    AddChild(_meshInstance);
  }

  private void RefreshCollision()
  {
    if (_collisionShape == null)
      return;

    if (_collisionShape.Shape is not SphereShape3D sphere)
    {
      sphere = new SphereShape3D();
      _collisionShape.Shape = sphere;
    }
    sphere.Radius = _radius;
  }

  private void RefreshMesh()
  {
    if (_meshInstance == null)
      return;

    SphereMesh sphereMesh;
    if (_meshInstance.Mesh is SphereMesh existing)
    {
      sphereMesh = existing;
    }
    else
    {
      sphereMesh = new SphereMesh();
      _meshInstance.Mesh = sphereMesh;
    }

    sphereMesh.RadialSegments = Mathf.Clamp(RadialSegments, 6, 64);
    sphereMesh.Rings = Mathf.Clamp(Rings, 4, 32);
    sphereMesh.Radius = _radius;
    sphereMesh.Height = _radius * 2.0f;
  }

  private void ApplyMaterial()
  {
    if (_meshInstance == null)
      return;

    _meshInstance.MaterialOverride = _material;
  }

  public static IReadOnlyList<ShopSafeZone> ActiveZones => _activeZones;

  public float EffectiveRadius => _radius;

  public bool ContainsPoint(Vector3 worldPosition, float padding = 0f)
  {
    float radius = Mathf.Max(0f, _radius + padding);
    if (radius <= 0f)
      return false;

    Vector3 toPoint = worldPosition - GlobalPosition;
    toPoint.Y = 0f;
    return toPoint.LengthSquared() <= radius * radius;
  }

  public bool TryGetRepulsion(Vector3 worldPosition, float padding, out Vector3 push)
  {
    float radius = Mathf.Max(0f, _radius + padding);
    if (radius <= 0f)
    {
      push = Vector3.Zero;
      return false;
    }

    Vector3 toPoint = worldPosition - GlobalPosition;
    Vector3 planar = new(toPoint.X, 0f, toPoint.Z);
    float distance = planar.Length();
    if (distance >= radius)
    {
      push = Vector3.Zero;
      return false;
    }

    if (distance < 0.0001f)
      planar = Vector3.Right;

    Vector3 direction = planar.Normalized();
    float penetration = radius - distance;
    push = new Vector3(direction.X, 0f, direction.Z) * penetration;
    return true;
  }

  public static bool TryGetRepulsion(Vector3 worldPosition, float padding, out Vector3 push, out ShopSafeZone? zone)
  {
    foreach (ShopSafeZone candidate in _activeZones)
    {
      if (candidate != null && candidate.TryGetRepulsion(worldPosition, padding, out push))
      {
        zone = candidate;
        return true;
      }
    }

    push = Vector3.Zero;
    zone = null;
    return false;
  }

  public bool TryGetSegmentIntersection(Vector3 from, Vector3 to, out Vector3 hitPoint, out Vector3 normal, out float fraction)
  {
    hitPoint = Vector3.Zero;
    normal = Vector3.Zero;
    fraction = 0.0f;

    Vector3 dir = to - from;
    float lengthSquared = dir.LengthSquared();
    if (lengthSquared <= 0.000001f)
    {
      if (!ContainsPoint(from))
        return false;
      Vector3 outward = from - GlobalPosition;
      if (outward.LengthSquared() <= 0.000001f)
        outward = Vector3.Up;
      else
        outward = outward.Normalized();
      hitPoint = from;
      normal = outward;
      fraction = 0.0f;
      return true;
    }

    float radius = _radius;
    Vector3 m = from - GlobalPosition;
    float a = lengthSquared;
    float b = 2.0f * m.Dot(dir);
    float c = m.Dot(m) - radius * radius;

    float discriminant = b * b - 4.0f * a * c;
    if (discriminant < 0.0f)
      return false;

    float sqrtDiscriminant = Mathf.Sqrt(discriminant);
    float denom = 2.0f * a;

    float t0 = (-b - sqrtDiscriminant) / denom;
    float t1 = (-b + sqrtDiscriminant) / denom;

    bool found = false;
    float bestT = float.MaxValue;

    if (t0 >= 0.0f && t0 <= 1.0f)
    {
      bestT = Mathf.Min(bestT, t0);
      found = true;
    }
    if (t1 >= 0.0f && t1 <= 1.0f)
    {
      bestT = Mathf.Min(bestT, t1);
      found = true;
    }

    if (!found)
      return false;

    Vector3 point = from + dir * bestT;
    Vector3 outwardNormal = point - GlobalPosition;
    if (outwardNormal.LengthSquared() <= 0.000001f)
      outwardNormal = Vector3.Up;
    else
      outwardNormal = outwardNormal.Normalized();

    hitPoint = point;
    normal = outwardNormal;
    fraction = Mathf.Clamp(bestT, 0.0f, 1.0f);
    return true;
  }

  public Node3D BarrierNode => this;

  public bool TryGetIntersection(in DamageBarrierQuery query, out DamageBarrierHit hit)
  {
    hit = default;
    Vector3 from = query.OriginPosition;
    Vector3 to = query.TargetPosition;
    Vector3 dir = to - from;
    float length = dir.Length();
    if (length <= 0.000001f)
    {
      if (!ContainsPoint(from, query.Padding))
        return false;
      Vector3 outward = from - GlobalPosition;
      if (outward.LengthSquared() <= 0.000001f)
        outward = Vector3.Up;
      else
        outward = outward.Normalized();
      hit = new DamageBarrierHit(this, from, outward, 0.0f);
      return true;
    }

    float radius = Mathf.Max(0.0f, _radius + query.Padding);
    Vector3 m = from - GlobalPosition;
    float a = dir.Dot(dir);
    float b = 2.0f * m.Dot(dir);
    float c = m.Dot(m) - radius * radius;
    float discriminant = b * b - 4.0f * a * c;
    if (discriminant < 0.0f)
      return false;

    float sqrtDiscriminant = Mathf.Sqrt(discriminant);
    float denom = 2.0f * a;
    float t0 = (-b - sqrtDiscriminant) / denom;
    float t1 = (-b + sqrtDiscriminant) / denom;

    float bestT = float.PositiveInfinity;
    if (t0 >= 0.0f && t0 <= 1.0f)
      bestT = Mathf.Min(bestT, t0);
    if (t1 >= 0.0f && t1 <= 1.0f)
      bestT = Mathf.Min(bestT, t1);

    if (float.IsNaN(bestT) || float.IsInfinity(bestT))
      return false;

    Vector3 point = from + dir * bestT;
    Vector3 normal = point - GlobalPosition;
    if (normal.LengthSquared() <= 0.000001f)
      normal = Vector3.Up;
    else
      normal = normal.Normalized();

    float distance = length * Mathf.Clamp(bestT, 0.0f, 1.0f);
    hit = new DamageBarrierHit(this, point, normal, distance);
    return true;
  }

  public bool ShouldBlockDamage(in DamageBarrierQuery query, in DamageBarrierHit hit)
  {
    if (!DamageBarrierUtilities.BlocksKind(query.Kind, _blocksDirectProjectiles, _blocksIndirectDamage))
      return false;
    return DamageBarrierUtilities.PassesDirection(Directionality, query.OriginPosition, query.TargetPosition, hit.Normal);
  }

  private void ApplyCollisionProfile()
  {
    CollisionLayer = _blocksDirectProjectiles ? LayerMask : 0;
  }
}
