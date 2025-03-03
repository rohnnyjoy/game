extends BulletWeapon
class_name OlReliableBulletWeapon

func _ready() -> void:
	super._ready()
	unique_module = OlReliableModule.new()
	modules = [StickyModule.new(), HomingModule.new()]
