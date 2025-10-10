shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, depth_test_disabled;

uniform sampler2D SVP_DEPTH : hint_depth_texture, filter_nearest, repeat_disable;

// Write the raw (non-linear, reversed-Z) depth directly to color so it can be
// compared against the main viewport depth using the same raw space.
void fragment()
{
    float raw_depth = texture(SVP_DEPTH, SCREEN_UV).r; // 1.0 near, 0.0 far (Godot 4 reversed-Z)
    ALBEDO = vec3(raw_depth);
    ALPHA = 1.0;
}
