shader_type spatial;
// Identical to outline_shell.gdshader except: depth_test_disabled + depth_draw_never
render_mode unshaded, cull_front, depth_draw_never, depth_test_disabled;

uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float outline_thickness : hint_range(0.0, 0.1) = 0.025;

void vertex() {
    VERTEX += NORMAL * outline_thickness;
}

void fragment() {
    ALBEDO = outline_color.rgb;
    ALPHA = outline_color.a;
    EMISSION = outline_color.rgb;
}
