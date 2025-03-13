shader_type particles;
#include "math.gdshaderinc"
uniform float width;
uniform float height;
uniform float num_particles;
uniform float wind_angle:hint_range(0.0, 360.0, 0.01);
uniform float wind_speed:hint_range(0.0, 1.0, 0.01);
uniform float wind_strength:hint_range(0.0, 1.0, 0.01);
uniform sampler2D _noise;
uniform sampler2D _noisecolor;
uniform sampler2D _grassmask;
uniform bool use_mask;

void start() {
}
void process() {
	//All the animations is donde in the particle shader

	// empty transform
	mat4 basis = mat4(vec4(1,0,0,0),vec4(0,1,0,0),vec4(0,0,1,0),vec4(0,0,0,1));
	// get the cols and rows
	float sq_particles = sqrt(num_particles);
	// this just works
	float cols = round(width/height * sq_particles);
	float rows = round(height/width * sq_particles);
	// mod function is a wrapper
	float col_idx = mod(float(INDEX),cols);
	// discard decimals (trunc)
	float row_idx = trunc(float(INDEX)/cols);
	// custom coordinates so I can load textures
	vec2 uv = vec2(col_idx/cols,row_idx/rows);

	float randvalue = rand(vec2(col_idx,row_idx));
	// remap to 0.3-1.0 so I don't get 0
	randvalue = remap(randvalue,0.0,1.0,0.3,1.0);
	// start from the top left corner
	vec3 pos = EMISSION_TRANSFORM[3].xyz - vec3(width/2.0,0.0,height/2.0);
	// define the space between grass blades
	float spacex = width/cols;
	float spacey = height/rows;
	// assign the position + space + randomspace
	basis[3].x = pos.x + spacex * col_idx + spacex/2.0 * (randvalue-0.5);
	basis[3].z = pos.z + spacey * row_idx + spacey/2.0 * (randvalue-0.5);
	// displace grass depending on the mask
	basis[3].y += 10000.0 * (1.0 - step(0.5,texture(_grassmask,uv).r)) * float(use_mask);

	// rotate wind_dir in the Y axis
	vec3 wind_dir = mat3(rotation3d(vec3(0,1,0),radians(wind_angle))) * vec3(1,0,0);
	// wind texture displaced by wind direction * wind speed
	float wind = texture(_noise,uv + wind_dir.xz * TIME * wind_speed).r;

	// rotate by random value (STATIC)
	float rot_y = TAU * randvalue;
	// sine wave rotation in X and Y axis
	float rot_x = sin(TIME+TAU/4.0*randvalue)/2.0 * radians(12.0*randvalue) + wind*wind_strength;
	float rot_z = sin(TIME+TAU/4.0*randvalue)/2.0 * radians(12.0*randvalue) + wind*wind_strength;

	// apply the rotations to the transform
	basis*= rotation3d(vec3(1,0,0),rot_x);
	basis*= rotation3d(vec3(0,1,0),rot_y);
	basis*= rotation3d(vec3(0,0,1),rot_z);

	// legacy code don't touch
	basis[1]+= (randvalue-0.5)*0.5;

	TRANSFORM = basis;
	// apply color to each particle
	CUSTOM.x = texture(_noisecolor,uv).r;

}
