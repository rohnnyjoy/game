extends CanvasLayer

var inventory_visible: bool = false

func _ready() -> void:
    visible = inventory_visible
    if GlobalEvents:
        GlobalEvents.SetMenuOpen(inventory_visible)

func _process(_delta: float) -> void:
    if Input.is_action_just_pressed("toggle_inventory"):
        inventory_visible = !inventory_visible
        visible = inventory_visible
        if GlobalEvents:
            GlobalEvents.SetMenuOpen(inventory_visible)
        
        if inventory_visible:
            # Unlock the mouse and stop FPS input
            Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
        else:
            # Re-capture the mouse for FPS input
            Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
