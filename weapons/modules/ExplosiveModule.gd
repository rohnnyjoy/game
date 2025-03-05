extends WeaponModule
class_name ExplosiveModule

@export var explosion_radius: float = 1.0
@export var explosion_damage_multiplier: float = 0.25

func _init() -> void:
	card_texture = preload("res://icons/explosive.png")
	module_description = "Attacks explode on impact, dealing 25% damage in a 2-meter radius."

func on_collision(collision: Dictionary, bullet: Bullet) -> void:
	var impact_point: Vector3 = collision.position
	var aoe_damage = bullet.damage * explosion_damage_multiplier
	spawn_explosion(impact_point, bullet.get_tree())
	var enemies = bullet.get_tree().get_nodes_in_group("enemies")
	for enemy in enemies:
		if enemy.is_in_group("enemies") and enemy.has_method("take_damage"):
			if enemy.global_position.distance_to(impact_point) <= explosion_radius:
				enemy.take_damage(aoe_damage)

func spawn_explosion(position: Vector3, tree: SceneTree) -> void:
	var explosion_area = Area3D.new()
	explosion_area.name = "ExplosionEffect"
	explosion_area.collision_layer = 0
	explosion_area.collision_mask = 0

	var visual_radius = explosion_radius * randf_range(0.8, 1)
	var explosion_instance = MeshInstance3D.new()
	var sphere_mesh = SphereMesh.new()
	sphere_mesh.radial_segments = int(randf_range(4, 8))
	sphere_mesh.rings = int(randf_range(2, 4))
	sphere_mesh.radius = visual_radius
	sphere_mesh.height = visual_radius * 2
	explosion_instance.mesh = sphere_mesh

	var material = StandardMaterial3D.new()
	var random_green = lerp(0.5, 1.0, randf())
	var explosion_color = Color(1, random_green, 0, 0.5)
	material.albedo_color = explosion_color
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

	var initial_scale_factor = 0.1 * randf_range(0.8, 1.2)
	explosion_instance.scale = Vector3(initial_scale_factor, initial_scale_factor, initial_scale_factor)

	var tween = explosion_instance.create_tween()
	var final_scale_factor = 3 * randf_range(0.8, 1.2)
	tween.tween_property(explosion_instance, "scale", Vector3(final_scale_factor, final_scale_factor, final_scale_factor), 0.02) \
	.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_OUT)
	tween.tween_property(material, "albedo_color", Color(1, random_green, 0, 0), 0.2) \
	.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_OUT)
	tween.finished.connect(Callable(explosion_area, "queue_free"))
