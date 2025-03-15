#include "noise3d.gdshaderinc"
float random(float s) {
    return fract(sin(s) * 43758.5453);
}
vec2 randomPolarV2(float s){
	float x = random(s);
	vec2 rv2 = vec2(x,random(s+x));
	float r = sqrt(rv2.x);
	float theta = TAU * (rv2.y);
	return vec2(r * cos(theta),r * sin(theta));
}
float random_range(float s, float a, float b){
	return a + (b - a) * random(s);
}
vec3 clampByMaxLength(vec3 v, float maxl) {
    return normalize(v) * clamp(length(v), 0.0, maxl);
}
vec3 noise3D(vec3 v, float frequency, float scale) {
    vec3 v_scaled = v * frequency;
    return v + vec3(snoise(v_scaled),
					snoise(v_scaled + vec3(31.34, 12.56, 74.12)),
					snoise(v_scaled + vec3(57.48, 83.94, 21.76)))
					* scale;
}
