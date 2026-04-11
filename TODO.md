# SuperRenderer — Prioritized TODO

Master roadmap organizing all deferred features across the project by priority.
Each item links to the detailed todo file where it is fully described.

> **Priority levels:**
> - **P0** — Critical. The browser is fundamentally broken without these. Blocks basic page viewing and interaction.
> - **P1** — High. Required by the vast majority of real-world websites. Without these, almost no modern site works.
> - **P2** — Medium. Important quality and completeness features that make the browser genuinely useful day-to-day.
> - **P3** — Low. Polish, advanced specs, and nice-to-have features for broader compatibility.
> - **P4** — Aspirational. Experimental ideas, long-term vision, and research projects.

Detailed todo files:
- [`src/SuperRender.Browser/browser-todos.md`](src/SuperRender.Browser/browser-todos.md) — Browser shell features (25 sections + experimental)
- [`src/SuperRender.Core/css-todos.md`](src/SuperRender.Core/css-todos.md) — CSS spec gaps (34 sections)
- [`src/SuperRender.Core/html-todos.md`](src/SuperRender.Core/html-todos.md) — HTML/DOM spec gaps (10 sections)
- [`src/SuperRender.EcmaScript/es-2025-todos.md`](src/SuperRender.EcmaScript/es-2025-todos.md) — ECMAScript 2025 gaps (26 items)

---

## P0 — Critical

Without these, the browser cannot display or interact with even the simplest multi-page website.

### Browser — Content scrolling ~~DONE~~
~~Pages taller than the viewport are silently clipped. This is the single most visible deficiency.~~
Implemented: `ScrollState` per tab with mouse wheel, keyboard scrolling (arrows, Page Up/Down, Home/End, Space), scroll-to-top on navigation, visual scrollbar indicator.
- Remaining: smooth scrolling, CSS `overflow`, `window.scrollTo()` JS API, scroll events
- *Details:* [browser-todos.md §3](src/SuperRender.Browser/browser-todos.md)

### Browser — Link navigation ~~DONE~~
~~`<a href>` elements render as text but clicking does nothing. Users cannot follow links.~~
Implemented: `LayoutBoxHitTester` hit-tests layout boxes, finds `<a>` ancestor, navigates on click. `target="_blank"` opens in new tab.
- Remaining: hover cursor, status bar, `rel="noopener"`, `javascript:` URIs
- *Details:* [browser-todos.md §17](src/SuperRender.Browser/browser-todos.md)

### Browser — Back/Forward history ~~DONE~~
~~Buttons exist in the chrome but have no history stack. There is no way to go back after navigating.~~
Implemented: `NavigationHistory` per tab with URI list and cursor. Back/Forward buttons wired to history navigation.
- Remaining: `window.history` API, `window.location` object
- *Details:* [browser-todos.md §1](src/SuperRender.Browser/browser-todos.md)

### Browser — Keyboard shortcuts ~~DONE~~
~~No keyboard shortcuts beyond address bar text editing. The browser is mouse-only.~~
Implemented: Cmd/Ctrl+T (new tab), Cmd/Ctrl+W (close tab), Cmd/Ctrl+Tab (switch tab), Cmd/Ctrl+L (focus address bar), Cmd/Ctrl+R / F5 (reload), Escape (unfocus). Platform-aware (Cmd on macOS, Ctrl elsewhere).
- Remaining: Ctrl+1-9 (tab by index), Alt+Left/Right (back/forward), Ctrl+F, zoom, F11
- *Details:* [browser-todos.md §2](src/SuperRender.Browser/browser-todos.md)

### Browser — DOM event system ~~DONE~~
~~No `addEventListener`, no event propagation. Virtually all interactive JS depends on this.~~
Implemented: `EventTarget` on Node with `addEventListener`/`removeEventListener`/`dispatchEvent`. Full capture/target/bubble propagation. `DomEvent`, `MouseEvent`, `KeyboardEvent` classes. `mousedown`/`mouseup`/`click` dispatched from InputHandler. `DOMContentLoaded`/`load` fired on page load. JS `JsEventWrapper` exposes event properties.
- Remaining: `mousemove`/`mouseover`/`mouseout`, `keydown`/`keyup` in content area, `scroll`/`resize`/`focus`/`blur` events
- *Details:* [browser-todos.md §8](src/SuperRender.Browser/browser-todos.md), [html-todos.md §3.5](src/SuperRender.Core/html-todos.md)

### Browser — Timers with real delays ~~DONE~~
~~`setTimeout` fires immediately, `setInterval` is a no-op. Breaks any JS that depends on timing.~~
Implemented: `TimerScheduler` with monotonic Stopwatch clock. `setTimeout` with real delay, `setInterval` (min 4ms), `requestAnimationFrame` tied to render loop. Timer queue drained each frame in `OnRender`.
- Remaining: microtask queue (`queueMicrotask`, `Promise.then()` scheduling)
- *Details:* [browser-todos.md §9](src/SuperRender.Browser/browser-todos.md)

### CSS — Positioning (apply in layout) ~~DONE~~
~~`position: relative/absolute` and `top/left/right/bottom` are parsed but **not applied** in layout.~~
Implemented: Relative positioning applies visual offset without affecting siblings. Absolute positioning removes element from flow, positions relative to containing block. z-index sorts positioned elements during painting.
- Remaining: `position: fixed`, `position: sticky`, stacking context edge cases
- *Details:* [css-todos.md §8](src/SuperRender.Core/css-todos.md)

### CSS — `inline-block` and `flow-root` ~~DONE~~
~~Without `inline-block`, many common layout patterns break (buttons, inline containers, nav items).~~
Implemented: `display: inline-block` participates in inline flow with block-level internal layout. `display: flow-root` creates a new block formatting context.
- *Details:* [css-todos.md §7](src/SuperRender.Core/css-todos.md)

### CSS — `box-sizing: border-box` ~~DONE~~
~~Nearly every modern site uses `* { box-sizing: border-box }`. Without it, all dimensions are wrong.~~
Implemented: `box-sizing: border-box` includes padding and border in specified width/height. `min-width`, `max-width`, `min-height`, `max-height` constraints applied in layout.
- *Details:* [css-todos.md §6](src/SuperRender.Core/css-todos.md)

### CSS — Overflow ~~DONE~~
~~No overflow handling means content bleeds out of its container with no way to clip or scroll.~~
Implemented: `overflow: hidden` clips content via PushClip/PopClip paint commands and clamps height. `text-overflow: ellipsis` parsed. `overflow: scroll | auto` parsed.
- Remaining: scroll containers, CSS `overflow` per-element scrolling
- *Details:* [css-todos.md §12](src/SuperRender.Core/css-todos.md)

### HTML — User-agent stylesheet defaults ~~DONE~~
~~Most text-level elements lack default styles.~~
Implemented: Full user-agent stylesheet with bold, italic, underline, strikethrough, monospace, link styling, heading sizes/margins, blockquote indent, hr border, pre white-space:pre, mark highlight, small size.
- *Details:* [html-todos.md §5](src/SuperRender.Core/html-todos.md), [html-todos.md §4.2](src/SuperRender.Core/html-todos.md)

### EcmaScript — Async/Await runtime ~~DONE~~
~~`async`/`await` parses correctly but does not execute. Breaks most modern JS.~~
Implemented: Thread-based coroutine pattern. Async functions return Promises. `await` suspends and resumes on promise resolution/rejection. try/catch around await works. Multiple sequential awaits supported.
- Remaining: microtask queue for spec-compliant scheduling
- *Details:* [es-2025-todos.md — Async/Await](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — Generator runtime ~~DONE~~
~~`function*`/`yield` parses correctly but does not execute.~~
Implemented: Thread-based coroutine for generator state machine. `next(value)` sends values, `return(value)` completes, `throw(error)` injects errors. `for-of` iteration, `yield*` delegation supported.
- *Details:* [es-2025-todos.md — Generators](src/SuperRender.EcmaScript/es-2025-todos.md)

---

## P1 — High

Required by the vast majority of real-world websites.

### Browser — Cookies
No cookie storage at all. Most sites require cookies for sessions, CSRF tokens, preferences.
- Cookie jar (in-memory), `Set-Cookie` parsing, automatic `Cookie` header attachment
- HttpOnly, Secure, SameSite enforcement
- `document.cookie` JS getter/setter
- *Details:* [browser-todos.md §5](src/SuperRender.Browser/browser-todos.md)

### Browser — Web Storage
No `localStorage` or `sessionStorage`. Many SPAs rely on these for state.
- Per-origin `localStorage` persisted to disk
- Per-tab `sessionStorage`
- `storage` event for cross-tab notification
- *Details:* [browser-todos.md §5](src/SuperRender.Browser/browser-todos.md)

### Browser — Fetch / XMLHttpRequest
JS cannot make any network requests. No AJAX, no API calls.
- `fetch()` with Promise-based API, `XMLHttpRequest`
- Request/Response objects, CORS preflight
- *Details:* [browser-todos.md §21](src/SuperRender.Browser/browser-todos.md)

### Browser — Form elements
No `<input>`, `<form>`, `<button>`, `<textarea>`, `<select>`. No way to submit data.
- Text input with focus/cursor/selection, password masking
- Checkbox, radio, submit/reset buttons
- `<form>` submission with `application/x-www-form-urlencoded`
- *Details:* [browser-todos.md §11](src/SuperRender.Browser/browser-todos.md)

### Browser — Image loading
No `<img>` support. Pages with images show nothing.
- HTTP fetch, PNG/JPEG/WebP decoding, GPU texture upload, rendered as textured quad
- `width`/`height` attributes, CSS sizing, `alt` text fallback
- *Details:* [browser-todos.md §10](src/SuperRender.Browser/browser-todos.md)

### Browser — HTTP caching
Every navigation re-fetches all resources. Slow and bandwidth-wasteful.
- In-memory response cache, `Cache-Control`/`ETag`/`Last-Modified` handling
- Content compression (`Accept-Encoding: gzip, deflate, br`)
- *Details:* [browser-todos.md §6](src/SuperRender.Browser/browser-todos.md)

### Browser — `window.location` and `window.history`
Scripts cannot read the current URL or manipulate browser history.
- `location` object (href, hostname, pathname, search, hash, assign, replace)
- `history.pushState()`, `replaceState()`, `popstate` event
- *Details:* [browser-todos.md §1](src/SuperRender.Browser/browser-todos.md)

### Browser — Mixed content blocking
HTTP sub-resources on HTTPS pages are loaded without warning.
- Block insecure sub-resource loads on secure pages
- HTTPS padlock / "Not Secure" label in address bar
- *Details:* [browser-todos.md §7](src/SuperRender.Browser/browser-todos.md)

### CSS — Flexbox
The single most used layout mode on modern websites. Without it, most page layouts break.
- `display: flex`, `flex-direction`, `flex-wrap`, `justify-content`, `align-items`
- `flex-grow`, `flex-shrink`, `flex-basis`, `gap`
- *Details:* [css-todos.md §9](src/SuperRender.Core/css-todos.md)

### CSS — Core text properties
`font-weight`, `font-style`, `text-decoration` are not implemented. Text all looks the same.
- `font-weight` (normal/bold/numeric), `font-style` (italic/oblique)
- `text-decoration-line` (underline/overline/line-through), `text-decoration-color/style`
- `text-transform`, `letter-spacing`, `word-spacing`
- `white-space` (pre, nowrap, pre-wrap, pre-line)
- *Details:* [css-todos.md §14](src/SuperRender.Core/css-todos.md), [css-todos.md §15](src/SuperRender.Core/css-todos.md)

### CSS — Selector essentials
Many stylesheets rely on attribute selectors, sibling combinators, and common pseudo-classes.
- Adjacent sibling (`+`), general sibling (`~`)
- Attribute selectors (`[attr]`, `[attr="val"]`, `[attr^=]`, `[attr$=]`, `[attr*=]`)
- `:hover`, `:focus`, `:active`
- `:first-child`, `:last-child`, `:nth-child()`, `:not()`
- *Details:* [css-todos.md §1](src/SuperRender.Core/css-todos.md)

### CSS — `border-radius`
Rounded corners are ubiquitous. Without them, all containers look boxy.
- `border-radius` and per-corner longhands
- *Details:* [css-todos.md §13](src/SuperRender.Core/css-todos.md)

### CSS — `opacity` and `visibility`
Cannot hide elements with opacity or visibility. Many show/hide animations depend on `opacity`.
- `opacity`, `visibility: hidden | collapse`
- *Details:* [css-todos.md §25](src/SuperRender.Core/css-todos.md)

### CSS — Full inherited property list
Only 5 properties inherit (color, font-size, font-family, text-align, line-height). The spec defines 40+.
- `font-weight`, `font-style`, `white-space`, `letter-spacing`, `word-spacing`, `cursor`, `visibility`, `list-style-*`, `direction`, `text-transform`, `word-break`, `overflow-wrap`, etc.
- *Details:* [css-todos.md §5](src/SuperRender.Core/css-todos.md)

### CSS — Global keywords
`initial`, `inherit`, `unset`, `revert` are not supported. Many resets rely on these.
- *Details:* [css-todos.md §2](src/SuperRender.Core/css-todos.md)

### CSS — Color completeness
Only 37 named colors are implemented; spec defines 148. `hsl()` is not supported.
- Full named color set, `hsl()`/`hsla()`, `currentcolor`
- Space-separated `rgb()` syntax
- *Details:* [css-todos.md §4](src/SuperRender.Core/css-todos.md)

### HTML — Character references
Only 6 named entities implemented; WHATWG defines 2231. Pages with `&mdash;`, `&copy;`, `&nbsp;` (beyond basic) break.
- Full named character reference table
- Numeric character reference overflow/surrogate checking
- *Details:* [html-todos.md §1](src/SuperRender.Core/html-todos.md)

### HTML — Tree construction improvements
Missing auto-closing rules, adoption agency, foster parenting. Malformed HTML is everywhere.
- `<p>` auto-closing before block elements
- `<li>`, `<dd>`/`<dt>`, `<option>` auto-closing
- Adoption agency algorithm for `<b>`, `<i>`, `<a>` misnesting
- *Details:* [html-todos.md §2](src/SuperRender.Core/html-todos.md), [html-todos.md §7](src/SuperRender.Core/html-todos.md)

### HTML — Essential DOM methods
Missing `replaceChild`, `cloneNode`, `contains`, `textContent`, `matches`, `closest` on C# DOM classes.
- Node: `replaceChild`, `cloneNode(deep)`, `normalize`, `contains`, `textContent`
- Element: `matches(selector)`, `closest(selector)`, `classList` full API, `dataset`
- Document: `getElementById`, `querySelector/All` on Document (currently only on DomMutationApi)
- *Details:* [html-todos.md §3.2–3.4](src/SuperRender.Core/html-todos.md)

### HTML — Forms (parsing & DOM)
Form elements are not recognized with any special behavior during parsing or in the DOM.
- `<form>`, `<input>`, `<textarea>`, `<select>`, `<button>`, `<label>`, `<fieldset>`
- Form submission, validation attributes
- *Details:* [html-todos.md §4.5](src/SuperRender.Core/html-todos.md)

### EcmaScript — Labels on loops
`break label` / `continue label` don't execute at runtime. Some transpiled code depends on this.
- *Details:* [es-2025-todos.md — Labels](src/SuperRender.EcmaScript/es-2025-todos.md)

---

## P2 — Medium

Important for quality and broader site compatibility.

### Browser — Find-in-page
No way to search for text on a page.
- Ctrl/Cmd+F, incremental search, match highlighting, next/prev navigation
- *Details:* [browser-todos.md §12](src/SuperRender.Browser/browser-todos.md)

### Browser — Page zoom
No zoom support. Important for accessibility and readability.
- Ctrl/Cmd+Plus/Minus, Ctrl/Cmd+0 reset, pinch-to-zoom
- *Details:* [browser-todos.md §4](src/SuperRender.Browser/browser-todos.md)

### Browser — Developer tools (console)
`console.log` output goes to the terminal, not visible in the browser.
- JS console panel, DOM inspector, network panel
- *Details:* [browser-todos.md §13](src/SuperRender.Browser/browser-todos.md)

### Browser — Security headers
No CSP, no content-type sniffing protection, no referrer policy.
- Content Security Policy, `X-Content-Type-Options`, `Referrer-Policy`, SRI
- CORS preflight (`OPTIONS`) for non-simple requests
- *Details:* [browser-todos.md §7](src/SuperRender.Browser/browser-todos.md)

### Browser — Bookmarks & session restore
No persistence between sessions.
- Bookmark current page, bookmarks bar/manager, session restore on launch
- *Details:* [browser-todos.md §14](src/SuperRender.Browser/browser-todos.md)

### Browser — Address bar enhancements
- Search engine fallback (non-URL input → search query)
- Select-all on focus, URL autocomplete from history
- *Details:* [browser-todos.md §23](src/SuperRender.Browser/browser-todos.md)

### Browser — Error page variants
Only a generic error page exists. Should distinguish DNS failure, timeout, HTTP errors, SSL errors.
- *Details:* [browser-todos.md §22](src/SuperRender.Browser/browser-todos.md)

### Browser — Link styling
`<a>` elements are visually indistinguishable from regular text.
- User-agent default: `a { color: blue; text-decoration: underline; }`
- `:visited` purple, `:hover` color change, `cursor: pointer`
- *Details:* [browser-todos.md §18](src/SuperRender.Browser/browser-todos.md)

### Browser — Tab enhancements
- Tab reordering, duplicate tab, pin tab, close others, loading spinner
- *Details:* [browser-todos.md §16](src/SuperRender.Browser/browser-todos.md)

### CSS — Grid layout
Second most common layout system after flexbox.
- `display: grid`, `grid-template-*`, `grid-row/column`, `fr` unit, `repeat()`, `gap`
- *Details:* [css-todos.md §10](src/SuperRender.Core/css-todos.md)

### CSS — Custom properties
CSS variables are used pervasively on modern sites (theming, design tokens).
- `--*` declarations, `var()` function with fallback, cyclic dependency detection
- *Details:* [css-todos.md §3](src/SuperRender.Core/css-todos.md)

### CSS — `calc()`
Many layouts depend on `calc()` for responsive sizing.
- `calc()`, `min()`, `max()`, `clamp()`
- *Details:* [css-todos.md §2](src/SuperRender.Core/css-todos.md)

### CSS — Media queries
No `@media` support. Cannot do responsive design.
- `@media` rule, width/height features, `prefers-color-scheme`, `prefers-reduced-motion`
- *Details:* [css-todos.md §28](src/SuperRender.Core/css-todos.md)

### CSS — At-rules
No at-rule support at all beyond style rules.
- `@import`, `@media`, `@font-face`, `@keyframes`, `@supports`
- *Details:* [css-todos.md §29](src/SuperRender.Core/css-todos.md)

### CSS — `background-image` and gradients
Only solid `background-color` works. No images, no gradients.
- `background-image: url()`, `linear-gradient()`, `radial-gradient()`
- `background-size`, `background-position`, `background-repeat`
- *Details:* [css-todos.md §13](src/SuperRender.Core/css-todos.md)

### CSS — `box-shadow`
Used heavily for card UI, dropdowns, modals.
- `box-shadow` (offset, blur, spread, color, inset)
- *Details:* [css-todos.md §13](src/SuperRender.Core/css-todos.md)

### CSS — Viewport-relative units
`vw`, `vh`, `vmin`, `vmax` are common in responsive design.
- *Details:* [css-todos.md §2](src/SuperRender.Core/css-todos.md)

### CSS — Transitions (basic)
Smooth property changes on hover/focus are a core UX pattern.
- `transition-property`, `transition-duration`, `transition-timing-function`, `transition-delay`
- *Details:* [css-todos.md §20](src/SuperRender.Core/css-todos.md)

### CSS — Pseudo-elements
`::before` and `::after` are extremely common for decorative content.
- `::before`, `::after`, `content` property
- `::placeholder`, `::selection`
- *Details:* [css-todos.md §1](src/SuperRender.Core/css-todos.md)

### CSS — Logical combinators
`:not()`, `:is()`, `:where()`, `:has()` are increasingly common in modern CSS.
- *Details:* [css-todos.md §1](src/SuperRender.Core/css-todos.md)

### HTML — Tables
Tables are still used for tabular data. No table layout exists.
- `<table>`, `<tr>`, `<td>`, `<th>`, `<thead>`, `<tbody>`, `<tfoot>`, `<caption>`
- Table layout algorithm, `colspan`/`rowspan`, `border-collapse`
- *Details:* [html-todos.md §4.4](src/SuperRender.Core/html-todos.md), [css-todos.md §18](src/SuperRender.Core/css-todos.md)

### HTML — List styling
Lists display as blocks but lack markers and correct indentation.
- `list-style-type`, `list-style-position`, `<ol>` numbering, `::marker`
- *Details:* [html-todos.md §4.3](src/SuperRender.Core/html-todos.md), [css-todos.md §17](src/SuperRender.Core/css-todos.md)

### HTML — Interactive elements
`<details>`/`<summary>` toggle and `<dialog>` modal are commonly used.
- *Details:* [html-todos.md §4.6](src/SuperRender.Core/html-todos.md)

### HTML — `<img>` element (parsing/DOM side)
The HTML side of image support: intrinsic sizing, `alt`, `srcset`, `loading`.
- *Details:* [html-todos.md §4.7](src/SuperRender.Core/html-todos.md)

### HTML — Tokenizer: RCDATA/RAWTEXT states
`<title>` and `<textarea>` content can contain HTML-like text that should not be parsed as tags.
- *Details:* [html-todos.md §1](src/SuperRender.Core/html-todos.md)

### EcmaScript — Set methods (ES2025)
`union`, `intersection`, `difference`, etc. — straightforward to add.
- *Details:* [es-2025-todos.md — Set Methods](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — Object.groupBy / Map.groupBy
Commonly used utility. Straightforward to implement.
- *Details:* [es-2025-todos.md — Object.groupBy](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — Iterator Helpers (ES2025)
`.map()`, `.filter()`, `.take()`, `.toArray()`, etc. on iterators.
- *Details:* [es-2025-todos.md — Iterator Helpers](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — RegExp enhancements
Named capture groups, lookbehind, Unicode property escapes.
- *Details:* [es-2025-todos.md — RegExp](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — `Promise.withResolvers()`
Simple static method, commonly used.
- *Details:* [es-2025-todos.md — Promise.withResolvers](src/SuperRender.EcmaScript/es-2025-todos.md)

---

## P3 — Low

Polish, advanced specs, broader compatibility.

### Browser — Downloads
No download handling (`Content-Disposition`, `<a download>`).
- *Details:* [browser-todos.md §15](src/SuperRender.Browser/browser-todos.md)

### Browser — Printing & export
No print, screenshot, or save-as capability.
- *Details:* [browser-todos.md §19](src/SuperRender.Browser/browser-todos.md)

### Browser — Accessibility
No focus rings, keyboard navigation, ARIA roles, or screen reader support.
- *Details:* [browser-todos.md §20](src/SuperRender.Browser/browser-todos.md), [html-todos.md §10](src/SuperRender.Core/html-todos.md)

### Browser — Multi-window support
Only a single window. No `window.open()`, no drag-tab-to-new-window.
- *Details:* [browser-todos.md §24](src/SuperRender.Browser/browser-todos.md)

### Browser — Notifications & permissions
No Notification API, `navigator.permissions`, geolocation, clipboard API stubs.
- *Details:* [browser-todos.md §25](src/SuperRender.Browser/browser-todos.md)

### CSS — Animations
`@keyframes`, `animation-*` properties.
- *Details:* [css-todos.md §21](src/SuperRender.Core/css-todos.md)

### CSS — Transforms
2D/3D `transform`, `transform-origin`, individual `translate`/`rotate`/`scale`.
- *Details:* [css-todos.md §19](src/SuperRender.Core/css-todos.md)

### CSS — Filters and blend modes
`filter`, `backdrop-filter`, `mix-blend-mode`.
- *Details:* [css-todos.md §22](src/SuperRender.Core/css-todos.md), [css-todos.md §23](src/SuperRender.Core/css-todos.md)

### CSS — Floats and clear
Legacy but still found on many sites.
- `float: left | right`, `clear: both`
- *Details:* [css-todos.md §11](src/SuperRender.Core/css-todos.md)

### CSS — `position: fixed` and `sticky`
Fixed headers/footers, sticky navigation.
- *Details:* [css-todos.md §8](src/SuperRender.Core/css-todos.md)

### CSS — Nesting
`.foo { .bar { ... } }` and `&` nesting selector.
- *Details:* [css-todos.md §30](src/SuperRender.Core/css-todos.md)

### CSS — Container queries
`@container`, `container-type`, container query units.
- *Details:* [css-todos.md §27](src/SuperRender.Core/css-todos.md)

### CSS — `@layer` (cascade layers)
Modern cascade management.
- *Details:* [css-todos.md §5](src/SuperRender.Core/css-todos.md)

### CSS — Logical properties
`margin-inline-*`, `padding-block-*`, `inline-size`, etc.
- *Details:* [css-todos.md §31](src/SuperRender.Core/css-todos.md)

### CSS — Writing modes
`writing-mode`, `direction`, `unicode-bidi`, `text-orientation`.
- *Details:* [css-todos.md §32](src/SuperRender.Core/css-todos.md)

### CSS — Masking and clipping
`clip-path`, `mask-image`, etc.
- *Details:* [css-todos.md §24](src/SuperRender.Core/css-todos.md)

### CSS — `@font-face` and variable fonts
Custom web font loading.
- *Details:* [css-todos.md §14](src/SuperRender.Core/css-todos.md)

### CSS — Pseudo-classes (input state)
`:enabled`, `:disabled`, `:checked`, `:required`, `:valid`/`:invalid`, etc.
- *Details:* [css-todos.md §1](src/SuperRender.Core/css-todos.md)

### CSS — Advanced color functions
`oklch()`, `oklab()`, `color-mix()`, `light-dark()`, system colors.
- *Details:* [css-todos.md §4](src/SuperRender.Core/css-todos.md)

### CSS — Containment
`contain`, `content-visibility` for rendering performance.
- *Details:* [css-todos.md §26](src/SuperRender.Core/css-todos.md)

### HTML — Shadow DOM
`attachShadow()`, `ShadowRoot`, slot-based distribution, `:host`.
- *Details:* [html-todos.md §3.7](src/SuperRender.Core/html-todos.md)

### HTML — Web Components
Custom elements, lifecycle callbacks, `<template>`, `<slot>`.
- *Details:* [html-todos.md §8](src/SuperRender.Core/html-todos.md)

### HTML — MutationObserver
Observe DOM changes programmatically.
- *Details:* [html-todos.md §3.6](src/SuperRender.Core/html-todos.md)

### HTML — Embedded content
`<canvas>`, `<svg>`, `<iframe>`, `<video>`, `<audio>`.
- *Details:* [html-todos.md §4.7](src/SuperRender.Core/html-todos.md)

### HTML — Global attribute behaviors
`hidden`, `tabindex`, `title` (tooltip), `contenteditable`, `draggable`, `popover`, `inert`.
- *Details:* [html-todos.md §6](src/SuperRender.Core/html-todos.md)

### HTML — Event handler attributes
`onclick`, `onload`, `onerror`, etc. as HTML attributes.
- *Details:* [html-todos.md §6](src/SuperRender.Core/html-todos.md)

### HTML — Range and Selection APIs
`Range`, `Selection`, `document.createRange()`, `window.getSelection()`.
- *Details:* [html-todos.md §3.8](src/SuperRender.Core/html-todos.md)

### HTML — Intersection/Resize observers
`IntersectionObserver`, `ResizeObserver`.
- *Details:* [html-todos.md §9.4](src/SuperRender.Core/html-todos.md)

### EcmaScript — BigInt
`BigInt` type and `123n` literals with operator support.
- *Details:* [es-2025-todos.md — BigInt](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — WeakRef / FinalizationRegistry
Weak references and GC finalization hooks.
- *Details:* [es-2025-todos.md — WeakRef](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — `structuredClone()`
Deep-clone for all value types.
- *Details:* [es-2025-todos.md — Structured Clone](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — `String.prototype.isWellFormed()` / `toWellFormed()`
Unicode well-formedness. Straightforward.
- *Details:* [es-2025-todos.md — String](src/SuperRender.EcmaScript/es-2025-todos.md)

---

## P4 — Aspirational

Experimental, long-term, and research features.

### CSS — Scroll snap
`scroll-snap-type`, `scroll-snap-align`, `scroll-padding/margin`.
- *Details:* [css-todos.md §33](src/SuperRender.Core/css-todos.md)

### CSS — Grid level 2/3
Subgrid, masonry layout.
- *Details:* [css-todos.md §10](src/SuperRender.Core/css-todos.md)

### CSS — Scroll-driven animations
`animation-timeline` tied to scroll progress.
- *Details:* [css-todos.md §21](src/SuperRender.Core/css-todos.md)

### CSS — `@scope`
Scoped style boundaries.
- *Details:* [css-todos.md §5](src/SuperRender.Core/css-todos.md)

### CSS — `@property`
Custom property type registration for animation.
- *Details:* [css-todos.md §3](src/SuperRender.Core/css-todos.md)

### EcmaScript — Intl (Internationalization)
Full `Intl.*` API surface (Collator, DateTimeFormat, NumberFormat, Segmenter, etc.).
- *Details:* [es-2025-todos.md — Intl](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — Temporal
Modern date/time API (`Temporal.PlainDate`, `ZonedDateTime`, etc.).
- *Details:* [es-2025-todos.md — Temporal](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — Decorators
`@decorator` syntax for classes and members.
- *Details:* [es-2025-todos.md — Decorators](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — SharedArrayBuffer / Atomics
Shared memory and atomic operations for threading.
- *Details:* [es-2025-todos.md — SharedArrayBuffer](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — ShadowRealm
Isolated global environments.
- *Details:* [es-2025-todos.md — ShadowRealm](src/SuperRender.EcmaScript/es-2025-todos.md)

### EcmaScript — Pipeline operator, pattern matching
Stage 1–2 proposals, not yet standard.
- *Details:* [es-2025-todos.md — Pipeline/Pattern](src/SuperRender.EcmaScript/es-2025-todos.md)

### Browser — Render tree visualizer
Side panel showing box tree with dimension overlays.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — CSS cascade debugger
Show matched rules, specificity scores, winning/losing properties per element.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — Step-through renderer
Pause after each pipeline stage and inspect intermediate state.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — Paint command inspector
List all paint commands with coordinates; click to highlight on canvas.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — Hot-reload local files
Watch local HTML file for changes, auto-reload on save.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — Built-in Markdown & JSON viewers
Render `.md` files as styled HTML, pretty-print JSON with collapsible tree.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — Plugin API & custom protocol handlers
.NET assembly plugins, `super://` protocol, extension points.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — Incognito mode & content blocker
Private browsing, simple ad/tracker blocking.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — MCP tool bridge
Expose browser state (DOM, screenshots, navigation) as MCP tools for AI-assisted browsing.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

### Browser — Dark mode chrome & theme system
Customizable chrome colors, dark mode tab/address bar.
- *Details:* [browser-todos.md — Brainstorming](src/SuperRender.Browser/browser-todos.md)

---

## Summary

| Priority | Count | Description |
|----------|-------|-------------|
| **P0** | 13 (13 done) | Critical blockers for basic browsing |
| **P1** | 18 | High-priority for real-world site compatibility |
| **P2** | 25 | Medium-priority quality and completeness |
| **P3** | 27 | Low-priority polish and advanced specs |
| **P4** | 20 | Aspirational and experimental |
| **Total** | **103** | |
