# HomingComponent.gd
extends Node
class_name HomingComponent

var bullet: Bullet
var homing_radius: float = 10.0
var tracking_strength: float = 0.1

func _physics_process(_delta: float) -> void:
    if not bullet or not bullet.is_inside_tree():
        queue_free()
        return

    # Find the closest enemy in the "enemies" group within the homing radius.
    var closest_enemy = null
    var closest_distance = homing_radius
    for enemy in get_tree().get_nodes_in_group("enemies"):
        print(enemy)
        # Ensure the enemy is a Node3D so we can access its global_position.
        if enemy is Node3D:
            print(enemy)
            var enemy_position: Vector3 = enemy.global_position
            var distance: float = bullet.global_position.distance_to(enemy_position)
            if distance < closest_distance:
                closest_distance = distance
                closest_enemy = enemy

    # If an enemy was found, adjust the bullet's velocity.
    if closest_enemy:
        var target_direction: Vector3 = (closest_enemy.global_position - bullet.global_position).normalized()
        var desired_velocity: Vector3 = target_direction * bullet.speed
        bullet.velocity = bullet.velocity.lerp(desired_velocity, tracking_strength)
