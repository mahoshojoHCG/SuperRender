#version 450

// YCbCr-to-RGBA compute shader.
// Each thread converts one pixel from YCbCr color space to RGBA.
// Y/Cb/Cr planes are already upsampled to per-pixel resolution.

layout(local_size_x = 256) in;

layout(set = 0, binding = 0) readonly buffer YPlane {
    int yData[];
};

layout(set = 0, binding = 1) readonly buffer CbPlane {
    int cbData[];
};

layout(set = 0, binding = 2) readonly buffer CrPlane {
    int crData[];
};

layout(set = 0, binding = 3) writeonly buffer RgbaOutput {
    uint rgbaPixels[];
};

layout(push_constant) uniform PushConsts {
    uint pixelCount;
};

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= pixelCount) return;

    float y  = float(yData[idx]);
    float cb = float(cbData[idx]) - 128.0;
    float cr = float(crData[idx]) - 128.0;

    int r = clamp(int(round(y + 1.402 * cr)), 0, 255);
    int g = clamp(int(round(y - 0.344136 * cb - 0.714136 * cr)), 0, 255);
    int b = clamp(int(round(y + 1.772 * cb)), 0, 255);

    rgbaPixels[idx] = uint(r) | (uint(g) << 8) | (uint(b) << 16) | (0xFFu << 24);
}
