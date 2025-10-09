extends CanvasLayer

var inventory_visible: bool = false

func _ready() -> void:
    # Ensure this UI processes both when paused and unpaused
    process_mode = Node.PROCESS_MODE_ALWAYS
    _apply_inventory_visibility()

func _process(_delta: float) -> void:
    if Input.is_action_just_pressed("toggle_inventory"):
        if not inventory_visible and GlobalEvents and GlobalEvents.MenuOpen:
            return
        inventory_visible = !inventory_visible
        _apply_inventory_visibility()

func is_inventory_visible() -> bool:
    return inventory_visible

func close_inventory_if_open() -> void:
    if not inventory_visible:
        return
    inventory_visible = false
    _apply_inventory_visibility()

func _apply_inventory_visibility() -> void:
    visible = inventory_visible
    if GlobalEvents:
        GlobalEvents.SetMenuOpen(inventory_visible)

    if inventory_visible:
        # Unlock the mouse and stop FPS input
        Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
    else:
        # Re-capture the mouse for FPS input
        Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
