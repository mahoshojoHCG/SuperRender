# Manual Test Plan

Tests requiring visual rendering or interactive verification that cannot be covered by xUnit unit tests.

## How to Run

```bash
dotnet run --project src/SuperRender.Browser
```

Then type in the address bar:

```
sr://test/
```

This loads the built-in test index page via embedded resources. You can also load individual tests directly:

```
sr://test/01-box-sizing.html
sr://test/13-timers.html
```

**Alternative:** load from disk via file path:

```
/path/to/SuperRenderer/tests/manual/index.html
```

---

## Test 01: Box Sizing and Min/Max

**URL:** `sr://test/01-box-sizing.html`

| ID | Description | Expected |
|----|-------------|----------|
| 1a | `box-sizing: border-box` (width:300px, padding:20px, border:5px) | Total outer width is exactly 300px. Content area = 300 - 40 - 10 = 250px. |
| 1b | `box-sizing: content-box` (width:300px, padding:20px, border:5px) | Content area is 300px. Total outer width = 300 + 40 + 10 = 350px. |
| 1c | `max-width: 200px` inside a 500px container | Div does not exceed 200px wide. |
| 1d | `min-height: 300px` with little content | Div is at least 300px tall despite short content. |

---

## Test 02: Overflow

**URL:** `sr://test/02-overflow.html`

| ID | Description | Expected |
|----|-------------|----------|
| 2a | `overflow: hidden` with fixed height (80px) | Text is clipped at the 80px boundary. |
| 2b | `overflow:hidden` + `white-space:nowrap` + `text-overflow:ellipsis` (width:250px) | Long text truncated with "..." at right edge. |
| 2c | `overflow: visible` (default) with fixed height (60px) | Content bleeds past the 60px container boundary. |

---

## Test 03: Inline-Block Layout

**URL:** `sr://test/03-inline-block.html`

| ID | Description | Expected |
|----|-------------|----------|
| 3a | Navigation bar with `display:inline-block` items | List items render horizontally with padding and background. |
| 3b | Inline-block boxes of varying sizes | Boxes wrap to next line when they exceed container width. |
| 3c | Inline-block element next to regular text | Inline-block box sits on the same line as surrounding text. |

---

## Test 04: Positioning

**URL:** `sr://test/04-position.html`

| ID | Description | Expected |
|----|-------------|----------|
| 4a | `position:relative` with `left:50px; top:20px` | Div offset visually; siblings below are NOT shifted. |
| 4b | `position:absolute` inside `position:relative` container | Absolute child positioned at specified offset within container, removed from flow. |
| 4c | z-index stacking (two overlapping positioned divs) | Higher z-index renders on top. |

---

## Test 05: White-Space Modes

**URL:** `sr://test/05-white-space.html`

| ID | Description | Expected |
|----|-------------|----------|
| 5a | `white-space: normal` (default) | Multiple spaces collapsed to one. Newlines become spaces. |
| 5b | `white-space: pre` | All spaces, tabs, and newlines preserved exactly. |
| 5c | `white-space: nowrap` | Whitespace collapsed but text does not wrap to next line. |
| 5d | `white-space: pre-wrap` | Whitespace preserved but text wraps at container edge. |
| 5e | `white-space: pre-line` | Spaces collapsed, newlines preserved as line breaks. |
| 5f | `<pre>` tag | UA stylesheet applies `white-space:pre` + monospace font. |

---

## Test 06: User-Agent Stylesheet

**URL:** `sr://test/06-ua-stylesheet.html`

| ID | Description | Expected |
|----|-------------|----------|
| 6a | `<h1>` through `<h6>` | Graduated sizes (h1=32px to h6=10.72px), all bold. |
| 6b | `<strong>`, `<b>` | Bold text. |
| 6c | `<em>`, `<i>` | Italic text. |
| 6d | `<u>`, `<ins>` | Underlined text. |
| 6e | `<s>`, `<del>` | Strikethrough text. |
| 6f | `<code>`, `<kbd>`, `<samp>` | Monospace font. |
| 6g | `<a>` link | Blue color + underline. |
| 6h | `<mark>` | Yellow background highlight. |
| 6i | `<small>` | Smaller font size. |
| 6j | `<blockquote>` | Indented left and right margins. |
| 6k | `<hr>` | Horizontal line with border. |
| 6l | `<ul>`, `<ol>`, `<li>` | Left padding on lists. |
| 6m | `<p>` paragraphs | Vertical margins between paragraphs. |

---

## Test 07: Scrolling

**URL:** `sr://test/07-scrolling.html`

| ID | Description | Expected |
|----|-------------|----------|
| 7a | Mouse wheel scrolling | Page scrolls. Scrollbar indicator on right edge updates. |
| 7b | Arrow keys (Up/Down) | Scrolls by step (~40px). |
| 7c | Page Up / Page Down / Space | Scrolls by ~90% of viewport height. |
| 7d | Home / End | Scrolls to top / bottom of page. |
| 7e | Scroll-to-top on navigation | Navigate away and back — page starts at top. |

---

## Test 08: Link Navigation

**URL:** `sr://test/08-links.html`

| ID | Description | Expected |
|----|-------------|----------|
| 8a | Click relative links to other test files | Navigates to the test page. Address bar updates. |
| 8b | Link with `target="_blank"` | Opens in a new tab. Current tab unchanged. |
| 8c | Multiple sequential navigation + Back/Forward | History works: Back retraces steps, Forward goes forward. |
| 8d | Link appearance | Links are blue with underline (UA stylesheet). |

---

## Test 09: Keyboard Shortcuts

**URL:** `sr://test/09-keyboard-shortcuts.html`

| ID | Description | Expected |
|----|-------------|----------|
| 9a | Cmd/Ctrl+T | Opens new tab. |
| 9b | Cmd/Ctrl+W | Closes active tab (keeps at least one). |
| 9c | Cmd/Ctrl+Tab / Cmd/Ctrl+Shift+Tab | Switches to next/previous tab. |
| 9d | Cmd/Ctrl+L | Focuses address bar, selects all text. |
| 9e | Escape | Unfocuses address bar. |
| 9f | Cmd/Ctrl+R / F5 | Reloads current page. |
| 9g | F12 / Cmd/Ctrl+Shift+I | Opens/closes Developer Tools. |

---

## Test 10: Text Selection

**URL:** `sr://test/10-text-selection.html`

| ID | Description | Expected |
|----|-------------|----------|
| 10a | Single paragraph selection | Click-and-drag highlights text with blue rectangles. |
| 10b | Multiple paragraphs | Selection spans across paragraph boundaries. |
| 10c | Mixed formatting (bold, italic, etc.) | Selection works across inline formatting elements. |
| 10d | Different font sizes | Selection highlight height adjusts to font size. |
| 10e | Long text precision | Character-level selection accuracy on long text. |

---

## Test 11: Context Menus

**URL:** `sr://test/11-context-menu.html`

| ID | Description | Expected |
|----|-------------|----------|
| 11a | Address bar right-click | Menu: Cut, Copy, Paste, Select All. |
| 11b | Content area right-click (no selection) | Menu: Select All, View Source, Developer Tools. |
| 11c | Content area right-click (with selection) | Menu: Copy, Select All, View Source, Developer Tools. |
| 11d | Menu behavior | Menu closes on click outside. Items highlight on hover. |

---

## Test 12: Developer Tools

**URL:** `sr://test/12-devtools.html`

| ID | Description | Expected |
|----|-------------|----------|
| 12a | Open DevTools (F12 / Cmd+Shift+I / right-click menu) | Separate window opens with console UI. |
| 12b | Console output capture | `console.log/warn/error` from page scripts appear in log. |
| 12c | Console input (JS execution) | Type `1 + 2` → shows `3`. Up/Down for history. |
| 12d | Console DOM manipulation | `document.body.style.backgroundColor = 'yellow'` changes page. |
| 12e | Toggle DevTools | F12 closes the DevTools window. F12 again reopens it. |

---

## Test 13: Timers

**URL:** `sr://test/13-timers.html`

| ID | Description | Expected |
|----|-------------|----------|
| 13a | `setTimeout` (2 second delay) | Status box changes from "Waiting..." to "FIRED!" after ~2s. Background turns green. |
| 13b | `setInterval` (1 second interval) | Counter increments every second up to 10, then stops. |
| 13c | `requestAnimationFrame` (smooth animation) | A box bounces left-to-right smoothly. |

---

## Test 14: DOM Events

**URL:** `sr://test/14-dom-events.html`

| ID | Description | Expected |
|----|-------------|----------|
| 14a | Click event changes text | Click the target div — counter increments on each click. |
| 14b | Event bubbling (parent + child) | Click child — both child and parent logs appear in order. |
| 14c | Add/Remove event listeners | Toggle button enables/disables a click listener on a target. |
| 14d | Event details display | Click in the area — shows event type, target tagName, clientX/Y. |

---

## Test 15: Generators

**URL:** `sr://test/15-generators.html`

| ID | Description | Expected |
|----|-------------|----------|
| 15a | Basic `function*` / `yield` | Displays yielded values: 1, 2, 3, then done. |
| 15b | Fibonacci generator | Displays first N fibonacci numbers. |
| 15c | `for-of` over generator | Collects generator values into an array via for-of. |
| 15d | `generator.return()` early exit | Generator ends early; done becomes true. |

---

## Test 16: Async/Await

**URL:** `sr://test/16-async-await.html`

| ID | Description | Expected |
|----|-------------|----------|
| 16a | Basic async function with `Promise.resolve` | Resolved value displayed in result box. |
| 16b | Multiple sequential `await`s | All three values resolved and displayed in order. |
| 16c | `async/await` with `try/catch` error handling | Caught error message displayed. |
| 16d | Async with `setTimeout` (real delay) | Result appears after the delay completes. |

---

## Test 17: Cross-Feature Integration

**URL:** `sr://test/17-integration.html`

| ID | Description | Expected |
|----|-------------|----------|
| 17a | Card component | `position:relative` container with `position:absolute` badge, `border-box` sizing, `overflow:hidden` clips content. |
| 17b | Two-column inline-block layout | Side-by-side columns using `display:inline-block`. |
| 17c | Overlay banner | Absolute-positioned banner clipped by `overflow:hidden` parent. |

---

## Test 18: External CSS and JS Resources

**URL:** `sr://test/18-external-resources.html`

| ID | Description | Expected |
|----|-------------|----------|
| 18a | External CSS (`<link rel="stylesheet" href="18-style.css">`) | Styles from external file applied (green theme, borders). |
| 18b | External JS (`<script src="18-script.js">`) | Script modifies DOM — replaces text, changes style. |
| 18c | Combined external CSS + JS | JS-created elements are styled by external CSS classes. |

---

## Test 19: HiDPI Rendering

**URL:** *(no HTML file — verify on any test page)*

| ID | Description | Expected |
|----|-------------|----------|
| 19a | Retina text sharpness | Text is crisp on HiDPI displays. Not blurry. |
| 19b | Content scale correctness | Layout dimensions identical at 1x and 2x. |

---

## Execution Checklist

| # | Test | URL | Status | Notes |
|---|------|-----|--------|-------|
| 1a | border-box dimensions | `sr://test/01-box-sizing.html` | Passed |  |
| 1b | content-box dimensions | | Passed |  |
| 1c | max-width constraint | | Passed |  |
| 1d | min-height constraint | | Passed | |
| 2a | overflow:hidden clips | `sr://test/02-overflow.html` | Passed |  |
| 2b | text-overflow:ellipsis | | Passed |  |
| 2c | overflow:visible bleeds | | Passed | |
| 3a | inline-block nav bar | `sr://test/03-inline-block.html` | Passed |  |
| 3b | inline-block wrapping | | Passed | |
| 3c | inline-block next to text | | Passed |  |
| 4a | position:relative | `sr://test/04-position.html` | Passed |  |
| 4b | position:absolute | | Passed |  |
| 4c | z-index stacking | | Passed |  |
| 5a | white-space:normal | `sr://test/05-white-space.html` | Passed | |
| 5b | white-space:pre | | Passed |  |
| 5c | white-space:nowrap | | Passed | |
| 5d | white-space:pre-wrap | | Passed | |
| 5e | white-space:pre-line | | Passed | |
| 5f | `<pre>` tag | | Passed |  |
| 6a-m | UA stylesheet elements | `sr://test/06-ua-stylesheet.html` | Passed |  |
| 7a | wheel scroll | `sr://test/07-scrolling.html` | Passed |  |
| 7b | arrow key scroll | | Passed | |
| 7c | page/space scroll | | Passed | |
| 7d | home/end scroll | | Passed | |
| 7e | scroll reset on nav | | Passed | |
| 8a | relative link click | `sr://test/08-links.html` | Passed | |
| 8b | target=_blank | | Passed | |
| 8c | back/forward history | | Passed |  |
| 8d | link appearance | | Passed |  |
| 9a-g | keyboard shortcuts | `sr://test/09-keyboard-shortcuts.html` | Passed |  |
| 10a | single paragraph select | `sr://test/10-text-selection.html` | Passed |  |
| 10b | multi-paragraph select | | Passed |  |
| 10c | mixed formatting select | | Passed |  |
| 10d | different sizes select | | Passed |  |
| 10e | precision select | | Passed |  |
| 11a | address bar menu | `sr://test/11-context-menu.html` | Passed |  |
| 11b | content menu (no sel) | | Passed |  |
| 11c | content menu (with sel) | | Passed |  |
| 11d | menu behavior | | Passed |  |
| 12a | open devtools | `sr://test/12-devtools.html` | Passed |  |
| 12b | console output | | Passed |  |
| 12c | console input | | Passed | |
| 12d | console DOM | | Passed | |
| 12e | toggle devtools | | Passed |  |
| 13a | setTimeout | `sr://test/13-timers.html` | Passed | |
| 13b | setInterval | | Passed | |
| 13c | requestAnimationFrame | | Passed | |
| 14a | click event | `sr://test/14-dom-events.html` | Passed | |
| 14b | event bubbling | | Passed | |
| 14c | add/remove listener | | Passed | |
| 14d | event details | | Passed |  |
| 15a | basic generator | `sr://test/15-generators.html` | Passed | |
| 15b | fibonacci generator | | Passed | |
| 15c | for-of generator | | Passed | |
| 15d | generator.return() | | Passed | |
| 16a | basic async/await | `sr://test/16-async-await.html` | Passed | |
| 16b | sequential awaits | | Passed | |
| 16c | async try/catch | | Passed | |
| 16d | async + setTimeout | | Passed | |
| 17a | card component | `sr://test/17-integration.html` | Passed |  |
| 17b | two-column layout | | Passed |  |
| 17c | overlay banner | | Passed |  |
| 18a | external CSS | `sr://test/18-external-resources.html` | Passed | |
| 18b | external JS | | Passed | |
| 18c | combined CSS+JS | | Passed | |
| 19a | HiDPI text | *(any page)* | Passed | |
| 19b | content scale | *(any page)* | Passed | |
