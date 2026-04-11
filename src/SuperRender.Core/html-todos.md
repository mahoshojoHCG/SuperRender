# HTML Feature Gaps vs Latest Spec

Audit of SuperRender.Core against the WHATWG HTML Living Standard and related DOM specifications.
Features already implemented are **not** listed here. See CLAUDE.md for the current implementation summary.

> Legend: `[P]` = partially implemented; `[ ]` = not implemented at all.

---

## 1. HTML Tokenizer (WHATWG Section 13.2.5)

Currently implemented: 16 states (Data, TagOpen, EndTagOpen, TagName, BeforeAttributeName, AttributeName, AfterAttributeName, BeforeAttributeValue, AttributeValueQuoted, AttributeValueUnquoted, SelfClosingStartTag, MarkupDeclarationOpen, Comment, CommentDash, CommentEnd, BogusComment).

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

### Character references
- [P] Named entity decoding — only 6 entities implemented (`amp`, `lt`, `gt`, `quot`, `apos`, `nbsp`); WHATWG spec defines 2231 named character references
- [ ] Numeric character reference overflow/surrogate checking per spec
- [ ] Legacy named character references without semicolons (compatibility)
- [ ] Character reference in attribute value special handling (ambiguous ampersand)

---

## 2. Tree Construction (WHATWG Section 13.2.6)

Currently implemented: simplified tag-stacking with auto html/head/body structure, void element recognition, block element whitespace filtering.

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
- [ ] Adoption agency algorithm (for formatting elements like `<b>`, `<i>`, `<a>`)
- [ ] Foster parenting (misnested table content)
- [ ] Active formatting elements list (reconstruct-the-active-formatting-elements)
- [ ] Template insertion mode stack
- [ ] Generic RCDATA/RAWTEXT element parsing algorithm (for `<title>`, `<style>`, `<textarea>`, etc.)
- [ ] Implied end tag generation (auto-closing `<p>`, `<li>`, `<dd>`, `<dt>`, `<option>`, etc.)
- [ ] Scope element checking (has-an-element-in-scope, has-an-element-in-list-item-scope, etc.)
- [ ] Reset the insertion mode appropriately

### Special element handling
- [ ] `<script>` — script execution integration, special content parsing
- [ ] `<style>` — RAWTEXT content mode (currently treated as regular text)
- [ ] `<template>` — template content document fragment
- [ ] `<table>` — foster parenting, implicit `<tbody>`
- [ ] `<form>` — form owner tracking, implicit closing
- [ ] `<select>` — restricted content model (only `<option>`, `<optgroup>`)
- [ ] `<p>` — auto-closing when block-level element opened
- [ ] `<li>` — auto-closing when new `<li>` opened
- [ ] `<dd>`/`<dt>` — auto-closing within definition lists
- [ ] `<option>` — auto-closing when new `<option>` or `<optgroup>` opened
- [ ] `<h1>`–`<h6>` — auto-closing when another heading opened
- [ ] `<pre>`, `<listing>`, `<textarea>` — first newline stripping
- [ ] `<noscript>` — conditional content based on scripting flag
- [ ] `<frameset>`, `<frame>`, `<noframes>` — frames parsing (legacy)

### Fragment parsing
- [ ] Fragment parsing context (HTML fragment parsing algorithm)
- [ ] innerHTML setter parsing

---

## 3. DOM Interfaces (DOM Living Standard + HTML DOM)

### 3.1 Core node types

Currently implemented: `Document`, `Element`, `TextNode`, `NodeType` enum (Document, Element, Text, Comment).

- [ ] `Comment` node class (enum value exists but no `CommentNode` class)
- [ ] `DocumentFragment`
- [ ] `DocumentType`
- [ ] `ProcessingInstruction`
- [ ] `Attr` node type (attribute nodes)
- [ ] `CDATASection` (XML)

### 3.2 Node interface

Currently implemented: `AppendChild`, `RemoveChild`, `InsertBefore`, `FirstChild`, `LastChild`, `NextSibling`, `PreviousSibling`, `Parent`, `Children`, `OwnerDocument`, `NodeType`.

- [ ] `replaceChild(newChild, oldChild)`
- [ ] `cloneNode(deep)` (exists on `DomMutationApi` but not on Node base class)
- [ ] `normalize()` — merge adjacent text nodes
- [ ] `contains(node)`
- [ ] `compareDocumentPosition(node)`
- [ ] `isEqualNode(node)`, `isSameNode(node)`
- [ ] `textContent` property (getter/setter, per spec — different from `innerText`)
- [ ] `nodeValue` property
- [ ] `nodeName` property
- [ ] `baseURI` property
- [ ] `isConnected` property
- [ ] `getRootNode()` method
- [ ] `hasChildNodes()` method
- [ ] `parentElement` property (returns null if parent is not an Element)
- [ ] `childNodes` as `NodeList` (currently `List<Node>`)
- [ ] `NodeList` and `HTMLCollection` wrapper types

### 3.3 Document interface

Currently implemented: `DocumentElement`, `Body`, `Head`, `Stylesheets`, `CreateElement`, `CreateTextNode`, `NeedsLayout`.

- [ ] `getElementById(id)`
- [ ] `getElementsByTagName(tagName)`
- [ ] `getElementsByClassName(className)`
- [ ] `querySelector(selector)` (exists on `DomMutationApi`, not on `Document`)
- [ ] `querySelectorAll(selector)` (exists on `DomMutationApi`, not on `Document`)
- [ ] `createComment(data)`
- [ ] `createDocumentFragment()`
- [ ] `createAttribute(name)`
- [ ] `createTreeWalker()`, `createNodeIterator()`
- [ ] `importNode(node, deep)`
- [ ] `adoptNode(node)`
- [ ] `title` property
- [ ] `characterSet`, `contentType`
- [ ] `doctype` property
- [ ] `URL`, `documentURI`
- [ ] `compatMode`

### 3.4 Element interface

Currently implemented: `TagName`, `Attributes` (dict), `Id`, `ClassList` (readonly), `GetAttribute`, `SetAttribute`, `RemoveAttribute`, `InnerText`.

- [ ] `hasAttribute(name)`, `hasAttributes()`
- [ ] `getAttributeNames()`
- [ ] `toggleAttribute(name, force?)`
- [ ] `matches(selector)` / `closest(selector)`
- [ ] `classList` as full `DOMTokenList` interface (add, remove, toggle, contains, replace, supports, item, forEach, entries, values, keys, length)
- [ ] `className` property (get/set)
- [ ] `innerHTML` (getter/setter with HTML parsing)
- [ ] `outerHTML` (getter/setter)
- [ ] `textContent` property (per DOM spec, distinct from `innerText`)
- [ ] `insertAdjacentHTML(position, html)`
- [ ] `insertAdjacentElement(position, element)`
- [ ] `insertAdjacentText(position, text)`
- [ ] `after()`, `before()`, `replaceWith()`, `remove()` — ChildNode mixin
- [ ] `append()`, `prepend()`, `replaceChildren()` — ParentNode mixin
- [ ] `children` (HTMLCollection of child Elements)
- [ ] `firstElementChild`, `lastElementChild`, `previousElementSibling`, `nextElementSibling`, `childElementCount`
- [ ] `dataset` property (DOMStringMap for `data-*` attributes)
- [ ] `style` property (CSSStyleDeclaration)
- [ ] `slot` property
- [ ] `scrollTop`, `scrollLeft`, `scrollWidth`, `scrollHeight`
- [ ] `clientTop`, `clientLeft`, `clientWidth`, `clientHeight`
- [ ] `getBoundingClientRect()`
- [ ] `getClientRects()`
- [ ] `scroll()`, `scrollTo()`, `scrollBy()`, `scrollIntoView()`

### 3.5 Event system (DOM Events)

- [ ] `EventTarget` interface (`addEventListener`, `removeEventListener`, `dispatchEvent`)
- [ ] `Event` class with `type`, `target`, `currentTarget`, `eventPhase`, `bubbles`, `cancelable`, `defaultPrevented`, `timeStamp`
- [ ] Event propagation (capture → target → bubble phases)
- [ ] `stopPropagation()`, `stopImmediatePropagation()`, `preventDefault()`
- [ ] UI events: `MouseEvent`, `KeyboardEvent`, `FocusEvent`, `InputEvent`, `WheelEvent`, `PointerEvent`, `TouchEvent`
- [ ] Custom events: `CustomEvent`
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

Elements recognized as inline for display. Per-element default styles (user-agent stylesheet):
- [x] `<strong>` — default `font-weight: bold`
- [x] `<em>` — default `font-style: italic`
- [x] `<b>` — default `font-weight: bold`
- [x] `<i>` — default `font-style: italic`
- [x] `<u>` — default `text-decoration: underline`
- [x] `<s>`, `<del>` — default `text-decoration: line-through`
- [x] `<ins>` — default `text-decoration: underline`
- [x] `<mark>` — default highlighted background
- [x] `<small>` — default smaller font size
- [P] `<sub>`, `<sup>` — default font-size smaller (missing vertical-align)
- [ ] `<abbr>` — default dotted underline on some UAs
- [x] `<code>`, `<kbd>`, `<samp>` — default monospace font
- [P] `<pre>` — default monospace font (missing `white-space: pre`)
- [x] `<blockquote>` — default margin
- [ ] `<q>` — automatic quotation marks via CSS `quotes`
- [x] `<cite>`, `<dfn>` — default italic
- [ ] `<var>` — default italic (already in UA stylesheet but not listed as done because `<var>` needs testing)
- [ ] `<time>`, `<data>`, `<output>` — no special visual but machine-readable
- [ ] `<ruby>`, `<rt>`, `<rp>` — ruby annotation layout
- [ ] `<bdi>`, `<bdo>` — bidirectional isolation/override
- [ ] `<wbr>` — word break opportunity

### 4.3 Lists

Recognized elements: `<ul>`, `<ol>`, `<li>` (as block display). Missing:
- [ ] `<ul>` — default `list-style-type: disc`, padding/margin
- [ ] `<ol>` — default `list-style-type: decimal`, padding/margin, `start`/`reversed`/`type` attributes
- [ ] `<li>` — `value` attribute for ordered lists, marker generation
- [ ] `<dl>`, `<dt>`, `<dd>` — definition list layout with default indentation
- [ ] `<menu>` — semantic list

### 4.4 Tables

Recognized in block elements set for whitespace handling. Missing entirely:
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
- [ ] `<input>` — all types: `text`, `password`, `email`, `number`, `tel`, `url`, `search`, `date`, `time`, `datetime-local`, `month`, `week`, `color`, `range`, `file`, `checkbox`, `radio`, `submit`, `reset`, `button`, `hidden`, `image`
- [ ] `<textarea>` — multi-line text input with `rows`, `cols`, `wrap`
- [ ] `<select>` + `<option>` + `<optgroup>` — dropdown/listbox
- [ ] `<button>` — button with `type` attribute
- [ ] `<label>` — label association via `for` attribute or containment
- [ ] `<fieldset>` + `<legend>` — grouping with caption
- [ ] `<datalist>` — suggested input values
- [ ] `<output>` — calculation result
- [ ] `<progress>`, `<meter>` — progress/gauge indicators
- [ ] Form validation: `required`, `pattern`, `min`, `max`, `step`, `minlength`, `maxlength`, `ValidityState`, `checkValidity()`, `reportValidity()`, `setCustomValidity()`
- [ ] Form submission: `submit()`, `reset()`, `FormData`
- [ ] `<input>` pseudo-classes styling (`:checked`, `:disabled`, `:placeholder-shown`, etc.)
- [ ] `autofocus`, `tabindex`, `disabled`, `readonly` attribute behavior

### 4.6 Interactive elements

- [ ] `<details>` + `<summary>` — disclosure widget (toggle open/closed)
- [ ] `<dialog>` — modal and non-modal dialog with `open`, `showModal()`, `show()`, `close()`, `returnValue`
- [ ] `<a>` — hyperlink behavior (`href`, `target`, `rel`, `download`, `ping`, `referrerpolicy`)
- [ ] `<area>` — image map areas

### 4.7 Embedded content

- [ ] `<img>` — image loading, `src`, `srcset`, `sizes`, `alt`, `width`, `height`, `loading`, `decoding`, intrinsic sizing, aspect ratio
- [ ] `<picture>` + `<source>` — responsive images
- [ ] `<video>` — video playback with `src`, `poster`, `controls`, `autoplay`, `loop`, `muted`, `preload`, tracks
- [ ] `<audio>` — audio playback
- [ ] `<source>` — media resource alternatives
- [ ] `<track>` — text tracks (subtitles, captions)
- [ ] `<canvas>` — 2D/WebGL rendering context
- [ ] `<svg>` — inline SVG and SVG DOM
- [ ] `<math>` — MathML integration
- [ ] `<iframe>` — nested browsing context with `src`, `srcdoc`, `sandbox`, `allow`, `loading`
- [ ] `<embed>`, `<object>` — plugin/external content
- [ ] `<map>` — image map

### 4.8 Scripting

- [ ] `<script>` — script execution, `src`, `type`, `async`, `defer`, `crossorigin`, `integrity`, `nomodule`
- [ ] `<noscript>` — fallback content when scripting disabled
- [ ] `<canvas>` scripting interface

### 4.9 Metadata elements

Head-content elements are hidden (`display: none`). Missing:
- [ ] `<title>` — document title (`document.title`)
- [ ] `<meta>` — metadata: `charset`, `http-equiv`, `name`, `content`
- [ ] `<link>` — external resources: `rel="stylesheet"`, `rel="icon"`, `rel="preload"`, `rel="preconnect"`, etc.
- [ ] `<base>` — base URL and target
- [ ] `<style>` — CSS extraction already works; missing: `media` attribute, `@import` support

---

## 5. User-Agent Stylesheet

Currently: default display types, heading sizes/margins, paragraph margins, body margin, blockquote indent, hr border, lists padding, mark highlight, small size, pre/code monospace, link blue color, and text-level element styles (bold, italic, underline, strikethrough) are all defined in the UA stylesheet. Font-weight, font-style, and text-decoration-line CSS properties are fully supported in the cascade, including inheritance for font-weight and font-style.

- [x] Heading sizes: `h1` = 2em, `h2` = 1.5em, `h3` = 1.17em, `h4` = 1em, `h5` = 0.83em, `h6` = 0.67em
- [x] Heading/paragraph default margins (`h1` = 0.67em top/bottom, `p` = 1em top/bottom, etc.)
- [x] `<strong>`, `<b>` → `font-weight: bold`
- [x] `<em>`, `<i>`, `<cite>`, `<dfn>`, `<var>` → `font-style: italic`
- [x] `<u>`, `<ins>` → `text-decoration: underline`
- [x] `<s>`, `<del>`, `<strike>` → `text-decoration: line-through`
- [x] `<code>`, `<kbd>`, `<samp>` → `font-family: monospace`
- [P] `<pre>` → `font-family: monospace` (missing `white-space: pre`)
- [x] `<blockquote>` → margin indentation
- [P] `<ul>`, `<ol>` → `padding-left: 40px` (missing list markers)
- [ ] `<li>` → `display: list-item`
- [ ] `<dd>` → `margin-left: 40px`
- [x] `<hr>` → border, margin
- [x] `<a>` → `color: blue`, `text-decoration: underline`
- [x] `<mark>` → `background-color: yellow`
- [x] `<small>` → `font-size: smaller`
- [P] `<sub>` → `font-size: smaller` (missing `vertical-align: sub`)
- [P] `<sup>` → `font-size: smaller` (missing `vertical-align: super`)
- [ ] `<table>` → `border-collapse: separate`, `border-spacing: 2px`
- [ ] `<th>` → `font-weight: bold`, `text-align: center`
- [ ] `<fieldset>` → border, padding, margin
- [ ] `<legend>` → positioning at fieldset border
- [ ] `<input>`, `<textarea>`, `<select>`, `<button>` → form control default styles
- [ ] Hidden attribute: `[hidden] { display: none }`
- [ ] `<tt>` → `font-family: monospace`

---

## 6. HTML Attributes — Missing Behavioral Support

Currently supported: `id`, `class`, `style` (inline CSS parsing).

### Global attributes
- [ ] `hidden` → should map to `display: none`
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

### Data attributes
- [ ] `data-*` → `element.dataset` DOMStringMap

### ARIA attributes
- [ ] `role` and all `aria-*` attributes — accessibility tree

### Event handler attributes
- [ ] `onclick`, `onload`, `onerror`, `onsubmit`, `onchange`, `oninput`, `onfocus`, `onblur`, `onkeydown`, `onkeyup`, `onmouseenter`, `onmouseleave`, etc.

---

## 7. HTML Parsing Error Recovery

Currently: basic error recovery (pops until matching tag found, stray end tags ignored).

- [ ] Spec-compliant parse error handling with error types
- [ ] Optional end tag handling (`<p>`, `<li>`, `<td>`, `<th>`, `<tr>`, `<dd>`, `<dt>`, `<option>`, `<optgroup>`, `<head>`, `<body>`, `<html>`, `<colgroup>`, `<caption>`, `<thead>`, `<tbody>`, `<tfoot>`)
- [ ] `<p>` auto-closing before block-level elements
- [ ] Formatting element recovery (adoption agency)
- [ ] Misnested table content foster parenting
- [ ] Missing end tags (implicit closing at parent end)

---

## 8. Web Components

- [ ] Custom Elements: `customElements.define()`, `HTMLElement` subclassing, lifecycle callbacks (`connectedCallback`, `disconnectedCallback`, `attributeChangedCallback`, `adoptedCallback`)
- [ ] Shadow DOM (see 3.7 above)
- [ ] HTML Templates: `<template>` element with `content` DocumentFragment
- [ ] `<slot>` element for content projection

---

## 9. APIs and Integration

### 9.1 Window / GlobalEventHandlers
- [ ] `window.requestAnimationFrame()`
- [ ] `window.getComputedStyle(element)`
- [ ] `window.matchMedia(mediaQuery)`
- [ ] `window.scrollX`, `window.scrollY`, `window.innerWidth`, `window.innerHeight`

### 9.2 Navigation / History
- [ ] `<a>` click → navigation
- [ ] `location` object
- [ ] `history` API (`pushState`, `replaceState`, `popstate`)
- [ ] `hashchange` event

### 9.3 Resource loading
- [ ] External stylesheet loading (`<link rel="stylesheet">`)
- [ ] Image loading (`<img>`)
- [ ] Script loading (`<script src>`)
- [ ] `fetch()` / `XMLHttpRequest`
- [ ] Preloading (`<link rel="preload">`)

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
