# ECMAScript 2025 — Feature Implementation Status

This document tracks ES2025 standard features in SuperRender.EcmaScript.
Features are grouped by category with implementation status.

*Last updated: 2026-04-12*

## Implemented Language Features

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

### BigInt ✓
- `BigInt` type and literals (`123n`, `0x1An`, `0o7n`, `0b101n`)
- All arithmetic operators (+, -, *, /, %, **)
- Comparison operators (===, ==, <, >, <=, >=)
- Bitwise operators (&, |, ^, ~, <<, >>)
- `BigInt()` constructor (callable, not constructible)
- `BigInt.asIntN()`, `BigInt.asUintN()` static methods
- Mixed BigInt/Number arithmetic throws TypeError (per spec)
- `typeof bigint === "bigint"`
- `JsBigInt` runtime type wrapping `System.Numerics.BigInteger`

### `with` Statement ✓
- Parses correctly
- Throws `SyntaxError` in strict mode (engine defaults to strict mode)

### Hashbang Grammar ✓
- `#!/usr/bin/env node` at start of file properly skipped by lexer

### `eval()` ✓
- Indirect eval implemented as global function
- Parses and compiles code string at runtime
- Executes in global environment
- Non-string arguments returned as-is

### `Function()` Constructor ✓
- `new Function('a', 'b', 'return a + b')` creates functions from strings
- Uses `Realm.FunctionFactory` delegate for compilation
- `Function.prototype.bind()`, `.call()`, `.apply()`

### Pipeline Operator (`|>`) ✓
- Hack-style pipeline: `x |> fn` compiles to `fn(x)`
- Chainable: `x |> fn1 |> fn2 |> fn3`
- Lexer/parser/compiler support

### `for-await-of` ✓
- Parses correctly (ForOfStatement.IsAwait)
- Runtime async iteration protocol via Symbol.asyncIterator fallback to Symbol.iterator

### Import Assertions / Attributes ✓
- `import x from 'y' with { type: 'json' }` syntax parsed
- Assertions stored on ImportDeclaration AST node
- Compiler passes through (no module loader to consume them)

## Implemented ES2025 Features

### Iterator Helpers ✓
- `Iterator.prototype.map()`, `.filter()`, `.take()`, `.drop()`, `.flatMap()`, `.reduce()`, `.toArray()`, `.forEach()`, `.some()`, `.every()`, `.find()`
- `Iterator.from()`
- Lazy evaluation for map/filter/take/drop/flatMap
- Works with generators, arrays, sets, maps

### Set Methods ✓
- `Set.prototype.union()`, `.intersection()`, `.difference()`, `.symmetricDifference()`, `.isSubsetOf()`, `.isSupersetOf()`, `.isDisjointFrom()`

### RegExp Features ✓
- Named capture groups (`(?<name>...)`) with `.groups` property on match
- Lookbehind assertions (`(?<=...)`, `(?<!...)`)
- Unicode property escapes (`\p{Lu}`, `\p{Sc}`, etc.)
- `/d` flag (hasIndices) with `.indices` property on match
- `/v` flag: partial support (complex set notation deferred)

### Promise.withResolvers() ✓
- Static method returning `{ promise, resolve, reject }`

### Object.groupBy() / Map.groupBy() ✓
- Static grouping methods on Object and Map

### String.prototype.isWellFormed() / toWellFormed() ✓
- Unicode well-formedness checks and lone surrogate replacement

### `Array.fromAsync()` ✓
- Async version of `Array.from()` returning a Promise

### `Atomics.waitAsync()` ✓
- Returns a Promise (part of Atomics implementation)

## Implemented APIs

### WeakRef / FinalizationRegistry ✓
- `WeakRef` wraps `System.WeakReference<JsObject>`
- `deref()` returns target or undefined
- `FinalizationRegistry` with register/unregister
- Cleanup callback invocation on collected targets

### structuredClone() ✓
- Deep cloning with circular reference detection
- Handles primitives, objects, arrays, Sets, Maps, Dates
- Throws on non-cloneable types (functions, symbols)

### SharedArrayBuffer / Atomics ✓
- `SharedArrayBuffer` with byte[] backing
- `ArrayBuffer` prerequisite implemented
- TypedArray views: Int8Array, Uint8Array, Int16Array, Uint16Array, Int32Array, Uint32Array, Float32Array, Float64Array
- `Atomics`: add, and, compareExchange, exchange, load, or, store, sub, xor, isLockFree, wait, notify, waitAsync

### Intl (Partial) ✓
- `Intl.Collator` — locale-aware string comparison via .NET CultureInfo
- `Intl.NumberFormat` — decimal/percent/currency formatting
- `Intl.DateTimeFormat` — date formatting
- `Intl.PluralRules` — cardinal plural category selection

### Temporal (Partial) ✓
- `Temporal.PlainDate` — date without time (wraps DateOnly)
- `Temporal.PlainTime` — time without date (wraps TimeOnly)
- `Temporal.PlainDateTime` — date and time (wraps DateTime)
- `Temporal.Instant` — epoch-based instant (epochMilliseconds)
- `Temporal.Duration` — ISO 8601 duration parsing (P1Y2M3D)
- `Temporal.Now` — current date/time accessors

### ShadowRealm ✓
- `new ShadowRealm()` creates isolated global environment
- `evaluate(code)` compiles and executes in shadow realm
- Only primitive return values cross boundary (per spec)
- Uses `Realm.EvalFactory` for cross-project compilation

## Deferred Features

### Tail Call Optimization
- **Reason**: Requires trampoline transform in compiler; would add overhead to all function calls

### Decorators
- **Reason**: Stage 3 proposal; syntax not yet finalized. Parser and compiler infrastructure ready.

### Pattern Matching
- **Reason**: Stage 1 proposal; no settled syntax

### Module Workers
- **Reason**: Requires threading model + module loader infrastructure

## Tracking

| Category | Count | Status |
|----------|-------|--------|
| Core language | 9 | **Implemented** (generators, async/await, labels, BigInt, with, hashbang, eval, Function(), pipeline) |
| ES2025 new features | 8 | **All Implemented** |
| APIs | 8 | **All Implemented** (WeakRef, structuredClone, SharedArrayBuffer, Atomics, Intl, Temporal, ShadowRealm, for-await-of) |
| Import assertions | 1 | **Implemented** (parser only) |
| Deferred | 4 | TCO, Decorators, Pattern Matching, Module Workers |
| **Total implemented** | **26** | |
| **Deferred** | **4** | |
