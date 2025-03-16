shader_type spatial;
render_mode cull_disabled;
#include "funcs_sardine.gdshaderinc"
group_uniforms animation;

group_uniforms;
uniform sampler2D tex_albedo:source_color;
uniform sampler2D tex_roughness;
uniform float roughness_factor:hint_range(0.0, 1.0);
uniform float alpha_scissor:hint_range(0.0, 1.0);
const float _length = 0.478;
varying float temp_mask;

void vertex() {
	float frequency = (TIME/2. + VERTEX.z)*22.0;
	VERTEX.x += cos(frequency) * 0.5;
}

void fragment() {
	vec4 colour = texture(tex_albedo,UV);
	ALBEDO = colour.rgb;
	ROUGHNESS = texture(tex_roughness,UV).r * roughness_factor;
	ALPHA = colour.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_scissor;
}
