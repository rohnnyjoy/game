# # Inventory.gd
# extends CanvasLayer
# class_name Inventory

# var inventory_items = [
#     {"name": "Sword", "icon": preload("res://icons/dolphin.png")},
#     {"name": "Shield", "icon": preload("res://icons/turtle.png")},
# ]

# func _ready() -> void:
#     visible = false
#     update_inventory()

# func _input(event: InputEvent) -> void:
#     if event.is_action_pressed("toggle_inventory"):
#         visible = not visible

# func update_inventory() -> void:
#     var grid = $Inventory/Modules
#     # Clear any existing children in the grid.
#     for child in grid.get_children():
#         child.queue_free()
    
#     # Create new inventory items.
#     for item in inventory_items:
#         var inv_item = InventoryItem.new()
#         inv_item.item_data = item
#         inv_item.text = item["name"]
#         if item.has("icon") and item["icon"]:
#             inv_item.icon = item["icon"]
#         # Connect the pressed signal to a function that handles item selection.
#         inv_item.pressed.connect(self._on_item_pressed.bind(item))
#         grid.add_child(inv_item)

# func _on_item_pressed(item: Dictionary) -> void:
#     print("Selected item:", item["name"])
