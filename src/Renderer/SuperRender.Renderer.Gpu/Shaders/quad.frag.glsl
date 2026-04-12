#version 450

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragPixelPos;
layout(location = 2) in vec2 fragRectCenter;
layout(location = 3) in vec2 fragRectHalfSize;
layout(location = 4) in vec4 fragBorderRadius;  // TL, TR, BR, BL

layout(location = 0) out vec4 outColor;

// Signed distance to a rounded rectangle.
// p: position relative to rect center
// b: half-size of the rect
// r: corner radius for the relevant corner
float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 q = abs(p) - b + vec2(r);
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

void main() {
    // Quick path: no border-radius at all
    float maxR = max(max(fragBorderRadius.x, fragBorderRadius.y),
                     max(fragBorderRadius.z, fragBorderRadius.w));
    if (maxR < 0.5) {
        outColor = fragColor;
        return;
    }

    vec2 p = fragPixelPos - fragRectCenter;
    vec2 b = fragRectHalfSize;

    // Select the radius for the current quadrant
    float r;
    if (p.x < 0.0 && p.y < 0.0) r = fragBorderRadius.x;  // top-left
    else if (p.x >= 0.0 && p.y < 0.0) r = fragBorderRadius.y;  // top-right
    else if (p.x >= 0.0 && p.y >= 0.0) r = fragBorderRadius.z;  // bottom-right
    else r = fragBorderRadius.w;  // bottom-left

    float dist = roundedBoxSDF(p, b, r);

    // Anti-aliased edge: smooth over ~1 pixel
    float alpha = 1.0 - smoothstep(-0.5, 0.5, dist);

    if (alpha < 0.001) discard;

    outColor = vec4(fragColor.rgb, fragColor.a * alpha);
}
