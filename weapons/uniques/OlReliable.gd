extends BulletWeapon
class_name OlReliableBulletWeapon

func _ready() -> void:
	super._ready()
	unique_module = OlReliableModule.new()
	modules = [BouncingModule.new(), ExplosiveModule.new()]
	modules = [BouncingModule.new(), TrackingModule.new(), SlowModule.new()]
	# modules = [PenetratingModule.new(), ExplosiveModule.new()]
	# modules = [BouncingModule.new()]