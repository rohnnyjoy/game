extends WeaponModule
class_name HomingModule

@export var homing_radius: float = 10.0
@export var tracking_strength: float = 0.1 # How quickly the bullet turns; 0.0 to 1.0

func modify_bullet(bullet: Bullet) -> Bullet:
    # Create and attach the homing component to the bullet.
    var homing_component = HomingComponent.new()
    homing_component.homing_radius = homing_radius
    homing_component.tracking_strength = tracking_strength
    homing_component.bullet = bullet
    bullet.add_child(homing_component)
    return bullet
