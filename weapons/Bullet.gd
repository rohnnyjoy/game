extends CharacterBody3D
class_name Bullet

@export var life_time: float = 3.0
@export var gravity: float = 0.0
@export var direction: Vector3 = Vector3.FORWARD
@export var speed: float = 20.0
@export var damage: float = 1.0
@export var color: Color = Color(1, 1, 1)
@export var destroy_on_impact: bool = true
@export var collision_cooldown: float = 0.2
@export var radius: float = 0.5:
    set = set_radius, get = get_radius

var _radius: float = 0.5
var _mesh: MeshInstance3D
var collision_handlers: Array = []

@export var trails: Array[BulletTrail] = []
@export var trail_gradient: Gradient = Gradient.new()

var last_enemy_damage_times: Dictionary = {}

func set_radius(new_radius: float) -> void:
    _radius = new_radius
    for trail in trails:
        if trail:
            trail.base_width = new_radius

func get_radius() -> float:
    return _radius

func _init() -> void:
    var default_trail := BulletTrail.new()
    default_trail.gradient = Gradient.new()
    default_trail.gradient.set_color(0, Color.WHITE)
    default_trail.gradient.set_color(1, Color.YELLOW)
    default_trail.base_width = radius
    default_trail.lifetime = 0.1
    trails.append(default_trail)

func _ready() -> void:
    set_process(true)
    set_physics_process(true)
    scale = Vector3.ONE

    _mesh = MeshInstance3D.new()
    var sphere_mesh := SphereMesh.new()
    sphere_mesh.radius = radius
    sphere_mesh.height = radius * 2
    _mesh.mesh = sphere_mesh
    _mesh.scale = Vector3.ONE
    add_child(_mesh)
    
    var mat := StandardMaterial3D.new()
    mat.albedo_color = color
    mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
    _mesh.material_override = mat
    
    var collision_shape := CollisionShape3D.new()
    var sphere_shape := SphereShape3D.new()
    sphere_shape.radius = radius + 0.5
    collision_shape.shape = sphere_shape
    add_child(collision_shape)

    collision_layer = 0
    collision_mask = 0
    
    var detection_area := Area3D.new()
    var area_collision_shape := CollisionShape3D.new()
    var area_sphere := SphereShape3D.new()
    area_sphere.radius = radius + 0.5
    area_collision_shape.shape = area_sphere
    detection_area.add_child(area_collision_shape)
    detection_area.collision_layer = 0
    detection_area.collision_mask = 1
    add_child(detection_area)
    detection_area.connect("body_entered", Callable(self, "_on_body_entered"))
    detection_area.connect("body_exited", Callable(self, "_on_body_exited"))
    
    velocity = direction.normalized() * speed
    
    await get_tree().process_frame
    
    var parent_node := get_parent()
    if parent_node:
        for trail in trails:
            if trail:
                trail.initialize(_mesh)
                parent_node.add_child(trail)
    
    await get_tree().create_timer(life_time).timeout
    _cleanup()

func _physics_process(delta: float) -> void:
    velocity += Vector3.DOWN * gravity * delta
    
    var current_position := global_transform.origin
    var predicted_motion := velocity * delta
    var predicted_position := current_position + predicted_motion
    
    var query := PhysicsRayQueryParameters3D.new()
    query.from = current_position
    query.to = predicted_position
    query.exclude = [self]

    var collision := get_world_3d().direct_space_state.intersect_ray(query)
    if collision:
        var collider: Node = collision.get("collider")
        if collider and not collider.is_in_group("enemies"):
            var collision_data := {
                "position": collision.get("position"),
                "normal": collision.get("normal"),
                "collider_id": collision.get("collider_id"),
                "collider": collider,
                "rid": collision.get("rid")
            }

            for handler in collision_handlers:
                if handler and handler.has_method("on_collision"):
                    handler.on_collision(collision_data, self)

            _on_bullet_collision(collision_data, self)
            
            if destroy_on_impact:
                _cleanup()
                return

            global_transform.origin = collision.position + velocity.normalized() * 0.001
            return
        else:
            global_transform.origin = predicted_position
    else:
        global_transform.origin = predicted_position

    _process_enemies_inside()
    _update_trails()

func _on_bullet_collision(collision: Dictionary, bullet: Bullet) -> void:
    var hit: Node = collision.get("collider")
    if hit and hit.is_in_group("enemies"):
        if hit.has_method("take_damage"):
            hit.take_damage(damage)

func _process_enemies_inside() -> void:
    var now_ms: int = Time.get_ticks_msec()
    for enemy in get_overlapping_enemies():
        var last_time_ms: int = int(last_enemy_damage_times.get(enemy, -1))
        if last_time_ms < 0:
            last_enemy_damage_times[enemy] = now_ms
        else:
            var elapsed: float = float(now_ms - last_time_ms) / 1000.0
            if elapsed < collision_cooldown:
                continue
            last_enemy_damage_times[enemy] = now_ms
        var collision_data := {
          "position": global_transform.origin,
          "normal": Vector3.ZERO,
          "collider": enemy
        }
        _on_bullet_collision(collision_data, self)

func _deal_damage_to_enemy(enemy: Node) -> void:
    if enemy.has_method("take_damage"):
        enemy.take_damage(damage)
    var collision_data := {
        "position": global_transform.origin,
        "normal": Vector3.ZERO,
        "collider": enemy
    }
    for handler in collision_handlers:
        if handler and handler.has_method("on_collision"):
            handler.on_collision(collision_data, self)

func get_overlapping_enemies() -> Array[Node3D]:
    var result: Array[Node3D] = []
    for child in get_children():
        if child is Area3D:
            var bodies := (child as Area3D).get_overlapping_bodies()
            for b in bodies:
                if b.is_in_group("enemies"):
                    result.append(b)
    return result

func _on_body_entered(body: Node) -> void:
    if body.is_in_group("enemies"):
        last_enemy_damage_times[body] = Time.get_ticks_msec()
        _deal_damage_to_enemy(body)

func _on_body_exited(body: Node) -> void:
    if body in last_enemy_damage_times:
        last_enemy_damage_times.erase(body)

func _update_trails() -> void:
    for trail in trails:
        if trail and trail.has_method("update_trail"):
            trail.update_trail(global_transform.origin)

func _cleanup() -> void:
    for trail in trails:
        if trail and trail.has_method("stop_trail"):
            trail.stop_trail()
    queue_free()
