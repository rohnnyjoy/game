extends CharacterBody3D
class_name Enemy

signal enemy_detected(target)
signal enemy_died()

@export var patrol: bool = true

#===============================================================================
# Constants & Variables
#===============================================================================
const SPEED: float = 5.0
const DETECTION_RADIUS: float = 20.0
const ATTACK_RADIUS: float = 2.0
const MOVE_DISTANCE: float = 10.0 # Distance the enemy will move side to side

var health: int = 100
var target: Node = null
var start_x: float
var direction: int = 1 # 1 = right, -1 = left

@onready var anim_player: AnimationPlayer = $AnimationPlayer
@onready var health_bar: ProgressBar = $HealthBar

#===============================================================================
# Initialization
#===============================================================================
func _ready() -> void:
	start_x = global_transform.origin.x
	add_to_group("enemies")
	
	# Set up NPC enemy behavior.
	set_physics_process(true)
	anim_player.play("idle")
	
	# Initialize the health bar.
	health_bar.max_value = health
	health_bar.value = health

#===============================================================================
# Frame Process (for UI updates)
#===============================================================================
func _process(delta: float) -> void:
	_update_healthbar_position()

#===============================================================================
# Physics Process (for movement, targeting, and combat)
#===============================================================================
func _physics_process(delta: float) -> void:
	if target == null:
		target = _find_nearest_player()
		if target:
			emit_signal("enemy_detected", target)
	
	if target:
		var distance: float = global_transform.origin.distance_to(target.global_transform.origin)
		if distance <= ATTACK_RADIUS:
			_attack_target()
		elif distance <= DETECTION_RADIUS:
			_move_towards_target(delta)
		else:
			_stop_and_reset()
	else:
		_patrol(delta)

	move_and_slide()

#===============================================================================
# Patrol Movement
#===============================================================================
func _patrol(delta: float) -> void:
	if not patrol:
		return
	velocity.x = direction * SPEED * speed_multiplier

	if global_transform.origin.x >= start_x + MOVE_DISTANCE:
		direction = -1 # Move left
	elif global_transform.origin.x <= start_x - MOVE_DISTANCE:
		direction = 1 # Move right

	anim_player.play("move")

#===============================================================================
# Stop Movement
#===============================================================================
func _stop_and_reset() -> void:
	velocity = Vector3.ZERO
	anim_player.play("idle")
	target = null

#===============================================================================
# Targeting Methods
#===============================================================================
func _find_nearest_player() -> Node:
	var players: Array = get_tree().get_nodes_in_group("players")
	var nearest: Node = null
	var min_dist: float = INF
	for player in players:
		var dist: float = global_transform.origin.distance_to(player.global_transform.origin)
		if dist < min_dist:
			min_dist = dist
			nearest = player
	return nearest

#===============================================================================
# Movement & Attack
#===============================================================================
func _move_towards_target(delta: float) -> void:
	anim_player.play("move")
	var move_direction: Vector3 = (target.global_transform.origin - global_transform.origin).normalized()
	velocity = move_direction * SPEED * speed_multiplier

func _attack_target() -> void:
	anim_player.play("attack")
	if target.has_method("take_damage"):
		target.take_damage(1)
		
var speed_multiplier: float = 1.0 # Default multiplier
func set_speed_multiplier(multiplier: float) -> void:
	speed_multiplier = multiplier


#===============================================================================
# Damage & Death
#===============================================================================
func take_damage(amount: int) -> void:
	health -= amount
	health_bar.value = health # Update the health bar display
	if health <= 0:
		_die()

func _die() -> void:
	anim_player.play("die")
	emit_signal("enemy_died")
	queue_free()

#===============================================================================
# UI Positioning
#===============================================================================
func _update_healthbar_position() -> void:
	# Get the active camera.
	var camera: Camera3D = get_viewport().get_camera_3d()
	if camera and health_bar:
		# Define an offset so the health bar appears above the enemy’s head.
		var head_world_position: Vector3 = global_transform.origin + Vector3(0, 2.0, 0)
		# Convert the enemy's head position to screen coordinates.
		var screen_position: Vector2 = camera.unproject_position(head_world_position)
		# Center the health bar by subtracting half of its size.
		screen_position -= health_bar.size * 0.5
		# Update the HealthBar's position.
		health_bar.position = screen_position
