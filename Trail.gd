extends Node3D
class_name Trail

# Trail sampling and appearance parameters.
@export var emit: bool = true
@export var distance: float = 0.1 # Minimum distance between sampled points
@export var segments: int = 20 # Desired number of segments
@export var lifetime: float = 0.5 # Lifetime of each trail point
@export var base_width: float = 0.5 # Base width of the trail
@export var tiled_texture: bool = false
@export var tiling: int = 0
@export var width_profile: Curve
@export var gradient: Gradient
@export var smoothing_iterations: int = 0
@export var smoothing_ratio: float = 0.25
@export var alignment: String = "View" # "View", "Normal", or "Object"
@export var axe: String = "Y" # Axis used for "Normal" or "Object" alignment: "X", "Y", or "Z"
@export var show_wireframe: bool = false
@export var wireframe_color: Color = Color(1, 1, 1, 1)
@export var wire_line_width: float = 1.0
# New configurable transparency mode.
@export var transparency_mode: int = BaseMaterial3D.TRANSPARENCY_DISABLED

# List of trail points.
var points: Array = []

# A MeshInstance3D child for rendering the trail.
var mesh_instance: MeshInstance3D

# Optional: A separate MeshInstance3D for the wireframe.
var wire_instance: MeshInstance3D

# Cache the camera for "View" alignment.
var cached_camera: Camera3D = null

# Class representing an individual trail point.
class TrailPoint:
	var transform: Transform3D
	var age: float
	func _init(transform: Transform3D, age: float) -> void:
		self.transform = transform
		self.age = age
	func update(delta: float) -> void:
		age -= delta

func initialize() -> void:
	self.top_level = true
	# Connect to the parent's "tree_exiting" signal.
	if is_instance_valid(get_parent()):
		get_parent().connect("tree_exiting", Callable(self, "_on_target_exiting"))

func _ready() -> void:
	# Ensure a target parent node exists.
	assert(get_parent() != null, "Trail requires a target node set via initialize()!")
	
	# Cache the camera once (if available) for performance.
	cached_camera = get_viewport().get_camera_3d()
	
	# Create a MeshInstance3D for the trail mesh.
	mesh_instance = MeshInstance3D.new()
	var mat = StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = transparency_mode
	mesh_instance.material_override = mat
	mesh_instance.shadow_casting_setting = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mesh_instance)
	
	# Optionally create a MeshInstance3D for the wireframe.
	if show_wireframe:
		wire_instance = MeshInstance3D.new()
		wire_instance.shadow_casting_setting = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(wire_instance)
	
	# Add two initial points so the trail starts rendering immediately.
	var init_tf = get_parent().global_transform
	add_point(init_tf)
	add_point(init_tf)

func _on_target_exiting() -> void:
	# Stop emitting new points when the parent is exiting.
	stop_trail()

func _process(delta: float) -> void:
	_update_points(delta)
	if emit:
		_emit(delta)
	else:
		_update_mesh()
		if points.size() == 0:
			queue_free()

func _emit(delta: float) -> void:
	var parent = get_parent()
	if not parent:
		return
	var current_tf = parent.global_transform
	# Only add a new point if moved enough.
	if points.size() == 0 or current_tf.origin.distance_squared_to(points[points.size() - 1].transform.origin) >= distance * distance:
		add_point(current_tf)
	_update_mesh()

func add_point(transform: Transform3D) -> void:
	points.push_back(TrailPoint.new(transform, lifetime))

func clear_points() -> void:
	points.clear()

func _update_points(delta: float) -> void:
	# Use a temporary array for points that still have age left.
	var new_points: Array = []
	for pt in points:
		pt.update(delta)
		if pt.age > 0:
			new_points.append(pt)
	points = new_points

func smooth_points(input_points: Array) -> Array:
	if input_points.size() < 3 or smoothing_iterations <= 0:
		return input_points
	var smoothed = input_points.duplicate()
	for i in range(smoothing_iterations):
		var new_points := [smoothed[0]]
		for j in range(1, smoothed.size() - 1):
			var A = smoothed[j - 1]
			var B = smoothed[j]
			var C = smoothed[j + 1]
			# Compute two intermediate points.
			var t1 = A.transform.interpolate_with(B.transform, 0.75)
			var t2 = B.transform.interpolate_with(C.transform, 0.25)
			var a1 = lerp(A.age, B.age, 0.75)
			var a2 = lerp(B.age, C.age, 0.25)
			new_points.append(TrailPoint.new(t1, a1))
			new_points.append(TrailPoint.new(t2, a2))
		new_points.push_back(smoothed[smoothed.size() - 1])
		smoothed = new_points
	return smoothed

func _prepare_geometry(prev_pt: TrailPoint, pt: TrailPoint, factor: float) -> Array:
	var normal: Vector3 = Vector3.ZERO
	if alignment == "View":
		if cached_camera:
			var cam_pos = cached_camera.global_transform.origin
			var path_dir = (pt.transform.origin - prev_pt.transform.origin).normalized()
			# Cache the midpoint.
			var mid_point = (pt.transform.origin + prev_pt.transform.origin) * 0.5
			normal = (cam_pos - mid_point).cross(path_dir).normalized()
		else:
			normal = Vector3.UP
	elif alignment == "Normal":
		match axe:
			"X": normal = pt.transform.basis.x.normalized()
			"Y": normal = pt.transform.basis.y.normalized()
			"Z": normal = pt.transform.basis.z.normalized()
	else: # "Object"
		if get_parent():
			match axe:
				"X": normal = get_parent().global_transform.basis.x.normalized()
				"Y": normal = get_parent().global_transform.basis.y.normalized()
				"Z": normal = get_parent().global_transform.basis.z.normalized()
		else:
			normal = Vector3.UP

	# Determine the current width; taper from 0 (tail) to full width (head).
	var current_width = base_width * factor
	if width_profile:
		current_width *= width_profile.interpolate(factor)
	
	var p1 = pt.transform.origin - normal * current_width
	var p2 = pt.transform.origin + normal * current_width
	return [p1, p2]

func _update_mesh() -> void:
	if points.size() < 2:
		mesh_instance.mesh = null
		if wire_instance:
			wire_instance.mesh = null
		return

	var st = SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLE_STRIP)

	# Smooth the points if needed.
	var pts = smooth_points(points)
	var u: float = 0.0

	for i in range(1, pts.size()):
		var factor = float(i) / float(pts.size() - 1)
		var col: Color = Color(1, 1, 1, 1)
		if gradient:
			col = gradient.sample(factor)
		st.set_color(col)
		
		var verts = _prepare_geometry(pts[i - 1], pts[i], factor)
		
		# Compute UV coordinates.
		if tiled_texture:
			if tiling > 0:
				factor *= tiling
			else:
				var travel = (pts[i - 1].transform.origin - pts[i].transform.origin).length()
				u += travel / base_width
				factor = u
		
		st.set_uv(Vector2(factor, 0))
		st.add_vertex(verts[0])
		st.set_uv(Vector2(factor, 1))
		st.add_vertex(verts[1])
	
	var new_mesh = st.commit()
	if new_mesh:
		mesh_instance.mesh = new_mesh

	# Update wireframe if enabled.
	if show_wireframe and wire_instance:
		var st_wire = SurfaceTool.new()
		st_wire.begin(Mesh.PRIMITIVE_LINE_STRIP)
		st_wire.set_color(wireframe_color)
		for pt in pts:
			st_wire.add_vertex(pt.transform.origin)
		var wire_mesh = st_wire.commit()
		if wire_mesh:
			wire_instance.mesh = wire_mesh

func stop_trail() -> void:
	emit = false
	# Optionally, initiate a fade-out timer for gradual removal.
