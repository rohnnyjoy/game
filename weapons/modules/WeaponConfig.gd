extends Node
class_name WeaponConfig

@export var reload_speed: float = 0.5
@export var fire_rate: float = 3
@export var bullet_speed: float = 100
@export var ammo: int = 10
@export var on_reload_start: Callable = Callable()
@export var on_reload_end: Callable = Callable()
@export var damage: int = 10
@export var accuracy: float = 1.0

func add_reload_start_logic(new_logic: Callable) -> void:
	if on_reload_start.is_valid():
		var prev_logic = on_reload_start
		on_reload_start = func():
			prev_logic.call()
			new_logic.call()
	else:
		on_reload_start = new_logic

func add_reload_end_logic(new_logic: Callable) -> void:
	if on_reload_end.is_valid():
		var prev_logic = on_reload_end
		on_reload_end = func():
			prev_logic.call()
			new_logic.call()
	else:
		on_reload_end = new_logic
