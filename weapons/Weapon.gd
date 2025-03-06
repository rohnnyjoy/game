extends Node3D
class_name Weapon

@export var unique_module: WeaponModule = WeaponModule.new()
@export var modules: Array[WeaponModule] = []
@export var fire_rate: float = 0.5
@export var reload_speed: float = 2
@export var ammo: int = 10
@export var damage: float = 10

# New variables to track current ammo and reload state.
var current_ammo: int
var reloading: bool = false

func _ready() -> void:
	current_ammo = get_weapon_config().ammo

func on_press() -> void:
	print("on_press not implemented")

func on_release() -> void:
	print("on_release not implemented")

func get_weapon_config() -> WeaponConfig:
	var config = WeaponConfig.new()
	for module in [unique_module] + modules:
		config = module.modify_weapon(config)
	return config
