extends WeaponModule
class_name BouncingModule

var damage_reduction: float = 0.2
var bounciness: float = 0.8
var max_bounces: int = 3

func _init() -> void:
	module_description = "Bullets bounce off surfaces, reducing damage with each bounce."

func modify_bullet(bullet: Bullet) -> Bullet:
	if not bullet.has_meta("bounce_count"):
		bullet.set_meta("bounce_count", 0)
	bullet.destroy_on_impact = false
	return bullet

func on_collision(collision: Dictionary, bullet: Bullet) -> void:
	var normal: Vector3 = collision["normal"]
	
	# Update bounce count.
	var bounce_count: int = bullet.get_meta("bounce_count")
	bounce_count += 1
	bullet.set_meta("bounce_count", bounce_count)
	
	# Apply damage reduction and bounce physics.
	bullet.damage *= (1.0 - damage_reduction)
	bullet.velocity = bullet.velocity.bounce(normal) * bounciness
	bullet.global_transform.origin = collision["position"] + normal * 0.01
	
	# Remove bullet if it has exceeded the maximum allowed bounces.
	print("Bounce count: ", bounce_count)
	if bounce_count >= max_bounces:
		bullet.queue_free()
