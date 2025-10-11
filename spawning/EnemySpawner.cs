using Godot;
using System;
using Shared.Runtime;
#nullable enable

public partial class EnemySpawner : Node3D
{
  [Signal]
  public delegate void DifficultyChangedEventHandler(float value);

  [Export] public PackedScene? EnemyScene { get; set; }

  // Difficulty scales spawn frequency: effective SPM = BaseSpawnsPerMinute * Difficulty
  [Export(PropertyHint.Range, "0.1,10.0,0.1")] public float Difficulty
  {
    get => _difficulty;
    set
    {
      float clamped = MathF.Max(0.01f, value);
      if (Mathf.IsEqualApprox(clamped, _difficulty))
        return;
      _difficulty = clamped;
      RecalculateTimer();
      EmitSignal(SignalName.DifficultyChanged, _difficulty);
    }
  }

  private float _difficulty = 1.0f;
  [Export(PropertyHint.Range, "0.1,120.0,0.1")] public float BaseSpawnsPerMinute { get; set; } = 12.0f;

  [Export(PropertyHint.Range, "1.0,100.0,0.5")] public float MinSpawnRadius { get; set; } = 12.0f;
  [Export(PropertyHint.Range, "1.0,200.0,0.5")] public float MaxSpawnRadius { get; set; } = 32.0f;
  [Export(PropertyHint.Range, "0.0,50.0,0.1")] public float MinSpawnSeparation { get; set; } = EnemySpawnUtility.DefaultMinSpawnSeparation;
  [Export(PropertyHint.Range, "0,500")] public int MaxAliveEnemies { get; set; } = 30;
  [Export(PropertyHint.Range, "0,25")] public int BurstCount { get; set; } = 3; // spawn multiple per tick if desired

  [Export] public bool AutoRampDifficulty { get; set; } = true;
  [Export(PropertyHint.Range, "0.01,5.0,0.01")] public float DifficultyRampIncrement { get; set; } = 0.1f;
  [Export(PropertyHint.Range, "0.5,120.0,0.5")] public float DifficultyRampIntervalSeconds { get; set; } = 10.0f;
  [Export(PropertyHint.Range, "0,20,0.1")] public float DifficultyRampMax { get; set; } = 4.0f; // 0 = no cap

  private Godot.Timer _timer = default!;
  private Godot.Timer? _difficultyTimer;
  private RandomNumberGenerator _rng = new RandomNumberGenerator();

  public override void _Ready()
  {
    _rng.Randomize();
    AddToGroup("enemy_spawners");

    _timer = new Godot.Timer
    {
      OneShot = false,
      Autostart = true
    };
    AddChild(_timer);
    _timer.Timeout += OnTimerTimeout;
    RecalculateTimer();
    EmitSignal(SignalName.DifficultyChanged, _difficulty);
    // Ensure timer is running with the latest wait time
    _timer.Start();
    // Trigger an initial deferred tick so Player.Instance is available
    CallDeferred(nameof(OnTimerTimeout));

    SetupDifficultyTimer();
  }

  public override void _ExitTree()
  {
    if (_timer != null)
      _timer.Timeout -= OnTimerTimeout;
    if (_difficultyTimer != null)
      _difficultyTimer.Timeout -= OnDifficultyTimerTimeout;
  }

  private void RecalculateTimer()
  {
    float spm = Math.Max(0.01f, BaseSpawnsPerMinute * Math.Max(0.01f, _difficulty));
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
    float spawnHeight = EnemySpawnUtility.GetSpawnHeightOffset(EnemyScene);
    var parent = GetParent() ?? this;
    var space = GetWorld3D()?.DirectSpaceState;

    var exclude = new Godot.Collections.Array<Rid>();
    if (player is PhysicsBody3D playerBody)
      exclude.Add(playerBody.GetRid());

    // Build an avoidance list: start with current active enemy positions, and append newly accepted positions as we spawn.
    var avoidPositions = new System.Collections.Generic.List<Vector3>(Math.Min(MaxAliveEnemies, 256));
    EnemySpawnUtility.FillActiveEnemyPositions(avoidPositions);

    for (int i = 0; i < toSpawn && alive < MaxAliveEnemies; i++)
    {
      var instance = EnemyScene.Instantiate<Node3D>();
      if (instance == null)
        continue;

      if (!EnemySpawnUtility.TrySampleSeparatedPosition(
        center: player.GlobalTransform.Origin,
        minRadius: MinSpawnRadius,
        maxRadius: MaxSpawnRadius,
        minSeparation: MathF.Max(0f, MinSpawnSeparation),
        rng: _rng,
        existing: avoidPositions,
        out Vector3 sample))
      {
        // Fallback to a simple sample if separation failed repeatedly
        sample = EnemySpawnUtility.SamplePlanarPosition(player.GlobalTransform.Origin, MinSpawnRadius, MaxSpawnRadius, _rng);
      }

      uint mask = instance is PhysicsBody3D body ? body.CollisionMask : uint.MaxValue;
      Vector3 grounded = EnemySpawnUtility.ResolveGroundedPosition(sample, spawnHeight, space, mask, exclude);

      parent.AddChild(instance);
      instance.GlobalTransform = new Transform3D(Basis.Identity, grounded);

      // Track accepted planar positions so subsequent spawns this tick keep spacing.
      avoidPositions.Add(sample);

      if (instance is Enemy enemy)
        enemy.CallDeferred(nameof(Enemy.EnsureSimulationSync));

      alive++;
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

  // Allow live tweaking via editor or setters
  public void SetDifficulty(float value)
  {
    Difficulty = value;
  }

  private void SetupDifficultyTimer()
  {
    bool shouldRun = AutoRampDifficulty && DifficultyRampIncrement > 0.0f && DifficultyRampIntervalSeconds > 0.0f;
    if (!shouldRun)
    {
      if (_difficultyTimer != null)
      {
        _difficultyTimer.Stop();
        _difficultyTimer.Timeout -= OnDifficultyTimerTimeout;
        _difficultyTimer.QueueFree();
        _difficultyTimer = null;
      }
      return;
    }

    if (_difficultyTimer == null)
    {
      _difficultyTimer = new Godot.Timer
      {
        OneShot = false,
        Autostart = true,
        WaitTime = MathF.Max(0.01f, DifficultyRampIntervalSeconds)
      };
      AddChild(_difficultyTimer);
      _difficultyTimer.Timeout += OnDifficultyTimerTimeout;
    }
    else
    {
      _difficultyTimer.WaitTime = MathF.Max(0.01f, DifficultyRampIntervalSeconds);
      _difficultyTimer.Start();
    }
  }

  private void OnDifficultyTimerTimeout()
  {
    float increment = MathF.Max(0.0f, DifficultyRampIncrement);
    if (increment <= 0.0f)
      return;

    float next = _difficulty + increment;
    if (DifficultyRampMax > 0.0f)
      next = MathF.Min(next, DifficultyRampMax);
    Difficulty = next;

    if (DifficultyRampMax > 0.0f && Mathf.IsEqualApprox(Difficulty, DifficultyRampMax) && _difficultyTimer != null)
    {
      _difficultyTimer.Stop();
    }
  }
}
