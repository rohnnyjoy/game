extends WeaponModule
class_name SlowModule

@export var default_trail_width: float = 0.5 # Fallback width if bullet.trail is nil.

func modify_bullet(bullet: Bullet) -> Bullet:
	bullet.speed *= .15
	return bullet
