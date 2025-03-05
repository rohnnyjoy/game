extends WeaponModule
class_name TrackingModule

@export var tracking_strength: float = 0.1 # How quickly the bullet turns; 0.0 to 1.0
@export var max_ray_distance: float = 1000.0 # Maximum distance for the ray trace

func _init() -> void:
    module_description = "Bullets track the mouse cursor, adjusting their trajectory to hit it."

func on_physics_process(_delta: float, bullet: Bullet) -> void:
    if not bullet or not bullet.is_inside_tree():
        queue_free()
        return

    var viewport = bullet.get_viewport()
    if not viewport:
        return

    var camera: Camera3D = viewport.get_camera_3d()
    if not camera:
        return

    var mouse_pos: Vector2 = viewport.get_mouse_position()
    
    var ray_origin: Vector3 = camera.project_ray_origin(mouse_pos)
    var ray_direction: Vector3 = camera.project_ray_normal(mouse_pos)
    var ray_end: Vector3 = ray_origin + ray_direction * max_ray_distance

    var ray_query = PhysicsRayQueryParameters3D.new()
    ray_query.from = ray_origin
    ray_query.to = ray_end
    ray_query.exclude = [bullet]

    var space_state = bullet.get_world_3d().direct_space_state
    var collision = space_state.intersect_ray(ray_query)
    
    var target_point: Vector3 = collision.position if collision else ray_end
    var target_direction: Vector3 = (target_point - bullet.global_position).normalized()
    var desired_velocity: Vector3 = target_direction * bullet.speed
    bullet.velocity = bullet.velocity.lerp(desired_velocity, tracking_strength)
