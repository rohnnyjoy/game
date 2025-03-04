extends WeaponModule
class_name GravityModule

const GRAVITY_FORCE: float = 700.0  # Stronger gravity for aggressive curve
const DAMAGE_GROWTH_RATE: float = 0.1  # More damage over distance

# Called when a bullet is fired
func modify_bullet(bullet: Bullet) -> Bullet:
	bullet.set_meta("initial_position", bullet.global_position)
	bullet.set_meta("traveled_distance", 0.0)
	bullet.set_meta("base_damage", bullet.damage)  # Store base damage for scaling

	# Apply gravity
	bullet.gravity = GRAVITY_FORCE  

	return bullet 

# Called every physics frame (ensures gravity is applied)
func on_physics_process(delta: float, bullet: Bullet) -> void:
	_apply_gravity(bullet, delta)

# Applies gravity and updates damage
func _apply_gravity(bullet: Bullet, delta: float) -> void:
	if not bullet:
		return

	# Apply gravity
	bullet.velocity.y -= GRAVITY_FORCE * delta  

	# Track distance traveled
	var initial_pos = bullet.get_meta("initial_position", bullet.global_position)
	var traveled = initial_pos.distance_to(bullet.global_position)
	bullet.set_meta("traveled_distance", traveled)

	# Increase damage over distance
	var base_damage = bullet.get_meta("base_damage", bullet.damage)
	bullet.damage = base_damage + (traveled * DAMAGE_GROWTH_RATE)

# Called on collision
func on_collision(collision: Dictionary, bullet: Bullet) -> void:
	var enemy = collision.collider

	# Ensure collision target is an enemy
	if enemy.is_in_group("enemies") and enemy.has_method("take_damage"):
		enemy.take_damage(bullet.damage)
		print(enemy.name, "took", bullet.damage, "damage (Distance traveled:", bullet.get_meta("traveled_distance"), ")")
