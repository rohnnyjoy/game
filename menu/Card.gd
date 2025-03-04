extends Button
class_name Card

signal drop

@export var card_color: Color = Color.WHITE: set = set_card_color, get = get_card_color
var _card_color: Color = Color.WHITE

@export var card_size: Vector2 = Vector2(100, 150)
@export var rotation_sensitivity: float = 1.0
@export var rotation_lerp_factor: float = 0.5
@export var rotation_follow_speed: float = 10.0
@export var return_speed: float = 3.0
@export var max_angle: float = 15.0

var target_rotation: float = 0.0
var picked_up: bool = false
var offset: Vector2 = Vector2.ZERO

const MOUSE_LEFT: int = 1

func set_card_color(new_color: Color) -> void:
	_card_color = new_color
	modulate = Color(new_color.r, new_color.g, new_color.b, 1.0)

func get_card_color() -> Color:
	return _card_color

func _ready() -> void:
	custom_minimum_size = card_size
	# Because the pivot_offset is half of card_size, position.x/y is the *center* of the card.
	pivot_offset = card_size * 0.5
	mouse_filter = Control.MOUSE_FILTER_STOP
	
	var style_box = StyleBoxFlat.new()
	style_box.bg_color = Color(1, 1, 1, 1)
	add_theme_stylebox_override("normal", style_box)
	add_theme_stylebox_override("pressed", style_box)
	add_theme_stylebox_override("hover", style_box)
	
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)

func _process(delta: float) -> void:
	if picked_up:
		# Smoothly rotate toward the target rotation while dragging.
		rotation_degrees = lerp(float(rotation_degrees), target_rotation, rotation_follow_speed * delta)
	else:
		# When not dragging, rotate back to 0.
		target_rotation = lerp(target_rotation, 0.0, return_speed * delta)
		rotation_degrees = lerp(float(rotation_degrees), target_rotation, return_speed * delta)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_LEFT:
		if event.pressed:
			_on_drag_start()
		else:
			_on_drag_end()
	elif event is InputEventMouseMotion and picked_up:
		global_position = get_global_mouse_position() + offset
		target_rotation = lerp(
			target_rotation,
			event.relative.x * rotation_sensitivity,
			rotation_lerp_factor
		)
		target_rotation = clamp(target_rotation, - max_angle, max_angle)

func _on_drag_start() -> void:
	# Calculate how far the card's center is from the mouse so we can maintain that offset.
	offset = global_position - get_global_mouse_position()
	picked_up = true
	
	var tween = create_tween()
	tween.tween_property(self, "scale", Vector2(1.1, 1.1), 0.1)
	tooltip_text = ""

func _on_drag_end() -> void:
	picked_up = false
	
	# Scale and rotation reset animations.
	var tween = create_tween()
	tween.tween_property(self, "scale", Vector2.ONE, 0.3)
	tween.tween_property(self, "rotation_degrees", 0.0, 0.3)
	
	# Find which CardStack, if any, the mouse is currently over.
	var target_stack: CardStack = null
	for stack in get_tree().get_nodes_in_group("CardStacks"):
		if stack.get_global_rect().has_point(get_global_mouse_position()):
			target_stack = stack
			break
	
	if target_stack:
		# If the card is being dropped into a *different* stack:
		if target_stack != get_parent():
			var old_parent = get_parent()
			
			# Preserve the card's global position so it doesn't jump when reparented.
			var old_global_pos = global_position
			
			old_parent.remove_child(self)
			old_parent.update_cards(true) # Clean up the old stack's layout.
			
			target_stack.add_child(self)
			global_position = old_global_pos # Place the card exactly where it was dropped.
			
			# Now let the new stack reorder (tween) it into place.
			target_stack.on_card_drop(self)
		
		# If the card is dropped onto the same stack, just reorder.
		else:
			get_parent().on_card_drop(self)
	else:
		# Dropped outside any CardStack, so snap back.
		get_parent().update_cards(true)

func _on_mouse_entered() -> void:
	if not picked_up:
		tooltip_text = "This is your card description. It can hold valuable information."

func _on_mouse_exited() -> void:
	tooltip_text = ""
