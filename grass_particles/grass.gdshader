shader_type spatial;
#include "math.gdshaderinc"
render_mode unshaded, cull_disabled;

// Wind parameters.
uniform float wind_speed : hint_range(0.0, 2.0) = 1.0;
uniform float wind_strength : hint_range(0.0, 1.0) = 0.5;
// Global wind angle (in degrees) – rotates the bending direction.
uniform float wind_angle : hint_range(0.0, 360.0) = 45.0;
// Maximum bending angle at the tip (in degrees). (Set to 90 for full 90° bend.)
uniform float max_bend_degrees : hint_range(0.0, 90.0) = 15.0;

// Noise texture parameters.
uniform float noise_scale : hint_range(0.01, 1.0) = 0.05;
uniform sampler2D noise_texture;

// Assumed maximum height of the grass blade (local space, with pivot at base).
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

    // Compute base UV from blade's base position.
    vec2 baseUV = bladePos.xz * noise_scale;

    // Rotate the UV coordinates by the wind_angle.
    float radWind = radians(wind_angle);
    // Construct a 2x2 rotation matrix using two vec2 columns.
    mat2 uvRot = mat2( vec2(cos(radWind), sin(radWind)), vec2(-sin(radWind), cos(radWind)) );
    vec2 rotatedUV = uvRot * baseUV;

    // Add a time-dependent offset to scroll the noise field.
    vec2 uv = rotatedUV + vec2(TIME * wind_speed, TIME * wind_speed);

    // Sample the noise texture.
    float wind_sample = texture(noise_texture, uv).r;
    windSampleOut = wind_sample;

    // Map noise value from [0, 1] to [0, 1] by clamping negative values to zero.
    float wind_offset = max((wind_sample - 0.5) * 2.0, 0.0);

    // Compute the overall bending angle in radians.
    float maxBend = radians(max_bend_degrees) * wind_strength;
    float bendingAngle = wind_offset * maxBend;

    // Compute a factor based on the vertex's height so that the base remains fixed.
    float factor = clamp(VERTEX.y / max_height, 0.0, 1.0);
    float finalAngle = bendingAngle * factor;

    // Get the blade's base rotation matrix (rotation about Y by baseAngle).
    mat3 baseRot = mat3(rotation3d(vec3(0, 1, 0), baseAngle));

    // Transform the vertex and normal into the blade’s local space.
    vec3 localPos = inverse(baseRot) * VERTEX;
    vec3 localNormal = inverse(baseRot) * NORMAL;

    // In local space, assume the unbent blade has its bending axis as the X axis.
    // Now, rotate this local X axis by the wind_angle so the bending occurs in that direction.
    vec3 baseBendAxis = vec3(1.0, 0.0, 0.0);
    mat3 windRot = mat3(rotation3d(vec3(0, 1, 0), radWind));
    vec3 bendAxisLocal = windRot * baseBendAxis; // This is now the local bending direction.

    // Apply a rotation about bendAxisLocal by finalAngle.
    mat3 bendMat = mat3(rotation3d(bendAxisLocal, finalAngle));
    localPos = bendMat * localPos;
    localNormal = bendMat * localNormal;

    // Transform the deformed vertex and normal back to world space.
    VERTEX = baseRot * localPos;
    NORMAL = normalize(baseRot * localNormal);
}

void fragment() {
    // Use the vertical UV coordinate to sample a color curve texture.
    float curve = texture(_colorcurve, vec2(UV.y, 0.0)).r;
    // Blend between two colors.
    vec3 base_color = mix(color1, color2, curve);
    ALBEDO = base_color;
}
