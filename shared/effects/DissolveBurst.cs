using Godot;
using System;
using System.Collections.Generic;

#nullable enable

namespace Shared.Effects
{
  /// <summary>Balatro-style dissolve burst driven by preconfigured GPUParticles3D emitters in the scene.</summary>
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
    private static GradientTexture1D? _alphaOnlyRamp;

    private readonly List<GpuParticles3D> _emitters = new();
    private int _remainingEmitters;

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
      // Collect all GPUParticles3D children set up in the .tscn
      foreach (var child in GetChildren())
      {
        if (child is GpuParticles3D p)
          _emitters.Add(p);
      }

      // Back-compat: if the scene only has a single emitter named "Particles".
      if (_emitters.Count == 0)
      {
        var p = GetNodeOrNull<GpuParticles3D>("Particles");
        if (p != null) _emitters.Add(p);
      }

      foreach (var p in _emitters)
      {
        p.Amount = Math.Max(1, ParticleCount / Math.Max(1, _emitters.Count));
        p.OneShot = true;
        p.DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth;
        p.Explosiveness = 1f;
      }
    }

    private void Configure(IReadOnlyList<Color>? palette, Vector3 halfExtents)
    {
      if (_emitters.Count == 0) return;

      Vector3 extents = new Vector3(Mathf.Max(halfExtents.X, 0.35f), Mathf.Max(halfExtents.Y, 0.2f), Mathf.Max(halfExtents.Z, 0.35f));
      float maxExtent = Mathf.Max(extents.X, Mathf.Max(extents.Y, extents.Z));

      foreach (var p in _emitters)
      {
        p.Emitting = false;
        p.Lifetime = LifetimeSeconds;
        p.Preprocess = 0f;
        p.VisibilityAabb = new Aabb(Vector3.Zero, extents * 4f);

        if (p.ProcessMaterial is ParticleProcessMaterial mat)
        {
          // Author the look in the scene; size the emission volume here.
          mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
          mat.EmissionBoxExtents = extents;

          // Respect scene-set velocities; only provide defaults if unset.
          bool hasVelocity = mat.InitialVelocityMin > 0.0001f || mat.InitialVelocityMax > 0.0001f;
          if (!hasVelocity)
          {
            mat.InitialVelocityMin = maxExtent * MinSpeedRatio;
            mat.InitialVelocityMax = maxExtent * MaxSpeedRatio;
          }

          // Respect scene scale if provided; else default to ratios.
          bool hasScale = mat.ScaleMin > 0.0001f || mat.ScaleMax > 0.0001f;
          if (!hasScale)
          {
            mat.ScaleMin = maxExtent * MinScaleRatio;
            mat.ScaleMax = maxExtent * MaxScaleRatio;
          }

          mat.ScaleCurve ??= GetScaleCurve();
          mat.ColorRamp ??= GetAlphaOnlyRamp();
        }

        p.Finished += OnEmitterFinished;
      }

      _remainingEmitters = _emitters.Count;
      CallDeferred(nameof(StartEmission));
    }

    private void StartEmission()
    {
      foreach (var p in _emitters)
      {
        if (IsInstanceValid(p))
        {
          p.Emitting = true;
          p.Restart();
        }
      }
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

    private static GradientTexture1D GetAlphaOnlyRamp()
    {
      if (_alphaOnlyRamp != null)
        return _alphaOnlyRamp;

      var gradient = new Gradient();
      gradient.AddPoint(0f, new Color(1, 1, 1, 0));
      gradient.AddPoint(0.08f, new Color(1, 1, 1, 1));
      gradient.AddPoint(0.85f, new Color(1, 1, 1, 1));
      gradient.AddPoint(1f, new Color(1, 1, 1, 0));

      _alphaOnlyRamp = new GradientTexture1D { Gradient = gradient };
      return _alphaOnlyRamp;
    }

    private void OnEmitterFinished()
    {
      _remainingEmitters = Math.Max(0, _remainingEmitters - 1);
      if (_remainingEmitters == 0)
        QueueFree();
    }
  }
}
