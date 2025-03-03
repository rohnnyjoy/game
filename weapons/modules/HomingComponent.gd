# HomingComponent.gd
extends Node
class_name HomingComponent

var bullet: Bullet
var homing_radius: float = 10.0
var tracking_strength: float = 0.1

# func _physics_process(_delta: float) -> void:
