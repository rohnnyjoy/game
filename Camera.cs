using Godot;
using System;

public partial class Camera : Camera3D
{
  private Vector3 originalLocalPosition;
  private float shakeDuration = 0;
  private float shakeIntensity = 0;

  public override void _Ready()
  {
    // Store the original local position (relative to the parent)
    originalLocalPosition = Transform.Origin;
  }

  public override void _Process(double delta)
  {
    // Get the current transform for its basis.
    Transform3D currentTransform = Transform;

    if (shakeDuration > 0)
    {
      shakeDuration -= (float)delta;
      // Generate a random offset for the shake effect.
      Vector3 offset = new Vector3(
          (float)GD.RandRange(-shakeIntensity, shakeIntensity),
          (float)GD.RandRange(-shakeIntensity, shakeIntensity),
          (float)GD.RandRange(-shakeIntensity, shakeIntensity)
      );
      // Update the local transform using Transform3D.
      Transform = new Transform3D(currentTransform.Basis, originalLocalPosition + offset);
    }
    else
    {
      // Reset the camera's local position.
      Transform = new Transform3D(currentTransform.Basis, originalLocalPosition);
    }
  }

  public void TriggerShake(float duration, float intensity)
  {
    shakeDuration = duration;
    shakeIntensity = intensity;
  }
}
