using Godot;
using System;

public partial class CollisionParticle : Node3D
{
  // Total time before the particle is removed.
  [Export] public float Lifetime { get; set; } = 2f;
  // Initial speed for movement.
  [Export] public float Speed { get; set; } = 50.0f;
  [Export] public Vector3 InitialDirection { get; set; } = Vector3.Forward;

  // Movement deceleration factor (higher values slow the particle faster).
  [Export] public float DecelerationFactor { get; set; } = 8.0f;

  // Fade-out starts at this time (in seconds).
  [Export] public float FadeStartTime { get; set; } = 0f;
  // Fade-out lasts for this duration (in seconds).
  [Export] public float FadeDuration { get; set; } = 0.2f;
  // Fade exponent (2 for quadratic fade-out).
  [Export] public float FadeExponent { get; set; } = 1.0f;
  [Export] public float Gravity { get; set; } = 9.8f;

  private float _timeElapsed = 0.0f;
  private MeshInstance3D _particleMeshInstance;
  private StandardMaterial3D _particleMaterial;
  private Color _baseParticleColor;

  public override void _Ready()
  {
    // Process early.
    SetProcessPriority(-1);

    // Apply a random rotation.
    Rotation = new Vector3(
        (float)GD.Randf() * Mathf.Tau,
        (float)GD.Randf() * Mathf.Tau,
        (float)GD.Randf() * Mathf.Tau
    );

    // Create a cube mesh.
    _particleMeshInstance = new MeshInstance3D();
    BoxMesh boxMesh = new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) };
    _particleMeshInstance.Mesh = boxMesh;

    // Increase size diversity: random scale between 0.5 and 1.5.
    float scaleFactor = 0.5f + (float)GD.Randf() * 1.0f;
    _particleMeshInstance.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

    // Create an unshaded material with alpha transparency enabled.
    _particleMaterial = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha
    };

    // Choose a random color between dark orange and light yellow.
    Color darkOrange = new Color(1.0f, 0.5f, 0.0f);
    Color lightYellow = new Color(1.0f, 1.0f, 0.8f);
    float t = (float)GD.Randf();
    _baseParticleColor = darkOrange.Lerp(lightYellow, t);
    _particleMaterial.AlbedoColor = _baseParticleColor;

    _particleMeshInstance.MaterialOverride = _particleMaterial;
    AddChild(_particleMeshInstance);
  }

  public override void _PhysicsProcess(double delta)
  {
    float dt = (float)delta;
    _timeElapsed += dt;

    // Calculate movement deceleration (exponential decay).
    float currentSpeed = Speed * Mathf.Exp(-DecelerationFactor * _timeElapsed);
    Translate(InitialDirection.Normalized() * currentSpeed * dt);

    // Fade-out calculation:
    // If before FadeStartTime, alpha remains 1.
    float alpha = 1.0f;
    if (_timeElapsed >= FadeStartTime)
    {
      float fadeTime = _timeElapsed - FadeStartTime;
      // Clamp the fraction of fade progress between 0 and 1.
      float fadeProgress = Mathf.Clamp(fadeTime / FadeDuration, 0.0f, 1.0f);
      // Quadratic fade-out (or use FadeExponent to change the curve).
      alpha = 1.0f - Mathf.Pow(fadeProgress, FadeExponent);
    }

    _particleMaterial.AlbedoColor = new Color(_baseParticleColor.R, _baseParticleColor.G, _baseParticleColor.B, alpha);

    Translate(Vector3.Down * Gravity * dt);

    if (_timeElapsed >= Lifetime)
    {
      QueueFree();
    }
  }
}
