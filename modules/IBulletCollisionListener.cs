using Godot;

/// <summary>
/// Implemented by weapon modules that want to observe bullet collisions.
/// Enemy hits can modify the outgoing damage; non-enemy collisions (or misses) are
/// reported with a null enemy reference.
/// </summary>
public interface IBulletCollisionListener
{
  /// <summary>
  /// Called whenever a bullet fired from the weapon collides with something.
  /// </summary>
  /// <param name="weapon">Weapon that spawned the bullet.</param>
  /// <param name="enemy">Enemy that was hit, or null if the collision was not an enemy.</param>
  /// <param name="enemyId">Instance id of the enemy, or 0 when <paramref name="enemy"/> is null.</param>
  /// <param name="damage">Damage about to be applied (or most recent damage when enemy is null).</param>
  /// <returns>The damage value to continue with. For non-enemy collisions, return <paramref name="damage"/>.</returns>
  float OnBulletCollision(Weapon weapon, Node3D enemy, ulong enemyId, float damage);
}
