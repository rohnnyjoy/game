extends CharacterBody3D
class_name Enemy

signal enemy_detected(target)
signal enemy_died()

@export var pistol_scene: PackedScene

@export var patrol: bool = true

@export var move: bool = true

#===============================================================================
# Constants & Variables
#===============================================================================
const SPEED: float = 5.0
const DETECTION_RADIUS: float = 20.0
const ATTACK_RADIUS: float = 10.0
const MOVE_DISTANCE: float = 10.0

var health: int = 100
var target: Node = null
var start_x: float
var direction: int = 1
var attack_cooldown: float = 0.5
var time_since_last_attack: float = 0.0
const GRAVITY = 60.0

var is_firing: bool = false # Tracks if the enemy is currently firing

@onready var anim_player: AnimationPlayer = $AnimationPlayer
@onready var health_bar: ProgressBar = $HealthBar

var current_weapon: Weapon = null # Changed from BulletWeapon to match Player's type

#===============================================================================
# Initialization
#===============================================================================
func _ready() -> void:
	start_x = global_transform.origin.x
	add_to_group("enemies")
	
	# Set up NPC enemy behavior.
	set_physics_process(true)
	# anim_player.play("idle")
	
	# Initialize the health bar.
	health_bar.max_value = health
	health_bar.value = health
	
	# Equip default weapon
	equip_default_weapon()

#===============================================================================
# Frame Process
#===============================================================================
func _process(delta: float) -> void:
	_update_healthbar_position()
	
	# Update attack cooldown timer
	if time_since_last_attack < attack_cooldown:
		time_since_last_attack += delta

#===============================================================================
# Physics Process
#===============================================================================
func _physics_process(delta: float) -> void:
	if target == null:
		target = _find_nearest_player()
		if target:
			emit_signal("enemy_detected", target)

	if target:
		_aim_at_target()
		var distance: float = global_transform.origin.distance_to(target.global_transform.origin)
		
		if distance <= ATTACK_RADIUS:
			_attack_target()
		elif is_firing:
			_stop_firing()
			
		# If within detection but outside attack range, move toward target
		elif distance <= DETECTION_RADIUS:
			_move_towards_target(delta)
		else:
			_stop_and_reset()
	else:
		_patrol(delta)

	_process_gravity(delta)
	move_and_slide()

func _process_gravity(delta):
	# Apply gravity when not on the ground.
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
		var floor_normal = get_floor_normal()
		var gravity_vector = Vector3.DOWN
		var natural_downhill = (gravity_vector - floor_normal * gravity_vector.dot(floor_normal)).normalized()
		var slope_angle = acos(floor_normal.dot(Vector3.UP))
		var gravity_accel = GRAVITY * sin(slope_angle)
		velocity = velocity + natural_downhill * gravity_accel * delta
		velocity.x = move_toward(velocity.x, 0, delta)
		velocity.z = move_toward(velocity.z, 0, delta)
#===============================================================================
# Weapon Handling
#===============================================================================
@onready var camera = $Camera3D

func equip_default_weapon():
	if pistol_scene:
		var pistol = pistol_scene.instantiate()
		$Camera3D/WeaponHolder.add_child(pistol)
		current_weapon = pistol

func _attack_target() -> void:
	# anim_player.play("attack")
	# Only start firing if we're not already firing and cooldown has elapsed
	if current_weapon and not is_firing and time_since_last_attack >= attack_cooldown:
		is_firing = true
		current_weapon.on_press() # Start firing
		time_since_last_attack = 0.0

func _stop_firing() -> void:
	if current_weapon and is_firing:
		is_firing = false
		current_weapon.on_release() # Stop firing
		
# Rotates the enemy to face the target
func _aim_at_target() -> void:
	if not target:
		return
	
	var direction = (target.global_transform.origin - global_transform.origin).normalized()
	var look_rotation = Vector3(direction.x, 0, direction.z) # Ignore Y-axis for aiming
	look_at(global_transform.origin + look_rotation, Vector3.UP)
	
	# Also aim the weapon holder if it exists
	if has_node("Camera3D/WeaponHolder"):
		var weapon_holder = $Camera3D/WeaponHolder
		var vertical_angle = atan2(direction.y, sqrt(direction.x * direction.x + direction.z * direction.z))
		weapon_holder.rotation.x = vertical_angle

#===============================================================================
# Patrol Movement
#===============================================================================
func _patrol(delta: float) -> void:
	if not patrol:
		return
		
	velocity.x = direction * SPEED * speed_multiplier
	velocity.z = 0 # Ensure Z velocity is zero during patrol

	if global_transform.origin.x >= start_x + MOVE_DISTANCE:
		direction = -1
	elif global_transform.origin.x <= start_x - MOVE_DISTANCE:
		direction = 1

	# anim_player.play("move")

#===============================================================================
# Stop Movement
#===============================================================================
func _stop_and_reset() -> void:
	velocity = Vector3.ZERO
	# anim_player.play("idle")
	
	# Stop shooting when no target
	if is_firing:
		_stop_firing()

#===============================================================================
# Targeting Methods
#===============================================================================
func _find_nearest_player() -> Node:
	var players: Array = get_tree().get_nodes_in_group("players")
	var nearest: Node = null
	var min_dist: float = INF
	
	for player in players:
		var dist: float = global_transform.origin.distance_to(player.global_transform.origin)
		if dist < min_dist and dist <= DETECTION_RADIUS:
			min_dist = dist
			nearest = player
			
	return nearest

#===============================================================================
# Movement
#===============================================================================
func _move_towards_target(delta: float) -> void:
	if not move or not target:
		return
		
	# anim_player.play("move")
	var move_direction: Vector3 = (target.global_transform.origin - global_transform.origin).normalized()
	
	# Keep the enemy on the ground plane
	move_direction.y = 0
	move_direction = move_direction.normalized()
	
	velocity = move_direction * SPEED * speed_multiplier

var speed_multiplier: float = 1.0
func set_speed_multiplier(multiplier: float) -> void:
	speed_multiplier = multiplier

#===============================================================================
# Damage & Death
#===============================================================================
func take_damage(amount: int) -> void:
	health -= amount
	health_bar.value = health
	
	if health <= 0:
		_die()
	else:
		# Optional: play hit animation or sound
		pass

func _die() -> void:
		# Stop any ongoing actions
		_stop_firing()
		velocity = Vector3.ZERO
		set_physics_process(false)
		
		# Optionally play death animation and signal death
		emit_signal("enemy_died")
		
		# Release any bullets parented to this enemy
		for child in get_children():
				if child is Bullet:
						remove_child(child)
						get_tree().current_scene.add_child(child)
						# Optionally, adjust the bullet’s transform if needed
						# so that it maintains its global position
						child.global_transform = child.global_transform
		
		# Finally, free the enemy
		queue_free()


#===============================================================================
# UI Positioning
#===============================================================================
func _update_healthbar_position() -> void:
	var camera: Camera3D = get_viewport().get_camera_3d()
	if camera and health_bar:
		var head_world_position: Vector3 = global_transform.origin + Vector3(0, 2.0, 0)
		var screen_position: Vector2 = camera.unproject_position(head_world_position)
		screen_position -= health_bar.size * 0.5
		health_bar.position = screen_position
