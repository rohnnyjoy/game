extends Node3D

#–––––– Exported Parameters ––––––
@export var emit: bool = true
@export var distance: float = 0.1               # Minimum distance to add a new sample.
@export var lifetime: float = 1.0               # Lifetime (seconds) of each trail sample.
@export var max_samples: int = 20               # Max number of samples to keep (performance).
@export var fixed_outline_vertex_count: int = 0 # If > 0, resample hull to this many points.
@export var base_width: float = 1.0             # The max width at the "head" of the trail.
@export var show_wireframe: bool = false        # Debug line strip showing sample origins.
@export var wireframe_color: Color = Color(1,1,1,1)
@export var wire_line_width: float = 1.0
@export var unshaded: bool = true               # If true, disables lighting on the trail material.

# A small non-zero scale for the tail. 0.05 means 5% of base_width at the very start.
# Increase if you want the tail bigger; decrease if you want it closer to a point (but risk spikes).
const MIN_TAIL_SCALE: float = 0.05

#–––––– Internal Variables ––––––
# Each trail sample is a dict with:
#   "origin": Vector3 – position
#   "outline": Array<Vector3> – 3D offsets (convex hull) relative to origin
#   "age": float – lifetime left
var trail_points: Array = []

# The target (MeshInstance3D) we are trailing.
var _target: MeshInstance3D

# Mesh for the trail geometry.
var mesh_instance: MeshInstance3D
# Optional wireframe instance for debugging.
var wire_instance: MeshInstance3D

func _ready() -> void:
	assert(_target != null, "Trail requires a target node set via initialize()!")
	mesh_instance = MeshInstance3D.new()
	var mat = StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED  # Render both sides
	mat.unshaded = unshaded
	mesh_instance.material_override = mat
	add_child(mesh_instance)

	if show_wireframe:
		wire_instance = MeshInstance3D.new()
		add_child(wire_instance)

# Call this before _ready() to set the target node.
func initialize(target: MeshInstance3D) -> void:
	_target = target

#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––
# 1) HELPER FUNCTIONS
#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––

# Return an array of world-space vertices for the target's mesh.
func get_mesh_vertices_at(transform: Transform3D) -> Array:
	var verts = []
	if _target.mesh:
		for s in range(_target.mesh.get_surface_count()):
			var arr = _target.mesh.surface_get_arrays(s)
			if arr and arr.size() > 0:
				var v_array = arr[Mesh.ARRAY_VERTEX]
				for v in v_array:
					verts.append(transform * v)
	return verts

# Return plane basis (two perpendicular vectors) given a normal.
func get_plane_basis(plane_normal: Vector3) -> Dictionary:
	var arbitrary = Vector3.UP
	if abs(plane_normal.dot(arbitrary)) > 0.99:
		arbitrary = Vector3.RIGHT
	var u = plane_normal.cross(arbitrary).normalized()
	var v = plane_normal.cross(u).normalized()
	return {"u": u, "v": v}

# Project 3D points onto a plane (plane_origin, plane_normal) -> Array<Vector2>
func project_points_to_2d(points: Array, plane_origin: Vector3, plane_normal: Vector3) -> Array:
	var basis = get_plane_basis(plane_normal)
	var u = basis["u"]
	var v = basis["v"]
	var pts2d = []
	for p in points:
		var local = p - plane_origin
		pts2d.append(Vector2(local.dot(u), local.dot(v)))
	return pts2d

# 2D Convex hull (Andrew’s monotone chain).
func convex_hull(points: Array) -> Array:
	if points.size() <= 1:
		return points.duplicate()
	points.sort_custom(Callable(self, "_compare_points"))
	var lower = []
	for p in points:
		while lower.size() >= 2 and _cross(lower[lower.size()-2], lower[lower.size()-1], p) <= 0:
			lower.pop_back()
		lower.append(p)
	var upper = []
	for i in range(points.size()-1, -1, -1):
		var p = points[i]
		while upper.size() >= 2 and _cross(upper[upper.size()-2], upper[upper.size()-1], p) <= 0:
			upper.pop_back()
		upper.append(p)
	upper.pop_back()
	lower.pop_back()
	return lower + upper

func _compare_points(a: Vector2, b: Vector2) -> int:
	if a.x == b.x:
		return int(a.y - b.y)
	return int(a.x - b.x)

func _cross(o: Vector2, a: Vector2, b: Vector2) -> float:
	return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x)

# Optionally resample a closed polygon to a fixed number of points.
func resample_outline(points: Array, count: int) -> Array:
	if points.size() < 2:
		return points.duplicate()
	var cum_length = [0.0]
	var total_length = 0.0
	for i in range(points.size()):
		var nxt = (i + 1) % points.size()
		total_length += points[i].distance_to(points[nxt])
		cum_length.append(total_length)
	var resampled = []
	for i in range(count):
		var target = i * total_length / count
		for j in range(cum_length.size() - 1):
			if cum_length[j] <= target and target <= cum_length[j+1]:
				var seg_length = cum_length[j+1] - cum_length[j]
				var t = (target - cum_length[j]) / seg_length
				var p = points[j] + (points[(j+1) % points.size()] - points[j]) * t
				resampled.append(p)
				break
	return resampled

#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––
# 2) OUTLINE ALIGNMENT FUNCTION
#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––

# Shift new_outline (cyclically) to best match ref_outline (min sum of distances).
func align_outline(ref_outline: Array, new_outline: Array) -> Array:
	if ref_outline.size() != new_outline.size() or ref_outline.size() < 1:
		return new_outline
	var best_offset = 0
	var best_error = INF
	var N = ref_outline.size()
	for offset in range(N):
		var error = 0.0
		for i in range(N):
			error += ref_outline[i].distance_to(new_outline[(i + offset) % N])
		if error < best_error:
			best_error = error
			best_offset = offset
	var result = []
	for i in range(N):
		result.append(new_outline[(i + best_offset) % N])
	return result

#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––
# 3) COMPUTE OUTLINE (convex hull) & TRAIL SAMPLING
#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––

func compute_outline(transform: Transform3D, travel_dir: Vector3) -> Array:
	var verts = get_mesh_vertices_at(transform)
	if verts.size() == 0:
		return []
	var pn = travel_dir.normalized()
	var origin = transform.origin
	var pts2d = project_points_to_2d(verts, origin, pn)
	var hull = convex_hull(pts2d)
	if fixed_outline_vertex_count > 0 and hull.size() > fixed_outline_vertex_count:
		hull = resample_outline(hull, fixed_outline_vertex_count)
	var basis = get_plane_basis(pn)
	var u = basis["u"]
	var v = basis["v"]
	var outline = []
	for p in hull:
		outline.append(u * p.x + v * p.y)
	return outline

func add_trail_point(transform: Transform3D) -> void:
	var origin = transform.origin
	var travel_dir = Vector3.FORWARD
	if trail_points.size() > 0:
		var last_origin = trail_points[trail_points.size() - 1].origin
		travel_dir = (origin - last_origin).normalized()
		if travel_dir == Vector3.ZERO:
			travel_dir = _target.global_transform.basis.z.normalized()
	else:
		travel_dir = _target.global_transform.basis.z.normalized()

	var outline = compute_outline(transform, travel_dir)
	if outline.size() == 0:
		return

	# Align the new outline with the previous if they have the same size.
	if trail_points.size() > 0:
		var last_outline = trail_points[trail_points.size() - 1].outline
		if last_outline.size() == outline.size():
			outline = align_outline(last_outline, outline)

	var sample = {
		"origin": origin,
		"outline": outline,
		"age": lifetime
	}
	trail_points.append(sample)

	# Limit the total number of samples for performance.
	if trail_points.size() > max_samples:
		trail_points.pop_front()

func _update_points(delta: float) -> void:
	for s in trail_points:
		s.age -= delta
	trail_points = trail_points.filter(func(s):
		return s.age > 0
	)

#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––
# 4) BUILD MESH (with capping)
#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––

func update_trail_mesh() -> void:
	if trail_points.size() < 2:
		# Not enough points to form any geometry
		mesh_instance.mesh = null
		if show_wireframe and wire_instance:
			wire_instance.mesh = null
		return

	var st = SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)

	var n_samples = trail_points.size()

	# ---------------------------
	# 1) Build side faces
	# ---------------------------
	for i in range(n_samples - 1):
		# Normalized progress from tail(=0) to head(=1)
		var f0 = float(i) / float(n_samples - 1)
		var f1 = float(i + 1) / float(n_samples - 1)

		# TAPER SETTINGS:
		# Instead of going from 0.0 to base_width, use a small min scale:
		var scale0 = lerp(MIN_TAIL_SCALE * base_width, base_width, f0)
		var scale1 = lerp(MIN_TAIL_SCALE * base_width, base_width, f1)

		var s0 = trail_points[i]
		var s1 = trail_points[i + 1]
		var o0 = s0.outline
		var o1 = s1.outline
		var n_pts = min(o0.size(), o1.size())
		if n_pts < 3:
			continue

		# Connect corresponding vertices in the two cross-sections
		for j in range(n_pts):
			var nj = (j + 1) % n_pts
			var v0 = s0.origin + o0[j]  * scale0
			var v1 = s1.origin + o1[j]  * scale1
			var v2 = s1.origin + o1[nj] * scale1
			var v3 = s0.origin + o0[nj] * scale0

			# First triangle
			var normal = (v1 - v0).cross(v2 - v0).normalized()
			st.set_normal(normal)
			st.add_vertex(v0)
			st.add_vertex(v1)
			st.add_vertex(v2)

			# Second triangle
			normal = (v2 - v0).cross(v3 - v0).normalized()
			st.set_normal(normal)
			st.add_vertex(v0)
			st.add_vertex(v2)
			st.add_vertex(v3)

	# ---------------------------
	# 2) Cap the last cross-section (the "head")
	# ---------------------------
	var last_outline = trail_points[n_samples - 1].outline
	if last_outline.size() >= 3:
		var last_origin = trail_points[n_samples - 1].origin
		# Normal points from second-last to last
		var normal_head = (trail_points[n_samples - 1].origin - trail_points[n_samples - 2].origin).normalized()

		# Triangle fan from the origin to each pair of adjacent points in the ring
		for j in range(last_outline.size()):
			var nj = (j + 1) % last_outline.size()
			var v0 = last_origin
			var v1 = last_origin + last_outline[j]  * base_width
			var v2 = last_origin + last_outline[nj] * base_width

			st.set_normal(normal_head)
			st.add_vertex(v0)
			st.add_vertex(v1)
			st.add_vertex(v2)

	# ---------------------------
	# 3) Commit the final mesh
	# ---------------------------
	var new_mesh = st.commit()
	if new_mesh:
		mesh_instance.mesh = new_mesh

	# Optionally update wireframe
	if show_wireframe:
		var st_wire = SurfaceTool.new()
		st_wire.begin(Mesh.PRIMITIVE_LINE_STRIP)
		st_wire.set_color(wireframe_color)
		for s in trail_points:
			st_wire.add_vertex(s.origin)
		var wire_mesh = st_wire.commit()
		if wire_mesh and wire_instance:
			wire_instance.mesh = wire_mesh

#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––
# 5) MAIN LOOP
#––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––

func _emit(delta: float) -> void:
	if not _target:
		return
	var current_pos = _target.global_transform.origin
	# Add a sample if we've moved far enough since the last one
	if trail_points.size() == 0 or current_pos.distance_squared_to(trail_points[trail_points.size() - 1].origin) >= distance * distance:
		add_trail_point(_target.global_transform)
	update_trail_mesh()

func _process(delta: float) -> void:
	_update_points(delta)
	if emit:
		_emit(delta)
	elif trail_points.size() > 0:
		# If not emitting new points, just refresh the mesh for existing ones
		update_trail_mesh()
