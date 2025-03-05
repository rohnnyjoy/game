extends Panel
class_name CardStack

signal card_moved(card, from_stack, to_stack)
signal cards_changed(cards)

@export var offset: float = 120.0
@export var anim_duration: float = 0.3
@export var anim_duration_offset: float = 0.05
@export var suction_duration: float = 0.15 # Shorter duration for drop animation
@export var stack_type: String = "inventory" # e.g., "inventory" or "weapon"

var tween: Tween
var cards: Array[Card2D] = [] # Exposed array to track cards in the stack.

func _ready() -> void:
	add_to_group("CardStacks")
	update_cards(false)
func update_cards(animated: bool = true, use_suction: bool = false) -> void:
	var new_cards = get_cards()
	if new_cards != cards:
		cards = new_cards
		emit_signal("cards_changed", cards)
	
	if cards.is_empty():
		return

	var center_y = size.y * 0.5
	if tween and tween.is_running():
		tween.kill()
	tween = create_tween().set_parallel(true)
	
	for i in range(cards.size()):
		var card = cards[i]
		# Calculate target position based on the card’s index.
		var target_pos = Vector2(20 + i * offset, center_y - card.card_base.card_size.y * 0.5)
		if animated:
			var duration = suction_duration if use_suction else (anim_duration + i * anim_duration_offset)
			tween.tween_property(card, "position", target_pos, duration).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
			# Use proper ternary operator syntax.
			if card.get_global_rect().has_point(get_global_mouse_position()):
				tween.tween_property(card, "scale", Vector2(1.2, 1.2), duration).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
			else:
				tween.tween_property(card, "scale", Vector2.ONE, duration).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
		else:
			card.position = target_pos
			card.scale = Vector2(1.2, 1.2) if card.get_global_rect().has_point(get_global_mouse_position()) else Vector2.ONE

func get_cards() -> Array[Card2D]:
	var card_list: Array[Card2D] = []
	for child in get_children():
		if child is Card2D:
			card_list.append(child)
	return card_list

func on_card_drop(card: Control) -> void:
	# Compute where in the stack the card was dropped.
	var local_x = card.global_position.x - global_position.x
	var new_index = int(clamp(round(local_x / offset), 0, get_card_count() - 1))
	var old_parent = card.get_parent()
	if old_parent != self:
		# Remove the card from its old stack and add it to this one.
		old_parent.remove_child(card)
		add_child(card)
		emit_signal("card_moved", card, old_parent, self)
	else:
		# If it’s the same stack, simply reposition it.
		move_child(card, new_index)
	
	# Update the positions of the cards (with animation).
	update_cards(true, true)
	_on_cards_reordered()

func get_card_count() -> int:
	return cards.size()

func _on_cards_reordered() -> void:
	# Base implementation does nothing. Subclasses (like InventoryStack)
	# can override this to update the underlying data.
	pass
