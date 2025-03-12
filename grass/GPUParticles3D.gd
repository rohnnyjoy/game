@tool
extends GPUParticles3D
@export_category("Grass Particles Shader")
@export_group("Particles")
@export var width:float = 20
@export var height:float = 20
@export var num_particles:int = 10
@export_range(0,360) var wind_angle:float = 0.0; 
@export_range(0,1) var wind_speed:float=0.0; 
@export_range(0,1) var wind_strength:float=0.0; 
@export var noise:Texture2D
@export var noise_color:Texture2D
@export var grass_mask:Texture2D
@export var use_mask:bool

#Not working
@export_group("Height Map")
@export var use_heightmap:bool
@export var heightmap:Texture2D

#spatial shader
@export_group("Spatial Instance")
@export_color_no_alpha var color1:Color
@export_color_no_alpha var color2:Color
@export var color_curve:Texture2D

	

func _process(delta):
	_update_parameters()
	
func _update_parameters():
	process_material.set_shader_parameter("width", width)
	process_material.set_shader_parameter("height", height)
	amount = num_particles
	process_material.set_shader_parameter("num_particles", num_particles)
	process_material.set_shader_parameter("wind_angle", wind_angle)
	process_material.set_shader_parameter("wind_speed", wind_speed)
	process_material.set_shader_parameter("wind_strength", wind_strength)
	process_material.set_shader_parameter("_noise", noise)
	process_material.set_shader_parameter("_noisecolor", noise_color)
	process_material.set_shader_parameter("_heightmap", heightmap)
	process_material.set_shader_parameter("_grassmask", grass_mask)
	process_material.set_shader_parameter("use_mask", use_mask)
	process_material.set_shader_parameter("use_heightmap", use_heightmap)
	
	#spatial shader parameters
	draw_pass_1.surface_get_material(0).set_shader_parameter("color1",color1)
	draw_pass_1.surface_get_material(0).set_shader_parameter("color2",color2)
	draw_pass_1.surface_get_material(0).set_shader_parameter("_colorcurve",color_curve)
