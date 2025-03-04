extends CardStack
class_name PrimaryWeaponStack

func _ready() -> void:
	super._ready()
	var root_scene = get_tree().current_scene
	var player = root_scene.find_child("Player", true, false)
	for module in player.current_weapon.modules:
			var card = WeaponModuleCard.new()
			card.module = module
			card.set_card_color(Color(0.5, 0.5, 0.5))
			add_child(card)
	update_cards(false)

func on_cards_changed(new_cards: Array[BaseCard]) -> void:
	super.on_cards_changed(new_cards)
	var root_scene = get_tree().current_scene
	var player = root_scene.find_child("Player", true, false)
	var new_modules: Array[WeaponModule] = []
	for card in new_cards:
		if card is WeaponModuleCard:
			new_modules.append(card.module)
	player.current_weapon.modules = new_modules
