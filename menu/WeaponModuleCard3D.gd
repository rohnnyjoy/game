extends Card3D
class_name WeaponModuleCard3D

@export var module: WeaponModule

func _init(_module: WeaponModule = null) -> void:
	module = _module
	# Ensure card_base is valid.
	if not card_base:
		card_base = CardCore.new()
	if module:
		# Associate the module's properties with the card.
		card_base.card_texture = module.card_texture
		card_base.card_description = module.module_description
		# Optionally set other module-specific data here.
