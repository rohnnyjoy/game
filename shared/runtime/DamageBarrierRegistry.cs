#nullable enable

using Godot;
namespace Shared.Runtime
{
  public readonly struct DamageBarrierQuery
  {
    public DamageBarrierQuery(
      Vector3 originPosition,
      Vector3 targetPosition,
      float padding,
      DamageKind kind,
      Node3D? source,
      Node3D? target)
    {
      OriginPosition = originPosition;
      TargetPosition = targetPosition;
      Padding = padding;
      Kind = kind;
      Source = source;
      Target = target;
    }

    public Vector3 OriginPosition { get; }
    public Vector3 TargetPosition { get; }
    public float Padding { get; }
    public DamageKind Kind { get; }
    public Node3D? Source { get; }
    public Node3D? Target { get; }
  }

  public readonly struct DamageBarrierHit
  {
    public DamageBarrierHit(Node3D barrier, Vector3 position, Vector3 normal, float distance)
    {
      Barrier = barrier;
      Position = position;
      Normal = normal;
      Distance = distance;
    }

    public Node3D Barrier { get; }
    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    public float Distance { get; }
  }

  public interface IDamageBarrierSurface
  {
    Node3D BarrierNode { get; }

    bool TryGetIntersection(in DamageBarrierQuery query, out DamageBarrierHit hit);

    bool ShouldBlockDamage(in DamageBarrierQuery query, in DamageBarrierHit hit);
  }

  public static class DamageBarrierRegistry
  {
    public static void Register(IDamageBarrierSurface surface)
    {
      // Damage barriers have been retired; keep method for compatibility.
      _ = surface;
    }

    public static void Unregister(IDamageBarrierSurface surface)
    {
      _ = surface;
    }

    private static bool DebugLogging = false;

    private static void DebugLog(string message)
    {
      if (!DebugLogging)
        return;
      GD.Print($"[DamageBarrier] {message}");
    }

    private static string DescribeNode(Node3D? node)
    {
      if (node == null || !GodotObject.IsInstanceValid(node))
        return "<null>";
      return $"{node.Name}#{node.GetInstanceId()}";
    }

    public static bool BlocksDamage(in DamageBarrierQuery query)
    {
      DebugLog($"query kind={query.Kind} src={DescribeNode(query.Source)} tgt={DescribeNode(query.Target)} origin={query.OriginPosition} target={query.TargetPosition} padding={query.Padding:0.###}");
      DebugLog("not blocked (barriers disabled)");
      return false;
    }

    public static bool TryGetFirstBlockingHit(in DamageBarrierQuery query, out DamageBarrierHit hit)
    {
      _ = query;
      hit = default;
      return false;
    }

    public static bool TryGetFirstIntersection(Vector3 origin, Vector3 target, float padding, out DamageBarrierHit hit)
    {
      _ = origin;
      _ = target;
      _ = padding;
      hit = default;
      return false;
    }

    private static string DescribeSurface(IDamageBarrierSurface surface)
    {
      Node3D node = surface.BarrierNode;
      if (node != null && GodotObject.IsInstanceValid(node))
        return $"{node.Name}#{node.GetInstanceId()}";
      return surface.ToString() ?? "<barrier>";
    }
  }
}
