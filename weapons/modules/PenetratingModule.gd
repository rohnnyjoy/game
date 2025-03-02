extends WeaponModule
class_name PenetratingModule

var damage_reduction: float = 0.2
var velocity_factor: float = 0.9
var max_penetrations: int = 3

func modify_bullet(bullet: Bullet) -> Bullet:
	bullet.collision_handlers.append(Callable(self, "_on_bullet_predicted_collision"))
	if not bullet.has_meta("penetration_count"):
		bullet.set_meta("penetration_count", 0)
	return bullet

func _on_bullet_predicted_collision(collision: Dictionary, bullet: Bullet) -> void:
	var penetration_count: int = bullet.get_meta("penetration_count")
	penetration_count += 1
	bullet.set_meta("penetration_count", penetration_count)
	
	bullet.damage *= (1.0 - damage_reduction)
	bullet.velocity *= velocity_factor
	
	bullet.global_transform.origin = collision["position"] + bullet.velocity.normalized() * 0.01
	
	if penetration_count >= max_penetrations:
		bullet.queue_free()
