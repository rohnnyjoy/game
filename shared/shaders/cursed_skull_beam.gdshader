shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always, blend_add;

uniform sampler2D effect_texture;
uniform float animation_fps = 30.0;
uniform float frame_count = 14.0;
uniform float frame_pixel_width = 32.0;
uniform float top_pixels = 48.0;
uniform float mid_pixels = 48.0;
uniform float bottom_pixels = 48.0;
uniform vec2 texture_pixel_size = vec2(448.0, 144.0);
uniform float fade_in_fraction = 0.12;
uniform float fade_out_fraction = 0.35;
uniform float mid_scroll_speed = 1.1;
uniform float distortion_amount = 0.05;
uniform float edge_softness = 0.55;

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
    float spawn_time = custom.x;
    float lifetime = max(0.001, custom.y);
    float repeat_factor = max(1.0, custom.z);
    float intensity = max(0.05, custom.w);

    float age = TIME - spawn_time;
    float t = clamp(age / lifetime, 0.0, 1.0);
    float fade_in = smoothstep(0.0, max(0.0001, fade_in_fraction), t);
    float fade_out = 1.0 - smoothstep(1.0 - max(0.0001, fade_out_fraction), 1.0, t);
    float fade = fade_in * fade_out;

    float alpha = fade;
    vec3 color = vec3(0.0);

    if (age >= 0.0 && age <= lifetime)
    {
        float frames = max(1.0, frame_count);
        float frame = floor(age * animation_fps);
        frame = clamp(frame, 0.0, frames - 1.0);
        float u = (frame + clamp(UV.x, 0.0, 1.0)) / frames;

        float top_len = top_pixels;
        float mid_len = mid_pixels * repeat_factor;
        float bottom_len = bottom_pixels;
        float total_len = max(0.001, top_len + mid_len + bottom_len);
        float py = clamp(UV.y, 0.0, 1.0) * total_len;

        float top_ratio = safe_divide(top_pixels, texture_pixel_size.y, 0.3333);
        float mid_ratio = safe_divide(mid_pixels, texture_pixel_size.y, 0.3333);
        float bottom_ratio = safe_divide(bottom_pixels, texture_pixel_size.y, 0.3333);

        float v;
        if (py <= top_len)
        {
            float local = safe_divide(py, top_pixels, 0.0);
            v = local * top_ratio;
        }
        else if (py <= top_len + mid_len)
        {
            float local = py - top_len;
            float scroll = age * mid_scroll_speed * mid_pixels;
            float wrapped = mod(local + scroll, max(0.001, mid_pixels));
            float frac = safe_divide(wrapped, mid_pixels, 0.0);
            v = top_ratio + frac * mid_ratio;
        }
        else
        {
            float local = py - top_len - mid_len;
            float frac = safe_divide(local, bottom_pixels, 0.0);
            v = top_ratio + mid_ratio + frac * bottom_ratio;
        }

        float wobble = sin((UV.x - 0.5) * 12.566 + age * 7.5) * distortion_amount;
        u = clamp(u + wobble * (1.0 - pow(abs(UV.x - 0.5) * 2.0, 1.5)), 0.0, 0.999);

        vec4 sample_color = texture(effect_texture, vec2(u, v));
        color = (sample_color.rgb + vec3(0.1)) * intensity;
        alpha = max(sample_color.a, 0.15) * fade;
    }

    float edge = 1.0 - smoothstep(0.0, max(0.0001, edge_softness), abs(UV.x - 0.5));
    alpha *= edge;

    ALBEDO = color;
    EMISSION = color;
    ALPHA = alpha;
}
