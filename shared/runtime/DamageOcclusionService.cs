#nullable enable

using Godot;

namespace Shared.Runtime
{
  public static class DamageOcclusionService
  {
    public static bool IsBlocked(Node? context, Vector3 from, Vector3 to, out Vector3 hitPoint, out Vector3 hitNormal)
    {
      hitPoint = Vector3.Zero;
      hitNormal = Vector3.Zero;

      Node3D? source = (context != null && GodotObject.IsInstanceValid(context)) ? context as Node3D : null;

      return false;
    }

    public static bool IsBlocked(PhysicsDirectSpaceState3D? _, Vector3 from, Vector3 to, out Vector3 hitPoint, out Vector3 hitNormal)
    {
      return IsBlocked(context: null, from, to, out hitPoint, out hitNormal);
    }
  }
}
