extends RigidBody3D
class_name Card3D

@export var card_base: CardCore
@export var scale_factor: float = 0.01

# Custom physics properties.
@export var card_mass: float = 1.0
@export var card_friction: float = 1.0
@export var card_bounce: float = 0.2

# Hover parameters.
@export var hover_damping: float = 5.0
@export var hover_height: float = 1.75 # Base desired distance above the ground.
@export var hover_force: float = 20.0 # Strength of the upward force to maintain hover.
@export var hover_activation_distance: float = 2.0 # Only apply hover force if the ground is within this distance.

# Oscillation parameters.
@export var oscillation_amplitude: float = 0.2 # How far above and below the base height.
@export var oscillation_frequency: float = 0.2 # Oscillations per second.

# Billboard speed (how quickly the card rotates to face the camera).
@export var billboard_speed: float = 10.0

var raycast: RayCast3D

func _ready() -> void:
	if not card_base:
		card_base = CardCore.new()
	
	# Set a default position (e.g. hover_height above ground) if still at origin.
	if global_transform.origin == Vector3.ZERO:
		global_transform.origin = Vector3(0, hover_height, 0)
	
	# Set physics properties.
	mass = card_mass
	physics_material_override = PhysicsMaterial.new()
	physics_material_override.friction = card_friction
	physics_material_override.bounce = card_bounce
	
	# Enable custom physics integration.
	custom_integrator = true
	
	# Disable collisions for this rigid body.
	collision_layer = 0
	collision_mask = 0
	
	# Create a RayCast3D node to detect the ground below.
	raycast = RayCast3D.new()
	raycast.target_position = Vector3.DOWN * 100 # Cast far downward.
	raycast.add_exception(self) # Prevent raycast from colliding with our own collision shape.
	add_child(raycast)
	raycast.enabled = true
	
	_setup_visuals()

func _integrate_forces(state: PhysicsDirectBodyState3D) -> void:
	var transform = state.transform
	
	# --- Smooth Billboarding: Always face the camera ---
	var camera = get_viewport().get_camera_3d()
	if camera:
		var to_camera = camera.global_transform.origin - transform.origin
		if to_camera.length() > 0.001:
			var up_dir = Vector3.UP
			if abs(to_camera.normalized().dot(up_dir)) > 0.99:
				up_dir = Vector3.FORWARD
			var target_basis = Basis.looking_at(- to_camera, up_dir)
			# Smoothly interpolate the current basis toward the target.
			transform.basis = transform.basis.slerp(target_basis, billboard_speed * state.step)
		# Zero out angular velocity.
		state.angular_velocity = Vector3.ZERO
	
	# --- Apply Gravity Manually ---
	var gravity = ProjectSettings.get_setting("physics/3d/default_gravity")
	state.linear_velocity += Vector3.DOWN * gravity * state.step
	
	# --- Compute Desired Hover Height with Sine Oscillation ---
	if raycast.is_colliding():
		var collision_point = raycast.get_collision_point()
		var ground_distance = transform.origin.y - collision_point.y
		
		# Compute oscillation offset (a sine wave over time).
		var time_in_sec = Time.get_ticks_msec() / 1000.0
		var oscillation_offset = oscillation_amplitude * sin(2.0 * PI * oscillation_frequency * time_in_sec)
		var desired_hover_height = hover_height + oscillation_offset
		
		# Only apply if the ground is close enough.
		if ground_distance < hover_activation_distance:
			var error = desired_hover_height - ground_distance
			var damping_force = state.linear_velocity.y * hover_damping
			var spring_force = error * hover_force
			var net_force = spring_force - damping_force
			state.linear_velocity.y += net_force * state.step
			
			# Prevent sliding on slopes by zeroing horizontal velocity.
			state.linear_velocity.x = 0
			state.linear_velocity.z = 0
	
	state.transform = transform

func _setup_visuals() -> void:
	# Remove any existing MeshInstance3D or CollisionShape3D children.
	for child in get_children():
		if child is MeshInstance3D or child is CollisionShape3D:
			child.queue_free()
	
	# Create a MeshInstance3D for the card's visual.
	var mesh_instance = MeshInstance3D.new()
	var mesh_size = card_base.card_size * scale_factor
	
	# Create a QuadMesh with the computed size.
	var quad_mesh = QuadMesh.new()
	quad_mesh.size = mesh_size
	mesh_instance.mesh = quad_mesh
	
	# Create a StandardMaterial3D with unshaded, nearest filtering settings.
	var material = StandardMaterial3D.new()
	material.flags_unshaded = true
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	# Disable backface culling so the card remains visible when flipped.
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	if card_base.card_texture:
		material.albedo_texture = card_base.card_texture
	else:
		material.albedo_color = card_base.card_color
	mesh_instance.material_override = material
	
	# Center the mesh relative to the node's pivot.
	mesh_instance.transform.origin = Vector3.ZERO
	add_child(mesh_instance)
	
	# Create a CollisionShape3D for physics interactions.
	var collision = CollisionShape3D.new()
	var shape = BoxShape3D.new()
	shape.extents = Vector3(mesh_size.x * 0.5, mesh_size.y * 0.5, 0.1)
	collision.shape = shape
	add_child(collision)
