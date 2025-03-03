extends WeaponModule
class_name FrostbiteModule

const SLOW_DURATION: float = 5.0
const SLOW_AMOUNT: float = 0.5  # 50% speed reduction
const SLOW_RADIUS: float = 5.0  # 5-meter range around the player

func on_fire(_bullet: Bullet) -> void:
	var player = get_parent()  # Assuming the weapon is a child of the player
	if not player:
		return

	var enemies = get_tree().get_nodes_in_group("enemies")
	for enemy in enemies:
		if enemy.global_transform.origin.distance_to(player.global_transform.origin) <= SLOW_RADIUS:
			apply_slow(enemy)

func on_collision(collision: Dictionary, _bullet: Bullet) -> void:
	if collision.collider.is_in_group("enemies"):
		apply_slow(collision.collider)

func apply_slow(enemy: Enemy) -> void:
	if enemy.has_method("set_speed_multiplier") and not enemy.has_meta("is_slowed"):
		enemy.set_meta("is_slowed", true)
		enemy.set_speed_multiplier(SLOW_AMOUNT)

		# Wait for the slow duration to end, then restore speed
		await get_tree().create_timer(SLOW_DURATION).timeout
		enemy.set_speed_multiplier(1.0)
		enemy.set_meta("is_slowed", false)
