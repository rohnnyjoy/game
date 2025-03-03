extends WeaponModule
class_name PenetratingModule

var damage_reduction: float = 0.2
var velocity_factor: float = 0.9
var max_penetrations: int = 5
var collision_cooldown: float = 0.2

func modify_bullet(bullet: Bullet) -> Bullet:
    if not bullet.has_meta("penetration_count"):
        bullet.set_meta("penetration_count", 0)
    bullet.destroy_on_impact = false

    # Dictionary to store the last time (in milliseconds) the bullet dealt damage to each enemy
    bullet.set_meta("last_collision_times", {})
    
    return bullet

func on_collision(collision: Dictionary, bullet: Bullet) -> void:
    var collider: Node = collision["collider"]
    if collider.is_in_group("enemies"):
        var last_collision_times := bullet.get_meta("last_collision_times") as Dictionary
        if not last_collision_times:
            last_collision_times = {}
            bullet.set_meta("last_collision_times", last_collision_times)
        
        var current_time: int = Time.get_ticks_msec()
        
        # Check if we have dealt damage to this enemy recently
        if last_collision_times.has(collider):
            var elapsed: float = float(current_time - last_collision_times[collider]) / 1000.0
            if elapsed < collision_cooldown:
                return
        
        # Record the time of this damage instance
        last_collision_times[collider] = current_time
        
        # Apply the penetration logic
        var penetration_count: int = bullet.get_meta("penetration_count")
        penetration_count += 1
        bullet.set_meta("penetration_count", penetration_count)

        bullet.damage *= (1.0 - damage_reduction)
        bullet.velocity *= velocity_factor
        
        if penetration_count >= max_penetrations:
            bullet.queue_free()
    else:
        bullet.queue_free()
