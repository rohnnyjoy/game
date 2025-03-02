extends Weapon
class_name BulletWeapon

@onready var bullet_origin: Node3D = $BulletOrigin
@onready var muzzle_flash: Node = $MuzzleFlash

var firing: bool = false

# Called when the fire button is pressed.
func on_press() -> void:
	if firing:
		return  # Already firing; ignore subsequent presses.
	firing = true
	_fire_loop()  # Start the firing loop.

# Called when the fire button is released.
func on_release() -> void:
	firing = false

# Main fire loop handling continuous firing.
func _fire_loop() -> void:
	while firing:
		if current_ammo <= 0:
			await _reload()  # Wait for reload to complete.
		else:
			_fire_bullet()
			current_ammo -= 1
			# Wait exactly the fire_rate delay after firing.
			await get_tree().create_timer(get_weapon_config().fire_rate).timeout

# Fire a single bullet with configurable accuracy.
func _fire_bullet() -> void:
	var bullet: Bullet = Bullet.new()
	var base_direction = -bullet_origin.global_transform.basis.z
	
	# Get accuracy value from the weapon configuration.
	# accuracy should be between 0.0 (worst) and 1.0 (perfect).
	var accuracy = get_weapon_config().accuracy
	
	# Define the maximum possible spread angle when accuracy is 0.
	# Here, we use 45 degrees as the maximum spread, converted to radians.
	var max_spread_angle = deg_to_rad(45)
	
	# Compute the current spread based on accuracy.
	# When accuracy is 1.0, spread_angle becomes 0 (perfect aim).
	var spread_angle = max_spread_angle * (1.0 - accuracy)
	
	# Generate random deviations around 0 for both horizontal and vertical directions.
	var random_y = randf_range(-spread_angle, spread_angle)
	var random_x = randf_range(-spread_angle, spread_angle)
	
	# Apply random deviation.
	# First, rotate around the up vector (Y axis) for horizontal deviation.
	var deviated_direction = base_direction.rotated(Vector3.UP, random_y)
	# Then, rotate around the bullet origin's right vector for vertical deviation.
	deviated_direction = deviated_direction.rotated(bullet_origin.global_transform.basis.x, random_x)
	
	# Set the bullet's direction to the deviated direction.
	bullet.direction = deviated_direction.normalized()
	bullet.speed = get_weapon_config().bullet_speed
	bullet.color = Color.YELLOW
	bullet.radius = 0.05
	bullet.global_transform = bullet_origin.global_transform
	
	for module in modules:
		bullet = module.modify_bullet(bullet)
	get_tree().current_scene.add_child(bullet)
	
	# Trigger the muzzle flash if available.
	if muzzle_flash and muzzle_flash.has_method("trigger_flash"):
		muzzle_flash.trigger_flash()

# Modularized reload logic.
func _reload() -> void:
	reloading = true
	# Wait for the reload speed duration.
	await get_tree().create_timer(get_weapon_config().reload_speed).timeout
	current_ammo = ammo
	reloading = false
