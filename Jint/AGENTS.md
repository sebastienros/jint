# Agent instructions: the Jint engine assembly

> **Read this when:** You are changing anything under `Jint/` that an embedder can observe — a public signature, an extension point, state that outlives one evaluation, or an `*Async` entry point.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

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
- `Jint.Runtime.Modules` — ES module system: `ModuleLoader` resolution, `CyclicModuleRecord` for cyclic dependencies, `ModuleBuilder` for programmatic modules, and the opt-in `NodeStyleModuleLoader`. See [Modules](Runtime/Modules/AGENTS.md#modules).
- `Jint.WebApi` — The opt-in WHATWG web platform APIs (`console`, `DOMException`, …), one subfolder and namespace per feature, every file gated `#if NET8_0_OR_GREATER`. See [Web APIs](WebApi/AGENTS.md#web-apis).
- `Jint.NodeCompat` — Node compatibility that is *not* a web standard, and therefore carries no `WebApiFeatures` flag: it is opt-in and all-TFM, bar the two modules that borrow the WHATWG URL parser (`node:querystring` and `node:url`, gated to `net8.0` like it). Two things live there: the `node:` built-in modules (`Options.UseNodeBuiltinModules`) and the `process` shim (`Options.UseNodeProcess`), whose whole design is what it refuses to expose — `process.env` is empty until the host allowlists a name and is materialized once with script-local writes, `cwd()` answers a configured string rather than the real directory, `argv` is empty, and `exit`/`abort`/`kill` are absent rather than throwing so a script's own feature detection can take its other branch. It installs the same way the web APIs do (a non-clobbering lazy global on the principal realm's global object) but without reaching into `WebApiRegistration`, and it cites [Node's own documentation](https://nodejs.org/api/process.html) the way the web APIs cite WHATWG.
- `Jint.Collections` — High-performance dictionaries (`HybridDictionary`, `StringDictionarySlim`, `DictionarySlim`). `PropertyDictionary` is a global-using alias for `HybridDictionary<PropertyDescriptor>`, which switches between list and hash storage by property count.
- `Jint.Pooling` — Pools for hot allocations (`ReferencePool`, `ArgumentsInstancePool`, `JsValueArrayPool`, `JsValueListBuilder`).

### Configuring an engine: properties first, one verb per intent

`Options` and its nested groups are the configuration surface. **Every setting is a property, and the
property is the only place its value lives.** Do not add an extension method that is one assignment to one
property: through 4.16.x about twenty of those existed, the class holding them called itself a
"compatibility layer to allow fluent syntax", and nothing told a host which of the two spellings to prefer.
They were deleted in v5.

An extension method on `Options` earns its place only by doing something an assignment cannot — install a
subsystem, append to a registry, register a factory the engine invokes later, set two coupled values at
once, or apply a named profile — and its verb says which of those it is:

| verb | intent |
| --- | --- |
| `Use*` | installs a subsystem the engine otherwise does not have (`UseModules`, `UseWebApis`, `UseNodeProcess`, `UseHostFactory`, …) |
| `Add*` / `Remove*` | appends to, or prunes, a registry that holds many (`AddObjectConverter`, `AddExtensionMethods`, `AddLazyGlobal`, `AddConstraint`, `RemoveConstraints`) |
| `Allow*` | grants script a capability that is denied by default (`AllowClr`) |
| `Limit*` | registers a built-in budget constraint (`LimitStatements`, `LimitMemory`, `LimitExecutionTime`) |
| `Observe*` | registers a built-in constraint that watches host state (`ObserveCancellation`) |
| `Set*` | replaces one host-supplied service whose registration an assignment cannot express (`SetReferenceResolver`, `SetTypeConverter`) |
| `For*` | applies a named profile (`ForUntrustedCode`) |
| `Expose*` | widens what script may see, across more than one group (`ExposeDetailedErrors`) |
| `Catch*` | routes CLR exceptions into script (`CatchClrExceptions`) |
| `Configure` | runs a callback against the engine being built |
| `Validate*` / `Ensure*` | inspects configuration; never changes it |

No synonyms. `Enable*`, `Max*`, `Decorate*`, `With*`, `Chain*`, `Disable*` and bare nouns each meant one of
the intents above and are gone; a new method picks the verb whose intent it matches, or it is a property.
`Disable*` in particular: a negated name across the property boundary (`DisableStringCompilation(true)`
setting `Host.StringCompilationAllowed = false`) is how a host ends up writing the opposite of what it meant.

**Every group is a read-only property materialized on first access**, through the one `Options.Materialize`
helper — including the nine on `WebApiOptions`. Two things about that shape are load-bearing. A default
`Options` allocates no group at all, so a host pays only for what it touches and `Options.Apply` can decide
from a null backing field that a whole feature area was never configured. And the publication is
interlocked rather than a plain `??=`, because `Options` is documented as safe to share between engines
being constructed **concurrently** and every engine build reads several groups: two builds racing a plain
`??=` would each get their own instance, and only one of them would see a later host mutation. A new group
uses the same helper and adds a `Clone()` that `CreateEngineOptions` calls, which is what keeps the
untrusted-code profile's private snapshot from sharing state with the host's options.

**`Options` is configuration until an engine reads it, and frozen afterwards.** The `Engine` constructor ends
with `MakeReadOnly()`, which cascades to every group and registry; a group materialized later is born frozen,
which is why `Materialize` takes the owner's state — **by reference, and read a second time after the
interlocked publication**, because the freeze sets the flag and *then* cascades over the backing fields,
so a group materialized inside that window used to be missed by both halves and stay writable on a frozen
`Options` for the life of the process. `SetReadOnly` publishes the flag with a full barrier for the same
handshake, and `OptionsList<T>.Clone` carries it the way a group's `MemberwiseClone` does, so no caller of
a `Clone` has to re-freeze what it copied. A new setting needs `ThrowIfReadOnly()` in its setter
(`[CallerMemberName]` names it), a new registry is an `OptionsList<T>` rather than a `List<T>`, and a new
group implements `IOptionsGroup` and joins the two cascades in `Options.ReadOnly.cs`. A setting that
*memoizes on read* resolves in `SetReadOnly` as well, ahead of the flag — `Engine.Options` hands the frozen
instance to a host, so reading one of its members must never be a write to it (`Options.TimeSystem` was, and
two threads reading it got a clock each); and a public method that writes a bare field rather than a guarded
property refuses through a door of its own (`Options.SetNodeBuiltinModules`), or it is the one write nothing
stops. Nothing Jint writes to `Options` may happen after that line — the profile re-expansion and the host's
`Configure` callbacks both run inside `Options.Apply`, well before it. The one sanctioned post-construction write is
`Engine.WebApi.Enable`'s callback, and it writes to a copy of the web-API subtree the engine takes for itself
first (`Engine.TakePrivateWebApiOptions`) — an `Options` is shareable, and for `new Engine()` it is one Jint
keeps process-wide, so a per-tenant client set there used to reach every default-built engine. The copy stays
frozen; the guard is suspended for it on the calling thread alone.

**Every field derived from an option is read after `Options.Apply`, and the two exceptions are refusals.**
`Apply` runs the host's `Configure` callbacks and then re-expands an untrusted-code profile over whatever
they wrote, so it is the first point at which the options are final; a reading taken above it silently
ignores a callback that writes it, which is what happened to the extension-method lookup
([#3568](https://github.com/sebastienros/jint/issues/3568)) and then to strict mode, the object converters
and the constraints ([#3583](https://github.com/sebastienros/jint/issues/3583)). The **one** group taken
before `Apply` is interop conversion — `_objectConverters`, its type filter, `_immutableCrossingFilter`,
`_enumsAsStrings`, plus `_extensionMethods` — because a callback's `engine.SetValue(name, clrValue)`
converts and so must find them built; each is taken *again* after `Apply`
(`TakeInteropConversionState`, `RefreshExtensionMethods`), unconditionally, so that a callback's
registration is honoured and a registry the profile cleared is obeyed.

A new field goes below `Apply`; one that must exist earlier joins that group and is re-taken. What cannot be
re-taken is **refused where it is written**, never ignored — two things are, and both pay for the rule rather
than costing it. A callback cannot run script ([#3581](https://github.com/sebastienros/jint/issues/3581)),
because it runs before Jint installs its globals and before the re-expansion, neither of which can move; that
refusal is what lets `_evaluationContext`, the constraints and the parser sit below `Apply`. And it cannot
declare an untrusted-code profile ([#3582](https://github.com/sebastienros/jint/issues/3582)): the expansion
that hardens an engine runs in `CreateEngineOptions`, before `Reset()` builds the realm from
`Options.Host.Factory`, so a late one could only half-harden.

### Type co-location

Keep small supporting types — enums, record structs, tiny helpers — **in the same file** as the type they serve, provided they share a namespace and the file stays readable (e.g. `ModuleImportPhase` lives in `ModuleRequest.cs`). Split them out when the type has several independent consumers, is `public` and needs its own XML-doc discoverability, or when the file would exceed ~500 lines or mix unrelated concepts.

### Unsigned-cast bounds check (`(uint) i < (uint) length`)

Prefer `(uint) index < (uint) array.Length` over `index >= 0 && index < array.Length` when guarding an indexed access where the index could be negative. Casting `int.MinValue..-1` to `uint` yields values above any non-negative `int`, so the single unsigned comparison is true exactly when `i` is in `[0, length)`. RyuJIT recognizes the idiom, lowers it to one `cmp`/`jae`, and can then elide the bounds check on the following access. It is established here in `DictionarySlim`, `StringDictionarySlim`, `ValueStringBuilder`, `TypeConverter`, `JsNumber` and others.

Use it for manual checks before a direct `array[i]` / `span[i]` where the index could be negative or oversized, and in `for (var i = ...; (uint) i < (uint) arr.Length; ...)` chains. Skip it for ordinary `for (var i = 0; ...)` loops, where the JIT already elides the check and the cast is noise; for indexes known non-negative by construction; and for already-unsigned types. Prefer the plain `(uint) i < (uint) arr.Length` phrasing — variants like `(uint) i <= (uint) (arr.Length - 1)` can defeat the JIT's elision. For `long` lengths use `(ulong)`.

### Code patterns

- **Lazy initialization** — Built-in objects and their properties initialize on first access to keep startup cheap. `JintStatement` subclasses set `_initialized = false` and override `Initialize()` for deferred setup.
- **Error throwing** — Use the static `Throw.*` helpers (`Jint/Runtime/Throw.cs`) rather than `throw new`; they are `[DoesNotReturn]` and keep non-error paths allocation-free.
- **Built-in JS types** follow the Constructor/Prototype/Instance split matching the spec structure.
- **Engine carries all state** — `ObjectInstance` and most runtime types take `Engine` in their constructors; interpreter classes receive state via `EvaluationContext`.
- **Partial classes** — Large types are split (`Engine.*.cs`, `Intrinsics.*.cs`, `ObjectInstance.*.cs`). Keep related functionality together when editing.
- **XML doc comments** — Every declaration of the public API surface carries a `<summary>`, and a test in `Jint.Tests.PublicInterface` says which do not. Before writing or rewriting one, read [`docs/xml-doc-style.md`](../docs/xml-doc-style.md): one sentence of ≤ 25 words, no `<para>` inside `<summary>`, `<remarks>` capped at four short paragraphs of caller guidance, and no history or benchmark numbers.
- **Type flags** — The `InternalTypes` enum enables fast type checks without casting; many hot paths depend on it.
- **Property keys** — `KnownKeys` holds pre-computed common property names.
- **Spec references** — Code cites the section it implements, in a `<summary>` or a comment, and the URL says which document is authoritative: `https://tc39.es/ecma262/#sec-...` for merged language features, `https://tc39.es/ecma402/#sec-...` for i18n, and `https://tc39.es/proposal-<name>/#sec-...` for a feature that is still a proposal. Maintain them when editing, and re-point a proposal's citations when it merges into ECMA-262 — the anchors get renamed on the way in (`sec-iteratorprototype.take` became `sec-iterator.prototype.take`). The web APIs under `Jint/WebApi/` are owned by WHATWG living standards instead, and [`Jint/WebApi/AGENTS.md`](../Jint/WebApi/AGENTS.md#citing-the-living-standard) says which document owns what. Every anchor cited here is registered in `Jint.Tests/SpecAnchors.txt`; a citation it does not hold fails `SpecCitationTests`, and `JINT_SPEC_ANCHORS=update` re-verifies the register against the living documents.

### Data structures

Prefer a **`readonly record struct` over a tuple** for returning multiple values — named properties beat `Item1`/`Item2` at the call site. Mark it `[StructLayout(LayoutKind.Auto)]` and pass it into methods with `in`. Use a class or plain struct instead once the type carries behavior, validation, or many fields.

### Visibility: internal-first

Default every new type, member, field and parameter to the **narrowest visibility that compiles**, and climb only when a real consumer requires it:

1. **`private`** — single-class implementation detail.
2. **`internal`** — shared within the Jint assembly; the default for most new runtime types.
3. **`protected internal`** — extension points on public abstract classes that user-derived classes legitimately need.
4. **`public`** — only when the type appears in an already-public signature, or end users must construct it directly.

If a type is only referenced by `internal` members, it must be `internal`. When a public surface seems to force your hand, first check whether that surface can be split so the implementation detail stays internal — `ModuleImportPhase` stayed internal by splitting `public GetModuleNamespace(ModuleRecord)` from `internal GetModuleNamespace(ModuleRecord, ModuleImportPhase)`. Public API is a durable commitment; `internal` costs nothing to widen later.

## AOT compatibility

Jint is AOT-compatible for .NET 7.0+ targets (`IsAotCompatible` is set for net7.0+ in `Jint.csproj`). See `Jint.AotExample/` for usage patterns.

### What counts as a public contract

The table of what an embedder may rely on — signatures, observable behaviour, the snapshot, event-loop and
`*Async` rules — lives with the baselines that pin it, in
[`Jint.Tests.PublicInterface/AGENTS.md`](../Jint.Tests.PublicInterface/AGENTS.md#what-counts-as-a-public-contract).
The one rule to carry across without opening it: **changing observable behaviour is breaking even when the
signature is untouched**, so assume an embedder depends on it before simplifying it.

### Gotchas

Each of these cost a real integrator or a real bug. These are the ones that bite in this area; the
rest of the list is split across the files indexed from the repository-root [`AGENTS.md`](../AGENTS.md).

- **Nothing on the public surface says a `JsValue` belongs to one engine, and that is the gap rather than the rule.** The rule itself is a root-[`AGENTS.md`](../AGENTS.md) gotcha: sharing a `JsValue` across engines is unsupported and nothing validates or guards it. What is missing is the record of it on the API itself. [`docs/guide/advanced-hosting.md`](../docs/guide/advanced-hosting.md) states it, but there is still no XML doc on `JsValue`, none on `Engine.SetValue`, and no test pinning the behaviour — so the first thing a change here owes is one of each, and until they exist the constraint survives only as prose somebody has to have read.

- **An `*Async` entry has two ways out, and which one a failure takes is a contract, not an accident.** Everything the operation itself does — parsing, running the script, an execution constraint tripping, a promise rejecting — is delivered through the returned `Task`. Only a **usage error** is thrown out of the call: a `null` argument, a `Prepared<Script>` that did not come from `PrepareScript`, and the `InvalidOperationException` refusing the call because the engine is already in use. Those three say the operation never started, so there is nothing for a task to describe; everything else says it started and failed, and a host that wrapped its `await` must see it. The rule holds for the whole family — `EvaluateAsync` and `ExecuteAsync` (both overloads each), `InvokeAsync`, `Modules.ImportAsync`, `Tasks.WaitForScheduledWorkAsync`, `JsValue.UnwrapIfPromiseAsync` — which is the point of stating it: before it did, the family was split by nothing more principled than which methods happened to be declared `async`. `ExecuteAsync` was, so a budget failure reached its task; `EvaluateAsync` was not, so the identical failure on the identical script erupted from the call. Worse, it erupted *conditionally*: `MemoryLimitConstraint` charges a host callback running on another thread to the same operation, so whether that charge landed before or after the engine thread reached `ScriptEvaluation`'s post-script check decided whether the exception came back as a faulted task or on the caller's stack — a coin flip resolved by thread scheduling, which a host cannot write a `catch` against (issue #3241; it flaked two unrelated PRs in one day). The mechanics that make the rule true: the entry validates arguments, takes the reservation, and *then* hands off to a `private async` body which owns the release in a `finally`. An `async` body captures its synchronous phase into the task, which is exactly the property wanted, so **never move the reservation into that body** (the failure it reports is a usage error) and **never hoist work out of it** (everything else belongs to the task). Nothing about the constraints themselves changed: same exception type, same message, still fatal, still bounding the same work — only the channel is now deterministic. Pinned from the embedder's side in `Jint.Tests.PublicInterface/HostAsyncFailureChannelTests.cs`, in both directions, including a forced-ordering reproduction of the cross-thread charge that needs no repetition to mean something.
- **`RestoreGlobalSnapshot` bumps version counters, it never restores them.** `_propertiesVersion`, `GlobalEnvironment._lexicalMutations`, `Engine._envBindingInjectionEpoch` and `EventLoop.Generation` are what every inline cache — and, for the last one, every queued job — validates against, so putting a counter *back* could make an entry built before the capture compare equal again and be revalidated against state it never saw. Anything added to the restore path obeys the same rule. The API's other half is its honesty: it reverts the global *binding table*, and explicitly not intrinsic/prototype mutations, object graphs behind restored bindings, host CLR state (including `Engine.HostDefined`, which a pooled engine keeps across a restore and the host swaps per request itself), `Symbol.for`, or the module registry — it is a configuration-reuse primitive, not an isolation boundary, and the non-guarantees are pinned as surviving in `Jint.Tests.PublicInterface/GlobalSnapshotTests.cs`. **A lazy global's memoized value belongs on that list, and this is the one nobody expects.** A restore *does* return an unmaterialized `LazyPropertyDescriptor` to its unmaterialized state, so the factory runs again — but a restore can only revert the descriptor, never whatever the factory *reads from*, so what the second run produces is decided entirely by the factory. Every global Jint installs bar two shapes is `e => e.Realm.Intrinsics.Something` over a memo, so `Math`, `JSON`, `console`, `crypto`, `performance`, `caches`, `localStorage`, `navigator`, `scheduler` and the other 140 come back carrying the previous cycle's monkey-patches; a *census* proving that (150 globals on the fullest engine, exactly nine rebuilt) is `WebApiRegistrationTests.OnlyTheGlobalObjectsOwnFunctionSlotsAreRebuiltByARestore`. The two exceptions both hold their value in the reverted slot and nowhere else: the global object's own `[JsFunction]` slots (`decodeURI` and its eight siblings — but *not* `parseInt`/`parseFloat`, which are `Intrinsics.ParseInt`/`ParseFloat` and therefore memoized), and `Jint.NodeCompat`'s `process`, whose factory constructs. The same rule is what a host has to apply to `AddLazyGlobal`: a factory that constructs gives the next cycle a fresh value, one that caches gives it the previous cycle's. Three sites used to say the web-API singletons were "rebuilt" (issue #3267); making that true was considered and rejected — `console` is structurally the 59th `[JsIntrinsicReference]` and reverting it means reverting the realm, dropping the memo desynchronizes it from a descriptor a capture-after-materialization reinstates, and it would not even isolate, since a singleton's methods live on an interface prototype that is a separate memo (`PerformanceTimelineTests.TheBufferBelongsToTheEngineAndSurvivesAGlobalSnapshotRestore` already pinned the opposite). Since bare identifiers resolve through the global's whole prototype chain, surviving intrinsic pollution is now a surviving **binding** as well as a surviving property: `Object.prototype.leaked = 1` in one cycle makes `leaked` resolvable as a free identifier in the next, where before it was readable only as `globalThis.leaked` and `typeof leaked` answered `"undefined"`. The global's own `[[Prototype]]` is a different matter and *is* captured and restored.
- **Discarding the event loop is a fence, not a flush.** `EventLoop.Clear()` can only throw away what is already queued, and the case that matters is the one where nothing is: a fire-and-forget async function suspended on a CLR `Task` enqueues its settle whenever that task happens to complete, which can be after a restore — and the resumed body then writes the previous cycle's data into the restored globals, a cross-cycle channel the fresh-engine-per-evaluation pattern never had. The fix is a generation captured at promise **registration** (engine thread), carried in the `EventLoopJob`, and checked at **dequeue** (engine thread): both ends are ordered by the single-thread contract, where a check inside the settle closure would race the restore. Any new enqueue path must stamp the registering cycle's generation, not the current one, whenever the two can differ. The consequence for hosts is real and documented: a promise registered before a restore never settles into the engine afterwards.
- **A suspended `EvaluateAsync` is invisible to the ordinary "evaluation in progress" signals.** Its synchronous phase has run to completion, so `_hostEntryDepth` is back to zero and the execution-context stack is back at base depth while the settlement loop sits in its `await`; `Engine._pendingAsyncOperations` is the only thing that sees it, and `RestoreGlobalSnapshot` consults it. Any future guard that means "is the engine busy" needs all three signals.
- **A public property of an exception must not return by reference.** Everything that renders a failed run reads one reflectively - a test runner, a structured logger, an error page - and .NET Framework answers `PropertyInfo.GetValue` on a by-ref-returning property with `NotSupportedException: ByRef return value not supported in reflection invocation` rather than dereferencing it the way .NET Core does. `JavaScriptException.Location` was `ref readonly` from #1270 until [#3549](https://github.com/sebastienros/jint/issues/3549): on `net472` a red test printed that reflection message in place of the JavaScript error and then took the test host down, losing every test queued behind it, while the identical test on `net8.0` reported normally. It returns by value now, and the private inner exception a renderer reaches through `InnerException` keeps its by-ref accessor `internal`, which is enough to hide it from `BindingFlags.Public`. Nothing about it is NUnit-specific - the same getter is what an embedder's logger calls - so `Jint.Tests.PublicInterface/ExceptionPropertyReflectionTests.cs` holds every `Exception` in the assembly to the rule, on every target framework rather than only the one that would crash. A non-exception type may still return by reference; `Completion`, `CallFrame`, `DebugInformation` and `ExceptionThrownEventArgs` all do, deliberately.
- **`PauseOnExceptions` rides a count the try statement keeps**; see [`Runtime/Interpreter/AGENTS.md`](Runtime/Interpreter/AGENTS.md).
- **`DebugHandler.GetStepLocations` and the step lane are one statement written twice.** `Runtime/Debugger/StepLocationCollector.cs` reproduces statically what `Engine.RunPerStatementChecks` (every statement whose node type is not `BlockStatement`), the loop handlers (a `test`, an `update`, a `for`-`in`/`of` `left`) and `ScriptFunction.OnReturnPoint` visit at run time, so a new step site has to be added on both sides. `Jint.Tests/Runtime/Debugger/StepLocationTests.cs` fails otherwise: it steps a corpus with `StepMode.Into` and compares the two sets, runtime as oracle. One position the walk cannot report is pinned there rather than fixed - `eval` is a program of its own - while a class field initializer and a static block report real positions only because `ClassDefinition` stamps its synthesized nodes with them. The third case is closed instead of pinned: a derived class with no explicit constructor runs `super(...args)` from a statically parsed AST belonging to no program, so the step lane is transparent to it (`ClassDefinition.IsSynthesizedConstructor` / `IsSynthesizedConstructorStatement`) and an implicit constructor is crossed as one unit - anything else synthesized from an AST no host was handed owes the same suppression, or the two sets part again.
