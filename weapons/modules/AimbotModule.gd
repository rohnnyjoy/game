extends WeaponModule
class_name AimbotModule

@export var aim_cone_angle: float = deg_to_rad(90) # Maximum allowed angle from bullet's current direction.
@export var vertical_offset: float = 0.0 # Vertical adjustment (0 aims at the enemy's center).

func on_collision(_collision: Dictionary, bullet: Bullet) -> void:
    aimbot(bullet)

func on_fire(bullet: Bullet) -> void:
    aimbot(bullet)

func aimbot(bullet: Bullet) -> void:
    # This function is called after all modify_bullet calls, in the module order.
    # Use the bullet's current velocity as the base direction.
    var origin: Vector3 = bullet.global_position
    var base_direction: Vector3 = bullet.velocity.normalized()
    var best_enemy: Node = null
    var best_angle: float = aim_cone_angle
    
    # Look for the enemy that lies within the cone defined by aim_cone_angle.
    for enemy in bullet.get_tree().get_nodes_in_group("enemies"):
        if enemy is Node3D:
            var to_enemy: Vector3 = (enemy.global_position - origin).normalized()
            var angle: float = acos(base_direction.dot(to_enemy))
            if angle < best_angle:
                best_angle = angle
                best_enemy = enemy
    
    # If an enemy was found, adjust the bullet's velocity.
    if best_enemy:
        print("HOMING")
        var aim_point: Vector3
        # If available, use the enemy's AABB center for a more accurate target.
        if best_enemy.has_method("get_aabb"):
            var aabb: AABB = best_enemy.get_aabb()
            aim_point = best_enemy.to_global(aabb.position + aabb.size * 0.5)
        else:
            aim_point = best_enemy.global_position
        # Apply vertical offset if needed.
        aim_point.y += vertical_offset
        
        bullet.velocity = (aim_point - origin).normalized() * bullet.speed
