#nullable enable

using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shared.Runtime
{
  public static class EnemySpawnUtility
  {
    private const float DefaultSpawnHeight = 1.0f;
    private const float RaycastHeight = 50.0f;
    public const float DefaultMinSpawnSeparation = 3.0f; // meters

    private static readonly Dictionary<string, float> SpawnHeightCache = new(StringComparer.Ordinal);

    public static float GetSpawnHeightOffset(PackedScene? enemyScene)
    {
      if (enemyScene == null)
        return DefaultSpawnHeight;

      string key = GetCacheKey(enemyScene);
      if (SpawnHeightCache.TryGetValue(key, out float cached))
        return cached;

      float computed = ComputeSpawnHeight(enemyScene);
      SpawnHeightCache[key] = computed;
      return computed;
    }

    public static Vector3 SamplePlanarPosition(Vector3 center, float minRadius, float maxRadius, RandomNumberGenerator rng)
    {
      if (maxRadius < minRadius)
        (minRadius, maxRadius) = (maxRadius, minRadius);

      minRadius = MathF.Max(0f, minRadius);
      maxRadius = MathF.Max(minRadius, maxRadius);

      float radius = minRadius;
      if (maxRadius > minRadius)
      {
        float minSq = minRadius * minRadius;
        float maxSq = maxRadius * maxRadius;
        float t = rng.Randf();
        radius = MathF.Sqrt(Mathf.Lerp(minSq, maxSq, t));
      }

      float angle = rng.RandfRange(0f, Mathf.Tau);
      Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
      return center + direction * radius;
    }

    public static Vector3 ResolveGroundedPosition(Vector3 basePosition, float spawnHeight, PhysicsDirectSpaceState3D? space, uint collisionMask, Godot.Collections.Array<Rid>? exclude = null)
    {
      if (space == null)
        return basePosition + Vector3.Up * spawnHeight;

      var query = PhysicsRayQueryParameters3D.Create(basePosition + Vector3.Up * RaycastHeight, basePosition + Vector3.Down * RaycastHeight);
      query.HitBackFaces = true;
      query.HitFromInside = true;
      query.CollideWithAreas = false;
      query.CollideWithBodies = true;
      query.CollisionMask = collisionMask == 0 ? uint.MaxValue : collisionMask;
      if (exclude != null && exclude.Count > 0)
        query.Exclude = exclude;

      var hit = space.IntersectRay(query);
      if (hit.Count > 0 && hit.ContainsKey("position"))
      {
        Vector3 ground = (Vector3)hit["position"];
        return ground + Vector3.Up * spawnHeight;
      }

      return basePosition + Vector3.Up * spawnHeight;
    }

    /// <summary>
    /// Try to sample a planar position around the given center that is at least <paramref name="minSeparation"/>
    /// away (XZ/planar) from all positions in <paramref name="existing"/>. Returns true on success.
    /// The returned position is not grounded; call ResolveGroundedPosition afterwards.
    /// </summary>
    public static bool TrySampleSeparatedPosition(
      Vector3 center,
      float minRadius,
      float maxRadius,
      float minSeparation,
      RandomNumberGenerator rng,
      System.Collections.Generic.IList<Vector3> existing,
      out Vector3 position,
      int maxAttempts = 32)
    {
      position = center;
      float sepSq = MathF.Max(0f, minSeparation) * MathF.Max(0f, minSeparation);
      for (int attempt = 0; attempt < Math.Max(1, maxAttempts); attempt++)
      {
        Vector3 sample = SamplePlanarPosition(center, minRadius, maxRadius, rng);
        bool ok = true;
        for (int i = 0; i < existing.Count; i++)
        {
          if (PlanarDistanceSquared(sample, existing[i]) < sepSq)
          {
            ok = false;
            break;
          }
        }
        if (ok)
        {
          position = sample;
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Collect current active enemy positions into the provided list.
    /// </summary>
    public static void FillActiveEnemyPositions(System.Collections.Generic.IList<Vector3> into)
    {
      if (into == null)
        return;
      foreach (var e in Enemy.ActiveEnemies)
      {
        if (e != null && e.IsInsideTree() && e.IsNodeReady())
        {
          into.Add(e.GlobalTransform.Origin);
        }
      }
    }

    private static float PlanarDistanceSquared(in Vector3 a, in Vector3 b)
    {
      float dx = a.X - b.X;
      float dz = a.Z - b.Z;
      return dx * dx + dz * dz;
    }

    public static void SyncEnemyWithSimulation(Enemy enemy)
    {
      EnemyAIManager.Instance?.SyncEnemyTransform(enemy);
    }

    private static string GetCacheKey(PackedScene scene)
    {
      if (!string.IsNullOrEmpty(scene.ResourcePath))
        return scene.ResourcePath;
      return scene.GetInstanceId().ToString(CultureInfo.InvariantCulture);
    }

    private static float ComputeSpawnHeight(PackedScene scene)
    {
      try
      {
        var instance = scene.Instantiate();
        if (instance is not Node node)
          return DefaultSpawnHeight;

        float offset = ExtractHeightFromNode(node);

        if (node.IsInsideTree())
          node.QueueFree();
        else
          node.Free();

        return MathF.Max(0.25f, offset);
      }
      catch
      {
        return DefaultSpawnHeight;
      }
    }

    private static float ExtractHeightFromNode(Node node)
    {
      if (node is Enemy enemy)
      {
        var shape = enemy.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.Shape;
        float height = ExtractHeightFromShape(shape);
        if (!Mathf.IsZeroApprox(height - DefaultSpawnHeight))
          return height;
      }

      if (node is CollisionShape3D collision && collision.Shape != null)
        return ExtractHeightFromShape(collision.Shape);

      foreach (Node child in node.GetChildren())
      {
        if (child is CollisionShape3D shapeNode && shapeNode.Shape != null)
          return ExtractHeightFromShape(shapeNode.Shape);
      }

      return DefaultSpawnHeight;
    }

    private static float ExtractHeightFromShape(Shape3D? shape)
    {
      if (shape == null)
        return DefaultSpawnHeight;

      return shape switch
      {
        CapsuleShape3D capsule => capsule.Height * 0.5f + capsule.Radius,
        SphereShape3D sphere => sphere.Radius,
        BoxShape3D box => MathF.Max(0.25f, box.Size.Y * 0.5f),
        CylinderShape3D cylinder => cylinder.Height * 0.5f,
        _ => DefaultSpawnHeight,
      };
    }
  }
}
