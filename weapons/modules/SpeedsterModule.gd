extends WeaponModule
class_name SpeedsterModule

@export var extra_trail_lifetime_multiplier: float = 1.5  # Extra trail lasts 1.5x longer than the bullet's lifetime.
@export var extra_trail_base_width_multiplier: float = 1.0  # Optionally scale width.
@export var extra_trail_distance: float = 0.05  # Finer sampling for the extra trail.
@export var default_trail_width: float = 0.5       # Fallback width if bullet.trail is nil.
@export var default_trail_segments: int = 20       # Fallback segments if bullet.trail is nil.

func modify_bullet(bullet: Bullet) -> Bullet:
	# Create a new instance of the BulletTrail for the additional effect.
	var extra_trail = BulletTrail.new()
	
	# Configure the extra trail to be translucent white.
	var white_gradient = Gradient.new()
	white_gradient.add_point(0.0, Color(1, 1, 1, 0.2))  # Start: translucent white.
	white_gradient.add_point(1.0, Color(1, 1, 1, 0.0))  # End: fully transparent.
	extra_trail.color_gradient = white_gradient
	
	# Use bullet.trail properties if available; otherwise, use defaults.
	var base_width = bullet.get_width()
	var segments = default_trail_segments
	
	# Set the trail parameters for a long, smooth effect.
	extra_trail.lifetime = 1.5
	extra_trail.base_width = base_width * extra_trail_base_width_multiplier
	extra_trail.distance = extra_trail_distance
	extra_trail.segments = segments
	extra_trail.emit = true
	
	# Use the bullet itself as the target for the trail.
	extra_trail.initialize(bullet)
	
	# Add the extra trail to the same parent as the bullet so it follows in the scene.
	bullet.get_parent().add_child(extra_trail)
	
	# Optionally, store a reference on the bullet for cleanup later.
	bullet.set("speedster_trail", extra_trail)
	
	return bullet

#func modify_weapon(config: WeaponConfig) -> WeaponConfig:
	## For example, increase fire rate if desired.
	#if config.has("fire_rate"):
		#config.fire_rate *= 1.2
	#return config
