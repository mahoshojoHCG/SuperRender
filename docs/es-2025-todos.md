# ECMAScript 2025 — Unimplemented Features

This document tracks ES2025 standard features not yet implemented in SuperRender.EcmaScript.
Features are grouped by category and include a brief rationale for deferral.

*Last updated: 2026-04-12*

## Implemented Language Features (previously deferred)

### Generators ✓
- `function*` declarations and expressions parse and compile correctly
- `yield` / `yield*` expressions work at runtime
- `GeneratorCoroutine` provides thread-based coroutine for state machines
- `JsGeneratorObject` implements next/return/throw with iterator protocol via Symbol.iterator
- `GeneratorPrototype` intrinsic on Realm

### Async / Await ✓
- `async function` / `async () =>` parse and compile correctly
- `await` expressions compile to coroutine yield points
- `RuntimeHelpers.RunAsyncFunction` creates Promise-based execution
- Integrates with `GeneratorCoroutine` for state machine execution

### Labels ✓
- `break label` / `continue label` runtime support in compiler
- Labeled statements fully compiled

## Language Features — Still Deferred

### BigInt
- `BigInt` type and literals (`123n`)
- All operators on BigInt values
- **Reason**: Requires a separate numeric type and pervasive operator changes throughout the runtime; cannot coexist with `double` in binary operations without dedicated dispatch.

### `for-await-of`
- Parses correctly
- **Missing**: Runtime execution of async iteration protocol
- **Reason**: Depends on async iterator protocol implementation.

### `eval()`
- Not implemented (intentionally)
- **Reason**: Security concern in sandboxed engine; allows arbitrary code execution and scope introspection.

### `with` statement
- Not implemented
- **Reason**: Forbidden in strict mode (engine defaults to strict mode).

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
| ~~Core language (previously deferred)~~ | ~~3~~ | **Implemented** (generators, async/await, labels) |
| Core language (remaining) | 4 | Deferred |
| ES2025 new features | 8 | Deferred |
| Deferred APIs | 11 | Deferred |
| **Total remaining** | **23** | |
