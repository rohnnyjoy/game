extends Node

@export var outline_layer_bit := 19
@export var main_camera: Camera3D
@export var outline_mask_vp: SubViewport
@export var outline_mask_cam: Camera3D
@export var depth_blit_mat: ShaderMaterial
@export var outline_mat: ShaderMaterial
const SUBVIEWPORT_USAGE_3D := 2
const SUBVIEWPORT_UPDATE_ALWAYS := 3

@export_range(0.25, 1.0, 0.05) var vp_scale := 0.5

var _mask_texture: Texture2D
var _mask_depth_texture: Texture2D
var _main_depth_texture: Texture2D

func _ready() -> void:
	_ensure_mask_world()
	if outline_mask_cam:
		outline_mask_cam.cull_mask = 1 << outline_layer_bit
	_configure_subviewport()
	_sync_cameras()
	_update_target_textures()
	_apply_uniforms()
	set_process(true)

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_SIZE_CHANGED:
		_configure_subviewport()
		_apply_screen_uniform()

func _process(_delta: float) -> void:
	_ensure_mask_world()
	_sync_cameras()
	var textures_changed := _update_target_textures()
	if textures_changed:
		_apply_texture_uniforms()
	_apply_camera_uniforms()

func _configure_subviewport() -> void:
	if outline_mask_vp:
		outline_mask_vp.usage = SUBVIEWPORT_USAGE_3D
		outline_mask_vp.disable_3d = false
		outline_mask_vp.render_target_update_mode = SUBVIEWPORT_UPDATE_ALWAYS
		var base_size := _get_main_viewport_size()
		if base_size == Vector2i.ZERO:
			var window := get_window()
			if window:
				base_size = window.size
		if base_size != Vector2i.ZERO:
			var scale := clamp(vp_scale, 0.25, 1.0)
			var scaled: Vector2 = Vector2(float(base_size.x), float(base_size.y)) * scale
			var target := Vector2i(max(1, int(round(scaled.x))), max(1, int(round(scaled.y))))
			outline_mask_vp.size = target
			_apply_mask_size_uniform()

func _sync_cameras() -> void:
	if not main_camera or not outline_mask_cam:
		return
	outline_mask_cam.global_transform = main_camera.global_transform
	outline_mask_cam.near = main_camera.near
	outline_mask_cam.far = main_camera.far
	outline_mask_cam.keep_aspect_mode = main_camera.keep_aspect_mode
	if main_camera.projection == Camera3D.PROJECTION_ORTHOGONAL:
		if outline_mask_cam.projection != Camera3D.PROJECTION_ORTHOGONAL:
			outline_mask_cam.projection = Camera3D.PROJECTION_ORTHOGONAL
		outline_mask_cam.size = main_camera.size
	else:
		if outline_mask_cam.projection != Camera3D.PROJECTION_PERSPECTIVE:
			outline_mask_cam.projection = Camera3D.PROJECTION_PERSPECTIVE
		outline_mask_cam.fov = main_camera.fov

func _update_target_textures() -> bool:
	var changed := false
	if outline_mask_vp:
		var mask_tex: ViewportTexture? = outline_mask_vp.get_texture()
		if mask_tex and mask_tex != _mask_texture:
			_mask_texture = mask_tex
			changed = true
		if mask_tex:
			var depth_tex: Texture2D? = mask_tex.get_depth_texture()
			if depth_tex and depth_tex != _mask_depth_texture:
				_mask_depth_texture = depth_tex
				changed = true
	if main_camera:
		var main_vp := main_camera.get_viewport()
		if main_vp:
			var main_tex: ViewportTexture? = main_vp.get_texture()
			if main_tex:
				var depth_tex_main: Texture2D? = main_tex.get_depth_texture()
				if depth_tex_main and depth_tex_main != _main_depth_texture:
					_main_depth_texture = depth_tex_main
					changed = true
	return changed

func _apply_uniforms() -> void:
	_apply_texture_uniforms()
	_apply_camera_uniforms()
	_apply_screen_uniform()
	_apply_mask_size_uniform()

func _apply_texture_uniforms() -> void:
	if depth_blit_mat and _mask_depth_texture:
		depth_blit_mat.set_shader_parameter("SVP_DEPTH", _mask_depth_texture)
	if outline_mat:
		if _mask_depth_texture:
			outline_mat.set_shader_parameter("MASK_DEPTH_TEX", _mask_depth_texture)
		if _main_depth_texture:
			outline_mat.set_shader_parameter("DEPTH_TEX", _main_depth_texture)

func _apply_camera_uniforms() -> void:
	if outline_mask_cam and depth_blit_mat:
		depth_blit_mat.set_shader_parameter("camera_z_near", outline_mask_cam.near)
		depth_blit_mat.set_shader_parameter("camera_z_far", outline_mask_cam.far)
		depth_blit_mat.set_shader_parameter("is_orthographic", outline_mask_cam.projection == Camera3D.PROJECTION_ORTHOGONAL)
	if outline_mat:
		if outline_mask_cam:
			outline_mat.set_shader_parameter("mask_camera_z_near", outline_mask_cam.near)
			outline_mat.set_shader_parameter("mask_camera_z_far", outline_mask_cam.far)
			outline_mat.set_shader_parameter("mask_is_orthographic", outline_mask_cam.projection == Camera3D.PROJECTION_ORTHOGONAL)
		if main_camera:
			outline_mat.set_shader_parameter("main_camera_z_near", main_camera.near)
			outline_mat.set_shader_parameter("main_camera_z_far", main_camera.far)
			outline_mat.set_shader_parameter("main_is_orthographic", main_camera.projection == Camera3D.PROJECTION_ORTHOGONAL)

func _apply_mask_size_uniform() -> void:
	if outline_mat and outline_mask_vp:
		outline_mat.set_shader_parameter("mask_viewport_size", Vector2(outline_mask_vp.size))

func _apply_screen_uniform() -> void:
	if not outline_mat:
		return
	var size := Vector2.ZERO
	if main_camera:
		var main_vp := main_camera.get_viewport()
		if main_vp:
			size = Vector2(main_vp.size)
	if size == Vector2.ZERO:
		var window := get_window()
		if window:
			size = Vector2(window.size)
	if size == Vector2.ZERO:
		size = Vector2.ONE
	outline_mat.set_shader_parameter("screen_size", size)

func _get_main_viewport_size() -> Vector2i:
	if main_camera:
		var vp := main_camera.get_viewport()
		if vp:
			return vp.size
	return Vector2i.ZERO

func _ensure_mask_world() -> void:
	if not main_camera or not outline_mask_vp:
		return
	var world := main_camera.get_world_3d()
	if world and outline_mask_vp.world_3d != world:
		outline_mask_vp.world_3d = world
