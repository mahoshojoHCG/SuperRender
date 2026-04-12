# HTML Feature Gaps vs Latest Spec

Audit of SuperRender against the WHATWG HTML Living Standard and related DOM specifications.
Each section lists what **is** implemented, then what remains.

> Legend: `[P]` = partially implemented; `[ ]` = not implemented at all.

*Last updated: 2026-04-12*

---

## 1. HTML Tokenizer (WHATWG Section 13.2.5)

**Implemented:** 16 states (Data, TagOpen, EndTagOpen, TagName, BeforeAttributeName, AttributeName, AfterAttributeName, BeforeAttributeValue, AttributeValueQuoted, AttributeValueUnquoted, SelfClosingStartTag, MarkupDeclarationOpen, Comment, CommentDash, CommentEnd, BogusComment). Full named entity decoding (2125 WHATWG entities via `HtmlEntityTable`). Numeric character references (decimal and hex) with overflow/surrogate checking per spec.

### Missing tokenizer states (WHATWG defines 80 states)
- [ ] RCDATA state — for `<title>`, `<textarea>` content
- [ ] RAWTEXT state — for `<style>`, `<xmp>`, `<iframe>`, `<noembed>`, `<noframes>`, `<noscript>` content
- [ ] Script data state — for `<script>` content
- [ ] Script data less-than sign / end tag open / end tag name / escape / double escape states (8 states)
- [ ] PLAINTEXT state
- [ ] CDATA section state / bracket / end states
- [ ] Character reference state and its sub-states (named, numeric, ambiguous ampersand, etc.)
- [ ] Comment start / comment start dash / comment less-than sign / comment end bang states
- [ ] DOCTYPE states (before name, name, after name, public identifier, system identifier, bogus — 17+ states total)
- [ ] Before attribute value (double-quoted/single-quoted) state separation
- [ ] After attribute value (quoted) state

### Character references — remaining gaps
- [ ] Legacy named character references without semicolons (compatibility)
- [ ] Character reference in attribute value special handling (ambiguous ampersand)

---

## 2. Tree Construction (WHATWG Section 13.2.6)

**Implemented:** Simplified tag-stacking with auto html/head/body structure, void element recognition, block element whitespace filtering. Adoption agency algorithm. Auto-closing rules for `<p>` (before block-level elements), `<li>`, `<dd>`/`<dt>`, `<option>`, `<h1>`–`<h6>`.

### Insertion modes (WHATWG defines 23 modes)
- [ ] "initial" mode
- [ ] "before html" mode
- [ ] "before head" mode
- [ ] "in head" mode
- [ ] "in head noscript" mode
- [ ] "after head" mode
- [ ] "in body" mode (full spec version with all tag-specific handling)
- [ ] "text" mode (for RAWTEXT/RCDATA/script-data elements)
- [ ] "in table" / "in table text" / "in caption" / "in column group" / "in table body" / "in row" / "in cell" modes
- [ ] "in select" / "in select in table" modes
- [ ] "in template" mode
- [ ] "after body" / "in frameset" / "after frameset" / "after after body" / "after after frameset" modes

### Algorithms
- [ ] Foster parenting (misnested table content)
- [ ] Active formatting elements list (reconstruct-the-active-formatting-elements)
- [ ] Template insertion mode stack
- [ ] Generic RCDATA/RAWTEXT element parsing algorithm (for `<title>`, `<style>`, `<textarea>`, etc.)
- [ ] Scope element checking (has-an-element-in-scope, has-an-element-in-list-item-scope, etc.)
- [ ] Reset the insertion mode appropriately

### Special element handling
- [ ] `<script>` — script execution integration, special content parsing
- [ ] `<style>` — RAWTEXT content mode (currently treated as regular text)
- [ ] `<template>` — template content document fragment
- [ ] `<table>` — foster parenting, implicit `<tbody>`
- [ ] `<form>` — form owner tracking, implicit closing
- [ ] `<select>` — restricted content model (only `<option>`, `<optgroup>`)
- [ ] `<pre>`, `<listing>`, `<textarea>` — first newline stripping
- [ ] `<noscript>` — conditional content based on scripting flag
- [ ] `<frameset>`, `<frame>`, `<noframes>` — frames parsing (legacy)

### Fragment parsing
- [ ] Fragment parsing context (HTML fragment parsing algorithm)
- [ ] innerHTML setter parsing (full spec compliance)

---

## 3. DOM Interfaces (DOM Living Standard + HTML DOM)

### 3.1 Core node types

**Implemented:** `Document`, `Element`, `TextNode`, `NodeType` enum (Document, Element, Text, Comment).

- [ ] `Comment` node class (enum value exists but no `CommentNode` class)
- [ ] `DocumentFragment`
- [ ] `DocumentType`
- [ ] `ProcessingInstruction`
- [ ] `Attr` node type (attribute nodes)
- [ ] `CDATASection` (XML)

### 3.2 Node interface

**Implemented:** `AppendChild`, `RemoveChild`, `InsertBefore`, `ReplaceChild`, `CloneNode(deep)`, `Contains(node)`, `HasChildNodes()`, `TextContent` (get/set), `FirstChild`, `LastChild`, `NextSibling`, `PreviousSibling`, `Parent`, `ParentElement`, `Children`, `OwnerDocument`, `NodeType`, `NodeName`. Full EventTarget: `AddEventListener`, `RemoveEventListener`, `DispatchEvent` with capture/target/bubble propagation.

- [ ] `normalize()` — merge adjacent text nodes
- [ ] `compareDocumentPosition(node)`
- [ ] `isEqualNode(node)`, `isSameNode(node)`
- [ ] `nodeValue` property (on non-TextNode types)
- [ ] `baseURI` property
- [ ] `isConnected` property
- [ ] `getRootNode()` method
- [ ] `childNodes` as `NodeList` (currently `List<Node>`)
- [ ] `NodeList` and `HTMLCollection` wrapper types

### 3.3 Document interface

**Implemented:** `DocumentElement`, `Body`, `Head`, `Title`, `Stylesheets`, `CreateElement`, `CreateTextNode`, `getElementById`, `getElementsByTagName`, `getElementsByClassName`, `querySelector`, `querySelectorAll`, `NeedsLayout`.

- [ ] `createComment(data)`
- [ ] `createDocumentFragment()`
- [ ] `createAttribute(name)`
- [ ] `createTreeWalker()`, `createNodeIterator()`
- [ ] `importNode(node, deep)`
- [ ] `adoptNode(node)`
- [ ] `characterSet`, `contentType`
- [ ] `doctype` property
- [ ] `URL`, `documentURI`
- [ ] `compatMode`

### 3.4 Element interface

**Implemented:** `TagName`, `Attributes` (dict), `Id`, `ClassName`, `ClassList` (add/remove/toggle/contains), `InnerText`, `InnerHTML` (get/set), `GetAttribute`, `SetAttribute`, `RemoveAttribute`, `HasAttribute`, `ToggleAttribute`, `Matches(selector)`, `Closest(selector)`, `QuerySelector`, `QuerySelectorAll`, `Style` (CSSStyleDeclaration), `Dataset` (data-* attributes), `Children`, `FirstElementChild`, `LastElementChild`, `ChildElementCount`, `After()`, `Before()`, `Remove()`. Pseudo-class state flags: `IsHovered`, `IsFocused`, `IsActive`.

- [ ] `hasAttributes()`
- [ ] `getAttributeNames()`
- [ ] `classList` as full `DOMTokenList` interface (item, length, value, replace, supports, forEach, entries, keys, values)
- [ ] `outerHTML` (getter/setter)
- [ ] `insertAdjacentHTML(position, html)`
- [ ] `insertAdjacentElement(position, element)`
- [ ] `insertAdjacentText(position, text)`
- [ ] `replaceWith(...nodes)`
- [ ] `append()`, `prepend()`, `replaceChildren()` — ParentNode mixin
- [ ] `previousElementSibling`, `nextElementSibling`
- [ ] `slot` property
- [ ] `scrollTop`, `scrollLeft`, `scrollWidth`, `scrollHeight`
- [ ] `clientTop`, `clientLeft`, `clientWidth`, `clientHeight`
- [ ] `getBoundingClientRect()`
- [ ] `getClientRects()`
- [ ] `scroll()`, `scrollTo()`, `scrollBy()`, `scrollIntoView()`

### 3.5 Event system (DOM Events)

**Implemented:** Full `EventTarget` interface on Node (`addEventListener`, `removeEventListener`, `dispatchEvent`). Event propagation with capture → target → bubble phases. `DomEvent` base class with `type`, `target`, `currentTarget`, `eventPhase`, `bubbles`, `cancelable`, `defaultPrevented`. `preventDefault()`, `stopPropagation()`, `stopImmediatePropagation()`. `MouseEvent` (clientX/Y, button, modifier keys). `KeyboardEvent` (key, code, modifier keys, repeat). Browser dispatches: `mousedown`, `mouseup`, `click`, `mousemove`, `mouseover`, `mouseout`, `mouseenter`, `mouseleave`. `DOMContentLoaded` and `load` events fire after page load. `JsEventWrapper` bridges all event types to JS.

- [ ] `Event` constructor (allow `new Event('custom')` from JS)
- [ ] `FocusEvent` — focus, blur, focusin, focusout
- [ ] `InputEvent` — input, beforeinput
- [ ] `WheelEvent` — wheel
- [ ] `PointerEvent` — pointerdown, pointerup, pointermove, pointerenter, pointerleave
- [ ] `TouchEvent` — touchstart, touchend, touchmove, touchcancel
- [ ] `DragEvent` — dragstart, drag, dragenter, dragleave, dragover, drop, dragend
- [ ] `CustomEvent` — user-defined events with `detail`
- [ ] DOM mutation events (deprecated) / `MutationObserver` (modern)

### 3.6 MutationObserver

- [ ] `MutationObserver` class
- [ ] `observe(target, options)` — `childList`, `attributes`, `characterData`, `subtree`, `attributeOldValue`, `characterDataOldValue`, `attributeFilter`
- [ ] `disconnect()`, `takeRecords()`
- [ ] `MutationRecord` type

### 3.7 Shadow DOM

- [ ] `Element.attachShadow(init)` — open/closed modes
- [ ] `ShadowRoot` interface
- [ ] `Element.shadowRoot` property
- [ ] Slot-based content distribution (`<slot>`, `slotchange` event)
- [ ] Composed event path
- [ ] CSS `::slotted()` pseudo-element
- [ ] CSS `:host`, `:host()`, `:host-context()` pseudo-classes

### 3.8 Range and Selection

- [ ] `Range` interface
- [ ] `Selection` interface
- [ ] `document.createRange()`
- [ ] `window.getSelection()`

### 3.9 TreeWalker and NodeIterator

- [ ] `TreeWalker` interface
- [ ] `NodeIterator` interface
- [ ] `NodeFilter` interface

---

## 4. HTML Elements — Missing Semantic/Behavioral Support

Currently recognized tags receive default display values (block/inline/none). No special runtime behavior beyond display type.

### 4.1 Sectioning and Structure

Elements are recognized for display only. Missing:
- [ ] `<main>` — unique main content constraint
- [ ] `<article>`, `<section>`, `<nav>`, `<aside>` — outline algorithm (deprecated but browsers still use heading-based implicit sections)
- [ ] `<header>`, `<footer>` — scoping within sectioning content
- [ ] `<hgroup>` — heading group semantics
- [ ] `<address>` — contact information semantics
- [ ] `<search>` — search landmark (new in HTML)

### 4.2 Text-level semantics

**Implemented in UA stylesheet:** `<strong>`/`<b>` (bold), `<em>`/`<i>`/`<cite>`/`<dfn>` (italic), `<u>`/`<ins>` (underline), `<s>`/`<del>` (line-through), `<mark>` (highlight), `<small>` (smaller), `<code>`/`<kbd>`/`<samp>` (monospace), `<blockquote>` (margin), `<pre>` (monospace + white-space:pre).

- [P] `<sub>`, `<sup>` — default font-size smaller (missing vertical-align)
- [ ] `<abbr>` — default dotted underline on some UAs
- [ ] `<q>` — automatic quotation marks via CSS `quotes`
- [ ] `<var>` — needs testing
- [ ] `<time>`, `<data>`, `<output>` — no special visual but machine-readable
- [ ] `<ruby>`, `<rt>`, `<rp>` — ruby annotation layout
- [ ] `<bdi>`, `<bdo>` — bidirectional isolation/override
- [ ] `<wbr>` — word break opportunity

### 4.3 Lists

**Implemented:** `<ul>`, `<ol>`, `<li>` as block display with `padding-left: 40px` indent. `list-style-type` supported with basic marker rendering.

- [ ] `<ol>` — `start`/`reversed`/`type` attributes
- [ ] `<li>` — `value` attribute for ordered lists
- [ ] `<dl>`, `<dt>`, `<dd>` — definition list layout with default indentation
- [ ] `<menu>` — semantic list

### 4.4 Tables

- [ ] `<table>` — table layout algorithm
- [ ] `<thead>`, `<tbody>`, `<tfoot>` — row group semantics
- [ ] `<tr>` — table row
- [ ] `<td>`, `<th>` — table cells with `colspan`, `rowspan`, `scope`, `headers`
- [ ] `<caption>` — table caption
- [ ] `<colgroup>`, `<col>` — column styling with `span` attribute
- [ ] Implicit `<tbody>` creation during parsing
- [ ] Table border model (separate/collapse)

### 4.5 Forms

Not implemented at all:
- [ ] `<form>` — form element with `action`, `method`, `enctype`, `target`, `novalidate`, `autocomplete`
- [ ] `<input>` — all types
- [ ] `<textarea>` — multi-line text input
- [ ] `<select>` + `<option>` + `<optgroup>` — dropdown/listbox
- [ ] `<button>` — button with `type` attribute
- [ ] `<label>` — label association
- [ ] `<fieldset>` + `<legend>` — grouping
- [ ] `<datalist>` — suggested input values
- [ ] `<output>` — calculation result
- [ ] `<progress>`, `<meter>` — progress/gauge indicators
- [ ] Form validation and submission

### 4.6 Interactive elements

- [ ] `<details>` + `<summary>` — disclosure widget
- [ ] `<dialog>` — modal and non-modal dialog
- [ ] `<area>` — image map areas

### 4.7 Embedded content

**Implemented:** `<img>` — image loading (`src`, `alt`, `width`, `height`), intrinsic sizing with aspect ratio preservation, pure C# decoders (PNG/BMP/JPEG), alt text fallback rendering. `<a>` — hyperlink click navigation, `target="_blank"` opens new tab.

- [ ] `<img>` — `srcset`, `sizes`, `loading`, `decoding`
- [ ] `<picture>` + `<source>` — responsive images
- [ ] `<video>` — video playback
- [ ] `<audio>` — audio playback
- [ ] `<canvas>` — 2D/WebGL rendering context
- [ ] `<svg>` — inline SVG and SVG DOM
- [ ] `<math>` — MathML integration
- [ ] `<iframe>` — nested browsing context
- [ ] `<embed>`, `<object>` — plugin/external content
- [ ] `<map>` — image map

### 4.8 Scripting

- [ ] `<script>` — script execution, `src`, `type`, `async`, `defer`, `crossorigin`, `integrity`, `nomodule`
- [ ] `<noscript>` — fallback content when scripting disabled
- [ ] `<canvas>` scripting interface

### 4.9 Metadata elements

Head-content elements are hidden (`display: none`). Missing:
- [ ] `<meta>` — metadata: `charset`, `http-equiv`, `name`, `content`
- [ ] `<link>` — `media` attribute, `@import` support
- [ ] `<base>` — base URL and target

---

## 5. User-Agent Stylesheet

**Implemented:** Default display types, heading sizes/margins (`h1`=2em through `h6`=0.67em), paragraph margins, body margin, blockquote indent, hr border, list padding, mark highlight, small size, pre/code monospace with `white-space: pre`, link blue color with underline, text-level element styles (bold, italic, underline, strikethrough), `[hidden] { display: none }`.

- [ ] `<li>` → `display: list-item` (currently block)
- [ ] `<dd>` → `margin-left: 40px`
- [ ] `<table>` → `border-collapse: separate`, `border-spacing: 2px`
- [ ] `<th>` → `font-weight: bold`, `text-align: center`
- [ ] `<fieldset>` → border, padding, margin
- [ ] `<legend>` → positioning at fieldset border
- [ ] `<input>`, `<textarea>`, `<select>`, `<button>` → form control default styles
- [ ] `<tt>` → `font-family: monospace`

---

## 6. HTML Attributes — Missing Behavioral Support

**Implemented:** `id`, `class`, `style` (inline CSS parsing), `hidden` (maps to `display: none`), `data-*` (accessible via `element.dataset`).

### Global attributes
- [ ] `tabindex` → focus ordering
- [ ] `title` → tooltip
- [ ] `lang` → language (affects hyphenation, quotes, etc.)
- [ ] `dir` → text directionality (`ltr`, `rtl`, `auto`)
- [ ] `draggable` → drag-and-drop
- [ ] `contenteditable` → editable content
- [ ] `spellcheck` → spell checking
- [ ] `autofocus` → initial focus
- [ ] `enterkeyhint` → virtual keyboard enter key
- [ ] `inputmode` → virtual keyboard type
- [ ] `is` → customized built-in element
- [ ] `slot` → shadow DOM slot assignment
- [ ] `part` → CSS `::part()` exposure
- [ ] `popover` — popover API
- [ ] `inert` — non-interactive subtree

### ARIA attributes
- [ ] `role` and all `aria-*` attributes — accessibility tree

### Event handler attributes
- [ ] `onclick`, `onload`, `onerror`, `onsubmit`, `onchange`, `oninput`, `onfocus`, `onblur`, `onkeydown`, `onkeyup`, `onmouseenter`, `onmouseleave`, etc.

---

## 7. HTML Parsing Error Recovery

**Implemented:** Basic error recovery (pops until matching tag found, stray end tags ignored). Adoption agency algorithm for formatting elements. Auto-closing for `<p>`, `<li>`, `<dd>`/`<dt>`, `<option>`, `<h1>`–`<h6>`.

- [ ] Spec-compliant parse error handling with error types
- [ ] Additional optional end tag handling (`<td>`, `<th>`, `<tr>`, `<colgroup>`, `<caption>`, `<thead>`, `<tbody>`, `<tfoot>`)
- [ ] Misnested table content foster parenting
- [ ] Missing end tags (implicit closing at parent end) — full spec compliance

---

## 8. Web Components

- [ ] Custom Elements: `customElements.define()`, `HTMLElement` subclassing, lifecycle callbacks
- [ ] Shadow DOM (see 3.7 above)
- [ ] HTML Templates: `<template>` element with `content` DocumentFragment
- [ ] `<slot>` element for content projection

---

## 9. APIs and Integration

### 9.1 Window / GlobalEventHandlers

**Implemented:** `window.requestAnimationFrame()`, `window.cancelAnimationFrame()`, `window.innerWidth`, `window.innerHeight`, `window.devicePixelRatio`, `window.setTimeout()`/`clearTimeout()`, `window.setInterval()`/`clearInterval()`, `window.alert()`, `window.console`.

- [ ] `window.getComputedStyle(element)`
- [ ] `window.matchMedia(mediaQuery)`
- [ ] `window.scrollX`, `window.scrollY`

### 9.2 Navigation / History

**Implemented:** `<a>` click → navigation, `target="_blank"` → new tab, `window.location` (href/protocol/host/hostname/port/pathname/search/hash/origin/assign/replace/reload), `window.history` (pushState/replaceState/back/forward/go/length/state), back/forward chrome buttons.

- [ ] `hashchange` event
- [ ] `popstate` event
- [ ] Fragment navigation — scroll to `#id` on same page

### 9.3 Resource loading

**Implemented:** External stylesheet loading (`<link rel="stylesheet">`), image loading (`<img>` with PNG/BMP/JPEG decode), `fetch()` API with Promise/Response.

- [ ] Script loading (`<script src>`)
- [ ] Preloading (`<link rel="preload">`)
- [ ] `XMLHttpRequest`

### 9.4 Intersection / Resize observers
- [ ] `IntersectionObserver`
- [ ] `ResizeObserver`

---

## 10. Accessibility

- [ ] Accessibility tree construction from DOM
- [ ] ARIA role mapping for HTML elements
- [ ] Focusable element management (`tabindex`, native focusable elements)
- [ ] Keyboard navigation
- [ ] `aria-*` attribute processing
- [ ] `role` attribute mapping
- [ ] Label association for form controls
