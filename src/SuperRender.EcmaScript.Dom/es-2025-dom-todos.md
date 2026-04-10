# ECMAScript DOM Bindings — Unimplemented Features

Tracks missing DOM / Web API features relative to the WHATWG DOM Living Standard and related specs.
Each section lists what **is** implemented, then what remains.

---

## 1. Node Interface

**Implemented:** `nodeType`, `nodeName`, `parentNode`, `parentElement`, `firstChild`, `lastChild`, `nextSibling`, `previousSibling`, `childNodes`, `textContent`, `appendChild()`, `removeChild()`, `insertBefore()`, `hasChildNodes()`

**TODO:**
- [ ] `nodeValue` property (on all node types, not just TextNode)
- [ ] `ownerDocument` property (backing exists in C# but not exposed to JS)
- [ ] `isConnected` property
- [ ] `cloneNode(deep)` method (C# `DomMutationApi.CloneElement` exists but not wired to JS)
- [ ] `replaceChild(newChild, oldChild)` method
- [ ] `contains(node)` method
- [ ] `getRootNode(options)` method
- [ ] `normalize()` method — merge adjacent text nodes
- [ ] `compareDocumentPosition(other)` method
- [ ] `isSameNode(other)` / `isEqualNode(other)` methods
- [ ] `lookupPrefix()` / `lookupNamespaceURI()` / `isDefaultNamespace()` — namespace support

---

## 2. Element Interface

**Implemented:** `tagName`, `id`, `className`, `classList` (add/remove/toggle/contains), `innerText`, `innerHTML` (get/set), `children`, `style`, `getAttribute()`, `setAttribute()`, `removeAttribute()`, `hasAttribute()`, `querySelector()`, `querySelectorAll()`

**TODO:**
- [ ] `attributes` property (NamedNodeMap)
- [ ] `outerHTML` property (read/write)
- [ ] `dataset` property — `data-*` attribute access
- [ ] `slot` property
- [ ] `classList` — full DOMTokenList (item, length, value, replace, supports, entries/keys/values/forEach)
- [ ] `matches(selector)` method
- [ ] `closest(selector)` method
- [ ] `getElementsByTagName(name)` method (on Element, not just Document)
- [ ] `getElementsByClassName(name)` method (on Element, not just Document)
- [ ] `insertAdjacentHTML(position, text)` method
- [ ] `insertAdjacentElement(position, element)` method
- [ ] `insertAdjacentText(position, text)` method
- [ ] `replaceWith(...nodes)` method
- [ ] `remove()` method
- [ ] `before(...nodes)` / `after(...nodes)` methods
- [ ] `prepend(...nodes)` / `append(...nodes)` / `replaceChildren(...nodes)` methods
- [ ] `getAttributeNames()` method
- [ ] `toggleAttribute(name, force)` method
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

**Implemented:** `documentElement`, `body`, `head`, `title`, `createElement()`, `createTextNode()`, `getElementById()`, `getElementsByTagName()`, `getElementsByClassName()`, `querySelector()`, `querySelectorAll()`

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
- [ ] `location` property (Location object)
- [ ] `referrer` property
- [ ] `cookie` property (read/write)
- [ ] `hidden` / `visibilityState` properties
- [ ] `defaultView` property (→ window)
- [ ] `forms` / `images` / `links` / `scripts` collections
- [ ] `DOMContentLoaded` and `load` event firing

---

## 4. Window Interface

**Implemented:** `document`, `innerWidth`, `innerHeight`, `devicePixelRatio`, `console`, `setTimeout()` (stub), `clearTimeout()`, `setInterval()` (stub), `clearInterval()`, `alert()`

**TODO:**

### Timers & Animation
- [ ] `setTimeout()` — actual async scheduling (currently only fires if delay ≤ 0)
- [ ] `setInterval()` — actual recurring scheduling (currently stub)
- [ ] `requestAnimationFrame(callback)` method
- [ ] `cancelAnimationFrame(id)` method
- [ ] `queueMicrotask(callback)` method

### Navigation & Location
- [ ] `location` property — full Location object (href, protocol, host, pathname, search, hash, assign, replace, reload)
- [ ] `history` property — History object (pushState, replaceState, back, forward, go)
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

### Storage
- [ ] `localStorage` property — Storage object (getItem, setItem, removeItem, clear, length, key)
- [ ] `sessionStorage` property — Storage object

### Networking
- [ ] `fetch()` function — Fetch API
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

## 5. Event System (completely missing)

The entire DOM event system is unimplemented.

### EventTarget Interface
- [ ] `addEventListener(type, listener, options)` method
- [ ] `removeEventListener(type, listener, options)` method
- [ ] `dispatchEvent(event)` method
- [ ] All Node/Element/Document/Window types should inherit EventTarget

### Event Interface
- [ ] `Event` constructor
- [ ] Properties: `type`, `target`, `currentTarget`, `eventPhase`, `bubbles`, `cancelable`, `defaultPrevented`, `composed`, `isTrusted`, `timeStamp`
- [ ] Methods: `preventDefault()`, `stopPropagation()`, `stopImmediatePropagation()`, `composedPath()`
- [ ] Event propagation phases: capture → target → bubble

### UI Event Types
- [ ] `MouseEvent` — click, dblclick, mousedown, mouseup, mousemove, mouseenter, mouseleave, mouseover, mouseout, contextmenu
- [ ] `KeyboardEvent` — keydown, keyup, keypress (deprecated but common)
- [ ] `FocusEvent` — focus, blur, focusin, focusout
- [ ] `InputEvent` — input, beforeinput
- [ ] `WheelEvent` — wheel
- [ ] `PointerEvent` — pointerdown, pointerup, pointermove, pointerenter, pointerleave
- [ ] `TouchEvent` — touchstart, touchend, touchmove, touchcancel
- [ ] `DragEvent` — dragstart, drag, dragenter, dragleave, dragover, drop, dragend

### Document/Window Events
- [ ] `DOMContentLoaded` event
- [ ] `load` / `unload` / `beforeunload` events
- [ ] `resize` event
- [ ] `scroll` event
- [ ] `hashchange` / `popstate` events
- [ ] `visibilitychange` event
- [ ] `error` event (window-level)

### Element Events
- [ ] `submit` / `reset` events (forms)
- [ ] `change` / `input` events (form controls)
- [ ] `on*` properties on elements (onclick, onmouseover, etc.)
- [ ] `CustomEvent` constructor — user-defined events

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

**Implemented (28 properties):** color, backgroundColor, fontSize, fontFamily, fontWeight, fontStyle, textAlign, lineHeight, margin (+ 4 sides), padding (+ 4 sides), width, height, display, position, top/left/right/bottom, borderWidth/Color/Style, textDecoration, cssText

**TODO:**
- [ ] `getPropertyValue(property)` method
- [ ] `setProperty(property, value, priority)` method
- [ ] `removeProperty(property)` method
- [ ] `getPropertyPriority(property)` method
- [ ] `length` property
- [ ] `item(index)` method
- [ ] Index-based access
- [ ] CSS custom properties (`--variable-name`)
- [ ] Additional properties: opacity, visibility, overflow, float, clear, zIndex, cursor, transform, transition, animation, flexDirection, justifyContent, alignItems, gap, gridTemplateColumns, gridTemplateRows, boxShadow, borderRadius, outline, minWidth, maxWidth, minHeight, maxHeight, boxSizing, verticalAlign, whiteSpace, wordBreak, overflowWrap, letterSpacing, wordSpacing, textTransform, textIndent, textShadow, listStyleType, listStylePosition, listStyleImage, tableLayout, borderCollapse, borderSpacing, emptyCells, captionSide, content, counterIncrement, counterReset, resize, userSelect, pointerEvents, objectFit, objectPosition, filter, backdropFilter, mixBlendMode, isolation, willChange, contain, aspectRatio, accentColor, colorScheme, etc.

---

## 9. Miscellaneous Missing Web APIs

- [ ] `DOMParser` — `parseFromString(string, mimeType)`
- [ ] `XMLSerializer` — `serializeToString(node)`
- [ ] `DOMException` — proper error types for DOM operations
- [ ] `AbortController` / `AbortSignal`
- [ ] `IntersectionObserver`
- [ ] `ResizeObserver`
- [ ] `FormData` constructor
- [ ] `Headers` / `Request` / `Response` (Fetch API types)
- [ ] `Blob` / `File` / `FileReader`
- [ ] `TextEncoder` / `TextDecoder`
- [ ] `Worker` / `SharedWorker` — Web Workers
- [ ] `BroadcastChannel`
- [ ] `MessageChannel` / `MessagePort`
