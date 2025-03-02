extends CharacterBody3D
class_name Bullet

@export var life_time: float = 3.0
@export var gravity: float = 0.0

# Bullet movement properties.
@export var direction: Vector3 = Vector3.FORWARD
@export var speed: float = 20.0
@export var damage: float = 1.0
@export var color: Color = Color(1, 1, 1)

# Use a backing variable for the radius.
@export var radius: float = 0.5 : set = set_radius, get = get_radius
var _radius: float = 0.5

# Array for trails.
@export var trails: Array = []

# Exported gradient for the trail.
@export var trail_gradient: Gradient = Gradient.new()

# Internal reference for the bullet's visual mesh.
var _mesh: MeshInstance3D

# Array to hold physics process callables.
var physics_processes: Array = []

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
	# For example, we can set a short lifetime for trail points.
	default_trail.lifetime = 0.1
	trails.append(default_trail)

func _ready() -> void:
	# Ensure that both process and physics process are enabled.
	set_process(true)
	set_physics_process(true)
	
	# Add the default physics process.
	physics_processes.append(Callable(self, "default_physics_process"))
	
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
	
	# Create the physical collision shape that is used for movement.
	# (Place it on a collision layer that does not interact with the player.)
	var collision_shape = CollisionShape3D.new()
	var sphere_shape = SphereShape3D.new()
	sphere_shape.radius = radius + 0.5
	collision_shape.shape = sphere_shape
	add_child(collision_shape)
	# For example, set this bullet’s collision layer to 2.
	# (Make sure the player is not on layer 2 so the bullet doesn’t physically affect the player.)
	collision_layer = 2
	collision_mask = 0  # No physical collision response.
	
	# Create an Area3D to detect collisions (including with the player).
	var detection_area = Area3D.new()
	var area_collision_shape = CollisionShape3D.new()
	var area_sphere = SphereShape3D.new()
	area_sphere.radius = radius + 0.5
	area_collision_shape.shape = area_sphere
	detection_area.add_child(area_collision_shape)
	# Configure the detection area to “see” the player.
	# For example, if the player is on collision layer 1, then set the mask accordingly.
	detection_area.collision_layer = 0  # It doesn't need to be on a physical layer.
	detection_area.collision_mask = 1   # Detect bodies on layer 1 (player's layer).
	add_child(detection_area)
	
	# Connect the Area3D’s signal to register collisions.
	detection_area.body_entered.connect(_on_body_entered)
	
	# Set the bullet's initial velocity.
	velocity = direction.normalized() * speed
	
	# Add trails to the scene if necessary.
	await get_tree().process_frame
	var parent_node = get_parent()
	if parent_node:
		for trail in trails:
			trail.initialize(_mesh)
			parent_node.add_child(trail)
	
	# Life time timer for the bullet.
	await get_tree().create_timer(life_time).timeout
	for t in trails:
		if t and t.has_method("stop_trail"):
			t.stop_trail()
	queue_free()

func default_physics_process(delta: float) -> void:
	velocity += Vector3.DOWN * gravity * delta
	
	# Optionally, you can also check for other collisions here if needed.
	var collision_count = get_slide_collision_count()
	for i in range(collision_count):
		var collision = get_slide_collision(i)
		var collider = collision.get_collider()
		if collider is Enemy:
			print("Hit enemy")
			collider.take_damage(10)
			
		print("Bullet collided with (via move_and_slide): ", collision.get_collider())

func _physics_process(delta: float) -> void:
	for process in physics_processes:
		if process and process.is_valid():
			process.call(delta)
	move_and_slide()

# This function will be called whenever the detection Area3D overlaps a body.
func _on_body_entered(body: Node) -> void:
	print("Bullet detected collision with: ", body)
	# Optionally, handle damage or other effects here.
