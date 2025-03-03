extends WeaponModule
class_name ExplosiveModule

@export var explosion_radius: float = 1.0 # Area of effect radius
@export var explosion_damage_multiplier: float = 0.5

# Called when a bullet collides with an enemy
func on_collision(collision: Dictionary, bullet: Bullet) -> void:
  var impact_point: Vector3 = collision.position
  var aoe_damage = bullet.damage * explosion_damage_multiplier
  
  # Generate the explosion effect at the collision point
  spawn_explosion(impact_point, bullet.get_tree())
  
  # Apply AOE damage to nearby enemies
  var enemies = bullet.get_tree().get_nodes_in_group("enemies")
  for enemy in enemies:
    if enemy.is_in_group("enemies") and enemy.has_method("take_damage"):
      if enemy.global_position.distance_to(impact_point) <= explosion_radius:
        enemy.take_damage(aoe_damage)

func spawn_explosion(position: Vector3, tree: SceneTree) -> void:
    var explosion_area = Area3D.new()
    explosion_area.name = "ExplosionEffect"
    # Set the collision layers/masks to avoid physics interactions.
    explosion_area.collision_layer = 0
    explosion_area.collision_mask = 0
    
    # Create the visual MeshInstance3D as a child.
    var explosion_instance = MeshInstance3D.new()
    var sphere_mesh = SphereMesh.new()
    sphere_mesh.radial_segments = 8
    sphere_mesh.rings = 4
    sphere_mesh.radius = explosion_radius
    sphere_mesh.height = explosion_radius * 2
    explosion_instance.mesh = sphere_mesh

    # Create a simple transparent material.
    var material = StandardMaterial3D.new()
    material.albedo_color = Color(1, 0.5, 0, 0.5)
    material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
    material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
    material.flags_transparent = true
    explosion_instance.material_override = material
    
    explosion_area.add_child(explosion_instance)
    explosion_area.position = position
    tree.current_scene.add_child(explosion_area)
    
    explosion_instance.rotation_degrees = Vector3(
        randf() * 360,
        randf() * 360,
        randf() * 360
    )
    explosion_instance.scale = Vector3(0.1, 0.1, 0.1)
    
    var tween = explosion_instance.create_tween()
    tween.tween_property(explosion_instance, "scale", Vector3(3, 3, 3), 0.02) \
        .set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_OUT)
    tween.tween_property(material, "albedo_color", Color(1, 0.5, 0, 0), 0.2) \
        .set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_OUT)
        
    tween.finished.connect(Callable(explosion_area, "queue_free"))
