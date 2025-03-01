# Pistol.gd
extends Weapon

@export var BulletScene: PackedScene
@onready var muzzle: Node3D = $Muzzle
@onready var muzzle_flash = $MuzzleFlash

func use() -> void:
	var bullet = BulletScene.instantiate()
	bullet.global_transform = muzzle.global_transform
	get_tree().current_scene.add_child(bullet)
	
	if bullet.has_method("initialize"):
		var direction = - muzzle.global_transform.basis.z
		bullet.initialize(direction, 0.1, 100, 1, Color.RED)
	
	# Trigger the configurable muzzle flash.
	if muzzle_flash and muzzle_flash.has_method("trigger_flash"):
		muzzle_flash.trigger_flash()
	
	print("Pistol fired!")
