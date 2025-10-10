shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D mask_tex : hint_default_black;
uniform vec4 outline_color : source_color = vec4(0.258824, 0.839216, 1.0, 1.0);
uniform float pixel_thickness : hint_range(0.0, 8.0) = 2.0;
uniform vec2 viewport_size = vec2(1920.0, 1080.0);

void fragment() {
    vec2 px = pixel_thickness / max(viewport_size, vec2(1.0));

    float c = texture(mask_tex, UV).a;
    float e = 0.0;
    e = max(e, texture(mask_tex, UV + vec2(-px.x,  0.0)).a);
    e = max(e, texture(mask_tex, UV + vec2( px.x,  0.0)).a);
    e = max(e, texture(mask_tex, UV + vec2( 0.0 , -px.y)).a);
    e = max(e, texture(mask_tex, UV + vec2( 0.0 ,  px.y)).a);
    e = max(e, texture(mask_tex, UV + vec2(-px.x, -px.y)).a);
    e = max(e, texture(mask_tex, UV + vec2(-px.x,  px.y)).a);
    e = max(e, texture(mask_tex, UV + vec2( px.x, -px.y)).a);
    e = max(e, texture(mask_tex, UV + vec2( px.x,  px.y)).a);

    float edge = clamp(e - c, 0.0, 1.0);

    vec3 col = outline_color.rgb;
    float a = outline_color.a * edge;
    COLOR = vec4(col, a);
}
