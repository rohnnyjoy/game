using Godot;
#nullable enable
using Godot.Collections;
using Shared.Runtime;
using System;

// Listens for global overkill events and applies Cursed Skull chaining to the nearest enemy.
public sealed partial class CursedSkullOverkillHandler : Node
{
  public static CursedSkullOverkillHandler? Instance { get; private set; }

  private const float DefaultKnockback = 3.5f;
  private const float BeamWidthScale = 1.5f;
  private static readonly Vector3 DefaultBeamVerticalOffset = Vector3.Up * 0.9f;

  public override void _EnterTree()
  {
    Instance = this;
  }

  public override void _ExitTree()
  {
    if (Instance == this)
      Instance = null;
  }

  public override void _Ready()
  {
    // Subscribe to global overkill events
    GlobalEvents.Instance?.Connect(nameof(GlobalEvents.OverkillOccurred), new Callable(this, nameof(OnOverkillOccurred)));
  }

  private void OnOverkillOccurred(Node3D victim, float overkillAmount)
  {
    try
    {
      if (victim == null || !IsInstanceValid(victim))
        return;
      if (overkillAmount <= 0.0f)
        return;

      float radius = GetCursedSkullRadius();
      if (radius < 0.0f) // no module active
        return;

      Node3D? neighbor = FindNearestAliveEnemy(victim, radius);
      if (neighbor == null)
        return;

      // VFX: beam between victim and neighbor
      try
      {
        float beamStrength = Mathf.Clamp(0.35f + overkillAmount * 0.05f, 0.35f, 2.5f);
        float baseBeamWidth = 0.45f + overkillAmount * 0.01f;
        float beamWidth = Mathf.Clamp(baseBeamWidth * BeamWidthScale, 0.45f * BeamWidthScale, 1.35f * BeamWidthScale);
        Vector3 originOffset = CalculateBeamVerticalOffset(victim);
        Vector3 targetOffset = CalculateBeamVerticalOffset(neighbor);
        BeamVfxManager.Spawn(victim, originOffset, neighbor, targetOffset, beamStrength, widthOverride: beamWidth);
      }
      catch (Exception fxEx)
      {
        GD.PrintErr($"CursedSkullOverkillHandler beam spawn failed: {fxEx.Message}");
      }

      // Apply damage to neighbor (chain)
      try
      {
        bool applied = false;
        if (neighbor is Enemy en)
        {
          if (en.CurrentHealth > 0.0f)
          {
            en.TakeDamage(overkillAmount);
            applied = true;
          }
        }
        else if (neighbor.HasMethod("take_damage"))
        {
          neighbor.CallDeferred("take_damage", overkillAmount);
          applied = true;
        }

        if (applied)
        {
          FloatingNumber3D.Spawn(this, neighbor, overkillAmount);
          Vector3 dir = (neighbor.GlobalTransform.Origin - victim.GlobalTransform.Origin);
          dir.Y = 0.15f * dir.Y;
          if (dir.LengthSquared() < 0.000001f)
            dir = Vector3.Forward;
          dir = dir.Normalized();
          var snap = new BulletManager.ImpactSnapshot(
            damage: overkillAmount,
            knockbackScale: 1.0f,
            enemyHit: true,
            enemyId: (ulong)neighbor.GetInstanceId(),
            hitPosition: neighbor.GlobalTransform.Origin,
            hitNormal: -dir,
            isCrit: false,
            critMultiplier: 1.0f
          );
          GlobalEvents.Instance?.EmitDamageDealt(neighbor, snap, dir, DefaultKnockback);
        }
      }
      catch (Exception applyEx)
      {
        GD.PrintErr($"CursedSkullOverkillHandler apply failed: {applyEx.Message}");
      }
    }
    catch (Exception e)
    {
      GD.PrintErr($"CursedSkullOverkillHandler OnOverkillOccurred error: {e.Message}");
    }
  }

  // Returns -1 if no active Cursed Skull module is present on the primary weapon.
  private static float GetCursedSkullRadius()
  {
    var store = InventoryStore.Instance;
    if (store == null)
      return -1.0f;
    Array<WeaponModule> modules = store.GetPrimaryWeaponModules();
    if (modules == null || modules.Count == 0)
      return -1.0f;
    float? radius = null;
    foreach (WeaponModule module in modules)
    {
      if (module is CursedSkullModule cursed)
      {
        radius = cursed.TransferRadius;
        break;
      }
    }
    return radius.HasValue ? radius.Value : -1.0f;
  }

  private static Node3D? FindNearestAliveEnemy(Node3D victim, float radius)
  {
    if (victim == null || !IsInstanceValid(victim))
      return null;
    Vector3 origin = victim.GlobalTransform.Origin;
    Node3D? nearest = null;
    float best = radius > 0.0f ? radius : float.PositiveInfinity;
    var tree = victim.GetTree();
    if (tree == null)
      return null;
    foreach (Node node in tree.GetNodesInGroup("enemies"))
    {
      if (node is not Node3D candidate || !IsInstanceValid(candidate))
        continue;
      if (candidate == victim)
        continue;

      // Prefer real Enemy instances that are still alive
      if (candidate is Enemy enemy)
      {
        if (enemy.CurrentHealth <= 0.0f)
          continue;
      }

      float d = candidate.GlobalTransform.Origin.DistanceTo(origin);
      if (d < best)
      {
        best = d;
        nearest = candidate;
      }
    }
    return nearest;
  }

  // Derive a world-space offset so beam endpoints target the visual midpoint of a node.
  private static Vector3 CalculateBeamVerticalOffset(Node3D? node)
  {
    if (node == null || !GodotObject.IsInstanceValid(node))
      return DefaultBeamVerticalOffset;

    if (!TryGetVerticalBounds(node, out float minY, out float maxY))
      return DefaultBeamVerticalOffset;

    float centerY = (minY + maxY) * 0.5f;
    float offsetY = centerY - node.GlobalTransform.Origin.Y;
    return Vector3.Up * offsetY;
  }

  private static bool TryGetVerticalBounds(Node3D node, out float minY, out float maxY)
  {
    minY = float.MaxValue;
    maxY = float.MinValue;
    bool found = false;

    AccumulateVisualExtents(node, ref minY, ref maxY, ref found);
    AccumulateCollisionExtents(node, ref minY, ref maxY, ref found);

    if (!found)
    {
      minY = maxY = node.GlobalTransform.Origin.Y;
      return false;
    }

    return true;
  }

  private static void AccumulateVisualExtents(Node node, ref float minY, ref float maxY, ref bool found)
  {
    if (!GodotObject.IsInstanceValid(node))
      return;

    if (node is VisualInstance3D visual)
    {
      var aabb = visual.GetAabb();
      ProcessAabb(visual.GlobalTransform, aabb, ref minY, ref maxY, ref found);
    }

    foreach (Node child in node.GetChildren())
      AccumulateVisualExtents(child, ref minY, ref maxY, ref found);
  }

  private static void AccumulateCollisionExtents(Node node, ref float minY, ref float maxY, ref bool found)
  {
    if (!GodotObject.IsInstanceValid(node))
      return;

    if (node is CollisionShape3D collisionShape)
    {
      try
      {
        var shape = collisionShape.Shape;
        Mesh? mesh = shape?.GetDebugMesh();
        if (mesh != null)
        {
          var aabb = mesh.GetAabb();
          ProcessAabb(collisionShape.GlobalTransform, aabb, ref minY, ref maxY, ref found);
        }
      }
      catch (Exception)
      {
        // Ignore debug mesh failures; rely on other nodes or fall back to default offset.
      }
    }

    foreach (Node child in node.GetChildren())
      AccumulateCollisionExtents(child, ref minY, ref maxY, ref found);
  }

  private static void ProcessAabb(Transform3D transform, Aabb aabb, ref float minY, ref float maxY, ref bool found)
  {
    Vector3 basePos = aabb.Position;
    Vector3 size = aabb.Size;

    for (int i = 0; i < 8; i++)
    {
      Vector3 corner = new Vector3(
        basePos.X + ((i & 1) != 0 ? size.X : 0f),
        basePos.Y + ((i & 2) != 0 ? size.Y : 0f),
        basePos.Z + ((i & 4) != 0 ? size.Z : 0f)
      );

      Vector3 worldCorner = transform * corner;
      float y = worldCorner.Y;

      if (!found)
      {
        minY = maxY = y;
        found = true;
      }
      else
      {
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }
  }
}
