#version 450

layout(push_constant) uniform PushConstants {
    mat4 projection;
} pc;

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 inRectCenter;
layout(location = 3) in vec2 inRectHalfSize;
layout(location = 4) in vec4 inBorderRadius;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 fragPixelPos;
layout(location = 2) out vec2 fragRectCenter;
layout(location = 3) out vec2 fragRectHalfSize;
layout(location = 4) out vec4 fragBorderRadius;

void main() {
    gl_Position = pc.projection * vec4(inPosition, 0.0, 1.0);
    fragColor = inColor;
    fragPixelPos = inPosition;
    fragRectCenter = inRectCenter;
    fragRectHalfSize = inRectHalfSize;
    fragBorderRadius = inBorderRadius;
}
