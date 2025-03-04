# Inventory.gd
extends CardStack
class_name Inventory

var modules: Array[WeaponModule] = [
	BouncingModule.new(),
	ExplosiveModule.new(),
	PenetratingModule.new(),
]

func _ready() -> void:
	# Call the parent _ready() to initialize the CardStack.
	super._ready()
	
	# Create and add a card for each module entry.
	for module in modules:
		print("Adding module: ", module)
		# Directly instantiate a new WeaponModuleCard.
		var card = WeaponModuleCard.new()

		# Assign the module to the card.
		card.module = module
		# Set the card's visual color.
		card.set_card_color(Color(0.5, 0.5, 0.5))
		
		# Add the card as a child of the inventory (which is a CardStack).
		add_child(card)
	
	# Update the layout of cards immediately.
	update_cards(false)
