#nullable enable

using Godot;
using System.Collections.Generic;

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
    private static readonly List<IDamageBarrierSurface> _surfaces = new();

    public static void Register(IDamageBarrierSurface surface)
    {
      if (surface == null)
        return;
      if (_surfaces.Contains(surface))
        return;
      _surfaces.Add(surface);
    }

    public static void Unregister(IDamageBarrierSurface surface)
    {
      if (surface == null)
        return;
      _surfaces.Remove(surface);
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
      bool blocked = TryGetFirstBlockingHit(query, out _);
      DebugLog(blocked ? "blocked" : "not blocked");
      return blocked;
    }

    public static bool TryGetFirstBlockingHit(in DamageBarrierQuery query, out DamageBarrierHit hit)
    {
      hit = default;
      if (_surfaces.Count == 0)
        return false;

      float bestDistance = float.PositiveInfinity;
      bool found = false;

      for (int i = _surfaces.Count - 1; i >= 0; i--)
      {
        IDamageBarrierSurface surface = _surfaces[i];
        Node3D node = surface.BarrierNode;
        if (node != null && !GodotObject.IsInstanceValid(node))
        {
          _surfaces.RemoveAt(i);
          continue;
        }

        try
        {
          if (!surface.TryGetIntersection(query, out DamageBarrierHit candidate))
            continue;
          if (!surface.ShouldBlockDamage(query, candidate))
            continue;
          if (candidate.Distance >= bestDistance)
            continue;
          bestDistance = candidate.Distance;
          hit = candidate;
          found = true;
        }
        catch
        {
          DebugLog($"barrier {DescribeSurface(surface)} threw during evaluation");
        }
      }

      return found;
    }

    public static bool TryGetFirstIntersection(Vector3 origin, Vector3 target, float padding, out DamageBarrierHit hit)
    {
      var query = new DamageBarrierQuery(origin, target, padding, DamageKind.Other, null, null);
      return TryGetFirstBlockingHit(query, out hit);
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
