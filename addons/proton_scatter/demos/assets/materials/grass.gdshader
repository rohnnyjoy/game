shader_type spatial;
#include "math.gdshaderinc"
render_mode vertex_lighting, cull_disabled;

uniform vec3 forced_normal = vec3(0.0, 1.0, 0.0);

// Wind parameters.
uniform float wind_speed : hint_range(0.0, 2.0) = 1.0;
uniform float wind_strength : hint_range(0.0, 1.0) = 0.5;
uniform float min_bend_degrees : hint_range(-180.0, 180.0) = -15.0;
uniform float max_bend_degrees : hint_range(-180.0, 180.0) = 15.0;

// Noise texture parameters.
uniform float noise_scale : hint_range(0.01, 1.0) = 0.05;
uniform sampler2D noise_texture;

// Assumed maximum height of the blade (with pivot at base, in local space).
uniform float max_height : hint_range(0.1, 10.0) = 1.0;

// Color parameters.
uniform vec3 color1 : source_color;
uniform vec3 color2 : source_color;
uniform sampler2D _colorcurve;

// Independent effect controls:
// • bending_effect: scales the vertical (X‑axis) bending as in the original.
// • rotation_effect: rotates the blade horizontally (about Y) toward the bend direction.
// • displacement_effect: displaces (translates) the blade horizontally toward the bend direction.
uniform float bending_effect      : hint_range(0.0, 1.0) = 1.0;
uniform float rotation_effect     : hint_range(0.0, 1.0) = 0.0;
uniform float displacement_effect : hint_range(0.0, 1.0) = 0.0;

// Maximum magnitudes for horizontal effects.
uniform float rotation_max_degrees      : hint_range(0.0, 30.0) = 15.0;
uniform float displacement_max_distance : hint_range(0.0, 0.5) = 0.1;

varying float windSampleOut;

void vertex() {
    // Get blade base data from INSTANCE_CUSTOM.
    vec3 bladeBase = INSTANCE_CUSTOM.xyz;
    float baseAngle = INSTANCE_CUSTOM.a;
    
    // Compute a noise sample from the blade base.
    vec2 uv = bladeBase.xz * noise_scale + vec2(TIME * wind_speed, TIME * wind_speed);
    float wind_sample = texture(noise_texture, uv).r;
    windSampleOut = wind_sample;
    
    // Compute a height factor (0 at the base, 1 at the tip).
    float factor = clamp(VERTEX.y / max_height, 0.0, 1.0);
    
    // --- Default Bending ---
    // Compute the default bending angle (in radians) from the noise sample.
    float defaultBend = mix(radians(min_bend_degrees), radians(max_bend_degrees), wind_sample) * wind_strength;
    // Scale by height and the bending_effect control.
    float bendingAngle = defaultBend * factor * bending_effect;
    
    // Build the base rotation matrix (from the blade's base Y–rotation).
    mat3 baseRot = mat3(rotation3d(vec3(0,1,0), baseAngle));
    
    // Transform the vertex and normal into the blade's local space.
    vec3 localPos = inverse(baseRot) * VERTEX;
    vec3 localNormal = inverse(baseRot) * NORMAL;
    
    // In local space, bend the blade by rotating about the X axis.
    mat3 bendMat = mat3(rotation3d(vec3(1,0,0), bendingAngle));
    localPos = bendMat * localPos;
    localNormal = bendMat * localNormal;
    
    // Transform the bent vertex back to world space.
    vec3 bentWorldPos = baseRot * localPos;
    vec3 bentWorldNormal = baseRot * localNormal;
    
    // --- Compute the Horizontal Bend Direction ---
    // In the original (unbent) blade the tip is assumed to point along local +Z.
    // After bending, the horizontal offset is caused by the sin(bendingAngle).
    // (The larger the bendingAngle, the larger sin(bendingAngle) becomes.)
    float sinBend = sin(bendingAngle);
    // Compute the default forward (from the base orientation) in world space.
    vec2 defaultForward = normalize((baseRot * vec3(0,0,1)).xz);
    vec2 bendDir;
    if (abs(sinBend) > 0.0001) {
        // The horizontal offset (ignoring Y) is given by applying baseRot to (0,0,sinBend).
        vec3 tipOffset = baseRot * vec3(0,0,sinBend);
        bendDir = normalize(tipOffset.xz);
    } else {
        bendDir = defaultForward;
    }
    
    // --- Horizontal Rotation ---
    // We want to rotate the entire blade (its bent position) toward the bend direction.
    // Compute the signed angle between defaultForward and bendDir.
    float defaultAngle = atan(defaultForward.y, defaultForward.x);
    float bendAngle2D = atan(bendDir.
