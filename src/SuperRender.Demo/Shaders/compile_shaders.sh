#!/bin/bash
# Compile GLSL shaders to SPIR-V using glslc (from Vulkan SDK).
# Install Vulkan SDK from https://vulkan.lunarg.com/ if glslc is not found.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$SCRIPT_DIR/../Resources/Shaders"
mkdir -p "$OUT_DIR"

if ! command -v glslc &> /dev/null; then
    echo "Error: glslc not found. Please install the Vulkan SDK."
    exit 1
fi

glslc "$SCRIPT_DIR/quad.vert.glsl" -o "$OUT_DIR/quad.vert.spv"
glslc "$SCRIPT_DIR/quad.frag.glsl" -o "$OUT_DIR/quad.frag.spv"
glslc "$SCRIPT_DIR/text.vert.glsl" -o "$OUT_DIR/text.vert.spv"
glslc "$SCRIPT_DIR/text.frag.glsl" -o "$OUT_DIR/text.frag.spv"

echo "Shaders compiled successfully to $OUT_DIR"
