extends WeaponModule
class_name ExplosiveModule

const EXPLOSION_RADIUS: float = 5.0  # Area of effect radius
const EXPLOSION_DAMAGE_MULTIPLIER: float = 0.5  # AOE deals 50% of bullet damage

# Called when a bullet collides with an enemy
func on_collision(collision: Dictionary, bullet: Bullet) -> void:
	var impact_point = collision.position
	var target_enemy = collision.collider
	var aoe_damage = bullet.damage * EXPLOSION_DAMAGE_MULTIPLIER
	
	# Apply AOE damage to nearby enemies
	var enemies = bullet.get_tree().get_nodes_in_group("enemies")
	for enemy in enemies:
		# Ensure the target is a valid enemy
		if enemy.is_in_group("enemies") and enemy.has_method("take_damage"):
		# Check if enemy is within explosion radius
			if enemy.global_position.distance_to(impact_point) <= EXPLOSION_RADIUS:
				enemy.take_damage(aoe_damage)
				print(enemy.name, "took", aoe_damage, "damage from explosion AOE!")
