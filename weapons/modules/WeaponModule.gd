extends Node
class_name WeaponModule

@export var module_name: String = "Base Module"
@export var module_description: String = "Base Module"

# This base module can be extended to modify bullets or weapons.

func modify_bullet(bullet: Bullet) -> Bullet:
	return bullet

func modify_weapon(config: WeaponConfig) -> WeaponConfig:
	return config

func on_fire(_bullet: Bullet) -> void:
	pass

# New on_impact callback that modules can override for impact effects.
func on_collision(_collision: Dictionary, _bullet: Bullet) -> void:
	pass

func on_physics_process(_delta: float, _bullet: Bullet) -> void:
	pass