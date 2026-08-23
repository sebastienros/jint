# Migrating from Jint 4.16 to Jint 5

Jint 5 is under development on `main`. This document is the running record of every change a
4.16.x embedder has to react to; it is written for someone upgrading, so it says what broke and
what to type instead, and leaves the rationale to the linked pull requests.

**v5 is not released yet.** `main` is a moving target and rows may still be added, amended or
withdrawn before it ships. Anything not listed here is intended to keep working unchanged.

- [1. Target frameworks](#1-target-frameworks)
- [2. Removed API](#2-removed-api)
- [3. Renamed and reshaped API](#3-renamed-and-reshaped-api)
- [4. Breaking without a signature change](#4-breaking-without-a-signature-change)
- [5. New in v5](#5-new-in-v5)
- [6. AOT](#6-aot)
- [Keeping this document current](#keeping-this-document-current)

## 1. Target frameworks

| Target | 4.16.x | 5.x |
| --- | --- | --- |
| `net462` | yes | **dropped** (planned) |
| `net472` | — | **added** (planned) |
| `netstandard2.0` | yes | yes |
| `netstandard2.1` | yes | yes |
| `net8.0` | yes | yes |
| `net10.0` | yes | yes |

> **Status: planned, not yet on `main`.** `Jint/Jint.csproj` still lists
> `net462;netstandard2.0;netstandard2.1;net8.0;net10.0`. Update this section when the change lands.

The `netstandard` and modern .NET targets are deliberately kept. `netstandard2.0` in particular is
what game engines and other embedded runtimes resolve against, and it costs far more to drop than
raising the .NET Framework floor does.

A consumer pinned to `net462`, `net47` or `net471` stays on the **4.16.x** line, which continues to
receive correctness and conformance fixes on the `4.x` branch.

.NET Framework support is expected to end with **v6**; treat v5 as the last major version that
carries a `net4x` target.

## 2. Removed API

This table is filled by the pull request that removes the member. A member that merely became
*less* accessible is listed here too: it breaks the same callers a deletion does.

| Removed | Replacement | PR |
| --- | --- | --- |
| `Options.StringCompilationAllowed` | `Options.Host.StringCompilationAllowed` — the property it already forwarded to | [#3304](https://github.com/sebastienros/jint/pull/3304) |
| `ExperimentalFeature.Generators` | nothing; generators are unconditional. The flag was `[Obsolete(error: true)]`, so nothing compiled against it. Bit 1 is not reused, so any other persisted `ExperimentalFeature` value keeps its meaning | [#3304](https://github.com/sebastienros/jint/pull/3304) |
| `TypeConverter.CheckObjectCoercible(Engine, JsValue)` | `TypeConverter.RequireObjectCoercible(Engine, JsValue)` — same behaviour, spec name | [#3304](https://github.com/sebastienros/jint/pull/3304) |
| `DeclarationBindingType` (enum) | nothing. It was public, had no members that anything read, and had exactly one reference in the repository: its own declaration | [#3304](https://github.com/sebastienros/jint/pull/3304) |
| `JsValue`'s `System.IConvertible` implementation | nothing. It was an explicit implementation, so `jsValue.ToInt32(...)` never compiled; only `((IConvertible) jsValue)` did, and 9 of its 17 members threw `NotImplementedException`. Use `JsValue.ToObject()`, or the `AsNumber()` / `AsString()` / `AsBoolean()` extension helpers | [#3304](https://github.com/sebastienros/jint/pull/3304) |
| `Realm()` (public parameterless constructor) | nothing. A `Realm` only makes sense as the one an `Engine` built; get it from `Engine.Advanced.HostDefined` or `Host.InitializeShadowRealm`. It was `public` only inside `#if DEBUG`, plus the implicit constructor in Release, so the shipped package's surface differed from a source build's | [#3304](https://github.com/sebastienros/jint/pull/3304) |
| `Engine.ModuleOperations(Engine, IModuleLoader)` → `internal` | nothing. `Engine.Modules`' setter is `internal`, so an instance a host constructed could never be installed | [#3304](https://github.com/sebastienros/jint/pull/3304) |
| `JsMap(Engine, Realm)` → `internal` | nothing. It required a `Realm`, which a host has no supported way to obtain; `JsSet`'s equivalent was already `internal` | [#3304](https://github.com/sebastienros/jint/pull/3304) |

### 2.1 Sealed types

`Options` and all nineteen of its nested option groups are now `sealed`. None had a virtual member, so a
subclass overrode nothing — and `Options.Clone()` is `MemberwiseClone`-based, so it silently sliced one.

```c#
// 4.16.x — compiled, and Clone() returned an Options, not a MyOptions
class MyOptions : Options { }

// 5.x — configure through the options object instead of subclassing it
var engine = new Engine(options => { options.Strict = true; /* … */ });
```

The sealed groups: `CacheOptions`, `ConsoleOptions`, `ConstraintOptions`, `CoverageOptions`,
`DebuggerOptions`, `DiagnosticsOptions`, `FetchOptions`, `HostOptions`, `InteropOptions`, `IntlOptions`,
`JsonOptions`, `MessagingOptions`, `ModuleOptions`, `ProfilingOptions`, `StorageOptions`, `TemporalOptions`,
`TimerOptions`, `WebApiOptions`, `WorkerOptions` (`ParsingOptions` already was).

### 2.2 The declared non-contracts now say so to the compiler

`Jint/AGENTS.md` has always said that a few public types are diagnostics rather than contracts — what they
report names an internal representation, so the answer may change in any release and the types may gain
members. Nothing in the type system said so. They now carry
`[Experimental("JINT0001")]`, which is a compiler **error** at the call site until it is acknowledged:

| Marked | |
| --- | --- |
| `ObjectRepresentation`, `Engine.Advanced.GetObjectRepresentation` | to *assert* an object is shaped, use `Engine.Advanced.HasSharedShape`, which **is** a contract and is not marked |
| `Engine.Advanced.GetMemoryReport` and the `Jint.Diagnostics` records (`EngineMemoryReport`, `HandlerTreeCacheReport`, `InteropCacheReport`, `PoolReport`, `ObjectCensusReport`) | |
| `InteropConversionDiagnostics`, `Engine.Advanced.GetInteropConversionDiagnostics` | |

```c#
#pragma warning disable JINT0001 // Jint diagnostic API, deliberately outside the compatibility contract
    var report = engine.Advanced.GetMemoryReport();
#pragma warning restore JINT0001
```

or, for a host that logs one on every request, `<NoWarn>$(NoWarn);JINT0001</NoWarn>` once in the project
file. The identifier is stable: a member marked `JINT0001` keeps that identifier for as long as it is marked,
so the suppression does not have to be revisited.

Two things this deliberately does *not* mark. `Engine.Advanced.ProcessTasks` is the canonical host loop —
every host with timers, promises or workers must call it — so its stale "this API may break and change
behavior!" line was corrected rather than promoted to an attribute. `Engine.Advanced.RegisterPromise` is a
real capability rather than a report about an internal representation, so its equally stale
"EXPERIMENTAL! Subject to change." banner was removed for the same reason: once the attribute exists, the
word has to mean one thing.

### 2.3 `UnwrapIfPromise()` honours the configured promise timeout

`JsValueExtensions.UnwrapIfPromise()` — the overload with no arguments — hard-coded ten seconds instead of
reading `Options.Constraints.PromiseTimeout`, whose default is also ten seconds. A host that configured a
different value was silently ignored. It now reads the promise's own engine. Nothing changes for a host that
left the default, and the `TimeSpan` and `CancellationToken` overloads are untouched.

## 3. Renamed and reshaped API

*Nothing recorded yet.* No public member has been renamed, and no public signature reshaped, since
`v4.16.0`.

Entries here are before/after code, not prose:

```c#
// 4.16.x
// (before)

// 5.x
// (after)
```

## 4. Breaking without a signature change

This is the section that matters most, because a compiler cannot find any of it. Every row below
compiles exactly as it did in 4.16 and behaves differently at run time.

### 4.1 Changed defaults, at a glance

| PR | Setting | 4.16.x | 5.x | Restore the 4.16 behaviour |
| --- | --- | --- | --- | --- |
| [#3054](https://github.com/sebastienros/jint/pull/3054) | `Interop.AllowWrite` | `true` | `false` | `options.AllowClrWrite()` |
| [#3056](https://github.com/sebastienros/jint/pull/3056) | `Interop.ArrayConversion` | `LiveView` | `Copy` | `options.Interop.ArrayConversion = ArrayConversionMode.LiveView;` |
| [#3057](https://github.com/sebastienros/jint/pull/3057) | `Constraints.StackOverflowGuard` | `false` | `true` | `options.Constraints.StackOverflowGuard = false;` |
| [#3058](https://github.com/sebastienros/jint/pull/3058) | `AgentCanSuspend` | `true` | `false` | `options.AgentCanSuspend = true;` |
| [#3051](https://github.com/sebastienros/jint/pull/3051) | script-visible CLR / module error text | detailed | redacted | `options.ExposeDetailedErrors()` |
| [#3052](https://github.com/sebastienros/jint/pull/3052) | namespace type discovery | implicit assembly search | closed allow-list | `options.AllowClr(typeof(YourType).Assembly)` |

Each is expanded below.

### 4.2 Projected CLR writes are disabled by default ([#3054](https://github.com/sebastienros/jint/pull/3054))

Script can no longer write through a wrapped CLR object — fields, properties, indexers, dictionary
entries, list and array elements. In sloppy mode the write is silently ignored; in strict mode it
raises a `TypeError`. Calling a CLR method or extension method that mutates host state is
unaffected: that is a capability the host handed out, not a projected write.

```c#
// 4.16.x — `host.Count = 5` wrote through
var engine = new Engine().SetValue("host", host);

// 5.x — the same engine now refuses that write; opt back in explicitly
var engine = new Engine(options => options.AllowClrWrite())
    .SetValue("host", host);
```

### 4.3 CLR array projection defaults to isolated copies ([#3056](https://github.com/sebastienros/jint/pull/3056))

`T[]` crossing into script is snapshotted into a real JavaScript array instead of being exposed as a
live, fixed-size view. Script-side mutations affect only the copy, and CLR-side mutations after the
conversion are not visible through it. (`LiveView` had itself only been the default since 4.14.)

Two consequences a script can see directly: `Array.isArray` now answers `true` where the view
answered `false`, and `push`, `pop` and `length` writes now succeed on the copy where the fixed-size
view threw `TypeError`.

```c#
// 5.x — opt back into the live view; write-through additionally needs AllowClrWrite()
var engine = new Engine(options =>
{
    options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
    options.AllowClrWrite();
});
```

A copy costs per element on the crossing where a view cost a single wrapper, so a host projecting
large arrays on a hot path should measure before accepting the default.

### 4.4 CLR type and member resolution is contained ([#3052](https://github.com/sebastienros/jint/pull/3052))

`Interop.AllowedAssemblies` is now a **closed** allow-list for namespace resolution. 4.16 fell back
to `Assembly.GetCallingAssembly()`, `Assembly.GetExecutingAssembly()` and `Type.GetType(name)`, so
`importNamespace` could reach types the host never named. Namespace lookup also admits only public
top-level types and nested types whose complete declaring-type chain is public, and the type-widening
`clrHelper` operations now require `Interop.AllowGetType`.

```c#
// 4.16.x — the host assembly was searched implicitly
var engine = new Engine(options => options.AllowClr());

// 5.x — name the assemblies whose namespaces script may resolve
var engine = new Engine(options => options.AllowClr(typeof(MyModel).Assembly));
```

`AllowClr()` with no arguments adds the assembly containing `object`, so `System.*` keeps working.
CLR objects, delegates and `TypeReference` values the host exports explicitly are capabilities in
their own right and are not subject to this policy.

### 4.5 The stack-overflow guard is enabled by default ([#3057](https://github.com/sebastienros/jint/pull/3057))

`Options.Constraints.StackOverflowGuard` defaults to `true`, so an unbounded script recursion raises
`RangeError: Maximum call stack size exceeded` — an ordinary JavaScript error, catchable by the
script and by `catch (JavaScriptException)`, with the engine still usable — instead of exhausting the
native stack and ending the host process with no exception at all.

It measures the remaining native stack rather than counting calls, which is what lets it cover the
routes a frame count cannot: `new`, a getter, a coercion, a Proxy trap, a host delegate calling back
in, and a recursion whose every level is a *different* function (`eval`, `new Function`).
`LimitRecursion(n)` answers a different question and takes precedence where it is configured.

Measured cost: recursion-heavy workloads roughly 1.5–3% slower, hot shallow calls within run-to-run
noise. Turn it off only for trusted, independently bounded scripts:

```c#
var engine = new Engine(options => options.Constraints.StackOverflowGuard = false);
```

See [Surviving an unbounded recursion](../README.md#surviving-an-unbounded-recursion).

### 4.6 Atomics agent suspension is disabled by default ([#3058](https://github.com/sebastienros/jint/pull/3058))

`Atomics.wait` on a default engine throws a JavaScript `TypeError` before registering a waiter.
`Atomics.waitAsync` is unaffected and remains available. A script can call `Atomics.wait` with no
timeout, so on a request, UI or event-loop thread the previous default let a script block that
thread indefinitely.

```c#
// worker-like host where blocking is acceptable
var engine = new Engine(options => options.AgentCanSuspend = true);
```

### 4.7 Concurrent `Engine` use is rejected ([#3035](https://github.com/sebastienros/jint/pull/3035))

Concurrent use of one `Engine` was always unsupported; it now fails fast with
`InvalidOperationException` instead of racing or appearing to work. Public host entries — execution,
mutation, modules, debugger state, conversion, the event loop — check ownership, and an engine stays
reserved for the whole lifetime of a returned async `Task`.

What a host has to change:

- **await before reuse.** `EvaluateAsync`, `ExecuteAsync`, `InvokeAsync`, `Modules.ImportAsync` and
  `UnwrapIfPromiseAsync` must complete before the engine is returned to a pool, reused, or disposed.
  `Dispose` fails fast while an operation owns the engine, which is observable during exception
  unwinding — a `using` scope must not outlive an async engine operation.
- **no nested async entry.** Starting an async engine API from inside an active engine callback is
  rejected before any work starts.
- **same-thread re-entry still works**, and a JavaScript callback converted to a CLR delegate may
  still be dispatched from another thread inside one of the four callback-admission windows.

The full contract, including those windows, is in
[Thread-safety](../README.md#thread-safety).

### 4.8 Memory is accounted across async continuations ([#3036](https://github.com/sebastienros/jint/pull/3036))

`LimitMemory` now charges managed allocations to the engine operation across promise reactions,
event-loop jobs and asynchronous module completions, including ones that resume on a different
thread. A budget that only ever saw one synchronous segment in 4.16 can therefore trip where it
previously did not. `MemoryLimitAccuracy` reports what the accounting can and cannot see, and
`MemoryLimitConstraint.Begin`/`End` brackets one budget across a multi-entry host operation.

### 4.9 Script-visible error text is redacted ([#3051](https://github.com/sebastienros/jint/pull/3051))

Host exception messages, module-loader failure messages and CLR resolution details are replaced with
generic text, because they routinely carry filesystem paths, URLs, connection strings and CLR type
names. Nothing host-side is lost: `JintException.TryGetClrException`, `TryGetClrType`,
`TryGetClrMemberName` and the CLR error decorators still see everything.

```c#
// 5.x — restore the 4.16 development-friendly messages on all three surfaces
var engine = new Engine(options => options
    .CatchClrExceptions()
    .EnableModules(loader)
    .ExposeDetailedErrors());
```

Narrower opt-ins: `Interop.ExposeDetailedExceptionMessages`, `Interop.ExposeDetailedResolutionErrors`
(which already existed and already defaulted to `false`), `Modules.ExposeDetailedLoadErrors`. See
[Detailed development errors](../README.md#detailed-development-errors).

### 4.10 `*Async` entries have one deterministic failure channel ([#3252](https://github.com/sebastienros/jint/pull/3252))

`EvaluateAsync` and `InvokeAsync` used to report parse errors, script throws and constraint failures
by throwing out of the call; `ExecuteAsync` and `Modules.ImportAsync` reported the identical failure
through the returned `Task`. Which one you got could even depend on thread scheduling, because a
host callback on another thread is charged to the same memory budget. A host could not write a
`catch` against that.

The rule now, for the whole family:

| Failure | Where it arrives |
| --- | --- |
| parse error, script throw, constraint tripping, promise rejection | the returned `Task` |
| `null` argument, a `Prepared<Script>` not from `PrepareScript`, engine already in use | thrown out of the call |

```c#
// 5.x — the catch goes around the await, wherever you await it
var pending = engine.EvaluateAsync(untrustedScript);   // never throws the script's failure
try
{
    var result = await pending;
}
catch (MemoryLimitExceededException) { }
catch (JavaScriptException) { }
```

Two smaller consequences: `ExecuteAsync` and `Modules.ImportAsync` now throw the concurrent-use
`InvalidOperationException` **synchronously** rather than faulting the task, and a `null` source now
raises `ArgumentNullException` where 4.16 raised `NullReferenceException` from inside the parser. No
constraint is weakened — same exception type, same message, same aborted run. See
[Where failures arrive](../README.md#where-failures-arrive).

### 4.11 Array-like `length` above 2^32−1 ([#3248](https://github.com/sebastienros/jint/pull/3248))

`ArrayOperations` carried the array-like length in two widths and only the `ulong` one clamped, so
an out-of-range `double`→integer conversion saturated on .NET and was unspecified on .NET Framework:
a `length` of 2^53 read as `4294967295` on `net10.0` and as `0` on `net472`, from the same script.
The `uint` overload is deleted rather than clamped, so every caller now implements
[*LengthOfArrayLike*](https://tc39.es/ecma262/#sec-lengthofarraylike) over its real `[0, 2^53−1]` range.

`ArrayOperations` is internal, so nothing to recompile. What a script sees changes:

```js
Array.from({ length: 2 ** 53 })
// 4.16: allocated a 4294967295-length array
// 5.x:  RangeError, from ArrayCreate

new Uint8Array(4).set([1], 1e20)
// 4.16 on net472: silently succeeded, wrote nothing
// 5.x:            RangeError on every target framework
```

### 4.12 New limits, all defaulting to unlimited

These add controls rather than change behaviour: a host that configures nothing gets the 4.16
behaviour. They are listed here because a host running untrusted code should now configure them.

| PR | What it bounds | Options | Default |
| --- | --- | --- | --- |
| [#3037](https://github.com/sebastienros/jint/pull/3037) | parser source length and AST size | `Parsing.MaxSourceLength`, `Parsing.MaxNodeCount` | `null` (unlimited) |
| [#3045](https://github.com/sebastienros/jint/pull/3045) | module graph size, depth, resolution hops, and destination | `Modules.MaxModuleCount`, `MaxTotalModuleSourceBytes`, `MaxModuleGraphDepth`, `MaxModuleResolutionHops`, `Modules.LoadPolicy` | `int.MaxValue` / `long.MaxValue` / `null` |
| [#3046](https://github.com/sebastienros/jint/pull/3046) | host-side result conversion, JSON serialization, error rendering | `Options.ResultLimits`, `Engine.Advanced.ConvertResult` | `ResultLimits.Unlimited` |

Crossing a parser limit throws `ParsingLimitException`, a module-graph limit throws
`ModuleGraphLimitException` and a result limit throws `ResultLimitExceededException`. None is
converted into a catchable JavaScript error — they bound the host, not the script.

Two further additions in the same stack are entirely opt-in and change nothing on their own:

- [#3059](https://github.com/sebastienros/jint/pull/3059) — the `JINTSEC*` configuration diagnostics,
  read through `options.ValidateSecurityConfiguration()` or enforced with
  `options.EnsureSecurityConfiguration()`. See
  [Validating options for untrusted scripts](../README.md#validating-options-for-untrusted-scripts).
- [#3060](https://github.com/sebastienros/jint/pull/3060) — the hardened `ForUntrustedCode(limits)`
  profile and its `UntrustedCodeLimits.BeginOperation(engine, token)` scope, which spans one
  cumulative deadline and allocation budget across every entry an operation makes. See
  [Running untrusted code](../README.md#running-untrusted-code).

The supported boundaries and the residual risks are in the
[threat model](../.github/THREAT_MODEL.md).

## 5. New in v5

Everything here is opt-in: nothing below is installed unless the host asks for it, so none of it
changes an engine that does not.

| Area | Enable with | Reference |
| --- | --- | --- |
| WHATWG web APIs — `console`, timers, `URL`, encoding, streams, `fetch`, storage, `WebSocket`, `EventSource`, crypto | `options.UseWebApis(...)` | [Web APIs (opt-in)](../README.md#web-apis-opt-in) |
| Web Workers, on a thread you supply | `options.UseWebApis().UseWorkers(provider)` | [`new Worker()`](../README.md#new-worker-with-the-thread-supplied-by-you) |
| Node compatibility — the `process` shim, `node:` builtin modules | `options.UseNodeProcess()`, `options.UseNodeBuiltinModules()` | [Node compatibility (opt-in)](../README.md#node-compatibility-opt-in) |
| Script profiling | `options.Profiling.Enabled = true` | [Profiling scripts (opt-in)](../README.md#profiling-scripts-opt-in) |
| Statement-level code coverage | `options.Coverage.Enabled = true` | [Code coverage (opt-in)](../README.md#code-coverage-opt-in) |

## 6. AOT

*Not yet written.* A separate task is measuring the current NativeAOT and trimming state; this
section will record what works, what warns, and what a trimmed host has to configure.

`Jint.csproj` sets `IsAotCompatible` for net7.0+ targets, and `Jint.AotExample/` is the worked
example.

## Keeping this document current

A pull request that changes public API or observable default behaviour adds its own row here, in the
same change. The sections are ordered by what a migrating reader needs first, so put the entry in
the section that matches the *shape* of the break, not the area of the code:

- a member that no longer exists → [Removed API](#2-removed-api);
- a member whose name or signature changed → [Renamed and reshaped API](#3-renamed-and-reshaped-api),
  as before/after code;
- anything that still compiles and behaves differently → [Breaking without a signature
  change](#4-breaking-without-a-signature-change), with the default before, the default after, and
  the one line that restores the old behaviour.

Cite the pull request. Keep the entry to what an embedder has to do — the reasoning belongs in the
pull request, and the reference material belongs in `README.md`.
