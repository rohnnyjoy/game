using Godot;
using System;

public partial class CameraShake : Node3D
{
  private Vector3 originalPosition;

  // Current offset applied to the camera's position.
  private Vector3 currentShakeOffset = Vector3.Zero;
  // Legacy random jitter removed; use Balatro-style sinusoidal shake exclusively.

  [Export] public bool FollowGameUI = true;
  // Rough mapping from pixels (UI offset) to local meters on camera rig.
  [Export] public float PixelsToMetersScale = 0.0035f;

  // Balatro-style screen shake parameters
  [Export(PropertyHint.Range, "0,100,1")] public int ScreenShakeSetting = 65;
  [Export] public bool ReducedMotion = false;
  // Global multiplier to quickly tune overall shake strength
  [Export] public float PulseScale = 1.0f;

  // Accumulated pulse energy; decays each tick
  private float jiggle = 0f;

  public override void _Ready()
  {
    originalPosition = Transform.Origin;
    SetPhysicsProcess(true);
  }

  // Trigger the shake: map duration/intensity to a Balatro-style jiggle pulse.
  public void TriggerShake(float duration, float intensity)
  {
    // Convert to a stronger pulse; primarily scale by intensity then duration
    // Typical calls (weapon/impact/explosion) produce a noticeable nudge now.
    float add = Mathf.Clamp((intensity * 1.5f + duration * 0.8f) * Mathf.Max(PulseScale, 0.01f), 0.05f, 2.5f);
    jiggle += add;
  }

  public override void _PhysicsProcess(double delta)
  {
    // If full-frame overlay is active, let it own the shake. Keep camera steady.
    if (GameUI.Instance != null && GameUI.Instance.UseFullFrameShake)
    {
      currentShakeOffset = currentShakeOffset.Lerp(Vector3.Zero, 0.8f);
      if (currentShakeOffset.LengthSquared() > 1e-8f)
      {
        Transform3D t0 = Transform;
        t0.Origin = originalPosition + currentShakeOffset;
        Transform = t0;
      }
      else
      {
        Transform3D t0 = Transform;
        t0.Origin = originalPosition;
        Transform = t0;
      }
      return;
    }
    // Prefer following GameUI's shared shake so world and UI move together
    bool appliedShared = false;
    if (FollowGameUI && GameUI.Instance != null && GameUI.Instance.EnableUiShake)
    {
      Vector2 px = GameUI.Instance.GetScreenShakeOffset();
      if (px.LengthSquared() > 0.000001f)
      {
        Vector3 uiTarget = new Vector3(px.X, -px.Y, 0f) * PixelsToMetersScale;
        currentShakeOffset = currentShakeOffset.Lerp(uiTarget, 0.8f);
        appliedShared = true;
      }
    }

    if (!appliedShared)
    {
      // Balatro-equivalent easing/timing
      float dt = (float)delta;
      jiggle = Mathf.Max(0f, jiggle * (1f - 5f * dt));
      float setting = Mathf.Clamp(ScreenShakeSetting, 0, 100);
      float baseStrength = (ReducedMotion ? 0f : 1f) * (setting / 100f) * 3f;
      float amp = baseStrength * Mathf.Clamp(jiggle, 0f, 1f);
      if (amp < 0.0005f)
      {
        currentShakeOffset = currentShakeOffset.Lerp(Vector3.Zero, 0.8f);
      }
      else
      {
        float timeSec = (float)Time.GetTicksMsec() / 1000f;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float S = Mathf.Min(vp.X, vp.Y);
        float offsetX = amp * (0.015f * Mathf.Sin(0.913f * timeSec) + 0.01f * Mathf.Sin(19.913f * timeSec));
        float offsetY = amp * (0.015f * Mathf.Sin(0.952f * timeSec) + 0.01f * Mathf.Sin(21.913f * timeSec));
        Vector2 px = new Vector2(offsetX * S, offsetY * S);
        Vector3 target = new Vector3(px.X, -px.Y, 0f) * PixelsToMetersScale;
        currentShakeOffset = currentShakeOffset.Lerp(target, 0.8f);
      }
    }

    // If there is effectively no offset, avoid rewriting the transform.
    if (currentShakeOffset.LengthSquared() < 1e-8f)
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
