using Godot;
#nullable enable
using System;
using System.Collections.Generic;

public sealed partial class ExplosionVfxManager : Node3D
{
  public static ExplosionVfxManager? Instance { get; private set; }

  private readonly struct Explosion
  {
    public readonly Vector3 Pos;
    public readonly float Radius;
    public readonly float Lifetime;
    public readonly float StartTime;
    public Explosion(Vector3 pos, float radius, float lifetime, float start)
    { Pos = pos; Radius = radius; Lifetime = lifetime; StartTime = start; }
  }

  private MultiMesh _mm = null!;
  private MultiMeshInstance3D _mmi = null!;
  private readonly List<Explosion> _active = new List<Explosion>(64);
  private float _now;

  [Export]
  public float DefaultLifetime { get; set; } = 0.35f;

  [Export]
  public Color ExplosionColor { get; set; } = new Color(1.0f, 0.6f, 0.2f, 0.8f);

  public override void _EnterTree()
  {
    Instance = this;
  }

  public override void _ExitTree()
  {
    if (Instance == this) Instance = null;
  }

  public override void _Ready()
  {
    // Create a billboarded quad mesh
    var quad = new QuadMesh
    {
      Size = new Vector2(1, 1)
    };

    _mm = new MultiMesh
    {
      TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
      Mesh = quad,
      UseCustomData = false,
    };

    _mmi = new MultiMeshInstance3D
    {
      Multimesh = _mm,
      Visible = true,
    };

    var mat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      AlbedoColor = ExplosionColor,
      DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
      BlendMode = BaseMaterial3D.BlendModeEnum.Add,
    };
    _mmi.MaterialOverride = mat;

    AddChild(_mmi);
  }

  public void Spawn(Vector3 position, float radius, float? lifetime = null)
  {
    float lt = Math.Max(0.05f, lifetime ?? DefaultLifetime);
    float r = Math.Max(0.05f, radius);
    _active.Add(new Explosion(position, r, lt, GetTimeSeconds()));
    // Also spawn a few sprite-sheet bursts to match legacy look
    SpawnImpactSprites(position, r);
  }

  public override void _Process(double delta)
  {
    _now = GetTimeSeconds();
    // Compact list and update MultiMesh transforms
    int write = 0;
    int count = _active.Count;
    for (int i = 0; i < count; i++)
    {
      var e = _active[i];
      float age = _now - e.StartTime;
      if (age >= e.Lifetime)
        continue;
      _active[write++] = e;
    }
    if (write < count)
      _active.RemoveRange(write, count - write);

    int activeCount = _active.Count;
    _mm.InstanceCount = activeCount;
    for (int i = 0; i < activeCount; i++)
    {
      var e = _active[i];
      float age = MathF.Max(0f, _now - e.StartTime);
      float t = MathF.Min(1f, age / MathF.Max(0.001f, e.Lifetime));
      // Scale from 60% to 100% of radius over life; quick in/out curve
      float scale = e.Radius * (0.6f + 0.4f * EaseOutCubic(t));
      var xform = new Transform3D(Basis.Identity.Scaled(new Vector3(scale, scale, scale)), e.Pos);
      _mm.SetInstanceTransform(i, xform);
    }
  }

  private static float EaseOutCubic(float t)
  {
    t = MathF.Max(0f, MathF.Min(1f, t));
    float inv = 1f - t;
    return 1f - inv * inv * inv;
  }

  private static float GetTimeSeconds() => (float)Time.GetTicksMsec() / 1000f;

  private void SpawnImpactSprites(Vector3 center, float radius)
  {
    // Main burst at center
    float basePixel = Mathf.Clamp(radius * 0.08f, 0.04f, 0.14f);
    ImpactSprite.Spawn(this, center, Vector3.Up, basePixel);

    // Add a few offset bursts for richness
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    int count = 3 + (int)Mathf.Round(Mathf.Clamp(radius, 0.5f, 3.5f));
    for (int i = 0; i < count; i++)
    {
      Vector3 jitter = new Vector3(
        rng.RandfRange(-1f, 1f),
        rng.RandfRange(-0.2f, 0.6f),
        rng.RandfRange(-1f, 1f)
      ).Normalized() * (radius * rng.RandfRange(0.1f, 0.35f));
      float px = Mathf.Clamp(basePixel * rng.RandfRange(0.7f, 1.15f), 0.03f, 0.16f);
      ImpactSprite.Spawn(this, center + jitter, Vector3.Up, px);
    }
  }
}
