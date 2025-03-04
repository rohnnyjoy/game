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
    return bullet

func on_collision(collision: Dictionary, bullet: Bullet) -> void:
    var collider: Node = collision["collider"]
    if collider.is_in_group("enemies"):
        print("pen count", bullet.get_meta("penetration_count"))
        var penetration_count: int = bullet.get_meta("penetration_count")
        penetration_count += 1
        bullet.set_meta("penetration_count", penetration_count)

        bullet.damage *= (1.0 - damage_reduction)
        bullet.velocity *= velocity_factor

        if penetration_count >= max_penetrations:
            bullet.destroy_on_impact = true
    else:
        bullet.destroy_on_impact = true
