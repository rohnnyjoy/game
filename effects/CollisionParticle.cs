using Godot;
using System;

public partial class CollisionParticle : Node3D
{
  [Export] public float Lifetime { get; set; } = 2f;
  [Export] public float Speed { get; set; } = 30.0f;
  [Export] public Vector3 InitialDirection { get; set; } = Vector3.Forward;
  [Export] public float DecelerationFactor { get; set; } = 8.0f;
  [Export] public float FadeStartTime { get; set; } = 0.1f;
  [Export] public float FadeDuration { get; set; } = 0.1f;
  [Export] public float FadeExponent { get; set; } = 1.0f;
  [Export] public float Gravity { get; set; } = 9.8f;

  private float _timeElapsed = 0.0f;
  private float _verticalVelocity = 0.0f; // Accumulates gravity effect.
  private MeshInstance3D _particleMeshInstance;
  private StandardMaterial3D _particleMaterial;
  private Color _baseParticleColor;
  private Vector3 _normalizedDirection;

  public override void _Ready()
  {
    // Apply a random rotation for visual variation.
    Rotation = new Vector3(
        (float)GD.Randf() * Mathf.Tau,
        (float)GD.Randf() * Mathf.Tau,
        (float)GD.Randf() * Mathf.Tau
    );

    // Create a cube mesh.
    _particleMeshInstance = new MeshInstance3D();

    SphereMesh sphereMesh = new SphereMesh
    {
      Radius = 0.1f,
      Height = 0.2f,
      RadialSegments = 4,
      Rings = 2,
    };
    _particleMeshInstance.Mesh = sphereMesh;

    float scaleFactor = (float)GD.Randf() * 2;
    _particleMeshInstance.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

    // Create an unshaded material with alpha transparency enabled.
    _particleMaterial = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha
    };

    // Choose a random color between dark orange and light yellow.
    Color darkOrange = new Color(1.0f, 0.6f, 0.0f);
    Color lightYellow = new Color(1.0f, 1.0f, 0.8f);
    float t = (float)GD.Randf();
    _baseParticleColor = darkOrange.Lerp(lightYellow, t);
    _particleMaterial.AlbedoColor = _baseParticleColor;

    _particleMeshInstance.MaterialOverride = _particleMaterial;
    AddChild(_particleMeshInstance);

    // Cache the normalized initial direction.
    _normalizedDirection = InitialDirection.Normalized();
  }

  public override void _PhysicsProcess(double delta)
  {
    float dt = (float)delta;
    _timeElapsed += dt;

    // Exponential decay for speed along the initial direction.
    float currentSpeed = Speed * Mathf.Exp(-DecelerationFactor * _timeElapsed);

    // Update vertical velocity with gravity (gravity is acceleration).
    _verticalVelocity += Gravity * dt;

    // Calculate translation as the sum of horizontal movement and vertical (gravity) movement.
    Vector3 translation = _normalizedDirection * currentSpeed * dt +
                          Vector3.Down * _verticalVelocity * dt;

    // Use global translation so that gravity remains in world space.
    GlobalTranslate(translation);

    // Fade-out calculation.
    float alpha = 1.0f;
    if (_timeElapsed >= FadeStartTime)
    {
      float fadeTime = _timeElapsed - FadeStartTime;
      float fadeProgress = Mathf.Clamp(fadeTime / FadeDuration, 0.0f, 1.0f);
      alpha = 1.0f - Mathf.Pow(fadeProgress, FadeExponent);
    }

    // Update the material's color (keeping RGB, updating alpha).
    _particleMaterial.AlbedoColor = new Color(_baseParticleColor.R, _baseParticleColor.G, _baseParticleColor.B, alpha);

    if (_timeElapsed >= Lifetime)
    {
      QueueFree();
    }
  }
}
