extends Area3D

@export var life_time: float = 3.0

var direction: Vector3 = Vector3.ZERO
var speed: float = 40
var damage: float = 1

func _ready() -> void:
	# Schedule the bullet to be freed after life_time seconds.
	await get_tree().create_timer(life_time).timeout
	queue_free()

func initialize(direction: Vector3, size: float, base_speed: float, damage: float, color: Color) -> void:
	self.direction = direction.normalized()
	
	# Allow scaling below 1 but prevent disappearing
	var clamped_size = max(size, 0.01)
	self.scale = Vector3(clamped_size, clamped_size, clamped_size)

	# Adjust bullet speed
	self.speed = base_speed

	# Change bullet color
	# Change bullet color
	var mesh_instance = $MeshInstance3D  # Assuming a MeshInstance3D is used
	if mesh_instance and mesh_instance.mesh:
		var material = mesh_instance.get_surface_override_material(0)
		
		# If no override material exists, try getting the base material
		if material == null:
			material = mesh_instance.mesh.surface_get_material(0)
			
			# If still null, create a new material
			if material == null:
				material = StandardMaterial3D.new()
			
			# Duplicate to prevent modifying a shared resource
			material = material.duplicate()
			mesh_instance.set_surface_override_material(0, material)

		# Set bullet color
		if material is StandardMaterial3D:
			material.albedo_color = color



	# Scale collision shape correctly
	var collision_shape = $CollisionShape3D
	if collision_shape and collision_shape.shape is SphereShape3D:
		collision_shape.shape.radius *= clamped_size
	elif collision_shape and collision_shape.shape is BoxShape3D:
		collision_shape.shape.size *= clamped_size

func _physics_process(delta: float) -> void:
	# Move the bullet forward based on speed
	global_translate(direction * speed * delta)

func _on_Bullet_body_entered(body: Node) -> void:
	if body.has_method("take_damage"):
		body.take_damage(damage)
	queue_free()
