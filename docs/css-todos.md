# CSS Feature Gaps vs Latest Spec

Audit of SuperRender against the CSS Snapshot 2023+ and related W3C/WHATWG specifications.
Each section lists what **is** implemented, then what remains.

> Legend: `[P]` = parsed/recognized but not fully applied; `[ ]` = not implemented at all.

*Last updated: 2026-04-12*

---

## 1. Selectors (CSS Selectors Level 4)

**Implemented:** type, class, ID, universal, descendant (` `), child (`>`), adjacent sibling (`+`), general sibling (`~`), comma-separated lists. Attribute selectors: presence (`[attr]`), exact (`[attr="val"]`), whitespace-separated (`[attr~="val"]`), dash-separated (`[attr|="val"]`), prefix (`[attr^="val"]`), suffix (`[attr$="val"]`), substring (`[attr*="val"]`). Tree-structural pseudo-classes: `:root`, `:empty`, `:first-child`, `:last-child`, `:only-child`, `:first-of-type`, `:last-of-type`, `:only-of-type`, `:nth-child(An+B)`, `:nth-last-child(An+B)`. User-action pseudo-classes: `:hover`, `:active`, `:focus`. Link pseudo-classes: `:link`, `:visited`. Logical combinators: `:not()`, `:is()`, `:where()`. Pseudo-elements: `::before`, `::after`.

### Combinators
- [ ] Column combinator (`||`)

### Attribute Selectors
- [ ] Case-insensitive flag (`[attr="val" i]`)
- [ ] Case-sensitive flag (`[attr="val" s]`)

### Pseudo-classes — Tree-structural
- [ ] `:nth-child(An+B of S)` — selector-filtered variant
- [ ] `:nth-of-type(An+B)`
- [ ] `:nth-last-of-type(An+B)`

### Pseudo-classes — User-action
- [ ] `:focus-within`
- [ ] `:focus-visible`

### Pseudo-classes — Link/Location
- [ ] `:any-link`
- [ ] `:target`
- [ ] `:target-within`

### Pseudo-classes — Input
- [ ] `:enabled`, `:disabled`
- [ ] `:checked`
- [ ] `:indeterminate`
- [ ] `:required`, `:optional`
- [ ] `:valid`, `:invalid`
- [ ] `:in-range`, `:out-of-range`
- [ ] `:read-only`, `:read-write`
- [ ] `:placeholder-shown`
- [ ] `:default`
- [ ] `:user-valid`, `:user-invalid`

### Pseudo-classes — Logical combinators
- [ ] `:has(relative-selector-list)`

### Pseudo-classes — Miscellaneous
- [ ] `:lang()`
- [ ] `:dir(ltr|rtl)`
- [ ] `:defined`
- [ ] `:scope`

### Pseudo-elements
- [ ] `::first-line`
- [ ] `::first-letter`
- [ ] `::marker`
- [ ] `::placeholder`
- [ ] `::selection`
- [ ] `::backdrop`
- [ ] `::file-selector-button`

---

## 2. Values and Units (CSS Values and Units Level 4)

**Implemented:** `px`, `em`, `rem`, `pt`, `%`, bare numbers, `auto`, viewport-relative (`vw`, `vh`, `vmin`, `vmax`), `calc()`, `min()`, `max()`, `clamp()` with `+`/`-`/`*`/`/` operators and relative unit support. Global keywords: `initial`, `inherit`, `unset`, `revert`.

### Absolute length units
- [ ] `cm`, `mm`, `in`, `pc`, `Q`

### Relative length units
- [ ] `ex`, `ch`, `lh`, `rlh`
- [ ] `cap`, `ic`
- [ ] Dynamic viewport: `dvw`, `dvh`, `dvmin`, `dvmax`
- [ ] Small viewport: `svw`, `svh`, `svmin`, `svmax`
- [ ] Large viewport: `lvw`, `lvh`, `lvmin`, `lvmax`
- [ ] Container query units: `cqw`, `cqh`, `cqi`, `cqb`, `cqmin`, `cqmax`

### Angle / Time / Frequency / Resolution units
- [ ] `deg`, `grad`, `rad`, `turn`
- [ ] `s`, `ms`
- [ ] `hz`, `khz`
- [ ] `dpi`, `dpcm`, `dppx`

### Math functions
- [ ] `round()`, `mod()`, `rem()`
- [ ] `abs()`, `sign()`
- [ ] `sin()`, `cos()`, `tan()`, `asin()`, `acos()`, `atan()`, `atan2()`
- [ ] `pow()`, `sqrt()`, `hypot()`, `log()`, `exp()`

### Global keywords
- [ ] `revert-layer`

---

## 3. Custom Properties (CSS Custom Properties Level 1)

- [ ] Custom property declarations (`--*: value`)
- [ ] `var()` function with fallback (`var(--name, fallback)`)
- [ ] Cyclic dependency detection
- [ ] Animation of custom properties (CSS Properties and Values API Level 1: `@property`)

---

## 4. Color (CSS Color Level 4)

**Implemented:** hex (#RGB, #RRGGBB, #RRGGBBAA), `rgb()`, `rgba()`, 37+ named colors.

### Color functions
- [ ] `hsl()` / `hsla()`
- [ ] `hwb()`
- [ ] `lab()`, `lch()`
- [ ] `oklch()`, `oklab()`
- [ ] `color()` — sRGB, display-p3, a98-rgb, prophoto-rgb, rec2020, xyz, xyz-d50, xyz-d65
- [ ] `color-mix()`
- [ ] `light-dark()`

### Modern rgb/hsl syntax
- [ ] Space-separated syntax: `rgb(255 0 0)`, `rgb(255 0 0 / 50%)`
- [ ] Percentage alpha: `rgba(0, 0, 0, 50%)`
- [ ] `none` keyword in color components

### Named colors
- [ ] Full CSS Color Level 4 named colors (148 colors — currently ~37 implemented)

### System colors
- [ ] `Canvas`, `CanvasText`, `LinkText`, `VisitedText`, `ActiveText`, `ButtonFace`, `ButtonText`, `Field`, `FieldText`, `Highlight`, `HighlightText`, `SelectedItem`, `SelectedItemText`, `Mark`, `MarkText`, `GrayText`, `AccentColor`, `AccentColorText`

### Color keyword
- [ ] `currentcolor`

---

## 5. Cascade and Inheritance (CSS Cascade Level 5/6)

**Implemented:** specificity, `!important`, source-order sorting, global keywords (`initial`/`inherit`/`unset`/`revert`), user-agent stylesheet. Inherited properties: `color`, `font-size`, `font-family`, `font-weight`, `font-style`, `text-align`, `line-height`, `white-space`, `visibility`, `text-transform`, `letter-spacing`, `word-spacing`, `cursor`, `word-break`, `overflow-wrap`, `list-style-type`.

- [ ] `@import` rule
- [ ] `@layer` (cascade layers)
- [ ] `@scope` (scoped styles)
- [ ] Origin-based cascade (user-agent / user / author)
- [ ] Additional inherited properties: `font-variant`, `border-collapse`, `caption-side`, `direction`, `quotes`, `text-indent`, `tab-size`, `orphans`, `widows`

---

## 6. Box Model (CSS Box Model Level 3/4)

**Implemented:** `width`, `height`, `margin-*`, `padding-*`, `border-*-width`, `box-sizing` (`content-box`/`border-box`), `min-width`, `max-width`, `min-height`, `max-height`.

- [ ] Logical properties: `margin-block-*`, `margin-inline-*`, `padding-block-*`, `padding-inline-*`, `border-block-*-width`, `border-inline-*-width`
- [ ] `inline-size`, `block-size`, `min-inline-size`, `max-inline-size`, `min-block-size`, `max-block-size`
- [ ] Margin collapsing through empty blocks, between parent and first/last child (only basic vertical sibling collapsing exists)

---

## 7. Display (CSS Display Level 3)

**Implemented:** `block`, `inline`, `inline-block`, `flex`, `flow-root`, `none`.

- [ ] `inline-flex`
- [ ] `grid` / `inline-grid`
- [ ] `table` / `inline-table` (and other table display types)
- [ ] `list-item`
- [ ] `contents`
- [ ] Multi-keyword syntax: `block flow`, `inline flow`, `block flex`, `inline flex`, `block grid`, `inline grid`

---

## 8. Positioning (CSS Positioned Layout Level 3)

**Implemented:** `position: static | relative | absolute` with layout application, `top`, `left`, `right`, `bottom`, `z-index` with stacking context.

- [ ] `position: fixed` — viewport-relative positioning
- [ ] `position: sticky` — scroll-aware positioning
- [ ] Inset shorthand: `inset`, `inset-block`, `inset-inline`

---

## 9. Flexbox (CSS Flexible Box Level 1)

**Implemented:** `display: flex`, `flex-direction` (row/row-reverse/column/column-reverse), `flex-wrap` (nowrap/wrap/wrap-reverse), `flex-flow` shorthand, `justify-content` (flex-start/flex-end/center/space-between/space-around/space-evenly), `align-items` (flex-start/flex-end/center/baseline/stretch), `align-self`, `flex-grow`, `flex-shrink`, `flex-basis`, `flex` shorthand, `gap`/`row-gap`/`column-gap`, `order`.

- [ ] `align-content`
- [ ] `inline-flex` display type

---

## 10. Grid (CSS Grid Level 1 / Level 2)

- [ ] `display: grid` / `display: inline-grid`
- [ ] `grid-template-rows`, `grid-template-columns`
- [ ] `grid-template-areas`
- [ ] `grid-template` shorthand
- [ ] `grid-row-start`, `grid-row-end`, `grid-column-start`, `grid-column-end`
- [ ] `grid-row`, `grid-column` shorthands
- [ ] `grid-area` shorthand
- [ ] `grid-auto-rows`, `grid-auto-columns`, `grid-auto-flow`
- [ ] `grid` shorthand
- [ ] `gap`, `row-gap`, `column-gap`
- [ ] `fr` unit
- [ ] `repeat()`, `minmax()`, `auto-fill`, `auto-fit`
- [ ] Named grid lines and areas
- [ ] Implicit grid
- [ ] Subgrid (Level 2)
- [ ] Masonry layout (Level 3, experimental)

---

## 11. Floats and Clear (CSS 2)

- [ ] `float` (`left`, `right`, `none`, `inline-start`, `inline-end`)
- [ ] `clear` (`left`, `right`, `both`, `none`, `inline-start`, `inline-end`)
- [ ] Float containing/clearing (block formatting context)

---

## 12. Overflow (CSS Overflow Level 3)

**Implemented:** `overflow` (`visible`, `hidden`, `scroll`, `auto`), `text-overflow` (`clip`, `ellipsis`). `overflow: hidden` triggers clip regions in painting.

- [ ] `overflow-x`, `overflow-y` (separate axes)
- [ ] `overflow: clip` value
- [ ] `overflow-clip-margin`
- [ ] Scroll container establishment (per-element scrolling)

---

## 13. Backgrounds and Borders (CSS Backgrounds and Borders Level 3)

**Implemented:** `background-color`, `border-width`/`border-color`/`border-style` (single and per-side), `border` shorthand, `border-top`/`border-right`/`border-bottom`/`border-left` shorthands, `border-radius` (shorthand and all four longhand corners).

### Background
- [ ] `background-image` (`url()`, gradients)
- [ ] `background-repeat`
- [ ] `background-position`
- [ ] `background-size`
- [ ] `background-attachment`
- [ ] `background-origin`
- [ ] `background-clip`
- [ ] `background` shorthand (full)
- [ ] Multiple backgrounds

### Gradients
- [ ] `linear-gradient()`, `repeating-linear-gradient()`
- [ ] `radial-gradient()`, `repeating-radial-gradient()`
- [ ] `conic-gradient()`, `repeating-conic-gradient()`

### Box shadow
- [ ] `box-shadow` (offset-x, offset-y, blur, spread, color, inset)

### Border image
- [ ] `border-image` and its longhand sub-properties

---

## 14. Fonts (CSS Fonts Level 4)

**Implemented:** `font-size` (px/em/rem/pt/%/keywords), `font-family` (comma-separated list with generic family fallback), `font-weight` (normal/bold/numeric 1-1000), `font-style` (normal/italic/oblique). Dynamic font atlas with Regular/Bold/Monospace variants. `SystemFontLocator` resolves families from system fonts. `GenericFontFamilies` maps serif/sans-serif/monospace/cursive/fantasy/system-ui to platform defaults. CJK fallback font chains.

- [ ] `font-variant` and its sub-properties
- [ ] `font-stretch`
- [ ] `font` shorthand
- [ ] `@font-face` rule
- [ ] `font-display`
- [ ] `font-feature-settings`
- [ ] `font-variation-settings`
- [ ] Variable fonts (`wght`, `wdth`, `ital`, `slnt`, `opsz` axes)
- [ ] `font-optical-sizing`
- [ ] System font keywords: `caption`, `icon`, `menu`, `message-box`, `small-caption`, `status-bar`

---

## 15. Text (CSS Text Level 3/4)

**Implemented:** `text-align` (left/right/center/justify), `text-transform` (uppercase/lowercase/capitalize/none), `text-decoration-line` (none/underline/overline/line-through), `text-overflow` (clip/ellipsis), `letter-spacing`, `word-spacing`, `white-space` (normal/pre/nowrap/pre-wrap/pre-line), `word-break` (normal/break-all/keep-all), `overflow-wrap` (normal/break-word).

- [ ] `text-align-last`
- [ ] `text-indent`
- [ ] `text-decoration` shorthand (full — only line is currently applied)
- [ ] `text-decoration-color`
- [ ] `text-decoration-style` (`solid`, `double`, `dotted`, `dashed`, `wavy`)
- [ ] `text-decoration-thickness`
- [ ] `text-underline-offset`
- [ ] `text-underline-position`
- [ ] `text-emphasis` and its sub-properties
- [ ] `text-shadow`
- [ ] `white-space-collapse`, `text-wrap` (CSS Text Level 4 replacements)
- [ ] `overflow-wrap: anywhere`
- [ ] `line-break`
- [ ] `hyphens` (`none`, `manual`, `auto`)
- [ ] `tab-size`
- [ ] `text-wrap-mode`, `text-wrap-style` (CSS Text Level 4)

---

## 16. Inline Layout (CSS Inline Level 3)

**Implemented:** basic inline text layout with word wrapping, `line-height` as multiplier.

- [ ] `vertical-align` (`baseline`, `sub`, `super`, `top`, `text-top`, `middle`, `bottom`, `text-bottom`, length, percentage)
- [ ] `dominant-baseline`, `alignment-baseline`
- [ ] `initial-letter`
- [ ] Line box height calculation per spec (strut, inline-level box baselines)

---

## 17. Lists (CSS Lists and Counters Level 3)

**Implemented:** `list-style-type` (basic marker rendering for `disc`/`circle`/`square`/`decimal`/`none`), list item indentation via UA stylesheet.

- [ ] `list-style-type` — additional values (`lower-alpha`, `upper-alpha`, `lower-roman`, `upper-roman`, custom `@counter-style`)
- [ ] `list-style-position` (`inside`, `outside`)
- [ ] `list-style-image`
- [ ] `list-style` shorthand
- [ ] `::marker` pseudo-element styling
- [ ] CSS Counters: `counter-reset`, `counter-increment`, `counter-set`, `counter()`, `counters()`

---

## 18. Tables (CSS 2 + CSS Table Level 3)

- [ ] Table layout algorithm (`table-layout: auto | fixed`)
- [ ] Display types: `table`, `table-row`, `table-cell`, `table-row-group`, `table-header-group`, `table-footer-group`, `table-column`, `table-column-group`, `table-caption`, `inline-table`
- [ ] `border-collapse` (`separate`, `collapse`)
- [ ] `border-spacing`
- [ ] `caption-side`
- [ ] `empty-cells`
- [ ] `vertical-align` in table cells

---

## 19. Transforms (CSS Transforms Level 1/2)

- [ ] `transform` (2D: `translate()`, `translateX()`, `translateY()`, `scale()`, `scaleX()`, `scaleY()`, `rotate()`, `skew()`, `skewX()`, `skewY()`, `matrix()`)
- [ ] `transform` (3D: `translate3d()`, `translateZ()`, `scale3d()`, `scaleZ()`, `rotate3d()`, `rotateX()`, `rotateY()`, `rotateZ()`, `perspective()`, `matrix3d()`)
- [ ] `transform-origin`
- [ ] `transform-style` (`flat`, `preserve-3d`)
- [ ] `perspective`, `perspective-origin`
- [ ] `backface-visibility`
- [ ] Individual transform properties: `translate`, `rotate`, `scale`

---

## 20. Transitions (CSS Transitions Level 1)

- [ ] `transition-property`
- [ ] `transition-duration`
- [ ] `transition-timing-function` (`ease`, `linear`, `ease-in`, `ease-out`, `ease-in-out`, `cubic-bezier()`, `steps()`)
- [ ] `transition-delay`
- [ ] `transition` shorthand
- [ ] `transition-behavior` (Level 2, experimental)

---

## 21. Animations (CSS Animations Level 1)

- [ ] `@keyframes` rule
- [ ] `animation-name`
- [ ] `animation-duration`
- [ ] `animation-timing-function`
- [ ] `animation-delay`
- [ ] `animation-iteration-count`
- [ ] `animation-direction`
- [ ] `animation-fill-mode`
- [ ] `animation-play-state`
- [ ] `animation` shorthand
- [ ] `animation-composition` (Level 2)
- [ ] `animation-timeline` (Scroll-driven Animations)

---

## 22. Filter Effects (CSS Filter Effects Level 1)

- [ ] `filter` (`blur()`, `brightness()`, `contrast()`, `drop-shadow()`, `grayscale()`, `hue-rotate()`, `invert()`, `opacity()`, `saturate()`, `sepia()`, `url()`)
- [ ] `backdrop-filter`

---

## 23. Compositing and Blending (CSS Compositing and Blending Level 1)

- [ ] `mix-blend-mode`
- [ ] `isolation`
- [ ] `background-blend-mode`

---

## 24. Masking (CSS Masking Level 1)

- [ ] `clip-path` (`inset()`, `circle()`, `ellipse()`, `polygon()`, `path()`, `url()`)
- [ ] `mask` and its sub-properties
- [ ] `mask-image`, `mask-mode`, `mask-repeat`, `mask-position`, `mask-clip`, `mask-origin`, `mask-size`

---

## 25. Visual Properties

**Implemented:** `background-color`, `color`, `opacity`, `visibility` (visible/hidden/collapse), `cursor` (auto/default/pointer/text/crosshair/move/not-allowed/grab/grabbing/etc.), `content` (for `::before`/`::after`), border rendering.

- [ ] `pointer-events`
- [ ] `user-select`
- [ ] `resize`
- [ ] `outline`, `outline-color`, `outline-style`, `outline-width`, `outline-offset`
- [ ] `caret-color`
- [ ] `accent-color`
- [ ] `appearance`
- [ ] `object-fit`, `object-position` (for replaced elements)
- [ ] `image-rendering`
- [ ] `quotes`

---

## 26. Containment (CSS Containment Level 2)

- [ ] `contain` (`none`, `strict`, `content`, `size`, `layout`, `style`, `paint`, `inline-size`)
- [ ] `content-visibility`
- [ ] `contain-intrinsic-size`, `contain-intrinsic-width`, `contain-intrinsic-height`

---

## 27. Container Queries (CSS Containment Level 3)

- [ ] `container-type`
- [ ] `container-name`
- [ ] `container` shorthand
- [ ] `@container` rule
- [ ] Container query length units (`cqw`, `cqh`, etc.)

---

## 28. Media Queries (CSS Media Queries Level 4/5)

- [ ] `@media` rule
- [ ] Media types: `all`, `screen`, `print`
- [ ] Width/height features: `width`, `min-width`, `max-width`, `height`, `min-height`, `max-height`
- [ ] Aspect ratio features
- [ ] Resolution features (`dpi`, `dpcm`, `dppx`)
- [ ] `prefers-color-scheme`, `prefers-reduced-motion`, `prefers-contrast`, `prefers-reduced-transparency`
- [ ] `forced-colors`
- [ ] `hover`, `pointer`, `any-hover`, `any-pointer`
- [ ] Range syntax: `(width >= 600px)`
- [ ] Boolean combinators: `and`, `not`, `or`
- [ ] `@media` nesting (CSS Nesting)

---

## 29. At-rules

Currently supported: none.

- [ ] `@import`
- [ ] `@media`
- [ ] `@font-face`
- [ ] `@keyframes`
- [ ] `@supports`
- [ ] `@layer`
- [ ] `@scope`
- [ ] `@container`
- [ ] `@property`
- [ ] `@namespace`
- [ ] `@page` (paged media)
- [ ] `@counter-style`
- [ ] `@font-palette-values`
- [ ] `@color-profile`

---

## 30. CSS Nesting

- [ ] Nesting selectors (`.foo { .bar { ... } }`)
- [ ] `&` nesting selector
- [ ] Nested at-rules (`@media`, `@supports`, etc.)

---

## 31. Logical Properties (CSS Logical Properties and Values Level 1)

- [ ] `margin-block-start`, `margin-block-end`, `margin-inline-start`, `margin-inline-end`
- [ ] `padding-block-start`, `padding-block-end`, `padding-inline-start`, `padding-inline-end`
- [ ] `border-block-*`, `border-inline-*`
- [ ] `inset-block-*`, `inset-inline-*`
- [ ] `inline-size`, `block-size`
- [ ] `text-align: start | end`

---

## 32. Writing Modes (CSS Writing Modes Level 4)

- [ ] `writing-mode` (`horizontal-tb`, `vertical-rl`, `vertical-lr`, `sideways-rl`, `sideways-lr`)
- [ ] `direction` (`ltr`, `rtl`)
- [ ] `unicode-bidi`
- [ ] `text-orientation`
- [ ] `text-combine-upright`

---

## 33. Scroll Snap (CSS Scroll Snap Level 1)

- [ ] `scroll-snap-type`
- [ ] `scroll-snap-align`
- [ ] `scroll-padding`, `scroll-margin`
- [ ] `scroll-snap-stop`

---

## 34. Miscellaneous

- [ ] `will-change`
- [ ] `all` shorthand (resets all properties)
- [ ] `aspect-ratio`
- [ ] `place-items`, `place-content`, `place-self` (shorthands)
- [ ] `touch-action`
- [ ] `overscroll-behavior`
- [ ] `color-scheme`
- [ ] `forced-color-adjust`
- [ ] `print-color-adjust`
- [ ] `math-style`, `math-depth`, `math-shift` (MathML styling)
