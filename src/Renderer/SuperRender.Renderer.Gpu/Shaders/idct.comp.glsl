#version 450

// JPEG 8x8 IDCT compute shader.
// Each workgroup processes one 8x8 DCT block (64 threads).
// Thread ID maps to output pixel position (row-major).

layout(local_size_x = 64) in;

layout(set = 0, binding = 0) readonly buffer DctInput {
    int dctCoeffs[];  // N blocks * 64 ints (dequantized DCT coefficients)
};

layout(set = 0, binding = 1) writeonly buffer PixelOutput {
    int pixels[];      // N blocks * 64 ints (clamped 0-255 pixel values)
};

layout(push_constant) uniform PushConsts {
    uint blockCount;
};

// Precomputed 1/sqrt(2) for C(0)
const float C0 = 0.70710678118;
const float PI_16 = 3.14159265359 / 16.0;

void main() {
    uint blockIdx = gl_WorkGroupID.x;
    uint tid = gl_LocalInvocationID.x;  // 0..63

    if (blockIdx >= blockCount) return;

    uint baseOffset = blockIdx * 64;
    int y = int(tid) / 8;
    int x = int(tid) % 8;

    float sum = 0.0;
    for (int v = 0; v < 8; v++) {
        float cv = (v == 0) ? C0 : 1.0;
        float cosYV = cos((2.0 * float(y) + 1.0) * float(v) * PI_16);
        for (int u = 0; u < 8; u++) {
            float cu = (u == 0) ? C0 : 1.0;
            float cosXU = cos((2.0 * float(x) + 1.0) * float(u) * PI_16);
            sum += cu * cv * float(dctCoeffs[baseOffset + v * 8 + u]) * cosXU * cosYV;
        }
    }

    int value = clamp(int(round(sum / 4.0)) + 128, 0, 255);
    pixels[baseOffset + tid] = value;
}
