extends WeaponModule
class_name StickyModule

var stick_duration: float = 2.0

func modify_bullet(bullet: Bullet) -> Bullet:
	bullet.set_meta("is_sticky", false)
	return bullet
func on_collision(collision: Dictionary, bullet: Bullet) -> void:
	# If bullet is already stuck, ignore additional collisions.
	if bullet.get_meta("is_sticky"):
		return

	if collision.collider.is_in_group("enemies"):
		# Mark bullet as stuck.
		bullet.set_meta("is_sticky", true)
		
		# Stop bullet motion.
		bullet.velocity = Vector3.ZERO
		
		# Position the bullet with a slight offset.
		var normal: Vector3 = collision.get("normal", Vector3.UP)
		bullet.global_transform.origin = collision["position"] + normal * 0.01
		
		# Optionally attach bullet to the enemy.
		collision.collider.add_child(bullet)
		
		# Use the bullet's scene tree.
		var tree = bullet.get_tree()
		if tree:
			await tree.create_timer(stick_duration).timeout
		else:
			# Fallback: if bullet isn't in the tree, you might want to log a warning
			print("Warning: bullet is not in the scene tree!")
		
		# After the timer, reset the sticky flag.
		bullet.set_meta("is_sticky", false)
