# SuperRender EcmaScript Engine

A pure C# ECMAScript 2025 engine for .NET, built on DLR expression trees.

## Features

- **Full ES2025 coverage** — classes, generators, async/await, destructuring, modules, BigInt, Temporal, and more
- **Zero external dependencies** — runs on any .NET 10+ platform
- **Sandboxed .NET interop** — mount specific types into JS via `RegisterType<T>()`
- **DLR-compiled** — JS source is compiled to .NET expression trees for efficient execution

## Quick Start

```csharp
using SuperRender.EcmaScript.Engine;

var engine = new JsEngine();

// Execute JavaScript
engine.Execute("const greeting = 'Hello from JS!'");

// Get values back
var result = engine.Execute("1 + 2");
Console.WriteLine(result); // 3

// .NET interop
engine.SetValue("log", new Action<string>(Console.WriteLine));
engine.Execute("log('Called from JavaScript!')");
```

## Packages

| Package | Description |
|---------|-------------|
| **SuperRender.EcmaScript.Engine** | Public API — start here |
| **SuperRender.EcmaScript.Compiler** | Lexer, parser, AST, DLR compiler |
| **SuperRender.EcmaScript.Runtime** | JsValue types, environments, builtins |

## License

MIT
