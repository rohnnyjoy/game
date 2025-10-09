#nullable enable

using Godot;
using Shared.Runtime;

public partial class DamageBarrierPlane : DamageBarrierSurface
{
  [Export]
  public Vector3 PlaneNormal
  {
    get => _planeNormal;
    set
    {
      Vector3 normalized = value.LengthSquared() > 0.0001f ? value.Normalized() : Vector3.Up;
      if (_planeNormal == normalized)
        return;
      _planeNormal = normalized;
      SyncShape();
    }
  }

  [Export]
  public float PlaneDistance
  {
    get => _planeDistance;
    set
    {
      if (Mathf.IsEqualApprox(_planeDistance, value))
        return;
      _planeDistance = value;
      SyncShape();
    }
  }

  private CollisionShape3D? _collisionShape;
  private Vector3 _planeNormal = Vector3.Up;
  private float _planeDistance = 0.0f;

  public override void _Ready()
  {
    EnsureCollisionShape();
    SyncShape();
    base._Ready();
  }

  private void EnsureCollisionShape()
  {
    _collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
    if (_collisionShape != null)
      return;

    _collisionShape = new CollisionShape3D
    {
      Name = "CollisionShape3D"
    };
    AddChild(_collisionShape);
  }

  private void SyncShape()
  {
    if (_collisionShape == null)
      return;

    if (_collisionShape.Shape is not WorldBoundaryShape3D plane)
    {
      plane = new WorldBoundaryShape3D();
      _collisionShape.Shape = plane;
    }

    plane.Plane = new Plane(_planeNormal, _planeDistance);
  }

  public override bool TryGetIntersection(in DamageBarrierQuery query, out DamageBarrierHit hit)
  {
    hit = default;
    if (_collisionShape?.Shape is not WorldBoundaryShape3D planeShape)
      return false;

    Plane plane = planeShape.Plane;
    Vector3 from = query.OriginPosition;
    Vector3 to = query.TargetPosition;
    Vector3 dir = to - from;
    float length = dir.Length();
    if (length <= 0.000001f)
      return false;

    float fromDist = plane.DistanceTo(from);
    float toDist = plane.DistanceTo(to);

    // If both points lie on the same side, no crossing.
    if (fromDist > 0.0f && toDist > 0.0f)
      return false;
    if (fromDist < 0.0f && toDist < 0.0f)
      return false;

    float denom = fromDist - toDist;
    if (Mathf.Abs(denom) <= 0.000001f)
      return false;

    float t = fromDist / denom;
    if (t < 0.0f || t > 1.0f)
      return false;

    Vector3 point = from + dir * t;
    Vector3 normal = plane.Normal;

    float distance = length * t;
    hit = new DamageBarrierHit(this, point, normal, distance);
    return true;
  }
}
