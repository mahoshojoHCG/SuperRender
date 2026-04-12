# Manual Test Plan -- CSS Features (Phases 9-14)

Tests for CSS features implemented in Phases 9-14 requiring visual rendering or interactive verification.

## How to Run

```bash
dotnet run --project src/Browser/SuperRender.Browser
```

Then type in the address bar:

```
sr://test/
```

Navigate to the CSS Tests section, or load individual tests directly:

```
sr://test/CSS/01-transforms.html
sr://test/CSS/20-integration.html
```

---

## Test 01: CSS Transforms

**URL:** `sr://test/CSS/01-transforms.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 1a | `translate(30px, 20px)` | Blue box shifted 30px right and 20px down from normal position. | | |
| 1b | `rotate(45deg)` | Blue box rotated 45 degrees clockwise around its center. | | |
| 1c | `scale(1.5)` | Blue box appears 50% larger than normal. | | |
| 1d | `skewX(20deg)` | Blue box skewed horizontally by 20 degrees. | | |
| 1e | Multiple transforms (translate + rotate + scale) | Box is translated, rotated 30deg, and scaled to 80%. | | |
| 1f | `transform-origin: top left` with rotation | Box rotates around its top-left corner. | | |

---

## Test 02: CSS Transitions

**URL:** `sr://test/CSS/02-transitions.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 2a | Color transition on hover (0.5s ease) | Box smoothly changes from red to green on hover. | | |
| 2b | Opacity transition (ease-in) | Box fades to 30% opacity on hover. | | |
| 2c | Transform transition (scale, ease-out) | Box smoothly grows 30% on hover. | | |
| 2d | Multiple property transition (color + translateX) | Box changes color and slides right simultaneously. | | |

---

## Test 03: CSS Animations

**URL:** `sr://test/CSS/03-animations.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 3a | Pulse animation (scale, infinite) | Red box pulses (grows and shrinks) continuously. | | |
| 3b | Slide animation (translateX, linear) | Blue box slides 200px right and repeats. | | |
| 3c | Color cycle animation (alternate) | Box cycles between green, orange, and purple. | | |

---

## Test 04: Timing Functions

**URL:** `sr://test/CSS/04-timing-functions.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 4a | Five timing functions compared | Five colored balls move at different speeds: ease starts slow/ends slow, linear is constant, ease-in starts slow, ease-out ends slow, ease-in-out is symmetric. | | |

---

## Test 05: Text Decoration Styles

**URL:** `sr://test/CSS/05-text-decoration.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 5a | `text-decoration-style: solid` | Standard solid underline. | | |
| 5b | `text-decoration-style: double` | Double-line underline. | | |
| 5c | `text-decoration-style: dotted` | Dotted underline. | | |
| 5d | `text-decoration-style: dashed` | Dashed underline. | | |
| 5e | `text-decoration-style: wavy` with red color | Red wavy underline. | | |
| 5f | `text-decoration-thickness: 3px` | Thick 3px underline. | | |
| 5g | `text-underline-offset: 5px` | Underline offset 5px below text. | | |

---

## Test 06: Text Shadow

**URL:** `sr://test/CSS/06-text-shadow.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 6a | Drop shadow (2px 2px 4px) | Text with subtle gray drop shadow offset down-right. | | |
| 6b | Red outline shadow (-1px -1px) | Text with red shadow creating outline effect. | | |
| 6c | Blue glow (0 0 10px) | Text with blue glow effect around it. | | |

---

## Test 07: Vertical Align

**URL:** `sr://test/CSS/07-vertical-align.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 7a | `vertical-align: baseline` | Inline span aligned to baseline (default). | | |
| 7b | `vertical-align: top` | Span aligned to top of line box. | | |
| 7c | `vertical-align: middle` | Span aligned to middle of line box. | | |
| 7d | `vertical-align: bottom` | Span aligned to bottom of line box. | | |
| 7e | `vertical-align: sub` | Small text positioned as subscript. | | |
| 7f | `vertical-align: super` | Small text positioned as superscript. | | |

---

## Test 08: List Styles

**URL:** `sr://test/CSS/08-list-styles.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 8a | `list-style-type: lower-alpha` | Items numbered a. b. c. | | |
| 8b | `list-style-type: upper-alpha` | Items numbered A. B. C. | | |
| 8c | `list-style-type: lower-roman` | Items numbered i. ii. iii. | | |
| 8d | `list-style-type: upper-roman` | Items numbered I. II. III. | | |
| 8e | `list-style-position: inside` | Marker inside the content area (red background). | | |
| 8f | `list-style-position: outside` | Marker outside the content area (green background). | | |

---

## Test 09: CSS Counters

**URL:** `sr://test/CSS/09-counters.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 9a | Automatic chapter/subsection numbering | "Chapter 1:" and "Chapter 2:" headings with "1.1", "1.2", "2.1", "2.2", "2.3" subsections. | | |

---

## Test 10: Font Shorthand

**URL:** `sr://test/CSS/10-font-shorthand.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 10a | `font: 20px Arial` | 20px Arial text. | | |
| 10b | `font: bold 16px sans-serif` | Bold 16px sans-serif text. | | |
| 10c | `font: italic bold 18px serif` | Italic bold 18px serif text. | | |
| 10d | `font: small-caps 14px monospace` | Small-caps monospace text. | | |
| 10e | `font: italic 24px/1.5 Georgia` | Italic 24px with 1.5 line-height. | | |

---

## Test 11: Grid Basic

**URL:** `sr://test/CSS/11-grid-basic.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 11a | 2-column grid (1fr 1fr) | 4 colored boxes in 2x2 grid, equal width columns. | | |
| 11b | 3-column grid (1fr 1fr 1fr) | 6 colored boxes in 2x3 grid, equal width columns. | | |
| 11c | Fixed-width columns (100px 200px 100px) | 3 boxes with center column twice as wide. | | |

---

## Test 12: Grid Placement

**URL:** `sr://test/CSS/12-grid-placement.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 12a | Grid with column span and explicit placement | Red item spans 2 columns, green placed at column 3, blue spans 2 rows. | | |

---

## Test 13: Grid FR Units

**URL:** `sr://test/CSS/13-grid-fr-units.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 13a | Equal columns (1fr 1fr 1fr) | Three equal-width columns. | | |
| 13b | Ratio columns (1fr 2fr 1fr) | Center column twice as wide as sides. | | |
| 13c | Mixed (200px 1fr 1fr) | First column fixed 200px, remaining split equally. | | |

---

## Test 14: Float Layout

**URL:** `sr://test/CSS/14-float-layout.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 14a | `float: left` | Blue box on left, text wraps around it. | | |
| 14b | `float: right` | Red box on right, text wraps around it. | | |
| 14c | `clear: both` | Paragraph appears below both floated boxes. | | |

---

## Test 15: Table Layout

**URL:** `sr://test/CSS/15-table-layout.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 15a | CSS table with border-collapse | Table with collapsed borders, header row has gray background. | | |
| 15b | Fixed table layout with border-spacing | Fixed-width table with 5px spacing between cells. | | |

---

## Test 16: Filters

**URL:** `sr://test/CSS/16-filters.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 16a | Various filter functions | Normal box plus 9 filtered variants: blur, brightness, contrast, grayscale, sepia, invert, hue-rotate, saturate, and multi-filter. Each should show a distinct visual effect. | | |

---

## Test 17: Blend Modes

**URL:** `sr://test/CSS/17-blend-modes.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 17a | `mix-blend-mode` variations | Blue overlay on red background with different blend modes: normal (opaque blue), multiply (darker), screen (lighter), overlay, difference. | | |

---

## Test 18: Clip Path

**URL:** `sr://test/CSS/18-clip-path.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 18a | `clip-path: circle(50%)` | Red square clipped to a circle. | | |
| 18b | `clip-path: ellipse(...)` | Blue square clipped to an ellipse. | | |
| 18c | `clip-path: polygon(...)` (triangle) | Green square clipped to a triangle. | | |
| 18d | `clip-path: inset(...)` | Orange square with inset rectangular clip. | | |

---

## Test 19: Writing Modes

**URL:** `sr://test/CSS/19-writing-modes.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 19a | `writing-mode: horizontal-tb` | Normal horizontal left-to-right text. | | |
| 19b | `writing-mode: vertical-rl` | Vertical text flowing right-to-left. | | |
| 19c | `writing-mode: vertical-lr` | Vertical text flowing left-to-right. | | |
| 19d | `direction: rtl` | Right-to-left text direction. | | |

---

## Test 20: Integration

**URL:** `sr://test/CSS/20-integration.html`

| ID | Description | Expected | Passed | Notes |
|----|-------------|----------|--------|-------|
| 20a | Dashboard layout combining grid, flex, transitions, transforms | Flexbox navigation bar, hero section, 4-column stats grid, 3-column card grid with hover transitions. All elements properly positioned with consistent spacing. | | |

---

## Execution Checklist

| # | Test | URL | Passed | Notes |
|---|------|-----|--------|-------|
| 1a | translate(30px, 20px) | `sr://test/CSS/01-transforms.html` | | |
| 1b | rotate(45deg) | | | |
| 1c | scale(1.5) | | | |
| 1d | skewX(20deg) | | | |
| 1e | multiple transforms | | | |
| 1f | transform-origin | | | |
| 2a | color transition | `sr://test/CSS/02-transitions.html` | | |
| 2b | opacity transition | | | |
| 2c | transform transition | | | |
| 2d | multi-property transition | | | |
| 3a | pulse animation | `sr://test/CSS/03-animations.html` | | |
| 3b | slide animation | | | |
| 3c | color cycle animation | | | |
| 4a | timing functions compared | `sr://test/CSS/04-timing-functions.html` | | |
| 5a | solid underline | `sr://test/CSS/05-text-decoration.html` | | |
| 5b | double underline | | | |
| 5c | dotted underline | | | |
| 5d | dashed underline | | | |
| 5e | wavy underline | | | |
| 5f | thick underline | | | |
| 5g | underline offset | | | |
| 6a | drop shadow | `sr://test/CSS/06-text-shadow.html` | | |
| 6b | outline shadow | | | |
| 6c | glow shadow | | | |
| 7a | baseline | `sr://test/CSS/07-vertical-align.html` | | |
| 7b | top | | | |
| 7c | middle | | | |
| 7d | bottom | | | |
| 7e | sub | | | |
| 7f | super | | | |
| 8a | lower-alpha | `sr://test/CSS/08-list-styles.html` | | |
| 8b | upper-alpha | | | |
| 8c | lower-roman | | | |
| 8d | upper-roman | | | |
| 8e | position inside | | | |
| 8f | position outside | | | |
| 9a | counters | `sr://test/CSS/09-counters.html` | | |
| 10a | font 20px Arial | `sr://test/CSS/10-font-shorthand.html` | | |
| 10b | font bold 16px | | | |
| 10c | font italic bold 18px | | | |
| 10d | font small-caps 14px | | | |
| 10e | font italic 24px/1.5 | | | |
| 11a | 2-column grid | `sr://test/CSS/11-grid-basic.html` | | |
| 11b | 3-column grid | | | |
| 11c | fixed-width columns | | | |
| 12a | grid placement | `sr://test/CSS/12-grid-placement.html` | | |
| 13a | equal fr | `sr://test/CSS/13-grid-fr-units.html` | | |
| 13b | ratio fr | | | |
| 13c | mixed px/fr | | | |
| 14a | float left | `sr://test/CSS/14-float-layout.html` | | |
| 14b | float right | | | |
| 14c | clear both | | | |
| 15a | border-collapse | `sr://test/CSS/15-table-layout.html` | | |
| 15b | border-spacing | | | |
| 16a | filter functions | `sr://test/CSS/16-filters.html` | | |
| 17a | blend modes | `sr://test/CSS/17-blend-modes.html` | | |
| 18a | clip circle | `sr://test/CSS/18-clip-path.html` | | |
| 18b | clip ellipse | | | |
| 18c | clip polygon | | | |
| 18d | clip inset | | | |
| 19a | horizontal-tb | `sr://test/CSS/19-writing-modes.html` | | |
| 19b | vertical-rl | | | |
| 19c | vertical-lr | | | |
| 19d | direction rtl | | | |
| 20a | integration dashboard | `sr://test/CSS/20-integration.html` | | |
