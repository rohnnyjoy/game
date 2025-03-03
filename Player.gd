extends CharacterBody3D

signal health_changed(health_value)
@export var pistol_scene: PackedScene

#===============================================================================
# AirLurchManager - Handles air lurch mechanics
#===============================================================================
class AirLurchManager:
	const LURCH_SPEED = 10.0
	const LURCH_SPEED_LOSS = 0.2
	const CONE_HALF_ANGLE = PI / 5
	const LURCH_DURATION = 15.0
	
	var used_cone_angles: Array = []
	var air_initial_dir: Vector2
	var lurch_end_time: int = 0
	
	# Initialize the manager with the normalized initial horizontal direction.
	func initialize(initial_direction: Vector2) -> void:
		air_initial_dir = initial_direction.normalized()
		used_cone_angles.clear()
		used_cone_angles.append(air_initial_dir.angle())
		lurch_end_time = Time.get_ticks_msec() + int(LURCH_DURATION * 1000)
	
	# Returns the signed smallest difference between two angles.
	func angle_difference(angle_a: float, angle_b: float) -> float:
		return atan2(sin(angle_a - angle_b), cos(angle_a - angle_b))
	
	# Determines whether a lurch can be performed given the current input.
	func can_lurch(input_direction: Vector2) -> bool:
		if Time.get_ticks_msec() > lurch_end_time:
			return false
		var input_angle = input_direction.angle()
		for used_angle in used_cone_angles:
			if abs(angle_difference(input_angle, used_angle)) < CONE_HALF_ANGLE:
				return false
		return true
	
	# Marks the given input direction as used for a lurch.
	func mark_lurch_used(input_direction: Vector2) -> void:
		used_cone_angles.append(input_direction.angle())
	
	# Attempts to perform a lurch; if valid, returns the new horizontal velocity.
	func perform_lurch(current_vel: Vector2, input_direction: Vector2) -> Vector2:
		if not can_lurch(input_direction):
			return current_vel
		var lurch_vector = input_direction.normalized() * LURCH_SPEED
		var new_vel = current_vel + lurch_vector
		var new_speed = new_vel.length() * (1.0 - LURCH_SPEED_LOSS)
		new_vel = new_vel.normalized() * new_speed
		mark_lurch_used(input_direction)
		return new_vel

#===============================================================================
# Constants & Variables
#===============================================================================
const SPEED = 8.0
const JUMP_VELOCITY = 20.0
const GROUND_ACCEL = 80.0
const GROUND_DECEL = 20.0
const INITIAL_BOOST_FACTOR = 0.8
const AIR_ACCEL = 8.0
const JUMP_BUFFER_TIME = 0.2
const MAX_JUMPS = 2
const GRAVITY = 60.0
const DASH_SPEED = 10.0

# Sliding settings
const SLIDE_COLLISION_SPEED_FACTOR = 0.7
const SLIDE_FRICTION_COEFFICIENT = 10

@onready var camera = $Camera3D
@onready var anim_player = $AnimationPlayer
@onready var muzzle_flash = $Camera3D/Pistol/MuzzleFlash
@onready var raycast = $Camera3D/RayCast3D

var health = 3
var jump_buffer_timer = 0.0
var jumps_remaining = MAX_JUMPS

var air_lurch_manager: AirLurchManager = null

# Sliding state
var is_sliding: bool = false

var current_weapon: Weapon = null


# Store the horizontal velocity before collision resolution for better bounce calculations.
var pre_slide_horizontal_velocity: Vector3 = Vector3.ZERO

func equip_default_weapon():
	var pistol = pistol_scene.instantiate()
	$Camera3D/WeaponHolder.add_child(pistol)
	current_weapon = pistol


#===============================================================================
# Multiplayer Setup & Initialization
#===============================================================================
func _enter_tree():
	# Set multiplayer authority based on this node's name (as int).
	set_multiplayer_authority(str(name).to_int())

func _ready():
	if not is_multiplayer_authority():
		return
	_setup_input()
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	camera.current = true
	equip_default_weapon()

func _setup_input() -> void:
	# Ensure the "slide" action exists.
	if not InputMap.has_action("slide"):
		InputMap.add_action("slide")
		var ev := InputEventKey.new()
		ev.keycode = KEY_C
		InputMap.action_add_event("slide", ev)
	if not InputMap.has_action("dash"):
		InputMap.add_action("dash")
		var ev := InputEventKey.new()
		ev.keycode = KEY_SHIFT
		InputMap.action_add_event("dash", ev)

#===============================================================================
# Input Handling
#===============================================================================
func _unhandled_input(event):
	if not is_multiplayer_authority():
		return
	_handle_camera_rotation(event)
	_handle_shooting(event)
	_handle_dash(event)

func _handle_camera_rotation(event):
	if event is InputEventMouseMotion:
		rotate_y(- event.relative.x * 0.005)
		camera.rotate_x(- event.relative.y * 0.005)
		# Clamp vertical camera rotation.
		camera.rotation.x = clamp(camera.rotation.x, - PI / 2, PI / 2)
#
func _handle_shooting(_event):
	if Input.is_action_just_pressed("shoot"):
		current_weapon.on_press()
	elif Input.is_action_just_released("shoot"):
		current_weapon.on_release()

func _handle_dash(_event):
	if Input.is_action_just_pressed("dash"):
		velocity = velocity + get_input_direction() * DASH_SPEED

func get_input_direction() -> Vector3:
	var raw_input = Input.get_vector("left", "right", "up", "down")
	return (transform.basis * Vector3(raw_input.x, 0, raw_input.y)).normalized()

#===============================================================================
# Physics Process
#===============================================================================
func _physics_process(delta):
	if not is_multiplayer_authority():
		return
	
	_process_jump_and_gravity(delta)
	
	# Get horizontal movement input.
	var raw_input = Input.get_vector("left", "right", "up", "down")
	var input_direction: Vector3 = Vector3.ZERO
	if raw_input != Vector2.ZERO:
		input_direction = (transform.basis * Vector3(raw_input.x, 0, raw_input.y)).normalized()
	
	# Process movement based on grounded or airborne state.
	if is_on_floor():
		_process_ground_movement(input_direction, delta)
	else:
		_process_air_movement(input_direction, raw_input, delta)
	
	# Save the horizontal velocity before collisions are resolved.
	pre_slide_horizontal_velocity = Vector3(velocity.x, 0, velocity.z)
	
	# Process movement and let move_and_slide handle collisions.
	move_and_slide()
	
	# After collision resolution, if sliding, process bounce behavior.
	if is_sliding:
		_process_slide_collisions_post()	
	
	if not is_on_floor and is_colliding_with_wall():
		jumps_remaining = min(jumps_remaining + 1, MAX_JUMPS)

#===============================================================================
# Jump and Gravity
#===============================================================================
func _process_jump_and_gravity(delta):
	# Handle jump buffering.
	jump_buffer_timer = max(jump_buffer_timer - delta, 0)
	if Input.is_action_just_pressed("ui_accept"):
		jump_buffer_timer = JUMP_BUFFER_TIME
	
	# Reset available jumps when on the ground.
	if is_on_floor():
		jumps_remaining = MAX_JUMPS
		
	# Wall jump detection
	var wall_normal = is_colliding_with_wall()
	if not is_on_floor() and wall_normal != Vector3.ZERO:
		jumps_remaining = min(jumps_remaining + 1, MAX_JUMPS)
		
	# Perform jump if buffered and available.
	if jump_buffer_timer > 0 and jumps_remaining > 0:
		velocity.y = JUMP_VELOCITY
		jump_buffer_timer = 0
		jumps_remaining -= 1
		
		# If wall jumping, push the player away from the wall
		if wall_normal != Vector3.ZERO:
			velocity += wall_normal * 10.0  # Push away from wall (adjustable force)
	
	# Apply gravity when not on the ground.
	if not is_on_floor():
		velocity.y -= GRAVITY * delta

func is_colliding_with_wall() -> Vector3:
	for i in range(get_slide_collision_count()):
		var collision = get_slide_collision(i)
		var col_normal = collision.get_normal()
		# If normal is not floor-like (not pointing up), it's a wall
		if abs(col_normal.dot(Vector3.UP)) < 0.7:
			return col_normal
	return Vector3.ZERO  # No wall detected
#===============================================================================
# Ground Movement
#===============================================================================
func _process_ground_movement(input_direction: Vector3, delta):
	# Reset air lurch manager on landing.
	air_lurch_manager = null
	
	# Cancel sliding when jumping to allow a proper jump.
	if Input.is_action_just_pressed("ui_accept"):
		is_sliding = false
		_process_standard_ground_movement(input_direction, delta)
	elif Input.is_action_pressed("slide"):
		_process_slide(delta)
	else:
		# If not sliding, ensure standard movement is processed.
		if is_sliding:
			is_sliding = false
		_process_standard_ground_movement(input_direction, delta)

func _process_standard_ground_movement(input_direction: Vector3, delta):
	# Process movement input for non-sliding movement.
	if input_direction != Vector3.ZERO:
		var current_horizontal = Vector3(velocity.x, 0, velocity.z)
		if current_horizontal.length() < 0.1:
			# Give an initial boost.
			velocity.x = input_direction.x * SPEED * INITIAL_BOOST_FACTOR
			velocity.z = input_direction.z * SPEED * INITIAL_BOOST_FACTOR
		else:
			velocity.x = move_toward(velocity.x, input_direction.x * SPEED, GROUND_ACCEL * delta)
			velocity.z = move_toward(velocity.z, input_direction.z * SPEED, GROUND_ACCEL * delta)
		anim_player.play("move")
	else:
		velocity.x = move_toward(velocity.x, 0, GROUND_DECEL * delta)
		velocity.z = move_toward(velocity.z, 0, GROUND_DECEL * delta)
		anim_player.play("idle")

#===============================================================================
# Sliding Mechanics
#===============================================================================
func _process_slide(delta):
	if not is_sliding:
		is_sliding = true
		anim_player.play("slide")
	var floor_normal = get_floor_normal()
	var gravity_vector = Vector3.DOWN
	var natural_downhill = (gravity_vector - floor_normal * gravity_vector.dot(floor_normal)).normalized()
	var slope_angle = acos(floor_normal.dot(Vector3.UP))
	var gravity_accel = GRAVITY * sin(slope_angle)
	velocity = velocity + natural_downhill * gravity_accel * delta
	velocity.x = move_toward(velocity.x, 0, SLIDE_FRICTION_COEFFICIENT * delta)
	velocity.z = move_toward(velocity.z, 0, SLIDE_FRICTION_COEFFICIENT * delta)

#===============================================================================
# Post-Collision Slide Bounce Processing
#===============================================================================
func _process_slide_collisions_post():
	# Check collisions after move_and_slide has resolved them.
	var collision_count = get_slide_collision_count()
	for i in range(collision_count):
		var collision = get_slide_collision(i)
		var col_normal = collision.get_normal()
		# If the collision is with a wall (non-floor), adjust the slide bounce.
		if abs(col_normal.dot(Vector3.UP)) < 0.7:
			var reflected = pre_slide_horizontal_velocity.bounce(col_normal)
			if reflected.length() < 0.1:
				reflected = - pre_slide_horizontal_velocity
			velocity.x = reflected.x * SLIDE_COLLISION_SPEED_FACTOR
			velocity.z = reflected.z * SLIDE_COLLISION_SPEED_FACTOR
			# Only process the first wall collision encountered.
			break

#===============================================================================
# Airborne Movement
#===============================================================================
func _process_air_movement(input_direction: Vector3, raw_input: Vector2, delta):
	# Initialize the air lurch manager if needed.
	if air_lurch_manager == null:
		air_lurch_manager = AirLurchManager.new()
		var horizontal_dir = Vector2(velocity.x, velocity.z)
		air_lurch_manager.initialize(horizontal_dir.normalized())
	
	# If input is minimal, decelerate in the air.
	if raw_input.length() < 0.1:
		velocity.x = move_toward(velocity.x, 0, AIR_ACCEL * delta)
		velocity.z = move_toward(velocity.z, 0, AIR_ACCEL * delta)
	else:
		# Map horizontal velocity from Vector2 back to Vector3.
		var current_vel = Vector2(velocity.x, velocity.z)
		var new_vel = air_lurch_manager.perform_lurch(current_vel, Vector2(input_direction.x, input_direction.z))
		velocity.x = new_vel.x
		velocity.z = new_vel.y

#===============================================================================
# RPC Functions
#===============================================================================
@rpc("call_local")
func play_shoot_effects():
	anim_player.stop()
	anim_player.play("shoot")

@rpc("any_peer")
func receive_damage():
	health -= 1
	if health <= 0:
		# Reset health and respawn.
		health = 3
		position = Vector3.ZERO
	emit_signal("health_changed", health)

#===============================================================================
# Animation Callbacks
#===============================================================================
func _on_animation_player_animation_finished(anim_name):
	if anim_name == "shoot":
		anim_player.play("idle")
