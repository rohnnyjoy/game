using Godot;
using System;

public partial class CameraShake : Node3D
{
  private Vector3 originalPosition;
  private float shakeDuration = 0;
  private float shakeIntensity = 0;

  // Current offset applied to the camera's position.
  private Vector3 currentShakeOffset = Vector3.Zero;
  // A target offset value that is updated randomly.
  private Vector3 targetShakeOffset = Vector3.Zero;

  // Use a random number generator for non-deterministic shake.
  private RandomNumberGenerator rng = new RandomNumberGenerator();

  public override void _Ready()
  {
    originalPosition = Transform.Origin;
    rng.Randomize();
    SetPhysicsProcess(true);
  }

  // Trigger the shake by setting the duration and intensity.
  public void TriggerShake(float duration, float intensity)
  {
    shakeDuration = duration;
    shakeIntensity = intensity;
  }

  public override void _PhysicsProcess(double delta)
  {
    if (shakeDuration > 0)
    {
      // Reduce the shake duration.
      shakeDuration -= (float)delta;

      // Pick a new random target offset for this frame.
      // Only X and Y are shaken; Z remains 0.
      targetShakeOffset = new Vector3(
          rng.RandfRange(-shakeIntensity, shakeIntensity),
          rng.RandfRange(-shakeIntensity, shakeIntensity),
          0
      );

      // Smoothly interpolate the current offset toward the target offset.
      // Adjust the factor (here 0.8f) to control the smoothness.
      currentShakeOffset = currentShakeOffset.Lerp(targetShakeOffset, 0.8f);
    }
    else
    {
      // When shaking is done, ease the offset back to zero.
      currentShakeOffset = currentShakeOffset.Lerp(Vector3.Zero, 0.8f);
    }

    // If there is effectively no offset and no active shake, avoid rewriting the transform.
    if (shakeDuration <= 0 && currentShakeOffset.LengthSquared() < 1e-8f)
    {
      return;
    }

    // Apply the offset to the stored original position,
    // ensuring the camera returns to exactly the same spot.
    Transform3D t = Transform;
    t.Origin = originalPosition + currentShakeOffset;
    Transform = t;
  }
}
