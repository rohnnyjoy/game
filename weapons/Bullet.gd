extends CharacterBody3D
class_name Bullet

@export var life_time: float = 3.0
@export var gravity: float = 0.0

# Bullet movement properties.
@export var direction: Vector3 = Vector3.FORWARD
@export var speed: float = 20.0
@export var damage: float = 1.0
@export var color: Color = Color(1, 1, 1)

# Backing variable for the radius.
@export var radius: float = 0.5: set = set_radius, get = get_radius
var _radius: float = 0.5

# Array for trails.
@export var trails: Array = []
@export var trail_gradient: Gradient = Gradient.new()

# Internal reference for the bullet's visual mesh.
var _mesh: MeshInstance3D

# Array for collision handlers.
var collision_handlers: Array = []

func set_radius(new_radius: float) -> void:
	_radius = new_radius
	# Update the base_width for each trail.
	for trail in trails:
		trail.base_width = new_radius

func get_radius() -> float:
	return _radius

func _init() -> void:
	# Create a default trail.
	var default_trail = BulletTrail.new()
	default_trail.gradient = Gradient.new()
	default_trail.gradient.set_color(0, Color.WHITE)
	default_trail.gradient.set_color(1, Color.YELLOW)
	default_trail.base_width = radius
	default_trail.lifetime = 0.1 # Short lifetime for trail points.
	trails.append(default_trail)

func _ready() -> void:
	set_process(true)
	set_physics_process(true)
	
	# Force a uniform scale.
	self.scale = Vector3.ONE
	
	# Create the bullet's visual mesh.
	_mesh = MeshInstance3D.new()
	var sphere_mesh = SphereMesh.new()
	sphere_mesh.radius = radius
	sphere_mesh.height = radius * 2
	_mesh.mesh = sphere_mesh
	_mesh.scale = Vector3.ONE
	add_child(_mesh)
	
	var mat = StandardMaterial3D.new()
	mat.albedo_color = color
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_mesh.material_override = mat
	
	# Create the physical collision shape.
	var collision_shape = CollisionShape3D.new()
	var sphere_shape = SphereShape3D.new()
	sphere_shape.radius = radius + 0.5
	collision_shape.shape = sphere_shape
	add_child(collision_shape)
	collision_layer = 2
	collision_mask = 0
	
	# Create an Area3D to detect collisions (e.g. with the player).
	var detection_area = Area3D.new()
	var area_collision_shape = CollisionShape3D.new()
	var area_sphere = SphereShape3D.new()
	area_sphere.radius = radius + 0.5
	area_collision_shape.shape = area_sphere
	detection_area.add_child(area_collision_shape)
	detection_area.collision_layer = 0
	detection_area.collision_mask = 1
	add_child(detection_area)
	
	# Set the bullet's initial velocity.
	velocity = direction.normalized() * speed
	
	# Add trails to the scene if necessary.
	await get_tree().process_frame
	var parent_node = get_parent()
	if parent_node:
		for trail in trails:
			trail.initialize(_mesh)
			parent_node.add_child(trail)
	
	# Timer to free the bullet after its life time.
	await get_tree().create_timer(life_time).timeout
	for t in trails:
		if t and t.has_method("stop_trail"):
			t.stop_trail()
	queue_free()

func _physics_process(delta: float) -> void:
	velocity += Vector3.DOWN * gravity * delta
	
	# Collision prediction: cast a ray from current to predicted position.
	var current_position: Vector3 = global_transform.origin
	var predicted_motion: Vector3 = velocity * delta
	var predicted_position: Vector3 = current_position + predicted_motion
	
	var query: PhysicsRayQueryParameters3D = PhysicsRayQueryParameters3D.new()
	query.from = current_position
	query.to = predicted_position
	query.exclude = [self]
	
	var collision = get_world_3d().direct_space_state.intersect_ray(query)
	if collision:
		# Invoke each collision handler (in the order they were added).
		for handler in collision_handlers:
			# Each handler now receives three arguments: collision, bullet, and impact_properties.
			handler.call(collision, self)
		# Base damage collision is applied last
		_on_bullet_collision(collision, self)
	move_and_slide()

func _on_bullet_collision(collision: Dictionary, _bullet: Bullet) -> void:
	print(collision)
	if collision.collider.is_in_group("enemies"):
		var enemy = collision.collider
		if enemy.has_method("take_damage"):
			enemy.take_damage(damage)
