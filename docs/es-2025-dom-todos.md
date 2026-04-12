# ECMAScript DOM Bindings — Unimplemented Features

Tracks missing DOM / Web API features relative to the WHATWG DOM Living Standard and related specs.
Each section lists what **is** implemented, then what remains.

*Last updated: 2026-04-12*

---

## 1. Node Interface

**Implemented:** `nodeType`, `nodeName`, `parentNode`, `parentElement`, `firstChild`, `lastChild`, `nextSibling`, `previousSibling`, `childNodes`, `textContent`, `appendChild()`, `removeChild()`, `insertBefore()`, `hasChildNodes()`, `replaceChild()`, `cloneNode(deep)`, `contains()`, `addEventListener()`, `removeEventListener()`, `dispatchEvent()`

**Partially implemented:**
- `nodeValue` — exposed on TextNode (via `JsTextNodeWrapper`), not on other node types

**TODO:**
- [ ] `nodeValue` property on all node types (currently only TextNode)
- [ ] `ownerDocument` property (backing exists in C# but not exposed to JS)
- [ ] `isConnected` property
- [ ] `getRootNode(options)` method
- [ ] `normalize()` method — merge adjacent text nodes
- [ ] `compareDocumentPosition(other)` method
- [ ] `isSameNode(other)` / `isEqualNode(other)` methods
- [ ] `lookupPrefix()` / `lookupNamespaceURI()` / `isDefaultNamespace()` — namespace support

---

## 2. Element Interface

**Implemented:** `tagName`, `id`, `className`, `classList` (add/remove/toggle/contains), `innerText`, `innerHTML` (get/set), `children`, `style`, `dataset`, `firstElementChild`, `lastElementChild`, `childElementCount`, `getAttribute()`, `setAttribute()`, `removeAttribute()`, `hasAttribute()`, `toggleAttribute()`, `querySelector()`, `querySelectorAll()`, `matches()`, `closest()`, `after()`, `before()`, `remove()`

**TODO:**
- [ ] `attributes` property (NamedNodeMap)
- [ ] `outerHTML` property (read/write)
- [ ] `slot` property
- [ ] `classList` — full DOMTokenList (item, length, value, replace, supports, entries/keys/values/forEach)
- [ ] `getElementsByTagName(name)` method (on Element, not just Document)
- [ ] `getElementsByClassName(name)` method (on Element, not just Document)
- [ ] `insertAdjacentHTML(position, text)` method
- [ ] `insertAdjacentElement(position, element)` method
- [ ] `insertAdjacentText(position, text)` method
- [ ] `replaceWith(...nodes)` method
- [ ] `prepend(...nodes)` / `append(...nodes)` / `replaceChildren(...nodes)` methods
- [ ] `getAttributeNames()` method
- [ ] `hasAttributes()` method
- [ ] `innerHTML` getter — proper HTML escaping for attribute values and text content

### Layout-Dependent Properties (require renderer integration)
- [ ] `clientWidth` / `clientHeight` — content + padding
- [ ] `clientTop` / `clientLeft` — border sizes
- [ ] `offsetWidth` / `offsetHeight` — content + padding + border
- [ ] `offsetTop` / `offsetLeft` / `offsetParent`
- [ ] `scrollWidth` / `scrollHeight` / `scrollTop` / `scrollLeft`
- [ ] `getBoundingClientRect()` → DOMRect
- [ ] `getClientRects()` → DOMRectList
- [ ] `scrollIntoView(options)` method
- [ ] `scroll()` / `scrollTo()` / `scrollBy()` methods

### Interaction
- [ ] `focus()` / `blur()` methods
- [ ] `click()` method
- [ ] `setPointerCapture()` / `releasePointerCapture()` / `hasPointerCapture()`

---

## 3. Document Interface

**Implemented:** `documentElement`, `body`, `head`, `title`, `cookie` (get/set, when configured via DomBridge), `createElement()`, `createTextNode()`, `getElementById()`, `getElementsByTagName()`, `getElementsByClassName()`, `querySelector()`, `querySelectorAll()`

**TODO:**
- [ ] `createComment(data)` method
- [ ] `createDocumentFragment()` method
- [ ] `createAttribute(name)` method
- [ ] `createElementNS(namespace, qualifiedName)` method
- [ ] `importNode(node, deep)` method
- [ ] `adoptNode(node)` method
- [ ] `createRange()` method
- [ ] `createEvent(type)` method (legacy but widely used)
- [ ] `createTreeWalker()` / `createNodeIterator()` methods
- [ ] `elementFromPoint(x, y)` method
- [ ] `elementsFromPoint(x, y)` method
- [ ] `getSelection()` method
- [ ] `readyState` property (`loading` / `interactive` / `complete`)
- [ ] `activeElement` property
- [ ] `characterSet` / `charset` property
- [ ] `contentType` property
- [ ] `doctype` property
- [ ] `documentURI` / `URL` property
- [ ] `domain` property
- [ ] `lastModified` property
- [ ] `location` property (Location object on document)
- [ ] `referrer` property
- [ ] `hidden` / `visibilityState` properties
- [ ] `defaultView` property (→ window)
- [ ] `forms` / `images` / `links` / `scripts` collections

---

## 4. Window Interface

**Implemented:** `document`, `innerWidth`, `innerHeight`, `devicePixelRatio`, `console`, `setTimeout()` (real async via TimerScheduler), `clearTimeout()`, `setInterval()` (real recurring via TimerScheduler), `clearInterval()`, `requestAnimationFrame()`, `cancelAnimationFrame()`, `alert()`, `location` (full Location object), `history` (full History object), `localStorage` (full Storage API), `sessionStorage` (full Storage API), `fetch()` (returns Promise with Response)

**TODO:**

### Timers & Animation
- [ ] `queueMicrotask(callback)` method

### Navigation & Location
- [ ] `navigator` property — Navigator object (userAgent, language, platform, etc.)

### Layout & Display
- [ ] `screen` property — Screen object (width, height, availWidth, availHeight, colorDepth)
- [ ] `getComputedStyle(element, pseudoElement)` method
- [ ] `matchMedia(query)` method → MediaQueryList
- [ ] `scrollX` / `scrollY` / `pageXOffset` / `pageYOffset` properties
- [ ] `scrollTo()` / `scrollBy()` / `scroll()` methods
- [ ] `open()` / `close()` methods
- [ ] `print()` method
- [ ] `confirm(message)` / `prompt(message, default)` methods

### Networking
- [ ] `XMLHttpRequest` constructor
- [ ] `WebSocket` constructor
- [ ] `URL` / `URLSearchParams` constructors

### Miscellaneous
- [ ] `globalThis` binding
- [ ] `atob()` / `btoa()` methods — base64
- [ ] `structuredClone()` method
- [ ] `performance` property — Performance API (now, mark, measure, timing)
- [ ] `crypto` property — Crypto.getRandomValues()
- [ ] `getSelection()` method
- [ ] `postMessage()` method (cross-origin messaging)
- [ ] Event handler properties (`onload`, `onunload`, `onerror`, `onresize`, `onscroll`, etc.)

---

## 5. Event System

**Implemented:** Full EventTarget interface on Node (addEventListener, removeEventListener, dispatchEvent). Event propagation with capture → target → bubble phases. DomEvent base class (type, target, currentTarget, bubbles, cancelable, eventPhase, defaultPrevented, preventDefault, stopPropagation, stopImmediatePropagation). MouseEvent (clientX, clientY, button, ctrlKey, shiftKey, altKey, metaKey). KeyboardEvent (key, code, ctrlKey, shiftKey, altKey, metaKey, repeat). JsEventWrapper exposes all event properties/methods to JS. Browser dispatches: mousedown, mouseup, click, mousemove, mouseover, mouseout, mouseenter, mouseleave. Document fires DOMContentLoaded and load events after page load. InteractionStateHelper manages :hover/:focus/:active pseudo-class state.

**TODO:**

### Event Interface Gaps
- [ ] `Event` constructor (allow `new Event('custom')` from JS)
- [ ] `composedPath()` method
- [ ] `composed` property
- [ ] `isTrusted` property
- [ ] `timeStamp` property

### UI Event Types
- [ ] `FocusEvent` — focus, blur, focusin, focusout
- [ ] `InputEvent` — input, beforeinput
- [ ] `WheelEvent` — wheel
- [ ] `PointerEvent` — pointerdown, pointerup, pointermove, pointerenter, pointerleave
- [ ] `TouchEvent` — touchstart, touchend, touchmove, touchcancel
- [ ] `DragEvent` — dragstart, drag, dragenter, dragleave, dragover, drop, dragend

### Document/Window Events
- [ ] `resize` event
- [ ] `scroll` event
- [ ] `hashchange` / `popstate` events
- [ ] `visibilitychange` event
- [ ] `error` event (window-level)
- [ ] `unload` / `beforeunload` events

### Element Events
- [ ] `submit` / `reset` events (forms)
- [ ] `change` / `input` events (form controls)
- [ ] `on*` properties on elements (onclick, onmouseover, etc.)
- [ ] `CustomEvent` constructor — user-defined events with `detail`

---

## 6. Advanced DOM Interfaces (completely missing)

### MutationObserver
- [ ] `MutationObserver` constructor
- [ ] `observe(target, options)` method
- [ ] `disconnect()` method
- [ ] `takeRecords()` method
- [ ] `MutationRecord` interface

### Range & Selection
- [ ] `Range` interface — `createRange()`, `setStart()`, `setEnd()`, `selectNode()`, `collapse()`, `cloneContents()`, `deleteContents()`, `extractContents()`, `toString()`
- [ ] `Selection` interface — `anchorNode`, `focusNode`, `getRangeAt()`, `addRange()`, `removeAllRanges()`, `toString()`

### TreeWalker & NodeIterator
- [ ] `TreeWalker` interface — `currentNode`, `parentNode()`, `firstChild()`, `nextSibling()`, `nextNode()`, `previousNode()`
- [ ] `NodeIterator` interface — `nextNode()`, `previousNode()`
- [ ] `NodeFilter` constants

### DocumentFragment
- [ ] `DocumentFragment` constructor / `document.createDocumentFragment()`
- [ ] Use as lightweight container for batch DOM operations

### DOMTokenList (proper classList implementation)
- [ ] `length` property
- [ ] `value` property
- [ ] `item(index)` method
- [ ] `replace(oldToken, newToken)` method
- [ ] `supports(token)` method
- [ ] `entries()` / `keys()` / `values()` / `forEach()` iterators
- [ ] Symbol.iterator support

### NamedNodeMap (element.attributes)
- [ ] `length` property
- [ ] `item(index)` / `getNamedItem(name)` / `setNamedItem(attr)` / `removeNamedItem(name)` methods

---

## 7. HTML-Specific Element Interfaces (completely missing)

- [ ] `HTMLAnchorElement` — href, target, download, rel, etc.
- [ ] `HTMLImageElement` — src, alt, width, height, naturalWidth, naturalHeight, complete
- [ ] `HTMLInputElement` — type, value, checked, disabled, name, placeholder, etc.
- [ ] `HTMLFormElement` — action, method, elements, submit(), reset(), etc.
- [ ] `HTMLSelectElement` — options, selectedIndex, value, etc.
- [ ] `HTMLTextAreaElement` — value, rows, cols, etc.
- [ ] `HTMLButtonElement` — type, disabled, form, etc.
- [ ] `HTMLCanvasElement` — getContext(), width, height, toDataURL()
- [ ] `HTMLVideoElement` / `HTMLAudioElement` — media controls
- [ ] `HTMLTableElement` — rows, insertRow, deleteRow, etc.
- [ ] `HTMLTemplateElement` — content property (DocumentFragment)
- [ ] `HTMLDialogElement` — open, showModal(), close()
- [ ] `HTMLDetailsElement` — open property
- [ ] All `HTMLElement` shared properties: hidden, title, lang, dir, contentEditable, tabIndex, draggable, etc.

---

## 8. CSSStyleDeclaration Gaps

**Implemented (65 properties via JsCssStyleDeclaration):** cssText, color, backgroundColor, fontSize, fontFamily, fontWeight, fontStyle, textAlign, lineHeight, letterSpacing, wordSpacing, textTransform, textDecoration, textOverflow, whiteSpace, visibility, opacity, cursor, overflow, boxSizing, margin, marginTop, marginRight, marginBottom, marginLeft, padding, paddingTop, paddingRight, paddingBottom, paddingLeft, width, height, minWidth, maxWidth, minHeight, maxHeight, display, position, top, left, right, bottom, zIndex, border, borderWidth, borderColor, borderStyle, borderTop, borderRight, borderBottom, borderLeft, borderRadius, borderTopLeftRadius, borderTopRightRadius, borderBottomRightRadius, borderBottomLeftRadius, flexDirection, flexWrap, flexGrow, flexShrink, flexBasis, flex, justifyContent, alignItems, alignSelf, gap, listStyleType

**TODO:**
- [ ] `getPropertyValue(property)` method
- [ ] `setProperty(property, value, priority)` method
- [ ] `removeProperty(property)` method
- [ ] `getPropertyPriority(property)` method
- [ ] `length` property
- [ ] `item(index)` method
- [ ] Index-based access
- [ ] CSS custom properties (`--variable-name`)
- [ ] Additional properties: float, clear, transform, transition, animation, gridTemplateColumns, gridTemplateRows, boxShadow, outline, verticalAlign, wordBreak, overflowWrap, textIndent, textShadow, listStylePosition, listStyleImage, tableLayout, borderCollapse, borderSpacing, emptyCells, captionSide, content, counterIncrement, counterReset, resize, userSelect, pointerEvents, objectFit, objectPosition, filter, backdropFilter, mixBlendMode, isolation, willChange, contain, aspectRatio, accentColor, colorScheme, etc.

---

## 9. Miscellaneous Missing Web APIs

- [ ] `DOMParser` — `parseFromString(string, mimeType)`
- [ ] `XMLSerializer` — `serializeToString(node)`
- [ ] `DOMException` — proper error types for DOM operations
- [ ] `AbortController` / `AbortSignal`
- [ ] `IntersectionObserver`
- [ ] `ResizeObserver`
- [ ] `FormData` constructor
- [ ] `Headers` / `Request` / `Response` (Fetch API types — partial: JsResponseWrapper exists with status/statusText/url/ok/headers/text()/json())
- [ ] `Blob` / `File` / `FileReader`
- [ ] `TextEncoder` / `TextDecoder`
- [ ] `Worker` / `SharedWorker` — Web Workers
- [ ] `BroadcastChannel`
- [ ] `MessageChannel` / `MessagePort`

---

## 10. Additional Implemented Infrastructure (not in spec but supporting)

- `JsTextNodeWrapper` — TextNode-specific: `data` (get/set), `nodeValue` (get/set), `length` (get)
- `JsNodeListWrapper` — NodeList: `length`, numeric index access, `item()`, `forEach()`
- `NodeWrapperCache` — ConditionalWeakTable for 1:1 C# node ↔ JS wrapper identity
- `JsWrapperExtensions` — `DefineMethod`/`DefineGetter`/`DefineGetterSetter` helpers
- `TimerScheduler` — Stopwatch-based monotonic timer queue: setTimeout/setInterval/requestAnimationFrame with real delays, drained per frame
- `JsResponseWrapper` — fetch() Response object: status, statusText, url, ok, headers, text(), json()
- `JsLocationWrapper` — window.location: href, protocol, host, hostname, port, pathname, search, hash, origin, assign(), replace(), reload(), toString()
- `JsHistoryWrapper` — window.history: length, state, pushState(), replaceState(), back(), forward(), go()
- `JsStorageWrapper` — localStorage/sessionStorage: length, getItem(), setItem(), removeItem(), clear(), key()
- `JsFetchApi` — global fetch() returning Promise, async HTTP via Task.Run + main-thread marshaling
- `DomBridge` — entry point installing document/window/fetch/timers/storage/location/history globals
