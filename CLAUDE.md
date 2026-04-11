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
- `tests/SuperRender.Tests/` — xUnit tests for Core (157 tests)
- `tests/SuperRender.EcmaScript.Tests/` — xUnit tests for EcmaScript (430 tests)
- `tests/SuperRender.Browser.Tests/` — xUnit tests for Browser + DOM bindings (140 tests)

## Build & Run

```bash
dotnet build              # Build all projects (warnings are errors)
dotnet test               # Run all unit tests (727 total)
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
- `StyleResolver` — cascade, specificity, `!important`, inherited properties (color, font-size, font-family, font-weight, font-style, text-align, line-height, white-space), `hidden` attribute, box-sizing, min/max constraints, overflow, z-index, CSS font-family list parsing (comma-separated with fallback)
- `LayoutEngine` — block layout, inline layout with word-wrap, anonymous block wrapping (style-isolated), inline-block layout with shrink-to-fit and visual-height vertical alignment, position:relative/absolute with shrink-to-fit and right-positioning recalculation, white-space modes (normal/pre/nowrap/pre-wrap/pre-line). TextRun height uses fontSize (character height) for tight bounds; line-height used only for inter-line spacing.
- `Painter` — generates FillRect/DrawText/PushClip/PopClip commands from layout tree, per-run inline background painting (for `<mark>` etc.), text-decoration rendering (underline, line-through, overline), z-index ordering for positioned elements with stacking-context clip segments, overflow:hidden clipping, list markers (bullets for ul, numbers for ol)
- `SelectionPainter` — generates highlight FillRect commands for text selection ranges, font-aware width measurement
- `TextHitTester` — hit-tests mouse coordinates against laid-out TextRuns to find character positions, font-aware measurement, respects overflow:hidden clip regions (clipped text cannot be selected)
- `LayoutBoxHitTester` — hit-tests layout boxes by coordinate to find clicked DOM elements, walks to `<a>` ancestors for link navigation
- `TextSelectionState` — tracks selection start/end as `TextPosition(RunIndex, CharOffset)`
- `VulkanRenderer` — frame loop with quad pipeline (backgrounds/borders) + text pipeline (font atlas with alpha blending), HiDPI content scale support
- `DomMutationApi` — runtime DOM modification with automatic re-layout
- `DomEvent` / `MouseEvent` / `KeyboardEvent` — DOM event classes with capture/target/bubble propagation
- `EventListener` — registered event handler on a DOM node (type, handler, capture flag)
- `UserAgentStylesheet` — default browser CSS styles (body margin, heading sizes/bold, list indent, text-level semantics: bold, italic, underline, strikethrough, monospace, link styling, pre white-space:pre)

## EcmaScript Engine

**Pipeline:** JS source → Lexer (tokens) → Parser (AST) → JsCompiler (DLR Expression trees) → Compiled delegate → Execution

**Key components:**
- `Lexer` — character-by-character scanner, full ES2025 token set
- `Parser` — recursive descent + Pratt parser (20 precedence levels), ASI, arrow detection, destructuring, modules
- `JsCompiler` — AST-to-DLR Expression tree compiler with `RuntimeHelpers` for JS semantics
- `JsValue` hierarchy — `IDynamicMetaObjectProvider` base, JsObject with prototype chain, JsFunction with closures
- `Environment` — lexical scope chain with TDZ and const enforcement
- `Realm` — global object + 15 intrinsic prototypes + GeneratorPrototype
- `Builtins` — 20 standard library objects (Object, Array, String, Number, Math, JSON, Date, RegExp, Map, Set, Promise, Proxy, Reflect, etc.)
- `GeneratorCoroutine` — thread-based coroutine for generator/async state machines
- `JsGeneratorObject` — JS generator with next/return/throw, iterator protocol via Symbol.iterator
- `JsEngine` — public API entry point, sandboxed .NET interop via `RegisterType<T>()`/`SetValue()`

**Deferred features** tracked in `src/SuperRender.EcmaScript/es-2025-todos.md`: BigInt, WeakRef, SharedArrayBuffer, Intl, Temporal, decorators (24 items remaining — generators and async/await are now implemented).

**Deferred CSS features** tracked in `src/SuperRender.Core/css-todos.md`: selectors level 4, flexbox, grid, custom properties, calc(), transforms, transitions, animations, media queries, container queries, CSS nesting, and more (34 sections).

**Deferred HTML features** tracked in `src/SuperRender.Core/html-todos.md`: full WHATWG tokenizer states, tree construction algorithm, adoption agency, forms, tables, embedded content, events, Shadow DOM, MutationObserver, user-agent stylesheet, and more (10 sections).

**Deferred browser features** tracked in `src/SuperRender.Browser/browser-todos.md`: navigation history, keyboard shortcuts, content scrolling, page zoom, cookies, HTTP caching, security hardening, DOM events, timers, images, form elements, find-in-page, dev tools, bookmarks, downloads, link navigation, fetch API, and more (25 sections + experimental ideas).

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
- **Fonts**: Dynamic font atlas with on-demand glyph rendering via FreeType (`FreeTypeSharp`). System font discovery by family name via `SystemFontLocator`. Platform-specific generic family defaults (serif, sans-serif, monospace, cursive, fantasy, system-ui) via `GenericFontFamilies`. CJK fallback fonts (PingFang SC on macOS, Microsoft YaHei on Windows, Noto Sans CJK on Linux). CSS font-family comma-separated list support with `FontFamilyParser`.

## Development Guidelines

- Core library must remain dependency-free (pure C#)
- EcmaScript engine must remain dependency-free (pure C#, DLR ships with .NET)
- EcmaScript Console has no dependencies beyond the EcmaScript engine
- All Vulkan/GPU code stays in the Gpu project (shared by Demo and Browser)
- `unsafe` is enabled globally (required by Silk.NET Vulkan bindings)
- Tests use `MonospaceTextMeasurer` (deterministic, no GPU needed)
- EcmaScript engine defaults to strict mode; no sloppy-mode support
- .NET interop is sandboxed: only explicitly mounted types are accessible from JS

### GPU-First Rendering

Prefer GPU rendering and compute over CPU wherever feasible:
- **Use Vulkan graphics pipelines** for all visual output (rects, text, effects). Never rasterize pixels on the CPU when a shader can do it.
- **Use Vulkan compute shaders** for parallelizable work: image decoding, glyph rasterization, layout pre-computation, hit-testing acceleration, or any batch data transformation that maps well to GPU threads.
- **Minimize per-frame CPU→GPU data transfer.** Use persistent mapped buffers (ring-buffered for double-buffering) instead of per-frame alloc/free. Upload only dirty regions of textures, not the entire atlas.
- **Let the GPU do index math.** Use `vertexOffset` in `vkCmdDrawIndexed` rather than adjusting index values on the CPU.
- **Enable hardware blending** on all pipelines that may carry alpha (quads included). Do not rely on draw-order opacity hacks.
- **Batch aggressively.** Merge draw calls where possible; minimize pipeline switches and scissor-rect flushes per frame.
- When adding a new rendering feature (gradients, rounded corners, shadows, filters, transforms), implement it as a GPU shader pipeline — not as CPU-generated quads or pixel manipulation.

## Browser

A Vulkan-powered browser application with tabbed browsing support.

**Key components:**
- `BrowserWindow` — main orchestrator: owns renderer, tab manager, chrome, input handler; drains timer queue and main-thread queue each frame
- `BrowserChrome` — renders tab bar (32px) + address bar (36px) as PaintCommands, vertically centered text via `CenterTextY`, font-metrics-accurate cursor positioning
- `TabManager` — tab lifecycle: create, close, switch tabs
- `Tab` — individual browsing context: owns Document, RenderPipeline, JsEngine, DomBridge, TextSelectionState, ScrollState, NavigationHistory, TimerScheduler
- `InputHandler` — routes keyboard/mouse to chrome or content area, handles text selection drag, address bar click-to-cursor, right-click context menus, link click navigation, DOM event dispatch, keyboard shortcuts (Cmd/Ctrl+T/W/Tab/L/R, F5, F12, Cmd+Shift+I, Escape, scroll keys)
- `ScrollState` — per-tab vertical scroll position tracking with mouse wheel and keyboard scrolling, scrollbar geometry computation
- `NavigationHistory` — per-tab URI history stack with back/forward cursor
- `ContextMenu` — reusable context menu: items with hover highlighting, hit-testing, PaintList rendering
- `ClipboardHelper` — cross-platform clipboard access (pbcopy/pbpaste on macOS, xclip on Linux, PowerShell on Windows)
- `ResourceLoader` — HTTP client for fetching HTML/CSS/JS resources, file:// URI support for local files, sr:// for embedded test pages
- `SecurityPolicy` — same-origin checks + CORS header validation for sub-resources
- `UrlResolver` — resolves relative URLs against base URI, normalizes address bar input (supports http/https/file/sr/about schemes, bare domain names, absolute file paths)
- `TestPages` — provides access to embedded manual test page resources via `sr://test/{name}` protocol

**HiDPI support:** Content scale derived from `FramebufferSize / Size`. Layout engine works in logical (CSS) pixels; projection matrix maps logical → physical coordinates. Font atlas is generated at `BaseFontSize * contentScale` for sharp text on Retina/HiDPI displays.

**Text selection:** Click-and-drag in the content area creates a text selection. `TextHitTester` maps mouse coordinates to `(runIndex, charOffset)` positions with font-aware measurement and overflow:hidden clip exclusion. `SelectionPainter` generates blue highlight rectangles behind selected text. `TextSelectionState` tracks start/end positions with ordered normalization. Address bar supports click-and-drag text selection.

**Context menus:** Right-click the address bar for Cut/Copy/Paste/Select All. Right-click the content area for Copy (when text selected)/Select All/View Source/Developer Tools.

**Content scrolling:** `ScrollState` per tab tracks `scrollY` with bounds clamping. Mouse wheel (via `mouse.Scroll` Silk.NET event), keyboard arrows/PageUp/PageDown/Home/End/Space. Scroll offset applied to content paint commands in `OnRender`. Visual scrollbar indicator (track + thumb) rendered on the right edge. Scroll-to-top on navigation.

**Link navigation:** `LayoutBoxHitTester.HitTest()` finds the deepest layout box at a coordinate. `FindAnchorAncestor()` walks up the DOM tree to find `<a>` elements. Click (mouseup within 5px of mousedown) on a link extracts `href`, resolves against current URI, navigates. `target="_blank"` opens in a new tab.

**Back/Forward history:** `NavigationHistory` per tab stores a list of URIs with a cursor index. `Tab.NavigateAsync()` pushes to history; `GoBackAsync()`/`GoForwardAsync()` move the cursor without pushing. Back/Forward chrome buttons call these methods. Address bar and window title update after history navigation.

**Keyboard shortcuts:** Platform-aware (Cmd on macOS, Ctrl on Windows/Linux). Global: Cmd+T (new tab), Cmd+W (close), Cmd+Tab/Shift+Tab (switch), Cmd+L (focus+select address bar), Cmd+R/F5 (reload), Cmd+[/Cmd+Left (back), Cmd+]/Cmd+Right (forward), F12/Cmd+Shift+I (toggle DevTools), Escape (unfocus). Content area: arrow keys (scroll step), Page Up/Down/Space (scroll page), Home/End (top/bottom).

**DOM events:** `Node` has `AddEventListener`/`RemoveEventListener`/`DispatchEvent` with capture/target/bubble propagation. `DomEvent`, `MouseEvent`, `KeyboardEvent` in Core. `JsEventWrapper` bridges to JS. `InputHandler` dispatches `mousedown`/`mouseup`/`click` to hit DOM nodes. `DOMContentLoaded` and `load` fired after page load.

**Timers:** `TimerScheduler` (in EcmaScript.Dom) uses `Stopwatch` for monotonic time. `setTimeout` with real delay, `setInterval` (min 4ms), `requestAnimationFrame` (fires next frame). Timer queue drained in `BrowserWindow.OnRender` before painting. `cancelAnimationFrame`/`clearTimeout`/`clearInterval` cancel by ID.

**Developer Tools:** Separate Vulkan window opened via F12/Cmd+Shift+I/right-click context menu. `DevToolsWindow` creates its own `IWindow` + `VulkanRenderer`; rendering is driven from the main window's render loop (Silk.NET only drives `Run()` on one window, so secondary windows are rendered manually via `RenderFrame()`). GLFW's `glfwPollEvents()` handles input events for all windows. `DevToolsPanel` builds PaintList for the console UI: toolbar, scrollable log area, JS input line. `ConsoleCapture` (TextWriter subclass) redirects `console.log/warn/error` to per-tab `ConsoleLog`. JS execution requests are queued via `ConcurrentQueue<string>` and drained on the main thread. `Tab.LoadHtmlDirect(html)` replaces the old reflection-based page loading and also sets up the JsEngine. `Tab.ExecuteConsoleInput(code)` lazy-initializes the JsEngine if needed.

## EcmaScript DOM Bindings

Bridges C# DOM objects to the JS runtime with correct Web API naming conventions.

**Key components:**
- `DomBridge` — entry point: installs `document` and `window` globals into JsEngine, owns `TimerScheduler`, forwards timer/event APIs (setTimeout, setInterval, requestAnimationFrame, etc.) to global scope
- `JsNodeWrapper` — wraps Node: nodeType, parentNode, childNodes, appendChild, removeChild, insertBefore, addEventListener, removeEventListener, dispatchEvent
- `JsElementWrapper` — wraps Element: tagName, id, className, classList, getAttribute/setAttribute, querySelector/querySelectorAll, innerHTML, style
- `JsDocumentWrapper` — wraps Document: createElement, createTextNode, getElementById, getElementsByTagName/ClassName, body, head, title
- `JsWindowWrapper` — window global: document, innerWidth/Height, devicePixelRatio, setTimeout/clearTimeout (real delays via TimerScheduler), setInterval/clearInterval, requestAnimationFrame/cancelAnimationFrame
- `JsEventWrapper` — wraps DomEvent/MouseEvent/KeyboardEvent for JS: type, target, currentTarget, preventDefault, stopPropagation, clientX/Y, key/code
- `JsCssStyleDeclaration` — element.style accessor: camelCase property get/set → inline style attribute
- `TimerScheduler` — monotonic timer queue: setTimeout/setInterval/requestAnimationFrame with real delays, drained per frame
- `NodeWrapperCache` — ConditionalWeakTable for 1:1 C# node ↔ JS wrapper identity, event handler mapping for removeEventListener

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
