shader_type spatial;
render_mode blend_mix, depth_prepass_alpha;

// Balatro-inspired dissolve tuned for pixelated UV-space masking with burn rims.

uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float edge_softness : hint_range(0.0005, 0.5, 0.0005) = 0.02;
uniform float pixel_size : hint_range(0.002, 0.2, 0.002) = 0.02;
uniform float pixel_jitter : hint_range(0.5, 4.0, 0.05) = 2.3;
uniform vec2 seam_offset = vec2(0.5, 0.0);
uniform vec4 base_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform bool use_albedo_texture = false;
uniform sampler2D albedo_texture : source_color;
uniform vec4 burn_color_1 : source_color = vec4(1.0, 0.66, 0.2, 1.0);
uniform vec4 burn_color_2 : source_color = vec4(1.0, 0.32, 0.05, 1.0);

float adjusted_dissolve(float t)
{
    float clamped = clamp(t, 0.0, 1.0);
    return (clamped * clamped * (3.0 - 2.0 * clamped)) * 1.02 - 0.01;
}

vec4 sample_base(vec2 uv)
{
    if (use_albedo_texture)
    {
        vec4 tex = texture(albedo_texture, uv);
        tex.rgb *= base_color.rgb;
        tex.a *= base_color.a;
        return tex;
    }
    return base_color;
}

void fragment()
{
    vec2 uv = UV;
    vec4 base = sample_base(uv);
    float adj = adjusted_dissolve(dissolve);

    float cell = max(0.0005, pixel_size);
    float jitter_scale = max(0.0, pixel_jitter);

    vec2 uv_wrapped = fract(uv + seam_offset);

    vec2 uv_pixel = floor(uv_wrapped / cell) * cell;

    float t = TIME * 10.0 + 2003.0;
    vec2 uv_scaled_centered = (uv_pixel - vec2(0.5)) * (jitter_scale / cell);

    vec2 field_part1 = uv_scaled_centered + 50.0 * vec2(sin(-t / 143.6340), cos(-t / 99.4324));
    vec2 field_part2 = uv_scaled_centered + 50.0 * vec2(cos(t / 53.1532), cos(t / 61.4532));
    vec2 field_part3 = uv_scaled_centered + 50.0 * vec2(sin(-t / 87.53218), sin(-t / 49.0));

    float field = (1.0 + (
        cos(length(field_part1) / 19.483) +
        sin(length(field_part2) / 33.155) * cos(field_part2.y / 15.73) +
        cos(length(field_part3) / 27.193) * sin(field_part3.x / 21.92))) * 0.5;

    vec2 borders = vec2(0.2, 0.8);

    float res = 0.5 + 0.5 * cos((adj) / 82.612 + (field - 0.5) * PI);
    res -= (uv_pixel.x > borders.y ? (uv_pixel.x - borders.y) * (5.0 + 5.0 * dissolve) : 0.0) * dissolve;
    res -= (uv_pixel.y > borders.y ? (uv_pixel.y - borders.y) * (5.0 + 5.0 * dissolve) : 0.0) * dissolve;
    res -= (uv_pixel.x < borders.x ? (borders.x - uv_pixel.x) * (5.0 + 5.0 * dissolve) : 0.0) * dissolve;
    res -= (uv_pixel.y < borders.x ? (borders.x - uv_pixel.y) * (5.0 + 5.0 * dissolve) : 0.0) * dissolve;

    vec3 colour = base.rgb;

    if (dissolve > 0.01)
    {
        if (burn_color_2.a > 0.01)
        {
            colour = mix(colour, burn_color_2.rgb, 0.6 * dissolve);
        }
        else if (burn_color_1.a > 0.01)
        {
            colour = mix(colour, burn_color_1.rgb, 0.6 * dissolve);
        }
    }

    float rim_window = 0.5 - abs(adj - 0.5);
    if (base.a > 0.01 && burn_color_1.a > 0.01 && res < adj + 0.8 * rim_window && res > adj)
    {
        if (res < adj + 0.5 * rim_window && res > adj)
        {
            colour = burn_color_1.rgb;
        }
        else if (burn_color_2.a > 0.01)
        {
            colour = burn_color_2.rgb;
        }
    }

    float width = max(0.0005, edge_softness);
    float mask = smoothstep(adj - width, adj + width, res);
    float alpha = base.a * mask;

    if (alpha <= 0.001)
    {
        discard;
    }

    ALBEDO = colour;
    ALPHA = alpha;
    SPECULAR = 0.2;
    ROUGHNESS = 0.75;
}
