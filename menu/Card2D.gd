extends Button
class_name Card2D

# A resource holding common card properties.
@export var card_base: CardCore

# 2D-specific exported properties.
@export var rotation_sensitivity: float = 1.0
@export var rotation_lerp_factor: float = 0.5
@export var rotation_follow_speed: float = 10.0
@export var return_speed: float = 3.0
@export var max_angle: float = 15.0
@export var drag_speed: float = 0.3

# Internal variables.
var picked_up: bool = false
var offset: Vector2 = Vector2.ZERO
var last_pos: Vector2
var target_rotation: float = 0.0

# Oscillator variables.
var oscillator_velocity: float = 0.0
var displacement: float = 0.0

signal drop

func _ready() -> void:
	# Ensure there is a CardCore instance.
	if not card_base:
		card_base = CardCore.new()
	
	custom_minimum_size = card_base.card_size
	pivot_offset = card_base.card_size * 0.5
	mouse_filter = Control.MOUSE_FILTER_STOP
	last_pos = position
	
	# Set style based on texture.
	if card_base.card_texture:
		var style_box = StyleBoxTexture.new()
		style_box.texture = card_base.card_texture
		add_theme_stylebox_override("normal", style_box)
		add_theme_stylebox_override("pressed", style_box)
		add_theme_stylebox_override("hover", style_box)
	else:
		var style_box = StyleBoxFlat.new()
		style_box.bg_color = card_base.card_color
		add_theme_stylebox_override("normal", style_box)
		add_theme_stylebox_override("pressed", style_box)
		add_theme_stylebox_override("hover", style_box)
	
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)

func _process(delta: float) -> void:
	if picked_up:
		_update_oscillator(delta)
		rotation_degrees = lerp(rotation_degrees, displacement, rotation_follow_speed * delta)
	else:
		target_rotation = lerp(target_rotation, 0.0, return_speed * delta)
		rotation_degrees = lerp(rotation_degrees, target_rotation, return_speed * delta)

func _update_oscillator(delta: float) -> void:
	var vel = (position - last_pos) / delta
	last_pos = position
	oscillator_velocity += vel.normalized().x # You can multiply by a factor if needed.
	var spring = 150.0
	var damp = 10.0
	var force = - spring * displacement - damp * oscillator_velocity
	oscillator_velocity += force * delta
	displacement += oscillator_velocity * delta

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == 1:
		if event.pressed:
			_on_drag_start()
		else:
			_on_drag_end()
	elif event is InputEventMouseMotion:
		if picked_up:
			var target_pos = get_global_mouse_position() + offset
			global_position = global_position.lerp(target_pos, drag_speed)
		else:
			var local_mouse = get_local_mouse_position()
			target_rotation = remap(local_mouse.x, 0.0, card_base.card_size.x, - max_angle, max_angle)
			target_rotation = lerp(target_rotation, event.relative.x * rotation_sensitivity, rotation_lerp_factor)
			target_rotation = clamp(target_rotation, - max_angle, max_angle)

func _on_drag_start() -> void:
	offset = global_position - get_global_mouse_position()
	picked_up = true
	oscillator_velocity = 0.0
	displacement = 0.0
	last_pos = position
	z_index = 999
	move_to_front()
	var tween = create_tween()
	tween.tween_property(self, "scale", Vector2(1.1, 1.1), 0.1)
	tooltip_text = ""

func _on_drag_end() -> void:
	print("Card dropped.")
	picked_up = false
	z_index = 0
	
	# Example: find a target card stack in a group.
	var target_stack: Node = null
	for stack in get_tree().get_nodes_in_group("CardStacks"):
		if stack.get_global_rect().has_point(get_global_mouse_position()):
			target_stack = stack
			break
	
	if target_stack:
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
		if get_parent().has_method("update_cards"):
			get_parent().update_cards(true)
			if get_parent().has_method("_on_cards_reordered"):
				get_parent()._on_cards_reordered()
	
	# Wait briefly and then reset scale.
	await get_tree().create_timer(0.05).timeout
	_reset_scale()
	if get_global_rect().has_point(get_global_mouse_position()):
		_on_mouse_entered()

func _reset_scale() -> void:
	var tween = create_tween()
	tween.tween_property(self, "scale", Vector2.ONE, 0.3)
	tween.tween_property(self, "rotation_degrees", 0.0, 0.3)

func _on_mouse_entered() -> void:
	if not picked_up:
		tooltip_text = card_base.card_description
	var tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_ELASTIC)
	tween.tween_property(self, "scale", Vector2(1.2, 1.2), 0.5)

func _on_mouse_exited() -> void:
	if picked_up:
		return
	tooltip_text = ""
	var tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK).set_parallel(true)
	tween.tween_property(self, "rotation_degrees", 0.0, 0.5)
	tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_ELASTIC)
	tween.tween_property(self, "scale", Vector2.ONE, 0.55)
