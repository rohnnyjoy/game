using Godot;
using System;

public partial class FloatingNumber3D : Node3D
{
  // Spawns a floating number above a target using a viewport-backed billboard.
  public static void Spawn(Node context, Node3D target, float amount, Color? color = null, float yJitterMax = 1.2f)
  {
    if (context == null || !GodotObject.IsInstanceValid(context)) return;
    var tree = context.GetTree();
    if (tree?.CurrentScene == null || target == null || !GodotObject.IsInstanceValid(target)) return;

    var node = new DamageNumberBillboard();

    // Use global UI font for damage numbers
    var dmgFont = GD.Load<FontFile>("res://assets/fonts/Born2bSportyV2.ttf");
    var text = ((int)Mathf.Round(amount)).ToString();
    var fill = color ?? Colors.White;

    var rng = new RandomNumberGenerator();
    rng.Randomize();
    // Add to tree first, then set global position (safer wrt parenting transforms)
    tree.CurrentScene.AddChild(node);
    node.BaseScale = 1.6f;
    node.Configure(text, fill, Colors.Black, 56, dmgFont, 14);

    Vector3 anchor = target.GlobalTransform.Origin;
    float baseMargin = 0.12f;

    if (TryGetWorldAabb(target, out var worldAabb))
    {
      var center = worldAabb.GetCenter();
      float topY = worldAabb.Position.Y + worldAabb.Size.Y;
      anchor = new Vector3(center.X, topY, center.Z);
    }
    else
    {
      anchor += Vector3.Up * 1.0f;
    }

    var spawnPosition = anchor + Vector3.Up * baseMargin;

    Vector3 directionalAxis = Vector3.Zero;
    var camera = node.GetViewport()?.GetCamera3D();
    if (camera != null)
    {
      var basis = camera.GlobalTransform.Basis;
      Vector3 camUp = basis.Y.Normalized();
      Vector3 camRight = basis.X.Normalized();
      Vector3[] dirs = { camUp, -camUp, camRight, -camRight };
      directionalAxis = dirs[rng.RandiRange(0, dirs.Length - 1)];
    }
    else
    {
      Vector3[] dirs = { Vector3.Up, Vector3.Down, Vector3.Right, Vector3.Left };
      directionalAxis = dirs[rng.RandiRange(0, dirs.Length - 1)];
    }

    float offsetMagnitude = Mathf.Max(0.05f, yJitterMax);
    float directionalDistance = rng.RandfRange(0.05f, offsetMagnitude);
    spawnPosition += directionalAxis * directionalDistance;
    if (spawnPosition.Y < anchor.Y + 0.02f)
      spawnPosition.Y = anchor.Y + 0.02f;

    node.SetSpawnPosition(spawnPosition);
    node.TriggerPulse(0.2f);
  }

  private static bool TryGetWorldAabb(Node3D root, out Aabb worldAabb)
  {
    Aabb aggregate = default;
    bool found = false;

    void MergeCandidate(Aabb candidate)
    {
      if (!found)
      {
        aggregate = candidate;
        found = true;
      }
      else
      {
        aggregate = MergeAabb(aggregate, candidate);
      }
    }

    void Visit(Node node)
    {
      if (node is not Node3D node3D) return;

      if (node3D is MeshInstance3D mesh && mesh.Mesh != null)
      {
        var localAabb = mesh.Mesh.GetAabb();
        var globalAabb = TransformAabb(localAabb, mesh.GlobalTransform);
        MergeCandidate(globalAabb);
      }
      else if (node3D is CollisionShape3D collider && collider.Shape != null)
      {
        var debugMesh = collider.Shape.GetDebugMesh();
        if (debugMesh != null)
        {
          var localAabb = debugMesh.GetAabb();
          var globalAabb = TransformAabb(localAabb, collider.GlobalTransform);
          MergeCandidate(globalAabb);
        }
      }

      foreach (Node child in node3D.GetChildren())
      {
        Visit(child);
      }
    }

    Visit(root);
    worldAabb = aggregate;
    return found;
  }

  private static Aabb TransformAabb(Aabb local, Transform3D transform)
  {
    Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    for (int i = 0; i < 8; i++)
    {
      Vector3 corner = new(
        local.Position.X + (((i & 1) == 0) ? 0f : local.Size.X),
        local.Position.Y + (((i & 2) == 0) ? 0f : local.Size.Y),
        local.Position.Z + (((i & 4) == 0) ? 0f : local.Size.Z)
      );
      Vector3 world = transform * corner;
      min.X = Mathf.Min(min.X, world.X);
      min.Y = Mathf.Min(min.Y, world.Y);
      min.Z = Mathf.Min(min.Z, world.Z);
      max.X = Mathf.Max(max.X, world.X);
      max.Y = Mathf.Max(max.Y, world.Y);
      max.Z = Mathf.Max(max.Z, world.Z);
    }

    return new Aabb(min, max - min);
  }

  private static Aabb MergeAabb(Aabb a, Aabb b)
  {
    Vector3 aMax = a.Position + a.Size;
    Vector3 bMax = b.Position + b.Size;
    Vector3 min = new(
      Mathf.Min(a.Position.X, b.Position.X),
      Mathf.Min(a.Position.Y, b.Position.Y),
      Mathf.Min(a.Position.Z, b.Position.Z)
    );
    Vector3 max = new(
      Mathf.Max(aMax.X, bMax.X),
      Mathf.Max(aMax.Y, bMax.Y),
      Mathf.Max(aMax.Z, bMax.Z)
    );
    return new Aabb(min, max - min);
  }
}
