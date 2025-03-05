extends BaseCard
class_name WeaponModuleCard

@export var module: WeaponModule

# Modify the _init function to accept a module parameter.
func _init(_module: WeaponModule) -> void:
	module = _module
	# Set the texture and tooltip early so that BaseCard's _ready() sees them.
	if module:
		card_texture = module.card_texture
		card_description = module.module_description

func _ready() -> void:
	# Now, when super._ready() is called, card_texture is already set.
	super._ready()
