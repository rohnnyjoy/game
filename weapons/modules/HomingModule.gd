extends WeaponModule
class_name HomingModule

@export var homing_radius: float = 10.0
@export var tracking_strength: float = 0.1 # How quickly the bullet turns; 0.0 to 1.0

func _init() -> void:
	card_texture = preload("res://icons/homing.png")
	module_description = "Attacks home in on the nearest enemy within 10m."

func on_physics_process(delta: float, bullet: Bullet) -> void:
	if not bullet or not bullet.is_inside_tree():
		queue_free()
		return

	# Find the closest enemy in the "enemies" group within the homing radius.
	var closest_enemy = null
	var closest_distance = homing_radius
	for enemy in bullet.get_tree().get_nodes_in_group("enemies"):
		# Ensure the enemy is a Node3D so we can access its global_position.
		if enemy is Node3D:
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
