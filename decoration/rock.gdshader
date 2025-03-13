shader_type spatial;

uniform vec3 u_edge_color : source_color = vec3(1.0, 1.0, 1.0); // Edge (crease) color
uniform vec3 u_base_color : source_color = vec3(0.5, 0.5, 0.5); // Base color

// Parameters to control crease detection
uniform float edge_threshold : hint_range(0.0, 1.0) = 0.1;
uniform float curvature_scale : hint_range(1.0, 100.0) = 10.0;

void fragment() {
    vec3 N = normalize(NORMAL);
    vec3 dNdx = dFdx(N);
    vec3 dNdy = dFdy(N);

    // Amplify curvature if needed
    float curvature = (length(dNdx) + length(dNdy)) * curvature_scale;

    // Use a hard threshold so any curvature above edge_threshold shows as 1
    float edge_mask = step(edge_threshold, curvature);

    // For debugging, mix the edge color if the threshold is passed
    ALBEDO = mix(u_base_color, u_edge_color, edge_mask);
}
