extends Weapon
class_name BulletWeapon

@onready var bullet_origin: Node3D = $BulletOrigin
@onready var muzzle_flash: Node = $MuzzleFlash

var firing: bool = false

# Called when the fire button is pressed.
func on_press() -> void:
	if firing:
		return # Already firing; ignore subsequent presses.
	firing = true
	_fire_loop() # Start the firing loop.

# Called when the fire button is released.
func on_release() -> void:
	firing = false

# Main fire loop handling continuous firing.
func _fire_loop() -> void:
	while firing:
		if current_ammo <= 0:
			await _reload() # Wait for reload to complete.
		else:
			_fire_bullet()
			current_ammo -= 1
			# Wait exactly the fire_rate delay after firing.
			await get_tree().create_timer(get_weapon_config().fire_rate).timeout

func _fire_bullet() -> void:
	var bullet: Bullet = Bullet.new()
	var base_direction = - bullet_origin.global_transform.basis.z

	# (Set up spread, bullet properties, etc.)
	bullet.direction = base_direction # or your deviated direction from spread logic.
	bullet.speed = get_weapon_config().bullet_speed
	bullet.color = Color.YELLOW
	bullet.radius = 0.05
	bullet.global_transform = bullet_origin.global_transform

	# First, let each module modify the bullet.
	for module in modules:
		bullet = module.modify_bullet(bullet)
		bullet.collision_handlers.append(Callable(module, "on_collision"))

	get_tree().current_scene.add_child(bullet)

	# Then, in the same order, trigger any on_fire callbacks.
	for module in modules:
		if module.has_method("on_fire"):
			module.on_fire(bullet)


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
