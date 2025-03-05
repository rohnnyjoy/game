extends BulletWeapon
class_name OlReliableBulletWeapon

func _init() -> void:
	# super._init()
	unique_module = OlReliableModule.new()
	# modules = [PenetratingModule.new(), ExplosiveModule.new()]
	# modules = [BouncingModule.new(), TrackingModule.new(), SlowModule.new()]
	# modules = [BouncingModule.new(), ExplosiveModule.new()]
	modules = [PenetratingModule.new()]
