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
  private readonly HashSet<Enemy> _activeSimEnemies = new();
  private static readonly List<Node3D> PlayerCache = new(2);
  private static readonly Enemy[] EmptyEnemies = Array.Empty<Enemy>();
  private const float WakeHysteresis = 6.0f;

  public static IReadOnlyList<Node3D> ActivePlayers => PlayerCache;
  public static IReadOnlyCollection<Enemy> ActiveSimulationEnemies
    => Instance != null ? (IReadOnlyCollection<Enemy>)Instance._activeSimEnemies : EmptyEnemies;

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

  public override void _ExitTree()
  {
    if (Instance == this)
    {
      Instance = null;
      PlayerCache.Clear();
      _activeSimEnemies.Clear();
    }
    base._ExitTree();
  }

  // Autoloaded; no manual EnsurePresent needed.

  public void Register(Enemy enemy)
  {
    if (enemy == null || !IsInstanceValid(enemy)) return;
    if (_enemies.Contains(enemy)) return;
    _enemies.Add(enemy);
    if (enemy.CurrentSimulationState != Enemy.SimulationState.Sleeping)
      _activeSimEnemies.Add(enemy);
  }

  public void Unregister(Enemy enemy)
  {
    if (enemy == null) return;
    _enemies.Remove(enemy);
    _activeSimEnemies.Remove(enemy);
    if (IsInstanceValid(enemy))
      enemy.TargetOverride = null;
  }

  public override void _PhysicsProcess(double delta)
  {
    base._PhysicsProcess(delta);

    // Clean up invalid references occasionally
    if (_frameIndex % 120 == 0)
    {
      _enemies.RemoveAll(e => e == null || !IsInstanceValid(e));
      _activeSimEnemies.RemoveWhere(e => e == null || !IsInstanceValid(e));
    }

    // Early out if nothing to do
    int total = _enemies.Count;
    if (total == 0)
    {
      _frameIndex++;
      return;
    }

    // Gather players once
    RefreshPlayerCache();
    var players = PlayerCache;

    var (start, count) = AIScheduler.ComputeSlice(total, MaxAiUpdatesPerFrame, _cursor);
    for (int i = 0; i < count; i++)
    {
      int idx = (start + i) % total;
      var enemy = _enemies[idx];
      if (enemy == null || !IsInstanceValid(enemy))
        continue;

      Vector3 origin = enemy.GlobalTransform.Origin;
      Node3D? nearest = null;
      float nearestDist2 = float.PositiveInfinity;

      if (players.Count > 0)
        nearest = NearestPlayer(players, origin, out nearestDist2);

      float nearestDist = float.IsPositiveInfinity(nearestDist2) ? float.PositiveInfinity : Mathf.Sqrt(nearestDist2);

      var desiredState = DetermineSimulationState(enemy, nearestDist);
      Enemy.SimulationState previousState = enemy.CurrentSimulationState;
      if (previousState != desiredState)
        enemy.SetSimulationState(desiredState);

      if (enemy.CurrentSimulationState == Enemy.SimulationState.Sleeping)
      {
        _activeSimEnemies.Remove(enemy);
        continue;
      }

      _activeSimEnemies.Add(enemy);

      bool shouldAssignTarget = previousState != enemy.CurrentSimulationState;
      if (!shouldAssignTarget && EnableLod)
      {
        int interval = 1;
        if (nearestDist > FarRangeDistance) interval = Math.Max(1, FarRangeIntervalFrames);
        else if (nearestDist > MidRangeDistance) interval = Math.Max(1, MidRangeIntervalFrames);
        if (interval > 1 && (_frameIndex % (ulong)interval) != 0)
          continue;
      }

      Node3D? target = null;
      if (nearest != null && nearestDist <= MaxConsiderDistance)
        target = nearest;

      enemy.TargetOverride = target;
    }

    _cursor = AIScheduler.AdvanceCursor(total, MaxAiUpdatesPerFrame, _cursor);
    _frameIndex++;
  }

  private static void RefreshPlayerCache()
  {
    PlayerCache.Clear();

    if (Player.Instance != null && IsInstanceValid(Player.Instance))
      PlayerCache.Add(Player.Instance);

    var scenePlayers = SceneTreeSingleton()?.GetNodesInGroup("players");
    if (scenePlayers == null)
      return;

    foreach (var n in scenePlayers)
    {
      if (n is not Node3D n3 || !IsInstanceValid(n3))
        continue;
      if (Player.Instance != null && IsInstanceValid(Player.Instance) && n3 == Player.Instance)
        continue;
      PlayerCache.Add(n3);
    }
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

  private static Enemy.SimulationState DetermineSimulationState(Enemy enemy, float distance)
  {
    if (float.IsPositiveInfinity(distance))
      return Enemy.SimulationState.Sleeping;

    float sleepRadius = enemy.SleepSimulationRadius;
    float farRadius = enemy.FarSimulationRadius;

    if (enemy.CurrentSimulationState == Enemy.SimulationState.Sleeping)
    {
      float wakeRadius = MathF.Max(farRadius, sleepRadius - WakeHysteresis);
      if (distance > wakeRadius)
        return Enemy.SimulationState.Sleeping;
    }

    if (distance > sleepRadius)
      return Enemy.SimulationState.Sleeping;
    if (distance <= enemy.ActiveSimulationRadius)
      return Enemy.SimulationState.Active;
    if (distance <= enemy.MidSimulationRadius)
      return Enemy.SimulationState.BudgetMid;
    if (distance <= farRadius)
      return Enemy.SimulationState.BudgetFar;
    return Enemy.SimulationState.Sleeping;
  }
}
