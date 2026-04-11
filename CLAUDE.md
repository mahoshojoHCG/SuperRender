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
- `tests/SuperRender.Tests/` — xUnit tests for Core (98 tests)
- `tests/SuperRender.EcmaScript.Tests/` — xUnit tests for EcmaScript (421 tests)

## Build & Run

```bash
dotnet build              # Build all projects (warnings are errors)
dotnet test               # Run all unit tests (519 total)
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
- `StyleResolver` — cascade, specificity, `!important`, inherited properties (color, font-size, font-family, font-weight, font-style, text-align, line-height)
- `LayoutEngine` — block layout, inline layout with word-wrap, anonymous block wrapping
- `Painter` — generates FillRect/DrawText commands from layout tree, text-decoration rendering (underline, line-through, overline)
- `SelectionPainter` — generates highlight FillRect commands for text selection ranges
- `TextHitTester` — hit-tests mouse coordinates against laid-out TextRuns to find character positions
- `LayoutBoxHitTester` — hit-tests layout boxes by coordinate to find clicked DOM elements, walks to `<a>` ancestors for link navigation
- `TextSelectionState` — tracks selection start/end as `TextPosition(RunIndex, CharOffset)`
- `VulkanRenderer` — frame loop with quad pipeline (backgrounds/borders) + text pipeline (font atlas with alpha blending), HiDPI content scale support
- `DomMutationApi` — runtime DOM modification with automatic re-layout
- `DomEvent` / `MouseEvent` / `KeyboardEvent` — DOM event classes with capture/target/bubble propagation
- `EventListener` — registered event handler on a DOM node (type, handler, capture flag)
- `UserAgentStylesheet` — default browser CSS styles (body margin, heading sizes/bold, list indent, text-level semantics: bold, italic, underline, strikethrough, monospace, link styling)

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
- `BrowserWindow` — main orchestrator: owns renderer, tab manager, chrome, input handler; drains timer queue and main-thread queue each frame
- `BrowserChrome` — renders tab bar (32px) + address bar (36px) as PaintCommands, vertically centered text via `CenterTextY`, font-metrics-accurate cursor positioning
- `TabManager` — tab lifecycle: create, close, switch tabs
- `Tab` — individual browsing context: owns Document, RenderPipeline, JsEngine, DomBridge, TextSelectionState, ScrollState, NavigationHistory, TimerScheduler
- `InputHandler` — routes keyboard/mouse to chrome or content area, handles text selection drag, address bar click-to-cursor, right-click context menus, link click navigation, DOM event dispatch, keyboard shortcuts (Cmd/Ctrl+T/W/Tab/L/R, F5, Escape, scroll keys)
- `ScrollState` — per-tab vertical scroll position tracking with mouse wheel and keyboard scrolling, scrollbar geometry computation
- `NavigationHistory` — per-tab URI history stack with back/forward cursor
- `ContextMenu` — reusable context menu: items with hover highlighting, hit-testing, PaintList rendering
- `ClipboardHelper` — cross-platform clipboard access (pbcopy/pbpaste on macOS, xclip on Linux, PowerShell on Windows)
- `ResourceLoader` — HTTP client for fetching HTML/CSS/JS resources
- `SecurityPolicy` — same-origin checks + CORS header validation for sub-resources
- `UrlResolver` — resolves relative URLs against base URI

**HiDPI support:** Content scale derived from `FramebufferSize / Size`. Layout engine works in logical (CSS) pixels; projection matrix maps logical → physical coordinates. Font atlas is generated at `BaseFontSize * contentScale` for sharp text on Retina/HiDPI displays.

**Text selection:** Click-and-drag in the content area creates a text selection. `TextHitTester` maps mouse coordinates to `(runIndex, charOffset)` positions. `SelectionPainter` generates blue highlight rectangles behind selected text. `TextSelectionState` tracks start/end positions with ordered normalization.

**Context menus:** Right-click the address bar for Cut/Copy/Paste/Select All. Right-click the content area for Copy (when text selected)/Select All/View Source.

**Content scrolling:** `ScrollState` per tab tracks `scrollY` with bounds clamping. Mouse wheel (via `mouse.Scroll` Silk.NET event), keyboard arrows/PageUp/PageDown/Home/End/Space. Scroll offset applied to content paint commands in `OnRender`. Visual scrollbar indicator (track + thumb) rendered on the right edge. Scroll-to-top on navigation.

**Link navigation:** `LayoutBoxHitTester.HitTest()` finds the deepest layout box at a coordinate. `FindAnchorAncestor()` walks up the DOM tree to find `<a>` elements. Click (mouseup within 5px of mousedown) on a link extracts `href`, resolves against current URI, navigates. `target="_blank"` opens in a new tab.

**Back/Forward history:** `NavigationHistory` per tab stores a list of URIs with a cursor index. `Tab.NavigateAsync()` pushes to history; `GoBackAsync()`/`GoForwardAsync()` move the cursor without pushing. Back/Forward chrome buttons call these methods. Address bar and window title update after history navigation.

**Keyboard shortcuts:** Platform-aware (Cmd on macOS, Ctrl on Windows/Linux). Global: Cmd+T (new tab), Cmd+W (close), Cmd+Tab/Shift+Tab (switch), Cmd+L (focus address bar), Cmd+R/F5 (reload), Escape (unfocus). Content area: arrow keys (scroll step), Page Up/Down/Space (scroll page), Home/End (top/bottom).

**DOM events:** `Node` has `AddEventListener`/`RemoveEventListener`/`DispatchEvent` with capture/target/bubble propagation. `DomEvent`, `MouseEvent`, `KeyboardEvent` in Core. `JsEventWrapper` bridges to JS. `InputHandler` dispatches `mousedown`/`mouseup`/`click` to hit DOM nodes. `DOMContentLoaded` and `load` fired after page load.

**Timers:** `TimerScheduler` (in EcmaScript.Dom) uses `Stopwatch` for monotonic time. `setTimeout` with real delay, `setInterval` (min 4ms), `requestAnimationFrame` (fires next frame). Timer queue drained in `BrowserWindow.OnRender` before painting. `cancelAnimationFrame`/`clearTimeout`/`clearInterval` cancel by ID.

## EcmaScript DOM Bindings

Bridges C# DOM objects to the JS runtime with correct Web API naming conventions.

**Key components:**
- `DomBridge` — entry point: installs `document` and `window` globals into JsEngine, owns `TimerScheduler`
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
- `PipelineManager` — quad pipeline (solid rects) + text pipeline (font atlas sampling)
- `BufferManager` — GPU buffer allocation, vertex/index upload, texture creation
- `VulkanRenderer` — frame loop orchestrator with HiDPI content scale support
- `FontAtlasGenerator` — FreeType-based font atlas (1024x1024, BaseFontSize=32, HiDPI-scaled via `AtlasRenderSize`)
- `QuadRenderer` / `TextRenderer` — PaintList → GPU vertex batch builders
- `BitmapFontTextMeasurer` — ITextMeasurer implementation using font atlas metrics
