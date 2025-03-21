shader_type spatial;
render_mode unshaded, depth_draw_always, cull_disabled;

uniform float outline_width : hint_range(0.0, 1.0) = 0.05;
uniform vec4 outline_color;

void vertex() {
    VERTEX *= (1.0 + outline_width);
}

void fragment() {
    ALBEDO = outline_color.rgb;
    ALPHA = outline_color.a;
}
