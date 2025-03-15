@tool
extends Node
class_name BakeUtil

@export_range(1, 2048) var samples: int = 2048
@export var path3D: Path3D
@export var particles: GPUParticles3D
const textureWidth: int = 2048
const textureHeight: int = 1

@export_tool_button("Bake data to GPUParticles3D", "InspectorIcons/Node")
var bake_button: Callable = Callable()

func _enter_tree() -> void:
	bake_button = Callable(self, "pathToPositionTextures")

func pathToPositionTextures():
	if not path3D:
		push_warning("No path3D has been assigned")
		return
	if not particles:
		push_warning("No particle system has been assigned")
		return
	if not particles.process_material is ShaderMaterial:
		push_warning("The particle system doesn't contain a Shader Material.")
		return
	
	var step = path3D.curve.get_baked_length() / samples
	
	# Create images using the static create() call and RGBAF format.
	var posImage: Image = Image.create(textureWidth, textureHeight, false, Image.FORMAT_RGBAF)
	var upImage: Image = Image.create(textureWidth, textureHeight, false, Image.FORMAT_RGBAF)
	
	# Use a PackedVector4Array to store RGBA float data.
	var posData: PackedVector4Array = PackedVector4Array()
	var upData: PackedVector4Array = PackedVector4Array()
	posData.resize(textureWidth)
	upData.resize(textureWidth)
	
	for i in range(samples):
		var transform: Transform3D = particles.global_transform.affine_inverse() * path3D.global_transform * path3D.curve.sample_baked_with_rotation(i * step, true)
		var pos: Vector3 = transform.origin
		var up: Vector3 = transform.basis.y
		posData[i] = Vector4(pos.x, pos.y, pos.z, 1.0)
		upData[i] = Vector4(up.x, up.y, up.z, 1.0)
	
	posImage.set_data(textureWidth, textureHeight, false, Image.FORMAT_RGBAF, posData.to_byte_array())
	upImage.set_data(textureWidth, textureHeight, false, Image.FORMAT_RGBAF, upData.to_byte_array())
	
	var posTex: ImageTexture = ImageTexture.create_from_image(posImage)
	var upTex: ImageTexture = ImageTexture.create_from_image(upImage)
	
	var processMat: ShaderMaterial = particles.process_material
	processMat.set_shader_parameter("segment_size", step)
	processMat.set_shader_parameter("path_samples", samples)
	processMat.set_shader_parameter("tex_pos", posTex)
	processMat.set_shader_parameter("tex_up", upTex)
	
	print("Assigned [segment_size, path_samples, tex_pos, tex_up] uniforms in the process material shader")
