# Browser Feature Gaps & Ideas

Audit of SuperRender.Browser against modern browser functionality.
Each section lists what **is** implemented, then what remains.

> Legend: `[S]` = stubbed/partially implemented; `[ ]` = not implemented at all.

*Last updated: 2026-04-12*

---

## 1. Navigation & History

**Implemented:** Back/Forward navigation buttons trigger history navigation, address bar updates. `NavigationHistory` class with URI list and forward/back cursor per tab. `window.history` API (pushState, replaceState, back, forward, go, length, state). `window.location` object (href, protocol, host, hostname, port, pathname, search, hash, origin, assign, replace, reload). Ctrl/Cmd+L focuses and selects address bar. Ctrl/Cmd+[/Left and Ctrl/Cmd+]/Right for back/forward.

- [ ] Fragment navigation — scroll to `#id` on same page, `hashchange` event
- [ ] `popstate` event firing on history navigation
- [ ] `beforeunload` / `unload` events — prompt before leaving page with unsaved changes
- [ ] Navigation progress indicator — loading spinner or progress bar in tab/address bar while page loads

---

## 2. Keyboard Shortcuts

**Implemented:** Platform-aware shortcuts (Cmd on macOS, Ctrl on Windows/Linux). Ctrl/Cmd+T (new tab), Ctrl/Cmd+W (close tab), Ctrl/Cmd+Tab/Shift+Tab (next/previous tab), Ctrl/Cmd+L (focus address bar + select all), Ctrl/Cmd+R/F5 (reload), Ctrl/Cmd+[/Left (back), Ctrl/Cmd+]/Right (forward), F12/Ctrl/Cmd+Shift+I (toggle dev tools), Escape (stop/unfocus). Content area: arrow keys (scroll step), Page Up/Down/Space (scroll page), Home/End (top/bottom).

- [ ] Ctrl/Cmd+1-9 — switch to tab by index
- [ ] Ctrl/Cmd+F — find in page
- [ ] Ctrl/Cmd+Plus / Ctrl/Cmd+Minus / Ctrl/Cmd+0 — zoom in/out/reset
- [ ] Ctrl/Cmd+U — view source
- [ ] F11 — toggle fullscreen

---

## 3. Content Scrolling & Overflow

**Implemented:** Vertical scrolling with `ScrollState` per tab. Mouse wheel scrolling, keyboard scrolling (arrows, Page Up/Down, Home/End, Space), visual scrollbar indicator on right edge. Scroll-to-top on navigation. `overflow: hidden` triggers clip regions in painting.

- [ ] Smooth scrolling — animated interpolation between scroll positions
- [ ] CSS `overflow: scroll | auto` — per-element scroll containers
- [ ] `window.scrollTo()` / `window.scrollBy()` / `Element.scrollIntoView()` JS APIs
- [ ] Scroll event firing — `scroll` event on window and scrollable elements

---

## 4. Page Zoom

No zoom support.

- [ ] Page zoom (Ctrl/Cmd+Plus/Minus) — scale factor applied to layout viewport width, re-layout at new effective width
- [ ] Zoom level indicator in chrome — show current zoom % when not 100%
- [ ] Ctrl/Cmd+0 — reset zoom to 100%
- [ ] Pinch-to-zoom (trackpad gesture support via Silk.NET)
- [ ] Per-site zoom memory — remember zoom level for each origin

---

## 5. Cookies & Storage

**Implemented:** Full cookie jar with `Set-Cookie` parsing (Domain, Path, Expires, Max-Age, Secure, HttpOnly, SameSite). Automatic `Cookie` request header attachment. `document.cookie` JS getter/setter. `localStorage` backed by SQLite (`StorageDatabase`). `sessionStorage` per-tab in memory. `JsStorageWrapper` bridges to JS with `getItem`/`setItem`/`removeItem`/`clear`/`key`/`length`.

- [ ] `storage` event — cross-tab notification for localStorage changes
- [ ] IndexedDB (stub returning `undefined` for feature detection)
- [ ] Cache API / Service Worker cache (stub)

---

## 6. HTTP Caching

**Implemented:** `HttpCache` backed by SQLite (`CacheDatabase`). Checks freshness (max-age, Expires), sends conditional requests (If-None-Match/ETag, If-Modified-Since/Last-Modified), handles 304 Not Modified. `Cache-Control` header parsing (max-age, no-cache, no-store).

- [ ] `must-revalidate`, `public`, `private` Cache-Control directives
- [ ] Content compression — `Accept-Encoding: gzip, deflate, br` request header, automatic decompression

---

## 7. Security Hardening

Only basic CORS `Access-Control-Allow-Origin` is checked.

### CORS Enhancements
- [ ] Preflight requests (`OPTIONS`) for non-simple requests
- [ ] `Access-Control-Allow-Methods`, `Access-Control-Allow-Headers` checking
- [ ] `Access-Control-Allow-Credentials` with `withCredentials`
- [ ] `Access-Control-Expose-Headers` enforcement
- [ ] `Access-Control-Max-Age` preflight caching

### Content Security
- [ ] Mixed content blocking — block HTTP sub-resources on HTTPS pages
- [ ] Content Security Policy (CSP) — `Content-Security-Policy` header parsing and enforcement
- [ ] `X-Content-Type-Options: nosniff` — prevent MIME sniffing
- [ ] `X-Frame-Options` — deny/sameorigin for future `<iframe>` support
- [ ] `Referrer-Policy` header — control `Referer` header in requests
- [ ] Subresource Integrity (SRI) — `integrity` attribute on `<script>` and `<link>`

### UI Security Indicators
- [ ] HTTPS padlock icon in address bar
- [ ] Certificate error interstitial page (self-signed, expired, domain mismatch)
- [ ] HTTP "Not Secure" label in address bar

---

## 8. Event System Integration

**Implemented:** Full EventTarget on all nodes (addEventListener/removeEventListener/dispatchEvent). Event propagation (capture → target → bubble). DomEvent, MouseEvent (clientX/Y, button, modifiers), KeyboardEvent (key, code, modifiers, repeat). Browser dispatches: mousedown, mouseup, click, mousemove, mouseover, mouseout, mouseenter, mouseleave. DOMContentLoaded and load events fire after page load. InteractionStateHelper manages :hover/:focus/:active state. JsEventWrapper bridges all event types to JS.

- [ ] `keydown`, `keyup` events dispatched to content area DOM elements
- [ ] `input`, `change` events (for future form elements)
- [ ] `submit` event (for future `<form>`)
- [ ] `scroll` event
- [ ] `resize` event on window
- [ ] `focus`, `blur` events
- [ ] `hashchange`, `popstate` events (tied to History API)

---

## 9. Timers & Animation Loop

**Implemented:** `TimerScheduler` provides real timer queue drained each frame in render loop. `setTimeout` with actual delays, `setInterval` with recurring scheduling (min 4ms), `requestAnimationFrame` tied to render loop, `cancelAnimationFrame`/`clearTimeout`/`clearInterval`.

- [ ] Microtask queue — `queueMicrotask()`, `Promise.then()` scheduling

---

## 10. Image & Media Support

**Implemented:** `<img>` element — fetch from `src` (HTTP/file/data: URIs), decode (PNG/BMP/baseline JPEG via pure C# decoders), render as `DrawImageCommand`. Image sizing with `width`/`height` attributes, intrinsic dimensions with aspect ratio preservation. Alt text fallback in gray placeholder box. `ImageCache` for decoded images. GPU-accelerated JPEG decode (YCbCr→RGB compute, dequant+IDCT compute).

### Images — remaining
- [ ] `loading="lazy"` — defer off-screen image fetches until near viewport
- [ ] `srcset`/`sizes` — responsive image selection
- [ ] CSS `background-image: url(...)` — fetch and tile/cover/contain as background
- [ ] GIF/WebP/AVIF decoders
- [ ] Inline SVG rendering (depends on SVG path parser + Vulkan path fill)

### Media (lower priority)
- [ ] `<video>` / `<audio>` element stubs — show placeholder, log unsupported
- [ ] `<canvas>` 2D context stub — basic fillRect/strokeRect/fillText for simple use cases

---

## 11. Form Elements & Input

No form or input element support.

- [ ] `<input type="text">` — editable text field with focus, cursor, selection
- [ ] `<input type="password">` — masked text display
- [ ] `<input type="submit">` / `<button>` — clickable, fires `submit` on parent form
- [ ] `<input type="checkbox">` / `<input type="radio">` — toggle state
- [ ] `<input type="hidden">` — no display, included in form data
- [ ] `<textarea>` — multi-line text editing
- [ ] `<select>` / `<option>` — dropdown menu
- [ ] `<form>` submission — collect name-value pairs, POST
- [ ] `<label>` `for` attribute — click label to focus associated input
- [ ] Tab-order focus management
- [ ] Form validation
- [ ] Autofocus attribute
- [ ] Input `value`, `checked` JS properties

---

## 12. Find-in-Page

No text search capability.

- [ ] Ctrl/Cmd+F — open find bar
- [ ] Incremental text search — highlight all matches, scroll to first
- [ ] Next/Previous match navigation
- [ ] Match count display ("3 of 17")
- [ ] Case-sensitive toggle
- [ ] Escape to close find bar

---

## 13. Developer Tools

**Implemented:** Separate DevTools window with Console tab. Opens via F12, Cmd/Ctrl+Shift+I, or right-click "Developer Tools". Renders in own Vulkan window driven from main render loop. JavaScript console captures `console.log/warn/error` from page scripts. Interactive JS execution with input history (Up/Down), cursor editing. Error display with line/column from JsErrorBase. Clear button. Scrollable log area with auto-scroll. Lazy JS engine init. Per-tab DevTools (each Tab owns its own DevToolsWindow).

### Not Yet Implemented
- [ ] DOM inspector — tree view of document, click-to-select element, show computed styles
- [ ] Network panel — log all HTTP requests with URL, method, status, size, timing
- [ ] View Source with syntax highlighting
- [ ] Element hover highlight — outline the hovered element in the page
- [ ] CSS property editor — modify styles in-place and see live results
- [ ] Performance timeline — frame times, layout/paint durations
- [ ] Console autocomplete — suggest properties of objects

---

## 14. Bookmarks & Session

No bookmark or session persistence.

- [ ] Bookmark current page — store title + URL
- [ ] Bookmarks bar (below address bar, optional)
- [ ] Bookmarks menu / manager page
- [ ] Session restore — save open tabs on close, offer to restore on next launch
- [ ] Recently closed tabs — Ctrl/Cmd+Shift+T to reopen last closed tab
- [ ] Startup page preference — blank, welcome, or last session

---

## 15. Downloads

No download handling.

- [ ] Content-Disposition header detection — trigger download instead of navigation
- [ ] Download progress UI — notification bar at bottom of window
- [ ] Save-as dialog (or auto-save to Downloads folder)
- [ ] `<a download="filename">` attribute support
- [ ] Blob URL support (`URL.createObjectURL()`)

---

## 16. Tab Enhancements

Basic tab create/switch/close works but lacks polish.

- [ ] Tab reordering — drag tabs to rearrange
- [ ] Duplicate tab — right-click tab context menu option
- [ ] Pin tab — shrink to icon width, lock in leftmost position
- [ ] Close other / close tabs to the right — right-click tab context menu
- [ ] Tab hover preview — tooltip showing page title and URL
- [ ] Middle-click link to open in new tab
- [ ] Ctrl/Cmd+click link to open in new tab
- [ ] Loading spinner per tab
- [ ] Close button on hover (not just active tab)
- [ ] Tab overflow — scroll or collapse tabs when too many to fit

---

## 17. Link Navigation

**Implemented:** Click on `<a href>` navigates the tab (hit-test via LayoutBoxHitTester, walk to `<a>` ancestor). `target="_blank"` opens in new tab. URL resolution against current URI. InteractionStateHelper manages :hover state on link elements.

- [ ] Hover cursor change — show pointer cursor over links (requires Silk.NET cursor API)
- [ ] Status bar on link hover — show destination URL at bottom of window
- [ ] `rel="noopener"` / `rel="noreferrer"` handling
- [ ] `javascript:` URI scheme handling
- [ ] `mailto:` / `tel:` URI scheme — attempt OS handler

---

## 18. `<a>` and Link Styling

**Implemented:** Default link styling in user-agent stylesheet (`a { color: blue; text-decoration: underline }`). `:hover`, `:active`, `:focus` pseudo-class state tracking via InteractionStateHelper. `:visited` pseudo-class in selector matcher.

- [ ] Visited link styling — `a:visited { color: purple }` (requires visited URL set)
- [ ] `cursor: pointer` CSS property applied to links via Silk.NET cursor API

---

## 19. Printing & Export

No print or export capability.

- [ ] Ctrl/Cmd+P — print dialog (or save to PDF)
- [ ] Screenshot to PNG — capture current viewport as image file
- [ ] Save page as HTML
- [ ] `@media print` stylesheet support

---

## 20. Accessibility

No accessibility support.

- [ ] Focus ring rendering — visible outline on focused elements
- [ ] Keyboard-only navigation — Tab/Shift+Tab through focusable elements
- [ ] Skip-to-content landmark
- [ ] ARIA role recognition
- [ ] `alt` text for images — already renders but not announced to assistive tech
- [ ] High contrast mode
- [ ] Font size scaling — respect OS text size preferences
- [ ] Screen reader text extraction API

---

## 21. Fetch & XMLHttpRequest

**Implemented:** `fetch()` API — GET/POST with Promises, returns JsResponseWrapper with `status`/`statusText`/`url`/`ok`/`headers`/`.text()`/`.json()`. Async HTTP via Task.Run + main-thread queue marshaling. CORS origin checking via SecurityPolicy.

- [ ] `XMLHttpRequest` — legacy API
- [ ] `AbortController` / `AbortSignal` — cancel in-flight requests
- [ ] CORS preflight for cross-origin requests
- [ ] `FormData` object for POST body construction
- [ ] Streaming response bodies (ReadableStream)
- [ ] Request/Response spec-compliant types

---

## 22. Error Pages & Special Pages

Only a generic navigation error page exists. Welcome page via `sr://` protocol.

- [ ] DNS resolution failure page — distinct from HTTP errors
- [ ] Connection timeout page
- [ ] SSL/TLS certificate error page with "proceed anyway" option
- [ ] HTTP 4xx/5xx error pages — show status code and reason
- [ ] `about:settings` — settings/preferences page
- [ ] `about:history` — browsable history list
- [ ] `about:bookmarks` — bookmark manager
- [ ] Offline / no network page

---

## 23. Address Bar Enhancements

**Implemented:** Text input with cursor movement, click-to-cursor positioning, click-and-drag text selection, right-click context menu (Cut/Copy/Paste/Select All). Supports http/https/file/sr/about schemes, bare domain names, absolute file paths.

- [ ] URL autocomplete — suggest from history and bookmarks as user types
- [ ] Search engine fallback — if input is not a URL, search via DuckDuckGo/Google
- [ ] URL highlighting — dim scheme, highlight domain in displayed URL
- [ ] Favicon display to the left of the URL
- [ ] SSL padlock icon in address bar

---

## 24. Multi-Window Support

Only a single window is supported.

- [ ] Ctrl/Cmd+N — open new window
- [ ] Drag tab out of tab bar to create new window
- [ ] Drag tab between windows to move it
- [ ] `window.open()` JS API

---

## 25. Notifications & Permissions

No notification or permission system.

- [ ] `Notification` API stub — request permission, show native notification
- [ ] `navigator.permissions` API stub
- [ ] Permission prompt UI
- [ ] Geolocation API stub (deny by default)
- [ ] Clipboard API (`navigator.clipboard.readText()` / `writeText()`)

---

## Brainstorming: New & Experimental Features

Ideas beyond standard browser functionality, unique to SuperRenderer.

### Rendering Engine Showcase
- [ ] **Render tree visualizer** — side panel showing the box tree with dimensions, margins, padding drawn as colored overlays
- [ ] **CSS cascade debugger** — for any element, show which rules matched, specificity scores, and which properties won/lost
- [ ] **Paint command inspector** — list all FillRect/DrawText/DrawImage commands with coordinates
- [ ] **Layout performance HUD** — overlay showing frame time, layout time, paint command count, vertex count

### Educational Tools
- [ ] **Step-through renderer** — pause after each pipeline stage and inspect intermediate state
- [ ] **Side-by-side comparison** — split view: SuperRenderer output vs reference screenshot
- [ ] **Spec compliance dashboard** — page showing which CSS/HTML/JS features are supported

### Developer Experience
- [ ] **Hot-reload local files** — watch a local HTML file for changes, auto-reload tab on save
- [ ] **Built-in Markdown renderer** — navigate to `.md` files and render as styled HTML
- [ ] **JSON viewer** — pretty-print JSON responses with collapsible tree
- [ ] **RSS/Atom feed reader** — detect and render feeds

### Theming & Customization
- [ ] **Browser theme system** — customizable chrome colors
- [ ] **Dark mode chrome** — dark background for tab bar and address bar
- [ ] **User stylesheet injection** — load a user CSS file applied to all pages
- [ ] **Custom new-tab page** — configurable start page

### Performance & Profiling
- [ ] **GPU memory dashboard** — show texture atlas usage, vertex buffer sizes, pipeline statistics
- [ ] **Network waterfall chart** — visualize resource load timing
- [ ] **Render pipeline profiler** — breakdown of time spent in parse, style, layout, paint, GPU submit phases

### Privacy & Security
- [ ] **Incognito mode** — tab or window with no persistent cookies, storage, or history
- [ ] **Content blocker** — block requests matching patterns
- [ ] **Request log** — show all outgoing HTTP requests with headers
- [ ] **Cookie manager** — view, edit, delete cookies per site

### Interop & Extension Points
- [ ] **C# script console** — evaluate C# expressions against the running browser state
- [ ] **Plugin API** — load .NET assemblies that can register custom elements, paint commands, or page actions
- [ ] **Custom protocol handlers** — register protocols for built-in pages, allow plugins to add more
- [ ] **MCP tool bridge** — expose browser state as MCP tools for AI-assisted browsing
