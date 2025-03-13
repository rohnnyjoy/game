using Godot;
using System;
using System.Threading.Tasks;

public partial class ScatterModule : WeaponModule
{
  [Export]
  public int DuplicationCount { get; set; } = 5; // Total bullets (original + duplicates)

  [Export]
  public float BulletDamageFactor { get; set; } = 1 / 5;

  // Spread angle in radians. Bullets will scatter randomly within ±SpreadAngle/2 both horizontally and vertically.
  [Export]
  public float SpreadAngle { get; set; } = Mathf.DegToRad(15.0f);

  public ScatterModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/shotgun.png");
    ModuleDescription = "Duplicates bullet in a scatter pattern with configurable horizontal and vertical spread.";
    Rarity = Rarity.Rare;
  }

  public override async Task OnFire(Bullet bullet)
  {
    if (!IsInstanceValid(bullet))
      return;

    // Get the parent so we can add duplicated bullets to the scene.
    Node parent = bullet.GetParent();
    if (parent == null)
      return;

    // Create a random number generator and randomize it.
    RandomNumberGenerator rng = new RandomNumberGenerator();
    rng.Randomize();

    // Base direction from the bullet's current velocity.
    Vector3 baseDirection = bullet.Velocity.Normalized();

    // Prepare a basis for applying both horizontal (yaw) and vertical (pitch) rotations.
    // Compute a right vector by crossing with a candidate up vector.
    Vector3 forward = baseDirection;
    Vector3 right = forward.Cross(Vector3.Up).Normalized();
    if (right == Vector3.Zero)
    {
      // Fallback if the base direction is vertical.
      right = Vector3.Right;
    }
    Vector3 up = right.Cross(forward).Normalized();

    // Duplicate the bullet into multiple copies.
    // The first iteration reuses the original bullet; the rest are new duplicates.
    for (int i = 0; i < DuplicationCount; i++)
    {
      // Compute random yaw and pitch offsets within [-SpreadAngle/2, SpreadAngle/2].
      float yawOffset = rng.RandfRange(-SpreadAngle / 2, SpreadAngle / 2);
      float pitchOffset = rng.RandfRange(-SpreadAngle / 2, SpreadAngle / 2);

      // Apply the yaw (rotation around the 'up' axis) and pitch (rotation around the 'right' axis).
      Vector3 newDirection = forward.Rotated(up, yawOffset).Rotated(right, pitchOffset).Normalized();

      Bullet currentBullet;

      if (i == 0)
      {
        // Use the original bullet.
        currentBullet = bullet;
      }
      else
      {
        // Duplicate the bullet using flags that duplicate signals, instancing, and groups.
        // Using 7 as a bitmask (1 | 2 | 4).
        currentBullet = bullet.Duplicate(7) as Bullet;
        currentBullet.Damage *= BulletDamageFactor;
        if (currentBullet == null)
        {
          GD.PrintErr("Failed to duplicate bullet");
          continue;
        }
        // Add the new bullet to the parent and sync its transform with the original.
        parent.AddChild(currentBullet);
        currentBullet.GlobalTransform = bullet.GlobalTransform;
      }

      // Update the bullet's velocity with the new direction while preserving its speed.
      currentBullet.Velocity = newDirection * currentBullet.Speed;
    }

    await Task.CompletedTask;
  }
}
