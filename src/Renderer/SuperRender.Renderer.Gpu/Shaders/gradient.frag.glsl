#version 450

// Gradient fragment shader.
// Uses the quad pipeline vertex layout. The gradient parameters
// (type, angle/direction, up to 8 color stops) are encoded in
// a UBO (set 0 binding 0) so we can change gradient data per draw call.
//
// For the initial implementation we render gradients as multi-stop
// quads on the CPU side and only rely on hardware interpolation.
// This shader applies border-radius SDF clipping to gradient rects.

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragPixelPos;
layout(location = 2) in vec2 fragRectCenter;
layout(location = 3) in vec2 fragRectHalfSize;
layout(location = 4) in vec4 fragBorderRadius;  // TL, TR, BR, BL

layout(location = 0) out vec4 outColor;

// Signed distance to a rounded rectangle
float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 q = abs(p) - b + vec2(r);
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

void main() {
    float maxR = max(max(fragBorderRadius.x, fragBorderRadius.y),
                     max(fragBorderRadius.z, fragBorderRadius.w));
    if (maxR < 0.5) {
        outColor = fragColor;
        return;
    }

    vec2 p = fragPixelPos - fragRectCenter;
    vec2 b = fragRectHalfSize;

    float r;
    if (p.x < 0.0 && p.y < 0.0) r = fragBorderRadius.x;
    else if (p.x >= 0.0 && p.y < 0.0) r = fragBorderRadius.y;
    else if (p.x >= 0.0 && p.y >= 0.0) r = fragBorderRadius.z;
    else r = fragBorderRadius.w;

    float dist = roundedBoxSDF(p, b, r);
    float alpha = 1.0 - smoothstep(-0.5, 0.5, dist);

    if (alpha < 0.001) discard;

    outColor = vec4(fragColor.rgb, fragColor.a * alpha);
}
