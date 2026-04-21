# SuperRenderer

HTML+CSS rendering engine in C# (.NET 10), Silk.NET + Vulkan backend. Includes DLR-based ECMAScript 2025 engine.

## Project Structure

- `src/Document/SuperRender.Document/` — DOM (Node, Element, Document), HTML/CSS parsers, Stylesheet, Selector, Color, events, DomMutationApi. Zero deps.
- `src/Renderer/`
  - `SuperRender.Renderer.Rendering/` — Style resolution, layout (block/inline/flex/grid/table/float), painting, `RenderPipeline` orchestrator.
  - `SuperRender.Renderer.Gpu/` — Shared Vulkan infrastructure: context, pipelines, font atlas, batch renderers.
  - `SuperRender.Renderer.Image/` — Pure C# decoders: PNG, BMP, baseline JPEG. Zero deps.
- `src/EcmaScript/`
  - `SuperRender.EcmaScript.Compiler/` — Lexer, Parser, AST, `JsCompiler` (DLR expression trees).
  - `SuperRender.EcmaScript.Runtime/` — `JsValue` types, Environment, Realm, Builtins (32 stdlib objects), Errors. `Interop/` contains `IJsType` marker, `[JsName]`, `JsValueExtension.AsInterface<T>`, `JsTypeInterfaceProxyRegistry`, `DispatchProxy` fallback.
  - `SuperRender.EcmaScript.Engine/` — `JsEngine` public API, .NET interop (TypeProxy, ObjectProxy).
  - `SuperRender.EcmaScript.Repl/` — Node.js-style REPL.
  - `SuperRender.EcmaScript.Dom/` — JS DOM bindings: document/element/window/fetch/location/history.
  - `SuperRender.EcmaScript.NodeSimulator/` — Node.js-compatible runtime. P0: `process`, `Buffer`, `console`, timers, `path`, `os`, `util`, `events`, `assert`, `fs` (+`fs/promises`, `fs.watch`). P1: `querystring`, `url`, `string_decoder`, `crypto`, `zlib`, `stream`. `ref/types-node/` pins `@types/node@25.6.0`. Plan in `docs/node-api-todos.md`.
- `src/Analyzer/SuperRender.Analyzer/` — Roslyn source generator (netstandard2.0). Emits `[JsObject]`/`[JsMethod]`/`[JsProperty]` attrs. For each `[JsObject] partial class : JsObject` (or `: JsDynamicObject` when the class also needs dynamic properties), generates switch-based `Get`/`HasProperty` (+optional `Set`) with lazy `JsFunction` caching; the default `switch` branch falls through to `base.Get`/etc so inheritance chains (e.g. `JsDocumentWrapper : JsElementWrapper : JsNodeWrapper`) compose naturally. Trampolines support **legacy** `(JsValue thisArg, JsValue[] args) => JsValue-derived` or **typed** signatures — params: `JsValue`/`JsObject`/`IJsType`-derived interfaces/C# primitives/`JsValue[]` rest. `JsObject` params throw `JsTypeError` pre-call; primitives coerce via `ToNumber`/`ToBoolean`/`ToJsString`; `IJsType` params are wrapped with `AsInterface<T>()`. Return may be `void`/JsValue-derived/primitive/`IJsType`. Diagnostics: JSGEN001 (bad param), JSGEN002 (bad return), JSGEN003 (bad interface member), JSGEN004 (info: member skipped from generated interface). `function.length` auto-derived — do NOT set `Length =`. Second generator `JsTypeInterfaceProxyGenerator` emits `__<Name>Proxy` (leading `I` stripped) for `IJsType` interfaces, self-registers via `[ModuleInitializer]`. Default JS name = camelCase; override with `[JsName]`. Referenced as `OutputItemType="Analyzer"` by Runtime/Engine/Dom/NodeSimulator/EcmaScript.Tests.
- `src/Browser/SuperRender.Browser/` — Browser app: tabs, address bar, networking, cookies, SQLite storage, HTTP caching, CORS, HiDPI, images.
- `src/Demo/SuperRender.Demo/` — Minimal Vulkan demo.
- `tests/` — xUnit. Document (546), Renderer (543), Renderer.Image (25), EcmaScript (668), NodeSimulator (124), Browser (265). Total 2171.

## Build & Run

```bash
dotnet build
dotnet test
dotnet run --project src/Demo/SuperRender.Demo
dotnet run --project src/Browser/SuperRender.Browser
dotnet run --project src/EcmaScript/SuperRender.EcmaScript.Repl
```

## Architecture

**Pipeline:** HTML → Parse → DOM → Style resolution (cascade/specificity/inheritance/custom props/at-rules) → Layout → Paint → Vulkan GPU.

**Rendering key components:**
- `RenderPipeline` — dirty-flag orchestrator; owns `TransitionController` and `AnimationController` which run after style resolution, overwrite fields before layout, re-render every frame while active.
- `TransitionController` — diffs snapshots, interpolates per `transition-property`/`-duration`/`-delay`/`-timing-function`. Colors, lengths, opacity, transform lists.
- `AnimationController` — reads `@keyframes`, evaluates `animation-*`, interpolates between keyframes each frame.
- `HtmlParser` — state-machine tokenizer + tree builder (auto-close, adoption agency). Tokenizer split: `HtmlTokenizer.cs` + `HtmlTokenizer.States.cs` (16 states).
- `CssParser` — tokenizer+parser, shorthand expansion, at-rules (`@import`/`@media`/`@supports`/`@layer`/`@scope`/`@font-face`/`@keyframes`/`@namespace`), CSS nesting, gradients, box-shadow. Split: `CssParser.cs` + `CssParser.Shorthands.cs`.
- `StyleResolver` — cascade, specificity, `!important`, global keywords, custom properties + `var()` with cycle detection, `@media`/`@supports`/`@layer`, inherited props, `calc()`/`min()`/`max()`/`clamp()` + 17 math funcs, all unit types (px/em/rem/%, absolute, font-relative, viewport, angles, time). CSS counters (`counter-reset`/`-increment` + `counter()`/`counters()` in `content:`). Text nodes get propagated text-decoration/text-shadow/vertical-align from parent. Split into partials by concern (BoxModel, Typography, Color, Position, Flex, Grid, Transform, Animation, Background, Visual, Filter, Helpers).
- `LayoutEngine` — block, inline with word-wrap, anonymous wrapping, inline-block, flex (`FlexLayout` phased: collect/wrap/size/position/align-content), grid (`GridLayout`: templates, `fr`, `repeat()`, auto-placement), table (`TableLayout` fixed/auto, border-collapse), float (`FloatLayout`), position static/relative/absolute/fixed/sticky, white-space, `<img>` intrinsic sizing, `aspect-ratio`, margin collapsing. Inline: mixed-size runs aligned to largest ascent, per-run `vertical-align`. `BoxDimensions` edge helpers. `ImageIntrinsicSizeHelper` shared.
- `Painter` — emits FillRect/DrawText/DrawImage/PushClip/PopClip/DrawGradient/DrawBoxShadow/DrawOutline/PushTransform/PopTransform/PushFilter/PopFilter. Inline backgrounds, text-decoration, z-index, overflow clip, list markers, opacity, visibility, alt text, `transform-origin` pivot wrap, 9-sample text-shadow kernel.
- `SelectorMatcher` — CSS Selectors L4: combinators, attribute selectors `[i]`/`[s]`, structural/functional/dynamic/form/linguistic pseudo-classes (`:has()` supported), pseudo-elements, `:root`/`:empty`/`:defined`/`:scope`/`:target`.
- `SelectionPainter`, `TextHitTester` (respects overflow clip), `LayoutBoxHitTester` (walks to `<a>`), `TextSelectionState`.
- `VulkanRenderer` — quad/text/gradient/shadow pipelines. Segment-based draw lists with per-segment 2D transform matrix (push-constant = `segment.Transform * projection`); CSS transforms applied GPU-side, no CPU vertex rewrites.
- `DomMutationApi`, `DomEvent`/`MouseEvent`/`KeyboardEvent` (capture/target/bubble), `EventListener`, `UserAgentStylesheet`, `InteractionStateHelper` (:hover/:focus/:active + mouseenter/leave).

## Image Decoding

Pure C# in `SuperRender.Renderer.Image`. `PngDecoder` (all color types/bit depths/filters, PLTE/tRNS, DEFLATE), `BmpDecoder` (24/32-bit), `JpegDecoder` (baseline SOF0, Huffman, IDCT, YCbCr→RGB, 4:4:4/4:2:0). `ImageDecoder.Decode(byte[])` → `ImageData {Width,Height,Pixels}` RGBA row-major. Hot paths use `Span`/`unsafe fixed`. PNG uses `ArrayPool<byte>`. JPEG has optional GPU callbacks `GpuYCbCrConverter` and `GpuDequantIdctTransformer`. `Tab.LoadImagesAsync()` fetches → decodes → `ImageCache` → sets `data-natural-width`/`-height`.

## CSS Feature Coverage

- **Color:** hex, `rgb()`/`rgba()`, `hsl()`/`hsla()`, `hwb()`, `lab()`/`lch()`, `oklab()`/`oklch()`, `color()`, `color-mix()`, `light-dark()`, `currentcolor`, 148 named, 19 system.
- **Custom props:** `--*`, `var()` fallback, cycle detection. `CustomPropertyResolver` runs before property application.
- **At-rules:** `@import`, `@media` (`MediaQuery`), `@supports` (`SupportsCondition`), `@layer`, `@scope`, `@font-face` parsed, `@keyframes` parsed, `@namespace`. Nesting with `&`.
- **Selectors:** 26 pseudo-classes, 9 pseudo-element types.
- **Units:** px/em/rem/pt/%, absolute (cm/mm/in/pc/Q), font-relative (ex/ch/lh/rlh/cap/ic), viewport (vw/vh/vmin/vmax + d/s/l variants), angle, time, resolution. Math: `calc`/`min`/`max`/`clamp`/`abs`/`sign`/`round`/`mod`/`rem`/trig/`pow`/`sqrt`/`hypot`/`log`/`exp`.
- **Layout:** block, inline, inline-block, flex + align-content, grid, table, float/clear, `display:contents`/`list-item`/`inline-flex`, position fixed/sticky, logical props, aspect-ratio, margin collapse.
- **Visual effects:** gradients (linear/radial/conic + repeating) GPU-rendered, box-shadow (GPU SDF blur), outline, 18 transform functions, transitions, animations, filters, backdrop-filter, mix-blend-mode, clip-path.

**Gaps** (`docs/css-todos.md`): subgrid, masonry, full `@font-face` loading, variable fonts, container query units, full writing-mode layout, scroll-driven animations. Test pages at `Resources/TestPages/CSS/`; see `docs/MANUAL_TESTS_CSS.md`.

## EcmaScript Engine

**Pipeline:** Source → Lexer → Parser (Pratt, 20 precedence levels, ASI, arrows, destructuring, modules) → `JsCompiler` (DLR Expression trees, `RuntimeHelpers`, labeled break/continue) → delegate → execute. Split: `JsCompiler.{Statements,Expressions,Classes,Functions,Helpers}.cs`.

**JsValue hierarchy:** `IDynamicMetaObjectProvider` base. `JsObject` (abstract, prototype-chain `Get`/`Set`/`HasProperty`/`Delete`/`ToPrimitive` driven by virtual `GetOwnPropertyDescriptor`/`DefineOwnPropertyCore`; no own storage). `JsDynamicObject` (dictionary + symbols). All attribute-driven built-ins, DOM wrappers, and NodeSim modules inherit `JsObject` (pure namespace) or `JsDynamicObject` (when the constructor needs `DefineOwnProperty` for dynamic/sub-object content) and have their property tables emitted by the source generator. `JsCompiler.GetMember`/`SetMember`/`DeleteMember` type-check on `JsObject`.

**Environment** (lexical, TDZ, const), **Realm** (globals + intrinsic prototypes + Eval/FunctionFactory delegates), **Builtins** (32 objects incl. Temporal, Intl, ShadowRealm, TypedArrays, Atomics, SharedArrayBuffer, Proxy, Reflect, WeakRef, FinalizationRegistry). Namespace-type builtins (`Math`, `JSON`, `Atomics`, `Reflect`, `Intl`) and most NodeSim modules are generator-driven; constructor/callable builtins (`Array`, `Promise`, `Buffer`, `StringDecoder`, `EventEmitter`, `Assert`, stream classes, `process`) still use `BuiltinHelper.DefineMethod`/`DefineProperty` + `JsFunction.CallTarget` since SG targets `JsObject`, not `JsFunction`.

**`GeneratorCoroutine`** (thread-based for generator/async), **`JsGeneratorObject`**, **`JsEngine`** (sandboxed interop via `RegisterType<T>()`/`SetValue()`).

**Deferred** (`docs/es-2025-todos.md`): TCO, Decorators, Pattern Matching, Module Workers. Most ES2025 features implemented. Test pages at `Resources/TestPages/JS/`; see `docs/MANUAL_TESTS_JS.md`.

**`IJsType` structural binding:** `value.AsInterface<T>()` where `T : IJsType`. Flow: (1) non-`JsObject` → `JsTypeError`; (2) if backing object already implements `T`, returned as-is; (3) else use generator-emitted `__<Name>Proxy` registered via `[ModuleInitializer]`; (4) `DispatchProxy` fallback (not AOT-safe). Cached via `ConditionalWeakTable<(object, Type), proxy>`. Allowed members: `JsValue`-derived, C# primitives, other `IJsType` (recursive), `JsValue[]` rest. Unsupported (events, indexers, generics, ref/out) → JSGEN003. Default name = camelCase, override via `[JsName]`.

## EcmaScript Console

- `Program.cs` — `--eval <code>`, `<file.js>`, or REPL.
- `Repl` — multiline detection, dot commands (`.help`/`.exit`/`.clear`/`.editor`), object-literal-vs-block disambiguation.
- `LineEditor` — arrow keys, history, Ctrl+Left/Right word nav, Ctrl+U/K/W.
- `ValueInspector` — Node-style ANSI-colored formatting.

## Browser

- `BrowserWindow` — orchestrator; drains timer + main-thread queues per frame.
- `BrowserChrome` — tab bar (32px) + address bar (36px) as PaintCommands.
- `TabManager`, `Tab` (owns Document, RenderPipeline, JsEngine, DomBridge, TextSelectionState, ScrollState, NavigationHistory, TimerScheduler, SessionStorage).
- `InputHandler` — routes kb/mouse, text selection drag, click-to-cursor, right-click menus, link nav, DOM events, `:hover`/`:focus`/`:active` tracking, platform-aware shortcuts.
- `ScrollState` (wheel + keys + scrollbar), `NavigationHistory` (back/forward cursor), `ContextMenu`, `ClipboardHelper` (pbcopy/xclip/PowerShell), `ResourceLoader` (http/file/sr/data), `SecurityPolicy` (same-origin + CORS), `UrlResolver`, `TestPages` (`sr://test/{P0|P1|JS|CSS}/{name}`), `ImageCache`, `CookieJar` (full Set-Cookie), `HttpCache` (SQLite + Cache-Control/ETag/Last-Modified/304).

**HiDPI:** ContentScale = framebuffer/size. Layout in logical px; projection maps logical → physical. Font atlas at `BaseFontSize * contentScale`.

**DOM events:** capture/target/bubble via `Node.AddEventListener`/`DispatchEvent`. `InputHandler` fires mouse* events to hit nodes. `DOMContentLoaded` + `load` after page load.

**Timers:** `TimerScheduler` (in EcmaScript.Dom) uses `Stopwatch`. setTimeout, setInterval (min 4ms), requestAnimationFrame. Queue drained in `BrowserWindow.OnRender` pre-paint.

**DevTools:** Per-tab separate Vulkan window (F12/Cmd+Shift+I). `DevToolsWindow` owns its `IWindow`+`VulkanRenderer`; main window drives its `RenderFrame()` each tick (Silk.NET only drives `Run()` on one window). GLFW `glfwPollEvents()` covers all windows. `DevToolsPanel` = toolbar + log + JS input. `ConsoleCapture` (TextWriter) redirects console.* into per-tab `ConsoleLog`. JS execution queued via `ConcurrentQueue<string>`, drained main-thread. `Tab.LoadHtmlDirect(html)` sets up JsEngine; `Tab.ExecuteConsoleInput(code)` lazy-inits.

**Storage:** `CookieJar` in-memory (full parsing). `document.cookie` via `JsDocumentWrapper`. `localStorage` SQLite-backed (`StorageDatabase`); `sessionStorage` per-tab. `JsStorageWrapper` bridges.

**Fetch:** `JsFetchApi` global `fetch()` → Promise. `Task.Run` + main-thread marshaling. `JsResponseWrapper`: status/ok/headers/.text()/.json().

**Location/History:** `JsLocationWrapper` (href/protocol/host/... + assign/replace/reload). `JsHistoryWrapper` (pushState/replaceState/back/forward/go/length/state).

**Deferred:** browser features (`docs/browser-todos.md`), HTML (`docs/html-todos.md`), DOM bindings (`docs/es-2025-dom-todos.md`).

## EcmaScript DOM Bindings

Bridges C# DOM to JS with Web API naming.

- `DomBridge` — installs `document`/`window`/`fetch`; owns `TimerScheduler`; wires cookie/storage/location/history.
- `JsNodeWrapper` (nodeType/parent/children/append/remove/insert/replace/clone/contains/textContent/event API).
- `JsElementWrapper` (tag/id/class/classList/attr API/querySelector(All)/innerHTML/style/matches/closest/dataset/traversal/after/before/remove).
- `JsDocumentWrapper`, `JsWindowWrapper` (innerWidth/Height, devicePixelRatio, timers, storage, location, history).
- `JsEventWrapper`, `JsCssStyleDeclaration` (camelCase → inline style attr), `JsFetchApi`, `JsLocationWrapper`, `JsHistoryWrapper`, `JsStorageWrapper`, `TimerScheduler`, `NodeWrapperCache` (`ConditionalWeakTable` for 1:1 node↔wrapper + event handler map for removal), `JsWrapperExtensions` (`DefineMethod`/`DefineGetter`/`DefineGetterSetter`).

## GPU Rendering

Shared Vulkan library.

- `VulkanContext` (instance/device/queues + MoltenVK on macOS).
- `SwapchainManager`, `PipelineManager` (quad + text + gradient + shadow, all alpha-blended).
- `BufferManager` (persistent-mapped ring buffers, staging helpers, partial atlas updates).
- `VulkanRenderer` — segment-based, per-segment scissor + 2D transform push-constant, GPU-side `vertexOffset`, HiDPI, partial atlas re-upload. `BrowserWindow` uses two-pass content rendering (quads → selection → text) for z-order; pass 2 preserves `PushTransform`/`PushFilter` alongside clips. Scroll offsets re-anchor transform pivots via `T(0,scrollY)·M·T(0,-scrollY)` (no vertex rewrites).
- `FontAtlas` (2048x4096, BaseFontSize=32, HiDPI-scaled, dirty-region uploads; regular/bold/monospace + CJK fallback; ASCII pre-rendered, rest on demand via FreeType).
- `FontAtlasGenerator`, `SystemFontLocator` (scans, FreeType-read names, .ttf/.otf/.ttc multi-face), `GenericFontFamilies` (serif/sans/mono/cursive/fantasy/system-ui per platform + CJK fallback lists).
- `QuadRenderer`/`TextRenderer` (PaintList → GPU vertex batches; font variant selection).
- `BitmapFontTextMeasurer` — ITextMeasurer from font atlas metrics.

### GPU Compute

Prefer GPU for all rendering/parallelizable work. Minimize per-frame transfers (persistent ring buffers, dirty-region uploads). Let GPU do index math (`vertexOffset`). Enable HW blending everywhere. Batch aggressively.

- `ComputePipelineManager` — SSBO + push-constants.
- `IdctCompute` — JPEG 8x8 IDCT GPU dispatch; standard (`TransformBlocks`) + combined dequant+IDCT (`TransformBlocksWithDequant`); graceful CPU fallback.
- `DequantIdctPipeline` (3 SSBOs: coeffs/quant/out), `YCbCrComputePipeline` (4 SSBOs), `YCbCrCompute` (uploads Y/Cb/Cr planes → RGBA; `JpegDecoder.GpuYCbCrConverter` callback keeps Renderer.Image dep-free).
- Shaders: `idct.comp.glsl` (64 threads/block), `idct_dequant.comp.glsl` (64 threads + shared-memory), `ycbcr_to_rgba.comp.glsl` (256 threads/pixel), `gradient.vert/frag.glsl` (SDF clip, multi-stop), `shadow.frag.glsl` (SDF Gaussian approx).
- `ShaderCompiler` supports `"comp"` stage via `LoadOrCompileComputeShader()`.
- `GradientRenderer` — vertex data for gradients, box shadows, 4-rect outline strokes.

## Platform Notes

- **macOS:** MoltenVK bundled via `Silk.NET.MoltenVK.Native`. `VulkanContext.EnsureMoltenVK()` symlinks dylib + sets `VK_DRIVER_FILES` pre-window.
- **Shaders:** GLSL in `Shaders/`, compiled to SPIR-V at runtime via shaderc; pre-compiled `.spv` may live in `Resources/Shaders/`.
- **Fonts:** FreeType via `FreeTypeSharp`. `SystemFontLocator`, `GenericFontFamilies`, `FontFamilyParser`. CJK fallback: PingFang SC (mac), Microsoft YaHei (win), Noto Sans CJK (linux).

## Code Quality

- `TreatWarningsAsErrors` globally — zero-warning build.
- `AnalysisLevel=latest-Recommended` + `EnforceCodeStyleInBuild`.
- Rule suppressions go in `.editorconfig`, not `.csproj <NoWarn>`.
- CA1707 suppressed under `tests/` only (xUnit `Method_Scenario_Expected`).
- CA1720 suppressed under `src/Renderer/.../Style/` (CSS-mandated names).
- CA1848 + CA1873 suppressed globally (log volume doesn't warrant).

## Development Guidelines

- **Dependency-free projects:** Document, Renderer.Image, EcmaScript.{Compiler,Runtime,Engine}. EcmaScript.Repl depends only on Engine.
- **Analyzer usage:** `SuperRender.Analyzer` (netstandard2.0) referenced as `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`. Consumers must be `partial` + inherit `JsObject` (or `JsDynamicObject` when the class also needs dynamic/sub-object props written via `DefineOwnProperty`). `[JsMethod]` supports legacy `(JsValue, JsValue[]) => JsValue-derived` (good for variadics and optional args) or typed signatures (`JsValue` / `JsObject` / `IJsType`-derived interfaces / C# primitives / `JsValue[]` rest). `IJsType` params are auto-unwrapped via `AsInterface<T>()`; `JsObject` params pre-check; primitives coerce. `function.length` auto-derived — don't pass `Length =`. Symbol-keyed members not yet generated; override `TryGetOwnSymbol` manually. Callable/constructor-style intrinsics (`Array`, `Promise`, `Buffer`, `EventEmitter`, `StringDecoder`, `assert()`, stream classes) stay on manual `JsFunction.CallTarget` + `BuiltinHelper.DefineMethod` — SG targets `JsObject`, not `JsFunction`.
- All Vulkan/GPU code lives in Renderer.Gpu (shared by Demo + Browser).
- `unsafe` enabled globally (Silk.NET requirement).
- Tests use `MonospaceTextMeasurer` (deterministic, GPU-free).
- EcmaScript defaults to strict mode; no sloppy mode.
- .NET interop is sandboxed — only explicitly mounted types are reachable from JS.
- Files under `SuperRender.Document.*` or `SuperRender.Renderer.*` need `using DomDocument = ...` alias due to namespace collision.
- Embedded resource names derived via reflection (`typeof(T).Assembly.GetName().Name`) — never hardcode.
- Centralized string constants: `CssPropertyNames`, `HtmlTagNames`, `HtmlAttributeNames` (plus common values `Stylesheet`/`TargetBlank`), `PropertyDefaults` (font size, viewport dims).
- DOM wrapper boilerplate: use `JsWrapperExtensions.DefineMethod`/`DefineGetter`/`DefineGetterSetter` over raw `DefineOwnProperty`.
- Logging: Browser + Gpu use `Microsoft.Extensions.Logging` (ILogger); zero-dep projects use `Console.WriteLine`.
- Inline HTML pages (welcome, error) are embedded resources in `Resources/`.
- DevTools is per-tab; closing a tab closes its DevTools.
- JS errors surface line/column from `JsErrorBase`. `RuntimeHelpers.SetLocation()` emits at statement boundaries; `ExecutionContext.CurrentLine`/`CurrentColumn` (thread-static) carry runtime location.
- Large classes split via `partial class` by concern (`JsCompiler.Statements.cs`, `StyleResolver.BoxModel.cs`). Replicate `using` + `#pragma` across partials.
- Browser tests use `TestEnvironmentHelper.Create(html)` for shared JsEngine/Document/DomBridge setup.
