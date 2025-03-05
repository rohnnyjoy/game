extends Card2D
class_name WeaponModuleCard2D

@export var module: WeaponModule

func _init(_module: WeaponModule = null) -> void:
	module = _module
	if not card_base:
		card_base = CardCore.new()
	if module:
		card_base.card_texture = module.card_texture
		card_base.card_description = module.module_description

func _ready() -> void:
	super._ready()

# Override the drag end behavior.
func _on_drag_end() -> void:
	picked_up = false
	z_index = 0
	var target_stack: Node = null
	# Look for any CardStack under the mouse.
	for stack in get_tree().get_nodes_in_group("CardStacks"):
		if stack.get_global_rect().has_point(get_global_mouse_position()):
			target_stack = stack
			break
	
	if target_stack:
		# Standard behavior: move the card into the stack.
		if target_stack != get_parent():
			var old_parent = get_parent()
			var old_global_pos = global_position
			old_parent.remove_child(self)
			if old_parent.has_method("update_cards"):
				old_parent.update_cards(true)
			target_stack.add_child(self)
			global_position = old_global_pos
			if target_stack.has_method("on_card_drop"):
				target_stack.on_card_drop(self)
		else:
			if get_parent().has_method("on_card_drop"):
				get_parent().on_card_drop(self)
	else:
		# No stack found: convert to a 3D card dropped on the ground.
		_convert_to_3d()
		return
	
	await get_tree().create_timer(0.05).timeout
	_reset_scale()
	if get_global_rect().has_point(get_global_mouse_position()):
		_on_mouse_entered()

func _convert_to_3d() -> void:
	# Create an instance of the 3D card (ensure your 3D card class is correctly named).
	var card3d = WeaponModuleCard3D.new(module)
	# Transfer the common data.
	card3d.card_base = card_base
	
	# Get the active 3D camera.
	var camera = get_viewport().get_camera_3d()
	if camera:
		# Calculate a drop position closer to the camera.
		var drop_distance = 2.0 # Closer spawn point.
		# In Godot 4, the camera faces along its negative Z axis.
		var forward = - camera.global_transform.basis.z.normalized()
		var drop_position = camera.global_transform.origin + forward * drop_distance
		
		# Raycast from the camera to the drop position to avoid spawning inside walls.
		var space_state = camera.get_world_3d().direct_space_state
		var query = PhysicsRayQueryParameters3D.new()
		query.from = camera.global_transform.origin
		query.to = drop_position
		query.exclude = []
		query.collision_mask = 0xFFFFFFFF
		var collision = space_state.intersect_ray(query)
		if collision:
			# Adjust drop position to be a safe offset away from the collision point.
			var safe_offset = 0.5
			drop_position = collision.position + collision.normal * safe_offset
		
		# Set the position on the card.
		var transform = card3d.global_transform
		transform.origin = drop_position
		card3d.global_transform = transform
		
		# Add the card to the scene tree first.
		var ground = get_node_or_null("/root/World/Ground")
		if ground:
			ground.add_child(card3d)
		else:
			get_tree().root.add_child(card3d)
		
		# Immediately orient the card to face the camera.
		var to_camera = camera.global_transform.origin - drop_position
		if to_camera.length() > 0.001:
			var up_dir = Vector3.UP
			# Prevent a near-zero dot product scenario.
			if abs(to_camera.normalized().dot(up_dir)) > 0.99:
				up_dir = Vector3.FORWARD
			transform = card3d.global_transform
			transform.basis = Basis.looking_at(- to_camera, up_dir)
			card3d.global_transform = transform
		
		# Instead of teleporting, apply an initial toss velocity (weaker toss).
		var toss_strength = 3.0
		var toss_up_strength = 1.0
		card3d.linear_velocity = forward * toss_strength + Vector3.UP * toss_up_strength
	else:
		# Fallback: place using the 2D global position (converted to 3D).
		card3d.global_transform.origin = Vector3(global_position.x, global_position.y, 0)
		var ground = get_node_or_null("/root/World/Ground")
		if ground:
			ground.add_child(card3d)
		else:
			get_tree().root.add_child(card3d)
	
	queue_free()
