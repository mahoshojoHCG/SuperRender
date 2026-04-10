# SuperRenderer

A complete HTML+CSS rendering engine built with C# (.NET 10), using Silk.NET + Vulkan as the graphics backend. Includes a DLR-based ECMAScript 2025 engine for scripting support.

## Project Structure

- `src/SuperRender.Core/` — Core library (zero external deps): HTML/CSS parsers, DOM, style resolution, layout engine, painting
- `src/SuperRender.EcmaScript/` — ECMAScript 2025 engine (DLR-based, zero external deps)
- `src/SuperRender.EcmaScript.Console/` — Interactive JS console (Node.js-style REPL)
- `src/SuperRender.Demo/` — Vulkan-powered windowed demo app
- `tests/SuperRender.Tests/` — xUnit tests for Core (68 tests)
- `tests/SuperRender.EcmaScript.Tests/` — xUnit tests for EcmaScript (421 tests)

## Build & Run

```bash
dotnet build              # Build all projects (warnings are errors)
dotnet test               # Run all unit tests (489 total)
dotnet run --project src/SuperRender.Demo  # Launch the demo window (requires Vulkan)
dotnet run --project src/SuperRender.EcmaScript.Console  # Launch the JS console REPL
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

## EcmaScript Engine

**Pipeline:** JS source → Lexer (tokens) → Parser (AST) → JsCompiler (DLR Expression trees) → Compiled delegate → Execution

**Key components:**
- `Lexer` — character-by-character scanner, full ES2025 token set
- `Parser` — recursive descent + Pratt parser (20 precedence levels), ASI, arrow detection, destructuring, modules
- `JsCompiler` — AST-to-DLR Expression tree compiler with `RuntimeHelpers` for JS semantics
- `JsValue` hierarchy — `IDynamicMetaObjectProvider` base, JsObject with prototype chain, JsFunction with closures
- `Environment` — lexical scope chain with TDZ and const enforcement
- `Realm` — global object + 15 intrinsic prototypes
- `Builtins` — 20 standard library objects (Object, Array, String, Number, Math, JSON, Date, RegExp, Map, Set, Promise, Proxy, Reflect, etc.)
- `JsEngine` — public API entry point, sandboxed .NET interop via `RegisterType<T>()`/`SetValue()`

**Deferred features** tracked in `src/SuperRender.EcmaScript/es-2025-todos.md`: BigInt, generators/async runtime, WeakRef, SharedArrayBuffer, Intl, Temporal, decorators (26 items total).

## EcmaScript Console

Node.js-style interactive REPL powered by the EcmaScript engine.

**Components:**
- `Program.cs` — CLI entry point: `--eval <code>`, `<file.js>`, or interactive REPL
- `Repl` — REPL loop with multiline detection, dot commands (`.help`, `.exit`, `.clear`, `.editor`), object-literal-vs-block disambiguation
- `LineEditor` — Readline-style key-by-key line editor: arrow keys, cursor movement, command history, word navigation (Ctrl+Left/Right), line editing shortcuts (Ctrl+U/K/W)
- `ValueInspector` — Node.js-style ANSI-colored value formatting (strings green, numbers yellow, functions cyan, etc.)

## Code Quality

- **TreatWarningsAsErrors** is on globally — the build must be zero-warning
- **AnalysisLevel**: `latest-Recommended` with `EnforceCodeStyleInBuild`
- **Rule suppressions** go in `.editorconfig`, not in `.csproj` `<NoWarn>` — keeps rules centralized and auditable
- CA1707 (underscore in identifiers) is suppressed only under `tests/` for xUnit `Method_Scenario_Expected` naming

## Platform Notes

- **macOS**: MoltenVK is bundled via `Silk.NET.MoltenVK.Native`. `VulkanContext.EnsureMoltenVK()` symlinks the dylib and sets `VK_DRIVER_FILES` before window creation.
- **Shaders**: GLSL sources in `Shaders/`, compiled to SPIR-V at runtime via shaderc. Pre-compiled `.spv` can be placed in `Resources/Shaders/`.
- **Fonts**: System fonts loaded via FreeType (`FreeTypeSharp`). Fallback chains: Helvetica (macOS), Segoe UI (Windows), DejaVu Sans (Linux).

## Development Guidelines

- Core library must remain dependency-free (pure C#)
- EcmaScript engine must remain dependency-free (pure C#, DLR ships with .NET)
- EcmaScript Console has no dependencies beyond the EcmaScript engine
- All Vulkan/GPU code stays in the Demo project
- `unsafe` is enabled globally (required by Silk.NET Vulkan bindings)
- Tests use `MonospaceTextMeasurer` (deterministic, no GPU needed)
- EcmaScript engine defaults to strict mode; no sloppy-mode support
- .NET interop is sandboxed: only explicitly mounted types are accessible from JS
