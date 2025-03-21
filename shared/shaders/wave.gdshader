shader_type spatial;
render_mode cull_disabled;
group_uniforms animation;

group_uniforms;
uniform sampler2D tex_albedo:source_color;
uniform sampler2D tex_roughness;
uniform float roughness_factor:hint_range(0.0, 1.0);
uniform float alpha_scissor:hint_range(0.0, 1.0);

// Uniforms to control the effects:
// amplitude_x: Original ripple on X (default 0.5 to match original behavior)
// amplitude_z: Twist effect in the XZ plane (rotation)
// amplitude_y: Vertical ripple in Y
uniform float amplitude_x : hint_range(0.0, 10.0) = 0.5;
uniform float amplitude_z : hint_range(0.0, 10.0) = 0.0;
uniform float amplitude_y : hint_range(0.0, 10.0) = 0.0;

const float _length = 0.478;
varying float temp_mask;

vec2 rotate2D(vec2 position, vec2 pivot, float angle) {
    mat2 m = mat2(vec2(cos(angle), -sin(angle)),
                  vec2(sin(angle), cos(angle)));
    position -= pivot;
    position = m * position;
    return position + pivot;
}

void vertex() {
    // Compute frequency based on time and depth.
    float frequency = (TIME / 2.0 + VERTEX.z) * 22.0;

    // Start with the original XZ position.
    vec2 pos = vec2(VERTEX.x, VERTEX.z);

    // Apply the original X ripple offset.
    float offsetX = cos(frequency) * amplitude_x;
    pos.x += offsetX;

    // Instead of twisting around the origin, use a pivot that incorporates the X offset.
    vec2 pivot = vec2(offsetX, 0.0);

    // Calculate twist angle.
    float twistAngle = sin(frequency) * amplitude_z;

    // Rotate the position about the shifted pivot.
    pos = rotate2D(pos, pivot, twistAngle);

    // Update vertex X and Z.
    VERTEX.x = pos.x;
    VERTEX.z = pos.y;

    // Apply a vertical ripple on Y.
    VERTEX.y += sin(frequency) * amplitude_y;
}


void fragment() {
    vec4 colour = texture(tex_albedo, UV);
    ALBEDO = colour.rgb;
    ROUGHNESS = texture(tex_roughness, UV).r * roughness_factor;
    ALPHA = colour.a;
    ALPHA_SCISSOR_THRESHOLD = alpha_scissor;
}
