using Godot;
using System;
using System.Collections.Generic;

#nullable enable

namespace Shared.Effects
{
  /// <summary>Balatro-style dissolve burst driven by a preconfigured GPUParticles3D.</summary>
  public partial class DissolveBurst : Node3D
  {
    private const string ScenePath = "res://shared/effects/DissolveBurst.tscn";
    private const int ParticleCount = 90;
    private const float LifetimeSeconds = 0.7f;
    private const float MinScaleRatio = 0.22f;
    private const float MaxScaleRatio = 0.62f;
    private const float MinSpeedRatio = 0.12f;
    private const float MaxSpeedRatio = 0.36f;

    private static PackedScene? _cachedScene;
    private static CurveTexture? _scaleCurve;

    private static readonly Color[] DefaultPalette =
    {
      new Color(0.215686f, 0.258823f, 0.266667f, 1f),
      new Color(0.992156f, 0.635294f, 0f, 1f),
      new Color(0.996078f, 0.372549f, 0.333333f, 1f),
      new Color(0.917647f, 0.752941f, 0.345098f, 1f),
      new Color(0.74902f, 0.780392f, 0.835294f, 1f)
    };

    private GpuParticles3D _particles = default!;

    public static void Spawn(Node parent, Transform3D transform, IReadOnlyList<Color>? palette, Vector3 halfExtents)
    {
      if (parent == null || !IsInstanceValid(parent))
        return;

      _cachedScene ??= ResourceLoader.Load<PackedScene>(ScenePath);
      if (_cachedScene == null)
      {
        GD.PushWarning($"Failed to load dissolve burst scene at {ScenePath}");
        return;
      }

      var instance = _cachedScene.Instantiate<DissolveBurst>();
      parent.AddChild(instance);
      instance.GlobalTransform = transform;
      instance.Configure(palette, halfExtents);
    }

    public override void _Ready()
    {
      _particles = GetNode<GpuParticles3D>("Particles");
      _particles.Amount = ParticleCount;
      _particles.OneShot = true;
      _particles.DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth;
      _particles.Finished += OnFinished;
    }

    private void Configure(IReadOnlyList<Color>? palette, Vector3 halfExtents)
    {
      if (_particles == null) return;

      Vector3 extents = new Vector3(Mathf.Max(halfExtents.X, 0.35f), Mathf.Max(halfExtents.Y, 0.2f), Mathf.Max(halfExtents.Z, 0.35f));
      float maxExtent = Mathf.Max(extents.X, Mathf.Max(extents.Y, extents.Z));

      _particles.Emitting = false;
      _particles.Lifetime = LifetimeSeconds;
      _particles.Preprocess = 0f;
      _particles.VisibilityAabb = new Aabb(Vector3.Zero, extents * 4f);

      var material = CreateProcessMaterial(extents, maxExtent, palette);
      _particles.ProcessMaterial = material;

      CallDeferred(nameof(StartEmission));
    }

    private void StartEmission()
    {
      if (!IsInstanceValid(_particles)) return;
      _particles.Restart();
      _particles.Emitting = true;
    }

    private static ParticleProcessMaterial CreateProcessMaterial(Vector3 extents, float maxExtent, IReadOnlyList<Color>? palette)
    {
      var material = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
        EmissionBoxExtents = extents,
        LifetimeRandomness = 0.25f,
        Spread = 180f,
        Gravity = Vector3.Zero,
        InitialVelocityMin = maxExtent * MinSpeedRatio,
        InitialVelocityMax = maxExtent * MaxSpeedRatio,
        ScaleMin = maxExtent * MinScaleRatio,
        ScaleMax = maxExtent * MaxScaleRatio,
        ScaleCurve = GetScaleCurve(),
        ColorRamp = CreateGradientTexture(palette)
      };

      return material;
    }

    private static CurveTexture GetScaleCurve()
    {
      if (_scaleCurve != null)
        return _scaleCurve;

      var curve = new Curve();
      curve.AddPoint(new Vector2(0f, 0f));
      curve.AddPoint(new Vector2(0.5f, 1f));
      curve.AddPoint(new Vector2(1f, 0f));
      _scaleCurve = new CurveTexture
      {
        Curve = curve
      };
      return _scaleCurve;
    }

    private static GradientTexture1D CreateGradientTexture(IReadOnlyList<Color>? palette)
    {
      var gradient = new Gradient();
      var source = (palette != null && palette.Count > 0) ? palette : DefaultPalette;

      if (source.Count <= 1)
      {
        gradient.AddPoint(0f, source[0]);
        gradient.AddPoint(1f, source[0]);
      }
      else
      {
        for (int i = 0; i < source.Count; i++)
        {
          float offset = (float)i / (source.Count - 1);
          gradient.AddPoint(offset, source[i]);
        }
      }

      return new GradientTexture1D
      {
        Gradient = gradient
      };
    }

    private void OnFinished()
    {
      QueueFree();
    }
  }
}
