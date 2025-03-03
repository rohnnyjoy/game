extends WeaponModule
class_name AimbotModule

@export var aim_cone_angle: float = deg_to_rad(90) # Maximum allowed angle from bullet's current direction.
@export var vertical_offset: float = 0.0 # Vertical adjustment (0 aims at the enemy's center).

func on_collision(_collision: Dictionary, bullet: Bullet) -> void:
    aimbot(bullet)

func on_fire(bullet: Bullet) -> void:
    aimbot(bullet)

func aimbot(bullet: Bullet) -> void:
    # Use bullet's current velocity as the base direction
    var origin: Vector3 = bullet.global_position
    var base_direction: Vector3 = bullet.velocity.normalized()

    var best_enemy: Node = null
    var best_angle: float = aim_cone_angle

    # Retrieve the last enemy this bullet locked onto (if any).
    var last_locked_enemy: Node = null
    if bullet.has_meta("last_locked_enemy"):
        var stored_enemy = bullet.get_meta("last_locked_enemy")
        if is_instance_valid(stored_enemy):
            last_locked_enemy = stored_enemy
        else:
            # Clear out the invalid reference
            bullet.set_meta("last_locked_enemy", null)

    # Look for an enemy within the cone, skipping if it was already locked by this bullet
    for enemy in bullet.get_tree().get_nodes_in_group("enemies"):
        # skip the previously locked enemy
        if enemy == last_locked_enemy:
            continue

        if enemy is Node3D:
            var to_enemy: Vector3 = (enemy.global_position - origin).normalized()
            var angle: float = acos(base_direction.dot(to_enemy))
            if angle < best_angle:
                best_angle = angle
                best_enemy = enemy

    # If an enemy was found, adjust the bullet's velocity
    if best_enemy:
        var aim_point: Vector3
        if best_enemy.has_method("get_aabb"):
            var aabb: AABB = best_enemy.get_aabb()
            aim_point = best_enemy.to_global(aabb.position + aabb.size * 0.5)
        else:
            aim_point = best_enemy.global_position

        # Apply vertical offset
        aim_point.y += vertical_offset

        bullet.velocity = (aim_point - origin).normalized() * bullet.speed

        # Store this enemy as the last locked target
        bullet.set_meta("last_locked_enemy", best_enemy)
