using Godot;
using System;
using System.Collections.Generic;

#nullable enable

[GlobalClass]
public partial class ProceduralTopologyGenerator : Node3D
{
  [Export(PropertyHint.Range, "32,256,1")]
  public int GridWidth { get; set; } = 96;

  [Export(PropertyHint.Range, "32,256,1")]
  public int GridDepth { get; set; } = 80;

  [Export(PropertyHint.Range, "1.0,16.0,0.5")]
  public float CellSize { get; set; } = 3.0f;

  [Export(PropertyHint.Range, "0.05,0.95,0.01")]
  public float InitialWallChance { get; set; } = 0.45f;

  [Export(PropertyHint.Range, "1,12,1")]
  public int SmoothingPasses { get; set; } = 5;

  [Export(PropertyHint.Range, "0.0,12.0,0.1")]
  public float HeightScale { get; set; } = 3.0f; // vertical distance between height tiers

  [Export(PropertyHint.Range, "0.5,30.0,0.5")]
  public float WallHeight { get; set; } = 8.0f;

  [Export(PropertyHint.Range, "0,32,1")]
  public int SpawnSafeRadius { get; set; } = 6;

  [Export]
  public bool AutoGenerateOnReady { get; set; } = true;

  [Export]
  public bool UseRandomSeed { get; set; } = true;

  [Export]
  public int Seed { get; set; } = 1337;

  [Export(PropertyHint.Range, "0.0,1.0,0.01")]
  public float PropChance { get; set; } = 0.02f;

  [Export]
  public PackedScene? PropScene { get; set; } = LoadOptional<PackedScene>("res://environment/RubbleSpawner.tscn");

  [Export]
  public Texture2D? FloorTexture { get; set; } = LoadOptional<Texture2D>("res://assets/textures/grass.png");

  [Export]
  public Texture2D? WallTexture { get; set; } = LoadOptional<Texture2D>("res://assets/textures/grid.png");

  [Export(PropertyHint.Range, "0.01,1.0,0.01")]
  public float TextureScale { get; set; } = 0.08f;

  [Export(PropertyHint.Range, "1,32,1")]
  public int FeatureCount { get; set; } = 8;

  [Export(PropertyHint.Range, "1.0,18.0,0.5")]
  public float FeatureRadiusMin { get; set; } = 4.0f;

  [Export(PropertyHint.Range, "1.0,24.0,0.5")]
  public float FeatureRadiusMax { get; set; } = 9.0f;

  [Export(PropertyHint.Range, "0,6,1")]
  public int CorridorRadius { get; set; } = 1;

  [Export(PropertyHint.Range, "0.2,0.8,0.02")]
  public float TargetOpenFraction { get; set; } = 0.48f;

  [Export(PropertyHint.Range, "2,8,1")]
  public int HeightTierCount { get; set; } = 5;

  [Export(PropertyHint.Range, "0.0,1.0,0.05")]
  public float HeightTierBlend { get; set; } = 0.45f;

  [Export(PropertyHint.Range, "0.0,1.0,0.05")]
  public float HeightNoiseJitter { get; set; } = 0.4f;

  [Export(PropertyHint.Range, "1.0,40.0,0.5")]
  public float CliffHeight { get; set; } = 10.0f;

  [Export(PropertyHint.Range, "0.005,0.1,0.005")]
  public float MacroNoiseFrequency { get; set; } = 0.02f;

  [Export(PropertyHint.Range, "0.0,1.0,0.05")]
  public float MacroNoiseStrength { get; set; } = 0.7f;

  [Export(PropertyHint.Range, "0.0,1.0,0.05")]
  public float MicroHeightStrength { get; set; } = 0.35f;

  [Export(PropertyHint.Range, "0.0,20.0,0.5")]
  public float FeatureHeightBoost { get; set; } = 6.0f;

  [Export(PropertyHint.Range, "-1.0,1.0,0.05")]
  public float FeatureHeightBias { get; set; } = 0.25f;

  [Export(PropertyHint.Range, "0.1,6.0,0.1")]
  public float FeatureHeightExponent { get; set; } = 2.5f;

  [Export(PropertyHint.Range, "0.0,80.0,0.5")]
  public float MacroHeightMultiplier { get; set; } = 12.0f;

  [Export(PropertyHint.Range, "0.0,30.0,0.5")]
  public float MicroHeightMultiplier { get; set; } = 3.5f;

  [Export(PropertyHint.Range, "0.1,8.0,0.1")]
  public float MaxSlopeDelta { get; set; } = 3.0f;

  [Export(PropertyHint.Range, "1,12,1")]
  public int HeightSmoothingPasses { get; set; } = 4;

  [Export(PropertyHint.Range, "0.0,1.0,0.1")]
  public float HeightSmoothingStrength { get; set; } = 0.35f;

  [Export(PropertyHint.Range, "0.0,1.0,0.1")]
  public float EdgeHeightFalloff { get; set; } = 0.25f;

  public IReadOnlyList<Vector3> FloorCenters => _floorCenters;

  private readonly List<Vector3> _floorCenters = new();
  private readonly List<Vector2I> _lastFeatureCenters = new();
  public IReadOnlyList<Vector2I> LastFeatureCenters => _lastFeatureCenters;
  private readonly List<FeatureSeed> _lastFeatureSeeds = new();
  public float[,]? LastCellHeights { get; private set; }
  private MeshInstance3D? _terrainMesh;
  private StaticBody3D? _collisionBody;
  private CollisionShape3D? _collisionShape;
  private NavigationRegion3D? _navigationRegion;
  private Node3D? _propContainer;
  private ArrayMesh? _lastMesh;
  private readonly FastNoiseLite _heightNoise = new();

  private readonly struct FeatureSeed
  {
    public FeatureSeed(Vector2I cell, float radius, bool isPeak)
    {
      Cell = cell;
      Radius = radius;
      IsPeak = isPeak;
    }

    public Vector2I Cell { get; }
    public float Radius { get; }
    public bool IsPeak { get; }
  }

  public override void _Ready()
  {
    if (Engine.IsEditorHint())
      return;

    if (AutoGenerateOnReady)
      Regenerate();
  }

  public void Regenerate(int? overrideSeed = null)
  {
    EnsureNodes();
    ClearProps();
    _floorCenters.Clear();

    int width = Math.Max(8, GridWidth);
    int depth = Math.Max(8, GridDepth);

    var rng = new RandomNumberGenerator();
    int workingSeed = DetermineSeed(rng, overrideSeed ?? Seed);
    rng.Seed = (ulong)workingSeed;

    int[,] map = GenerateTopology(rng, width, depth);
    float[,] cornerHeights = GenerateCornerHeights(map, width, depth, workingSeed, _lastFeatureSeeds, out var cellHeights);
    LastCellHeights = cellHeights;
    BuildGeometry(map, cornerHeights, width, depth);

    ScatterProps(rng);
  }

  private int DetermineSeed(RandomNumberGenerator rng, int configured)
  {
    if (UseRandomSeed)
    {
      rng.Randomize();
      return (int)(rng.Randi() & int.MaxValue) + 1;
    }

    if (configured == 0)
      configured = 1;
    return Math.Abs(configured);
  }

  private int[,] GenerateTopology(RandomNumberGenerator rng, int width, int depth)
  {
    int[,] map = new int[width, depth];

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        bool border = x == 0 || z == 0 || x == width - 1 || z == depth - 1;
        map[x, z] = border ? 1 : (rng.Randf() < InitialWallChance ? 1 : 0);
      }
    }

    var buffer = new int[width, depth];
    for (int step = 0; step < Math.Max(0, SmoothingPasses); step++)
    {
      for (int x = 0; x < width; x++)
      {
        for (int z = 0; z < depth; z++)
        {
          int walls = CountWallNeighbours(map, width, depth, x, z);
          if (walls > 4)
            buffer[x, z] = 1;
          else if (walls < 4)
            buffer[x, z] = 0;
          else
            buffer[x, z] = map[x, z];
        }
      }

      (map, buffer) = (buffer, map);
    }

    CarveSpawnArea(map, width, depth);

    var featureSeeds = CarveFeatureRooms(map, rng, width, depth);
    _lastFeatureCenters.Clear();
    _lastFeatureSeeds.Clear();
    foreach (var seed in featureSeeds)
    {
      _lastFeatureSeeds.Add(seed);
      _lastFeatureCenters.Add(seed.Cell);
    }
    ConnectFeatureRooms(map, _lastFeatureSeeds, rng, width, depth);
    EnsureOpenFraction(map, rng, width, depth);

    KeepLargestRegion(map, width, depth);

    return map;
  }

  private static int CountWallNeighbours(int[,] map, int width, int depth, int cx, int cz)
  {
    int count = 0;
    for (int x = cx - 1; x <= cx + 1; x++)
    {
      for (int z = cz - 1; z <= cz + 1; z++)
      {
        if (x == cx && z == cz)
          continue;
        if (x < 0 || z < 0 || x >= width || z >= depth)
        {
          count++;
          continue;
        }
        if (map[x, z] == 1)
          count++;
      }
    }
    return count;
  }

  private void CarveSpawnArea(int[,] map, int width, int depth)
  {
    int radius = Math.Clamp(SpawnSafeRadius, 1, Math.Min(width, depth) / 3);
    int centerX = width / 2;
    int centerZ = depth / 2;

    for (int x = -radius; x <= radius; x++)
    {
      for (int z = -radius; z <= radius; z++)
      {
        int px = centerX + x;
        int pz = centerZ + z;
        if (px <= 0 || pz <= 0 || px >= width - 1 || pz >= depth - 1)
          continue;
        if (x * x + z * z <= radius * radius)
          map[px, pz] = 0;
      }
    }
  }

  private List<FeatureSeed> CarveFeatureRooms(int[,] map, RandomNumberGenerator rng, int width, int depth)
  {
    var seeds = new List<FeatureSeed>();
    int clampedCount = Math.Clamp(FeatureCount, 1, Math.Min(width, depth) / 2);
    float minRadius = Mathf.Max(1.0f, Math.Min(FeatureRadiusMin, FeatureRadiusMax));
    float maxRadius = Mathf.Max(minRadius, FeatureRadiusMax);
    int margin = Mathf.CeilToInt(maxRadius) + Math.Max(2, CorridorRadius + 1);
    int maxMargin = Math.Max(2, Math.Min(width, depth) / 2 - 1);
    margin = Math.Clamp(margin, 2, Math.Max(2, maxMargin));

    for (int i = 0; i < clampedCount; i++)
    {
      int cx = (int)rng.RandiRange(margin, width - margin - 1);
      int cz = (int)rng.RandiRange(margin, depth - margin - 1);
      int radius = Mathf.Clamp(Mathf.RoundToInt(rng.RandfRange(minRadius, maxRadius)), 1, Math.Max(1, Math.Min(width, depth) / 2));
      CarveDisc(map, cx, cz, radius, width, depth);
      float peakChance = Mathf.Clamp(0.5f + FeatureHeightBias * 0.5f, 0.0f, 1.0f);
      bool isPeak = rng.Randf() < peakChance;
      seeds.Add(new FeatureSeed(new Vector2I(cx, cz), radius, isPeak));
    }

    return seeds;
  }

  private void ConnectFeatureRooms(int[,] map, IReadOnlyList<FeatureSeed> seeds, RandomNumberGenerator rng, int width, int depth)
  {
    if (seeds.Count < 2)
      return;

    var centers = new List<Vector2I>(seeds.Count);
    foreach (var seed in seeds)
      centers.Add(seed.Cell);

    centers.Sort((a, b) => a.X.CompareTo(b.X));
    for (int i = 0; i < centers.Count - 1; i++)
      CarveCorridor(map, centers[i], centers[i + 1], width, depth);

    var byZ = new List<Vector2I>(centers);
    byZ.Sort((a, b) => a.Y.CompareTo(b.Y));
    for (int i = 0; i < byZ.Count - 1; i++)
      CarveCorridor(map, byZ[i], byZ[i + 1], width, depth);

    int extraLinks = Math.Max(0, centers.Count / 2);
    for (int i = 0; i < extraLinks; i++)
    {
      var a = centers[(int)rng.RandiRange(0, centers.Count - 1)];
      var b = centers[(int)rng.RandiRange(0, centers.Count - 1)];
      if (a == b)
        continue;
      CarveCorridor(map, a, b, width, depth);
    }
  }

  private void EnsureOpenFraction(int[,] map, RandomNumberGenerator rng, int width, int depth)
  {
    float target = Mathf.Clamp(TargetOpenFraction, 0.15f, 0.85f);
    int total = width * depth;
    int open = CountOpenCells(map, width, depth);
    int maxIterations = total;

    while (open < target * total && maxIterations-- > 0)
    {
      int startX = (int)rng.RandiRange(2, width - 3);
      int startZ = (int)rng.RandiRange(2, depth - 3);
      int steps = (int)rng.RandiRange(width / 3, width);
      DrunkardWalk(map, new Vector2I(startX, startZ), steps, rng, width, depth);
      open = CountOpenCells(map, width, depth);
    }
  }

  private void CarveDisc(int[,] map, int centerX, int centerZ, int radius, int width, int depth)
  {
    int radiusSq = radius * radius;
    for (int x = centerX - radius; x <= centerX + radius; x++)
    {
      if (x <= 0 || x >= width - 1)
        continue;
      for (int z = centerZ - radius; z <= centerZ + radius; z++)
      {
        if (z <= 0 || z >= depth - 1)
          continue;
        int dx = x - centerX;
        int dz = z - centerZ;
        if (dx * dx + dz * dz <= radiusSq)
          map[x, z] = 0;
      }
    }
  }

  private void CarveCorridor(int[,] map, Vector2I from, Vector2I to, int mapWidth, int mapDepth)
  {
    int x = from.X;
    int z = from.Y;
    int dx = Math.Abs(to.X - from.X);
    int dz = Math.Abs(to.Y - from.Y);
    int sx = from.X < to.X ? 1 : -1;
    int sz = from.Y < to.Y ? 1 : -1;
    int err = dx - dz;

    while (true)
    {
      CarveCellWithWidth(map, x, z, mapWidth, mapDepth);
      if (x == to.X && z == to.Y)
        break;
      int e2 = err * 2;
      if (e2 > -dz)
      {
        err -= dz;
        x += sx;
      }
      if (e2 < dx)
      {
        err += dx;
        z += sz;
      }
    }
  }

  private void CarveCellWithWidth(int[,] map, int cx, int cz, int mapWidth, int mapDepth)
  {
    int radius = Math.Max(0, CorridorRadius);
    for (int dx = -radius; dx <= radius; dx++)
    {
      int px = cx + dx;
      if (px <= 0 || px >= mapWidth - 1)
        continue;
      for (int dz = -radius; dz <= radius; dz++)
      {
        int pz = cz + dz;
        if (pz <= 0 || pz >= mapDepth - 1)
          continue;
        map[px, pz] = 0;
      }
    }
  }

  private void DrunkardWalk(int[,] map, Vector2I start, int steps, RandomNumberGenerator rng, int width, int depth)
  {
    Vector2I pos = start;
    steps = Math.Max(1, steps);
    for (int i = 0; i < steps; i++)
    {
      CarveCellWithWidth(map, pos.X, pos.Y, width, depth);
      int dir = (int)rng.RandiRange(0, 3);
      pos += dir switch
      {
        0 => Vector2I.Right,
        1 => Vector2I.Left,
        2 => Vector2I.Down,
        _ => Vector2I.Up
      };
      pos.X = Math.Clamp(pos.X, 1, width - 2);
      pos.Y = Math.Clamp(pos.Y, 1, depth - 2);
    }
  }

  private int CountOpenCells(int[,] map, int width, int depth)
  {
    int count = 0;
    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (map[x, z] == 0)
          count++;
      }
    }
    return count;
  }

  private void KeepLargestRegion(int[,] map, int width, int depth)
  {
    bool[,] visited = new bool[width, depth];
    List<Vector2I> largest = new();

    int[,] directions = new int[,] { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (map[x, z] == 1 || visited[x, z])
          continue;

        List<Vector2I> region = new();
        Queue<Vector2I> frontier = new();
        frontier.Enqueue(new Vector2I(x, z));
        visited[x, z] = true;

        while (frontier.Count > 0)
        {
          Vector2I cell = frontier.Dequeue();
          region.Add(cell);

          for (int i = 0; i < 4; i++)
          {
            int nx = cell.X + directions[i, 0];
            int nz = cell.Y + directions[i, 1];
            if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
              continue;
            if (visited[nx, nz] || map[nx, nz] == 1)
              continue;

            visited[nx, nz] = true;
            frontier.Enqueue(new Vector2I(nx, nz));
          }
        }

        if (region.Count > largest.Count)
          largest = region;
      }
    }

    if (largest.Count == 0)
      return;

    bool[,] keep = new bool[width, depth];
    foreach (Vector2I c in largest)
      keep[c.X, c.Y] = true;

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (!keep[x, z])
          map[x, z] = 1;
      }
    }
  }

  private float[,] GenerateCornerHeights(int[,] map, int width, int depth, int workingSeed, IReadOnlyList<FeatureSeed> featureSeeds, out float[,] cellHeights)
  {
    bool[,] walkable = new bool[width, depth];
    int walkableCount = 0;
    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (map[x, z] == 0)
        {
          walkable[x, z] = true;
          walkableCount++;
        }
      }
    }

    cellHeights = new float[width, depth];

    if (walkableCount == 0)
      return new float[width + 1, depth + 1];

    var seeds = featureSeeds ?? Array.Empty<FeatureSeed>();

    var macroNoise = new FastNoiseLite
    {
      Seed = workingSeed * 37 + 97,
      NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
      Frequency = Mathf.Max(0.001f, MacroNoiseFrequency),
      FractalOctaves = 4,
      FractalGain = 0.55f,
      FractalWeightedStrength = 0.35f
    };

    var detailNoise = new FastNoiseLite
    {
      Seed = workingSeed * 67 + 211,
      NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
      Frequency = Mathf.Max(0.001f, MacroNoiseFrequency * 3.25f),
      FractalOctaves = 3,
      FractalGain = 0.6f
    };

    var jitterRng = new RandomNumberGenerator();
    jitterRng.Seed = (ulong)(workingSeed * 97 + 13);

    float macroScale = Math.Max(0.05f, MacroNoiseStrength);
    float microScale = Math.Max(0.0f, MicroHeightStrength);
    float featureScale = Math.Max(0.0f, FeatureHeightBoost);

    float centerX = (width - 1) * 0.5f;
    float centerZ = (depth - 1) * 0.5f;
    float invHalfW = width > 1 ? 1.0f / (width * 0.5f) : 1.0f;
    float invHalfD = depth > 1 ? 1.0f / (depth * 0.5f) : 1.0f;

    float[,] heights = new float[width, depth];

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (!walkable[x, z])
          continue;

        float macro = macroNoise.GetNoise2D(x, z);
        float detail = detailNoise.GetNoise2D(x, z);

        float height = macro * MacroHeightMultiplier * macroScale;
        height += detail * MicroHeightMultiplier * microScale;

        if (seeds.Count > 0 && featureScale > 0.0f)
        {
          foreach (var seed in seeds)
          {
            float dx = x - seed.Cell.X;
            float dz = z - seed.Cell.Y;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            float influence = 1.0f - dist / (seed.Radius * 1.35f);
            if (influence <= 0.0f)
              continue;

            float shaped = Mathf.Pow(Mathf.Clamp(influence, 0.0f, 1.0f), Math.Max(0.1f, FeatureHeightExponent));
            float bump = shaped * featureScale;
            if (!seed.IsPeak)
              bump = -bump;
            height += bump;
          }
        }

        if (EdgeHeightFalloff > 0.0f)
        {
          float nx = (x - centerX) * invHalfW;
          float nz = (z - centerZ) * invHalfD;
          float radial = Mathf.Clamp(Mathf.Sqrt(nx * nx + nz * nz), 0.0f, 1.0f);
          height = Mathf.Lerp(height, 0.0f, radial * EdgeHeightFalloff);
        }

        height += RandomHeightJitter(jitterRng);
        heights[x, z] = height;
      }
    }

    Vector2I spawn = FindSpawnCell(map, width, depth);
    float spawnHeight = heights[spawn.X, spawn.Y];
    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (walkable[x, z])
          heights[x, z] -= spawnHeight;
      }
    }

    ApplyHeightSmoothing(heights, walkable);

    spawnHeight = heights[spawn.X, spawn.Y];
    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (walkable[x, z])
          heights[x, z] -= spawnHeight;
      }
    }

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (walkable[x, z])
          cellHeights[x, z] = heights[x, z];
      }
    }

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (walkable[x, z])
          continue;

        float maxNeighbor = float.MinValue;
        for (int dx = -1; dx <= 1; dx++)
        {
          for (int dz = -1; dz <= 1; dz++)
          {
            if (dx == 0 && dz == 0)
              continue;
            int nx = x + dx;
            int nz = z + dz;
            if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
              continue;
            if (!walkable[nx, nz])
              continue;
            maxNeighbor = Mathf.Max(maxNeighbor, heights[nx, nz]);
          }
        }

        if (maxNeighbor == float.MinValue)
          maxNeighbor = 0.0f;

        cellHeights[x, z] = maxNeighbor + CliffHeight;
      }
    }

    float[,] cornerHeights = new float[width + 1, depth + 1];
    for (int x = 0; x <= width; x++)
    {
      for (int z = 0; z <= depth; z++)
      {
        float sum = 0.0f;
        int count = 0;
        AddCornerHeight(map, cellHeights, width, depth, x - 1, z - 1, ref sum, ref count);
        AddCornerHeight(map, cellHeights, width, depth, x, z - 1, ref sum, ref count);
        AddCornerHeight(map, cellHeights, width, depth, x - 1, z, ref sum, ref count);
        AddCornerHeight(map, cellHeights, width, depth, x, z, ref sum, ref count);

        cornerHeights[x, z] = count > 0 ? sum / count : 0.0f;
      }
    }

    return cornerHeights;
  }

  private void ApplyHeightSmoothing(float[,] heights, bool[,] walkable)
  {
    int width = heights.GetLength(0);
    int depth = heights.GetLength(1);
    float maxDelta = Mathf.Max(0.05f, MaxSlopeDelta);
    int passes = Math.Max(0, HeightSmoothingPasses);
    if (passes == 0)
      return;

    float strength = Mathf.Clamp(HeightSmoothingStrength, 0.0f, 1.0f);
    var delta = new float[width, depth];
    var smooth = new float[width, depth];
    int[,] directions = new int[,] { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

    for (int iter = 0; iter < passes; iter++)
    {
      for (int x = 0; x < width; x++)
      {
        for (int z = 0; z < depth; z++)
        {
          if (walkable[x, z])
            delta[x, z] = 0.0f;
        }
      }

      for (int x = 0; x < width; x++)
      {
        for (int z = 0; z < depth; z++)
        {
          if (!walkable[x, z])
            continue;

          float current = heights[x, z];

          for (int dir = 0; dir < 4; dir++)
          {
            int nx = x + directions[dir, 0];
            int nz = z + directions[dir, 1];
            if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
              continue;
            if (!walkable[nx, nz])
              continue;
            if (nx < x || (nx == x && nz <= z))
              continue;

            float neighbor = heights[nx, nz];
            float diff = current - neighbor;
            if (diff > maxDelta)
            {
              float adjust = (diff - maxDelta) * 0.5f;
              delta[x, z] -= adjust;
              delta[nx, nz] += adjust;
            }
            else if (diff < -maxDelta)
            {
              float adjust = (-diff - maxDelta) * 0.5f;
              delta[x, z] += adjust;
              delta[nx, nz] -= adjust;
            }
          }
        }
      }

      for (int x = 0; x < width; x++)
      {
        for (int z = 0; z < depth; z++)
        {
          if (walkable[x, z])
            heights[x, z] += delta[x, z];
        }
      }

      if (strength > 0.0f)
      {
        for (int x = 0; x < width; x++)
        {
          for (int z = 0; z < depth; z++)
          {
            if (!walkable[x, z])
              continue;

            float sum = heights[x, z];
            int count = 1;
            for (int dir = 0; dir < 4; dir++)
            {
              int nx = x + directions[dir, 0];
              int nz = z + directions[dir, 1];
              if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                continue;
              if (!walkable[nx, nz])
                continue;
              sum += heights[nx, nz];
              count++;
            }

            float average = sum / count;
            smooth[x, z] = Mathf.Lerp(heights[x, z], average, strength);
          }
        }

        for (int x = 0; x < width; x++)
        {
          for (int z = 0; z < depth; z++)
          {
            if (walkable[x, z])
              heights[x, z] = smooth[x, z];
          }
        }
      }
    }
  }

  private static void AddCornerHeight(int[,] map, float[,] cellHeights, int width, int depth, int cellX, int cellZ, ref float sum, ref int count)
  {
    if (cellX < 0 || cellZ < 0 || cellX >= width || cellZ >= depth)
      return;
    if (map[cellX, cellZ] != 0)
      return;

    sum += cellHeights[cellX, cellZ];
    count++;
  }

  private Vector2I FindSpawnCell(int[,] map, int width, int depth)
  {
    int centerX = width / 2;
    int centerZ = depth / 2;
    if (map[centerX, centerZ] == 0)
      return new Vector2I(centerX, centerZ);

    Queue<Vector2I> queue = new();
    bool[,] visited = new bool[width, depth];
    int[,] directions = new int[,] { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

    queue.Enqueue(new Vector2I(centerX, centerZ));
    visited[centerX, centerZ] = true;

    while (queue.Count > 0)
    {
      Vector2I cell = queue.Dequeue();
      if (map[cell.X, cell.Y] == 0)
        return cell;

      for (int i = 0; i < 4; i++)
      {
        int nx = cell.X + directions[i, 0];
        int nz = cell.Y + directions[i, 1];
        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
          continue;
        if (visited[nx, nz])
          continue;

        visited[nx, nz] = true;
        queue.Enqueue(new Vector2I(nx, nz));
      }
    }

    return new Vector2I(centerX, centerZ);
  }

  private float IndexToHeight(int tier)
  {
    float offset = (tier - (Math.Max(2, HeightTierCount) - 1) * 0.5f) * HeightScale;
    return offset;
  }

  private float RandomHeightJitter(RandomNumberGenerator rng)
  {
    if (HeightNoiseJitter <= 0.0f)
      return 0.0f;
    return rng.RandfRange(-HeightNoiseJitter, HeightNoiseJitter);
  }

  private void BuildGeometry(int[,] map, float[,] heights, int width, int depth)
  {
    var floorTool = new SurfaceTool();
    floorTool.Begin(Mesh.PrimitiveType.Triangles);

    var wallTool = new SurfaceTool();
    wallTool.Begin(Mesh.PrimitiveType.Triangles);

    var ceilingTool = new SurfaceTool();
    ceilingTool.Begin(Mesh.PrimitiveType.Triangles);

    float offsetX = (width * CellSize) * 0.5f;
    float offsetZ = (depth * CellSize) * 0.5f;

    Vector3[,] cornerPositions = new Vector3[width + 1, depth + 1];
    for (int x = 0; x <= width; x++)
    {
      for (int z = 0; z <= depth; z++)
      {
        float px = x * CellSize - offsetX;
        float pz = z * CellSize - offsetZ;
        cornerPositions[x, z] = new Vector3(px, heights[x, z], pz);
      }
    }

    List<Vector3> newFloorCenters = new();

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (map[x, z] != 0)
          continue;

        Vector3 v00 = cornerPositions[x, z];
        Vector3 v10 = cornerPositions[x + 1, z];
        Vector3 v11 = cornerPositions[x + 1, z + 1];
        Vector3 v01 = cornerPositions[x, z + 1];

        AddQuad(floorTool, v00, v10, v11, v01, true);

        Vector3 center = (v00 + v10 + v11 + v01) * 0.25f;
        newFloorCenters.Add(center);

        TryAddWall(map, wallTool, width, depth, x, z - 1, v00, v10, Vector3.Forward);
        TryAddWall(map, wallTool, width, depth, x, z + 1, v01, v11, Vector3.Back);
        TryAddWall(map, wallTool, width, depth, x - 1, z, v00, v01, Vector3.Left);
        TryAddWall(map, wallTool, width, depth, x + 1, z, v10, v11, Vector3.Right);
      }
    }

    for (int x = 0; x < width; x++)
    {
      for (int z = 0; z < depth; z++)
      {
        if (map[x, z] != 1)
          continue;
        if (!HasFloorNeighbour(map, width, depth, x, z))
          continue;

        Vector3 v00 = cornerPositions[x, z] + Vector3.Up * CliffHeight;
        Vector3 v10 = cornerPositions[x + 1, z] + Vector3.Up * CliffHeight;
        Vector3 v11 = cornerPositions[x + 1, z + 1] + Vector3.Up * CliffHeight;
        Vector3 v01 = cornerPositions[x, z + 1] + Vector3.Up * CliffHeight;

        AddQuad(ceilingTool, v00, v10, v11, v01, true);
      }
    }

    floorTool.GenerateNormals();
    wallTool.GenerateNormals();
    ceilingTool.GenerateNormals();

    var mesh = new ArrayMesh();
    floorTool.Commit(mesh);
    wallTool.Commit(mesh);
    ceilingTool.Commit(mesh);

    ApplyMaterials(mesh);

    _terrainMesh!.Mesh = mesh;
    _terrainMesh.Transform = Transform3D.Identity;

    var shape = mesh.CreateTrimeshShape();
    _collisionShape!.Shape = shape;

    EnsureNavigation(mesh);

    _lastMesh = mesh;
    _floorCenters.AddRange(newFloorCenters);
  }

  private void AddQuad(SurfaceTool tool, Vector3 v00, Vector3 v10, Vector3 v11, Vector3 v01, bool doubleSided)
  {
    Vector2 uv00 = new(v00.X * TextureScale, v00.Z * TextureScale);
    Vector2 uv10 = new(v10.X * TextureScale, v10.Z * TextureScale);
    Vector2 uv11 = new(v11.X * TextureScale, v11.Z * TextureScale);
    Vector2 uv01 = new(v01.X * TextureScale, v01.Z * TextureScale);

    tool.SetUV(uv00);
    tool.AddVertex(v00);
    tool.SetUV(uv10);
    tool.AddVertex(v10);
    tool.SetUV(uv11);
    tool.AddVertex(v11);

    tool.SetUV(uv00);
    tool.AddVertex(v00);
    tool.SetUV(uv11);
    tool.AddVertex(v11);
    tool.SetUV(uv01);
    tool.AddVertex(v01);

    if (!doubleSided)
      return;

    tool.SetUV(uv00);
    tool.AddVertex(v00);
    tool.SetUV(uv11);
    tool.AddVertex(v11);
    tool.SetUV(uv10);
    tool.AddVertex(v10);

    tool.SetUV(uv00);
    tool.AddVertex(v00);
    tool.SetUV(uv01);
    tool.AddVertex(v01);
    tool.SetUV(uv11);
    tool.AddVertex(v11);
  }

  private void TryAddWall(int[,] map, SurfaceTool wallTool, int width, int depth, int nx, int nz, Vector3 edgeA, Vector3 edgeB, Vector3 outward)
  {
    if (nx >= 0 && nz >= 0 && nx < width && nz < depth && map[nx, nz] == 0)
      return;

    Vector3 bottomA = edgeA;
    Vector3 bottomB = edgeB;
    Vector3 topA = new(edgeA.X, edgeA.Y + CliffHeight, edgeA.Z);
    Vector3 topB = new(edgeB.X, edgeB.Y + CliffHeight, edgeB.Z);

    Vector3 normal = (bottomB - bottomA).Cross(topA - bottomA).Normalized();
    if (normal.Dot(outward) < 0)
    {
      (bottomA, bottomB) = (bottomB, bottomA);
      (topA, topB) = (topB, topA);
    }

    AddQuad(wallTool, bottomA, bottomB, topB, topA, false);
  }

  private static bool HasFloorNeighbour(int[,] map, int width, int depth, int x, int z)
  {
    for (int dx = -1; dx <= 1; dx++)
    {
      for (int dz = -1; dz <= 1; dz++)
      {
        if (Math.Abs(dx) + Math.Abs(dz) != 1)
          continue;
        int nx = x + dx;
        int nz = z + dz;
        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
          continue;
        if (map[nx, nz] == 0)
          return true;
      }
    }
    return false;
  }

  private void ApplyMaterials(ArrayMesh mesh)
  {
    var floorMaterial = CreateFloorMaterial();
    var wallMaterial = CreateWallMaterial();

    if (mesh.GetSurfaceCount() >= 1)
      mesh.SurfaceSetMaterial(0, floorMaterial);
    if (mesh.GetSurfaceCount() >= 2)
      mesh.SurfaceSetMaterial(1, wallMaterial);
    if (mesh.GetSurfaceCount() >= 3)
      mesh.SurfaceSetMaterial(2, floorMaterial);
  }

  private Material CreateFloorMaterial()
  {
    var mat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      DisableReceiveShadows = true,
      Roughness = 1.0f,
      Metallic = 0.0f,
      AlbedoColor = new Color(0.4f, 0.8f, 0.4f)
    };

    if (FloorTexture != null)
    {
      mat.AlbedoTexture = FloorTexture;
      mat.Uv1Scale = Vector3.One;
    }

    return mat;
  }

  private Material CreateWallMaterial()
  {
    var mat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      DisableReceiveShadows = true,
      Roughness = 0.9f,
      Metallic = 0.0f,
      AlbedoColor = new Color(0.6f, 0.6f, 0.65f),
      CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    if (WallTexture != null)
    {
      mat.AlbedoTexture = WallTexture;
      mat.Uv1Scale = Vector3.One;
    }

    return mat;
  }

  private void EnsureNavigation(ArrayMesh mesh)
  {
    if (_navigationRegion == null)
      return;

    var navMesh = new NavigationMesh
    {
      CellHeight = 0.2f,
      AgentHeight = 1.8f,
      AgentRadius = 0.6f,
      AgentMaxSlope = 38.0f
    };

    float mapCellSize = 0.25f;
    float mapCellHeight = 0.2f;
    var world = GetWorld3D();
    if (world != null)
    {
      Rid navMap = world.NavigationMap;
      if (navMap.IsValid)
      {
        mapCellSize = NavigationServer3D.MapGetCellSize(navMap);
        mapCellHeight = NavigationServer3D.MapGetCellHeight(navMap);
      }
    }

    if (mapCellSize <= 0.0f)
      mapCellSize = 0.25f;
    if (mapCellHeight <= 0.0f)
      mapCellHeight = 0.2f;

    navMesh.CellSize = mapCellSize;
    navMesh.CellHeight = mapCellHeight;
    navMesh.CreateFromMesh(mesh);
    _navigationRegion.NavigationMesh = navMesh;
  }

  private void ScatterProps(RandomNumberGenerator rng)
  {
    if (PropScene == null || _floorCenters.Count == 0 || PropChance <= 0)
      return;

    foreach (Vector3 position in _floorCenters)
    {
      if (rng.Randf() > PropChance)
        continue;

      var inst = PropScene.Instantiate<Node3D>();
      inst.Position = position + new Vector3(0, 0.25f, 0);
      inst.Rotation = new Vector3(0, rng.RandfRange(0, Mathf.Tau), 0);
      _propContainer!.AddChild(inst);
    }
  }

  private void EnsureNodes()
  {
    _terrainMesh ??= GetNodeOrNull<MeshInstance3D>("TerrainMesh");
    if (_terrainMesh == null)
    {
      _terrainMesh = new MeshInstance3D { Name = "TerrainMesh" };
      AddChild(_terrainMesh);
    }

    _collisionBody ??= GetNodeOrNull<StaticBody3D>("TerrainBody");
    if (_collisionBody == null)
    {
      _collisionBody = new StaticBody3D { Name = "TerrainBody" };
      AddChild(_collisionBody);
    }

    _collisionShape ??= _collisionBody.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
    if (_collisionShape == null)
    {
      _collisionShape = new CollisionShape3D { Name = "CollisionShape3D" };
      _collisionBody.AddChild(_collisionShape);
    }

    _navigationRegion ??= GetNodeOrNull<NavigationRegion3D>("Navigation");
    if (_navigationRegion == null)
    {
      _navigationRegion = new NavigationRegion3D { Name = "Navigation" };
      AddChild(_navigationRegion);
    }

    _propContainer ??= GetNodeOrNull<Node3D>("Props");
    if (_propContainer == null)
    {
      _propContainer = new Node3D { Name = "Props" };
      AddChild(_propContainer);
    }
  }

  private void ClearProps()
  {
    if (_propContainer == null)
      return;

    foreach (Node child in _propContainer.GetChildren())
    {
      child.QueueFree();
    }
  }

  private static T? LoadOptional<T>(string path) where T : class
  {
    return ResourceLoader.Exists(path) ? ResourceLoader.Load<T>(path) : null;
  }
}
