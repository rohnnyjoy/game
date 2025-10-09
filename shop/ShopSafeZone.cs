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
  private float _barrierThickness = 0.5f;
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

  [Export(PropertyHint.Range, "0.0,10.0,0.05")]
  public float BarrierThickness
  {
    get => _barrierThickness;
    set
    {
      float clamped = Mathf.Max(0.0f, value);
      if (Mathf.IsEqualApprox(_barrierThickness, clamped))
        return;
      _barrierThickness = clamped;
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
    sphere.Radius = GetOuterRadius();
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

    float outerRadius = GetOuterRadius();

    sphereMesh.RadialSegments = Mathf.Clamp(RadialSegments, 6, 64);
    sphereMesh.Rings = Mathf.Clamp(Rings, 4, 32);
    sphereMesh.Radius = outerRadius;
    sphereMesh.Height = outerRadius * 2.0f;
  }

  private void ApplyMaterial()
  {
    if (_meshInstance == null)
      return;

    _meshInstance.MaterialOverride = _material;
  }

  public static IReadOnlyList<ShopSafeZone> ActiveZones => _activeZones;

  private float GetHalfThickness()
  {
    return Mathf.Max(0.0f, _barrierThickness * 0.5f);
  }

  private float GetInnerRadius(float padding = 0f)
  {
    return Mathf.Max(0.0f, _radius - GetHalfThickness() + padding);
  }

  private float GetOuterRadius(float padding = 0f)
  {
    float innerRadius = GetInnerRadius(padding);
    float outerRadius = _radius + GetHalfThickness() + padding;
    return Mathf.Max(innerRadius, outerRadius);
  }

  public float EffectiveRadius => GetOuterRadius();

  public bool ContainsPoint(Vector3 worldPosition, float padding = 0f)
  {
    float innerRadius = GetInnerRadius(padding);
    if (innerRadius <= 0f)
      return false;

    Vector3 toPoint = worldPosition - GlobalPosition;
    toPoint.Y = 0f;
    return toPoint.LengthSquared() <= innerRadius * innerRadius;
  }

  public bool TryGetRepulsion(Vector3 worldPosition, float padding, out Vector3 push)
  {
    float radius = GetOuterRadius(padding);
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

    Vector3 center = GlobalPosition;
    Vector3 dir = to - from;
    float lengthSquared = dir.LengthSquared();
    float innerRadius = GetInnerRadius();
    float outerRadius = GetOuterRadius();
    float innerRadiusSq = innerRadius * innerRadius;
    float outerRadiusSq = outerRadius * outerRadius;

    Vector3 fromOffset = from - center;
    float fromDistSq = fromOffset.LengthSquared();

    if (lengthSquared <= 0.000001f)
    {
      if (fromDistSq > outerRadiusSq)
        return false;

      Vector3 outward = fromOffset;
      if (fromDistSq <= innerRadiusSq)
        outward = outward.LengthSquared() <= 0.000001f ? Vector3.Up : -outward.Normalized();
      else
        outward = outward.LengthSquared() <= 0.000001f ? Vector3.Up : outward.Normalized();

      hitPoint = from;
      normal = outward;
      return true;
    }

    bool fromInsideInner = fromDistSq <= innerRadiusSq;
    bool fromInsideOuter = fromDistSq <= outerRadiusSq;
    bool fromInsideShell = fromInsideOuter && !fromInsideInner;

    if (fromInsideShell)
    {
      Vector3 outward = fromDistSq <= 0.000001f ? Vector3.Up : fromOffset.Normalized();
      hitPoint = from;
      normal = outward;
      return true;
    }

    bool TrySolve(float radius, bool invertNormal, out float t, out Vector3 point, out Vector3 surfaceNormal)
    {
      t = 0.0f;
      point = Vector3.Zero;
      surfaceNormal = Vector3.Zero;

      if (radius <= 0.0f)
        return false;

      Vector3 m = fromOffset;
      float a = lengthSquared;
      float b = 2.0f * m.Dot(dir);
      float c = m.Dot(m) - radius * radius;

      float discriminant = b * b - 4.0f * a * c;
      if (discriminant < 0.0f)
        return false;

      float sqrtDiscriminant = Mathf.Sqrt(discriminant);
      float denom = 2.0f * a;

      float candidate0 = (-b - sqrtDiscriminant) / denom;
      float candidate1 = (-b + sqrtDiscriminant) / denom;
      float best = float.PositiveInfinity;

      if (candidate0 >= 0.0f && candidate0 <= 1.0f)
        best = Mathf.Min(best, candidate0);
      if (candidate1 >= 0.0f && candidate1 <= 1.0f)
        best = Mathf.Min(best, candidate1);

      if (float.IsNaN(best) || float.IsInfinity(best))
        return false;

      Vector3 surfacePoint = from + dir * best;
      Vector3 outwardNormal = surfacePoint - center;
      if (outwardNormal.LengthSquared() <= 0.000001f)
        outwardNormal = Vector3.Up;
      else
        outwardNormal = outwardNormal.Normalized();

      if (invertNormal)
        outwardNormal = -outwardNormal;

      t = best;
      point = surfacePoint;
      surfaceNormal = outwardNormal;
      return true;
    }

    if (fromInsideInner)
    {
      if (TrySolve(innerRadius, invertNormal: true, out float hitT, out Vector3 point, out Vector3 surfaceNormal))
      {
        hitPoint = point;
        normal = surfaceNormal;
        fraction = Mathf.Clamp(hitT, 0.0f, 1.0f);
        return true;
      }
      return false;
    }

    if (TrySolve(outerRadius, invertNormal: false, out float outerT, out Vector3 outerPoint, out Vector3 outerNormal))
    {
      hitPoint = outerPoint;
      normal = outerNormal;
      fraction = Mathf.Clamp(outerT, 0.0f, 1.0f);
      return true;
    }

    return false;
  }


  public Node3D BarrierNode => this;

  public bool TryGetIntersection(in DamageBarrierQuery query, out DamageBarrierHit hit)
  {
    hit = default;
    Vector3 from = query.OriginPosition;
    Vector3 to = query.TargetPosition;
    Vector3 dir = to - from;
    float length = dir.Length();
    float innerRadius = GetInnerRadius(query.Padding);
    float outerRadius = GetOuterRadius(query.Padding);
    float innerRadiusSq = innerRadius * innerRadius;
    float outerRadiusSq = outerRadius * outerRadius;

    Vector3 center = GlobalPosition;
    Vector3 fromOffset = from - center;
    float fromDistSq = fromOffset.LengthSquared();

    if (length <= 0.000001f)
    {
      if (fromDistSq > outerRadiusSq)
        return false;

      Vector3 outward = fromOffset;
      if (fromDistSq <= innerRadiusSq)
        outward = outward.LengthSquared() <= 0.000001f ? Vector3.Up : -outward.Normalized();
      else
        outward = outward.LengthSquared() <= 0.000001f ? Vector3.Up : outward.Normalized();

      hit = new DamageBarrierHit(this, from, outward, 0.0f);
      return true;
    }

    float lengthSquared = dir.Dot(dir);
    bool fromInsideInner = fromDistSq <= innerRadiusSq;
    bool fromInsideOuter = fromDistSq <= outerRadiusSq;
    bool fromInsideShell = fromInsideOuter && !fromInsideInner;

    if (fromInsideShell)
    {
      Vector3 outward = fromDistSq <= 0.000001f ? Vector3.Up : fromOffset.Normalized();
      hit = new DamageBarrierHit(this, from, outward, 0.0f);
      return true;
    }

    bool TrySolve(float radius, bool invertNormal, out DamageBarrierHit barrierHit)
    {
      barrierHit = default;
      if (radius <= 0.0f)
        return false;

      Vector3 m = fromOffset;
      float a = lengthSquared;
      float b = 2.0f * m.Dot(dir);
      float c = m.Dot(m) - radius * radius;

      float discriminant = b * b - 4.0f * a * c;
      if (discriminant < 0.0f)
        return false;

      float sqrtDiscriminant = Mathf.Sqrt(discriminant);
      float denom = 2.0f * a;
      float candidate0 = (-b - sqrtDiscriminant) / denom;
      float candidate1 = (-b + sqrtDiscriminant) / denom;
      float best = float.PositiveInfinity;

      if (candidate0 >= 0.0f && candidate0 <= 1.0f)
        best = Mathf.Min(best, candidate0);
      if (candidate1 >= 0.0f && candidate1 <= 1.0f)
        best = Mathf.Min(best, candidate1);

      if (float.IsNaN(best) || float.IsInfinity(best))
        return false;

      Vector3 point = from + dir * best;
      Vector3 normal = point - center;
      if (normal.LengthSquared() <= 0.000001f)
        normal = Vector3.Up;
      else
        normal = normal.Normalized();

      if (invertNormal)
        normal = -normal;

      float distance = length * Mathf.Clamp(best, 0.0f, 1.0f);
      barrierHit = new DamageBarrierHit(this, point, normal, distance);
      return true;
    }

    if (fromInsideInner)
    {
      if (TrySolve(innerRadius, invertNormal: true, out DamageBarrierHit innerHit))
      {
        hit = innerHit;
        return true;
      }
      return false;
    }

    if (TrySolve(outerRadius, invertNormal: false, out DamageBarrierHit outerHit))
    {
      hit = outerHit;
      return true;
    }

    return false;
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
