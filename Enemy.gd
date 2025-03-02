extends CharacterBody3D

signal enemy_detected(target)
signal enemy_died()

#===============================================================================
# Constants & Variables
#===============================================================================
const SPEED: float = 5.0
const DETECTION_RADIUS: float = 20.0
const ATTACK_RADIUS: float = 2.0
const MOVE_DISTANCE: float = 10.0  # Distance the enemy will move side to side

var health: int = 5
var target: Node = null
var start_x: float
var direction: int = 1  # 1 = right, -1 = left

@onready var anim_player: AnimationPlayer = $AnimationPlayer

#===============================================================================
# Initialization
#===============================================================================
func _ready() -> void:
	start_x = global_transform.origin.x
	anim_player.play("idle")

#===============================================================================
# Physics Process
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
	velocity.x = direction * SPEED

	if global_transform.origin.x >= start_x + MOVE_DISTANCE:
		direction = -1  # Move left
	elif global_transform.origin.x <= start_x - MOVE_DISTANCE:
		direction = 1  # Move right

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
	velocity = move_direction * SPEED

func _attack_target() -> void:
	anim_player.play("attack")
	if target.has_method("take_damage"):
		target.take_damage(1)

#===============================================================================
# Damage & Death
#===============================================================================
func take_damage(amount: int) -> void:
	health -= amount
	if health <= 0:
		_die()

func _die() -> void:
	anim_player.play("die")
	emit_signal("enemy_died")
	queue_free()
