extends Card3D
class_name WeaponModuleCard3D

@export var module: WeaponModule

func _init(_module: WeaponModule = null) -> void:
	module = _module
	# Ensure card_base is valid.
	if not card_base:
		card_base = CardCore.new()
	if module:
		# Associate the module's properties with the card.
		card_base.card_texture = module.card_texture
		card_base.card_description = module.card_description
		# Optionally set other module-specific data here.

# Use an Area3D child for input detection.
# For example, attach this script to the root node and ensure you have an Area3D node
# named "InputArea" with a CollisionShape3D set up.
func _ready() -> void:
	# Connect the input_event signal from the child Area3D.
	$InputArea.input_event.connect(_on_input_event)

func _on_input_event(_camera: Camera3D, event: InputEvent, _click_position: Vector3, _click_normal: Vector3, _shape_idx: int) -> void:
	if event is InputEventAction and event.action == "interact" and event.pressed:
		interact()

func interact() -> void:
	# Add the module to the inventory.
	InventorySingleton.weapon_modules.append(module)
	# Emit the inventory_changed signal to update any inventory UI.
	InventorySingleton.inventory_changed.emit()
	# Optionally, play a pickup animation or sound here.
	# Remove this 3D card from the scene.
	queue_free()
