# SuperRenderer

A from-scratch HTML + CSS rendering engine and ECMAScript 2025 engine, built entirely in C# on .NET 10. Renders to a native window via Vulkan (Silk.NET).

No WebView. No Chromium. No Gecko. Just parsers, a layout engine, and a GPU.

## What It Does

```
HTML string
  -> Tokenizer -> DOM tree
  -> CSS parser -> Stylesheets
  -> Style resolution (cascade, specificity, inheritance)
  -> Layout (block + inline box model)
  -> Paint commands (rects, text)
  -> Vulkan rendering
```

The ECMAScript engine compiles JavaScript to .NET DLR expression trees and runs them natively -- no interpreter loop.

## Quick Start

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and a Vulkan-capable GPU (MoltenVK is bundled for macOS).

```bash
# Build
dotnet build

# Run all 489 tests
dotnet test

# Launch the demo window (1024x768, Vulkan)
dotnet run --project src/SuperRender.Demo

# Launch the interactive JavaScript console
dotnet run --project src/SuperRender.EcmaScript.Console
```

The demo renders a sample page with styled headings, containers, lists, and a dynamically-inserted DOM element -- all from raw HTML/CSS parsed and laid out at runtime.

## Project Structure

```
SuperRenderer/
  src/
    SuperRender.Core/            Core library (zero dependencies)
      Html/                        HTML tokenizer + tree builder
      Css/                         CSS tokenizer, parser, selector engine
      Dom/                         Document, Element, TextNode, mutation API
      Style/                       Cascade, specificity, computed styles
      Layout/                      Block + inline layout, box model
      Painting/                    Paint command generation
    SuperRender.EcmaScript/      ECMAScript 2025 engine (zero dependencies)
    SuperRender.EcmaScript.Console/  Node.js-style interactive REPL
    SuperRender.Demo/            Vulkan-powered windowed demo
  tests/
    SuperRender.Tests/           Core engine tests (68 tests)
    SuperRender.EcmaScript.Tests/  JS engine tests (421 tests)
```

## Rendering Engine

### HTML Parser
- State-machine tokenizer (16 states) with entity decoding
- Tree builder with auto html/head/body structure, void element handling, and error recovery
- DOM with `Node`, `Element`, `TextNode`, `Document` types
- DOM mutation: `AppendChild`, `RemoveChild`, `InsertBefore`, `CloneElement`, `QuerySelector`/`QuerySelectorAll`, class manipulation

### CSS Engine
- Full tokenizer and parser with shorthand expansion (`margin`, `padding`, `border`, `border-width`)
- Selectors: type, class, ID, universal, compound, descendant (` `), child (`>`), comma-separated lists
- Cascade with specificity calculation, `!important`, source-order sorting
- Inheritance for `color`, `font-size`, `font-family`, `text-align`, `line-height`
- Inline `style` attribute support

### Supported CSS Properties
| Category | Properties |
|---|---|
| Display | `display` (block, inline, none) |
| Box model | `width`, `height`, `margin-*`, `padding-*`, `border-*-width` |
| Colors | `color`, `background-color`, `border-color` |
| Borders | `border-style`, `border` shorthand |
| Typography | `font-size`, `font-family`, `text-align`, `line-height` |
| Position | `position` (static, relative, absolute), `top`, `left`, `right`, `bottom` |

Units: `px`, `em`, `rem`, `pt`, `%`, `auto`. Colors: hex (#RGB/#RRGGBB/#RRGGBBAA), `rgb()`, `rgba()`, 37 named colors.

### Layout
- Block layout with width calculation, margin auto-centering, vertical stacking
- Inline layout with word-based text wrapping, line-height, text alignment (left/center/right/justify)
- Whitespace collapsing, anonymous block wrapping for mixed block/inline children

### Vulkan Renderer
- Quad pipeline for backgrounds and borders
- Text pipeline with FreeType font atlas and alpha blending
- Dirty-flag optimization -- only re-renders when the DOM changes
- Cross-platform: MoltenVK on macOS, native Vulkan on Windows/Linux

## ECMAScript Engine

A DLR-based JavaScript engine with no external dependencies.

**Pipeline:** Source -> Lexer -> Parser (recursive descent + Pratt) -> DLR Expression Trees -> Compiled Delegates

### What's Implemented
- Full lexer with ES2025 token set
- Recursive descent parser with 20-level Pratt precedence, ASI, arrow functions, destructuring, modules
- Variables: `var`, `let`, `const` with TDZ
- Functions: declarations, expressions, arrows, closures, default/rest params
- Control flow: `if`/`else`, `for`, `for-in`, `for-of`, `while`, `do-while`, `switch`, `try`/`catch`/`finally`
- Classes: `class`, `extends`, `super`, static members, computed properties
- Operators: all arithmetic, comparison, logical, bitwise, nullish coalescing, optional chaining
- Template literals, tagged templates, spread/rest, destructuring assignment
- Prototype chain with `IDynamicMetaObjectProvider`
- 20 built-in objects: Object, Array, String, Number, Boolean, Math, JSON, Date, RegExp, Map, Set, WeakMap, WeakSet, Symbol, Promise, Proxy, Reflect, Error, ArrayBuffer, DataView
- Sandboxed .NET interop: `RegisterType<T>()` / `SetValue()` for controlled host access

### JS Console (REPL)
```bash
dotnet run --project src/SuperRender.EcmaScript.Console

# Or evaluate directly:
dotnet run --project src/SuperRender.EcmaScript.Console -- --eval "1 + 2"

# Or run a file:
dotnet run --project src/SuperRender.EcmaScript.Console -- script.js
```

Features: multiline editing, command history, ANSI-colored output, `.help`/`.exit`/`.clear`/`.editor` dot commands.

## Roadmap

Unimplemented features are tracked in detail:

- **CSS:** [`src/SuperRender.Core/css-todos.md`](src/SuperRender.Core/css-todos.md) -- Selectors Level 4, flexbox, grid, custom properties, `calc()`, transforms, transitions, animations, media queries, container queries, CSS nesting (34 sections)
- **HTML:** [`src/SuperRender.Core/html-todos.md`](src/SuperRender.Core/html-todos.md) -- Full WHATWG tokenizer, tree construction algorithm, forms, tables, embedded content, events, Shadow DOM, user-agent stylesheet (10 sections)
- **ECMAScript:** [`src/SuperRender.EcmaScript/es-2025-todos.md`](src/SuperRender.EcmaScript/es-2025-todos.md) -- BigInt, generators, async/await runtime, WeakRef, Intl, Temporal (26 items)

## Dependencies

| Project | Dependencies |
|---|---|
| SuperRender.Core | None (pure C#) |
| SuperRender.EcmaScript | None (pure C#, DLR ships with .NET) |
| SuperRender.EcmaScript.Console | None (references EcmaScript project only) |
| SuperRender.Demo | Silk.NET 2.23 (Vulkan, Windowing, Input, Shaderc), FreeTypeSharp 3.1 |
| Tests | xUnit 2.9, Microsoft.NET.Test.Sdk 17.12 |

## Requirements

- .NET 10 SDK
- Vulkan-capable GPU (for the demo app only; Core and EcmaScript run anywhere)
- macOS, Windows, or Linux
