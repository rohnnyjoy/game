# WeaponModuleCard.gd
extends BaseCard
class_name WeaponModuleCard

@export var module: WeaponModule

func _ready() -> void:
	super._ready()
	if module:
		tooltip_text = module.module_description
