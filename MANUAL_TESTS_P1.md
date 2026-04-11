# Manual Test Plan — P1 (Advanced Features)

Tests for P1 priority features requiring visual rendering or interactive verification.

## How to Run

```bash
dotnet run --project src/Browser/SuperRender.Browser
```

Then type in the address bar:

```
sr://test/
```

Navigate to the P1 Tests section, or load individual tests directly:

```
sr://test/P1/01-selectors.html
sr://test/P1/18-integration.html
```

---

## Test 01: CSS Selectors

**URL:** `sr://test/P1/01-selectors.html`

| ID | Description | Expected |
|----|-------------|----------|
| 1a | Adjacent sibling combinator (`h2 + p`) | Only the first paragraph immediately after h2 is red. Second paragraph stays default. |
| 1b | General sibling combinator (`h2 ~ p`) | All p elements that are siblings after h2 are blue, regardless of intervening elements. |
| 1c | Attribute selectors (`[type="text"]`, `[data-role]`) | Element with [type="text"] has green border. Element with [data-role] has yellow background. |
| 1d | Structural pseudo-classes (`:first-child`, `:last-child`, `:nth-child(odd)`) | First item green bg, last item red bg, odd items (1,3,5) have blue left border. |
| 1e | `:not(.excluded)` pseudo-class | Paragraphs without class "excluded" are red. Excluded paragraph stays dark. |
| 1f | `:is(h1, h2, h3)` pseudo-class | All h1, h2, h3 headings are italic purple. Paragraph is unaffected. |

---

## Test 02: Flexbox Layout

**URL:** `sr://test/P1/02-flexbox.html`

| ID | Description | Expected |
|----|-------------|----------|
| 2a | `flex-direction: row` | Three colored boxes (R,G,B) arranged horizontally in a row. |
| 2b | `flex-direction: column` | Three colored boxes stacked vertically in a 200px wide column. |
| 2c | `justify-content` variations (space-between, center, space-around) | space-between: items at edges. center: grouped in middle. space-around: equal space around each. |
| 2d | `align-items: center` and `stretch` | center: items vertically centered in 120px container. stretch: items fill container height. |
| 2e | `flex-grow` proportional sizing (1x, 2x, 3x) | Blue (3x) three times as wide as Red (1x). Green (2x) twice as wide as Red. |
| 2f | `gap: 20px` between items | 20px gap between boxes. No gap before first or after last. |

---

## Test 03: Border Radius

**URL:** `sr://test/P1/03-border-radius.html`

| ID | Description | Expected |
|----|-------------|----------|
| 3a | `border-radius: 10px` (all corners) | Blue box with evenly rounded corners on all four sides. |
| 3b | `border-radius: 50%` on a 100x100 square | A perfect red circle. |
| 3c | Per-corner radius (TL:0, TR:20, BR:0, BL:40) | Green box with sharp TL/BR and rounded TR(20px)/BL(40px). |
| 3d | Rounded button (border-radius: 25px) | Purple capsule-shaped button. |
| 3e | Rounded corners with visible border | Light yellow box with gold border that follows the 15px curve. |
| 3f | Pill shape (border-radius: 999px) | Teal pill/capsule with fully semicircular left and right ends. |

---

## Test 04: Opacity and Visibility

**URL:** `sr://test/P1/04-opacity-visibility.html`

| ID | Description | Expected |
|----|-------------|----------|
| 4a | `opacity: 0.5` vs `opacity: 1` | Second red box appears faded/translucent compared to the full-opacity first box. |
| 4b | `opacity: 0` (invisible but occupies space) | Visible gap between markers. Blue box is invisible but takes 80px height. |
| 4c | `visibility: hidden` (invisible, occupies space) | Visible gap between markers. Green box invisible but takes layout space. |
| 4d | Nested opacity (parent 0.5, child 0.5 = 0.25 effective) | Outer red at 50%. Inner blue very faded at 25% effective (0.5 * 0.5). |

---

## Test 05: Colors

**URL:** `sr://test/P1/05-colors.html`

| ID | Description | Expected |
|----|-------------|----------|
| 5a | HSL color boxes: hsl(0/120/240, 100%, 50%) | Three boxes: pure red, pure green, pure blue using HSL notation. |
| 5b | Named colors (rebeccapurple, coral, darkcyan, tomato, gold, steelblue) | Six boxes showing correct named CSS colors. |
| 5c | `currentcolor` on border | First box: red text + red border. Second box: blue text + blue border. Border matches text color. |
| 5d | Space-separated `rgb(255 128 0)` | Orange box using space-separated rgb syntax. |

---

## Test 06: calc() and Viewport Units

**URL:** `sr://test/P1/06-calc-viewport.html`

| ID | Description | Expected |
|----|-------------|----------|
| 6a | `width: calc(100% - 40px)` in 500px container | Blue box is 460px wide (500 - 40). Slightly narrower than container. |
| 6b | `width: 50vw` | Red box is exactly half the viewport width. Changes on resize. |
| 6c | `height: 25vh` | Green box height is one quarter of viewport height. Changes on resize. |
| 6d | `width: clamp(200px, 50%, 400px)` | Purple box width clamped between 200px and 400px, preferring 50%. |

---

## Test 07: Text Properties

**URL:** `sr://test/P1/07-text-properties.html`

| ID | Description | Expected |
|----|-------------|----------|
| 7a | `text-transform`: uppercase, lowercase, capitalize | uppercase: ALL CAPS. lowercase: all lowercase. capitalize: First Letter Capitalized. |
| 7b | `letter-spacing: 5px` | Visibly wider gaps between each individual character. |
| 7c | `word-spacing: 10px` | Visibly wider gaps between words; character spacing within words normal. |
| 7d | Combined uppercase + letter-spacing: 3px | Text is ALL CAPS with extra letter spacing. |

---

## Test 08: Pseudo-elements

**URL:** `sr://test/P1/08-pseudo-elements.html`

| ID | Description | Expected |
|----|-------------|----------|
| 8a | `::before` with `content: ">>> "` | Red bold ">>> " generated before each paragraph's content. |
| 8b | `::after` with `content: " <<<"` | Blue bold " <<<" generated after each paragraph's content. |
| 8c | Styled pseudo-element (different color/size) | Orange bold "NOTE: " (14px) before text. Grey "[end]" (12px) after text. Main text 18px. |
| 8d | Combined with class selectors | .important items get red "* " prefix. .note items get blue "- " prefix. Plain items no prefix. |

---

## Test 09: Global Keywords

**URL:** `sr://test/P1/09-global-keywords.html`

| ID | Description | Expected |
|----|-------------|----------|
| 9a | `color: initial` | Middle paragraph is black (initial value) while siblings are red (inherited). |
| 9b | `width: inherit` | Child box (red border) same width as parent box (blue border), both 300px. |
| 9c | `color: unset` on inherited property | Paragraph inside green parent is also green. unset on inherited = inherit. |
| 9d | `background-color: unset` on non-inherited property | Child has transparent background. Parent pink shows through. unset on non-inherited = initial. |

---

## Test 10: Hover and Active States

**URL:** `sr://test/P1/10-hover-active.html`

| ID | Description | Expected |
|----|-------------|----------|
| 10a | Div `:hover` background change | Light blue box turns dark blue with white text on hover. Reverts on leave. |
| 10b | Link `:hover` color change | Blue link turns red on hover. Reverts on leave. |
| 10c | Button `:active` state | Grey button goes lighter on hover, very dark on active (click+hold). |
| 10d | Multiple independent hover targets | Each card highlights independently on hover (blue border, light blue bg). |

---

## Test 11: Cookies and Storage

**URL:** `sr://test/P1/11-cookies-storage.html`

| ID | Description | Expected |
|----|-------------|----------|
| 11a | `localStorage` set/get/clear | Set stores 3 key-value pairs. Get reads them back. Clear removes all. |
| 11b | `sessionStorage` set/get | Stores and retrieves session-specific data. Does not persist after tab close. |
| 11c | `document.cookie` set/read | Sets cookies via `document.cookie`. Reads back cookie string. |

---

## Test 12: Fetch API

**URL:** `sr://test/P1/12-fetch.html`

| ID | Description | Expected |
|----|-------------|----------|
| 12a | Basic `fetch()` to httpbin.org | Output shows HTTP status and response body (or error if fetch not implemented). |
| 12b | `fetch()` with error handling (invalid URL) | Shows caught error message for failed network request. |
| 12c | `fetch()` the current test page | Shows the HTML source of the test page itself. |

---

## Test 13: Location and History

**URL:** `sr://test/P1/13-location-history.html`

| ID | Description | Expected |
|----|-------------|----------|
| 13a | `window.location` properties | Displays href, protocol, hostname, pathname, search, hash. |
| 13b | `history.pushState()` | Clicking buttons updates URL without navigation. history.length increases. |
| 13c | `history.back()` / `history.forward()` | Back returns to previous pushState. Forward goes forward. URL updates accordingly. |

---

## Test 14: Tree Construction

**URL:** `sr://test/P1/14-tree-construction.html`

| ID | Description | Expected |
|----|-------------|----------|
| 14a | `<p>` auto-closes before block `<div>` | The div is a sibling of the first p, not a child. DOM output confirms structure. |
| 14b | Multiple `<li>` without closing tags | Three separate list items created. Each auto-closed by the next. |
| 14c | `<dd>` and `<dt>` auto-closing | dl has 4 children (dt, dd, dt, dd). Auto-closing produces correct structure. |
| 14d | Summary verification | JS output confirms all tree construction tests produce correct DOM. |

---

## Test 15: DOM Methods

**URL:** `sr://test/P1/15-dom-methods.html`

| ID | Description | Expected |
|----|-------------|----------|
| 15a | `cloneNode(true)` (deep clone) | Each click creates a clone of the source paragraph. Clone count increments. |
| 15b | `matches()` and `closest()` | matches('span') true, matches('div') false. closest('div') finds parent. closest('body') finds body. |
| 15c | `dataset` attribute access | Reads data-color, data-size, data-item-count via dataset property (camelCase). |
| 15d | `toggleAttribute('hidden')` | Each click toggles the hidden attribute on/off. Output shows current state. |
| 15e | `element.remove()` | Removes the blue box from DOM. Subsequent clicks report element already gone. |

---

## Test 16: HTML Entities

**URL:** `sr://test/P1/16-entities.html`

| ID | Description | Expected |
|----|-------------|----------|
| 16a | Common entities: `&amp;` `&lt;` `&gt;` `&quot;` `&apos;` `&nbsp;` | Each renders as correct character. |
| 16b | Extended: `&mdash;` `&copy;` `&reg;` `&trade;` `&hellip;` `&laquo;` `&raquo;` | Typographic characters render correctly. |
| 16c | Mathematical: `&sum;` `&infin;` `&pi;` `&times;` `&divide;` `&plusmn;` | Math symbols render correctly. |
| 16d | Greek: `&Alpha;` `&Beta;` `&Gamma;` `&Delta;` `&Omega;` `&alpha;` `&beta;` | Greek letters (upper and lower) render correctly. |
| 16e | Arrows: `&larr;` `&rarr;` `&uarr;` `&darr;` `&harr;` | Arrow characters render in each direction. |

---

## Test 17: JS Labels

**URL:** `sr://test/P1/17-labels.html`

| ID | Description | Expected |
|----|-------------|----------|
| 17a | `break` with label (exits outer loop) | Shows (0,0), (0,1), (0,2), (1,0), then breaks at i=1, j=1. |
| 17b | `continue` with label (skips to next outer iteration) | When j=1, skips rest of inner loop. Shows (0,0), (1,0), (2,0). |
| 17c | Labeled block with `break` | Shows "Before block", "Inside block, before break", "After block". Does NOT show "after break". |
| 17d | Nested labels (three loops, two labels) | Correctly targets break/continue at specified nesting level. |

---

## Test 18: P1 Integration

**URL:** `sr://test/P1/18-integration.html`

| ID | Description | Expected |
|----|-------------|----------|
| 18a | Card layout (flexbox + border-radius + hover) | Three cards side-by-side via flexbox. Rounded corners. Colored headers. Pill badges. Blue border on hover. |
| 18b | HSL color palette with border-radius | Eight rainbow color swatches in a flexbox row. Each swatch has rounded corners. |
| 18c | Interactive hover button + dynamic content | Purple button inverts on hover. Clicking adds styled items to the dashed area. |

---

## Execution Checklist

| # | Test | URL | Status | Notes |
|---|------|-----|--------|-------|
| 1a | adjacent sibling `+` | `sr://test/P1/01-selectors.html` | | |
| 1b | general sibling `~` | | | |
| 1c | attribute selectors | | | |
| 1d | structural pseudo-classes | | | |
| 1e | :not() | | | |
| 1f | :is() | | | |
| 2a | flex-direction: row | `sr://test/P1/02-flexbox.html` | | |
| 2b | flex-direction: column | | | |
| 2c | justify-content | | | |
| 2d | align-items | | | |
| 2e | flex-grow | | | |
| 2f | gap | | | |
| 3a | border-radius uniform | `sr://test/P1/03-border-radius.html` | | |
| 3b | border-radius circle | | | |
| 3c | per-corner radius | | | |
| 3d | rounded button | | | |
| 3e | rounded with border | | | |
| 3f | pill shape | | | |
| 4a | opacity: 0.5 | `sr://test/P1/04-opacity-visibility.html` | | |
| 4b | opacity: 0 | | | |
| 4c | visibility: hidden | | | |
| 4d | nested opacity | | | |
| 5a | HSL colors | `sr://test/P1/05-colors.html` | | |
| 5b | named colors | | | |
| 5c | currentcolor | | | |
| 5d | rgb space syntax | | | |
| 6a | calc(100% - 40px) | `sr://test/P1/06-calc-viewport.html` | | |
| 6b | 50vw | | | |
| 6c | 25vh | | | |
| 6d | clamp() | | | |
| 7a | text-transform | `sr://test/P1/07-text-properties.html` | | |
| 7b | letter-spacing | | | |
| 7c | word-spacing | | | |
| 7d | combined | | | |
| 8a | ::before | `sr://test/P1/08-pseudo-elements.html` | | |
| 8b | ::after | | | |
| 8c | styled pseudo-element | | | |
| 8d | combined selectors | | | |
| 9a | initial | `sr://test/P1/09-global-keywords.html` | | |
| 9b | inherit | | | |
| 9c | unset (inherited) | | | |
| 9d | unset (non-inherited) | | | |
| 10a | div hover | `sr://test/P1/10-hover-active.html` | | |
| 10b | link hover | | | |
| 10c | button active | | | |
| 10d | multiple hover targets | | | |
| 11a | localStorage | `sr://test/P1/11-cookies-storage.html` | | |
| 11b | sessionStorage | | | |
| 11c | document.cookie | | | |
| 12a | basic fetch | `sr://test/P1/12-fetch.html` | | |
| 12b | fetch error handling | | | |
| 12c | fetch self | | | |
| 13a | window.location | `sr://test/P1/13-location-history.html` | | |
| 13b | pushState | | | |
| 13c | back/forward | | | |
| 14a | p auto-close | `sr://test/P1/14-tree-construction.html` | | |
| 14b | li auto-close | | | |
| 14c | dt/dd auto-close | | | |
| 14d | summary | | | |
| 15a | cloneNode | `sr://test/P1/15-dom-methods.html` | | |
| 15b | matches/closest | | | |
| 15c | dataset | | | |
| 15d | toggleAttribute | | | |
| 15e | remove() | | | |
| 16a | common entities | `sr://test/P1/16-entities.html` | | |
| 16b | extended entities | | | |
| 16c | math entities | | | |
| 16d | greek entities | | | |
| 16e | arrow entities | | | |
| 17a | break label | `sr://test/P1/17-labels.html` | | |
| 17b | continue label | | | |
| 17c | labeled block | | | |
| 17d | nested labels | | | |
| 18a | card layout | `sr://test/P1/18-integration.html` | | |
| 18b | color palette | | | |
| 18c | interactive button | | | |
