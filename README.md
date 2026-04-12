# SuperRenderer

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A from-scratch HTML + CSS rendering engine, ECMAScript 2025 engine, and tabbed browser -- built entirely in C# on .NET 10. Renders to a native window via Vulkan (Silk.NET).

No WebView. No Chromium. No Gecko. Just parsers, a layout engine, a JS compiler, and a GPU.

## What It Does

```
HTML string
  -> Tokenizer -> DOM tree
  -> CSS parser -> Stylesheets
  -> Style resolution (cascade, specificity, inheritance)
  -> Layout (block + inline + flex box model)
  -> Paint commands (rects, text, images, clipping)
  -> Vulkan GPU rendering

JavaScript source
  -> Lexer -> Parser (AST)
  -> DLR Expression Trees -> Compiled delegates
  -> Execution with DOM bindings
```

The ECMAScript engine compiles JavaScript to .NET DLR expression trees and runs them natively -- no interpreter loop.

## Quick Start

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and a Vulkan-capable GPU (MoltenVK is bundled for macOS).

```bash
# Build
dotnet build

# Run all 1242 tests (295 Document + 216 Renderer + 25 Image + 441 EcmaScript + 265 Browser)
dotnet test

# Launch the browser (tabs, address bar, networking)
dotnet run --project src/Browser/SuperRender.Browser

# Launch the rendering demo window (1024x768, Vulkan)
dotnet run --project src/Demo/SuperRender.Demo

# Launch the interactive JavaScript console
dotnet run --project src/EcmaScript/SuperRender.EcmaScript.Repl
```

## Project Structure

```
SuperRenderer/
  src/
    Document/
      SuperRender.Document/             Core DOM, CSS, events (zero dependencies)
    Renderer/
      SuperRender.Renderer.Rendering/   Style resolution, layout engine, painting
      SuperRender.Renderer.Gpu/         Vulkan rendering, font atlas, batch renderers
      SuperRender.Renderer.Image/       Pure C# image decoders (PNG, BMP, JPEG)
    EcmaScript/
      SuperRender.EcmaScript.Runtime/   JS value types, environment, errors
      SuperRender.EcmaScript.Compiler/  Lexer, parser, DLR expression tree compiler
      SuperRender.EcmaScript.Engine/    Engine API, builtins, .NET interop
      SuperRender.EcmaScript.Dom/       JS DOM bindings (document, window, fetch)
      SuperRender.EcmaScript.Repl/      Interactive JS console (REPL)
    Browser/
      SuperRender.Browser/              Tabbed browser application
    Demo/
      SuperRender.Demo/                 Minimal Vulkan demo
  tests/
    SuperRender.Document.Tests/         295 tests
    SuperRender.Renderer.Tests/         216 tests
    SuperRender.Renderer.Image.Tests/   25 tests
    SuperRender.EcmaScript.Tests/       441 tests
    SuperRender.Browser.Tests/          265 tests
```

## Browser

A Vulkan-powered browser with tabbed browsing, built on the rendering and JS engines.

- **Tabbed browsing** -- create, switch, and close tabs; each tab owns its own document, render pipeline, and JS engine
- **Address bar** -- type URLs and press Enter to navigate; auto-adds `https://` for domain-like input
- **Navigation** -- Back/Forward history stack per tab, Reload, error pages, `about:blank` support
- **External resources** -- fetches HTML, CSS (`<link rel="stylesheet">`), and JavaScript (`<script src>`) with CORS validation
- **JavaScript execution** -- inline and external scripts run with full DOM bindings (`document.createElement`, `querySelector`, `innerHTML`, `classList`, `style`, etc.)
- **DOM events** -- `mousedown`/`mouseup`/`click`/`mousemove`/`mouseover`/`mouseout`/`mouseenter`/`mouseleave` dispatched to DOM nodes; capture/target/bubble propagation; `DOMContentLoaded` and `load` events
- **Timers** -- `setTimeout`/`setInterval`/`requestAnimationFrame` with real delays via monotonic `TimerScheduler`; timer queue drained per frame
- **Cookies** -- `CookieJar` with full `Set-Cookie` parsing (Domain, Path, Expires, Max-Age, Secure, HttpOnly, SameSite); `document.cookie` JS accessor
- **Storage** -- `localStorage` backed by SQLite, `sessionStorage` per-tab in memory; JS bindings (`getItem`/`setItem`/`removeItem`/`clear`/`key`/`length`)
- **HTTP caching** -- SQLite-backed cache with `Cache-Control`/`max-age`/`Expires` freshness, conditional requests (`If-None-Match`/`If-Modified-Since`), 304 handling
- **Fetch API** -- global `fetch()` returning Promises; `Response` with `status`/`ok`/`headers`/`.text()`/`.json()`
- **Location and History APIs** -- `window.location` (href, protocol, host, pathname, assign, replace, reload) and `window.history` (pushState, replaceState, back, forward, go)
- **Image loading** -- fetches `<img>` sources via HTTP/file/data: URIs, decodes with pure C# decoders (PNG/JPEG/BMP), caches in `ImageCache`, sets intrinsic dimensions for layout; alt text fallback
- **Text selection** -- click-and-drag to select text with font-aware hit testing, copy to clipboard
- **Content scrolling** -- mouse wheel, arrow keys, Page Up/Down, Home/End, Space; visual scrollbar indicator
- **Context menus** -- right-click for Cut/Copy/Paste/Select All (address bar) or Copy/Select All/View Source/Developer Tools (content)
- **Developer Tools** -- separate Vulkan window (F12 / Cmd+Shift+I) with console log, JS input, toolbar
- **Keyboard shortcuts** -- Cmd/Ctrl+T (new tab), Cmd/Ctrl+W (close), Cmd/Ctrl+Tab (switch), Cmd/Ctrl+L (address bar), Cmd/Ctrl+R / F5 (reload), Cmd+[/] (back/forward), Escape (unfocus)
- **CORS** -- same-origin checks and CORS header validation for sub-resources
- **HiDPI support** -- content scale derived from framebuffer size; font atlas rendered at scaled resolution for sharp text on Retina displays
- **Cross-platform** -- macOS (MoltenVK), Windows, Linux (native Vulkan)

## Rendering Engine

### HTML Parser
- State-machine tokenizer (16 states) with entity decoding
- Tree builder with auto html/head/body structure, void element handling, adoption agency algorithm, and error recovery
- DOM with `Node`, `Element`, `TextNode`, `Document` types
- DOM mutation: `AppendChild`, `RemoveChild`, `InsertBefore`, `CloneElement`, `QuerySelector`/`QuerySelectorAll`, class manipulation
- DOM events: `AddEventListener`/`RemoveEventListener`/`DispatchEvent` with capture/target/bubble propagation

### CSS Engine
- Full tokenizer and parser with shorthand expansion (`margin`, `padding`, `border`, `border-width`, `border-color`, `border-style`, `flex`, `flex-flow`, `border-radius`)
- Selectors Level 4:
  - Combinators: descendant (` `), child (`>`), adjacent-sibling (`+`), general-sibling (`~`)
  - Simple: type, class, ID, universal, attribute selectors, compound, comma-separated lists
  - Structural pseudo-classes: `:first-child`, `:last-child`, `:nth-child(An+B)`, `:only-child`, `:first-of-type`, `:last-of-type`, `:only-of-type`
  - Functional pseudo-classes: `:not()`, `:is()`, `:where()`
  - Dynamic pseudo-classes: `:hover`, `:focus`, `:active`, `:link`, `:visited`
  - Pseudo-elements: `::before`, `::after`
  - Other: `:root`, `:empty`
- Cascade with specificity calculation, `!important`, source-order sorting
- Global keywords: `initial`, `inherit`, `unset`, `revert`
- Inherited properties: `color`, `font-size`, `font-family`, `font-weight`, `font-style`, `text-align`, `line-height`, `white-space`, `visibility`, `text-transform`, `letter-spacing`, `word-spacing`, `cursor`, `word-break`, `overflow-wrap`, `list-style-type`
- CSS functions: `calc()`, `min()`, `max()`, `clamp()`
- Viewport units: `vw`, `vh`, `vmin`, `vmax`
- Inline `style` attribute support
- User-agent stylesheet with default browser styles
- `hidden` attribute support, box-sizing

### Supported CSS Properties
| Category | Properties |
|---|---|
| Display | `display` (block, inline, inline-block, flex, none) |
| Box model | `width`, `height`, `min-width`, `min-height`, `max-width`, `max-height`, `margin-*`, `padding-*`, `border-*-width`, `box-sizing` |
| Colors | `color`, `background-color`, `border-*-color` |
| Borders | `border-style`, `border` shorthand, `border-radius` |
| Typography | `font-size`, `font-family`, `font-weight`, `font-style`, `text-align`, `line-height`, `text-decoration`, `text-transform`, `letter-spacing`, `word-spacing`, `white-space` |
| Flexbox | `flex-direction`, `flex-wrap`, `justify-content`, `align-items`, `align-self`, `flex-grow`, `flex-shrink`, `flex-basis`, `gap` |
| Position | `position` (static, relative, absolute), `top`, `left`, `right`, `bottom`, `z-index` |
| Visual | `opacity`, `visibility`, `overflow`, `overflow-x`, `overflow-y`, `cursor` |
| Text | `word-break`, `overflow-wrap`, `list-style-type` |

Units: `px`, `em`, `rem`, `pt`, `%`, `auto`, `vw`, `vh`, `vmin`, `vmax`. Colors: hex (#RGB/#RRGGBB/#RRGGBBAA), `rgb()`, `rgba()`, named colors.

### Layout
- Block layout with width calculation, margin auto-centering, vertical stacking
- Inline layout with word-based text wrapping, line-height, text alignment (left/center/right/justify)
- Inline-block layout
- Flexbox layout: direction, wrap, justify-content, align-items, align-self, grow/shrink/basis, gap
- Position: relative and absolute positioning
- Whitespace collapsing, anonymous block wrapping for mixed block/inline children
- Overflow: hidden clipping via PushClip/PopClip paint commands
- Replaced element sizing for `<img>` with intrinsic dimensions and aspect ratio preservation
- Min/max width/height constraints

### Vulkan Renderer (Gpu Library)
- Quad pipeline for backgrounds, borders, and selection highlights (alpha-blended)
- Text pipeline with FreeType font atlas and alpha blending
- Persistent mapped ring buffers for vertex/index data (eliminates per-frame alloc/free)
- Partial dirty-region atlas re-upload
- GPU-side `vertexOffset` in draw calls
- Two-pass content rendering (quads pass -> selection highlights -> text pass) for correct z-ordering
- HiDPI content scale support -- layout in logical pixels, projection matrix maps to physical coordinates
- Font atlas generated at `BaseFontSize * contentScale` for sharp text on Retina/HiDPI displays
- Dirty-flag optimization -- only re-renders when the DOM changes
- Shared by both the Browser and Demo applications
- Cross-platform: MoltenVK on macOS, native Vulkan on Windows/Linux

## Image Decoders

Pure C# image decoders in `SuperRender.Renderer.Image` with zero external dependencies. Auto-detects format and returns RGBA pixel data.

| Format | Details |
|---|---|
| PNG | Color types 0/2/3/4/6, bit depths 1/2/4/8/16, all 5 filter types (None/Sub/Up/Average/Paeth), PLTE/tRNS transparency, DEFLATE decompression |
| JPEG | Baseline DCT (SOF0), Huffman decode, IDCT, YCbCr to RGB conversion, 4:4:4 and 4:2:0 chroma subsampling |
| BMP | 24-bit and 32-bit uncompressed, bottom-up and top-down row order |

## ECMAScript Engine

A DLR-based JavaScript engine with no external dependencies.

**Pipeline:** Source -> Lexer -> Parser (recursive descent + Pratt) -> DLR Expression Trees -> Compiled Delegates

### What's Implemented
- Full lexer with ES2025 token set
- Recursive descent parser with 20-level Pratt precedence, ASI, arrow functions, destructuring, modules
- Variables: `var`, `let`, `const` with TDZ
- Functions: declarations, expressions, arrows, closures, default/rest params
- Generators: `function*`, `yield`, `yield*`, iterator protocol via `Symbol.iterator`
- Async/await foundations with Promise integration
- Control flow: `if`/`else`, `for`, `for-in`, `for-of`, `while`, `do-while`, `switch`, `try`/`catch`/`finally`, labeled statements (`break label`/`continue label`)
- Classes: `class`, `extends`, `super`, static members, computed properties
- Operators: all arithmetic, comparison, logical, bitwise, nullish coalescing, optional chaining
- Template literals, tagged templates, spread/rest, destructuring assignment
- Prototype chain with `IDynamicMetaObjectProvider`
- Symbol: well-known symbols, `Symbol.iterator`, `Symbol.toPrimitive`, etc.
- Proxy and Reflect: meta-programming with handler traps
- 20 built-in objects: Object, Array, String, Number, Boolean, Math, JSON, Date, RegExp, Map, Set, WeakMap, WeakSet, Symbol, Promise, Proxy, Reflect, Error, ArrayBuffer, DataView
- Sandboxed .NET interop: `RegisterType<T>()` / `SetValue()` for controlled host access

### DOM Bindings
- `document` global: `createElement`, `createTextNode`, `getElementById`, `getElementsByTagName`/`getElementsByClassName`, `querySelector`/`querySelectorAll`, `body`, `head`, `title`, `cookie`
- `window` global: `document`, `innerWidth`/`innerHeight`, `devicePixelRatio`, `setTimeout`/`clearTimeout`, `setInterval`/`clearInterval`, `requestAnimationFrame`/`cancelAnimationFrame`, `localStorage`, `sessionStorage`, `location`, `history`, `console`, `fetch`
- Node API: `appendChild`, `removeChild`, `insertBefore`, `replaceChild`, `cloneNode`, `contains`, `parentNode`, `childNodes`, `textContent`, `addEventListener`, `removeEventListener`, `dispatchEvent`
- Element API: `tagName`, `id`, `className`, `classList` (add/remove/toggle/contains), `getAttribute`/`setAttribute`/`hasAttribute`/`toggleAttribute`, `querySelector`/`querySelectorAll`, `innerHTML`, `style`, `matches`, `closest`, `dataset`, `after`/`before`/`remove`
- Event API: `DomEvent`/`MouseEvent`/`KeyboardEvent` with `type`, `target`, `currentTarget`, `preventDefault`, `stopPropagation`, `clientX`/`clientY`, `key`/`code`
- `element.style`: camelCase CSS property get/set mapped to inline style attributes
- Fetch API: `fetch(url, options)` returning Promises with `Response` object
- Location API: `window.location` with `href`/`protocol`/`host`/`pathname`/`assign()`/`replace()`/`reload()`
- History API: `window.history` with `pushState()`/`replaceState()`/`back()`/`forward()`/`go()`
- Storage API: `localStorage`/`sessionStorage` with `getItem`/`setItem`/`removeItem`/`clear`/`key`/`length`
- 1:1 identity mapping between C# DOM nodes and JS wrapper objects via `ConditionalWeakTable`

### JS Console (REPL)
```bash
dotnet run --project src/EcmaScript/SuperRender.EcmaScript.Repl

# Or evaluate directly:
dotnet run --project src/EcmaScript/SuperRender.EcmaScript.Repl -- --eval "1 + 2"

# Or run a file:
dotnet run --project src/EcmaScript/SuperRender.EcmaScript.Repl -- script.js
```

Features: multiline editing, command history, ANSI-colored output, `.help`/`.exit`/`.clear`/`.editor` dot commands, Readline-style key bindings (arrow keys, word navigation, Ctrl+U/K/W).

## Roadmap

The master roadmap with P0-P4 prioritization is in [`TODO.md`](TODO.md).

Detailed feature gap audits by subsystem:

- **Browser:** [`src/Browser/SuperRender.Browser/browser-todos.md`](src/Browser/SuperRender.Browser/browser-todos.md) -- page zoom, find-in-page, bookmarks, downloads, forms, and more
- **CSS:** [`src/Document/SuperRender.Document/css-todos.md`](src/Document/SuperRender.Document/css-todos.md) -- grid, custom properties, transforms, transitions, animations, media queries, container queries, CSS nesting
- **HTML:** [`src/Document/SuperRender.Document/html-todos.md`](src/Document/SuperRender.Document/html-todos.md) -- full WHATWG tokenizer states, forms, tables, embedded content, Shadow DOM, MutationObserver
- **ECMAScript:** [`src/EcmaScript/SuperRender.EcmaScript.Compiler/es-2025-todos.md`](src/EcmaScript/SuperRender.EcmaScript.Compiler/es-2025-todos.md) -- BigInt, WeakRef, SharedArrayBuffer, Intl, Temporal, decorators

## Dependencies

| Project | Dependencies |
|---|---|
| SuperRender.Document | None (pure C#) |
| SuperRender.Renderer.Rendering | None (references Document only) |
| SuperRender.Renderer.Image | None (pure C#) |
| SuperRender.EcmaScript.Runtime | None (pure C#) |
| SuperRender.EcmaScript.Compiler | None (pure C#, DLR ships with .NET) |
| SuperRender.EcmaScript.Engine | None (references Compiler + Runtime only) |
| SuperRender.EcmaScript.Dom | None (references Document + Engine only) |
| SuperRender.EcmaScript.Repl | None (references Engine only) |
| SuperRender.Renderer.Gpu | Silk.NET (Vulkan, Windowing, Input, Shaderc), FreeTypeSharp, Silk.NET.MoltenVK.Native |
| SuperRender.Browser | References Document, Renderer.Rendering, Renderer.Gpu, Renderer.Image, EcmaScript.Engine, EcmaScript.Dom; Microsoft.Data.Sqlite |
| SuperRender.Demo | References Document, Renderer.Rendering, Renderer.Gpu |
| Tests | xUnit, Microsoft.NET.Test.Sdk |

## Requirements

- .NET 10 SDK
- Vulkan-capable GPU (for Browser and Demo; Document, Renderer.Rendering, Renderer.Image, and EcmaScript run anywhere)
- macOS, Windows, or Linux
