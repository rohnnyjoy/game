extends Panel
class_name CardStack

signal card_moved(card, from_stack, to_stack)
signal cards_changed(cards)

@export var offset: float = 120.0
@export var anim_duration: float = 0.3
@export var anim_duration_offset: float = 0.05
@export var stack_type: String = "inventory" # e.g., "inventory" or "weapon"

var tween: Tween
var cards: Array[BaseCard] = [] # Exposed array to track cards in the stack.

func _ready() -> void:
	add_to_group("CardStacks")
	update_cards(false)

# Repositions all cards in the stack.
func update_cards(animated: bool = true) -> void:
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
		var target_pos = Vector2(20 + i * offset, center_y - card.size.y * 0.5)
		if animated:
			tween.tween_property(card, "position", target_pos, anim_duration + i * anim_duration_offset)
		else:
			card.position = target_pos

func get_cards() -> Array[BaseCard]:
	var card_list: Array[BaseCard] = []
	for child in get_children():
		if child is BaseCard:
			card_list.append(child)
	return card_list

# Called when a card is dropped onto this stack.
func on_card_drop(card: Control) -> void:
	var local_x = card.global_position.x - global_position.x
	var new_index = int(clamp(round(local_x / offset), 0, get_card_count() - 1))

	var old_parent = card.get_parent()
	if old_parent != self:
		old_parent.remove_child(card)
		add_child(card)
		emit_signal("card_moved", card, old_parent, self)
	else:
		move_child(card, new_index)
	
	update_cards(true)
	# Let subclasses update the inventory.
	_on_cards_reordered()

func get_card_count() -> int:
	return cards.size()

# Virtual method for subclasses to override.
func _on_cards_reordered() -> void:
	# Base implementation does nothing.
	pass
