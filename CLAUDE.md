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
  - `SuperRender.EcmaScript.Runtime/` — JsValue types, Environment, Realm, Builtins (20 standard library objects), Errors
  - `SuperRender.EcmaScript.Engine/` — JsEngine public API, .NET interop (TypeProxy, ObjectProxy)
  - `SuperRender.EcmaScript.Repl/` — Interactive JS console (Node.js-style REPL)
  - `SuperRender.EcmaScript.Dom/` — JS DOM API bindings: bridges C# DOM to JS runtime (document, element, window, fetch, location, history APIs)
- `src/Browser/`
  - `SuperRender.Browser/` — Browser application with tabs, address bar, networking, cookies, storage (SQLite), HTTP caching, CORS, HiDPI, image loading
- `src/Demo/`
  - `SuperRender.Demo/` — Minimal Vulkan demo app (uses Gpu library)
- `tests/SuperRender.Document.Tests/` — xUnit tests for Document (295 tests: HTML, CSS, DOM, selectors, entities)
- `tests/SuperRender.Renderer.Tests/` — xUnit tests for Renderer (216 tests: Style, Layout, Flexbox, Painting)
- `tests/SuperRender.Renderer.Image.Tests/` — xUnit tests for Image decoders (25 tests: PNG, BMP, JPEG)
- `tests/SuperRender.EcmaScript.Tests/` — xUnit tests for EcmaScript (441 tests)
- `tests/SuperRender.Browser.Tests/` — xUnit tests for Browser + DOM bindings (265 tests)

## Build & Run

```bash
dotnet build              # Build all projects (warnings are errors)
dotnet test               # Run all unit tests (1242 total)
dotnet run --project src/Demo/SuperRender.Demo  # Launch the demo window (requires Vulkan)
dotnet run --project src/Browser/SuperRender.Browser  # Launch the browser (requires Vulkan)
dotnet run --project src/EcmaScript/SuperRender.EcmaScript.Repl  # Launch the JS console REPL
```

## Architecture

**Rendering pipeline:** HTML string → Parse → DOM tree → Style resolution (cascade/specificity/inheritance) → Layout (block/inline/flex box model) → Paint commands → Vulkan GPU rendering

**Key components:**
- `RenderPipeline` — orchestrator with dirty-flag optimization
- `HtmlParser` — state-machine tokenizer + tree builder with auto-closing rules and adoption agency algorithm. `HtmlTokenizer` split into partial classes: `HtmlTokenizer.cs` (fields, dispatch loop, helpers) and `HtmlTokenizer.States.cs` (16 state handler methods)
- `CssParser` — tokenizer + parser with shorthand expansion (margin/padding/border/flex/flex-flow/border-radius). Split into partial classes: `CssParser.cs` (core parsing, value parsing, token helpers) and `CssParser.Shorthands.cs` (all `Expand*Shorthand` methods)
- `StyleResolver` — cascade, specificity, `!important`, global keywords (`initial`/`inherit`/`unset`/`revert`), inherited properties (color, font-size, font-family, font-weight, font-style, text-align, line-height, white-space, visibility, text-transform, letter-spacing, word-spacing, cursor, word-break, overflow-wrap, list-style-type), `hidden` attribute, box-sizing, min/max constraints, overflow, z-index, opacity, CSS font-family list parsing, `calc()`/`min()`/`max()`/`clamp()` evaluation, viewport units (vw/vh/vmin/vmax). Split into partial classes: `StyleResolver.cs` (core resolution), `StyleResolver.BoxModel.cs`, `StyleResolver.Typography.cs`, `StyleResolver.Color.cs`, `StyleResolver.Position.cs`, `StyleResolver.Flex.cs`, `StyleResolver.Helpers.cs`
- `LayoutEngine` — block layout, inline layout with word-wrap, anonymous block wrapping, inline-block layout, flexbox layout (`FlexLayout` with direction/wrap/justify/align/grow/shrink/basis/gap, decomposed into phases: collect items, wrap into lines, resolve sizes, position items), position:relative/absolute, white-space modes, replaced element sizing for `<img>` with intrinsic dimensions and aspect ratio preservation. `BoxDimensions` provides `HorizontalEdge`/`VerticalEdge`/`LeftEdge`/`TopEdge` helpers for margin+border+padding calculations. `ImageIntrinsicSizeHelper` provides shared image dimension reading used by both `BlockLayout` and `FlexLayout`.
- `Painter` — generates FillRect/DrawText/DrawImage/PushClip/PopClip commands from layout tree, per-run inline background painting, text-decoration rendering, z-index ordering, overflow:hidden clipping, list markers, opacity compositing, visibility:hidden support, `<img>` alt text fallback
- `SelectorMatcher` — CSS Selectors Level 4: descendant/child/adjacent-sibling/general-sibling combinators, attribute selectors, structural pseudo-classes (`:first-child`/`:last-child`/`:nth-child(An+B)`/`:only-child`/`:first-of-type`/etc.), functional pseudo-classes (`:not()`/`:is()`/`:where()`), dynamic pseudo-classes (`:hover`/`:focus`/`:active`/`:link`/`:visited`), pseudo-elements (`::before`/`::after`), `:root`/`:empty`
- `SelectionPainter` — generates highlight FillRect commands for text selection ranges, font-aware width measurement
- `TextHitTester` — hit-tests mouse coordinates against laid-out TextRuns to find character positions, font-aware measurement, respects overflow:hidden clip regions (clipped text cannot be selected)
- `LayoutBoxHitTester` — hit-tests layout boxes by coordinate to find clicked DOM elements, walks to `<a>` ancestors for link navigation
- `TextSelectionState` — tracks selection start/end as `TextPosition(RunIndex, CharOffset)`
- `VulkanRenderer` — frame loop with quad pipeline (backgrounds/borders) + text pipeline (font atlas with alpha blending), HiDPI content scale support
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

## EcmaScript Engine

**Pipeline:** JS source → Lexer (tokens) → Parser (AST) → JsCompiler (DLR Expression trees) → Compiled delegate → Execution

**Key components:**
- `Lexer` — character-by-character scanner, full ES2025 token set
- `Parser` — recursive descent + Pratt parser (20 precedence levels), ASI, arrow detection, destructuring, modules
- `JsCompiler` — AST-to-DLR Expression tree compiler with `RuntimeHelpers` for JS semantics, labeled statement support (`break label`/`continue label`). Split into partial classes: `JsCompiler.cs` (core dispatch), `JsCompiler.Statements.cs`, `JsCompiler.Expressions.cs`, `JsCompiler.Classes.cs`, `JsCompiler.Functions.cs`, `JsCompiler.Helpers.cs`
- `JsValue` hierarchy — `IDynamicMetaObjectProvider` base, JsObject with prototype chain, JsFunction with closures
- `Environment` — lexical scope chain with TDZ and const enforcement
- `Realm` — global object + 15 intrinsic prototypes + GeneratorPrototype
- `Builtins` — 20 standard library objects (Object, Array, String, Number, Math, JSON, Date, RegExp, Map, Set, Promise, Proxy, Reflect, etc.)
- `GeneratorCoroutine` — thread-based coroutine for generator/async state machines
- `JsGeneratorObject` — JS generator with next/return/throw, iterator protocol via Symbol.iterator
- `JsEngine` — public API entry point, sandboxed .NET interop via `RegisterType<T>()`/`SetValue()`

**Deferred features** tracked in `docs/es-2025-todos.md`: BigInt, WeakRef, SharedArrayBuffer, Intl, Temporal, decorators.

**Deferred CSS features** tracked in `docs/css-todos.md`: grid, custom properties, transforms, transitions, animations, media queries, container queries, CSS nesting, and more.

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
- `TestPages` — provides access to embedded manual test page resources via `sr://test/{name}` protocol, supports P0/P1 subdirectories
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
- `PipelineManager` — quad pipeline (solid rects, alpha-blended) + text pipeline (font atlas sampling, alpha-blended)
- `BufferManager` — persistent mapped ring buffers for vertex/index data (eliminates per-frame alloc/free), staging helpers for texture uploads, partial-region atlas updates
- `VulkanRenderer` — segment-based frame loop with per-segment scissor clipping, GPU-side `vertexOffset` in draw calls, HiDPI content scale support, partial dirty-region atlas re-upload. `BrowserWindow` uses two-pass content rendering (quads pass → selection highlights → text pass) to ensure correct z-ordering across clip segments.
- `FontAtlas` — dynamic glyph atlas (2048x4096, BaseFontSize=32, HiDPI-scaled): pre-renders ASCII + common symbols at startup, renders additional glyphs (CJK, Unicode) on demand via FreeType. Regular + bold + monospace variants. CJK fallback font chain. Dirty-region tracking for partial GPU texture uploads.
- `FontAtlasGenerator` — static helpers for atlas generation with explicit font paths
- `SystemFontLocator` — scans system font directories, uses FreeType to read family/style names from font files, builds case-insensitive family→path index. Handles `.ttf`, `.otf`, `.ttc` (multi-face collections with per-face index tracking for bold/italic variants). Resolves CSS font-family lists with generic family fallback.
- `GenericFontFamilies` — maps CSS generic families (serif, sans-serif, monospace, cursive, fantasy, system-ui) to platform-specific real font family names. Provides CJK fallback font lists per platform.
- `QuadRenderer` / `TextRenderer` — PaintList → GPU vertex batch builders, font variant selection (bold/monospace) via DrawTextCommand properties and font-family list walking
- `BitmapFontTextMeasurer` — ITextMeasurer implementation using font atlas metrics, font-family-aware measurement (selects correct glyph set for monospace/bold)
