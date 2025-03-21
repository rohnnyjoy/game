shader_type particles;

#include "funcs_particles.gdshaderinc"
group_uniforms constants;
uniform sampler2D tex_pos;
uniform sampler2D tex_up;
uniform int path_samples;
uniform float segment_size;
group_uniforms;

// Controls how fast the school (target position along the path) progresses.
uniform float school_speed: hint_range(0.1, 10.0) = 2.0;

uniform float mass: hint_range(1.0,100.0) = 5.0;
uniform float path_spread: hint_range(0.1,10);
uniform float _rand_speed: hint_range(0,5);
uniform float noise_frequency: hint_range(0.1, 2.0);
uniform float noise_scale: hint_range(0, 5.0);
uniform float radial_spread: hint_range(0.0, 3.0);

// Control visual scale of each fish.
uniform float particle_scale: hint_range(0.001, 10.0) = 1.0;

// --- New uniform rotations (in degrees) ---
// These values will be applied as an extra rotation on top of the computed orientation.
uniform float extra_rot_x: hint_range(-180.0, 180.0) = 0.0;
uniform float extra_rot_y: hint_range(-180.0, 180.0) = 0.0;
uniform float extra_rot_z: hint_range(-180.0, 180.0) = 0.0;

// A simple hash function to generate a pseudo–random number from a float seed.
float hash(float n) {
    return fract(sin(n) * 43758.5453123);
}

// Returns a random value between a and b based on the seed.
float random_range_custom(float seed, float a, float b) {
    return mix(a, b, hash(seed));
}

// Returns a random 2D unit vector based on the seed.
vec2 randomPolarCustom(float seed) {
    float angle = hash(seed) * 6.28318530718; // 2*PI
    return vec2(cos(angle), sin(angle));
}

void start() {
    // Spawn at the baked local position.
    vec3 spawnPos = texelFetch(tex_pos, ivec2(0, 0), 0).rgb;
    TRANSFORM[3].xyz = spawnPos;
    CUSTOM.z = TIME;
}

void process() {
    // Calculate particle age.
    float particle_age = TIME - CUSTOM.z;

    // --- School progression along the path ---
    float total_distance = particle_age * school_speed;
    total_distance += mix(0.0, path_spread, CUSTOM.y);

    float idx;
    float t = modf(total_distance / segment_size, idx);
    int index0 = int(idx) % path_samples;
    int index1 = (index0 + 1) % path_samples;

    vec3 pos0 = texelFetch(tex_pos, ivec2(index0, 0), 0).rgb;
    vec3 pos1 = texelFetch(tex_pos, ivec2(index1, 0), 0).rgb;
    vec3 target_pos = mix(pos0, pos1, t);

    // --- Apply a random polar offset ---
    // Instead of using the current TRANSFORM (which includes particle_scale),
    // we divide by particle_scale so that the radial offset remains independent.
    vec2 polar = randomPolarCustom(float(INDEX) + 2.0);
    vec3 rnd_spread = vec3(polar, 0.0);
    rnd_spread = mat3(TRANSFORM) * rnd_spread;
    target_pos += rnd_spread * radial_spread / particle_scale;

    // Optionally add noise.
    target_pos = noise3D(target_pos, noise_frequency, noise_scale);

    // --- Unit steering ---
    float fish_speed = school_speed + mix(-_rand_speed, _rand_speed, CUSTOM.x);
    fish_speed = clamp(fish_speed, 0.1, 10.0);

    vec3 desired_vel = normalize(target_pos - TRANSFORM[3].xyz) * fish_speed;
    vec3 steering = desired_vel - VELOCITY;
    steering = clampByMaxLength(steering, fish_speed);
    steering = steering / mass;
    VELOCITY += steering;
    VELOCITY = clampByMaxLength(VELOCITY, fish_speed);

    // --- Update orientation based on the path's up vectors ---
    vec3 yb0 = normalize(texelFetch(tex_up, ivec2(index0, 0), 0).rgb);
    vec3 yb1 = normalize(texelFetch(tex_up, ivec2(index1, 0), 0).rgb);
    vec3 yb = mix(yb0, yb1, t);
    vec3 zb = normalize(VELOCITY);

    vec3 tempUp = yb - dot(yb, zb) * zb;
    if (length(tempUp) < 0.001) {
        tempUp = vec3(0.0, 1.0, 0.0);
        if (abs(dot(normalize(tempUp), zb)) > 0.99) {
            tempUp = vec3(1.0, 0.0, 0.0);
        }
    }
    yb = normalize(tempUp);

    vec3 xb = normalize(cross(zb, yb));
    if (dot(xb, cross(yb, zb)) < 0.0) {
        xb = -xb;
    }

    // --- Apply extra Euler rotations in degrees (converted to radians) ---
    float rot_x_rad = extra_rot_x * 0.0174533;
    float rot_y_rad = extra_rot_y * 0.0174533;
    float rot_z_rad = extra_rot_z * 0.0174533;

    mat3 rot_x = mat3(
        vec3(1.0, 0.0, 0.0),
        vec3(0.0, cos(rot_x_rad), -sin(rot_x_rad)),
        vec3(0.0, sin(rot_x_rad), cos(rot_x_rad))
    );
    mat3 rot_y = mat3(
        vec3(cos(rot_y_rad), 0.0, sin(rot_y_rad)),
        vec3(0.0, 1.0, 0.0),
        vec3(-sin(rot_y_rad), 0.0, cos(rot_y_rad))
    );
    mat3 rot_z = mat3(
        vec3(cos(rot_z_rad), -sin(rot_z_rad), 0.0),
        vec3(sin(rot_z_rad), cos(rot_z_rad), 0.0),
        vec3(0.0, 0.0, 1.0)
    );
    // Combine the rotations (order: Z * Y * X).
    mat3 extra_rot = rot_z * rot_y * rot_x;

    // --- Combine the computed orientation with the extra rotation ---
    mat3 computed_basis = mat3(xb, yb, zb);
    // Apply the extra rotation in local space.
    mat3 final_basis = computed_basis * extra_rot;
    // Extract the final basis vectors.
    xb = final_basis[0];
    yb = final_basis[1];
    zb = final_basis[2];

    // --- Apply visual scaling ---
    TRANSFORM[0].xyz = xb * particle_scale;
    TRANSFORM[1].xyz = yb * particle_scale;
    TRANSFORM[2].xyz = zb * particle_scale;

    // --- Integrate position ---
    TRANSFORM[3].xyz += VELOCITY * DELTA;
}
