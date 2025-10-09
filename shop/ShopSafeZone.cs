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
public partial class ShopSafeZone : StaticBody3D
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
    CollisionLayer = LayerMask;
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

    if (!_activeZones.Contains(this))
      _activeZones.Add(this);
  }

  public override void _ExitTree()
  {
    _activeZones.Remove(this);
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
}
