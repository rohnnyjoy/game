extends WeaponModule
class_name AimbotModule

@export var aim_cone_angle: float = deg_to_rad(45) # Maximum allowed angle from bullet's current direction.
@export var vertical_offset: float = 0.0 # Vertical adjustment (0 aims at the enemy's center).
@export var target_line_width: float = 0.01 # Width of the target line.
@export var target_line_duration: float = 0.01 # Duration to show the target line.

func on_collision(_collision: Dictionary, bullet: Bullet) -> void:
    aimbot(bullet)

func on_fire(bullet: Bullet) -> void:
    aimbot(bullet)

func aimbot(bullet: Bullet) -> void:
    # Use bullet's current velocity as the base direction.
    var origin: Vector3 = bullet.global_transform.origin
    var base_direction: Vector3 = bullet.velocity.normalized()

    # Retrieve the last enemy this bullet locked onto (if any).
    var last_locked_enemy: Node = null
    if bullet.has_meta("last_locked_enemy"):
        var stored_enemy = bullet.get_meta("last_locked_enemy")
        if is_instance_valid(stored_enemy):
            last_locked_enemy = stored_enemy
        else:
            bullet.set_meta("last_locked_enemy", null)

    # We'll choose the best enemy not equal to last_locked_enemy if available.
    var best_enemy: Node = null
    var best_angle: float = aim_cone_angle

    # Also track the best candidate even if it's the same as last_locked_enemy.
    var fallback_enemy: Node = null
    var fallback_angle: float = aim_cone_angle

    # Get the physics space state for raycasting.
    var space_state = bullet.get_world_3d().direct_space_state

    # Look for an enemy within the cone.
    for enemy in bullet.get_tree().get_nodes_in_group("enemies"):
        if enemy is Node3D:
            var enemy_origin: Vector3 = enemy.global_transform.origin
            var to_enemy: Vector3 = (enemy_origin - origin).normalized()
            var angle: float = acos(base_direction.dot(to_enemy))
            # Check if enemy falls within the allowed cone.
            if angle < aim_cone_angle:
                # Determine the aim point on the enemy.
                var aim_point: Vector3
                if enemy.has_method("get_aabb"):
                    var aabb: AABB = enemy.get_aabb()
                    aim_point = enemy.to_global(aabb.position + aabb.size * 0.5)
                else:
                    aim_point = enemy_origin

                # Apply vertical offset.
                aim_point.y += vertical_offset

                # Prepare raycast parameters.
                var ray_params = PhysicsRayQueryParameters3D.new()
                ray_params.from = origin
                ray_params.to = aim_point

                # Build an exclusion list: exclude the bullet and every enemy except the current one.
                var enemy_list = bullet.get_tree().get_nodes_in_group("enemies")
                enemy_list.erase(enemy)
                ray_params.exclude = [bullet] + enemy_list

                # Raycast from bullet's origin to the aim point.
                var ray_result = space_state.intersect_ray(ray_params)
                if ray_result.size() == 0 or ray_result.collider == enemy:
                    # If enemy is not the last locked, check if it's the best candidate.
                    if enemy != last_locked_enemy:
                        if angle < best_angle:
                            best_angle = angle
                            best_enemy = enemy
                    # Regardless, track a fallback candidate.
                    if angle < fallback_angle:
                        fallback_angle = angle
                        fallback_enemy = enemy

    # Use best_enemy if found, otherwise fallback to last_locked_enemy candidate.
    if best_enemy:
        best_enemy = best_enemy
    elif fallback_enemy:
        best_enemy = fallback_enemy

    # If an enemy was found, adjust the bullet's velocity and flash the red line.
    if best_enemy:
        var aim_point: Vector3
        if best_enemy.has_method("get_aabb"):
            var aabb: AABB = best_enemy.get_aabb()
            aim_point = best_enemy.to_global(aabb.position + aabb.size * 0.5)
        else:
            aim_point = best_enemy.global_transform.origin

        # Apply vertical offset.
        aim_point.y += vertical_offset

        # Adjust bullet velocity.
        bullet.velocity = (aim_point - origin).normalized() * bullet.speed

        # Update the bullet's last locked enemy.
        bullet.set_meta("last_locked_enemy", best_enemy)

        # Flash a red line from the bullet to the target.
        await flash_line(bullet, origin, aim_point)

func flash_line(bullet: Node, start: Vector3, end: Vector3) -> void:
    var tree = bullet.get_tree()
    if not tree:
        return

    # Get a valid parent node using the current scene.
    var parent_node = tree.get_current_scene()
    if parent_node == null:
        parent_node = tree.get_root().get_child(0)

    # Create a MeshInstance3D with a BoxMesh.
    var line_instance = MeshInstance3D.new()
    var box = BoxMesh.new()
    
    # Set box size based on the target_line_width and distance.
    var thickness: float = target_line_width
    var distance: float = start.distance_to(end)
    box.size = Vector3(thickness, thickness, distance)
    line_instance.mesh = box

    # Position the box at the midpoint.
    var mid_point = (start + end) * 0.5
    var transform = Transform3D.IDENTITY
    transform.origin = mid_point

    # Align the box so its local Z axis points from start to end.
    var direction: Vector3 = (end - start).normalized()
    transform.basis = Basis().looking_at(direction, Vector3.UP)
    line_instance.transform = transform

    # Use an unshaded material and set depth_draw_mode to always draw.
    var material = StandardMaterial3D.new()
    material.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
    material.depth_draw_mode = StandardMaterial3D.DEPTH_DRAW_ALWAYS
    material.albedo_color = Color(1, 0, 0)
    line_instance.material_override = material

    parent_node.add_child(line_instance)
    
    # Debug print.
    print("Flash line added at: ", mid_point, " with distance: ", distance)
    
    # Keep the flash visible for target_line_duration seconds, then remove.
    await tree.create_timer(target_line_duration).timeout
    line_instance.queue_free()
