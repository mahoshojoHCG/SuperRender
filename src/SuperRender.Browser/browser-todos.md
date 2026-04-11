# Browser Feature Gaps & Ideas

Audit of SuperRender.Browser against modern browser functionality.
Features already implemented are **not** listed here. See CLAUDE.md for the current implementation summary.

> Legend: `[S]` = stubbed/partially implemented; `[ ]` = not implemented at all.

---

## 1. Navigation & History

Back/Forward buttons are wired to a per-tab history stack. `window.history` and `window.location` APIs are not yet exposed to JS.

- [x] Back/Forward navigation — buttons trigger history navigation, address bar updates
- [x] History stack — `NavigationHistory` class with URI list and forward/back cursor per tab
- [ ] `window.history` API — `pushState()`, `replaceState()`, `back()`, `forward()`, `go(n)`, `state`, `popstate` event
- [ ] `window.location` object — `href`, `origin`, `protocol`, `host`, `hostname`, `port`, `pathname`, `search`, `hash`, `assign()`, `replace()`, `reload()`
- [ ] Fragment navigation — scroll to `#id` on same page, `hashchange` event
- [ ] `beforeunload` / `unload` events — prompt before leaving page with unsaved changes
- [ ] Navigation progress indicator — loading spinner or progress bar in tab/address bar while page loads
- [ ] Ctrl+L / Cmd+L keyboard shortcut to focus address bar

---

## 2. Keyboard Shortcuts

Platform-aware shortcuts (Cmd on macOS, Ctrl on Windows/Linux) for core browser actions. Content area scrolling keys are also implemented.

- [x] Ctrl/Cmd+T — new tab
- [x] Ctrl/Cmd+W — close tab
- [x] Ctrl/Cmd+Tab / Ctrl/Cmd+Shift+Tab — next/previous tab
- [ ] Ctrl/Cmd+1-9 — switch to tab by index
- [x] Ctrl/Cmd+L — focus address bar, select all text
- [x] Ctrl/Cmd+R / F5 — reload page
- [ ] Alt+Left / Alt+Right — back/forward
- [ ] Ctrl/Cmd+F — find in page
- [ ] Ctrl/Cmd+Plus / Ctrl/Cmd+Minus / Ctrl/Cmd+0 — zoom in/out/reset
- [ ] Ctrl/Cmd+Shift+I — toggle dev tools
- [ ] Ctrl/Cmd+U — view source
- [x] Escape — stop loading / unfocus address bar
- [ ] F11 — toggle fullscreen

---

## 3. Content Scrolling & Overflow

Vertical scrolling is implemented with `ScrollState` per tab. Mouse wheel, keyboard scrolling, and a visual scrollbar indicator are supported.

- [x] Vertical scroll offset per tab — `ScrollState` tracks `scrollY`, applied when rendering content paint list
- [x] Mouse wheel scrolling — map scroll delta to `scrollY` change
- [x] Scroll bar widget — visual indicator on right edge showing scroll position and viewport proportion
- [x] Keyboard scrolling — arrow keys, Page Up/Down, Home/End, Space when content is focused
- [ ] Smooth scrolling — animated interpolation between scroll positions
- [x] Scroll-to-top on navigation — reset scroll position when loading a new page
- [ ] CSS `overflow: hidden | scroll | auto` — per-element scroll containers
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

No persistent or session storage of any kind.

### Cookies
- [ ] Cookie jar — in-memory store keyed by domain+path+name
- [ ] `Set-Cookie` response header parsing — name, value, Domain, Path, Expires, Max-Age, Secure, HttpOnly, SameSite
- [ ] Automatic `Cookie` request header attachment based on domain/path/secure match
- [ ] Cookie expiry enforcement (session vs persistent)
- [ ] HttpOnly enforcement (block JS `document.cookie` access)
- [ ] SameSite enforcement (Strict / Lax / None)
- [ ] Secure flag enforcement (only send over HTTPS)
- [ ] `document.cookie` JS getter/setter

### Web Storage
- [ ] `localStorage` — per-origin key-value store, persisted to disk
- [ ] `sessionStorage` — per-tab key-value store, lost on tab close
- [ ] `storage` event — cross-tab notification for localStorage changes

### Other
- [ ] IndexedDB (stub returning `undefined` for feature detection)
- [ ] Cache API / Service Worker cache (stub)

---

## 6. HTTP Caching

Every navigation re-fetches all resources from the network.

- [ ] In-memory response cache keyed by URL
- [ ] `Cache-Control` header parsing — `max-age`, `no-cache`, `no-store`, `must-revalidate`, `public`, `private`
- [ ] `ETag` / `If-None-Match` conditional requests — 304 Not Modified handling
- [ ] `Last-Modified` / `If-Modified-Since` conditional requests
- [ ] `Expires` header support (fallback when no Cache-Control)
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

Core DOM event system is implemented with `addEventListener`/`removeEventListener`/`dispatchEvent` on all nodes. Capture/target/bubble propagation works. Mouse events (`mousedown`, `mouseup`, `click`) are dispatched from InputHandler. `DOMContentLoaded` and `load` fire after page load.

- [x] `EventTarget` base — `addEventListener()`, `removeEventListener()`, `dispatchEvent()`
- [x] `Event` object — `type`, `target`, `currentTarget`, `bubbles`, `cancelable`, `preventDefault()`, `stopPropagation()`
- [x] Event propagation — capture phase, target phase, bubble phase
- [x] `click` event — fire on mouse up over same element as mouse down
- [x] `mousedown`, `mouseup` — dispatched from InputHandler content area
- [ ] `mousemove`, `mouseover`, `mouseout`, `mouseenter`, `mouseleave`
- [ ] `keydown`, `keyup`, `keypress` (content area, when not in address bar)
- [ ] `input`, `change` events (for future form elements)
- [ ] `submit` event (for future `<form>`)
- [x] `DOMContentLoaded` event on document
- [x] `load` event on window and elements
- [ ] `scroll` event
- [ ] `resize` event on window
- [ ] `focus`, `blur` events
- [ ] `hashchange`, `popstate` events (tied to History API)

---

## 9. Timers & Animation Loop

`TimerScheduler` provides a real timer queue drained each frame in the render loop. `setTimeout`, `setInterval`, and `requestAnimationFrame` work with actual delays.

- [x] Timer queue — schedule callbacks with real delays, drain during frame loop
- [x] `setTimeout` with actual delay — enqueue callback + delay, fire when elapsed
- [x] `setInterval` — repeating timer, returns ID for `clearInterval`
- [x] `requestAnimationFrame` — per-frame callback tied to render loop
- [x] `cancelAnimationFrame`
- [ ] Microtask queue — `queueMicrotask()`, `Promise.then()` scheduling

---

## 10. Image & Media Support

No image or media rendering capability.

### Images
- [ ] `<img>` element — fetch image from `src`, decode (PNG/JPEG/GIF/WebP), render as textured quad
- [ ] Image decoding pipeline — HTTP fetch → byte decode → upload to GPU texture
- [ ] Image sizing — `width`/`height` attributes, CSS `width`/`height`, intrinsic aspect ratio
- [ ] `alt` text fallback — display alt text when image fails to load
- [ ] `loading="lazy"` — defer off-screen image fetches until near viewport
- [ ] CSS `background-image: url(...)` — fetch and tile/cover/contain as background
- [ ] Inline SVG rendering (depends on SVG path parser + Vulkan path fill)

### Media (lower priority)
- [ ] `<video>` / `<audio>` element stubs — show placeholder, log unsupported
- [ ] `<canvas>` 2D context stub — basic fillRect/strokeRect/fillText for simple use cases

---

## 11. Form Elements & Input

No form or input element support.

- [ ] `<input type="text">` — editable text field with focus, cursor, selection (reuse address bar editing logic)
- [ ] `<input type="password">` — masked text display
- [ ] `<input type="submit">` / `<button>` — clickable, fires `submit` on parent form
- [ ] `<input type="checkbox">` / `<input type="radio">` — toggle state, custom quad rendering
- [ ] `<input type="hidden">` — no display, included in form data
- [ ] `<textarea>` — multi-line text editing
- [ ] `<select>` / `<option>` — dropdown menu with overlay rendering
- [ ] `<form>` submission — collect name-value pairs, `application/x-www-form-urlencoded` POST
- [ ] `<label>` `for` attribute — click label to focus associated input
- [ ] Tab-order focus management — `tabindex`, natural DOM order focus cycling
- [ ] Form validation — `required`, `pattern`, `min`/`max`/`minlength`/`maxlength`
- [ ] Autofocus attribute
- [ ] Input `value`, `checked` JS properties

---

## 12. Find-in-Page

No text search capability.

- [ ] Ctrl/Cmd+F — open find bar (overlay at top of content area)
- [ ] Incremental text search — highlight all matches in content, scroll to first match
- [ ] Next/Previous match navigation (Enter / Shift+Enter or arrow buttons)
- [ ] Match count display ("3 of 17")
- [ ] Case-sensitive toggle
- [ ] Escape to close find bar
- [ ] `window.find()` JS API (non-standard but widely supported)

---

## 13. Developer Tools

No developer tooling.

### Essential
- [ ] JavaScript console panel — display `console.log/warn/error` output, accept input, tied to active tab's JsEngine
- [ ] DOM inspector — tree view of document, click-to-select element, show computed styles
- [ ] Network panel — log all HTTP requests with URL, method, status, size, timing
- [ ] View Source with syntax highlighting — colorize HTML tags, attributes, CSS, JS

### Nice-to-have
- [ ] Element hover highlight — outline the hovered element in the page when inspecting
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
- [ ] Middle-click link to open in new tab (depends on `<a>` click handling)
- [ ] Ctrl/Cmd+click link to open in new tab
- [ ] Loading spinner per tab — animated indicator while `IsLoading` is true
- [ ] Close button on hover (not just active tab)
- [ ] Tab overflow — scroll or collapse tabs when too many to fit

---

## 17. Link Navigation

Click on `<a href>` navigates the tab. `target="_blank"` opens in a new tab. Hit-testing uses `LayoutBoxHitTester` to find the deepest layout box at a coordinate and walks up to find the enclosing `<a>` element.

- [x] Click on `<a>` to navigate — hit-test against `<a>` elements in layout, trigger navigation to `href`
- [ ] Hover cursor change — show pointer cursor over links (requires Silk.NET cursor API)
- [ ] Status bar on link hover — show destination URL at bottom of window
- [x] `target="_blank"` — open link in new tab
- [ ] `rel="noopener"` / `rel="noreferrer"` handling
- [ ] `javascript:` URI scheme handling (execute JS)
- [ ] `mailto:` / `tel:` URI scheme — attempt OS handler

---

## 18. `<a>` and Link Styling

Links are not visually distinguished from regular text.

- [ ] Default link styling in user-agent stylesheet — `a { color: blue; text-decoration: underline; }`
- [ ] Visited link styling — `a:visited { color: purple; }` (requires visited URL set)
- [ ] `:hover` styling — change color on mouse over (depends on event/hover tracking)
- [ ] `:active` styling — change color on mouse down
- [ ] `cursor: pointer` CSS property — change cursor shape over links

---

## 19. Printing & Export

No print or export capability.

- [ ] Ctrl/Cmd+P — print dialog (or save to PDF)
- [ ] Screenshot to PNG — capture current viewport as image file
- [ ] Save page as HTML — save document source + linked resources to disk
- [ ] `@media print` stylesheet support — alternate styles for print layout

---

## 20. Accessibility

No accessibility support.

- [ ] Focus ring rendering — visible outline on focused elements
- [ ] Keyboard-only navigation — Tab/Shift+Tab through focusable elements
- [ ] Skip-to-content landmark
- [ ] ARIA role recognition — landmark, widget, live region roles on elements
- [ ] `alt` text for images (when images are supported)
- [ ] High contrast mode — respect OS accessibility settings
- [ ] Font size scaling — respect OS text size preferences
- [ ] Screen reader text extraction API (platform-specific)

---

## 21. Fetch & XMLHttpRequest

JS cannot make network requests.

- [ ] `fetch()` API — basic GET/POST with Promises, return Response object with `.text()`, `.json()`
- [ ] `XMLHttpRequest` — legacy API, synchronous and async modes
- [ ] Request/Response objects — headers, status, body, URL
- [ ] `AbortController` / `AbortSignal` — cancel in-flight requests
- [ ] CORS preflight for `fetch()` cross-origin requests
- [ ] `FormData` object for POST body construction
- [ ] Streaming response bodies (ReadableStream)

---

## 22. Error Pages & Special Pages

Only a generic navigation error page exists.

- [ ] DNS resolution failure page — distinct from HTTP errors
- [ ] Connection timeout page
- [ ] SSL/TLS certificate error page with "proceed anyway" option
- [ ] HTTP 4xx/5xx error pages — show status code and reason
- [ ] `about:blank` — works, but no other `about:` pages
- [ ] `about:settings` — settings/preferences page
- [ ] `about:history` — browsable history list
- [ ] `about:bookmarks` — bookmark manager
- [ ] Offline / no network page

---

## 23. Address Bar Enhancements

The address bar handles text input and basic cursor movement.

- [ ] URL autocomplete — suggest from history and bookmarks as user types
- [ ] Search engine fallback — if input is not a URL, search via DuckDuckGo/Google
- [ ] URL highlighting — dim scheme, highlight domain in displayed URL
- [ ] Select-all on focus — single click selects entire URL (like Chrome/Firefox)
- [ ] Drag-to-select in address bar — partial text selection with mouse
- [ ] Favicon display to the left of the URL (when favicon loading is implemented)
- [ ] SSL padlock icon in address bar (see Security section)

---

## 24. Multi-Window Support

Only a single window is supported.

- [ ] Ctrl/Cmd+N — open new window
- [ ] Drag tab out of tab bar to create new window
- [ ] Drag tab between windows to move it
- [ ] `window.open()` JS API — open URL in new window/tab

---

## 25. Notifications & Permissions

No notification or permission system.

- [ ] `Notification` API stub — request permission, show native notification
- [ ] `navigator.permissions` API stub
- [ ] Permission prompt UI — overlay asking user to Allow/Deny
- [ ] Geolocation API stub (deny by default)
- [ ] Clipboard API (`navigator.clipboard.readText()` / `writeText()`)

---

## Brainstorming: New & Experimental Features

Ideas beyond standard browser functionality, unique to SuperRenderer.

### Rendering Engine Showcase
- [ ] **Render tree visualizer** — side panel showing the box tree with dimensions, margins, padding drawn as colored overlays on the page
- [ ] **CSS cascade debugger** — for any element, show which rules matched, specificity scores, and which properties won/lost
- [ ] **Paint command inspector** — list all FillRect/DrawText/StrokeRect commands with coordinates; click to highlight on canvas
- [ ] **Layout performance HUD** — overlay showing frame time, layout time, paint command count, vertex count

### Educational Tools
- [ ] **Step-through renderer** — pause after each pipeline stage (parse → DOM → style → layout → paint) and inspect intermediate state
- [ ] **Side-by-side comparison** — split view: SuperRenderer output vs reference screenshot from another browser
- [ ] **Spec compliance dashboard** — page showing which CSS/HTML/JS features are supported with live test results

### Developer Experience
- [ ] **Hot-reload local files** — watch a local HTML file for changes, auto-reload tab on save
- [ ] **Built-in Markdown renderer** — navigate to `.md` files and render as styled HTML
- [ ] **JSON viewer** — pretty-print JSON responses with collapsible tree
- [ ] **RSS/Atom feed reader** — detect and render feeds as readable article lists

### Theming & Customization
- [ ] **Browser theme system** — customizable chrome colors (tab bar, address bar, buttons)
- [ ] **Dark mode chrome** — dark background for tab bar and address bar
- [ ] **User stylesheet injection** — load a user CSS file applied to all pages
- [ ] **Custom new-tab page** — configurable start page with bookmarks grid or custom HTML

### Performance & Profiling
- [ ] **GPU memory dashboard** — show texture atlas usage, vertex buffer sizes, pipeline statistics
- [ ] **Network waterfall chart** — visualize resource load timing like Chrome's Network panel
- [ ] **Render pipeline profiler** — breakdown of time spent in parse, style, layout, paint, GPU submit phases

### Privacy & Security
- [ ] **Incognito mode** — tab or window with no persistent cookies, storage, or history
- [ ] **Content blocker** — block requests matching patterns (simple ad/tracker blocking)
- [ ] **Request log** — show all outgoing HTTP requests with headers for transparency
- [ ] **Cookie manager** — view, edit, delete cookies per site

### Interop & Extension Points
- [ ] **C# script console** — evaluate C# expressions against the running browser state (DOM, layout, paint list)
- [ ] **Plugin API** — load .NET assemblies that can register custom elements, paint commands, or page actions
- [ ] **Custom protocol handlers** — register `super://` protocol for built-in pages, allow plugins to add more
- [ ] **MCP tool bridge** — expose browser state (DOM, screenshots, navigation) as MCP tools for AI-assisted browsing
