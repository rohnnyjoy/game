extends Node3D
class_name BulletTrail

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
# New configurable transparency mode (set to disabled by default).
@export var transparency_mode: int = BaseMaterial3D.TRANSPARENCY_DISABLED

# List of trail points.
var points: Array = []

# A MeshInstance3D child for rendering the trail.
var mesh_instance: MeshInstance3D

# Optional: A separate MeshInstance3D for the wireframe.
var wire_instance: MeshInstance3D

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
	# Connect to the target's "tree_exiting" signal using a Callable.
	if is_instance_valid(get_parent()):
		get_parent().connect("tree_exiting", Callable(self, "_onget_parent()_exiting"))

# Called once this node enters the scene tree.
func _ready() -> void:
	# Expect get_parent() to be set by initialize() before _ready() is called.
	assert(get_parent() != null, "Trail requires a target node set via initialize()!")
	
	# Create a MeshInstance3D for the trail mesh.
	mesh_instance = MeshInstance3D.new()
	# Create a material that uses vertex colors.
	var mat = StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	# Assign transparency from the exported transparency_mode variable.
	mat.transparency = transparency_mode
	mesh_instance.material_override = mat
	add_child(mesh_instance)
	
	# Optionally create a MeshInstance3D for the wireframe.
	if show_wireframe:
		wire_instance = MeshInstance3D.new()
		add_child(wire_instance)
	
	# Add initial points so the trail starts rendering immediately.
	add_point(get_parent().global_transform)
	add_point(get_parent().global_transform)

func _on_target_exiting() -> void:
	# When the target is about to be removed from the scene tree, stop emitting new points.
	stop_trail()

func _process(delta: float) -> void:
	_update_points(delta)
	if emit:
		_emit(delta)
	else:
		_update_mesh()
		# If not emitting and no points remain, free the trail node.
		if points.size() == 0:
			queue_free()

func _emit(delta: float) -> void:
	if not get_parent():
		return
	# Only add a new point if moved enough.
	if points.size() == 0 or get_parent().global_transform.origin.distance_squared_to(points[points.size() - 1].transform.origin) >= distance * distance:
		add_point(get_parent().global_transform)
	_update_mesh()

func add_point(transform: Transform3D) -> void:
	var pt = TrailPoint.new(transform, lifetime)
	points.push_back(pt)

func clear_points() -> void:
	points.clear()

func _update_points(delta: float) -> void:
	for pt in points:
		pt.update(delta)
	points = points.filter(func(pt):
		return pt.age > 0
	)

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
			# Create two intermediate points.
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
		var cam = get_viewport().get_camera_3d()
		if cam:
			var cam_pos = cam.global_transform.origin
			var path_dir = (pt.transform.origin - prev_pt.transform.origin).normalized()
			normal = (cam_pos - (pt.transform.origin + prev_pt.transform.origin) * 0.5).cross(path_dir).normalized()
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

	# Width goes from 0 at the tail (factor=0) to full base_width at the head (factor=1).
	var current_width = base_width * factor
	if width_profile:
		current_width *= width_profile.interpolate(factor)
	
	# The width is applied equally to both sides, perpendicular to the trail direction.
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

	var pts = smooth_points(points)
	var u: float = 0.0

	for i in range(1, pts.size()):
		var factor = float(i) / float(pts.size() - 1)
		var col: Color = Color(1, 0, 0, 1)
		if gradient:
			col = gradient.sample(factor)
		st.set_color(col)
		
		# Taper the width according to factor.
		var verts = _prepare_geometry(pts[i - 1], pts[i], factor)
		
		# Handle UV tiling or normal progression.
		if tiled_texture:
			if tiling > 0:
				if tiling != 0:
					factor *= tiling
			else:
				var travel = (pts[i - 1].transform.origin - pts[i].transform.origin).length()
				u += travel / base_width
				factor = u
		
		st.set_uv(Vector2(factor, 0))
		st.add_vertex(verts[0])
		st.set_uv(Vector2(factor, 1))
		st.add_vertex(verts[1])

	st.index() # finalize
	var new_mesh = st.commit()
	if new_mesh:
		mesh_instance.mesh = new_mesh

	if show_wireframe:
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
	# Optionally, you can start a fade-out timer here if you want a gradual removal.
