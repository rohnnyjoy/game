extends WeaponModule
class_name FrostbiteModule

const SLOW_DURATION: float = 5.0
const SLOW_AMOUNT: float = 0.5  # 50% speed reduction
const SLOW_RADIUS: float = 10.0  # 10-meter range around the player

# Called when the player fires the weapon.
func on_fire(_bullet: Bullet) -> void:
	var player = get_parent()
	if not player:
		return

	# Find all enemies within range
	var enemies = get_tree().get_nodes_in_group("enemies")
	for enemy in enemies:
		if enemy.global_position.distance_to(player.global_position) <= SLOW_RADIUS:
			apply_slow(enemy)

# Called when a bullet collides with an enemy.
func on_collision(collision: Dictionary, _bullet: Bullet) -> void:
	var enemy = collision.collider
	if enemy.is_in_group("enemies"):
		apply_slow(enemy)

# Apply the slow effect to an enemy
func apply_slow(enemy: Node) -> void:
	if enemy.has_method("set_speed_multiplier"):
		# Always apply slow, even if it was previously removed
		enemy.set_speed_multiplier(SLOW_AMOUNT)
		print("Slowing:", enemy.name)

		# Remove any existing slow timer before applying a new one
		if enemy.has_meta("slow_timer"):
			var existing_timer = enemy.get_meta("slow_timer")
			if existing_timer and is_instance_valid(existing_timer):
				existing_timer.queue_free()  # Delete old timer

		# Create a new timer to reset speed after SLOW_DURATION
		var timer = Timer.new()
		timer.wait_time = SLOW_DURATION
		timer.one_shot = true
		timer.timeout.connect(func():
			on_slow_timeout(enemy)
		)
		enemy.add_child(timer)  # Attach timer to enemy
		enemy.set_meta("slow_timer", timer)  # Store reference
		timer.start()

# Reset enemy speed when slow expires
func on_slow_timeout(enemy: Node) -> void:
	if enemy and is_instance_valid(enemy):
		enemy.set_speed_multiplier(1.0)
		print(enemy.name, "slow effect expired, resetting speed.")
