extends Panel
class_name CardStack

@export var offset: float = 120.0
@export var anim_duration: float = 0.3
@export var anim_duration_offset: float = 0.05

@export var inventory: Array = [
	{"color": Color.RED},
	{"color": Color.GREEN},
	{"color": Color.BLUE},
	{"color": Color.YELLOW}
]

var tween: Tween

func _ready() -> void:
	add_to_group("CardStacks")
	
	# Clear any pre-existing cards (optional).
	for child in get_children():
		if child is Card:
			child.queue_free()
	
	# Instantiate a Card node for each item in the inventory.
	for item in inventory:
		var card = Card.new()
		card.card_color = item.get("color", Color.WHITE)
		add_child(card)
	
	update_cards(false)

func update_cards(animated: bool = true) -> void:
	var cards: Array = []
	for child in get_children():
		if child is Card:
			cards.append(child)
	if cards.size() == 0:
		return

	var center_y = size.y * 0.5
	
	if tween and tween.is_running():
		tween.kill()
	tween = create_tween().set_parallel(true)
	
	for i in range(cards.size()):
		var card = cards[i]
		var target_pos = Vector2(20 + i * offset, center_y - card.pivot_offset.y)
		
		if animated:
			tween.tween_property(
				card,
				"position",
				target_pos,
				anim_duration + i * anim_duration_offset
			)
		else:
			card.position = target_pos

func on_card_drop(card: Button) -> void:
	# Reorder the card based on drop x-position relative to this stack.
	var local_x = card.global_position.x - global_position.x
	var new_index = int(clamp(round(local_x / offset), 0, get_card_count() - 1))
	
	move_child(card, new_index)
	update_cards(true)

func get_card_count() -> int:
	var count = 0
	for child in get_children():
		if child is Card:
			count += 1
	return count
