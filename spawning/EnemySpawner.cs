using Godot;
using System;
#nullable enable

public partial class EnemySpawner : Node3D
{
  [Export] public PackedScene? EnemyScene { get; set; }

  // Difficulty scales spawn frequency: effective SPM = BaseSpawnsPerMinute * Difficulty
  [Export(PropertyHint.Range, "0.1,10.0,0.1")] public float Difficulty { get; set; } = 1.0f;
  [Export(PropertyHint.Range, "0.1,120.0,0.1")] public float BaseSpawnsPerMinute { get; set; } = 12.0f;

  [Export(PropertyHint.Range, "1.0,100.0,0.5")] public float MinSpawnRadius { get; set; } = 12.0f;
  [Export(PropertyHint.Range, "1.0,200.0,0.5")] public float MaxSpawnRadius { get; set; } = 32.0f;
  [Export(PropertyHint.Range, "0,500")] public int MaxAliveEnemies { get; set; } = 30;
  [Export(PropertyHint.Range, "0,25")] public int BurstCount { get; set; } = 3; // spawn multiple per tick if desired

  private Godot.Timer _timer = default!;
  private RandomNumberGenerator _rng = new RandomNumberGenerator();

  public override void _Ready()
  {
    _rng.Randomize();

    _timer = new Godot.Timer
    {
      OneShot = false,
      Autostart = true
    };
    AddChild(_timer);
    _timer.Timeout += OnTimerTimeout;
    RecalculateTimer();
    // Ensure timer is running with the latest wait time
    _timer.Start();
    // Trigger an initial deferred tick so Player.Instance is available
    CallDeferred(nameof(OnTimerTimeout));
  }

  public override void _ExitTree()
  {
    if (_timer != null)
      _timer.Timeout -= OnTimerTimeout;
  }

  private void RecalculateTimer()
  {
    float spm = Math.Max(0.01f, BaseSpawnsPerMinute * Math.Max(0.01f, Difficulty));
    float period = 60.0f / spm;
    if (_timer != null)
    {
      _timer.WaitTime = period;
      _timer.Start();
    }
  }

  private void OnTimerTimeout()
  {
    if (EnemyScene == null)
      return;

    // Respect alive cap
    var alive = GetTree().GetNodesInGroup("enemies").Count;
    if (alive >= MaxAliveEnemies)
      return;

    var player = FindPlayer();
    if (player == null)
      return;

    int toSpawn = Math.Max(1, BurstCount);
    for (int i = 0; i < toSpawn && alive < MaxAliveEnemies; i++)
    {
      if (TryGetSpawnPositionNear(player, out var spawnPos))
      {
        var enemy = EnemyScene.Instantiate<Node3D>();
        enemy.GlobalTransform = new Transform3D(Basis.Identity, spawnPos);
        // Spawn as sibling under the spawner's parent (scene root is fine)
        // Use deferred add to avoid "parent is busy setting up children" during scene build.
        var parent = GetParent() ?? this;
        parent.CallDeferred(Node.MethodName.AddChild, enemy);
        alive++;
      }
    }
  }

  private Node3D? FindPlayer()
  {
    // Prefer Player.Instance if available
    if (Player.Instance != null)
      return Player.Instance;

    foreach (var n in GetTree().GetNodesInGroup("players"))
    {
      if (n is Node3D n3) return n3;
    }
    return null;
  }

  private bool TryGetSpawnPositionNear(Node3D player, out Vector3 position)
  {
    position = player.GlobalTransform.Origin;
    if (MinSpawnRadius >= MaxSpawnRadius)
      MaxSpawnRadius = MinSpawnRadius + 1.0f;

    // Pick a random angle around the player and a radius in [min,max]
    float angle = _rng.RandfRange(0, Mathf.Tau);
    float radius = _rng.RandfRange(MinSpawnRadius, MaxSpawnRadius);
    Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
    Vector3 sample = player.GlobalTransform.Origin + offset;

    // Try to raycast down from above to find the floor
    var space = GetWorld3D().DirectSpaceState;
    Vector3 from = sample + new Vector3(0, 50, 0);
    Vector3 to = sample + new Vector3(0, -50, 0);
    var query = PhysicsRayQueryParameters3D.Create(from, to);
    // Exclude the player so we don't hit their body
    query.Exclude = new Godot.Collections.Array<Rid>();
    if (player is PhysicsBody3D pb)
    {
      query.Exclude.Add(pb.GetRid());
    }

    var hit = space.IntersectRay(query);
    if (hit != null && hit.Count > 0 && hit.ContainsKey("position"))
    {
      Vector3 hitPos = (Vector3)hit["position"];
      position = hitPos + new Vector3(0, 0.5f, 0);
      return true;
    }

    // Fallback: spawn at player's height at the sampled XZ
    position = new Vector3(sample.X, player.GlobalTransform.Origin.Y, sample.Z);
    return true;
  }

  // Allow live tweaking via editor or setters
  public void SetDifficulty(float value)
  {
    Difficulty = MathF.Max(0.01f, value);
    RecalculateTimer();
  }
}
