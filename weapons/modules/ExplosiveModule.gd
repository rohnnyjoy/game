extends WeaponModule
class_name ExplosiveModule

const EXPLOSION_RADIUS: float = 5.0  # Area of effect radius
const EXPLOSION_DAMAGE_MULTIPLIER: float = 0.5  # AOE deals 50% of bullet damage
const DIRECT_HIT_BONUS_DAMAGE: float = 10.0  # Extra damage to direct hit

# Called when a bullet collides with an enemy
func on_collision(collision: Dictionary, bullet: Bullet) -> void:
	var impact_point = collision.position
	var target_enemy = collision.collider
	
	# Ensure the target is a valid enemy
	if target_enemy.is_in_group("enemies") and target_enemy.has_method("apply_damage"):
		var total_damage = bullet.damage + DIRECT_HIT_BONUS_DAMAGE
		target_enemy.apply_damage(total_damage)
		print(target_enemy.name, "took", total_damage, "damage from direct hit!")

	# Apply AOE damage to nearby enemies
	var enemies = bullet.get_tree().get_nodes_in_group("enemies")
	for enemy in enemies:
		# Skip the direct hit enemy (we already applied full damage)
		if enemy == target_enemy:
			continue

		# Check if enemy is within explosion radius
		if enemy.global_position.distance_to(impact_point) <= EXPLOSION_RADIUS:
			var aoe_damage = bullet.damage * EXPLOSION_DAMAGE_MULTIPLIER
			enemy.take_damage(aoe_damage)
			print(enemy.name, "took", aoe_damage, "damage from explosion AOE!")
