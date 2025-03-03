extends BulletWeapon
class_name OlReliableBulletWeapon

func _ready() -> void:
	super._ready()
	unique_module = OlReliableModule.new()
	# modules = [PenetratingModule.new(), AimbotModule.new(), SpeedsterModule.new()]
	# modules = [BouncingModule.new(), HomingModule.new(), SlowModule.new()]
	modules = [PenetratingModule.new()]
	# modules = [BouncingModule.new()]