extends WeaponModule
class_name SpeedsterModule

@export var default_trail_width: float = 0.5 # Fallback width if bullet.trail is nil.

func modify_bullet(bullet: Bullet) -> Bullet:
	var extra_trail = BulletTrail.new()
	# Use the new transparency_mode property instead of shading_mode.
	extra_trail.transparency_mode = BaseMaterial3D.TRANSPARENCY_ALPHA
	
	var white_gradient = Gradient.new()
	white_gradient.set_color(1, Color(1, 1, 1, 0.1)) # Start: translucent white.
	white_gradient.set_color(0, Color(1, 1, 1, 0.0)) # End: fully transparent.
	extra_trail.gradient = white_gradient
		
	extra_trail.base_width = 0.2
	extra_trail.lifetime = 1
	extra_trail.emit = true
	
	bullet.trails.append(extra_trail)
	
	bullet.speed *= 2 # Increase speed by 100%.
	
	return bullet
