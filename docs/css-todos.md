# CSS Feature Gaps vs Latest Spec

Audit of SuperRender.Core against the CSS Snapshot 2023+ and related W3C/WHATWG specifications.
Features already implemented are **not** listed here. See CLAUDE.md for the current implementation summary.

> Legend: `[P]` = parsed/recognized but not applied in layout/paint; `[ ]` = not implemented at all.

---

## 1. Selectors (CSS Selectors Level 4)

Currently supported: type, class, ID, universal, descendant (` `), child (`>`), comma-separated lists.

### Combinators
- [ ] Adjacent sibling combinator (`+`)
- [ ] General sibling combinator (`~`)
- [ ] Column combinator (`||`)

### Attribute Selectors
- [ ] Attribute presence (`[attr]`)
- [ ] Exact match (`[attr="val"]`)
- [ ] Whitespace-separated match (`[attr~="val"]`)
- [ ] Dash-separated match (`[attr|="val"]`)
- [ ] Prefix match (`[attr^="val"]`)
- [ ] Suffix match (`[attr$="val"]`)
- [ ] Substring match (`[attr*="val"]`)
- [ ] Case-insensitive flag (`[attr="val" i]`)
- [ ] Case-sensitive flag (`[attr="val" s]`)

### Pseudo-classes — Tree-structural
- [ ] `:root`
- [ ] `:empty`
- [ ] `:first-child`
- [ ] `:last-child`
- [ ] `:only-child`
- [ ] `:first-of-type`
- [ ] `:last-of-type`
- [ ] `:only-of-type`
- [ ] `:nth-child(An+B [of S]?)`
- [ ] `:nth-last-child(An+B [of S]?)`
- [ ] `:nth-of-type(An+B)`
- [ ] `:nth-last-of-type(An+B)`

### Pseudo-classes — User-action
- [ ] `:hover`
- [ ] `:active`
- [ ] `:focus`
- [ ] `:focus-within`
- [ ] `:focus-visible`

### Pseudo-classes — Link/Location
- [ ] `:link`
- [ ] `:visited`
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

### Pseudo-classes — Logical combinators (Selectors Level 4)
- [ ] `:not(selector-list)`
- [ ] `:is(selector-list)`
- [ ] `:where(selector-list)`
- [ ] `:has(relative-selector-list)`

### Pseudo-classes — Miscellaneous
- [ ] `:lang()`
- [ ] `:dir(ltr|rtl)`
- [ ] `:defined`
- [ ] `:scope`

### Pseudo-elements
- [ ] `::before`
- [ ] `::after`
- [ ] `::first-line`
- [ ] `::first-letter`
- [ ] `::marker`
- [ ] `::placeholder`
- [ ] `::selection`
- [ ] `::backdrop`
- [ ] `::file-selector-button`

---

## 2. Values and Units (CSS Values and Units Level 4)

Currently supported: `px`, `em`, `rem`, `pt`, `%`, bare numbers, `auto`.

### Absolute length units
- [ ] `cm`, `mm`, `in`, `pc`, `Q`

### Relative length units
- [ ] `ex`, `ch`, `lh`, `rlh`
- [ ] `cap`, `ic`
- [ ] Viewport-relative: `vw`, `vh`, `vmin`, `vmax`
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
- [ ] `calc()`
- [ ] `min()`, `max()`, `clamp()`
- [ ] `round()`, `mod()`, `rem()`
- [ ] `abs()`, `sign()`
- [ ] `sin()`, `cos()`, `tan()`, `asin()`, `acos()`, `atan()`, `atan2()`
- [ ] `pow()`, `sqrt()`, `hypot()`, `log()`, `exp()`

### Global keywords
- [ ] `initial`
- [ ] `inherit`
- [ ] `unset`
- [ ] `revert`
- [ ] `revert-layer`

---

## 3. Custom Properties (CSS Custom Properties Level 1)

- [ ] Custom property declarations (`--*: value`)
- [ ] `var()` function with fallback (`var(--name, fallback)`)
- [ ] Cyclic dependency detection
- [ ] Animation of custom properties (CSS Properties and Values API Level 1: `@property`)

---

## 4. Color (CSS Color Level 4)

Currently supported: hex (#RGB, #RRGGBB, #RRGGBBAA), `rgb()`, `rgba()`, 37 named colors.

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
- [ ] Full CSS Color Level 4 named colors (148 colors — currently only 37 implemented)

### System colors
- [ ] `Canvas`, `CanvasText`, `LinkText`, `VisitedText`, `ActiveText`, `ButtonFace`, `ButtonText`, `Field`, `FieldText`, `Highlight`, `HighlightText`, `SelectedItem`, `SelectedItemText`, `Mark`, `MarkText`, `GrayText`, `AccentColor`, `AccentColorText`

### Color keyword
- [ ] `currentcolor`

---

## 5. Cascade and Inheritance (CSS Cascade Level 5/6)

Currently supported: specificity, `!important`, source-order sorting, 5 inherited properties.

- [ ] `@import` rule
- [ ] `@layer` (cascade layers)
- [ ] `@scope` (scoped styles)
- [ ] Origin-based cascade (user-agent / user / author)
- [ ] Full inherited property list (spec defines 40+; only 5 implemented: `color`, `font-size`, `font-family`, `text-align`, `line-height`)
- [ ] Additional inherited properties: `font-weight`, `font-style`, `font-variant`, `letter-spacing`, `word-spacing`, `white-space`, `visibility`, `cursor`, `list-style-*`, `border-collapse`, `caption-side`, `direction`, `quotes`, `text-indent`, `text-transform`, `word-break`, `overflow-wrap`, `tab-size`, `orphans`, `widows`

---

## 6. Box Model (CSS Box Model Level 3/4)

Currently supported: `width`, `height`, `margin-*`, `padding-*`, `border-*-width`.

- [ ] `box-sizing` (`content-box`, `border-box`)
- [ ] `min-width`, `max-width`
- [ ] `min-height`, `max-height`
- [ ] Logical properties: `margin-block-*`, `margin-inline-*`, `padding-block-*`, `padding-inline-*`, `border-block-*-width`, `border-inline-*-width`
- [ ] `inline-size`, `block-size`, `min-inline-size`, `max-inline-size`, `min-block-size`, `max-block-size`
- [ ] Margin collapsing through empty blocks, between parent and first/last child (only basic vertical sibling collapsing exists)

---

## 7. Display (CSS Display Level 3)

Currently supported: `block`, `inline`, `none`.

- [ ] `inline-block`
- [ ] `flex` / `inline-flex`
- [ ] `grid` / `inline-grid`
- [ ] `table` / `inline-table` (and other table display types)
- [ ] `flow-root`
- [ ] `list-item`
- [ ] `contents`
- [ ] Multi-keyword syntax: `block flow`, `inline flow`, `block flex`, `inline flex`, `block grid`, `inline grid`

---

## 8. Positioning (CSS Positioned Layout Level 3)

Currently supported: `position: static | relative | absolute` (parsed but **not applied** in layout), `top`, `left`, `right`, `bottom` (parsed but not applied).

- [P] `position: relative` — offset calculation
- [P] `position: absolute` — out-of-flow positioning with containing block resolution
- [ ] `position: fixed` — viewport-relative positioning
- [ ] `position: sticky` — scroll-aware positioning
- [ ] Containing block resolution for positioned elements
- [ ] `z-index` and stacking context creation
- [ ] Inset shorthand: `inset`, `inset-block`, `inset-inline`

---

## 9. Flexbox (CSS Flexible Box Level 1)

- [ ] `display: flex` / `display: inline-flex`
- [ ] `flex-direction` (`row`, `row-reverse`, `column`, `column-reverse`)
- [ ] `flex-wrap` (`nowrap`, `wrap`, `wrap-reverse`)
- [ ] `flex-flow` shorthand
- [ ] `justify-content` (`flex-start`, `flex-end`, `center`, `space-between`, `space-around`, `space-evenly`)
- [ ] `align-items` (`flex-start`, `flex-end`, `center`, `baseline`, `stretch`)
- [ ] `align-self`
- [ ] `align-content`
- [ ] `order`
- [ ] `flex-grow`, `flex-shrink`, `flex-basis`
- [ ] `flex` shorthand
- [ ] `gap`, `row-gap`, `column-gap`

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

- [ ] `overflow`, `overflow-x`, `overflow-y` (`visible`, `hidden`, `clip`, `scroll`, `auto`)
- [ ] `overflow-clip-margin`
- [ ] `text-overflow` (`clip`, `ellipsis`)
- [ ] Scroll container establishment

---

## 13. Backgrounds and Borders (CSS Backgrounds and Borders Level 3)

Currently supported: `background-color`, `border-width`, `border-color`, `border-style` (as single values), `border` shorthand.

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

### Border per-side style/color
- [ ] `border-top-style`, `border-right-style`, `border-bottom-style`, `border-left-style`
- [ ] `border-top-color`, `border-right-color`, `border-bottom-color`, `border-left-color`
- [ ] `border-top`, `border-right`, `border-bottom`, `border-left` shorthands

### Border radius
- [ ] `border-radius`
- [ ] `border-top-left-radius`, `border-top-right-radius`, `border-bottom-right-radius`, `border-bottom-left-radius`

### Box shadow
- [ ] `box-shadow` (offset-x, offset-y, blur, spread, color, inset)

### Border image
- [ ] `border-image` and its longhand sub-properties

---

## 14. Fonts (CSS Fonts Level 4)

Currently supported: `font-size` (px/em/rem/pt/%/keywords), `font-family`.

- [ ] `font-weight` (`normal`, `bold`, `bolder`, `lighter`, numeric 1-1000)
- [ ] `font-style` (`normal`, `italic`, `oblique [angle]`)
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
- [ ] Generic font families: `serif`, `sans-serif`, `monospace`, `cursive`, `fantasy`, `system-ui`, `ui-serif`, `ui-sans-serif`, `ui-monospace`, `ui-rounded`, `math`, `emoji`, `fangsong`

---

## 15. Text (CSS Text Level 3/4)

Currently supported: `text-align` (left/right/center/justify).

- [ ] `text-align-last`
- [ ] `text-indent`
- [ ] `text-transform` (`uppercase`, `lowercase`, `capitalize`, `none`, `full-width`, `full-size-kana`)
- [ ] `text-decoration` (shorthand)
- [ ] `text-decoration-line` (`none`, `underline`, `overline`, `line-through`)
- [ ] `text-decoration-color`
- [ ] `text-decoration-style` (`solid`, `double`, `dotted`, `dashed`, `wavy`)
- [ ] `text-decoration-thickness`
- [ ] `text-underline-offset`
- [ ] `text-underline-position`
- [ ] `text-emphasis` and its sub-properties
- [ ] `text-shadow`
- [ ] `text-overflow` (`clip`, `ellipsis`)
- [ ] `letter-spacing`
- [ ] `word-spacing`
- [ ] `white-space` (`normal`, `pre`, `nowrap`, `pre-wrap`, `pre-line`, `break-spaces`)
- [ ] `white-space-collapse`, `text-wrap` (CSS Text Level 4 replacements)
- [ ] `word-break` (`normal`, `break-all`, `keep-all`)
- [ ] `overflow-wrap` / `word-wrap` (`normal`, `break-word`, `anywhere`)
- [ ] `line-break`
- [ ] `hyphens` (`none`, `manual`, `auto`)
- [ ] `tab-size`
- [ ] `text-wrap-mode`, `text-wrap-style` (CSS Text Level 4)

---

## 16. Inline Layout (CSS Inline Level 3)

Currently supported: basic inline text layout with word wrapping, line-height as multiplier.

- [ ] `vertical-align` (`baseline`, `sub`, `super`, `top`, `text-top`, `middle`, `bottom`, `text-bottom`, length, percentage)
- [ ] `dominant-baseline`, `alignment-baseline`
- [ ] `initial-letter`
- [ ] Line box height calculation per spec (strut, inline-level box baselines)

---

## 17. Lists (CSS Lists and Counters Level 3)

- [ ] `list-style-type` (`disc`, `circle`, `square`, `decimal`, `lower-alpha`, `upper-alpha`, `lower-roman`, `upper-roman`, `none`, custom `@counter-style`)
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

Currently supported: `background-color`, `color` (text color), border rendering.

- [ ] `opacity`
- [ ] `visibility` (`visible`, `hidden`, `collapse`)
- [ ] `cursor`
- [ ] `pointer-events`
- [ ] `user-select`
- [ ] `resize`
- [ ] `outline`, `outline-color`, `outline-style`, `outline-width`, `outline-offset`
- [ ] `caret-color`
- [ ] `accent-color`
- [ ] `appearance`
- [ ] `object-fit`, `object-position` (for replaced elements)
- [ ] `image-rendering`
- [ ] `content` (for `::before`/`::after`)
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
- [ ] `gap` (for flex/grid)
- [ ] `place-items`, `place-content`, `place-self` (shorthands)
- [ ] `touch-action`
- [ ] `overscroll-behavior`
- [ ] `color-scheme`
- [ ] `forced-color-adjust`
- [ ] `print-color-adjust`
- [ ] `math-style`, `math-depth`, `math-shift` (MathML styling)
