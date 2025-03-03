extends CharacterBody3D
class_name Bullet

@export var life_time: float = 30.0
@export var gravity: float = 0.0
@export var direction: Vector3 = Vector3.FORWARD
@export var speed: float = 20.0
@export var damage: float = 1.0
@export var color: Color = Color(1, 1, 1)
@export var destroy_on_impact: bool = true

@export var radius: float = 0.5:
    set = set_radius, get = get_radius
var _radius: float = 0.5

@export var trails: Array = []
@export var trail_gradient: Gradient = Gradient.new()
@export var modules: Array[WeaponModule] = []

var _mesh: MeshInstance3D
var collision_handlers: Array = []

# New variable to store last collider id
var _last_collision_collider_id: int = -1

func set_radius(new_radius: float) -> void:
    _radius = new_radius
    for trail in trails:
        trail.base_width = new_radius

func get_radius() -> float:
    return _radius

func _init() -> void:
    var default_trail = Trail.new()
    default_trail.gradient = Gradient.new()
    default_trail.gradient.set_color(1, Color.WHITE)
    default_trail.gradient.set_color(0, Color.YELLOW)
    default_trail.base_width = radius
    default_trail.lifetime = 0.1
    trails.append(default_trail)

func _ready() -> void:
    set_process(true)
    set_physics_process(true)
    scale = Vector3.ONE

    # Apply a random rotation
    randomize()
    rotation_degrees = Vector3(
        randf_range(0, 360),
        randf_range(0, 360),
        randf_range(0, 360)
    )

    _mesh = MeshInstance3D.new()
    var sphere_mesh = SphereMesh.new()
    sphere_mesh.radius = radius
    sphere_mesh.height = radius * 2
    sphere_mesh.radial_segments = 4
    sphere_mesh.rings = 2
    _mesh.mesh = sphere_mesh
    _mesh.scale = Vector3.ONE
    add_child(_mesh)
    
    var mat = StandardMaterial3D.new()
    mat.albedo_color = color
    mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
    _mesh.material_override = mat
    
    var collision_shape = CollisionShape3D.new()
    var sphere_shape = SphereShape3D.new()
    sphere_shape.radius = radius + 0.5
    collision_shape.shape = sphere_shape
    add_child(collision_shape)

    # Disable physical collision so the bullet won't get stuck
    collision_layer = 0
    collision_mask = 0
    
    var detection_area = Area3D.new()
    var area_collision_shape = CollisionShape3D.new()
    var area_sphere = SphereShape3D.new()
    area_sphere.radius = radius + 0.5
    area_collision_shape.shape = area_sphere
    detection_area.add_child(area_collision_shape)
    detection_area.collision_layer = 0
    detection_area.collision_mask = 1
    add_child(detection_area)
    
    velocity = direction.normalized() * speed

    for module in modules:
        await module.on_fire(self)
    
    # Wait one frame so that the bullet and its trails are fully initialized in the scene
    await get_tree().process_frame
    
    for trail in trails:
        trail.initialize()
        self.add_child(trail)
    
    # Bullet self-destruction after life_time
    await get_tree().create_timer(life_time).timeout
    _cleanup()


func _physics_process(delta: float) -> void:
    velocity += Vector3.DOWN * gravity * delta
    
    var current_position = global_transform.origin
    var predicted_motion = velocity * delta
    var predicted_position = current_position + predicted_motion
    
    var query = PhysicsRayQueryParameters3D.new()
    query.from = current_position
    query.to = predicted_position
    query.exclude = [self]
    
    var collision = get_world_3d().direct_space_state.intersect_ray(query)
    if collision:
        # Check if the collider is the same as last frame.
        # print("Collision id", collision.collider_id, "last id", _last_collision_collider_id, "collider", collision.collider, "enemy", collision.collider.is_in_group("enemy"))
        if collision.collider.is_in_group("enemies") && collision.collider_id == _last_collision_collider_id:
            print("Skipping")
            # Skip triggering collision events and simply update position.
            global_transform.origin = predicted_position
        else:
            #  teleport bullet to the collision point
            global_transform.origin = collision.position

            _last_collision_collider_id = collision.collider_id
            
            var collision_data = {
                "position": collision.position,
                "normal": collision.normal,
                "collider_id": collision.collider_id,
                "collider": collision.collider,
                "rid": collision.rid,
            }
            
            for module in modules:
                await module.on_collision(collision_data, self)
    
            _on_bullet_collision(collision_data, self)
            
            if destroy_on_impact:
                _cleanup()
                return
    else:
        # Reset the last collision ID when no collision occurs.
        # _last_collision_collider_id = -1
        global_transform.origin = predicted_position

    for module in modules:
        await module.on_physics_process(delta, self)

    _process_enemies_inside()

func _on_bullet_collision(collision: Dictionary, bullet: Bullet) -> void:
    var hit = collision.get("collider")
    if !is_instance_valid(hit):
        return
    if hit and hit.is_in_group("enemies"):
        if hit.has_method("take_damage"):
            hit.take_damage(damage)
    
func _process_enemies_inside() -> void:
    var now_ms: int = Time.get_ticks_msec()
    for enemy in get_overlapping_enemies():
        pass
                
func get_overlapping_enemies() -> Array[Node3D]:
    var result: Array[Node3D] = []
    for child in get_children():
        if child is Area3D:
            var bodies := (child as Area3D).get_overlapping_bodies()
            for b in bodies:
                if b.is_in_group("enemies"):
                    result.append(b)
    return result

func _cleanup() -> void:
    for t in trails:
        if is_instance_valid(t) and t.has_method("stop_trail"):
            t.stop_trail()
    queue_free()
