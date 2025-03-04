extends WeaponModule
class_name SlowModule

@export var default_trail_width: float = 0.5 # Fallback width if bullet.trail is nil.

func _init() -> void:
	module_description = "Bullets move slower, but do more damage."

func modify_bullet(bullet: Bullet) -> Bullet:
	bullet.speed *= .15
	bullet.damage *= 1.5
	return bullet
