shader_type spatial;
render_mode cull_disabled;

// Material parameters
uniform sampler2D tex_albedo : source_color;
uniform sampler2D tex_roughness;
uniform float roughness_factor : hint_range(0.0, 1.0) = 1.0;
uniform float alpha_scissor : hint_range(0.0, 1.0) = 0.0;

// 3D Wiggle parameters
uniform float amplitude_x : hint_range(0.0, 1) = 0.005;
uniform float amplitude_y : hint_range(0.0, 1) = 0.005;
uniform float amplitude_z : hint_range(0.0, 1) = 0.005;

uniform float speed_x : hint_range(0.0, 50.0) = 22.0;
uniform float speed_y : hint_range(0.0, 50.0) = 22.0;
uniform float speed_z : hint_range(0.0, 50.0) = 22.0;

uniform float time_scale : hint_range(0.0, 2.0) = 0.5;

// Ripple size parameter: larger values produce larger (wider) ripples.
uniform float ripple_size : hint_range(0.1, 10.0) = 1.0;

// Boolean flag to enable/disable random phase.
uniform bool use_random_phase = true;

void vertex() {
    float phase_offset = 0.0;
    if (use_random_phase) {
        // If a custom phase is provided via INSTANCE_CUSTOM.x, use it.
        if (INSTANCE_CUSTOM.x != 0.0) {
            phase_offset = INSTANCE_CUSTOM.x;
        } else {
            // Use the built-in INSTANCE_ID (converted to float) to generate a stable pseudo-random value.
            phase_offset = fract(sin(float(INSTANCE_ID) * 12.9898) * 43758.5453);
        }
    }

    // Compute phases by adding the per-instance phase offset to the time-based phase.
    float phase_x = (TIME * time_scale + phase_offset + VERTEX.x / ripple_size) * speed_x;
    float phase_y = (TIME * time_scale + phase_offset + VERTEX.y / ripple_size) * speed_y;
    float phase_z = (TIME * time_scale + phase_offset + VERTEX.z / ripple_size) * speed_z;

    // Apply oscillations additively on each axis.
    VERTEX.x += cos(phase_x) * amplitude_x;
    VERTEX.y += sin(phase_y) * amplitude_y;
    VERTEX.z += cos(phase_z) * amplitude_z;
}

void fragment() {
    vec4 colour = texture(tex_albedo, UV);
    ALBEDO = colour.rgb;
    ROUGHNESS = texture(tex_roughness, UV).r * roughness_factor;
    ALPHA = colour.a;
    ALPHA_SCISSOR_THRESHOLD = alpha_scissor;
}
