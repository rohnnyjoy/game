extends Node
class_name Inventory

signal inventory_changed


var primary_weapon: Weapon = preload("res://weapons/unique/OlReliable.tscn").instantiate(): set = _set_primary_weapon, get = _get_primary_weapon

var weapon_modules: Array[WeaponModule] = [
  BouncingModule.new(),
	ExplosiveModule.new(),
	HomingModule.new(),
	StickyModule.new(),
]: set = _set_weapon_modules, get = _get_weapon_modules

func _set_primary_weapon(weapon: Weapon):
	primary_weapon = weapon
	inventory_changed.emit()

func _get_primary_weapon() -> Weapon:
	return primary_weapon

func _set_weapon_modules(modules: Array[WeaponModule]):
	weapon_modules = modules
	inventory_changed.emit()

func _get_weapon_modules() -> Array[WeaponModule]:
	return weapon_modules
