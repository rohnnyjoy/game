shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always, blend_mix;

uniform sampler2D effect_texture;
uniform float frame_count = 1.0;
uniform float frame_pixel_width = 32.0;
uniform float frame_index = 1.0;
uniform float top_pixels = 48.0;
uniform float mid_pixels = 48.0;
uniform float bottom_pixels = 48.0;
uniform vec2 texture_pixel_size = vec2(448.0, 144.0);
uniform float fade_in_fraction = 0.12;
uniform float fade_out_fraction = 0.35;
uniform float edge_softness = 0.55;
uniform float minimum_alpha = 1.0;

varying vec4 v_custom;

void vertex()
{
    v_custom = INSTANCE_CUSTOM;
}

float safe_divide(float a, float b, float fallback)
{
    return b == 0.0 ? fallback : a / b;
}

void fragment()
{
    vec4 custom = v_custom;
    float progress = clamp(custom.x, 0.0, 1.0);
    float repeat = max(1.0, custom.y);
    float pixel_scale = max(0.0001, custom.z);
    float intensity = max(0.05, custom.w);

    float fade = 1.0;

    float alpha = fade;
    vec3 color = vec3(0.0);

    if (progress >= 0.0)
    {
        float frames = max(1.0, frame_count);
        float frame = clamp(frame_index, 0.0, frames - 1.0);

        float u_min = frame / frames;
        float u_max = (frame + 1.0) / frames;
        float u = mix(u_min, u_max, clamp(UV.x, 0.0, 1.0));

        float top_len = max(0.001, top_pixels * pixel_scale);
        float mid_len = max(0.001, mid_pixels * pixel_scale) * repeat;
        float bottom_len = max(0.001, bottom_pixels * pixel_scale);
        float total_len = top_len + mid_len + bottom_len;

        float py = clamp(UV.y, 0.0, 1.0) * total_len;

        float tex_height = max(0.001, texture_pixel_size.y);
        float top_ratio = top_pixels / tex_height;
        float mid_ratio_unit = mid_pixels / tex_height;
        float bottom_ratio = bottom_pixels / tex_height;

        float v;
        if (py <= top_len)
        {
            float frac = safe_divide(py, top_len, 0.0);
            v = frac * top_ratio;
        }
        else if (py <= top_len + mid_len)
        {
            float frac = safe_divide(py - top_len, mid_len, 0.0);
            float tiled = frac * repeat;
            float tile_frac = tiled - floor(tiled);
            v = top_ratio + tile_frac * mid_ratio_unit;
        }
        else
        {
            float frac = safe_divide(py - top_len - mid_len, bottom_len, 0.0);
            v = top_ratio + mid_ratio_unit + frac * bottom_ratio;
        }

        vec2 tex_size = max(vec2(1.0), texture_pixel_size);
        vec2 uv_clamped = clamp(vec2(u, v), vec2(0.0), (tex_size - vec2(1.0)) / tex_size);
        vec2 pixel_coord = floor(uv_clamped * tex_size);
        vec2 snapped_uv = (pixel_coord + vec2(0.5)) / tex_size;
        vec4 sample_color = texture(effect_texture, snapped_uv);
        float base_alpha = sample_color.a;
        if (base_alpha <= 0.0001)
        {
            discard;
        }

        float alpha_scale = max(base_alpha, minimum_alpha);
        color = sample_color.rgb * intensity;
        alpha = alpha_scale;
    }

    alpha = clamp(alpha, 0.0, 1.0);

    ALBEDO = color;
    EMISSION = color;
    ALPHA = alpha;
}
