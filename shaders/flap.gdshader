shader_type spatial;

uniform float flap_speed : hint_range(0.0, 10.0) = 2.0;
uniform float flap_amplitude_degrees : hint_range(0.0, 45.0) = 15.0;

void vertex() {
    // Identify which wing based on the vertex's x value.
    float wing_side = sign(VERTEX.x);

    // Compute the flap angle in radians using sine for smooth oscillation.
    float theta = radians(flap_amplitude_degrees) * sin(TIME * flap_speed);

    // Flip the rotation direction for the opposite wing.
    float final_angle = theta * wing_side;

    // Calculate cosine and sine of the final angle.
    float c = cos(final_angle);
    float s = sin(final_angle);

    // Construct the rotation matrix around the X-axis using column vectors.
    mat3 rot_x = mat3(
        vec3(1.0, 0.0, 0.0),    // First column: basis vector for x remains unchanged.
        vec3(0.0, c, s),        // Second column: rotated y basis.
        vec3(0.0, -s, c)        // Third column: rotated z basis.
    );

    // Apply the rotation. Ensure the pivot is at the origin of the wing.
    VERTEX = rot_x * VERTEX;
}
