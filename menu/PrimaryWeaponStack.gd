extends CardStack
class_name PrimaryWeaponStack

func _ready() -> void:
	super._ready()
	InventorySingleton.inventory_changed.connect(_on_inventory_changed)
	_populate_cards()

func _populate_cards() -> void:
	# Remove existing cards.
	for card in get_cards():
		card.queue_free()

	# Create cards based on primary weapon's modules.
	for module in InventorySingleton.primary_weapon.modules:
		var card = WeaponModuleCard2D.new(module)
		card.module = module
		add_child(card)
	
	update_cards(false)

func _on_inventory_changed() -> void:
	_populate_cards()

# Override to update primary_weapon.modules from the new card order.
func _on_cards_reordered() -> void:
	var new_modules = []
	for card in get_cards():
		if card is WeaponModuleCard2D:
			new_modules.append(card.module)
	# Instead of assigning a new array, update the existing one.
	InventorySingleton.primary_weapon.modules.clear()
	InventorySingleton.primary_weapon.modules.append_array(new_modules)
