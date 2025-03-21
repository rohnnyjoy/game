using Godot;
using System;
using System.Threading.Tasks;
using Godot.Collections;

public partial class ScatterBulletModifier : BulletModifier
{
  [Export]
  public int DuplicationCount { get; set; } = 5; // Total bullets (original + duplicates)

  [Export]
  public float BulletDamageFactor { get; set; } = 1.0f / 5.0f; // Use float division

  // Spread angle in radians. Bullets will scatter randomly within ±SpreadAngle/2 both horizontally and vertically.
  [Export]
  public float SpreadAngle { get; set; } = Mathf.DegToRad(15.0f);

  public override async Task OnFire(Bullet bullet)
  {
    GD.Print("ScatterBulletModifier OnFire");
    if (!IsInstanceValid(bullet))
      return;

    // Check if this bullet has already been scattered.
    if (bullet.HasMeta("has_scattered"))
      return;
    bullet.SetMeta("has_scattered", true);

    // Get the parent so we can add duplicated bullets to the scene.
    Node parent = bullet.GetParent();
    if (parent == null)
      return;

    // Create and randomize a new RNG.
    RandomNumberGenerator rng = new RandomNumberGenerator();
    rng.Randomize();

    // Base direction from the bullet's current velocity.
    Vector3 baseDirection = bullet.Velocity.Normalized();

    // Prepare a basis for applying both horizontal (yaw) and vertical (pitch) rotations.
    Vector3 forward = baseDirection;
    Vector3 right = forward.Cross(Vector3.Up).Normalized();
    if (right == Vector3.Zero)
    {
      // Fallback if the base direction is vertical.
      right = Vector3.Right;
    }
    Vector3 up = right.Cross(forward).Normalized();

    // Duplicate the bullet into multiple copies.
    for (int i = 0; i < DuplicationCount; i++)
    {
      // Compute random yaw and pitch offsets within [-SpreadAngle/2, SpreadAngle/2].
      float yawOffset = rng.RandfRange(-SpreadAngle / 2, SpreadAngle / 2);
      float pitchOffset = rng.RandfRange(-SpreadAngle / 2, SpreadAngle / 2);

      // Apply yaw (rotation around the 'up' axis) and pitch (rotation around the 'right' axis).
      Vector3 newDirection = forward.Rotated(up, yawOffset).Rotated(right, pitchOffset).Normalized();

      Bullet currentBullet;
      if (i == 0)
      {
        // Use the original bullet for the first iteration.
        currentBullet = bullet;
      }
      else
      {
        // Duplicate the bullet using a bitmask that omits signals.
        // Using 6 (which is 2 | 4) instead of 7 (1 | 2 | 4) prevents duplicating signals.
        currentBullet = bullet.Duplicate(6) as Bullet;
        if (currentBullet == null)
        {
          GD.PrintErr("Failed to duplicate bullet");
          continue;
        }
        // Mark the duplicate so it doesn't scatter again.
        currentBullet.SetMeta("has_scattered", true);

        // Scale down damage for duplicate bullets.
        currentBullet.Damage *= BulletDamageFactor;
        // Add the new bullet to the parent and match its transform with the original.
        parent.AddChild(currentBullet);
        currentBullet.GlobalTransform = bullet.GlobalTransform;
      }

      // Update the bullet's velocity with the new direction while preserving its speed.
      currentBullet.Velocity = newDirection * currentBullet.Speed;
    }

    await Task.CompletedTask;
  }
}
