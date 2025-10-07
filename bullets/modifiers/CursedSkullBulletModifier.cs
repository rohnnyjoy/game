using Godot;
using System.Threading.Tasks;

public partial class CursedSkullBulletModifier : BulletModifier
{
  [Export] public float TransferRadius { get; set; } = 8.0f;

  public override Task OnCollision(Bullet bullet, Bullet.CollisionData collision)
  {
    if (bullet == null || collision == null)
      return Task.CompletedTask;

    Node3D collider = collision.Collider;
    if (collider == null || !collider.IsInGroup("enemies"))
      return Task.CompletedTask;

    if (collider is Enemy enemy && GodotObject.IsInstanceValid(enemy))
    {
      float healthBefore = enemy.CurrentHealth;
      // Approximate damage that will be applied by default collision logic
      float incoming = bullet.Damage + collision.TotalDamageDealt;
      float leftover = incoming - healthBefore;
      if (leftover > 0.0f)
      {
        // Find nearest other enemy within radius
        Node3D nearest = null;
        float best = TransferRadius;
        foreach (Node n in bullet.GetTree().GetNodesInGroup("enemies"))
        {
          if (n is not Node3D e) continue;
          if (!GodotObject.IsInstanceValid(e)) continue;
          if (e == collider) continue;
          float d = e.GlobalTransform.Origin.DistanceTo(enemy.GlobalTransform.Origin);
          if (d < best)
          {
            best = d;
            nearest = e;
          }
        }

        if (nearest != null && GodotObject.IsInstanceValid(nearest))
        {
          try
          {
            if (nearest is Enemy en)
              en.TakeDamage(leftover);
            else
              nearest.CallDeferred("take_damage", leftover);

            // Visual number and knockback toward the same direction as this bullet
            FloatingNumber3D.Spawn(bullet, nearest, leftover);
            Vector3 dir = bullet.Velocity.LengthSquared() > 0.000001f ? bullet.Velocity.Normalized() : Vector3.Forward;
            dir = new Vector3(dir.X, 0.15f * dir.Y, dir.Z).Normalized();
            GlobalEvents.Instance?.EmitDamageDealt(nearest, leftover, dir * Mathf.Max(0.0f, bullet.KnockbackStrength));
          }
          catch { }
        }
      }
    }
    return Task.CompletedTask;
  }
}

