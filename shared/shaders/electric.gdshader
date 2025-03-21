shader_type spatial;
render_mode unshaded, blend_add;

uniform sampler2D noise_texture;
uniform vec4 light_color : source_color = vec4( 0.32, 0.5, 0.82, 0.5 );
uniform vec4 arc_color : source_color = vec4( 0.46, 0.68, 1.0, 1.0 );
uniform float emission_intensity : hint_range(0.0, 10.0) = 1.0;

uniform vec2 seed = vec2( 0.0, 0.0 );
uniform float speed = 5.1;
uniform float random_scale = 4.6;
uniform float electro_scale = 2.0;

void fragment() {
    vec2 uv = seed + UV;
    float noiseValue = texture(noise_texture, uv * random_scale).r;
    float f = abs(cos(TIME * speed + noiseValue * electro_scale));

    vec3 color = mix(light_color.rgb, arc_color.rgb, 1.0 - clamp((f - 0.08) * 20.0, 0.0, 1.0));
    ALBEDO = color;
    EMISSION = color * emission_intensity;
    ALPHA = 1.0 - clamp((f - 0.3) * 3.0, 0.0, 1.0);
}
