# SuperRenderer — Prioritized TODO

Master roadmap organizing all features by priority level. Each item shows current status.
Detailed specs for unimplemented items live in the per-project TODO files linked at the bottom.

---

## P0 Features — COMPLETED

Core rendering engine foundation. All implemented with 727 tests (P0 baseline).

### CSS
- [x] Box model: `width`, `height`, `margin`, `padding`, `border-width`/`color`/`style`
- [x] `box-sizing`: `content-box`, `border-box`
- [x] `min-width`, `max-width`, `min-height`, `max-height`
- [x] `display`: `block`, `inline`, `inline-block`, `flow-root`, `none`
- [x] `position`: `static`, `relative`, `absolute` with `top`/`left`/`right`/`bottom`
- [x] `z-index` and stacking context
- [x] `overflow`: `visible`, `hidden` (with `text-overflow: ellipsis`)
- [x] `white-space`: `normal`, `pre`, `nowrap`, `pre-wrap`, `pre-line`
- [x] `color`, `background-color`, hex colors, `rgb()`/`rgba()`, 37 named colors
- [x] `font-size` (px/em/rem/pt/%/keywords), `font-family` (comma-separated with generic fallback)
- [x] `font-weight` (normal/bold/numeric/bolder/lighter), `font-style` (normal/italic/oblique)
- [x] `text-align`: `left`, `right`, `center`, `justify`
- [x] `line-height` (number/length/percentage)
- [x] `text-decoration-line` (underline/overline/line-through), `text-decoration-color`
- [x] Cascade: specificity, `!important`, source order, inherited properties
- [x] Shorthand expansion: `margin`, `padding`, `border-width`, `border`
- [x] Selectors: type, class, ID, universal, descendant (` `), child (`>`), comma lists

### HTML/DOM
- [x] State-machine HTML tokenizer (16 states)
- [x] Tree builder with auto html/head/body structure, void elements
- [x] 6 named character references (`amp`, `lt`, `gt`, `quot`, `apos`, `nbsp`)
- [x] DOM: `Node`, `Element`, `TextNode`, `Document` with parent/child/sibling links
- [x] `DomMutationApi`: `SetTextContent`, `AddClass`/`RemoveClass`/`ToggleClass`, `CloneElement`, `QuerySelector`/`All`
- [x] DOM events: `addEventListener`/`removeEventListener`/`dispatchEvent`, capture/target/bubble
- [x] `DomEvent`, `MouseEvent`, `KeyboardEvent`
- [x] `DOMContentLoaded` and `load` events

### Browser
- [x] Tabbed browsing: create/close/switch tabs
- [x] Address bar with text editing, cursor, selection
- [x] Navigation: HTTP fetch, `file://`, `sr://` (embedded test pages)
- [x] External CSS/JS resource loading with CORS
- [x] Scrolling: mouse wheel, keyboard (arrows/PgUp/PgDn/Home/End/Space), scrollbar indicator
- [x] Link navigation: click `<a href>`, `target="_blank"` in new tab
- [x] Back/forward history (`NavigationHistory`)
- [x] Text selection: click-and-drag with font-aware hit testing
- [x] Context menus: right-click in address bar and content area
- [x] Keyboard shortcuts: Cmd+T/W/Tab/L/R, F5, F12, Escape, scroll keys
- [x] Developer tools: console window with JS execution, log capture, error display
- [x] HiDPI/Retina support

### EcmaScript
- [x] Full ES2025 lexer and parser (recursive descent + Pratt, 20 precedence levels)
- [x] DLR Expression tree compiler
- [x] JsValue hierarchy with prototype chains
- [x] 20 standard library builtins (Object, Array, String, Number, Math, JSON, Date, RegExp, Map, Set, Promise, Proxy, Reflect, etc.)
- [x] Generators (`function*`, `yield`, `yield*`) with thread-based coroutines
- [x] `async`/`await` with Promise integration
- [x] `for-of`, `for-in`, destructuring, spread, rest, template literals, modules
- [x] DOM bindings: `document`, `window`, `setTimeout`/`setInterval`/`requestAnimationFrame`

### GPU Rendering
- [x] Vulkan quad pipeline (backgrounds/borders) with alpha blending
- [x] Vulkan text pipeline (font atlas sampling)
- [x] Dynamic font atlas (2048x4096) with on-demand glyph rendering via FreeType
- [x] System font discovery, generic family mapping, CJK fallback
- [x] Persistent mapped ring buffers, partial dirty-region atlas uploads
- [x] HiDPI content scale (logical→physical projection)

---

## P1 Features — COMPLETED

Advanced rendering and browser interactivity. All implemented with ~500 additional tests.

### CSS
- [x] Selectors: `+`, `~`, `[attr]` variants, `:hover`/`:focus`/`:active`, `:first-child`/`:last-child`/`:nth-child(An+B)`, `:not()`/`:is()`/`:where()`, `:root`/`:empty`/`:link`/`:visited`, `::before`/`::after`
- [x] Flexbox: `display:flex`, `flex-direction`, `flex-wrap`, `justify-content`, `align-items`, `align-self`, `flex-grow`/`shrink`/`basis`, `flex` shorthand, `gap`
- [x] `border-radius` (4 corners + shorthand)
- [x] `opacity` (0-1) + `visibility` (visible/hidden/collapse)
- [x] Global keywords: `initial`, `inherit`, `unset`, `revert`
- [x] Full 148 CSS named colors + `hsl()`/`hsla()` + `currentcolor` + space-separated `rgb()`
- [x] `calc()`, `min()`, `max()`, `clamp()` + `vw`/`vh`/`vmin`/`vmax`
- [x] `text-transform`, `letter-spacing`, `word-spacing`
- [x] 9 additional inherited properties

### HTML/DOM
- [x] Full 2231 named character references (WHATWG spec)
- [x] Tree construction: auto-closing (`<p>`, `<li>`, `<dd>`/`<dt>`, `<option>`), adoption agency
- [x] DOM methods: `replaceChild`, `cloneNode`, `contains`, `textContent`, `matches`, `closest`, `dataset`, `hasAttribute`, `toggleAttribute`, element traversal, `after`/`before`/`remove`
- [x] JS DOM bindings for all new methods

### Browser
- [x] Cookies (in-memory jar, `Set-Cookie`, `Cookie` header, `document.cookie`)
- [x] `localStorage` (SQLite) + `sessionStorage` (per-tab)
- [x] HTTP caching (SQLite: `Cache-Control`/`ETag`/`Last-Modified`/`Expires`)
- [x] Fetch API (`fetch()` with Promise, `Response` with `.text()`/`.json()`)
- [x] `window.location` + `window.history` (`pushState`/`replaceState`/`popstate`)
- [x] Mouse events (`mousemove`/`mouseover`/`mouseout`/`mouseenter`/`mouseleave`) + `:hover` CSS
- [x] Image loading: pure C# PNG/BMP/JPEG decoders, `<img>` layout sizing, `data:` URI, alt text fallback

### EcmaScript
- [x] Labels on loops: `break label` / `continue label` runtime

---

## P2 Features — High Priority Deferred

Features needed for most modern web pages. Next implementation target.

### CSS
- [ ] Grid layout: `display: grid`, `grid-template-rows`/`columns`/`areas`, `fr` unit, `repeat()`/`minmax()`, `gap` — `css-todos.md §10`
- [ ] Custom properties: `--*` declarations, `var()` with fallback — `css-todos.md §3`
- [ ] `@media` queries: width/height, `prefers-color-scheme`, `prefers-reduced-motion` — `css-todos.md §28`
- [ ] `@import` rule — `css-todos.md §29`
- [ ] `@font-face` rule, `font-display` — `css-todos.md §14`
- [ ] `position: fixed` (viewport-relative), `position: sticky` (scroll-aware) — `css-todos.md §8`
- [ ] `float`/`clear` — `css-todos.md §11`
- [ ] `text-shadow`, `box-shadow` — `css-todos.md §13, §15`
- [ ] `background-image: url()`, gradients (`linear-gradient`, `radial-gradient`) — `css-todos.md §13`
- [ ] `transform` (2D: translate/scale/rotate/skew) — `css-todos.md §19`
- [ ] `transition` (property/duration/timing-function/delay) — `css-todos.md §20`
- [ ] `vertical-align` — `css-todos.md §16`
- [ ] `outline` and sub-properties — `css-todos.md §25`
- [ ] Per-side border style/color: `border-top-style`, `border-top-color`, etc. (longhands) — `css-todos.md §13`
- [ ] `@supports` rule — `css-todos.md §29`
- [ ] Relative length units: `ch`, `ex`, `lh` — `css-todos.md §2`

### HTML/DOM
- [ ] Form elements: `<input>` (text/password/checkbox/radio/submit/hidden), `<textarea>`, `<select>`/`<option>`, `<button>`, `<form>` submission — `html-todos.md §4.5`
- [ ] `<table>` layout: `<table>`/`<tr>`/`<td>`/`<th>`/`<thead>`/`<tbody>`, `border-collapse`, `border-spacing` — `html-todos.md §4.4`, `css-todos.md §18`
- [ ] `<details>`/`<summary>` disclosure widget — `html-todos.md §4.6`
- [ ] `<dialog>` modal and non-modal — `html-todos.md §4.6`
- [ ] `innerHTML` getter (HTML serialization) — `html-todos.md §3.4`
- [ ] `DocumentFragment` — `html-todos.md §3.1`
- [ ] `MutationObserver` — `html-todos.md §3.6`
- [ ] `getBoundingClientRect()` — `html-todos.md §3.4`
- [ ] `window.getComputedStyle()` — `html-todos.md §9.1`
- [ ] Focus management: `tabindex`, Tab/Shift+Tab cycling — `html-todos.md §6`
- [ ] `<script src>` with `async`/`defer` — `html-todos.md §4.8`

### Browser
- [ ] Find-in-page (Ctrl+F): highlight matches, scroll to match — `browser-todos.md §12`
- [ ] Page zoom (Ctrl+Plus/Minus) — `browser-todos.md §4`
- [ ] `window.open()` — `browser-todos.md §24`
- [ ] Keyboard events to content: `keydown`/`keyup` — `browser-todos.md §8`
- [ ] `scroll` event, `resize` event — `browser-todos.md §8`
- [ ] `focus`/`blur` events — `browser-todos.md §8`
- [ ] Mixed content blocking (HTTP on HTTPS) — `browser-todos.md §7`
- [ ] CORS preflight (`OPTIONS`) for non-simple requests — `browser-todos.md §7`
- [ ] Error pages: DNS failure, timeout, HTTP 4xx/5xx — `browser-todos.md §22`
- [ ] `about:blank` and other `about:` pages — `browser-todos.md §22`
- [ ] Loading spinner per tab — `browser-todos.md §16`

### EcmaScript
- [ ] `Promise.withResolvers()` — `es-2025-todos.md`
- [ ] `Object.groupBy()` / `Map.groupBy()` — `es-2025-todos.md`
- [ ] `String.prototype.isWellFormed()` / `toWellFormed()` — `es-2025-todos.md`
- [ ] Iterator Helpers (`.map()`, `.filter()`, `.take()`, etc.) — `es-2025-todos.md`
- [ ] Set methods (`.union()`, `.intersection()`, `.difference()`, etc.) — `es-2025-todos.md`
- [ ] `structuredClone()` — `es-2025-todos.md`

---

## P3 Features — Medium Priority Deferred

Features for richer rendering and interactivity. Important but less urgent.

### CSS
- [ ] `@keyframes` and `animation` properties — `css-todos.md §21`
- [ ] `transform` (3D: `translate3d`, `rotate3d`, `perspective`) — `css-todos.md §19`
- [ ] `filter` (`blur()`, `brightness()`, `grayscale()`, `drop-shadow()`, etc.) — `css-todos.md §22`
- [ ] `backdrop-filter` — `css-todos.md §22`
- [ ] `mix-blend-mode`, `isolation` — `css-todos.md §23`
- [ ] `clip-path` — `css-todos.md §24`
- [ ] CSS Nesting (`.foo { .bar { } }`, `&`) — `css-todos.md §30`
- [ ] `@layer` (cascade layers) — `css-todos.md §5`
- [ ] `@scope` — `css-todos.md §5`
- [ ] `@container` queries — `css-todos.md §27`
- [ ] Logical properties: `margin-block-*`, `margin-inline-*`, `padding-block-*`, etc. — `css-todos.md §31`
- [ ] `writing-mode`, `direction`, `text-orientation` — `css-todos.md §32`
- [ ] `list-style-type` full set (disc/circle/square/decimal/alpha/roman) — `css-todos.md §17`
- [ ] `::marker` pseudo-element styling — `css-todos.md §17`
- [ ] CSS counters: `counter-reset`, `counter-increment`, `counter()` — `css-todos.md §17`
- [ ] `content` property values beyond strings (counters, `attr()`, images) — `css-todos.md §25`
- [ ] `contain`, `content-visibility` — `css-todos.md §26`
- [ ] `aspect-ratio` — `css-todos.md §34`
- [ ] `will-change` — `css-todos.md §34`
- [ ] `scroll-snap` properties — `css-todos.md §33`
- [ ] `text-indent`, `text-align-last` — `css-todos.md §15`
- [ ] `hyphens` — `css-todos.md §15`

### HTML/DOM
- [ ] Full WHATWG tokenizer (80 states, currently 16) — `html-todos.md §1`
- [ ] Full insertion modes (23 modes) — `html-todos.md §2`
- [ ] `<iframe>` — `html-todos.md §4.7`
- [ ] `<canvas>` 2D context — `html-todos.md §4.7`
- [ ] Inline SVG — `html-todos.md §4.7`
- [ ] `<video>` / `<audio>` stubs — `html-todos.md §4.7`
- [ ] Custom Elements (`customElements.define()`) — `html-todos.md §8`
- [ ] Shadow DOM (`attachShadow()`) — `html-todos.md §3.7`
- [ ] `<template>` element — `html-todos.md §8`
- [ ] `Range` and `Selection` interfaces — `html-todos.md §3.8`
- [ ] `TreeWalker`, `NodeIterator` — `html-todos.md §3.9`
- [ ] `IntersectionObserver`, `ResizeObserver` — `html-todos.md §9.4`
- [ ] `<meta charset>`, `<base>` — `html-todos.md §4.9`

### Browser
- [ ] Bookmarks and session restore — `browser-todos.md §14`
- [ ] Downloads (Content-Disposition, `<a download>`) — `browser-todos.md §15`
- [ ] Tab reordering, pin tab, duplicate tab — `browser-todos.md §16`
- [ ] Status bar on link hover — `browser-todos.md §17`
- [ ] Hover cursor change (pointer over links) — `browser-todos.md §17`
- [ ] URL autocomplete from history — `browser-todos.md §23`
- [ ] Search engine fallback in address bar — `browser-todos.md §23`
- [ ] DOM inspector in DevTools — `browser-todos.md §13`
- [ ] Network panel in DevTools — `browser-todos.md §13`
- [ ] View Source with syntax highlighting — `browser-todos.md §13`
- [ ] Smooth scrolling — `browser-todos.md §3`
- [ ] `window.scrollTo()` / `Element.scrollIntoView()` — `browser-todos.md §3`
- [ ] Printing / export to PDF — `browser-todos.md §19`

### EcmaScript
- [ ] BigInt — `es-2025-todos.md`
- [ ] RegExp: named captures, lookbehind, Unicode property escapes, `/v` flag — `es-2025-todos.md`
- [ ] `Array.fromAsync()` — `es-2025-todos.md`
- [ ] Tail call optimization — `es-2025-todos.md`

---

## P4 Features — Low Priority / Experimental

Long-term goals, standards compliance, and experimental features.

### CSS
- [ ] `@property` (CSS Properties and Values API) — `css-todos.md §3`
- [ ] Color Level 4: `hwb()`, `lab()`, `lch()`, `oklch()`, `oklab()`, `color()`, `color-mix()`, `light-dark()` — `css-todos.md §4`
- [ ] System colors (`Canvas`, `CanvasText`, `LinkText`, etc.) — `css-todos.md §4`
- [ ] Variable fonts (`font-variation-settings`) — `css-todos.md §14`
- [ ] `font-feature-settings`, `font-optical-sizing` — `css-todos.md §14`
- [ ] Math functions: `round()`, `mod()`, `rem()`, `abs()`, `sign()`, trig — `css-todos.md §2`
- [ ] Dynamic viewport units: `dvw`/`dvh`, `svw`/`svh`, `lvw`/`lvh` — `css-todos.md §2`
- [ ] Container query units: `cqw`/`cqh` — `css-todos.md §2`
- [ ] `overscroll-behavior` — `css-todos.md §34`
- [ ] `color-scheme` — `css-todos.md §34`
- [ ] `touch-action` — `css-todos.md §34`
- [ ] `@page` (paged media) — `css-todos.md §29`
- [ ] `@counter-style` — `css-todos.md §29`
- [ ] Subgrid, Masonry layout — `css-todos.md §10`

### HTML/DOM
- [ ] Accessibility: ARIA roles, `aria-*` attributes, accessibility tree — `html-todos.md §10`
- [ ] `popover` attribute — `html-todos.md §6`
- [ ] `inert` attribute — `html-todos.md §6`
- [ ] `contenteditable` — `html-todos.md §6`
- [ ] Drag-and-drop — `html-todos.md §6`
- [ ] `<ruby>`/`<rt>`/`<rp>` — `html-todos.md §4.2`
- [ ] `<bdi>`/`<bdo>` bidi — `html-todos.md §4.2`

### Browser
- [ ] Multi-window support (`Ctrl+N`, drag-tab-out) — `browser-todos.md §24`
- [ ] Incognito mode — `browser-todos.md brainstorming`
- [ ] Content blocker — `browser-todos.md brainstorming`
- [ ] Plugin API — `browser-todos.md brainstorming`
- [ ] Render tree visualizer — `browser-todos.md brainstorming`
- [ ] CSS cascade debugger — `browser-todos.md brainstorming`
- [ ] Step-through renderer — `browser-todos.md brainstorming`
- [ ] Hot-reload local files — `browser-todos.md brainstorming`
- [ ] Dark mode chrome — `browser-todos.md brainstorming`

### EcmaScript
- [ ] WeakRef / FinalizationRegistry — `es-2025-todos.md`
- [ ] SharedArrayBuffer / Atomics — `es-2025-todos.md`
- [ ] Intl (Collator, DateTimeFormat, NumberFormat, etc.) — `es-2025-todos.md`
- [ ] Temporal — `es-2025-todos.md`
- [ ] Decorators — `es-2025-todos.md`
- [ ] Import assertions / attributes — `es-2025-todos.md`
- [ ] ShadowRealm — `es-2025-todos.md`

---

## Detailed Specs

- `src/Document/SuperRender.Document/css-todos.md` — 34 CSS sections
- `src/Document/SuperRender.Document/html-todos.md` — 10 HTML/DOM sections
- `src/Browser/SuperRender.Browser/browser-todos.md` — 25 browser sections + brainstorming
- `src/EcmaScript/SuperRender.EcmaScript.Compiler/es-2025-todos.md` — 26 ES2025 items
