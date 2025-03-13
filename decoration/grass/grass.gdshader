shader_type spatial;
#include "math.gdshaderinc"
render_mode vertex_lighting, cull_disabled;

// Forced normal – set this to your main light’s direction for full brightness.
uniform vec3 forced_normal = vec3(0.0, 1.0, 0.0);

// Wind parameters.
uniform float wind_speed : hint_range(0.0, 2.0) = 1.0;
uniform float wind_strength : hint_range(0.0, 1.0) = 0.5;
// Define the bending range in degrees.
uniform float min_bend_degrees : hint_range(-180.0, 180.0) = -15.0;
uniform float max_bend_degrees : hint_range(-180.0, 180.0) = 15.0;

// Noise texture parameters.
uniform float noise_scale : hint_range(0.01, 1.0) = 0.05;
uniform sampler2D noise_texture;

// Assumed maximum height of the grass blade (in local space, with pivot at base).
uniform float max_height : hint_range(0.1, 10.0) = 1.0;

// Color parameters.
uniform vec3 color1 : source_color;
uniform vec3 color2 : source_color;
uniform sampler2D _colorcurve;

varying float windSampleOut;

void vertex() {
    // Retrieve the blade’s base world position and its base Y-rotation.
    vec3 bladePos = INSTANCE_CUSTOM.xyz;
    float baseAngle = INSTANCE_CUSTOM.a; // in radians

    // Compute a continuous noise sample using the blade’s base position.
    vec2 uv = bladePos.xz * noise_scale + vec2(TIME * wind_speed, TIME * wind_speed);
    float wind_sample = texture(noise_texture, uv).r;
    windSampleOut = wind_sample;
    
    // Compute the bending angle by interpolating between the minimum and maximum rotations.
    float bendingAngle = mix(radians(min_bend_degrees), radians(max_bend_degrees), wind_sample) * wind_strength;
    
    // Compute a factor based on the vertex’s height so that the base stays fixed.
    float factor = clamp(VERTEX.y / max_height, 0.0, 1.0);
    float finalAngle = bendingAngle * factor;
    
    // Get the blade's base rotation matrix (rotation about Y by baseAngle).
    mat3 baseRot = mat3(rotation3d(vec3(0, 1, 0), baseAngle));
    
    // Transform the vertex and normal into the blade’s local space.
    vec3 localPos = inverse(baseRot) * VERTEX;
    vec3 localNormal = inverse(baseRot) * NORMAL;
    
    // In local space, assume the blade is oriented so that bending occurs around the X-axis.
    mat3 bendMat = mat3(rotation3d(vec3(1, 0, 0), finalAngle));
    localPos = bendMat * localPos;
    localNormal = bendMat * localNormal;
    
    // Transform the deformed vertex and normal back to world space.
    VERTEX = baseRot * localPos;
    // Override the computed normal with the forced normal to ensure uniform shading.
	// Compute the transformed normal.
	vec3 transformed_normal = baseRot * localNormal;
	// Optionally blend with forced normal if needed:
	// float blendFactor = 0.5; // Adjust blend factor as required.
	// NORMAL = normalize(mix(transformed_normal, forced_normal, blendFactor));
	NORMAL = transformed_normal;
}

void fragment() {
    // Use the vertical UV coordinate to sample a color curve texture.
    float curve = texture(_colorcurve, vec2(UV.y, 0.0)).r;
    // Blend between two colors.
    vec3 base_color = mix(color1, color2, curve);
    ALBEDO = base_color;
}
