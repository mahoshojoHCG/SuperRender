# ECMAScript 2025 — Unimplemented Features

This document tracks ES2025 standard features not yet implemented in SuperRender.EcmaScript.
Features are grouped by category and include a brief rationale for deferral.

## Language Features

### BigInt
- `BigInt` type and literals (`123n`)
- All operators on BigInt values
- **Reason**: Requires a separate numeric type and pervasive operator changes throughout the runtime; cannot coexist with `double` in binary operations without dedicated dispatch.

### Generators (partial)
- `function*` declarations and expressions parse correctly
- `yield` / `yield*` expressions parse correctly
- **Missing**: Runtime execution (state machine compilation in JsCompiler)
- **Reason**: Requires transforming generator bodies into DLR state machines; planned for next iteration.

### Async / Await (partial)
- `async function` / `async () =>` parse correctly
- `await` expressions parse correctly
- **Missing**: Runtime execution (continuation-passing transform in JsCompiler)
- **Reason**: Requires integration with microtask queue and state machine compilation.

### `for-await-of`
- Parses correctly
- **Missing**: Runtime execution of async iteration protocol
- **Reason**: Depends on async/await runtime support.

### `eval()`
- Not implemented (intentionally)
- **Reason**: Security concern in sandboxed engine; allows arbitrary code execution and scope introspection.

### `with` statement
- Not implemented
- **Reason**: Forbidden in strict mode (engine defaults to strict mode).

### Labels on loops/switch
- Parsed correctly
- **Missing**: `break label` / `continue label` runtime support in compiler
- **Reason**: Rarely used; can be added incrementally.

### Tail Call Optimization
- Not implemented
- **Reason**: Requires trampoline or continuation-passing transform in the compiler.

## ES2025 Specific Features

### Iterator Helpers
- `Iterator.prototype.map()`, `.filter()`, `.take()`, `.drop()`, `.flatMap()`, `.reduce()`, `.toArray()`, `.forEach()`, `.some()`, `.every()`, `.find()`
- `Iterator.from()`
- **Reason**: New in ES2025; can be implemented as prototype methods on IteratorPrototype.

### Set Methods
- `Set.prototype.union()`, `.intersection()`, `.difference()`, `.symmetricDifference()`, `.isSubsetOf()`, `.isSupersetOf()`, `.isDisjointFrom()`
- **Reason**: New in ES2025; straightforward to add to SetConstructor.

### RegExp Features
- Named capture groups (`(?<name>...)`)
- Lookbehind assertions (`(?<=...)`, `(?<!...)`)
- Unicode property escapes (`\p{...}`)
- `/v` flag (unicodeSets)
- `/d` flag (hasIndices)
- **Reason**: Requires translating JS regex extensions to .NET Regex; partial support exists.

### `Promise.withResolvers()`
- New static method returning `{ promise, resolve, reject }`
- **Reason**: Simple to add; deferred for prioritization.

### `Array.fromAsync()`
- Async version of `Array.from()`
- **Reason**: Depends on async iteration runtime support.

### `Object.groupBy()` / `Map.groupBy()`
- Static grouping methods
- **Reason**: Straightforward to implement; deferred for prioritization.

### `String.prototype.isWellFormed()` / `toWellFormed()`
- Unicode well-formedness checks
- **Reason**: Straightforward to implement.

### `Atomics.waitAsync()`
- Async version of `Atomics.wait()`
- **Reason**: Depends on SharedArrayBuffer (see below).

## Deferred APIs

### SharedArrayBuffer / Atomics
- Shared memory and atomic operations
- **Reason**: Requires a threading model and shared memory semantics not present in the current single-threaded engine.

### WeakRef / FinalizationRegistry
- Weak references and destructor callbacks
- **Reason**: Requires integration with .NET GC finalization; complex lifetime semantics.

### Intl (Internationalization API)
- `Intl.Collator`, `Intl.DateTimeFormat`, `Intl.NumberFormat`, `Intl.PluralRules`, `Intl.RelativeTimeFormat`, `Intl.ListFormat`, `Intl.Segmenter`, etc.
- **Reason**: Massive API surface area; depends on ICU data and .NET globalization APIs.

### Temporal
- `Temporal.PlainDate`, `Temporal.PlainTime`, `Temporal.ZonedDateTime`, etc.
- **Reason**: Stage 3 proposal, not yet fully standardized; very large API surface.

### Decorators
- `@decorator` syntax for classes and class members
- **Reason**: Stage 3 proposal; requires parser and compiler changes.

### Pipeline Operator (`|>`)
- **Reason**: Stage 2 proposal; not yet standard.

### Pattern Matching
- **Reason**: Stage 1 proposal; not yet standard.

### Import Assertions / Attributes
- `import x from 'y' with { type: 'json' }`
- **Reason**: Module system integration not fully implemented.

### ShadowRealm
- Isolated global environments
- **Reason**: Complex sandboxing requirements.

### Module Workers
- **Reason**: Requires a worker/threading model.

### Structured Clone
- `structuredClone()` global function
- **Reason**: Requires deep-clone semantics for all value types.

### `Function()` constructor
- Creating functions from strings
- **Reason**: Security concern; equivalent to `eval()`.

### Hashbang Grammar
- `#!/usr/bin/env node` at start of file
- Parsed (lexer skips it) but not formally spec-compliant.

## Tracking

| Category | Count | Status |
|----------|-------|--------|
| Core language features | 7 | Deferred |
| ES2025 new features | 8 | Deferred |
| Deferred APIs | 11 | Deferred |
| **Total** | **26** | |
