#version 450

// Box shadow fragment shader.
// Uses the quad pipeline vertex layout.
// Implements SDF-based Gaussian blur approximation for box shadows.
//
// fragColor = shadow color (with opacity pre-applied)
// fragRectCenter = center of the ELEMENT (not the shadow quad)
// fragRectHalfSize = half-size of the ELEMENT
// fragBorderRadius = corner radii of the element

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragPixelPos;
layout(location = 2) in vec2 fragRectCenter;
layout(location = 3) in vec2 fragRectHalfSize;
layout(location = 4) in vec4 fragBorderRadius;

layout(location = 0) out vec4 outColor;

float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 q = abs(p) - b + vec2(r);
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

void main() {
    vec2 p = fragPixelPos - fragRectCenter;
    vec2 b = fragRectHalfSize;

    // Select corner radius
    float r;
    if (p.x < 0.0 && p.y < 0.0) r = fragBorderRadius.x;
    else if (p.x >= 0.0 && p.y < 0.0) r = fragBorderRadius.y;
    else if (p.x >= 0.0 && p.y >= 0.0) r = fragBorderRadius.z;
    else r = fragBorderRadius.w;

    float dist = roundedBoxSDF(p, b, r);

    // For outer shadow: visible outside the box.
    // Use a smooth falloff approximating Gaussian blur.
    // The blur radius is encoded as a scale factor in the alpha.
    // For simplicity, use a 3-pixel smooth edge on the outer boundary.
    float blurWidth = max(3.0, length(fragRectHalfSize) * 0.05);
    float alpha = 1.0 - smoothstep(0.0, blurWidth, dist);

    if (alpha < 0.001) discard;

    outColor = vec4(fragColor.rgb, fragColor.a * alpha);
}
