shader_type spatial;
#include "math.gdshaderinc"
render_mode  cull_disabled;
uniform vec3 color1:source_color;
uniform vec3 color2:source_color;
uniform sampler2D _colorcurve;
varying float noise;

void vertex(){
	// gets noise from particle shader
	noise = INSTANCE_CUSTOM.x;
}
void fragment() {
	// custom curve texture
	float curve = texture(_colorcurve,vec2(UV.y)).r;
	// use the curve to assign the colors
	vec3 base_color = mix(color1,color2,curve);
	// apply noise to the color
	vec3 blade_color = base_color * remap(noise,0.0,1.0,0.65,1.0);
	// adjust the normals (always face to front view)
	NORMAL *= (FRONT_FACING)?1.0:-1.0;
	ALBEDO = blade_color;

}
