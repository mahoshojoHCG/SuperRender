# Node.js API Simulator — Priority Plan

Target: provide a Node.js-compatible runtime surface on top of `SuperRender.EcmaScript.Engine`, so scripts authored against `@types/node` (v25.6.0, pinned in `src/EcmaScript/SuperRender.EcmaScript.NodeSimulator/ref/types-node/`) can be executed by the built-in EcmaScript engine.

Ranking criteria:
- **P0** — Required for *any* realistic Node program to start. Globals + core I/O primitives used by virtually every script, package, and test runner.
- **P1** — Needed to run most npm packages / tooling (module resolution, streams, http, events, url, crypto basics).
- **P2** — Broadly useful but not on the critical path (child_process, net, tls, worker_threads, zlib, perf_hooks, readline).
- **P3** — Specialized / platform-heavy (dgram, dns, cluster, v8, vm, inspector, wasi, sqlite, sea, quic).
- **P4** — Deprecated, niche, or out-of-scope (domain, punycode, trace_events, repl module, string_decoder as standalone, constants).

Parity goal per tier: P0 = spec-level fidelity, P1 = behavioral parity for common code paths, P2 = "shape-compatible" (API present, semantics best-effort), P3/P4 = stub or skip unless a consumer materializes.

---

## P0 — Launch blockers

Without these, no meaningful Node script runs.

| Surface            | Notes                                                                                     |
| ------------------ | ----------------------------------------------------------------------------------------- |
| `globalThis` / `global` | Alias `globalThis` → `global`; expose `queueMicrotask`, `structuredClone`, `setImmediate`. |
| `process`          | `argv`, `argv0`, `execPath`, `cwd()`, `chdir()`, `env`, `platform`, `arch`, `version`, `versions`, `pid`, `ppid`, `exit()`, `exitCode`, `stdout`/`stderr`/`stdin` (minimal Writable/Readable), `hrtime()`, `hrtime.bigint()`, `nextTick()`, `on('uncaughtException'|'unhandledRejection'|'exit')`, `emitWarning()`. |
| `Buffer`           | `from`, `alloc`, `allocUnsafe`, `byteLength`, `concat`, `isBuffer`, `isEncoding`, read/write of utf8/ascii/latin1/hex/base64/base64url/utf16le, slicing semantics that share memory with backing `Uint8Array`. |
| `console`          | Node-specific overloads (already partly present in engine); ensure `console.log/info/warn/error/debug/dir/table/time/timeEnd/trace/group*/count*/assert` write to `process.stdout`/`stderr`. |
| `timers` globals   | `setTimeout`, `setInterval`, `setImmediate`, `clearTimeout`, `clearInterval`, `clearImmediate`, returning a Node-style `Timeout` object with `.ref()`/`.unref()`/`.refresh()`. |
| `timers/promises`  | `setTimeout`, `setInterval`, `setImmediate` promise variants. |
| CommonJS + ESM loader | `require`, `module`, `exports`, `__filename`, `__dirname`, `require.resolve`, `require.cache`, `module.createRequire`, dynamic `import()`, package.json `"exports"`/`"main"`/`"type"` resolution, `.mjs`/`.cjs` distinction. |
| `path` (+ `path/posix`, `path/win32`) | Full API: `join`, `resolve`, `normalize`, `dirname`, `basename`, `extname`, `parse`, `format`, `isAbsolute`, `relative`, `sep`, `delimiter`, `matchesGlob`. |
| `fs` sync + `fs.promises` (+ `fs/promises`) | Read/write/stat/readdir/mkdir/rm/rename/copyFile/access/realpath/readlink/symlink/chmod/chown/utimes/watch/createReadStream/createWriteStream/constants. Covers what `require`-based package loading, test runners, and build tools hit. |
| `os`               | `platform`, `arch`, `cpus`, `totalmem`, `freemem`, `homedir`, `tmpdir`, `hostname`, `userInfo`, `endianness`, `EOL`, `release`, `version`, `type`, `uptime`, `loadavg`. |
| `util`             | `promisify`, `callbackify`, `inspect` (Node-format, ANSI), `format`, `formatWithOptions`, `types.*`, `deprecate`, `isDeepStrictEqual`, `parseArgs`, `styleText`, `TextEncoder`/`TextDecoder` aliases. |
| `events`           | `EventEmitter` (on/once/off/emit/listeners/prependListener/setMaxListeners/rawListeners/listenerCount), static `once`, `on`, `getEventListeners`, `captureRejectionSymbol`, `errorMonitor`. |
| `assert` + `assert/strict` | Full assertion API — pulled in by most test code before anything else. |

## P1 — Needed by most real packages

| Surface               | Notes |
| --------------------- | ----- |
| `stream` (+ `stream/promises`, `stream/web`) | `Readable`, `Writable`, `Duplex`, `Transform`, `PassThrough`, `pipeline`, `finished`, `compose`, WHATWG bridges (`Readable.toWeb`/`fromWeb`). Foundation for `fs`, `http`, `zlib`, `crypto`. |
| `http` / `https`      | `createServer`, `request`/`get`, `Agent`, `ClientRequest`, `IncomingMessage`, `ServerResponse`, header parsing, keep-alive. Most CLIs + dev servers depend on this. |
| `url`                 | WHATWG `URL`/`URLSearchParams` already spec-mandated; add legacy `url.parse/format/resolve`, `fileURLToPath`, `pathToFileURL`, `urlToHttpOptions`. |
| `querystring`         | `parse`, `stringify`, `escape`, `unescape`. |
| `crypto`              | `randomUUID`, `randomBytes`, `randomFillSync`, `createHash` (sha1/sha256/sha512/md5), `createHmac`, `timingSafeEqual`, `webcrypto` subset, `createCipheriv`/`createDecipheriv` (aes-256-gcm, aes-256-cbc), `pbkdf2`/`pbkdf2Sync`, `scrypt`. Asymmetric (RSA/EC) deferred to P2. |
| `string_decoder`      | `StringDecoder` class — leaf dep of many stream consumers. |
| `zlib`                | `gzip`/`gunzip`/`deflate`/`inflate`/`brotliCompress`/`brotliDecompress` sync+async+stream. Required for npm tarball handling, http content-encoding. |
| `module` (advanced)   | `Module._resolveFilename`, `register`, `syncBuiltinESMExports`, `findPackageJSON`, `enableCompileCache`. |
| `fs.watch` / `fsPromises.watch` | Event-based file watching — required by test runners/bundlers. |
| `worker_threads`      | Needed to run Vite/webpack/esbuild workers; can initially map to a thread-backed sandbox engine. |

## P2 — Broadly useful

| Surface              | Notes |
| -------------------- | ----- |
| `child_process`      | `spawn`, `exec`, `execFile`, `fork`, `spawnSync`, `execSync`, `execFileSync`. Maps to `System.Diagnostics.Process`. `fork` harder (requires IPC channel). |
| `net`                | TCP `Server`/`Socket`, `createServer`, `createConnection`, Unix domain sockets. |
| `tls`                | `TLSSocket`, `createSecureContext`, `connect`, `createServer` — layered on `net` + `System.Net.Security`. |
| `dns` / `dns/promises` | `lookup`, `resolve*`, `reverse`. |
| `readline` / `readline/promises` | Line-based stdin reader; already partly covered by REPL’s `LineEditor`. |
| `perf_hooks`         | `performance.now`, `PerformanceObserver`, `monitorEventLoopDelay`, `createHistogram`. |
| `async_hooks`        | `AsyncLocalStorage` (critical for observability tooling), `executionAsyncId`, `triggerAsyncId`. Hooks themselves best-effort. |
| `diagnostics_channel` | Channel pub/sub — cheap; widely used by OpenTelemetry. |
| `tty`                | `isatty`, `ReadStream`, `WriteStream` (color support query). |
| `test` / `node:test` | Built-in test runner + `describe`/`it`/`mock`. Useful once CommonJS + assert work. |
| `inspector` / `inspector/promises` | Only stubs unless we integrate a DevTools channel. |

## P3 — Specialized

| Surface            | Notes |
| ------------------ | ----- |
| `cluster`          | Multi-process primary/worker. Likely stub. |
| `dgram`            | UDP sockets. |
| `http2`            | Complex; defer until HTTP/1 is solid. |
| `vm`               | `Script`, `SourceTextModule`, contextification — partially satisfied by the existing JsEngine but API shape differs. |
| `v8`               | Serialize/deserialize, heap snapshots — minimal stubs only. |
| `wasi`             | Requires a WebAssembly runtime. |
| `sqlite` (`node:sqlite`) | New in Node 22+; we already ship a SQLite dep in Browser — feasible if demand appears. |
| `sea` (single executable app) | Deep runtime integration; skip unless packaging goal changes. |
| `quic`             | Experimental in Node itself; stub. |
| `process.binding`  | Undocumented; ignore. |

## P4 — Deprecated / skip

| Surface         | Notes |
| --------------- | ----- |
| `domain`        | Deprecated. Provide empty shim so `require('domain')` doesn’t throw. |
| `punycode`      | Deprecated; provide via pure-JS fallback if any dependency still imports it. |
| `trace_events`  | Profiling-only; skip. |
| `repl` (module) | Our standalone REPL is already the `SuperRender.EcmaScript.Repl` project; library surface isn't a priority. |
| `constants`     | Legacy aggregate of per-module constants; superseded by each module’s own `.constants`. |
| `string_decoder` lone usage already covered in P1. |

---

## Execution order (suggested)

1. **Phase 1 (P0):** `global`/`process`/`Buffer`/`console`/timers + `util`/`events`/`assert` + CommonJS loader + `path` + `os`.
2. **Phase 2 (P0 cont.):** `fs` + `fs/promises` with real disk I/O through `System.IO`. Enables loading npm packages.
3. **Phase 3 (P1):** `stream` → `zlib` → `crypto` hashes → `url`/`querystring` → `http`/`https`.
4. **Phase 4 (P1 cont.):** `worker_threads`, `module` advanced hooks, `fs.watch`.
5. **Phase 5 (P2):** `child_process`, `net`/`tls`, `dns`, `perf_hooks`, `async_hooks`, `readline`, `node:test`.
6. **Phase 6 (P3+):** driven by concrete consumers; otherwise stub to `throw new Error('not implemented')` with a clear message.

## Reference material

- Type definitions (pinned): `src/EcmaScript/SuperRender.EcmaScript.NodeSimulator/ref/types-node/` — `@types/node@25.6.0`.
- Authoritative behavior: <https://nodejs.org/docs/latest/api/>.
- When a surface has both a callback and promise form, implement the promise form first and build the callback form on top via `util.callbackify`.
- When semantics differ across platforms (e.g., `path`, `os.EOL`, `process.platform`), prefer the host OS of the running `dotnet` process; expose `path.posix`/`path.win32` for the opposite case.
