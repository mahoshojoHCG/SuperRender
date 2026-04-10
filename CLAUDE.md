# SuperRenderer

A complete HTML+CSS rendering engine built with C# (.NET 10), using Silk.NET + Vulkan as the graphics backend.

## Project Structure

- `src/SuperRender.Core/` — Core library (zero external deps): HTML/CSS parsers, DOM, style resolution, layout engine, painting
- `src/SuperRender.Demo/` — Vulkan-powered windowed demo app
- `tests/SuperRender.Tests/` — xUnit tests (68 tests)

## Build & Run

```bash
dotnet build              # Build all projects
dotnet test               # Run 68 unit tests
dotnet run --project src/SuperRender.Demo  # Launch the demo window (requires Vulkan)
```

## Architecture

**Rendering pipeline:** HTML string → Parse → DOM tree → Style resolution (cascade/specificity/inheritance) → Layout (block/inline box model) → Paint commands → Vulkan GPU rendering

**Key components:**
- `RenderPipeline` — orchestrator with dirty-flag optimization
- `HtmlParser` — state-machine tokenizer + tree builder
- `CssParser` — tokenizer + parser with shorthand expansion (margin/padding/border)
- `StyleResolver` — cascade, specificity, `!important`, inherited properties
- `LayoutEngine` — block layout, inline layout with word-wrap, anonymous block wrapping
- `Painter` — generates FillRect/DrawText commands from layout tree
- `VulkanRenderer` — frame loop with quad pipeline (backgrounds/borders) + text pipeline (font atlas with alpha blending)
- `DomMutationApi` — runtime DOM modification with automatic re-layout

## Platform Notes

- **macOS**: MoltenVK is bundled via `Silk.NET.MoltenVK.Native`. `VulkanContext.EnsureMoltenVK()` symlinks the dylib and sets `VK_DRIVER_FILES` before window creation.
- **Shaders**: GLSL sources in `Shaders/`, compiled to SPIR-V at runtime via shaderc. Pre-compiled `.spv` can be placed in `Resources/Shaders/`.
- **Fonts**: System fonts loaded via FreeType (`FreeTypeSharp`). Fallback chains: Helvetica (macOS), Segoe UI (Windows), DejaVu Sans (Linux).

## Development Guidelines

- Core library must remain dependency-free (pure C#)
- All Vulkan/GPU code stays in the Demo project
- `unsafe` is enabled globally (required by Silk.NET Vulkan bindings)
- Tests use `MonospaceTextMeasurer` (deterministic, no GPU needed)
