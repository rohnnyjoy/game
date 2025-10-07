using Godot;
using System;
using System.Collections.Generic;
using AI;

#nullable enable

public partial class EnemyAIManager : Node
{
  public static EnemyAIManager? Instance { get; private set; }

  // Budgeting and LOD
  [Export(PropertyHint.Range, "0,1000,1")] public int MaxAiUpdatesPerFrame { get; set; } = 0; // 0 = all
  [Export(PropertyHint.Range, "0,500,1")] public float MidRangeDistance { get; set; } = 40f;
  [Export(PropertyHint.Range, "0,1000,1")] public float FarRangeDistance { get; set; } = 100f;
  [Export] public int MidRangeIntervalFrames { get; set; } = 2;
  [Export] public int FarRangeIntervalFrames { get; set; } = 5;
  [Export] public bool EnableLod { get; set; } = true;
  [Export(PropertyHint.Range, "0,500,1")] public float MaxConsiderDistance { get; set; } = 200f;

  private readonly List<Enemy> _enemies = new();
  private int _cursor = 0;
  private ulong _frameIndex = 0;

  public override void _Ready()
  {
    if (Instance != null && Instance != this)
    {
      QueueFree();
      return;
    }
    Instance = this;
    Name = nameof(EnemyAIManager);
    AddToGroup("ai_manager");
    // Run earlier than most gameplay nodes to assign targets before actors step.
    ProcessPriority = -10;

    // Opportunistically register any enemies already in the scene.
    foreach (var n in GetTree().GetNodesInGroup("enemies"))
    {
      if (n is Enemy e)
        Register(e);
    }
  }

  // Autoloaded; no manual EnsurePresent needed.

  public void Register(Enemy enemy)
  {
    if (enemy == null || !IsInstanceValid(enemy)) return;
    if (_enemies.Contains(enemy)) return;
    _enemies.Add(enemy);
  }

  public void Unregister(Enemy enemy)
  {
    if (enemy == null) return;
    _enemies.Remove(enemy);
    if (IsInstanceValid(enemy))
      enemy.TargetOverride = null;
  }

  public override void _PhysicsProcess(double delta)
  {
    base._PhysicsProcess(delta);

    // Clean up invalid references occasionally
    if (_frameIndex % 120 == 0)
      _enemies.RemoveAll(e => e == null || !IsInstanceValid(e));

    // Early out if nothing to do
    int total = _enemies.Count;
    if (total == 0)
    {
      _frameIndex++;
      return;
    }

    // Gather players once
    var players = GatherPlayers();

    var (start, count) = AIScheduler.ComputeSlice(total, MaxAiUpdatesPerFrame, _cursor);
    for (int i = 0; i < count; i++)
    {
      int idx = (start + i) % total;
      var enemy = _enemies[idx];
      if (enemy == null || !IsInstanceValid(enemy))
        continue;

      // Distance LOD gating
      if (EnableLod)
      {
        float dist = players.Count > 0 ? DistanceToNearest(players, enemy.GlobalTransform.Origin) : float.PositiveInfinity;
        int interval = 1;
        if (dist > FarRangeDistance) interval = Math.Max(1, FarRangeIntervalFrames);
        else if (dist > MidRangeDistance) interval = Math.Max(1, MidRangeIntervalFrames);
        if (interval > 1 && (_frameIndex % (ulong)interval) != 0)
          continue; // skip this frame
      }

      // Choose nearest player as target within max consider distance
      Node3D? target = null;
      if (players.Count > 0)
      {
        var best = NearestPlayer(players, enemy.GlobalTransform.Origin, out float d2);
        if (Mathf.Sqrt(d2) <= MaxConsiderDistance)
          target = best;
      }

      if (IsInstanceValid(enemy))
        enemy.TargetOverride = target;
    }

    _cursor = AIScheduler.AdvanceCursor(total, MaxAiUpdatesPerFrame, _cursor);
    _frameIndex++;
  }

  private static List<Node3D> GatherPlayers()
  {
    var result = new List<Node3D>(2);
    if (Player.Instance != null && IsInstanceValid(Player.Instance))
      result.Add(Player.Instance);
    else
    {
      var scenePlayers = SceneTreeSingleton()?.GetNodesInGroup("players");
      if (scenePlayers != null)
      {
        foreach (var n in scenePlayers)
          if (n is Node3D n3 && IsInstanceValid(n3))
            result.Add(n3);
      }
    }
    return result;
  }

  private static SceneTree? SceneTreeSingleton()
  {
    // Try to access scene tree via any existing Instance
    return Instance?.GetTree();
  }

  private static Node3D? NearestPlayer(List<Node3D> players, Vector3 pos, out float bestDist2)
  {
    Node3D? best = null;
    bestDist2 = float.PositiveInfinity;
    foreach (var p in players)
    {
      var d2 = pos.DistanceSquaredTo(p.GlobalTransform.Origin);
      if (d2 < bestDist2)
      {
        bestDist2 = d2;
        best = p;
      }
    }
    return best;
  }

  private static float DistanceToNearest(List<Node3D> players, Vector3 pos)
  {
    if (players.Count == 0) return float.PositiveInfinity;
    float best = float.PositiveInfinity;
    foreach (var p in players)
    {
      float d = pos.DistanceTo(p.GlobalTransform.Origin);
      if (d < best) best = d;
    }
    return best;
  }
}
