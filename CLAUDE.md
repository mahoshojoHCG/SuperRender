# SuperRenderer

A complete HTML+CSS rendering engine built with C# (.NET 10), using Silk.NET + Vulkan as the graphics backend. Includes a DLR-based ECMAScript 2025 engine for scripting support.

## Project Structure

- `src/SuperRender.Core/` — Core library (zero external deps): HTML/CSS parsers, DOM, style resolution, layout engine, painting, user-agent stylesheet
- `src/SuperRender.Gpu/` — Shared Vulkan rendering infrastructure: GPU context, pipelines, font atlas, batch renderers
- `src/SuperRender.EcmaScript/` — ECMAScript 2025 engine (DLR-based, zero external deps)
- `src/SuperRender.EcmaScript.Dom/` — JS DOM API bindings: bridges C# DOM to JS runtime (document, element, window APIs)
- `src/SuperRender.EcmaScript.Console/` — Interactive JS console (Node.js-style REPL)
- `src/SuperRender.Browser/` — Browser application with tabs, address bar, networking, CORS, HiDPI
- `src/SuperRender.Demo/` — Minimal Vulkan demo app (uses Gpu library)
- `tests/SuperRender.Tests/` — xUnit tests for Core (68 tests)
- `tests/SuperRender.EcmaScript.Tests/` — xUnit tests for EcmaScript (421 tests)

## Build & Run

```bash
dotnet build              # Build all projects (warnings are errors)
dotnet test               # Run all unit tests (489 total)
dotnet run --project src/SuperRender.Demo  # Launch the demo window (requires Vulkan)
dotnet run --project src/SuperRender.Browser  # Launch the browser (requires Vulkan)
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
- `VulkanRenderer` — frame loop with quad pipeline (backgrounds/borders) + text pipeline (font atlas with alpha blending), HiDPI content scale support
- `DomMutationApi` — runtime DOM modification with automatic re-layout
- `UserAgentStylesheet` — default browser CSS styles (body margin, heading sizes, list indent, etc.)

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

**Deferred CSS features** tracked in `src/SuperRender.Core/css-todos.md`: selectors level 4, flexbox, grid, custom properties, calc(), transforms, transitions, animations, media queries, container queries, CSS nesting, and more (34 sections).

**Deferred HTML features** tracked in `src/SuperRender.Core/html-todos.md`: full WHATWG tokenizer states, tree construction algorithm, adoption agency, forms, tables, embedded content, events, Shadow DOM, MutationObserver, user-agent stylesheet, and more (10 sections).

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
- All Vulkan/GPU code stays in the Gpu project (shared by Demo and Browser)
- `unsafe` is enabled globally (required by Silk.NET Vulkan bindings)
- Tests use `MonospaceTextMeasurer` (deterministic, no GPU needed)
- EcmaScript engine defaults to strict mode; no sloppy-mode support
- .NET interop is sandboxed: only explicitly mounted types are accessible from JS

## Browser

A Vulkan-powered browser application with tabbed browsing support.

**Key components:**
- `BrowserWindow` — main orchestrator: owns renderer, tab manager, chrome, input handler
- `BrowserChrome` — renders tab bar (32px) + address bar (36px) as PaintCommands
- `TabManager` — tab lifecycle: create, close, switch tabs
- `Tab` — individual browsing context: owns Document, RenderPipeline, JsEngine, DomBridge
- `InputHandler` — routes keyboard/mouse to chrome or content area
- `ResourceLoader` — HTTP client for fetching HTML/CSS/JS resources
- `SecurityPolicy` — same-origin checks + CORS header validation for sub-resources
- `UrlResolver` — resolves relative URLs against base URI

**HiDPI support:** Content scale derived from `FramebufferSize / Size`. Layout engine works in logical (CSS) pixels; projection matrix maps logical → physical coordinates.

## EcmaScript DOM Bindings

Bridges C# DOM objects to the JS runtime with correct Web API naming conventions.

**Key components:**
- `DomBridge` — entry point: installs `document` and `window` globals into JsEngine
- `JsNodeWrapper` — wraps Node: nodeType, parentNode, childNodes, appendChild, removeChild, insertBefore
- `JsElementWrapper` — wraps Element: tagName, id, className, classList, getAttribute/setAttribute, querySelector/querySelectorAll, innerHTML, style
- `JsDocumentWrapper` — wraps Document: createElement, createTextNode, getElementById, getElementsByTagName/ClassName, body, head, title
- `JsWindowWrapper` — window global: document, innerWidth/Height, devicePixelRatio, setTimeout/clearTimeout
- `JsCssStyleDeclaration` — element.style accessor: camelCase property get/set → inline style attribute
- `NodeWrapperCache` — ConditionalWeakTable for 1:1 C# node ↔ JS wrapper identity

## Gpu Rendering Infrastructure

Shared Vulkan rendering library used by both Demo and Browser.

**Key components:**
- `VulkanContext` — Vulkan instance/device/queues + MoltenVK setup for macOS
- `SwapchainManager` — swapchain, render pass, framebuffers
- `PipelineManager` — quad pipeline (solid rects) + text pipeline (font atlas sampling)
- `BufferManager` — GPU buffer allocation, vertex/index upload, texture creation
- `VulkanRenderer` — frame loop orchestrator with HiDPI content scale support
- `FontAtlasGenerator` — FreeType-based font atlas (1024x1024, BaseFontSize=32)
- `QuadRenderer` / `TextRenderer` — PaintList → GPU vertex batch builders
- `BitmapFontTextMeasurer` — ITextMeasurer implementation using font atlas metrics
