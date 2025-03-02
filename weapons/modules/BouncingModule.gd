extends WeaponModule
class_name BouncingModule

var damage_reduction: float = 0.2
var bounciness: float = 0.8
var max_bounces: int = 3

# Adds the bouncing process to the bullet.
func modify_bullet(bullet: Bullet) -> Bullet:
	bullet.physics_processes.append(Callable(self, "bouncing_process").bind(bullet))
	if not bullet.has_meta("bounce_count"):
		bullet.set_meta("bounce_count", 0)
	return bullet
	

func bouncing_process(delta: float, bullet: Bullet) -> void:
	var current_position: Vector3 = bullet.global_transform.origin
	var predicted_motion: Vector3 = bullet.velocity * delta
	var predicted_position: Vector3 = current_position + predicted_motion
	
	var query = PhysicsRayQueryParameters3D.new()
	query.from = current_position
	query.to = predicted_position
	query.exclude = [bullet]
	
	# Check for a collision along the predicted path.
	var collision = bullet.get_world_3d().direct_space_state.intersect_ray(query)
	if collision:
		var normal: Vector3 = collision["normal"]
		var bounce_count: int = bullet.get_meta("bounce_count") if bullet.has_meta("bounce_count") else 0
		bounce_count += 1
		bullet.set_meta("bounce_count", bounce_count)
		
		bullet.damage *= (1.0 - damage_reduction)
		bullet.velocity = bullet.velocity.bounce(normal) * bounciness
		bullet.global_transform.origin = collision["position"] + normal * 0.01
		
		if bounce_count >= max_bounces:
			bullet.queue_free()
			
	else:
		pass
