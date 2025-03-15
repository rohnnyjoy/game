vec2 rotate2D(vec2 position, vec2 pivot, float angle) {
    mat2 m = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
    position -= pivot;
    position = m * position;
    return position + pivot;
}