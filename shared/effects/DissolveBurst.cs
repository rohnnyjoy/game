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
    private const int ParticleCount = 48; // Lowered for perf; evenly divisible across 6 faces
#if DEBUG
    // Slow particle burst in Debug to match slow-mo dissolve
    private const float LifetimeSeconds = 1.0f;
#else
    private const float LifetimeSeconds = 0.27f;
#endif
    private const float MinScaleRatio = 0.22f;
    private const float MaxScaleRatio = 0.62f;
    private const float MinSpeedRatio = 0.12f;
    private const float MaxSpeedRatio = 0.36f;
    // Performance knob: number of distinct colors cloned per face.
    // Keep small to reduce node/particle overhead while preserving good mixing.
    private const int MaxColorsPerFace = 2;

    private static PackedScene? _cachedScene;
    private static GradientTexture1D? _constantAlphaRamp;

    private readonly List<GpuParticles3D> _emitters = new();
    private int _remainingEmitters;
    // Reserved for future use if we switch to directed emission points
    // private static Vector3[] _cachedShellPoints = System.Array.Empty<Vector3>();
    // private static Vector3[] _cachedShellNormals = System.Array.Empty<Vector3>();
    // private static Vector3 _cachedShellExtents = Vector3.Zero;

    public static void Spawn(Node parent, Transform3D transform, IReadOnlyList<Color>? palette, Vector3 halfExtents, float dissolveDuration)
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
      instance.Configure(palette, halfExtents, dissolveDuration);
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

    private void Configure(IReadOnlyList<Color>? palette, Vector3 halfExtents, float dissolveDuration)
    {
      if (_emitters.Count == 0) return;

      // Treat any pre-authored child particle systems as color templates; we will clone them per face
      var colorTemplates = new List<GpuParticles3D>(_emitters);
      _emitters.Clear();

      Vector3 extents = new Vector3(Mathf.Max(halfExtents.X, 0.35f), Mathf.Max(halfExtents.Y, 0.2f), Mathf.Max(halfExtents.Z, 0.35f));
      float maxExtent = Mathf.Max(extents.X, Mathf.Max(extents.Y, extents.Z));
      float d = Mathf.Max(0.1f, dissolveDuration);
#if DEBUG
      // Slow down particle window in Debug to aid observation
      d *= 3.5f;
#endif
      float particleLifetime = 0.7f * d; // Balatro: lifespan = 0.7 * dissolve_time

      // We want one emission area per face and evenly mix all palette colors on each face.
      int faceCount = 6;
      int paletteCount = palette != null && palette.Count > 0 ? palette.Count : Math.Max(1, colorTemplates.Count);
      int perFaceAmount = Math.Max(1, ParticleCount / Math.Max(1, faceCount));

      // Define faces: outward normal, local offset to face plane, and thin box extents (shell thickness on normal)
      float thickness = Mathf.Max(0.05f, maxExtent * 0.06f);
      var faces = new (Vector3 normal, Vector3 offset, Vector3 boxExtents)[]
      {
        (Vector3.Right,   new Vector3( extents.X, 0, 0), new Vector3(thickness,     extents.Y, extents.Z)),
        (Vector3.Left,    new Vector3(-extents.X, 0, 0), new Vector3(thickness,     extents.Y, extents.Z)),
        (Vector3.Up,      new Vector3(0,  extents.Y, 0), new Vector3(extents.X,     thickness,  extents.Z)),
        (Vector3.Down,    new Vector3(0, -extents.Y, 0), new Vector3(extents.X,     thickness,  extents.Z)),
        (Vector3.Back,    new Vector3(0, 0,  extents.Z), new Vector3(extents.X,     extents.Y,  thickness )),
        (Vector3.Forward, new Vector3(0, 0, -extents.Z), new Vector3(extents.X,     extents.Y,  thickness ))
      };

      // Build emitters: for each face, clone one template per color and position it on that face
      for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
      {
        // Select a subset of colors for this face to limit emitter count while maintaining variety.
        int colorsForFace = Math.Min(MaxColorsPerFace, Math.Max(1, paletteCount));
        int stride = 2; // good spread for odd paletteCounts like 5; OK for others too
        for (int k = 0; k < colorsForFace; k++)
        {
          int paletteIndex = ((faceIndex + k * stride) % Math.Max(1, paletteCount));
          var template = colorTemplates[k % Math.Max(1, colorTemplates.Count)];
          var clone = template.Duplicate() as GpuParticles3D;
          if (clone == null) continue;

          clone.Name = $"Particles_face{faceIndex}_c{paletteIndex}";
          clone.Emitting = false;
          clone.OneShot = true;
          clone.Explosiveness = 0.0f; // continuous emission over lifetime
          clone.Preprocess = 0f;
          clone.Lifetime = Mathf.Max(0.05f, particleLifetime);
          clone.DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth;
          clone.VisibilityAabb = new Aabb(Vector3.Zero, extents * 4f);
          // Mild per-emitter randomness for variety
          clone.Randomness = MathF.Max(clone.Randomness, 0.33f);

          // Position on face plane
          var t = clone.Transform;
          t.Origin = faces[faceIndex].offset;
          clone.Transform = t;

          // Configure process material
          if (clone.ProcessMaterial is ParticleProcessMaterial srcMat)
          {
            var mat = srcMat.Duplicate() as ParticleProcessMaterial ?? new ParticleProcessMaterial();

            // Thin shell box on the face surface with outward direction and decent spread
            mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            mat.EmissionBoxExtents = faces[faceIndex].boxExtents;
            mat.Direction = faces[faceIndex].normal;
            if (mat.Spread < 30f) mat.Spread = 45f;

            // Provide velocity defaults if unset (keeps authored values when present)
            bool hasVelocity = mat.InitialVelocityMin > 0.0001f || mat.InitialVelocityMax > 0.0001f;
            if (!hasVelocity)
            {
              mat.InitialVelocityMin = maxExtent * MinSpeedRatio;
              mat.InitialVelocityMax = maxExtent * MaxSpeedRatio;
            }

            // Respect scene scale; fallback ratios if not set
            bool hasScale = mat.ScaleMin > 0.0001f || mat.ScaleMax > 0.0001f;
            if (!hasScale)
            {
              mat.ScaleMin = maxExtent * MinScaleRatio;
              mat.ScaleMax = maxExtent * MaxScaleRatio;
            }

            // Apply palette color if provided; otherwise keep template color
            if (palette != null && palette.Count > 0)
            {
              mat.Color = palette[paletteIndex % palette.Count];
            }

            clone.ProcessMaterial = mat;
          }

          // Amount distribution (add 1 to the first 'remainder' colors to account for division remainder)
          int baseAmount = Math.Max(0, perFaceAmount / Math.Max(1, colorsForFace));
          int remainder = perFaceAmount - baseAmount * colorsForFace;
          int amount = baseAmount + (k < remainder ? 1 : 0);
          clone.Amount = Math.Max(0, amount);

          AddChild(clone);
          _emitters.Add(clone);
        }
      }

      // Hide/disable template particles (they were only used for cloning material settings)
      foreach (var t in colorTemplates)
      {
        if (!IsInstanceValid(t)) continue;
        t.Emitting = false;
        t.Amount = 0;
        t.Visible = false;
      }

      foreach (var p in _emitters)
      {
        p.Finished += OnEmitterFinished;
      }

      _remainingEmitters = _emitters.Count;
      CallDeferred(nameof(StartEmission));
    }

    // Surface emission shell generation omitted for now (requires Godot API with EmissionPoints/Normals).

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

    private static GradientTexture1D GetConstantAlphaRamp()
    {
      if (_constantAlphaRamp != null)
        return _constantAlphaRamp;

      var gradient = new Gradient();
      // Constant alpha across particle lifetime; global fade handled by effect timing
      gradient.AddPoint(0f, new Color(1, 1, 1, 1));
      gradient.AddPoint(1f, new Color(1, 1, 1, 1));

      _constantAlphaRamp = new GradientTexture1D { Gradient = gradient };
      return _constantAlphaRamp;
    }

    private void OnEmitterFinished()
    {
      _remainingEmitters = Math.Max(0, _remainingEmitters - 1);
      if (_remainingEmitters == 0)
        QueueFree();
    }
  }
}
