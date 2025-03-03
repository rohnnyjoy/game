extends WeaponModule
class_name StickyModule

var stick_duration: float = 1.0
var collision_damage: float = 1

func modify_bullet(bullet: Bullet) -> Bullet:
    bullet.set_meta("is_sticky", false)
    return bullet

func on_collision(collision: Dictionary, bullet: Bullet) -> void:
    if collision.collider.is_in_group("enemies"):
        if is_instance_valid(collision.collider):
            collision.collider.take_damage(collision_damage)
    var initial_velocity = bullet.velocity
    
    # If the collision is with another bullet, try a secondary raycast to hit the object behind.
    if collision.collider is Bullet:
        var secondary_query: PhysicsRayQueryParameters3D = PhysicsRayQueryParameters3D.new()
        secondary_query.from = bullet.global_transform.origin
        secondary_query.to = bullet.global_transform.origin + bullet.velocity.normalized() * 100.0
        secondary_query.exclude = [bullet]
        secondary_query.collision_mask = 1
        var new_collision = bullet.get_world_3d().direct_space_state.intersect_ray(secondary_query)
        if new_collision:
            collision = new_collision
        else:
            return

    if bullet.get_meta("is_sticky"):
        return

    bullet.set_meta("is_sticky", true)
    
    bullet.velocity = Vector3.ZERO
    var normal: Vector3 = collision.get("normal", Vector3.UP)
    bullet.global_transform.origin = collision["position"] + normal * 0.01

    var original_parent = bullet.get_parent()
    
    if collision.collider:
        var local_transform = collision.collider.to_local(bullet.global_transform.origin)
        bullet.reparent(collision.collider) # Ensures correct hierarchy update
        bullet.transform.origin = local_transform # Preserve relative position

    var tree = bullet.get_tree()
    if tree:
        await tree.create_timer(stick_duration).timeout
    else:
        print("Warning: bullet is not in the scene tree!")
    
    if is_instance_valid(bullet):
        bullet.set_meta("is_sticky", false)
        if is_instance_valid(original_parent):
            var current_global_transform = bullet.global_transform
            bullet.get_parent().remove_child(bullet)
            original_parent.add_child(bullet)
            bullet.global_transform = current_global_transform
            bullet.velocity = initial_velocity