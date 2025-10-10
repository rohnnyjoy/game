shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, depth_test_disabled;

uniform sampler2D MASK_DEPTH_TEX : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D DEPTH_TEX : hint_depth_texture, filter_nearest, repeat_disable;

uniform vec2 screen_size = vec2(1920.0, 1080.0);
uniform vec2 mask_viewport_size = vec2(1920.0, 1080.0);
uniform int thickness_px = 3;
uniform bool outer_only = true;

uniform vec4 color_front = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec4 color_occluded = vec4(0.0, 1.0, 1.0, 1.0);
uniform bool draw_front = true;
uniform bool draw_occluded = true;
// Comparison happens in raw reversed-Z space (0 = far, 1 = near)
uniform float depth_epsilon_raw = 0.002;
uniform bool debug_show_mask = false;
uniform bool debug_show_main_depth = false;

const int DIR_COUNT = 16;
const int MAX_THICKNESS = 24;
const vec2 DIRS[DIR_COUNT] = vec2[](
    vec2(1.0, 0.0),
    vec2(-1.0, 0.0),
    vec2(0.0, 1.0),
    vec2(0.0, -1.0),
    vec2(0.70710678, 0.70710678),
    vec2(-0.70710678, 0.70710678),
    vec2(0.70710678, -0.70710678),
    vec2(-0.70710678, -0.70710678),
    vec2(0.92387953, 0.38268343),
    vec2(-0.92387953, 0.38268343),
    vec2(0.92387953, -0.38268343),
    vec2(-0.92387953, -0.38268343),
    vec2(0.38268343, 0.92387953),
    vec2(-0.38268343, 0.92387953),
    vec2(0.38268343, -0.92387953),
    vec2(-0.38268343, -0.92387953)
);

vec4 fetch_mask(vec2 uv)
{
    vec2 clamped_uv = clamp(uv, vec2(0.0), vec2(1.0));
    float depth_raw = texture(MASK_DEPTH_TEX, clamped_uv).r; // reversed-Z raw depth from mask viewport
    // Non-zero depth indicates presence; empty (far cleared) is 0.0 in reversed-Z
    float flag = depth_raw > 0.00001 ? 1.0 : 0.0;
    return vec4(flag, depth_raw, 0.0, 0.0);
}

// No linearization needed; compare raw reversed-Z values consistently

void fragment()
{
    vec3 out_color = vec3(0.0);
    float out_alpha = 0.0;

    if (debug_show_mask)
    {
        float m = texture(MASK_DEPTH_TEX, SCREEN_UV).r;
        out_color = vec3(m);
        out_alpha = 1.0;
    }
    else if (debug_show_main_depth)
    {
        float d = texture(DEPTH_TEX, SCREEN_UV).r;
        out_color = vec3(d);
        out_alpha = 1.0;
    }
    else
    {
        vec2 safe_screen = max(screen_size, vec2(1.0));
        vec2 safe_mask = max(mask_viewport_size, vec2(1.0));
        vec2 one_px = 1.0 / safe_screen;

        vec4 base_sample = fetch_mask(SCREEN_UV);
        float base_flag = base_sample.x;
        float base_depth_raw = base_sample.y;
        float dilated = base_flag;
        float max_mask_depth = base_flag > 0.0 ? base_depth_raw : 0.0;

        float per_mask_x = safe_screen.x / safe_mask.x;
        float per_mask_y = safe_screen.y / safe_mask.y;
        float per_mask = max(max(per_mask_x, per_mask_y), 1.0);
        float total_steps_f = ceil(float(thickness_px) * per_mask);
        total_steps_f = clamp(total_steps_f, 0.0, float(MAX_THICKNESS));
        int total_steps = int(total_steps_f);

        float eroded = base_flag > 0.5 ? 1.0 : 0.0;

        for (int i = 0; i < DIR_COUNT; i++)
        {
            vec2 dir = DIRS[i];
            for (int step = 1; step <= MAX_THICKNESS; step++)
            {
                if (step > total_steps)
                {
                    break;
                }
                float radius_screen = float(step) / per_mask;
                vec2 sample_uv = SCREEN_UV + dir * one_px * radius_screen;
                vec4 mask_sample = fetch_mask(sample_uv);
                if (mask_sample.x > 0.0)
                {
                    dilated = max(dilated, mask_sample.x);
                    max_mask_depth = max(max_mask_depth, mask_sample.y);
                }
                if (eroded > 0.0)
                {
                    eroded = min(eroded, mask_sample.x);
                }
            }
        }

        float outer_outline = max(dilated - base_flag, 0.0);
        float inner_outline = base_flag > 0.0 ? max(base_flag - eroded, 0.0) : 0.0;

        float main_depth_raw = texture(DEPTH_TEX, SCREEN_UV).r;
        bool front_visible = (max_mask_depth >= main_depth_raw - depth_epsilon_raw);

        float outline_front = outer_only ? outer_outline : max(outer_outline, inner_outline);
        float outline_occluded = max(outer_outline, inner_outline);
        float outline = front_visible ? outline_front : outline_occluded;

        vec4 color = vec4(0.0);
        if (front_visible)
        {
            if (draw_front)
            {
                color = color_front;
            }
        }
        else
        {
            if (draw_occluded)
            {
                color = color_occluded;
            }
        }

        out_color = color.rgb;
        out_alpha = color.a * outline;
    }

    ALBEDO = out_color;
    ALPHA = out_alpha;
}
