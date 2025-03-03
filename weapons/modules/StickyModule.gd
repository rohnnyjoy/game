extends WeaponModule
class_name StickyModule

var stick_duration: float = 2.0

func modify_bullet(bullet: Bullet) -> Bullet:
    bullet.set_meta("is_sticky", false)
    bullet.destroy_on_impact = false
    return bullet

func on_collision(collision: Dictionary, bullet: Bullet) -> void:
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

    if collision.collider:
        collision.collider.add_child(bullet)
    
    var tree = bullet.get_tree()
    if tree:
        await tree.create_timer(stick_duration).timeout
    else:
        print("Warning: bullet is not in the scene tree!")
    
    if is_instance_valid(bullet):
        bullet.set_meta("is_sticky", false)
