extends CardStack
class_name InventoryStack

func _ready() -> void:
	super._ready()
	InventorySingleton.inventory_changed.connect(_on_inventory_changed)
	_populate_cards()

func _populate_cards() -> void:
	# Remove existing cards.
	for card in get_cards():
		card.queue_free()

	# Create a card for each module in weapon_modules.
	for module in InventorySingleton.weapon_modules:
		var card = WeaponModuleCard.new(module)
		card.set_card_color(Color(1, 1, 1))
		add_child(card)
	
	update_cards(false)

func _on_inventory_changed() -> void:
	_populate_cards()

# Override to update InventorySingleton.weapon_modules.
func _on_cards_reordered() -> void:
	var new_modules = []
	for card in get_cards():
		if card is WeaponModuleCard:
			new_modules.append(card.module)
	InventorySingleton.weapon_modules.clear()
	InventorySingleton.weapon_modules.append_array(new_modules)
