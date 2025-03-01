extends CharacterBody3D

signal enemy_detected(target)
signal enemy_died()

#===============================================================================
# Constants & Variables
#===============================================================================
const SPEED: float = 5.0
const DETECTION_RADIUS: float = 20.0
const ATTACK_RADIUS: float = 2.0

var health: int = 5
var target: Node = null

@onready var anim_player: AnimationPlayer = $AnimationPlayer

#===============================================================================
# Initialization
#===============================================================================
func _ready() -> void:
	# Set up NPC enemy behavior.
	set_physics_process(true)
	anim_player.play("idle")

#===============================================================================
# Physics Process
#===============================================================================
func _physics_process(delta: float) -> void:
	if target == null:
		# Look for the nearest player if no target is set.
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
			# Target out of range; stop moving.
			velocity = Vector3.ZERO
			anim_player.play("idle")
			target = null
	else:
		# No target found; remain idle.
		anim_player.play("idle")

#===============================================================================
# Targeting Methods
#===============================================================================
func _find_nearest_player() -> Node:
	# Assumes all player nodes are in the "players" group.
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
	if target:
		anim_player.play("move")
		var direction: Vector3 = (target.global_transform.origin - global_transform.origin).normalized()
		velocity = direction * SPEED
		move_and_slide()
		
func _attack_target() -> void:
	# Play attack animation and inflict damage to the target.
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
	# Optionally delay removal to allow death animation.
	queue_free()
