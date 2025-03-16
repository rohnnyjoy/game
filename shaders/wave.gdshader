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
    // Use the same frequency as before.
    float frequency = (TIME / 2.0 + VERTEX.z) * 22.0;

    // Compute twist angle from the sine of the frequency.
    float twistAngle = sin(frequency) * amplitude_z;

    // Create a 2D vector for the current X and Z coordinates.
    vec2 pos = vec2(VERTEX.x, VERTEX.z);

    // Rotate the (X,Z) position by the twist angle.
    pos = rotate2D(pos, vec2(0.0), twistAngle);

    // Add the original ripple offset on X.
    pos.x += cos(frequency) * amplitude_x;

    // Update the vertex's X and Z.
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
