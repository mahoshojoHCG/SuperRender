# SuperRenderer

A complete HTML+CSS rendering engine built with C# (.NET 10), using Silk.NET + Vulkan as the graphics backend. Includes a DLR-based ECMAScript 2025 engine for scripting support.

## Project Structure

- `src/Document/`
  - `SuperRender.Document/` — Document model: DOM (Node, Element, Document), HTML/CSS parsers, Stylesheet, Selector, Color, events, DomMutationApi (zero external deps)
- `src/Renderer/`
  - `SuperRender.Renderer.Rendering/` — Style resolution, layout engine (block/inline/flex), painting, RenderPipeline orchestrator
  - `SuperRender.Renderer.Gpu/` — Shared Vulkan rendering infrastructure: GPU context, pipelines, font atlas, batch renderers
  - `SuperRender.Renderer.Image/` — Pure C# image decoders: PNG, BMP, baseline JPEG (zero external deps)
- `src/EcmaScript/`
  - `SuperRender.EcmaScript.Compiler/` — Lexer, Parser, AST, JsCompiler (DLR expression tree compiler)
  - `SuperRender.EcmaScript.Runtime/` — JsValue types, Environment, Realm, Builtins (32 standard library objects), Errors
  - `SuperRender.EcmaScript.Engine/` — JsEngine public API, .NET interop (TypeProxy, ObjectProxy)
  - `SuperRender.EcmaScript.Repl/` — Interactive JS console (Node.js-style REPL)
  - `SuperRender.EcmaScript.Dom/` — JS DOM API bindings: bridges C# DOM to JS runtime (document, element, window, fetch, location, history APIs)
- `src/Browser/`
  - `SuperRender.Browser/` — Browser application with tabs, address bar, networking, cookies, storage (SQLite), HTTP caching, CORS, HiDPI, image loading
- `src/Demo/`
  - `SuperRender.Demo/` — Minimal Vulkan demo app (uses Gpu library)
- `tests/SuperRender.Document.Tests/` — xUnit tests for Document (546 tests: HTML, CSS, DOM, selectors, entities, color functions, at-rules, media queries, gradients, box-shadow)
- `tests/SuperRender.Renderer.Tests/` — xUnit tests for Renderer (543 tests: Style, Layout, Flexbox, Grid, Painting, Transforms, Transitions, Filters, Logical Properties)
- `tests/SuperRender.Renderer.Image.Tests/` — xUnit tests for Image decoders (25 tests: PNG, BMP, JPEG)
- `tests/SuperRender.EcmaScript.Tests/` — xUnit tests for EcmaScript (668 tests)
- `tests/SuperRender.Browser.Tests/` — xUnit tests for Browser + DOM bindings (265 tests)

## Build & Run

```bash
dotnet build              # Build all projects (warnings are errors)
dotnet test               # Run all unit tests (2047 total)
dotnet run --project src/Demo/SuperRender.Demo  # Launch the demo window (requires Vulkan)
dotnet run --project src/Browser/SuperRender.Browser  # Launch the browser (requires Vulkan)
dotnet run --project src/EcmaScript/SuperRender.EcmaScript.Repl  # Launch the JS console REPL
```

## Architecture

**Rendering pipeline:** HTML string → Parse → DOM tree → Style resolution (cascade/specificity/inheritance/custom properties/at-rules) → Layout (block/inline/flex/grid/table/float box model) → Paint commands → Vulkan GPU rendering

**Key components:**
- `RenderPipeline` — orchestrator with dirty-flag optimization; owns `TransitionController` and `AnimationController`, which run after style resolution and overwrite transitionable/animated fields before layout. Re-renders each frame while a transition or animation is active.
- `TransitionController` — tracks in-flight CSS property transitions keyed by (element, property). Diffs newly resolved styles against the previous snapshot, starts interpolations for properties listed in `transition-property`, honors `transition-duration`/`delay`/`timing-function`, and writes interpolated values back into the computed style each tick. Handles colors, lengths, opacity, and transform lists.
- `AnimationController` — loads `@keyframes` from the document stylesheets, evaluates each element's `animation-name`/`duration`/`delay`/`iteration-count`/`direction`/`timing-function`/`fill-mode`, interpolates between adjacent keyframes, and overwrites animated fields on the computed style each frame.
- `HtmlParser` — state-machine tokenizer + tree builder with auto-closing rules and adoption agency algorithm. `HtmlTokenizer` split into partial classes: `HtmlTokenizer.cs` (fields, dispatch loop, helpers) and `HtmlTokenizer.States.cs` (16 state handler methods)
- `CssParser` — tokenizer + parser with shorthand expansion (margin/padding/border/flex/flex-flow/border-radius/inset/background/outline/place-*/logical properties), at-rule parsing (`@import`/`@media`/`@supports`/`@layer`/`@scope`/`@font-face`/`@keyframes`/`@namespace`), CSS nesting support, gradient parsing (linear/radial/conic), box-shadow parsing. Split into partial classes: `CssParser.cs` (core parsing, value parsing, token helpers, at-rules, nesting) and `CssParser.Shorthands.cs` (all `Expand*Shorthand` methods)
- `StyleResolver` — cascade, specificity, `!important`, global keywords (`initial`/`inherit`/`unset`/`revert`), custom properties (`--*`/`var()` with cycle detection), `@media`/`@supports` conditional evaluation, `@layer` cascade layers, inherited properties (color, font-size, font-family, font-weight, font-style, text-align, line-height, white-space, visibility, text-transform, letter-spacing, word-spacing, cursor, word-break, overflow-wrap, list-style-type, text-indent, tab-size, font-variant, direction, quotes), `hidden` attribute, box-sizing, min/max constraints, overflow, z-index, opacity, CSS font-family list parsing, `calc()`/`min()`/`max()`/`clamp()` + 17 extended math functions evaluation, viewport units (vw/vh/vmin/vmax + dynamic/small/large variants), absolute units (cm/mm/in/pc/Q), font-relative units (ex/ch/lh/rlh), angle units (deg/grad/rad/turn), time units (s/ms), CSS counters (`counter-reset`/`counter-increment` accumulated in a flat scope during tree traversal; `counter()`/`counters()` resolved inside `content:` with decimal/upper-roman/lower-roman/upper-alpha/lower-alpha formatting). Text nodes receive propagated text-decoration, text-shadow, and vertical-align from their parent element so text runs paint correctly (these properties are not inherited normally but text nodes have no declarations). Split into partial classes: `StyleResolver.cs` (core resolution, custom property wiring, counters, content-value parsing), `StyleResolver.BoxModel.cs` (+ logical properties), `StyleResolver.Typography.cs`, `StyleResolver.Color.cs`, `StyleResolver.Position.cs`, `StyleResolver.Flex.cs`, `StyleResolver.Helpers.cs` (+ ResolveAngle/ResolveTime), `StyleResolver.Transform.cs`, `StyleResolver.Animation.cs`, `StyleResolver.Grid.cs`, `StyleResolver.Background.cs`, `StyleResolver.Visual.cs` (also parses `text-shadow` into `ComputedStyle.TextShadows` list), `StyleResolver.Filter.cs`
- `LayoutEngine` — block layout, inline layout with word-wrap, anonymous block wrapping, inline-block layout, flexbox layout (`FlexLayout` with direction/wrap/justify/align-items/align-content/grow/shrink/basis/gap, decomposed into phases: collect items, wrap into lines, resolve sizes, position items, align-content distribution), grid layout (`GridLayout` with template columns/rows, `fr` units, `repeat()`, auto-placement, explicit placement), table layout (`TableLayout` with fixed/auto algorithms, border-collapse/spacing), float layout (`FloatLayout` with left/right floating, clear), position:static/relative/absolute/fixed/sticky, white-space modes, replaced element sizing for `<img>` with intrinsic dimensions and aspect ratio preservation, `aspect-ratio` property, margin collapsing (sibling, parent-first-child, empty blocks). Inline layout aligns mixed-font-size runs to the largest ascent on each line and applies per-run `vertical-align` (baseline/top/bottom/middle/sub/super/text-top/text-bottom/length) relative to the line box before line-box alignment. `BoxDimensions` provides `HorizontalEdge`/`VerticalEdge`/`LeftEdge`/`TopEdge` helpers for margin+border+padding calculations. `ImageIntrinsicSizeHelper` provides shared image dimension reading used by both `BlockLayout` and `FlexLayout`.
- `Painter` — generates FillRect/DrawText/DrawImage/PushClip/PopClip/DrawGradient/DrawBoxShadow/DrawOutline/PushTransform/PopTransform/PushFilter/PopFilter commands from layout tree, per-run inline background painting, text-decoration rendering, z-index ordering, overflow:hidden clipping, list markers, opacity compositing, visibility:hidden support, `<img>` alt text fallback, gradient backgrounds, box-shadow (before background), outline (after content), `transform-origin`-aware matrix push/pop (pre/post-multiplies translations around the border-box pivot), text-shadow painted before the glyph run using a 9-sample weighted kernel to approximate Gaussian blur
- `SelectorMatcher` — CSS Selectors Level 4: descendant/child/adjacent-sibling/general-sibling combinators, attribute selectors (with case-insensitive `[i]`/case-sensitive `[s]` flags), structural pseudo-classes (`:first-child`/`:last-child`/`:nth-child(An+B)`/`:nth-of-type(An+B)`/`:nth-last-of-type(An+B)`/`:only-child`/`:first-of-type`/etc.), functional pseudo-classes (`:not()`/`:is()`/`:where()`/`:has()`), dynamic pseudo-classes (`:hover`/`:focus`/`:active`/`:focus-within`/`:focus-visible`/`:link`/`:visited`/`:any-link`), form pseudo-classes (`:enabled`/`:disabled`/`:checked`/`:required`/`:optional`/`:read-only`/`:read-write`/`:placeholder-shown`), linguistic pseudo-classes (`:lang()`/`:dir()`), pseudo-elements (`::before`/`::after`/`::first-line`/`::first-letter`/`::marker`/`::placeholder`/`::selection`/`::backdrop`), `:root`/`:empty`/`:defined`/`:scope`/`:target`
- `SelectionPainter` — generates highlight FillRect commands for text selection ranges, font-aware width measurement
- `TextHitTester` — hit-tests mouse coordinates against laid-out TextRuns to find character positions, font-aware measurement, respects overflow:hidden clip regions (clipped text cannot be selected)
- `LayoutBoxHitTester` — hit-tests layout boxes by coordinate to find clicked DOM elements, walks to `<a>` ancestors for link navigation
- `TextSelectionState` — tracks selection start/end as `TextPosition(RunIndex, CharOffset)`
- `VulkanRenderer` — frame loop with quad pipeline (backgrounds/borders) + text pipeline (font atlas with alpha blending) + gradient pipeline (linear/radial with SDF clipping) + shadow pipeline (SDF-based Gaussian blur), HiDPI content scale support. Segment-based draw lists carry a per-segment 2D transform matrix composed from `PushTransformCommand`/`PopTransformCommand`; each segment's push-constant block is `segment.Transform * projection`, so CSS transforms are applied on the GPU without CPU-side vertex rewrites.
- `DomMutationApi` — runtime DOM modification with automatic re-layout
- `DomEvent` / `MouseEvent` / `KeyboardEvent` — DOM event classes with capture/target/bubble propagation
- `EventListener` — registered event handler on a DOM node (type, handler, capture flag)
- `UserAgentStylesheet` — default browser CSS styles (body margin, heading sizes/bold, list indent, text-level semantics: bold, italic, underline, strikethrough, monospace, link styling, pre white-space:pre)
- `InteractionStateHelper` — manages `:hover`/`:focus`/`:active` state flags on DOM elements, fires mouseenter/mouseleave events

## Image Decoding

Pure C# image decoders in `SuperRender.Renderer.Image` (zero external dependencies).

**Supported formats:**
- `PngDecoder` — PNG (color types 0/2/3/4/6, bit depths 1-16, all 5 filter types, PLTE/tRNS transparency, DEFLATE via `System.IO.Compression`)
- `BmpDecoder` — BMP (24-bit/32-bit uncompressed, bottom-up/top-down row order)
- `JpegDecoder` — Baseline JPEG (SOF0, Huffman decode, IDCT, YCbCr→RGB, 4:4:4/4:2:0 subsampling)

**Pipeline:** `ImageDecoder.Decode(byte[])` → auto-detects format → returns `ImageData { Width, Height, byte[] Pixels }` (RGBA, 4 bytes/pixel, row-major). Hot decode paths use `Span<byte>`/`ReadOnlySpan<byte>` and `unsafe` fixed pointers for bounds-check elimination. JPEG Huffman decoding caches table arrays as `ReadOnlySpan<T>` locals for reduced indirection. PNG uses `ArrayPool<byte>` for intermediate reconstruction buffers. `JpegDecoder.GpuYCbCrConverter` static callback enables optional GPU-accelerated YCbCr→RGBA conversion. `JpegDecoder.GpuDequantIdctTransformer` static callback enables optional GPU-accelerated combined dequantization + IDCT.

**Integration:** `Tab.LoadImagesAsync()` fetches image bytes via `ResourceLoader.FetchImageBytesAsync()` (supports HTTP/file/data: URIs), decodes with `ImageDecoder`, stores in `ImageCache`, sets `data-natural-width`/`data-natural-height` attributes on `<img>` elements for layout sizing.

## CSS Feature Coverage

**Color:** hex (#RGB/#RRGGBB/#RRGGBBAA), `rgb()`/`rgba()` (comma + space-separated, percentage alpha, `none` keyword), `hsl()`/`hsla()`, `hwb()`, `lab()`/`lch()`, `oklab()`/`oklch()`, `color()` (sRGB), `color-mix()`, `light-dark()`, `currentcolor`, 148 named colors, 19 system colors

**Custom Properties:** `--*` declarations, `var()` with fallback, cyclic dependency detection, inherits by default. `CustomPropertyResolver` handles var() substitution before property application.

**At-rules:** `@import`, `@media` (with `MediaQuery` evaluator: all/screen/print, min-width/max-width/min-height/max-height, orientation, and/not/only, comma lists), `@supports` (with `SupportsCondition` evaluator: and/or/not, property feature queries), `@layer`, `@scope`, `@font-face` (parsed), `@keyframes` (parsed), `@namespace`. CSS Nesting (`.parent { .child {} }`, `&` selector expansion).

**Selectors:** type, class, ID, universal, combinators (descendant/child/adjacent-sibling/general-sibling), attribute selectors with `[i]`/`[s]` case flags, 26 pseudo-classes (structural, dynamic, form, linguistic, logical including `:has()`), 9 pseudo-element types.

**Values & Units:** `px`/`em`/`rem`/`pt`/`%`/`auto`, absolute (`cm`/`mm`/`in`/`pc`/`Q`), font-relative (`ex`/`ch`/`lh`/`rlh`/`cap`/`ic`), viewport (`vw`/`vh`/`vmin`/`vmax` + dynamic/small/large variants), angles (`deg`/`grad`/`rad`/`turn`), time (`s`/`ms`), resolution (`dpi`/`dpcm`/`dppx`). Math: `calc()`/`min()`/`max()`/`clamp()` + `abs()`/`sign()`/`round()`/`mod()`/`rem()`/`sin()`/`cos()`/`tan()`/`asin()`/`acos()`/`atan()`/`atan2()`/`pow()`/`sqrt()`/`hypot()`/`log()`/`exp()`.

**Layout:** block, inline, inline-block, flex (with align-content), grid (template rows/columns, fr, repeat, auto-placement), table (fixed/auto), float/clear, `display: contents`/`list-item`/`inline-flex`, position fixed/sticky, logical properties, aspect-ratio, margin collapsing.

**Visual Effects:** `linear-gradient()`/`radial-gradient()`/`conic-gradient()` + repeating variants (GPU-rendered), `box-shadow` (GPU SDF blur), `outline`, CSS transforms (18 functions, 4x4 matrix), CSS transitions (timing functions: ease/linear/cubic-bezier/steps), CSS animations (@keyframes), CSS filters (blur/brightness/contrast/grayscale/sepia/invert/hue-rotate/saturate/drop-shadow), `backdrop-filter`, `mix-blend-mode`, `clip-path`.

## EcmaScript Engine

**Pipeline:** JS source → Lexer (tokens) → Parser (AST) → JsCompiler (DLR Expression trees) → Compiled delegate → Execution

**Key components:**
- `Lexer` — character-by-character scanner, full ES2025 token set
- `Parser` — recursive descent + Pratt parser (20 precedence levels), ASI, arrow detection, destructuring, modules
- `JsCompiler` — AST-to-DLR Expression tree compiler with `RuntimeHelpers` for JS semantics, labeled statement support (`break label`/`continue label`). Split into partial classes: `JsCompiler.cs` (core dispatch), `JsCompiler.Statements.cs`, `JsCompiler.Expressions.cs`, `JsCompiler.Classes.cs`, `JsCompiler.Functions.cs`, `JsCompiler.Helpers.cs`
- `JsValue` hierarchy — `IDynamicMetaObjectProvider` base, JsObject with prototype chain, JsFunction with closures
- `Environment` — lexical scope chain with TDZ and const enforcement
- `Realm` — global object + intrinsic prototypes (Object, Function, Array, String, Number, Boolean, RegExp, Date, Error, Symbol, Map, Set, Promise, Iterator, Generator, BigInt, WeakRef, FinalizationRegistry, ArrayBuffer, SharedArrayBuffer) + EvalFactory/FunctionFactory delegates for dynamic compilation
- `Builtins` — 32 standard library objects (Object, Array, String, Number, Math, JSON, Date, RegExp, Map, Set, Promise, Proxy, Reflect, BigInt, WeakRef, FinalizationRegistry, Iterator, ArrayBuffer, SharedArrayBuffer, TypedArrays, Atomics, Intl, Temporal, ShadowRealm, structuredClone, etc.)
- `GeneratorCoroutine` — thread-based coroutine for generator/async state machines
- `JsGeneratorObject` — JS generator with next/return/throw, iterator protocol via Symbol.iterator
- `JsEngine` — public API entry point, sandboxed .NET interop via `RegisterType<T>()`/`SetValue()`

**Deferred features** tracked in `docs/es-2025-todos.md`: Tail Call Optimization, Decorators, Pattern Matching, Module Workers. Most ES2025 features are now implemented (BigInt, Iterator Helpers, Set Methods, RegExp enhancements, Promise.withResolvers, groupBy, String well-formed, WeakRef, FinalizationRegistry, SharedArrayBuffer, Atomics, TypedArrays, structuredClone, eval, Function(), Pipeline Operator, Intl, Temporal, ShadowRealm, Import Assertions, for-await-of, Array.fromAsync). Manual test pages for JS features at `Resources/TestPages/JS/`; see `docs/MANUAL_TESTS_JS.md`.

**Remaining CSS feature gaps** tracked in `docs/css-todos.md`: subgrid, masonry layout, full @font-face font loading, variable fonts, container query units (cqw/cqh), full writing-mode layout integration, scroll-driven animations. Many CSS features are now implemented (custom properties, at-rules, media queries, CSS nesting, transforms, transitions, animations, grid, table, float layout, gradients, box-shadow, filters, blend modes, clip-path, logical properties, and more). Manual test pages for CSS features at `Resources/TestPages/CSS/`; see `docs/MANUAL_TESTS_CSS.md`.

**Deferred HTML features** tracked in `docs/html-todos.md`: full WHATWG tokenizer states, forms, tables, embedded content, Shadow DOM, MutationObserver, and more.

**Deferred browser features** tracked in `docs/browser-todos.md`: page zoom, find-in-page, bookmarks, downloads, and more.

**Deferred DOM binding features** tracked in `docs/es-2025-dom-todos.md`: full Web API coverage, typed arrays, workers, and more.

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
- CA1720 (identifier contains type name) is suppressed under `src/Renderer/SuperRender.Renderer.Rendering/Style/` for CSS-mandated property names (e.g., `CursorType.Pointer`)
- CA1848 (LoggerMessage delegates) and CA1873 (expensive logging params) suppressed globally — project log volume doesn't warrant the complexity

## Platform Notes

- **macOS**: MoltenVK is bundled via `Silk.NET.MoltenVK.Native`. `VulkanContext.EnsureMoltenVK()` symlinks the dylib and sets `VK_DRIVER_FILES` before window creation.
- **Shaders**: GLSL sources in `Shaders/`, compiled to SPIR-V at runtime via shaderc. Pre-compiled `.spv` can be placed in `Resources/Shaders/`.
- **Fonts**: Dynamic font atlas with on-demand glyph rendering via FreeType (`FreeTypeSharp`). System font discovery by family name via `SystemFontLocator`. Platform-specific generic family defaults (serif, sans-serif, monospace, cursive, fantasy, system-ui) via `GenericFontFamilies`. CJK fallback fonts (PingFang SC on macOS, Microsoft YaHei on Windows, Noto Sans CJK on Linux). CSS font-family comma-separated list support with `FontFamilyParser`.

## Development Guidelines

- Document project must remain dependency-free (pure C#)
- Renderer.Image project must remain dependency-free (pure C#)
- EcmaScript.Compiler, Runtime, and Engine must remain dependency-free (pure C#, DLR ships with .NET)
- EcmaScript.Repl has no dependencies beyond the EcmaScript.Engine project
- All Vulkan/GPU code stays in the Renderer.Gpu project (shared by Demo and Browser)
- `unsafe` is enabled globally (required by Silk.NET Vulkan bindings)
- Tests use `MonospaceTextMeasurer` (deterministic, no GPU needed)
- EcmaScript engine defaults to strict mode; no sloppy-mode support
- .NET interop is sandboxed: only explicitly mounted types are accessible from JS
- The `Document` class requires a `DomDocument` using alias in files under `SuperRender.Document.*` or `SuperRender.Renderer.*` namespaces due to namespace collision with the `SuperRender.Document` namespace segment
- Embedded resource names are derived via reflection (`typeof(T).Assembly.GetName().Name`), never hardcoded — keeps resource lookup stable across project renames
- CSS property name strings are centralized in `CssPropertyNames` (Style/ directory) — use constants, not string literals
- HTML tag name strings are centralized in `HtmlTagNames` (Dom/ directory) — use constants, not string literals
- HTML attribute name strings are centralized in `HtmlAttributeNames` (Dom/ directory) — use constants, not string literals. Also contains common attribute values (`Stylesheet`, `TargetBlank`)
- Style default values (font size, viewport dimensions) are centralized in `PropertyDefaults` — use constants, not magic numbers
- DOM wrapper boilerplate uses `JsWrapperExtensions` helpers (`DefineMethod`, `DefineGetter`, `DefineGetterSetter`) — prefer these over raw `DefineOwnProperty` calls
- Browser and Gpu projects use `Microsoft.Extensions.Logging` (ILogger); zero-dep projects keep Console.WriteLine
- Inline HTML pages (welcome, error) are embedded resources in `Resources/` — not inline strings
- DevTools is per-tab: each Tab owns its own DevToolsWindow; closing a tab closes its DevTools
- JS errors surface line/column from `JsErrorBase` in ConsoleMessage and DevTools output
- `RuntimeHelpers.SetLocation()` emits source line/column at statement boundaries in compiled JS code; `ExecutionContext.CurrentLine`/`CurrentColumn` (thread-static) provide location context for runtime error throws
- Large classes use `partial class` splits by concern (e.g., `JsCompiler.Statements.cs`, `StyleResolver.BoxModel.cs`). Each partial file replicates the same `using` directives and `#pragma` suppressions as the main file
- Browser test classes use `TestEnvironmentHelper.Create(html)` for shared JsEngine/Document/DomBridge setup — prefer this over per-class `CreateTestEnvironment` methods

### GPU-First Rendering

Prefer GPU rendering and compute over CPU wherever feasible:
- **Use Vulkan graphics pipelines** for all visual output (rects, text, effects). Never rasterize pixels on the CPU when a shader can do it.
- **Use Vulkan compute shaders** for parallelizable work: image decoding, glyph rasterization, layout pre-computation, hit-testing acceleration, or any batch data transformation that maps well to GPU threads.
- **Minimize per-frame CPU→GPU data transfer.** Use persistent mapped buffers (ring-buffered for double-buffering) instead of per-frame alloc/free. Upload only dirty regions of textures, not the entire atlas.
- **Let the GPU do index math.** Use `vertexOffset` in `vkCmdDrawIndexed` rather than adjusting index values on the CPU.
- **Enable hardware blending** on all pipelines that may carry alpha (quads included). Do not rely on draw-order opacity hacks.
- **Batch aggressively.** Merge draw calls where possible; minimize pipeline switches and scissor-rect flushes per frame.
- When adding a new rendering feature (gradients, rounded corners, shadows, filters, transforms), implement it as a GPU shader pipeline — not as CPU-generated quads or pixel manipulation.

### GPU Compute Infrastructure

- `ComputePipelineManager` — creates Vulkan compute pipelines with SSBO descriptors and push constants
- `IdctCompute` — orchestrates GPU-accelerated JPEG 8x8 IDCT: uploads DCT coefficients → dispatches compute shader → reads back pixel values. Supports both standard IDCT (`TransformBlocks`) and combined dequant+IDCT (`TransformBlocksWithDequant`). Falls back gracefully when compute pipeline is unavailable.
- `DequantIdctPipeline` — Vulkan compute pipeline with 3 SSBOs (raw DCT coefficients, quantization table, output pixels) for combined dequantization + IDCT in a single GPU dispatch
- `YCbCrComputePipeline` — Vulkan compute pipeline with 4 SSBOs for YCbCr→RGBA color conversion
- `YCbCrCompute` — orchestrates GPU-accelerated YCbCr→RGBA conversion: uploads per-pixel Y/Cb/Cr planes → dispatches compute shader → reads back RGBA pixels. `JpegDecoder.GpuYCbCrConverter` static callback enables GPU path while keeping Renderer.Image dependency-free.
- `Shaders/idct.comp.glsl` — compute shader: 64 threads per workgroup process one 8x8 DCT block in parallel
- `Shaders/idct_dequant.comp.glsl` — compute shader: 64 threads per workgroup, shared memory dequantization + IDCT in a single dispatch (eliminates one CPU→GPU transfer vs separate passes)
- `Shaders/ycbcr_to_rgba.comp.glsl` — compute shader: 256 threads per workgroup, each thread converts one pixel from YCbCr to RGBA
- `ShaderCompiler` supports `"comp"` stage (shaderc kind 5) via `LoadOrCompileComputeShader()`
- `Shaders/gradient.vert.glsl` — vertex shader for gradient quads (same layout as quad pipeline)
- `Shaders/gradient.frag.glsl` — fragment shader with rounded-rect SDF clipping for gradient rectangles, multi-stop color interpolation for linear/radial gradients
- `Shaders/shadow.frag.glsl` — fragment shader with SDF-based Gaussian blur approximation for box-shadow rendering
- `GradientRenderer` — builds GPU vertex data for linear/radial gradients (multi-stop with hardware color interpolation), box shadows (SDF-based), and outlines (4-rect stroke)

## Browser

A Vulkan-powered browser application with tabbed browsing support.

**Key components:**
- `BrowserWindow` — main orchestrator: owns renderer, tab manager, chrome, input handler; drains timer queue and main-thread queue each frame
- `BrowserChrome` — renders tab bar (32px) + address bar (36px) as PaintCommands, vertically centered text via `CenterTextY`, font-metrics-accurate cursor positioning
- `TabManager` — tab lifecycle: create, close, switch tabs
- `Tab` — individual browsing context: owns Document, RenderPipeline, JsEngine, DomBridge, TextSelectionState, ScrollState, NavigationHistory, TimerScheduler, SessionStorage
- `InputHandler` — routes keyboard/mouse to chrome or content area, handles text selection drag, address bar click-to-cursor, right-click context menus, link click navigation, DOM event dispatch, mouse hover tracking with `:hover`/`:focus`/`:active` state management, keyboard shortcuts (Cmd/Ctrl+T/W/Tab/L/R, F5, F12, Cmd+Shift+I, Escape, scroll keys)
- `InteractionStateHelper` — manages hover/focus/active CSS pseudo-class flags, fires mouseover/mouseout/mouseenter/mouseleave events
- `ScrollState` — per-tab vertical scroll position tracking with mouse wheel and keyboard scrolling, scrollbar geometry computation
- `NavigationHistory` — per-tab URI history stack with back/forward cursor
- `ContextMenu` — reusable context menu: items with hover highlighting, hit-testing, PaintList rendering
- `ClipboardHelper` — cross-platform clipboard access (pbcopy/pbpaste on macOS, xclip on Linux, PowerShell on Windows)
- `ResourceLoader` — HTTP client for fetching HTML/CSS/JS/image resources, file:// URI support for local files, sr:// for embedded test pages, data: URI for embedded images
- `SecurityPolicy` — same-origin checks + CORS header validation for sub-resources
- `UrlResolver` — resolves relative URLs against base URI (root-relative paths resolve to origin, not file://), normalizes address bar input (supports http/https/file/sr/about schemes, bare domain names, absolute file paths)
- `TestPages` — provides access to embedded manual test page resources via `sr://test/{name}` protocol, supports P0/P1/JS/CSS subdirectories. JS test pages (`sr://test/JS/{name}`) cover ECMAScript 2025 features (BigInt, Iterator Helpers, Set Methods, Temporal, Intl, etc.). CSS test pages (`sr://test/CSS/{name}`) cover advanced CSS features (transforms, transitions, animations, grid, filters, etc.). See `docs/MANUAL_TESTS_JS.md` and `docs/MANUAL_TESTS_CSS.md` for test plans.
- `ImageCache` — thread-safe in-memory cache for decoded images, keyed by URL
- `CookieJar` — in-memory cookie storage with Set-Cookie parsing, domain/path matching, Secure/HttpOnly/SameSite enforcement
- `HttpCache` — SQLite-backed HTTP response cache with Cache-Control/ETag/Last-Modified/Expires support

**HiDPI support:** Content scale derived from `FramebufferSize / Size`. Layout engine works in logical (CSS) pixels; projection matrix maps logical → physical coordinates. Font atlas is generated at `BaseFontSize * contentScale` for sharp text on Retina/HiDPI displays.

**Text selection:** Click-and-drag in the content area creates a text selection. `TextHitTester` maps mouse coordinates to `(runIndex, charOffset)` positions with font-aware measurement and overflow:hidden clip exclusion. `SelectionPainter` generates blue highlight rectangles behind selected text. `TextSelectionState` tracks start/end positions with ordered normalization. Address bar supports click-and-drag text selection.

**Context menus:** Right-click the address bar for Cut/Copy/Paste/Select All. Right-click the content area for Copy (when text selected)/Select All/View Source/Developer Tools.

**Content scrolling:** `ScrollState` per tab tracks `scrollY` with bounds clamping. Mouse wheel (via `mouse.Scroll` Silk.NET event), keyboard arrows/PageUp/PageDown/Home/End/Space. Scroll offset applied to content paint commands in `OnRender`. Visual scrollbar indicator (track + thumb) rendered on the right edge. Scroll-to-top on navigation.

**Link navigation:** `LayoutBoxHitTester.HitTest()` finds the deepest layout box at a coordinate. `FindAnchorAncestor()` walks up the DOM tree to find `<a>` elements. Click (mouseup within 5px of mousedown) on a link extracts `href`, resolves against current URI, navigates. `target="_blank"` opens in a new tab.

**Back/Forward history:** `NavigationHistory` per tab stores a list of URIs with a cursor index. `Tab.NavigateAsync()` pushes to history; `GoBackAsync()`/`GoForwardAsync()` move the cursor without pushing. Back/Forward chrome buttons call these methods. Address bar and window title update after history navigation.

**Keyboard shortcuts:** Platform-aware (Cmd on macOS, Ctrl on Windows/Linux). Global: Cmd+T (new tab), Cmd+W (close), Cmd+Tab/Shift+Tab (switch), Cmd+L (focus+select address bar), Cmd+R/F5 (reload), Cmd+[/Cmd+Left (back), Cmd+]/Cmd+Right (forward), F12/Cmd+Shift+I (toggle DevTools), Escape (unfocus). Content area: arrow keys (scroll step), Page Up/Down/Space (scroll page), Home/End (top/bottom).

**DOM events:** `Node` has `AddEventListener`/`RemoveEventListener`/`DispatchEvent` with capture/target/bubble propagation. `DomEvent`, `MouseEvent`, `KeyboardEvent` in Document. `JsEventWrapper` bridges to JS. `InputHandler` dispatches `mousedown`/`mouseup`/`click`/`mousemove`/`mouseover`/`mouseout`/`mouseenter`/`mouseleave` to hit DOM nodes. `DOMContentLoaded` and `load` fired after page load.

**Timers:** `TimerScheduler` (in EcmaScript.Dom) uses `Stopwatch` for monotonic time. `setTimeout` with real delay, `setInterval` (min 4ms), `requestAnimationFrame` (fires next frame). Timer queue drained in `BrowserWindow.OnRender` before painting. `cancelAnimationFrame`/`clearTimeout`/`clearInterval` cancel by ID.

**Developer Tools:** Separate Vulkan window opened via F12/Cmd+Shift+I/right-click context menu. `DevToolsWindow` creates its own `IWindow` + `VulkanRenderer`; rendering is driven from the main window's render loop (Silk.NET only drives `Run()` on one window, so secondary windows are rendered manually via `RenderFrame()`). GLFW's `glfwPollEvents()` handles input events for all windows. `DevToolsPanel` builds PaintList for the console UI: toolbar, scrollable log area, JS input line. `ConsoleCapture` (TextWriter subclass) redirects `console.log/warn/error` to per-tab `ConsoleLog`. JS execution requests are queued via `ConcurrentQueue<string>` and drained on the main thread. `Tab.LoadHtmlDirect(html)` replaces the old reflection-based page loading and also sets up the JsEngine. `Tab.ExecuteConsoleInput(code)` lazy-initializes the JsEngine if needed.

**Cookies & Storage:** `CookieJar` stores cookies in memory with full `Set-Cookie` parsing (Domain, Path, Expires, Max-Age, Secure, HttpOnly, SameSite). `document.cookie` JS getter/setter via `JsDocumentWrapper`. `localStorage` backed by SQLite (`StorageDatabase`), `sessionStorage` per-tab in memory. `JsStorageWrapper` bridges to JS with `getItem`/`setItem`/`removeItem`/`clear`/`key`/`length`.

**HTTP Caching:** `HttpCache` backed by SQLite (`CacheDatabase`). Checks freshness (max-age, Expires), sends conditional requests (If-None-Match, If-Modified-Since), handles 304 Not Modified.

**Fetch API:** `JsFetchApi` installs global `fetch(url, options)` returning a JS Promise. Async HTTP via `Task.Run` + main-thread queue marshaling. `JsResponseWrapper` exposes `status`/`ok`/`headers`/`.text()`/`.json()`.

**Location & History APIs:** `JsLocationWrapper` exposes `href`/`protocol`/`host`/`hostname`/`port`/`pathname`/`search`/`hash`/`origin`/`assign()`/`replace()`/`reload()`. `JsHistoryWrapper` exposes `pushState()`/`replaceState()`/`back()`/`forward()`/`go()`/`length`/`state`.

**Image loading:** `Tab.LoadImagesAsync()` scans for `<img>` elements after CSS loading, fetches bytes via `ResourceLoader.FetchImageBytesAsync()` (HTTP/file/data: URIs), decodes with `ImageDecoder` (pure C# PNG/BMP/JPEG), stores in `ImageCache`, sets intrinsic dimensions on elements. `Painter.PaintImage()` emits `DrawImageCommand` or renders alt text fallback in a gray placeholder box.

## EcmaScript DOM Bindings

Bridges C# DOM objects to the JS runtime with correct Web API naming conventions.

**Key components:**
- `DomBridge` — entry point: installs `document`, `window`, `fetch` globals into JsEngine, owns `TimerScheduler`, configures cookies/storage/location/history delegates
- `JsNodeWrapper` — wraps Node: nodeType, parentNode, childNodes, appendChild, removeChild, insertBefore, replaceChild, cloneNode, contains, textContent, addEventListener, removeEventListener, dispatchEvent
- `JsElementWrapper` — wraps Element: tagName, id, className, classList, getAttribute/setAttribute/hasAttribute/toggleAttribute, querySelector/querySelectorAll, innerHTML, style, matches, closest, dataset, firstElementChild/lastElementChild/childElementCount, after/before/remove
- `JsDocumentWrapper` — wraps Document: createElement, createTextNode, getElementById, getElementsByTagName/ClassName, body, head, title, cookie
- `JsWindowWrapper` — window global: document, innerWidth/Height, devicePixelRatio, setTimeout/clearTimeout, setInterval/clearInterval, requestAnimationFrame/cancelAnimationFrame, localStorage, sessionStorage, location, history
- `JsEventWrapper` — wraps DomEvent/MouseEvent/KeyboardEvent for JS: type, target, currentTarget, preventDefault, stopPropagation, clientX/Y, key/code
- `JsCssStyleDeclaration` — element.style accessor: camelCase property get/set → inline style attribute
- `JsFetchApi` — global `fetch()` function returning Promise
- `JsLocationWrapper` — `window.location` object
- `JsHistoryWrapper` — `window.history` object
- `JsStorageWrapper` — wraps `localStorage`/`sessionStorage`
- `TimerScheduler` — monotonic timer queue: setTimeout/setInterval/requestAnimationFrame with real delays, drained per frame
- `NodeWrapperCache` — ConditionalWeakTable for 1:1 C# node ↔ JS wrapper identity, event handler mapping for removeEventListener
- `JsWrapperExtensions` — extension methods (`DefineMethod`/`DefineGetter`/`DefineGetterSetter`) that reduce boilerplate in all wrapper classes

## Gpu Rendering Infrastructure

Shared Vulkan rendering library used by both Demo and Browser.

**Key components:**
- `VulkanContext` — Vulkan instance/device/queues + MoltenVK setup for macOS
- `SwapchainManager` — swapchain, render pass, framebuffers
- `PipelineManager` — quad pipeline (solid rects, alpha-blended) + text pipeline (font atlas sampling, alpha-blended) + gradient pipeline (linear/radial gradient rendering with SDF clipping) + shadow pipeline (SDF-based Gaussian blur for box-shadow)
- `BufferManager` — persistent mapped ring buffers for vertex/index data (eliminates per-frame alloc/free), staging helpers for texture uploads, partial-region atlas updates
- `VulkanRenderer` — segment-based frame loop with per-segment scissor clipping, per-segment 2D transform matrix (push-constant `transform * projection`), GPU-side `vertexOffset` in draw calls, HiDPI content scale support, partial dirty-region atlas re-upload. `BrowserWindow` uses two-pass content rendering (quads pass → selection highlights → text pass) to ensure correct z-ordering across clip segments; pass 2 preserves `PushTransform`/`PopTransform`/`PushFilter`/`PopFilter` alongside clips so transformed/filtered text renders with the same matrix as its backgrounds. Scroll offsets re-anchor `PushTransformCommand` pivots via `T(0,scrollY)·M·T(0,-scrollY)` instead of baking translations into vertex data.
- `FontAtlas` — dynamic glyph atlas (2048x4096, BaseFontSize=32, HiDPI-scaled): pre-renders ASCII + common symbols at startup, renders additional glyphs (CJK, Unicode) on demand via FreeType. Regular + bold + monospace variants. CJK fallback font chain. Dirty-region tracking for partial GPU texture uploads.
- `FontAtlasGenerator` — static helpers for atlas generation with explicit font paths
- `SystemFontLocator` — scans system font directories, uses FreeType to read family/style names from font files, builds case-insensitive family→path index. Handles `.ttf`, `.otf`, `.ttc` (multi-face collections with per-face index tracking for bold/italic variants). Resolves CSS font-family lists with generic family fallback.
- `GenericFontFamilies` — maps CSS generic families (serif, sans-serif, monospace, cursive, fantasy, system-ui) to platform-specific real font family names. Provides CJK fallback font lists per platform.
- `QuadRenderer` / `TextRenderer` — PaintList → GPU vertex batch builders, font variant selection (bold/monospace) via DrawTextCommand properties and font-family list walking
- `BitmapFontTextMeasurer` — ITextMeasurer implementation using font atlas metrics, font-family-aware measurement (selects correct glyph set for monospace/bold)
