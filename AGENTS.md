# Agent Instructions for Jint

Jint is a JavaScript interpreter for .NET. It parses JavaScript using the Acornima library (AST), then interprets it directly — no bytecode generation or DLR usage.

This is the canonical instruction file for every agent working on this repository; `CLAUDE.md` imports it. Target and compare work against the **`main`** branch, which is also the PR target.

## Build & Test

```bash
# Build (solution, or a single project)
dotnet build -c Release
dotnet build -c Release Jint/Jint.csproj

# Run all tests
dotnet test -c Release

# A specific project, class, or single test
dotnet test Jint.Tests\Jint.Tests.csproj -c Release
dotnet test -c Release --filter "FullyQualifiedName~Jint.Tests.Runtime.EngineTests"
dotnet test -c Release --filter "FullyQualifiedName~Jint.Tests.Runtime.EngineTests.CanAccessCLR"

# Test262 conformance suite
dotnet test -c Release Jint.Tests.Test262/Jint.Tests.Test262.csproj
```

Always build and test in **Release** — it is the faster feedback loop and the configuration performance claims are about. Never pass `--no-build`; always work against freshly compiled code. `TreatWarningsAsErrors` is on, so every warning must be fixed. Packages are managed centrally through `Directory.Packages.props`.

### Quick manual testing with Jint.Repl

```bash
# -f <path> executes a file, -t <secs> sets a timeout, stdin works too
dotnet run --project Jint.Repl -c Release -- -f script.js -t 10
echo "Math.sqrt(16)" | dotnet run --project Jint.Repl -c Release -- -t 10
```

**Always pass `-t`** so a runaway script cannot hang the session. Use the REPL for quick one-off checks; use a test in `Jint.Tests` for anything worth keeping.

## Benchmarks

Benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) and live in `Jint.Benchmark/`.

```bash
# List, then run a class, a method, or a wildcard match
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --list flat
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --filter "Jint.Benchmark.EngineConstructionBenchmark"
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --filter "*EngineConstructionBenchmark.BuildEngine"
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --filter "*Array*"
```

Running everything is slow — filter unless you need the full set.

> **Do not report or compare numbers from `--job short`.** It reduces warmup and iteration counts to ~3, so its run-to-run variance (~10%) exceeds most of the wins we measure. It is a smoke-test that a benchmark *runs*; use the default job for any figure that reaches a README, PR, or commit message.

The cross-engine comparison (`EngineComparisonBenchmark`) has its own notes and published results in [`Jint.Benchmark/README.md`](Jint.Benchmark/README.md). Run it from the `Jint.Benchmark` directory so the `Scripts/*.js` files resolve: `dotnet run -c Release -- --allCategories EngineComparison`.

### Adding a new benchmark

1. Create a class in `Jint.Benchmark/` with `[MemoryDiagnoser]`.
2. For script-file benchmarks, extend `SingleScriptBenchmark` and override `FileName` to point at a file in `Scripts/`. The base class handles loading, parsing and the `Execute` / `Execute_ParsedScript` methods.
3. For standalone benchmarks, add `[Benchmark]` methods directly, using `Engine.PrepareScript()` to separate parsing from execution.
4. Put required JS files in `Jint.Benchmark/Scripts/`; they are copied to the output directory automatically.

## Architecture

```
Acornima Parser (external) → AST → Interpreter → Runtime → Interop
```

### Execution pipeline

1. **Parsing** — Acornima parses JavaScript source into an AST (`Acornima.Ast` nodes).
2. **Jint wrapping** — AST nodes are wrapped in `Jint*` interpreter classes: `JintExpression` subclasses in `Runtime/Interpreter/Expressions/` (`JintCallExpression`, `JintBinaryExpression`, …) and `JintStatement` subclasses in `Runtime/Interpreter/Statements/` (`JintIfStatement`, `JintForStatement`, …).
3. **Execution** — `Engine` drives execution. Statements return `Completion` (a value plus a completion type: Normal/Break/Continue/Return/Throw); expressions return `JsValue` or `Reference`.

### Key types

- **`Engine`** — Central entry point, split across partial files (`Engine.cs`, `Engine.Advanced.cs`, `Engine.Modules.cs`, …). Holds the global environment, execution context stack, realms, constraints and object pools. Configured via `Options`.
- **`JsValue`** — Abstract base for all JavaScript values: `JsString`, `JsNumber`, `JsBoolean`, `JsNull`, `JsUndefined`, `JsSymbol`, `JsBigInt`, `ObjectInstance`.
- **`ObjectInstance`** — Base for all JS objects; own properties in `_properties`, symbols in `_symbols`. Subclassed for Array, Function, Date, RegExp, Map, Set, Promise, Proxy, etc.
- **`Intrinsics`** — All built-in constructors and prototypes for a realm, lazily initialized. **`Realm`** encapsulates a global environment plus its intrinsics.
- **`TypeConverter`** — All JavaScript coercion (`ToPrimitive`, `ToNumber`, `ToString`, `ToObject`, …).
- **`Completion`** — Readonly struct for statement results, per the ECMAScript spec.
- **`Key`** — Internal struct with a pre-calculated hash code for fast property lookups.

### Namespace organization

- `Jint.Native` — JS value types and built-ins. Each built-in has a subdirectory with `*Constructor`, `*Prototype`, `*Instance` classes (e.g. `Native/Array/ArrayConstructor.cs`).
- `Jint.Runtime` — Environments, descriptors, references, type conversion, call stack, interop, interpreter.
- `Jint.Runtime.Interpreter` — The statement/expression classes wrapping Acornima AST nodes.
- `Jint.Runtime.Interop` — CLR interop: `ObjectWrapper`, `TypeReference`, `ClrFunction`, `DelegateWrapper`, `DefaultTypeConverter`, and `Reflection/` for cached type discovery and method binding.
- `Jint.Runtime.Environments` — Lexical environments and environment records (Declarative, Function, Global, Object).
- `Jint.Runtime.Modules` — ES module system: `ModuleLoader` resolution, `CyclicModule` for cyclic dependencies, `ModuleBuilder` for programmatic modules.
- `Jint.Collections` — High-performance dictionaries (`HybridDictionary`, `StringDictionarySlim`, `DictionarySlim`). `PropertyDictionary` is a global-using alias for `HybridDictionary<PropertyDescriptor>`, which switches between list and hash storage by property count.
- `Jint.Pooling` — Pools for hot allocations (`ReferencePool`, `ArgumentsInstancePool`, `JsValueArrayPool`, `JsValueListBuilder`).

### Test projects

- **`Jint.Tests`** — Main unit tests (xUnit v3, AwesomeAssertions), organized by topic (`Runtime/`, `Parser/`, `Debugger/`, …). Test classes mirror runtime types; JS scripts are embedded resources in `Runtime/Scripts/` and `Parser/Scripts/`. Use a 30-second timeout when invoking the runner.
- **`Jint.Tests.Test262`** — Official TC39 conformance suite (NUnit). `Test262Harness.settings.json` holds exclusions/inclusions. Test sources live in `..\test262\test`, which you may always read. Failure output contains the failing script — strip the line numbers to reproduce. **Never "fix" these tests.** No runner timeout needed; the engine defaults to 30 seconds.
- **`Jint.Tests.CommonScripts`** — Real-world scripts (crypto, 3D rendering, …) run as correctness and performance validation (NUnit).
- **`Jint.Tests.PublicInterface`** — API contract tests (xUnit v3). See the integration-surface section below.
- **`Jint.Tests.SourceGenerators`** — Tests for the source generators.

## Third-party integration surface

Jint is not only an engine to work on, it is an engine that gets **embedded**. Integrators host it in-process, project host-supplied objects into script, and bound execution. A significant part of the public API exists only to serve them, and for everything in this section **changing observable behaviour is breaking even when the signature is untouched**. Assume an embedder depends on it before simplifying or "optimising" it.

### What counts as a public contract

| Surface | Location |
| --- | --- |
| `ObjectInstance` overridable virtuals — `GetOwnProperty`, `HasProperty`, `Delete`, `DefineOwnProperty`, `GetOwnPropertyKeys`, `GetOwnProperties`, `RemoveOwnProperty`, `PreventExtensions`, `TryGetProperty`, `Initialize`, plus the `protected internal` `ProbeOwnProperty`, `SetOwnProperty`, `GetPrototypeOf` | `Jint/Native/Object/ObjectInstance.cs` |
| `PropertyDescriptor` and `PropertyFlag.CustomJsValue` | `Jint/Runtime/Descriptors/` |
| `ProbeOwnProperty` / `OwnPropertyProbe` | `Jint/Native/Object/` |
| `PropertyAccessSemantics` + `ObjectInstance.SetPropertyAccessSemantics` | `Jint/Native/Object/PropertyAccessSemantics.cs` |
| `IReferenceResolver` + `ReferenceResolverInterests` | `Jint/Runtime/Interop/IReferenceResolver.cs` |
| `IObjectConverter`, `ITypeConverter` | `Jint/Runtime/Interop/` |
| `Constraint` + `IsAmortizable` | `Jint/IConstraint.cs` |
| `ProxyHandler` — public abstract class, 13 virtual traps (the ECMAScript trap set), `null` meaning "forward to target" | `Jint/Runtime/Interop/ProxyHandler.cs` |
| `ObjectWrapper.GetPropertyDescriptor(Engine, object, MemberInfo)` — public **static** | `Jint/Runtime/Interop/ObjectWrapper.cs` |
| `JsObjectLayout`, `JsObject.Create`, `JsObject.CreateFromEntries` | `Jint/Native/` |
| `Options.AddLazyGlobal` — extension method on `OptionsExtensions` | `Jint/Options.Extensions.cs` |
| `ReferencedGlobals` + `Prepared<T>.ReferencedGlobals` + `{Script,Module}PreparationOptions.CollectReferencedGlobals` | `Jint/ReferencedGlobals.cs`, `Jint/Prepared.cs`, `Jint/PreparationOptions.cs` |

`Get` and `Set` are overridable too, but they are `public override` of `JsValue` virtuals rather than declared on `ObjectInstance`.

One public type is deliberately **not** a contract: `ObjectRepresentation` and `Engine.AdvancedOperations.GetObjectRepresentation` name an internal representation for diagnostics and tests. Which representation an object lands in may change in any release and the enum may gain members; neither counts as breaking. Do not let a host branch on it in production code, and do not freeze the engine's behaviour to preserve it.

**`PropertyFlag.CustomJsValue` is the supported lazy-value hook.** A `PropertyDescriptor` subclass overriding `CustomValue` keeps working under the read inline caches: every caching lane returns through `ObjectInstance.UnwrapJsValue`, which re-reads the flag on each hit and caches the descriptor *reference*, never a value snapshot. Overriding `Get` is now also honoured — a subclass that overrides it is derived `Exotic` (below) and every read routes through it — but that correctness costs it the descriptor lanes entirely. Prefer `CustomJsValue` when the value is lazy but the property is otherwise ordinary. (The *write* fast path and the global-identifier cache deliberately decline to cache `CustomJsValue` descriptors; that is correct-but-uncached, not broken.)

### The subclassing cliff

`ObjectInstance` has two constructors: `protected ObjectInstance(Engine)` and an `internal` one whose parameter types (`ObjectClass`, `InternalTypes`) are themselves internal. A third-party subclass can therefore only reach the protected one, and never carries `InternalTypes.PlainObject` (supplied only through the internal constructor) or `InternalTypes.ShapeMode` (only ever set on the **sealed** `JsObject`). Both are *storage* claims — they let the engine read `_properties` or a shape slot directly — so an object projecting properties lazily from native state could not honour them anyway. The consequence stands: **a host subclass gets no own-property inline caching**, and every own read reaches its `GetOwnProperty`.

What it does get, automatically, is a *semantics* flag. `protected ObjectInstance(Engine)` derives `PropertyAccessSemantics` from the runtime type — overriding `Get` means `Exotic`, not overriding it means `Ordinary` — cached per `Type` process-wide, so the reflection runs once per type however many instances are built. A host declares nothing to be read correctly:

- **Ordinary** (overrides at most `GetOwnProperty`) resolves an own read from a **single** probe: the probe proving the name is not shadowed *is* the read. Such a receiver also reaches the prototype-method inline cache — but only *after* that probe, which re-proves the own miss on every read, so a prototype read costs the same single probe whether the cache is warm or cold.
- **Exotic** (overrides `Get`) routes every read through `Get`, the engine probing nothing itself. This is what a host synthesising values needs, and `Get` is no longer bypassed for names resolving on the prototype.

`SetPropertyAccessSemantics` exists only for the two shapes the rule cannot see: a type overriding `Get` that is nevertheless ordinary (declare `Ordinary` to win the short lane back), and one not overriding `Get` that still is not — e.g. a `GetOwnProperty` unstable across calls for the same name (declare `Exotic`). Call it from the constructor; the last call wins. Debug builds verify an `Ordinary` claim on every read, so running an integration suite against a Debug Jint is the checker.

Release probe counts, pinned by `Jint.Tests.PublicInterface/HostObjectProbeCountTests.cs` and `HostObjectSemanticsTests.cs`: for an ordinary host an own hit costs **1**, a name resolving on the direct prototype **1** (warm or cold), and a name absent everywhere **2** — the probe establishing a miss cannot also produce a value, so that read still ends in a `Get` which re-probes before walking the prototype, the one count the derivation did not improve. An exotic host costs **1** either way, and a member-call base like `host.prop.toUpperCase()` costs **1** in both columns. Under Debug those three ordinary rows read **3 / 3 / 4**, the extra probes being the verifier's own; never quote a Debug figure as a cost.

The prototype row moved. It used to be **0**: the prototype-method cache answered a warm read without asking the host at all. That was wrong — a host keeps its own-property set outside the engine, so nothing bumps `_propertiesVersion` when a projected member appears, and the cached prototype hit went on shadowing it. The probe is what re-proves the own miss and cannot be removed. What the cache still earns is the second probe and the prototype walk: **1** rather than **2**.

Three further levers:

- Override `TryGetOwnPropertyValue` so an own read resolves with **no descriptor at all** — the projected value is handed straight over, on the reads the member lane never sees too (computed keys, `Reflect.get`, the base of a member call). It takes every count in the paragraph above to **0** for that host: `true` carries the value, and `false` is an *authoritative* own miss, which is precisely the fact the discarded probe existed to establish, so the prototype-method cache stays honest without it. That authority is the obligation. `false` must mean `GetOwnProperty` would return `Undefined` — never "I could not produce the value", which would make the read resolve on the prototype, or yield `undefined`, for a property that exists. For a key you cannot serve yourself call `base.TryGetOwnPropertyValue`, which *is* the descriptor-driven answer and costs exactly what not overriding would have. Debug builds verify both directions on every read. Overriding it is the whole opt-in: the engine derives it from the type, so a host that does not is never asked and its counts do not move.
- Override `ProbeOwnProperty` so existence/enumerability questions (`in`, `hasOwnProperty`, `propertyIsEnumerable`, `Object.keys`/`values`/`entries`, `Object.assign`, spread, `JSON.stringify`) are answered without materializing a descriptor at all. The override must agree with `GetOwnProperty` at the same instant — the engine trusts it without re-verifying, and a wrong `Missing` silently drops the key from every enumeration above.
- Where the data is a fixed record, do not subclass at all: `JsObject.Create(engine, layout, values)` and `JsObject.CreateFromEntries` build straight into the hidden-class representation, so objects sharing a layout share one shape and a script reading a batch of them keeps a monomorphic inline cache. `Engine.Advanced.GetObjectRepresentation` lets a test prove the shaping actually happened, since both fall back to the dictionary representation silently.

### When you add a fast lane, decide who can reach it

Several of Jint's fastest lanes are keyed on `internal` type flags or `internal virtual` members that a host type cannot opt into — the member-read inline caches, the built-in fast-call lane, the dense-array lanes. **When adding a new fast lane, decide deliberately whether a host type can reach it, and if not, whether it should be able to.** The `PropertyAccessSemantics` work is the worked example: the lane was originally gated on a storage claim no host could make, and the fix was to split out the weaker *semantics* claim a host could — then derive it, so nobody has to ask. A lane keyed on something only in-box types can assert silently sorts embedders into a slow path they cannot see or escape.

### Engine-affine vs shareable state

- **`Prepared<Script>` / `Prepared<Module>` are reusable and thread-safe** and may be shared across engines. The guarantee is documented on `Engine.PrepareScript` / `Engine.PrepareModule` (not on `Prepared<T>` itself); `Jint.Tests.CommonScripts/ConcurrencyTest.cs` runs one prepared AST on several engines in parallel.
- **Why that holds:** interpreter handler trees accumulate per-node inline caches that are engine-affine, so they are engine-owned (`Engine._functionDefinitions`, `Engine._scriptStatementLists`) and never shared through the AST. The rule for AST `UserData` is *nothing engine-affine may be stashed there* — not *nothing mutable*: mutable but engine-independent state (slot arrays, a compiled-RegExp memo) is deliberately allowed. `JintStatement.Build` carries an explicit `INVARIANT` comment saying so (`Jint/Runtime/Interpreter/Statements/JintStatement.cs:66`); `JintExpression.Build` relies on the same rule but has no equivalent comment. `Jint.Tests/Runtime/GarbageCollectionTests.cs:115` → `SharedPreparedScriptDoesNotRetainEngines` pins it: 20 engines run one prepared script and every one must be collectable while the script stays rooted.
- **`Prepared<T>.ReferencedGlobals` inherits that contract** — it is built once at prepare time and fully immutable afterwards (sorted `string[]` plus an eagerly built lookup, no lazy init that would need synchronization under concurrent readers), so it is safe to read from every engine sharing the `Prepared<T>`. It is `null` unless `CollectReferencedGlobals` was set, which is deliberately distinguishable from an empty set. The collector never touches AST `UserData` and holds nothing engine-affine.
- **`Options` is meant to be shared** across engines, including concurrent ones. Constraints carry per-execution state (a statement counter, a deadline), which used to break that; the built-in helpers now register a **factory** so each engine gets its own instance. The instance overload `Constraint(Options, Constraint)` stays documented as single-engine-only — nothing can be cloned out of an arbitrary user-derived `Constraint`.
- **Shared interop caches may hold only engine-independent values.** Resolved CLR member accessors are cached on `TypeResolver` and shared by every engine using that resolver — and engines constructed without an explicit one all share `TypeResolver.Default`, which lives for the process. The key is `(Type, member name, MemberResolutionRequirement, InteropResolutionProfile)`; the profile is part of it because two engines whose interop configuration differs must not answer from one another's entries. `TypeResolver.IsShareable` is the exclusion: a `NestedTypeAccessor` never enters the cache (it holds a `TypeReference`, a `JsValue` owned by the engine that created it), and an `IndexerAccessor` does not when a host `ITypeConverter` is installed. `TypeReference` keeps its own process-wide static member cache carrying the same hazard on a *weaker* key — just `(Type, member name)`, with no requirement or profile — so it is shared even between engines whose interop configuration differs. Pinned by `Jint.Tests/Runtime/GarbageCollectionTests.cs` → `NestedTypeAccessDoesNotRetainEngines`. A resolver's cache keeps the reflected `Type`s alive for as long as the resolver does, so give a private resolver to any engine that must not outlive the types it touches.

### Gotchas

Each of these cost a real integrator or a real bug.

- **`FastSetProperty` / `FastSetDataProperty` always create an *own* property.** They shadow anything of that name on the prototype chain, invoke no inherited setter, and run no `[[DefineOwnProperty]]` validation (so they can never raise `TypeError`). Storing a raw descriptor under a string key is a dictionary-mode operation, so a shape-mode receiver is permanently deoptimized and forfeits the shape inline cache. Two refinements: symbol keys do not deopt, and a `BuiltinShapeMode` receiver can survive an in-place slot replacement. Use them for setup-time writes only — `Set` for steady-state mutation, `JsObject.Create` to build objects.
- **A registered `IObjectConverter` used to disable the compiled interop member-read lane engine-wide.** It can now declare the CLR types it handles — through the registration overload `AddObjectConverter(converter, params Type[] handledTypes)`, not an interface member — so unrelated *members* keep the read lane. A converter registered untyped still degrades every wrapped member. Note the two lanes are gated differently: the type filter covers the compiled member-read lane, while the compiled method-invoker lane keys on the method's return type being unobservable to a converter (`ReturnValueIsInvisibleToObjectConverters`), so even a typed converter still costs the invoker lane on any method with an observable return type.
- **A non-default `IReferenceResolver` used to disable the member and call inline caches engine-wide.** It can now declare `ReferenceResolverInterests` at registration or via `Options.ReferenceResolverInterests`. The scope was always the non-computed member-read caches, the dense-array indexed-read lane and the member-call callee lane; identifier caches were never affected.
- **Execution constraints and the interpreter's tight-loop lane.** The lane is disarmed whenever the *exact* (non-amortizable) partition is non-empty, with one exception: a lone `MaxStatementsConstraint` is charged inline and keeps the lane armed. Membership is decided by the constraint's own `public virtual bool IsAmortizable => false`. So a timeout or cancellation keeps the lane, `LimitMemory` disarms it, `MaxStatements` + `LimitMemory` together disarm it, and **any user-derived `Constraint` disarms it unless it overrides `IsAmortizable`**. Only override that for a constraint which merely *observes* external state — never one that counts its own invocations, and never a budget over a quantity that can grow without bound between two checks. Debug mode disarms the lane on its own, whatever the constraints, so never measure this with a debugger attached.
- **The handler-tree caches engage only on the *second* evaluation of a given script on a given engine.** A host that builds a fresh engine per operation never reaches them, by design. This concerns that cross-run carry-over only — caches which are not engine-scoped still pay back, and CLR member resolution in particular now survives across engines through the shared `TypeResolver` described above, which is the main reason a fresh engine per operation costs less than it used to.
- **Saturated sentinels register nothing.** `MaxStatements(int.MaxValue)`, `LimitMemory(long.MaxValue)` and `TimeoutInterval(TimeSpan.MaxValue)` — and any non-positive value, including `MaxStatements()`'s own parameter default — produce exactly the same engine as never calling the method, and additionally *remove* any previously registered constraint of that kind. A host spelling "effectively unlimited" that way has no limit, not a very large one.
- **Sharing a `JsValue` across engines** is neither validated nor documented anywhere in this repository. An `ObjectInstance` holds a hard reference to its creating engine and realm, so treat it as unsupported — but do not claim the repo says so.

### Where integrator-facing tests belong

`Jint.Tests.PublicInterface` is the only test project **without** `InternalsVisibleTo` (the grant list is `Jint.Tests`, `Jint.Tests.Test262`, `Jint.Benchmark`, `Jint.Repl`), so a test there actually proves the surface is reachable by a third party. Put new integrator-facing tests there, in **generically named files** describing the capability rather than any particular integrator — the `Host*Tests.cs` family (`HostObjectSemanticsTests`, `HostObjectProbeCountTests`, `HostObjectEnumerationTests`, `HostDelegateTests`, …) is the established precedent. Remember that a `protected internal` member is seen as `protected` from outside the assembly, so an override is spelled `protected override`.

### Benchmarking host-object shapes

`Jint.Benchmark` **does** have `InternalsVisibleTo`, so a "host object" written there can accidentally use members no real embedder could reach. Restrict such types to the public surface deliberately and say so in the type's doc comment — the existing host types in that project do, and record where the restriction bites. Measurement must be serial on an otherwise idle machine; treat a small delta on an untouched control row as the cross-process floor rather than as a result.

## Conventions

Global usings for Acornima and `Acornima.Ast` are defined in `Directory.Build.props`. Nullable reference types are enabled across the codebase, unsafe code is allowed for performance-critical paths, and the latest analyzers run with `EnforceCodeStyleInBuild`.

### Performance is critical

Performance is a first-class concern; every change must consider its impact.

- Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot paths.
- Prefer `readonly struct` and `readonly record struct` with primary constructors for small data types.
- Use `Span<T>`, `ReadOnlySpan<T>` and stack allocation wherever possible.
- Leverage the pools in `Jint.Pooling` instead of allocating fresh instances.
- Mark types `sealed` whenever possible — it enables devirtualization and inlining.
- Prefer `internal` visibility — it avoids virtual dispatch and enables inlining.
- Cache `Prepared<Script>` / `Prepared<Module>` when executing the same source repeatedly, and prefer strict mode, which executes faster.

### Code patterns

- **Lazy initialization** — Built-in objects and their properties initialize on first access to keep startup cheap. `JintStatement` subclasses set `_initialized = false` and override `Initialize()` for deferred setup.
- **Error throwing** — Use the static `Throw.*` helpers (`Jint/Runtime/Throw.cs`) rather than `throw new`; they are `[DoesNotReturn]` and keep non-error paths allocation-free.
- **Built-in JS types** follow the Constructor/Prototype/Instance split matching the spec structure.
- **Engine carries all state** — `ObjectInstance` and most runtime types take `Engine` in their constructors; interpreter classes receive state via `EvaluationContext`.
- **Partial classes** — Large types are split (`Engine.*.cs`, `Intrinsics.*.cs`, `ObjectInstance.*.cs`). Keep related functionality together when editing.
- **Type flags** — The `InternalTypes` enum enables fast type checks without casting; many hot paths depend on it.
- **Property keys** — `KnownKeys` holds pre-computed common property names.
- **Spec references** — Code cites TC39 sections in comments (`// https://tc39.es/ecma262/#sec-...`). Maintain them when editing.

### Data structures

Prefer a **`readonly record struct` over a tuple** for returning multiple values — named properties beat `Item1`/`Item2` at the call site. Mark it `[StructLayout(LayoutKind.Auto)]` and pass it into methods with `in`. Use a class or plain struct instead once the type carries behavior, validation, or many fields.

### Visibility: internal-first

Default every new type, member, field and parameter to the **narrowest visibility that compiles**, and climb only when a real consumer requires it:

1. **`private`** — single-class implementation detail.
2. **`internal`** — shared within the Jint assembly; the default for most new runtime types.
3. **`protected internal`** — extension points on public abstract classes that user-derived classes legitimately need.
4. **`public`** — only when the type appears in an already-public signature, or end users must construct it directly.

If a type is only referenced by `internal` members, it must be `internal`. When a public surface seems to force your hand, first check whether that surface can be split so the implementation detail stays internal — `ModuleImportPhase` stayed internal by splitting `public GetModuleNamespace(Module)` from `internal GetModuleNamespace(Module, ModuleImportPhase)`. Public API is a durable commitment; `internal` costs nothing to widen later.

### Type co-location

Keep small supporting types — enums, record structs, tiny helpers — **in the same file** as the type they serve, provided they share a namespace and the file stays readable (e.g. `ModuleImportPhase` lives in `ModuleRequest.cs`). Split them out when the type has several independent consumers, is `public` and needs its own XML-doc discoverability, or when the file would exceed ~500 lines or mix unrelated concepts.

### Unsigned-cast bounds check (`(uint) i < (uint) length`)

Prefer `(uint) index < (uint) array.Length` over `index >= 0 && index < array.Length` when guarding an indexed access where the index could be negative. Casting `int.MinValue..-1` to `uint` yields values above any non-negative `int`, so the single unsigned comparison is true exactly when `i` is in `[0, length)`. RyuJIT recognizes the idiom, lowers it to one `cmp`/`jae`, and can then elide the bounds check on the following access. It is established here in `DictionarySlim`, `StringDictionarySlim`, `ValueStringBuilder`, `TypeConverter`, `JsNumber` and others.

Use it for manual checks before a direct `array[i]` / `span[i]` where the index could be negative or oversized, and in `for (var i = ...; (uint) i < (uint) arr.Length; ...)` chains. Skip it for ordinary `for (var i = 0; ...)` loops, where the JIT already elides the check and the cast is noise; for indexes known non-negative by construction; and for already-unsigned types. Prefer the plain `(uint) i < (uint) arr.Length` phrasing — variants like `(uint) i <= (uint) (arr.Length - 1)` can defeat the JIT's elision. For `long` lengths use `(ulong)`.

## ECMAScript compliance

Follow the specification as closely as practical, support both strict and sloppy mode with their spec-defined differences, and do not introduce non-standard language extensions. When implementing a feature:

1. Read the TC39 spec section, and cite it in comments.
2. Put the built-in where its peers live under `Jint/Native/` (e.g. `Array/` for Array methods).
3. Register new globals and well-known symbols in `Intrinsics`.
4. Update `TypeConverter` if new coercion rules apply.
5. Add a statement/expression handler under `Runtime/Interpreter/` if it is new syntax.

## Modules

```csharp
var engine = new Engine(options => options.EnableModules(@"C:\Scripts"));
var ns = engine.Modules.Import("./my-module.js");

// or programmatically
engine.Modules.Add("lib", builder => builder.ExportType<MyClass>().ExportValue("version", 1));
```

## Constraints & security

CLR access is disabled by default; enable it with `new Engine(cfg => cfg.AllowClr())`. Execution constraints bound resource use: `options.LimitMemory(4_000_000)`, `options.TimeoutInterval(TimeSpan.FromSeconds(4))`, `options.MaxStatements(1000)`, or a custom `Constraint` subclass. Read the gotchas in the integration-surface section before relying on any of them.

## AOT compatibility

Jint is AOT-compatible for .NET 7.0+ targets (`IsAotCompatible` is set for net7.0+ in `Jint.csproj`). See `Jint.AotExample/` for usage patterns.
