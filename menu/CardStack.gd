extends Panel
class_name CardStack

signal card_moved(card, from_stack, to_stack) # Emitted when a card is moved between stacks.
signal cards_changed(cards) # Emitted when the card stack changes.

@export var offset: float = 120.0
@export var anim_duration: float = 0.3
@export var anim_duration_offset: float = 0.05
@export var stack_type: String = "inventory" # For example: "inventory" or "weapon"

var tween: Tween
var cards: Array[BaseCard] = [] # Exposed array to track cards in the stack.

func _ready() -> void:
	add_to_group("CardStacks")
	update_cards(false)

# This method repositions all children (cards) in the stack.
func update_cards(animated: bool = true) -> void:
	var new_cards = get_cards()
	
	# Check if cards have changed
	if new_cards != cards:
		cards = new_cards
		on_cards_changed(cards)
		emit_signal("cards_changed", cards)

	if cards.is_empty():
		return

	var center_y = size.y * 0.5
	
	if tween and tween.is_running():
		tween.kill()
	tween = create_tween().set_parallel(true)
	
	# Position each card horizontally with an offset.
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
		# If the card is coming from a different stack, reparent it.
		old_parent.remove_child(card)
		add_child(card)
		emit_signal("card_moved", card, old_parent, self)
	else:
		# Reorder within the same stack.
		move_child(card, new_index)
	
	update_cards(true)

func get_card_count() -> int:
	return cards.size()

# Hook for subclasses or external logic when cards change.
func on_cards_changed(_new_cards: Array[BaseCard]) -> void:
	pass