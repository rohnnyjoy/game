using Godot;
using System;
using System.Collections.Generic;

public class AirLurchManager
{
  private const float LURCH_SPEED = 10.0f;
  private const float LURCH_SPEED_LOSS = 0.2f;
  private const float CONE_HALF_ANGLE = Mathf.Pi / 4;  // Loosen angle restriction
  private const float LURCH_DURATION = 15.0f;          // Duration in seconds

  private List<float> usedConeAngles = new();
  private double lurchEndTime;
  private Vector2 airInitialDir;

  public AirLurchManager(Vector2 initialDirection)
  {
    Reset(initialDirection);
  }

  public void Reset(Vector2 initialDirection)
  {
    airInitialDir = initialDirection.Normalized();
    usedConeAngles.Clear();
    usedConeAngles.Add(airInitialDir.Angle());
    lurchEndTime = Time.GetUnixTimeFromSystem() + LURCH_DURATION;
  }

  private float AngleDifference(float angleA, float angleB)
  {
    return Mathf.Atan2(Mathf.Sin(angleA - angleB), Mathf.Cos(angleA - angleB));
  }

  private bool CanLurch(Vector2 inputDirection)
  {
    if (Time.GetUnixTimeFromSystem() > lurchEndTime)
      return false;

    if (inputDirection.Length() < 0.1f)
      return false;

    float inputAngle = inputDirection.Angle();
    foreach (float usedAngle in usedConeAngles)
    {
      float diff = Mathf.Abs(AngleDifference(inputAngle, usedAngle));
      if (diff < CONE_HALF_ANGLE)
        return false;
    }

    return true;
  }

  private void MarkLurchUsed(Vector2 inputDirection)
  {
    usedConeAngles.Add(inputDirection.Angle());
  }

  public Vector2 PerformLurch(Vector2 currentVel, Vector2 inputDirection)
  {
    if (!CanLurch(inputDirection))
      return currentVel;

    Vector2 lurchVector = inputDirection.Normalized() * LURCH_SPEED;
    Vector2 newVel = currentVel + lurchVector;
    float newSpeed = newVel.Length() * (1.0f - LURCH_SPEED_LOSS);
    newVel = newVel.Normalized() * newSpeed;

    MarkLurchUsed(inputDirection);

    return newVel;
  }
}
