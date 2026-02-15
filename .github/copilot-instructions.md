# Copilot Instructions for Jint

Jint is a JavaScript interpreter for .NET. It parses JavaScript using the Esprima library (AST), then interprets it directly — no bytecode generation or DLR usage.

## Build & Test

```bash
# Build
dotnet build -c Release

# Run all tests
dotnet test -c Release

# Run a specific test project
dotnet test Jint.Tests\Jint.Tests.csproj -c Release

# Run a single test by name
dotnet test Jint.Tests\Jint.Tests.csproj -c Release --filter "FullyQualifiedName~Jint.Tests.Runtime.EngineTests.CanAccessCLR"

# Benchmarks (BenchmarkDotNet)
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj
```

Tests use **xunit**. The solution has `TreatWarningsAsErrors` enabled.

## Architecture

### Execution Pipeline

1. **Parsing** — Esprima parses JavaScript source into an AST (`Esprima.Ast` nodes)
2. **Jint Wrapping** — AST nodes are wrapped in `Jint*` interpreter classes:
   - `JintExpression` subclasses in `Runtime/Interpreter/Expressions/` (e.g., `JintCallExpression`, `JintBinaryExpression`)
   - `JintStatement` subclasses in `Runtime/Interpreter/Statements/` (e.g., `JintIfStatement`, `JintForStatement`)
3. **Execution** — `Engine` drives execution. Statements return `Completion` (a value + completion type: Normal/Break/Continue/Return/Throw). Expressions return `JsValue` or `Reference`.

### Key Types

- **`Engine`** — Central entry point. Holds global environment, built-in constructors, execution context stack, constraints, and object pools. Configured via `Options`.
- **`JsValue`** — Abstract base for all JavaScript values. Concrete types: `JsString`, `JsNumber`, `JsBoolean`, `JsNull`, `JsUndefined`, `JsSymbol`, `ObjectInstance`.
- **`ObjectInstance`** — Base for all JS objects. Properties stored in `PropertyDictionary`, symbols in `SymbolDictionary`. Subclassed for Array, Function, Date, RegExp, Map, Set, Promise, Proxy, etc.
- **`Completion`** — Readonly struct representing statement execution results per the ECMAScript spec (§8.9).
- **`Key`** — Internal struct with pre-calculated hash code for fast dictionary lookups.

### Namespace Organization

- `Jint.Native` — JS value types and built-in objects. Each built-in has a subdirectory with `*Constructor`, `*Prototype`, `*Instance` classes (e.g., `Native/Array/ArrayConstructor.cs`, `ArrayPrototype.cs`, `ArrayInstance.cs`).
- `Jint.Runtime` — Execution runtime: environments, descriptors, references, type conversion, call stack, interop, interpreter.
- `Jint.Runtime.Interpreter` — The statement/expression interpreter classes that wrap Esprima AST nodes.
- `Jint.Runtime.Interop` — .NET/CLR interop layer: `ObjectWrapper`, `TypeReference`, `ClrFunctionInstance`, `DelegateWrapper`.
- `Jint.Runtime.Environments` — Lexical environments and environment records (Declarative, Function, Global, Object).
- `Jint.Collections` — Custom high-performance dictionary implementations (`PropertyDictionary`, `StringDictionarySlim`, `DictionarySlim`).
- `Jint.Pooling` — Object pools for frequently allocated types (`ReferencePool`, `ArgumentsInstancePool`, `JsValueArrayPool`).

### Test Projects

- **`Jint.Tests`** — Main unit tests. Test classes mirror runtime types (e.g., `EngineTests`, `InteropTests`, `ArrayTests`). JS test scripts are embedded resources in `Runtime/Scripts/` and `Parser/Scripts/`.
- **`Jint.Tests.Test262`** — ECMAScript Test262 conformance suite.
- **`Jint.Tests.Ecma`** — Legacy ECMA test cases.
- **`Jint.Tests.CommonScripts`** — SunSpider benchmark scripts used for testing.

## Conventions

### Performance is Critical

Performance is a first-class concern in this codebase. Every change must consider its performance impact:

- Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot paths.
- Prefer `readonly struct` types to avoid heap allocations.
- Prefer `readonly record struct` and primary constructors for small data types — combines immutability, value semantics, and concise syntax.
- Use `Span<T>`, `ReadOnlySpan<T>`, and stack-based allocation wherever possible.
- Use modern .NET language features (pattern matching, ranges, nullable reference types, etc.).
- Leverage object pooling (`Jint.Pooling`) for frequently allocated types instead of creating new instances.
- Mark types as `sealed` whenever possible — it enables devirtualization and inlining by the JIT.
- Use `internal` visibility where possible — it avoids virtual dispatch and enables inlining. Public API surface is limited to `Engine`, `Options`, `JsValue`, and interop types.

### Code Patterns

- **Lazy initialization** — Interpreter classes (`JintExpression`/`JintStatement`) set `_initialized = false` in constructors and override `Initialize()` for deferred setup on first execution.
- **Error throwing** uses the static `ExceptionHelper` class (avoids allocations in non-error paths via `[DoesNotReturn]`).
- **Built-in JS types** follow the Constructor/Prototype/Instance pattern matching the ECMAScript spec structure.
- **Engine carries all state** — expression and statement interpreters hold an `_engine` reference; `ObjectInstance` and most runtime types receive `Engine` in constructors.
- **PR target branch** is `main`.
