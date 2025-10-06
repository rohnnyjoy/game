using System.Threading.Tasks;
using Godot;

public partial class HomingBulletModifier : BulletModifier
{
  [Export]
  public float HomingRadius { get; set; } = 10.0f;

  [Export]
  public float TrackingStrength { get; set; } = 0.04f; // How quickly the bullet turns; 0.0 to 1.0

  public override async Task OnUpdate(Bullet bullet, float delta)
  {
    if (bullet == null || !bullet.IsInsideTree() || !IsInstanceValid(bullet))
    {
      return;
    }

    if (bullet.HasMeta("is_sticky") && (bool)bullet.GetMeta("is_sticky"))
    {
      return;
    }

    Node closestEnemy = null;
    float closestDistance = HomingRadius;

    // Iterate through all nodes in the "enemies" group.
    foreach (Node enemy in bullet.GetTree().GetNodesInGroup("enemies"))
    {
      // Ensure the enemy is a Node3D so we can access its GlobalPosition.
      if (enemy is Node3D enemyNode)
      {
        float distance = bullet.GlobalPosition.DistanceTo(enemyNode.GlobalPosition);
        if (distance < closestDistance)
        {
          closestDistance = distance;
          closestEnemy = enemy;
        }
      }
    }

    // If an enemy was found, adjust the bullet's velocity.
    if (closestEnemy is Node3D enemy3D)
    {
      Vector3 targetDirection = (enemy3D.GlobalPosition - bullet.GlobalPosition).Normalized();
      Vector3 desiredVelocity = targetDirection * bullet.Speed;
      bullet.Velocity = bullet.Velocity.Lerp(desiredVelocity, TrackingStrength);
    }
    await Task.CompletedTask;
    return;
  }
}
