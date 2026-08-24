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
- [6. AOT and trimming](#6-aot-and-trimming)
- [Keeping this document current](#keeping-this-document-current)

## 1. Target frameworks

| Target | 4.16.x | 5.x |
| --- | --- | --- |
| `net462` | yes | **dropped** |
| `net472` | — | **added** |
| `netstandard2.0` | yes | yes |
| `netstandard2.1` | yes | yes |
| `net8.0` | yes | yes |
| `net10.0` | yes | yes |

> **Status: landed** ([#3296](https://github.com/sebastienros/jint/pull/3296)). `Jint/Jint.csproj`
> targets `net472;netstandard2.0;netstandard2.1;net8.0;net10.0`.

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
| The `Options` extension methods that mirrored a property — `Strict`, `Culture`, `LocalTimeZone`, `DebugMode`, `AllowClrWrite`, `LimitRecursion`, `SetTypeResolver` and sixteen more | the property they set — the full table is in [3.3](#33-options-is-configured-through-its-properties) | [#3310](https://github.com/sebastienros/jint/pull/3310) |
| `JsValueExtensions.AsInstance<TInstance>(JsValue)` | `value as TInstance` for the answer that may be absent, `(TInstance) value` for the one that must not be — see [2.4](#24-the-four-spellings-of-as-are-gone) | [#3319](https://github.com/sebastienros/jint/pull/3319) |
| `JsValueExtensions.As<T>(JsValue)` (`where T : ObjectInstance`) | `value as T`. The `IsObject()` test it ran first is implied by the constraint, so the two are the same question | [#3319](https://github.com/sebastienros/jint/pull/3319) |
| `JsValueExtensions.TryCast<T>(JsValue)` | `value as T` — the body was exactly that, and it is not the `Try` pattern: no `bool`, no `out` | [#3319](https://github.com/sebastienros/jint/pull/3319) |
| `JsValueExtensions.TryCast<T>(JsValue, Action<JsValue> fail)` | `value is T t`, with the failure written where it happens — see [2.4](#24-the-four-spellings-of-as-are-gone) | [#3319](https://github.com/sebastienros/jint/pull/3319) |
| `Arguments.From(params JsValue[] o)` | nothing. It returned its own argument unchanged, so `Arguments.From(a, b)` and `new[] { a, b }` were the same array | [#3320](https://github.com/sebastienros/jint/pull/3320) |
| `Arguments.Skip(this JsValue[] args, int count)` | a slice copy — `args.AsSpan(count).ToArray()`, guarded for an argument list shorter than `count` — or `System.Linq.Enumerable.Skip`, which the same source text now binds to. See [2.5](#25-argumentsskip-no-longer-shadows-linqs-skip) | [#3320](https://github.com/sebastienros/jint/pull/3320) |
| `AstExtensions` (the whole class, with its two `GetKey` overloads) → `internal` | nothing. `GetKey` extracts a property key from an Acornima expression and needs a live execution context to do it for a computed one, so it was never callable from host code at a meaningful moment; it appeared in IntelliSense for every host with `using Jint;` and an `Expression` in scope | [#3320](https://github.com/sebastienros/jint/pull/3320) |
| `GlobalSymbolRegistry()` (public parameterless constructor) → `internal` | nothing. The well-known symbols on it are `static` and still readable — `GlobalSymbolRegistry.Iterator` and its fourteen siblings are unchanged. The *instance* half holds the symbols one engine's `Symbol.for` created, and `Engine.GlobalSymbolRegistry` is `internal`, so a host-constructed registry could never be installed | [#3320](https://github.com/sebastienros/jint/pull/3320) |
| `ManualPromise(JsValue, Action<JsValue>, Action<JsValue>)` → `internal` | `Engine.Advanced.RegisterPromise()`, the only thing that ever returned one. Settling requires the resolving functions the engine built for that particular promise, so a hand-constructed handle settled nothing. The properties, `with` and `var (promise, resolve, reject) = …` are unchanged | [#3320](https://github.com/sebastienros/jint/pull/3320) |

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

`Engine`'s three facets — `Engine.AdvancedOperations`, `Engine.ConstraintOperations` and
`Engine.ModuleOperations`, the types behind `engine.Advanced`, `engine.Constraints` and `engine.Modules` — are
`sealed` too ([#3320](https://github.com/sebastienros/jint/pull/3320)). Each one's only constructor is
`internal` and each is handed out by an `Engine` that built it, so nothing outside the assembly could have
derived from one; they were the last nested public classes left unsealed.

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

### 2.4 The four spellings of `as` are gone

`JsValueExtensions` carried four ways to narrow a `JsValue` to a concrete runtime type — `As<T>()`,
`AsInstance<TInstance>()` and two `TryCast<T>()` overloads — and each was C#'s own `as` with a method name
around it. Narrowing goes back to the language: `value as JsArray` for the answer that may be absent,
`value is JsArray array` for the checked binding, `(JsArray) value` for the one that must not be absent.

One of the four was also wrong. `AsInstance<TInstance>()` was declared to return a **non-nullable**
`TInstance` and returned `null` whenever the value was not one, so the caller's nullable flow analysis was
told the result could not be null, no check was written, and the `NullReferenceException` surfaced somewhere
else entirely:

```c#
// 4.16.x — compiled with no warning, and threw at the second line, not the first
JsArray array = engine.Evaluate("({ a: 1 })").AsInstance<JsArray>();
var first = array.Get(0);

// 5.x — the compiler types the answer JsArray? and will not let the second line past a check
var array = engine.Evaluate("({ a: 1 })") as JsArray;
var first = array?.Get(0);
```

Note that the two failure modes it had are now one. `AsInstance<TInstance>()` threw `ArgumentException` for a
non-object and returned `null` for an object of the wrong type; `(TInstance) value` throws
`InvalidCastException` for both, and `value as TInstance` answers `null` for both.

The `Action<JsValue> fail` overload of `TryCast<T>()` is the least obvious replacement, because it was a
"Try" that invoked a failure **callback** and then returned `null` — a control-flow idiom with no precedent
in the BCL. Write the failure where it happens:

```c#
// 4.16.x
private UuidInstance EnsureUuidInstance(JsValue thisObject)
{
    return thisObject.TryCast<UuidInstance>(
        value => throw new JavaScriptException(Engine.Realm.Intrinsics.TypeError, "Invalid Uuid"));
}

// 5.x
private UuidInstance EnsureUuidInstance(JsValue thisObject)
{
    if (thisObject is not UuidInstance instance)
    {
        throw new JavaScriptException(Engine.Realm.Intrinsics.TypeError, "Invalid Uuid");
    }

    return instance;
}
```

That is the actual before/after of a prototype in this repository's own test suite, which was the only caller
of the overload anywhere in the tree.
### 2.5 `Arguments.Skip` no longer shadows LINQ's `Skip`

`Arguments.Skip(this JsValue[], int)` was an extension method on `JsValue[]`, and `JsValue[]` is a more
specific receiver than `IEnumerable<TSource>`. So in any file with both `using System.Linq;` and
`using Jint.Runtime;` — which is what a host function's file looks like — overload resolution preferred it,
and `args.Skip(1)` meant Jint's eager, array-allocating copy rather than LINQ's lazy one. Same source text,
different method, no diagnostic; the allocation was per host-function call.

```c#
// inside a host function, with both `using System.Linq;` and `using Jint.Runtime;` in scope

// 4.16.x
var rest = args.Skip(1);                                            // JsValue[], eagerly copied

// 5.x — the same text, now Enumerable.Skip
var rest = args.Skip(1);                                            // IEnumerable<JsValue>, deferred
JsValue[] copy = args.Length > 1 ? args.AsSpan(1).ToArray() : [];   // what 4.16.x handed back
```

The length test in that last line is not decoration: a script may call a host function with fewer arguments
than it reads, `Arguments.Skip` answered an empty array for that, and `AsSpan(start)` throws when `start`
is past the end.

A call site that needed the array is a compile error wherever an array is expected, which is most of them.
Where the result flows straight into something taking an `IEnumerable<JsValue>` it keeps compiling and
becomes lazy instead — so grep for `.Skip(` in code that also has `using Jint.Runtime;`. `Arguments.At`,
the other extension on that class, is unaffected and stays: it has no LINQ counterpart to collide with.

## 3. Renamed and reshaped API

### 3.1 The `string` overloads of `Engine.Call` and `Engine.Construct` are gone ([#3309](https://github.com/sebastienros/jint/pull/3309))

`Engine.Call(string)` and `Engine.Construct(string)` passed their argument to `Evaluate`, so they compiled
and ran it as JavaScript. Their XML docs said "the name of the callable" and "the name of the constructor to
call", and the identically documented `Engine.Invoke(string)` beside them did a literal property lookup on
the global object — so `engine.Call(name)` with a host-supplied `name` was an arbitrary-execution sink that
nothing in the API said was one ([#3289](https://github.com/sebastienros/jint/issues/3289)).

A `string` now names **one property of the global object** wherever it reaches an invocation entry, which is
the same name `SetValue(string, …)` writes, and `Invoke` is the only entry that takes one.

```c#
// 4.16.x — the string was parsed and executed
engine.Call("add", 1, 2);
engine.Construct("MyCtor", arg);

// 5.x — a property of the global object, by that name
engine.Invoke("add", 1, 2);
engine.Construct(engine.GetValue("MyCtor"), arg);
```

For a nested callable, read the value and call it — `Invoke("api.add")` looks for a global literally called
`api.add`, in v5 as in 4.16:

```c#
// 4.16.x — worked, because the string was evaluated
engine.Call("api.add", 1, 2);

// 5.x
var add = engine.GetValue(engine.GetValue("api"), "add");
engine.Invoke(add, 1, 2);
```

A `class`, `let` or `const` declaration is a **lexical binding of the global environment**, not a property
of the global object, so `GetValue` cannot read one — evaluate the identifier instead. `var` and `function`
declarations, and anything `SetValue` installed, are properties and `GetValue` reads them:

```c#
// 4.16.x
engine.Construct("MyClass", arg);

// 5.x
engine.Construct(engine.Evaluate("MyClass"), arg);
```

And if evaluating source really was the intent, say so:

```c#
// 5.x
engine.Evaluate("api.add").Call(1, 2);
```

**A call site that is not updated is not necessarily a compile error.** `JsValue` has an implicit conversion
from `string`, so `engine.Call("add", 1, 2)` still compiles and binds to
`Call(JsValue, params JsValue[])` — where the callable is the *string* `"add"`, which is not callable, so it
throws `ArgumentException` on the first execution instead of running anything. Grep for `.Call("` and
`.Construct("` rather than relying on the compiler.

### 3.2 `Evaluate` and `Execute` are one overload each, plus the prepared form ([#3309](https://github.com/sebastienros/jint/pull/3309))

Each family had four overloads for what is one operation with two optional arguments. `source` and
`parsingOptions` are now optional parameters of a single method, so eight public overloads become four.
Only the call passing parsing options without a source name has to change:

```c#
// 4.16.x
engine.Evaluate(code, parsingOptions);
engine.Execute(code, parsingOptions);

// 5.x
engine.Evaluate(code, parsingOptions: parsingOptions);
engine.Execute(code, parsingOptions: parsingOptions);
```

`Evaluate(code)`, `Evaluate(code, source)`, `Evaluate(code, source, parsingOptions)` and the
`Prepared<Script>` overload are unchanged, and so are their `Execute` counterparts.

`EvaluateAsync` and `ExecuteAsync` deliberately take no parsing options: prepare the source once with
`Engine.PrepareScript` and pass the result to the `Prepared<Script>` overload, which is also the cheaper
thing to do when the same source runs more than once. `ExecuteAsync(in Prepared<Script>, CancellationToken)`
is new in v5 — `EvaluateAsync` already had it.
### 3.3 `Options` is configured through its properties ([#3310](https://github.com/sebastienros/jint/pull/3310))

`Options` and its groups carry every setting as a property. Through 4.16.x about twenty extension methods
mirrored one of those properties one for one, so most settings had two spellings and the class that held
them described itself as a "compatibility layer to allow fluent syntax". Those mirrors are gone: **assign
the property**.

```c#
// 4.16.x
var engine = new Engine(options => options
    .Strict()
    .AllowClrWrite()
    .LimitRecursion(64));

// 5.x
var engine = new Engine(options =>
{
    options.Strict = true;
    options.Interop.AllowWrite = true;
    options.Constraints.MaxRecursionDepth = 64;
});
```

Every deleted method and the property that replaces it:

| 4.16.x | 5.x |
| --- | --- |
| `options.Strict(v)` | `options.Strict = v;` |
| `options.RetainFunctionSourceText(v)` | `options.RetainFunctionSourceText = v;` |
| `options.Culture(c)` | `options.Culture = c;` |
| `options.LocalTimeZone(tz)` | `options.TimeZone = tz;` |
| `options.DebugMode(v)` | `options.Debugger.Enabled = v;` |
| `options.InitialStepMode(m)` | `options.Debugger.InitialStepMode = m;` |
| `options.DebuggerStatementHandling(h)` | `options.Debugger.StatementHandling = h;` |
| `options.DisableStringCompilation(v)` | `options.Host.StringCompilationAllowed = !v;` |
| `options.AllowClrWrite(v)` | `options.Interop.AllowWrite = v;` |
| `options.AllowOperatorOverloading(v)` | `options.Interop.AllowOperatorOverloading = v;` |
| `options.PreferJsPrototypeMethods(v)` | `options.Interop.PreferJsPrototypeMethods = v;` |
| `options.ChainClrExceptions(v)` | `options.Interop.ChainClrExceptionAsInnerException = v;` |
| `options.SetWrapObjectHandler(h)` | `options.Interop.WrapObjectHandler = h;` |
| `options.SetBuildCallStackHandler(h)` | `options.Interop.BuildCallStackHandler = h;` |
| `options.SetMemberAccessor(a)` | `options.Interop.MemberAccessor = a;` |
| `options.SetTypeResolver(r)` | `options.Interop.TypeResolver = r;` |
| `options.DecorateClrExceptionErrors(d)` | `options.Interop.ClrExceptionErrorDecorator = d;` |
| `options.DecorateClrResolutionErrors(d)` | `options.Interop.ClrResolutionErrorDecorator = d;` |
| `options.CatchClrExceptions(handler)` | `options.Interop.ExceptionHandler = handler;` |
| `options.LimitRecursion(n)` | `options.Constraints.MaxRecursionDepth = n;` |
| `options.RegexTimeoutInterval(t)` | `options.Constraints.RegexTimeout = t;` |
| `options.MaxArraySize(n)` | `options.Constraints.MaxArraySize = n;` |
| `options.MaxJsonParseDepth(n)` | `options.Json.MaxParseDepth = n;` |

`options.CatchClrExceptions()` — the parameterless one — stays: it is the preset every host writes,
`Interop.ExceptionHandler = static _ => true`.

Note that a property assignment is an expression, so a one-setting configuration callback still fits on one
line (`new Engine(o => o.Strict = true)`); a chain of two or more becomes a statement block.

### 3.4 The surviving extension methods have one verb per intent ([#3310](https://github.com/sebastienros/jint/pull/3310))

What is left on `Options` is only what an assignment cannot do — install a subsystem, append to a registry,
register a factory, set two coupled values at once, apply a profile — and the verb now says which:

| verb | intent |
| --- | --- |
| `Use*` | installs a subsystem the engine otherwise does not have |
| `Add*` / `Remove*` | appends to, or prunes, a registry that holds many |
| `Allow*` | grants script a capability that is denied by default |
| `Limit*` | registers a built-in budget constraint |
| `Observe*` | registers a built-in constraint that watches host state |
| `Set*` | replaces one host-supplied service an assignment cannot express |
| `For*` | applies a named profile |
| `Expose*` | widens what script may see, across more than one group |
| `Catch*` | routes CLR exceptions into script |
| `Configure` | runs a callback against the engine being built |
| `Validate*` / `Ensure*` | inspects configuration; never changes it |

The renames that follow from it:

| 4.16.x | 5.x |
| --- | --- |
| `options.EnableModules(path)` / `(loader)` | `options.UseModules(path)` / `(loader)` |
| `options.MaxStatements(n)` | `options.LimitStatements(n)` |
| `options.TimeoutInterval(t)` | `options.LimitExecutionTime(t)` |
| `options.CancellationToken(token)` | `options.ObserveCancellation(token)` |
| `options.Constraint(c)` / `(factory)` | `options.AddConstraint(c)` / `(factory)` |
| `options.WithoutConstraint(predicate)` | `options.RemoveConstraints(predicate)` |

```c#
// 4.16.x
var options = new Options()
    .EnableModules(loader)
    .MaxStatements(100_000)
    .TimeoutInterval(TimeSpan.FromSeconds(2))
    .CancellationToken(requestAborted)
    .Constraint(static () => new OperationDeadlineConstraint());

// 5.x
var options = new Options()
    .UseModules(loader)
    .LimitStatements(100_000)
    .LimitExecutionTime(TimeSpan.FromSeconds(2))
    .ObserveCancellation(requestAborted)
    .AddConstraint(static () => new OperationDeadlineConstraint());
```

`LimitStatements` additionally lost its parameter default. `MaxStatements()` written with no argument passed
`0`, which the helper reads as *no limit* — the opposite of what it looked like. Say the number.

### 3.5 A reference resolver and its interests are one registration ([#3310](https://github.com/sebastienros/jint/pull/3310))

`Options.ReferenceResolver` and `Options.ReferenceResolverInterests` are read-only now, and
`SetReferencesResolver` is `SetReferenceResolver` with the interests defaulted. The two values were always
one registration — an interest set describes one particular resolver — and while both were settable, the
resolver's setter silently reset the interests to `All`, so the same two assignments meant different things
depending on the order they were written in.

```c#
// 4.16.x — this narrows nothing: assigning the resolver reset the interests afterwards
options.ReferenceResolverInterests = ReferenceResolverInterests.NullishPropertyBase;
options.ReferenceResolver = new MyResolver();

// 5.x — one call, so the order cannot be got wrong
options.SetReferenceResolver(new MyResolver(), ReferenceResolverInterests.NullishPropertyBase);

// and with no interests named, the resolver is consulted for everything, as before
options.SetReferenceResolver(new MyResolver());
```

### 3.6 `Options.Json` is read-only, like every other group ([#3310](https://github.com/sebastienros/jint/pull/3310))

Every option group — including `Options.Json`, which was the only one with a public setter — is now a
read-only property materialized on first access. A host that replaced the whole group assigns into it
instead:

```c#
// 4.16.x
options.Json = new Options.JsonOptions { MaxParseDepth = 32 };

// 5.x
options.Json.MaxParseDepth = 32;
```

Two things follow that are not visible in a signature. A default `Options` now allocates **no** group at all,
where it used to allocate ten eagerly; and `ForUntrustedCode`'s private snapshot copies every group rather
than the six it happened to name, so a group added later cannot silently escape the profile.

### 3.7 `ShadowRealm.SetValue` is `Engine.SetValue`, mirrored ([#3321](https://github.com/sebastienros/jint/pull/3321))

The two are the same API pointed at different global objects, and they had drifted. `ShadowRealm` was
missing three overloads, disagreed about `null`, and — the part that mattered — carried its trimming
annotation on the wrong overload. All of it now matches, member for member:

| | 4.16.x | 5.x |
| --- | --- | --- |
| `SetValue(string, Delegate)` | `[RequiresUnreferencedCode("User supplied delegate")]` | no annotation, as on the engine |
| `SetValue(string, object)` | no annotation | `SetValue(string, object?)`, `[RequiresUnreferencedCode]` with the engine's message |
| `SetValue(string, string)` | throws `ArgumentNullException` on `null` | `SetValue(string, string?)`; `null` registers as JavaScript `null` |
| `SetValue(string, Type)` | — | added |
| `SetValue<T>(string, T)` | — | added |
| `SetValue<T>(string, T[])` | — | added |

The annotation swap is the reason to read this section. A delegate registration reflects over nothing a
trimmer can remove, so the warning was noise; the `object` overload is the one that resolves members from a
runtime type, and it was silent. A trimming host projecting a host object into a shadow realm therefore got
no diagnostic, and a trimmed-away member read as `undefined` at run time. See
[§6.3](#63-apis-that-now-warn) for what the message says and what to do about it.

Two consequences worth knowing. Passing a typed variable now picks `SetValue<T>` rather than
`SetValue(string, object)`, which is an improvement — `[DynamicallyAccessedMembers]` preserves what Jint
reflects over — but it also means a delegate registration wants the same cast the engine's does
(`(Delegate) new Func<int, int>(…)`, see [§6.4](#64-what-you-owe-your-own-project)). And every overload now
takes the engine's host-call reservation for the duration of the write, exactly as
`ShadowRealm.Evaluate` already did, so registering a value from another thread while the engine is running
is rejected with `InvalidOperationException` instead of racing the global object.
### 3.8 The option registries are `OptionsList<T>` ([#3327](https://github.com/sebastienros/jint/pull/3327))

Six registries changed type from `List<T>` to `OptionsList<T>`, so that they can refuse a change after an
engine has read them — see [4.16](#416-options-is-configuration-until-an-engine-reads-it-and-frozen-afterwards),
which is the reason. `OptionsList<T>` is an `IList<T>` and an `IReadOnlyList<T>` with `AddRange`, `RemoveAll`
and `ToArray`, so the calls a host already writes compile unchanged:

```c#
// compiles in both
options.Interop.ExtensionMethodTypes.Add(typeof(StringExtensions));
options.Interop.AllowedAssemblies.AddRange(assemblies);
foreach (var converter in options.Interop.ObjectConverters) { /* … */ }
```

What does not compile is **replacing** one wholesale. `Options.Interop.AllowedAssemblies` was the only one
with a public setter, and lost it:

```c#
// 4.16.x
options.Interop.AllowedAssemblies = new List<Assembly> { typeof(Foo).Assembly };

// 5.x
options.Interop.AllowedAssemblies.Clear();
options.Interop.AllowedAssemblies.Add(typeof(Foo).Assembly);
```

The six are `Interop.ExtensionMethodTypes`, `Interop.ObjectConverters`, `Interop.ImmutableCrossingTypes`,
`Interop.AllowedAssemblies`, `Constraints.Constraints` and `WebApi.Fetch.AllowedSchemes`. A variable or
parameter declared `List<T>` and assigned from one needs its type changed; `IList<T>`, `IReadOnlyList<T>`,
`IEnumerable<T>` and `var` are unaffected.


### 3.8 `ManualPromise` settles with a CLR value ([#3329](https://github.com/sebastienros/jint/issues/3329))

`Engine.Advanced.RegisterPromise()`'s resolve and reject callbacks took a `JsValue`. They now take an
`object?`, and the conversion runs inside the enqueued settlement job — on the engine's thread — rather than
wherever the callback was invoked.

```c#
var manual = engine.Advanced.RegisterPromise();

// 4.16.x - the conversion is on whatever thread the completion landed on
_ = Task.Run(async () => manual.Resolve(JsValue.FromObject(engine, await FetchAsync(url))));

// 5.x - hand over the CLR value; the engine converts it on its own turn
_ = Task.Run(async () => manual.Resolve(await FetchAsync(url)));
```

Most code needs no edit. `JsValue` converts to `object?` implicitly and `JsValue.FromObject` returns a
`JsValue` unchanged, so every `resolve(someJsValue)` call site compiles and behaves as before, and
`var (promise, resolve, reject) = engine.Advanced.RegisterPromise();` still deconstructs. What does need an
edit is a callback stored in an explicitly typed `Action<JsValue>` local, field or parameter — change it to
`Action<object?>`.

One behaviour is newly defined rather than changed: `Resolve(null)` settles with `JsValue.Null`, where it
used to put a null reference into a non-nullable `JsValue` parameter.

The reason for the change is that the old signature had no safe call site. A host settling from a `Task`
continuation holds a CLR value, and a `JsValue` belongs to the engine that built it — so converting where the
callback runs is a write into an engine another thread may be inside. Jint had the safe shape internally all
along (`RegisterPromiseWithClrValue`, used by `ExperimentalFeature.TaskInterop` for exactly this reason);
both registrations are now one.
### 3.9 `ArrayLikeObject` seals the three members a named getter used to override ([#3338](https://github.com/sebastienros/jint/pull/3338))

`ArrayLikeObject` derives a whole property model from `Length` and `TryGetIndex` and seals almost all of
it, so a subclass cannot make the parts disagree. Three members were left open, and the type's own
documentation told a host wanting a **named** member to override them:

```c#
// 4.16.x / early 5.0 previews — no longer compiles
public override PropertyDescriptor GetOwnProperty(JsValue property) { /* answer "last", else base */ }
public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol) { /* add "last" */ }
public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties() { /* …and here too */ }
```

That was a trap with the same shape as the one `NamedPropertyObject` was created to close. The three had to
be kept mutually consistent by hand, and `GetOwnProperties` is **not** the enumeration hook — the key
enumerations go through `GetOwnPropertyKeys` — so the natural two-member version was visible to
`Object.keys` and invisible to `GetOwnProperties`' real consumers. Measured against this repository's own
worked example: `Object.keys(list)` answered `0,1,2,last` while `list.ToObject()` answered `0,1,2`. And
`ProbeOwnProperty` was already sealed with nothing to answer a named key from, so every `in`,
`hasOwnProperty` and `Object.keys` about `last` materialized a `PropertyDescriptor`.

All three are `sealed override` in v5. Declare the named member instead — the same hooks
`NamedPropertyObject` publishes, here with empty defaults so a collection with no named state declares
nothing:

```c#
protected override int NameCount => _items.Count == 0 ? 0 : 1;

protected override string NameAt(int index) => "last";

protected override bool TryGetNamedValue(string name, out JsValue value)
{
    if (_items.Count > 0 && name == "last") { value = _items[^1]; return true; }
    value = JsValue.Undefined;
    return false;
}
```

The base class derives `GetOwnProperty`, `TryGetOwnPropertyValue`, `ProbeOwnProperty`, both key
enumerations, `Set`, `Delete` and `DefineOwnProperty` from those, so the named key is now visible to every
enumeration including the CLR conversion, and reaches the probe lane it could not reach before. See
[§5.3](#53-host-objects-one-hook-set-for-named-properties-3338) for the whole hook set. A collection whose named
member genuinely needs to intercept reads that resolve on its *prototype* was never served by this class —
`Get` has always been sealed — and stays on plain `ObjectInstance`.

## 4. Breaking without a signature change

This is the section that matters most, because a compiler cannot find any of it. Every row below
compiles exactly as it did in 4.16 and behaves differently at run time.

### 4.1 Changed defaults, at a glance

| PR | Setting | 4.16.x | 5.x | Restore the 4.16 behaviour |
| --- | --- | --- | --- | --- |
| [#3054](https://github.com/sebastienros/jint/pull/3054) | `Interop.AllowWrite` | `true` | `false` | `options.Interop.AllowWrite = true;` |
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
var engine = new Engine(options => options.Interop.AllowWrite = true)
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
// 5.x — opt back into the live view; write-through additionally needs Interop.AllowWrite
var engine = new Engine(options =>
{
    options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
    options.Interop.AllowWrite = true;
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
    .UseModules(loader)
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

### 4.12 A limit that cannot be reached is not a limit ([#3310](https://github.com/sebastienros/jint/pull/3310))

One rule now governs every built-in bound, where 4.16 had two that disagreed. A *constraint* that could
never fail is not registered — which is what `LimitStatements`, `LimitMemory` and `LimitExecutionTime`
already did for a saturated or non-positive value — and a *value* setting that could never be reached no
longer arms its check either. Two settings change behaviour because of it:

| Setting | 4.16.x | 5.x |
| --- | --- | --- |
| `Constraints.MaxRecursionDepth = int.MaxValue` | armed the call-stack depth tracking, which then never fired | identical to `-1`: nothing is tracked, nothing can fire |
| `Constraints.RegexTimeout = TimeSpan.MaxValue` (or any non-positive interval, or anything above `Regex`'s own ~24.8-day ceiling) | untimed inside Jint's regex engine, `ArgumentOutOfRangeException` out of the .NET `Regex` constructor | untimed on both paths |

Neither weakens anything: both spellings already meant "no limit" to a script, and both are still reported
as configuration errors by `options.ValidateSecurityConfiguration()`, which reads back the value the host
assigned rather than the normalized one — so `JINTSEC010` (recursion limit disabled) and `JINTSEC011`
(recursion limit saturated) still tell the two mistakes apart.

`LimitStatements` also lost the parameter default that made `MaxStatements()` mean "no limit"; see
[3.4](#34-the-surviving-extension-methods-have-one-verb-per-intent).

### 4.13 New limits, all defaulting to unlimited

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

### 4.14 `Engine.Constraints.Find<T>()` matches derived constraints ([#3309](https://github.com/sebastienros/jint/pull/3309))

The match was `constraint.GetType() == typeof(T)` — an exact type identity, documented nowhere. It is
`constraint is T` in v5, so a base type matches. Two shapes that used to answer `null` now answer:

```c#
// 4.16.x: null on an engine that has constraints, because Constraint is never the exact type
// 5.x:    the first registered constraint
engine.Constraints.Find<Constraint>();

// 4.16.x: null, because the registered instance is MyDerivedBudget
// 5.x:    the registered instance
class MyBudget : Constraint { /* … */ }
class MyDerivedBudget : MyBudget { /* … */ }
engine.Constraints.Find<MyBudget>();
```

Asking for an exact type still answers the same instance it always did, and where several constraints match
the one registered first is returned — ask for the most derived type you know when that matters.

### 4.15 `ObjectInstance.Extensible` is no longer virtual ([#3322](https://github.com/sebastienros/jint/pull/3322))

The getter was `virtual` over an `internal` setter, so a subclass could override it to a constant and keep a
setter that no longer fed it. `Object.preventExtensions(host)` then returned the object,
`Object.isExtensible(host)` went on answering `true` — in strict mode too, with nothing thrown — and
`Object.seal` / `Object.freeze` became no-ops on it. Reading `Extensible` is unchanged for every caller;
only an override of it stops compiling.

Say the same thing through `PreventExtensions`, which is still `virtual`. Returning `false` is an answer
`[[PreventExtensions]]` is allowed to give, and it makes `Object.preventExtensions` raise a `TypeError`
rather than report a success that did not happen:

```c#
// 4.16.x — compiled, and made Object.preventExtensions a silent no-op
public sealed class ContentDataObject : ObjectInstance
{
    public override bool Extensible => true;
}

// 5.x — the same intent, said where the spec puts it
public sealed class ContentDataObject : ObjectInstance
{
    public override bool PreventExtensions() => false;
}
```

A host that overrode nothing needs no change. A host that overrides `PreventExtensions` and returns `true`
now owes the object `Extensible == false` afterwards — call `base.PreventExtensions()`; host-contract
verification reports an override that does not.
### 4.16 `Options` is configuration until an engine reads it, and frozen afterwards ([#3327](https://github.com/sebastienros/jint/pull/3327))

An `Engine` keeps the very `Options` instance it was handed — `CreateEngineOptions` returns `this` unless a
hardened profile made a private clone — and reads about thirty of its settings **live**, on hot paths.
So this compiled, and granted CLR writes to a running engine:

```c#
// 4.16.x: the engine reads Interop.AllowWrite on every projected write, so this reaches it
var engine = new Engine(options);
options.Interop.AllowWrite = true;
engine.Execute("host.Balance = 0");   // writes through

// 5.x
options.Interop.AllowWrite = true;
// InvalidOperationException: Options.Interop.AllowWrite cannot be changed: these options are read-only
// because an engine has been built from them.
```

Six other properties did the opposite — read once at construction, and documented with *changing it
afterwards has no effect*. Which of the two behaviours a given property had was discoverable only by reading
its XML documentation. Now there is one rule: the `Engine` constructor calls `MakeReadOnly()` on the options
when it has finished reading them, and every setter and every registry on the instance and on its groups
throws an `InvalidOperationException` naming the setting from that point. Those six documentation paragraphs
are gone; the exception says it instead. The model is `JsonSerializerOptions.MakeReadOnly()` / `IsReadOnly`,
and the two members are spelled the same way.

Nothing about *building* engines changes. Sharing one configured `Options` between engines — including
concurrently constructed ones — is exactly as supported as before, because a second freeze is a no-op:

```c#
var options = new Options();
options.Strict = true;
var first = new Engine(options);
var second = new Engine(options);   // still fine
options.IsReadOnly;                 // true
```

A configuration callback still runs before the freeze, so both documented ways of configuring at construction
time are unaffected:

```c#
options.Configure(engine => { /* runs during construction; may still write to options */ });
new Engine(o => o.Interop.AllowWrite = true);
```

**What to change.** Configure the options fully before constructing the engine, or give the second engine its
own `Options`. Three shapes need rewriting:

```c#
// 1. Reconfiguring between evaluations. Build a second engine from a second Options instead.
var engine = new Engine(options);
options.Modules.ExposeDetailedLoadErrors = true;   // now throws

// 2. Swapping a handler mid-run. Put the mutable part in an object you own.
options.Interop.WrapObjectHandler = SomeOtherHandler;   // now throws
// instead:
var mode = Mode.First;
options.Interop.WrapObjectHandler = (e, target, type) => Wrap(mode, e, target, type);
mode = Mode.Second;                                     // your field, not the engine's configuration

// 3. Swapping the console sink. Same shape: a sink of your own that forwards.
options.WebApi.Console.Sink = second;                   // now throws
sealed class ForwardingSink : ConsoleSink
{
    public ConsoleSink Target { get; set; } = Null;
    public override void Write(ConsoleLogLevel level, string message) => Target.Write(level, message);
}
```

`Options.WebApi.Console.Sink` is the one member whose documentation used to *invite* the post-construction
write ("read afresh on every emit, so a host may swap it between evaluations"). The forwarding sink above is
its replacement, and it is strictly better: the option write reached every other engine that happened to
share the same `Options`, and a sink you own does not.

Three details worth knowing:

- **`Engine.Advanced.EnableWebApis(features, configure)` still works.** Its callback is the one sanctioned
  write to an engine's own options after construction, and it is the only place the freeze is suspended. The
  suspension covers that engine's own web-API group and its sub-groups, on the calling thread, for the
  duration of the callback — no other `Options` instance, no other group, and not the registries: a live
  enable sets a value, it never grows an `OptionsList<T>`. Note that the instance it writes to is shared with
  every engine built from it, and for an engine built by `new Engine()` it is one Jint keeps process-wide, so
  give an engine its own `Options` before configuring it there.
- **A group is allocated on first touch, and one materialized after the freeze is born frozen.** So
  `options.Intl.CldrProvider = …` on an engine's options throws even though nothing had ever touched
  `Intl`.
- **`MakeReadOnly()` is public and idempotent**, so a host that builds a configured `Options` in one place
  and hands it around can freeze it itself, before any engine exists.

### 4.17 `JsonParser.Parse` takes the engine's host-call reservation ([#3330](https://github.com/sebastienros/jint/issues/3330))

All three `Parse` overloads now claim the engine for the duration of the parse, so a second thread reaching
one while the engine is in use is rejected with `InvalidOperationException` instead of building objects and
arrays into that engine's realm concurrently.

It was the last conversion entry that did not. `JsonSerializer.Serialize` — its sibling, same namespace, same
`(Engine)` constructor shape — has always been bracketed, and so is `JsValue.FromObject`, so a host had no way
to guess that this one was different. It is also the wrong half to have left open: serializing walks a graph
the host already holds, while parsing *creates* engine-owned state.

Nothing changes for a host that parses on the engine's own thread, which includes every use from inside
script (`JSON.parse`), from a JSON module, from `response.json()` and from a JWK import — the guard takes its
re-entrant branch there. A host parsing while the engine is *idle* is unaffected too: the reservation claims
an unowned engine rather than refusing it. What changes is a parse issued from a background thread while the
engine is busy, which now fails loudly instead of corrupting quietly.

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
| `NamedPropertyObject` — one base class for a host object projecting *named* properties, the string-keyed sibling of `ArrayLikeObject` | derive from it instead of overriding `GetOwnProperty` / `ProbeOwnProperty` / `GetOwnPropertyKeys` / `TryGetOwnPropertyValue` / `GetOwnProperties` by hand | [Projecting host data](../README.md#embedding-performance) |
| Source-generated CLR interop for annotated types | `[JsAccessible]` on the type, plus one `JsAccessibleRegistration.RegisterAll()` call | [§5.1](#51-source-generated-clr-interop) |

### 5.1 Source-generated CLR interop

Annotate a CLR type with `[JsAccessible]` and reference Jint's interop source generator, and its public
instance properties, fields and methods are reached through generated C# in your own assembly instead of
through reflection:

```csharp
[JsAccessible]
public sealed class Player
{
    public int Score { get; set; }
    public string Name { get; set; } = "";

    public JsValue Describe(JsValue prefix) => prefix + Name;
}

// once, during startup, before the first engine resolves a member of an annotated type
MyApp.JsAccessibleRegistration.RegisterAll();
```

`RegisterAll()` is generated into your assembly's root namespace, one per assembly, and is idempotent.
**Registration is explicit on purpose.** The generator does not use `[ModuleInitializer]`: that attribute
does not exist on `net472` or `netstandard2.0` without a polyfill you have no other reason to carry, and a
registration that happens because an assembly loaded is one you can neither see in a stack trace nor turn
off in a test.

**What it claims.** Two things, both narrow. The annotated members need no metadata a trimmer could remove,
and reading, writing or invoking one runs no reflection — on every target framework, including the ones
where Jint's run-time compiled lanes decline outright (`net472`, `netstandard2.0`, and anything without
dynamic code). **It is not an AOT claim**; section 6 is still the whole story there, and this changes
nothing in it.

**What it does not change.** Everything else. A generated member goes through the same property descriptor,
the same conversion, the same `Options.Interop.AllowWrite` check and the same execution-constraint boundary
as the reflected one it replaces, and behaves identically — which is asserted differentially, member shape by
member shape, in `Jint.Tests.PublicInterface/HostGeneratedInteropTests.cs`. One observable differs, and in
the generated form's favour: a generated method is a function object with its own `length`, where a reflected
one reports the arity it inherits from `Function.prototype`.

**What the generator declines**, leaving the member to resolve through reflection exactly as it did before
you annotated the type:

| shape | why |
| --- | --- |
| an overloaded method name | overload resolution is what the reflected path exists for |
| a method parameter not typed `JsValue` | its reflected binding is a conversion chain steered by engine options; reproducing it in emitted code is where a generated accessor stops being equivalent |
| an optional, `params`, `ref`/`out` parameter, or a generic method | same |
| a property with a non-public or `init` accessor | reflection writes those and emitted C# cannot, so half a member would be worse than none |
| a `static` member, an indexer, a `const` field | resolved by a different lane |
| an `abstract`, `static`, generic or nested-private type, and any value type | never the runtime type of a receiver, or unreachable from emitted code — and a value type's instance member would be written through a boxed copy |

The generated lane is also skipped **entirely, for every annotated type**, when the host has installed a
`TypeResolver.MemberFilter`, `MemberNameCreator` or `MemberNameComparer`, or binding flags that no longer
report public instance members. Those four steer which members exist and under what names; the generated
lanes do not run through them yet, and declining is the difference between "not yet supported" and "quietly
bypasses the filter you installed".

Consume the generator with an `Analyzer` project reference for now:

```xml
<ProjectReference Include="path\to\Jint.SourceGenerators.Interop.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

Shipping it inside the `Jint` NuGet package, diagnostics for the declined shapes above, and routing the
generated members through `MemberFilter` and the name policy are each their own follow-up.
| Writable named projections, and named members on an `ArrayLikeObject` | `IsNameWritable` / `TrySetNamedValue` / `TryDeleteName`, and the same `NameCount` / `NameAt` / `TryGetNamedValue` triple on both classes | [§5.3](#53-host-objects-one-hook-set-for-named-properties-3338) |

### 5.3 Host objects: one hook set for named properties ([#3338](https://github.com/sebastienros/jint/pull/3338))

`NamedPropertyObject` shipped read-only, which is not the shape the hosts it was designed for have: a
document, a content item and a settings bag are all written to as well as read. Such a host fell back to
raw `ObjectInstance` and six to nine hand-written overrides. It now declares writability the same way it
declares enumerability — per name, as an attribute — and the base class routes `[[Set]]` and `[[Delete]]`
to the host:

| hook | default | decides |
| --- | --- | --- |
| `NameCount` / `NameAt(int)` | *(abstract here, `0` on `ArrayLikeObject`)* | which names exist, and the enumeration order |
| `TryGetNamedValue(string, out JsValue)` | *(abstract here)* | the value; `false` is an authoritative own miss |
| `HasName(string)` | ask `TryGetNamedValue`, discard | existence, with no value produced |
| `IsNameEnumerable(string)` | `true` | the `enumerable` attribute |
| `IsNameWritable(string)` | `false` | the `writable` attribute **and** whether assignment routes to the host |
| `TrySetNamedValue(string, JsValue)` | refuses | one assignment |
| `TryDeleteName(string)` | refuses | one `delete` |

The last three are new, and their defaults are exactly the old behaviour, so **an existing subclass keeps
compiling and behaving**. The same eight are now published by `ArrayLikeObject` too, all `virtual` with
empty defaults ([§3.9](#39-arraylikeobject-seals-the-three-members-a-named-getter-used-to-override-3338)).

```c#
internal sealed class Document : NamedPropertyObject
{
    public override int NameCount => _names.Count;
    public override string NameAt(int index) => _names[index];
    public override bool TryGetNamedValue(string name, out JsValue value) => _values.TryGetValue(name, out value!);

    protected override bool IsNameWritable(string name) => !_computed.Contains(name);
    protected override bool TrySetNamedValue(string name, JsValue value) { Store(name, value); return true; }
    protected override bool TryDeleteName(string name) => Remove(name);
}
```

Four things worth knowing before writing one:

- **A `false` from `TrySetNamedValue` or `TryDeleteName` is a refusal, not an error.** It produces exactly
  what an ordinary non-writable or non-configurable property produces: a silent no-op (or `false` from
  `delete`) in sloppy mode, a `TypeError` in strict mode. Refuse rather than throw — a CLR exception
  crosses into script as a host error instead of the language's own.
- **`IsNameWritable` may answer `true` for a name the projection does not yet carry**, in which case an
  assignment *creates* it. That routing is WebIDL's named-property-setter shape: it runs ahead of the
  prototype chain, and only when the assignment's receiver is the object itself, so
  `Reflect.set(doc, k, v, other)` still defines on `other`.
- **`Object.defineProperty` on a projected name stays refused**, whether or not the name is writable. The
  projection owns all three attributes — `configurable: true` is forced by the `[[GetOwnProperty]]`
  invariants for a projection that may lose a name — so assignment, not `defineProperty`, is the write
  path.
- **The two halves of the writability declaration are checked.** Declaring a name writable without a
  `TrySetNamedValue` override, or overriding `TrySetNamedValue` without ever overriding `IsNameWritable`,
  is reported by host-contract verification (`AppContext.SetSwitch("Jint.EnableHostContractVerification",
  true)`), as is a `TryDeleteName` that answered `true` for a name the projection still carries. Overriding
  `TryDeleteName` alone is *not* a mistake: deletion is governed by `configurable`, which a projected name
  always reports `true`, so a read-only-but-removable projection is an ordinary shape.

## 6. AOT and trimming

Jint 4.16 asserted Native AOT compatibility with the `IsAotCompatible` property and nothing else. In
v5 the claim is measured: a CI leg publishes `Jint.AotExample` with `PublishAot=true` and *runs* the
native binary ([#3300](https://github.com/sebastienros/jint/pull/3300)), and this section is what that
run says. `Jint.AotExample/Program.cs` is the worked example and the executable form of every claim
below.

### 6.1 What works natively

Everything a script does inside the engine - the language, `JSON`, `RegExp`, promises, `async`,
modules - needs no reflection and works natively. So does the great majority of CLR interop, which is
the part that was never certain:

| | |
| --- | --- |
| properties, fields, indexers, methods on a host object | works |
| `T[]`, `List<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`, `Dictionary<K,V>` | works |
| delegates in both directions (`Func<int, int>` to and from script) | works |
| extension methods, `TypeReference` construction from script | works |
| generic host methods with a **reference-type** argument | works |
| an engine with `options.Interop.Enabled = false` | works, and needs none of this section |

### 6.2 The one thing that does not: a generic instantiation over a value type

Native AOT shares one compiled body across every reference-type argument, so `Identity<string>` works.
A value-type argument needs a specific instantiation the compiler had to have seen, and two places in
Jint build one at run time:

| shape | script that reaches it | what you get |
| --- | --- | --- |
| a JS function converted to `Func<double, Task<double>>` (any `Task<T>` / `ValueTask<T>` over a value type) | calling a host method that takes such a delegate | `NotSupportedException` |
| a generic host method called with a number | `host.identity(7)` - `host.identity('hi')` is fine | `NotSupportedException` |

Both throw rather than degrade, deliberately: there is no non-generic way to produce a `Task<double>`,
and declining the generic method would report *no matching overload* for a method that plainly exists.
A wrong answer is worse than a diagnosable throw. If you need either shape under Native AOT, give the
host method a non-generic overload, or take `Task<object>` and box.

Three shapes that used to throw here now degrade instead
([#3299](https://github.com/sebastienros/jint/issues/3299)): `List<int>`, `IReadOnlyList<double>` and
an `IEnumerable<int>` under `EnumerableConversionMode.Snapshot` fall back to an untyped wrapper and
answer correctly. The only loss is that a collection reached through the last-resort fallback is not
array-*like*: `IReadOnlyList<double>` keeps `ro[0]` but loses `ro.length` and the `Array.prototype`
generics unless the type also implements `IList`.

### 6.3 APIs that now warn

Eight members carry `[RequiresUnreferencedCode]`, so a trimming or AOT host sees the diagnostic at their
own call site rather than as a wrong answer at run time. Each message names what to do instead.

| member | why | what to do |
| --- | --- | --- |
| `Options.AllowClr()` and `AllowClr(params Assembly[])` | script names CLR types and namespaces as strings; nothing statically references what they expose | root the assemblies you allow, or drop `AllowClr` and hand each type to script explicitly |
| `Options.AddExtensionMethods(params Type[])` | the types are reflected over, and a `Type[]` parameter cannot carry `[DynamicallyAccessedMembers]` - only a `Type` or a `string` can | root the declaring types; a trimmed-away extension method fails as `not a function`, with no diagnostic |
| `Engine.SetValue(string, object?)` | no type at the call site to annotate | prefer `SetValue<T>(string, T)`, or pass a `JsValue` |
| `ShadowRealm.SetValue(string, object?)` | the same overload on the same API, pointed at a shadow realm's global object; it carried no annotation at all until v5, while the harmless `Delegate` overload beside it carried one ([§3.7](#37-shadowrealmsetvalue-is-enginesetvalue-mirrored)) | the same: prefer `SetValue<T>(string, T)`, or pass a `JsValue` |
| `ObjectWrapper.Create(Engine, object, Type?)` | same - and it is what a custom `WrapObjectHandler` calls | root the type, or project through `SetValue<T>` |

(`ClrHelper.Unwrap` and `ClrHelper.Wrap`, reachable only from script through `clrHelper`, carry it too.)

One annotation was **removed** in the same change, and a removal is worth as much as an addition here:
`ShadowRealm.SetValue(string, Delegate)` warned that it required unreferenced code, which it does not — a
delegate is handed over as a delegate and nothing is resolved from its type by reflection. Over-annotating
teaches an embedder to suppress the diagnostic on APIs that are fine, which is exactly how the one that
mattered stayed unread.

`Options.Interop.Enabled = true` opens the same door unannotated: it is a property setter, and
annotating it would warn on `= false` as well, which would be absurd. If you set it directly, you are
taking on what `AllowClr`'s message says. It is not otherwise the same thing — it is the gate and not
the grant, so on its own it installs `System`, `importNamespace` and `clrHelper` while
`Interop.AllowedAssemblies` stays empty and every namespace script names is unresolvable. `AllowClr()`
opens the gate *and* names assemblies.

Deliberately **not** annotated: `SetValue(string, Type)`, `SetValue<T>` — on `Engine` and on
`ShadowRealm` alike — `TypeReference.CreateTypeReference`, `ModuleBuilder.ExportType` and
`JsValue.FromObject`. The first four carry `[DynamicallyAccessedMembers]`, which *preserves* what Jint
reflects over instead of merely warning about it - they are the AOT-friendly way to expose a type, which
is exactly why the messages above point at them. Nothing carries `[RequiresDynamicCode]`, because §6.1 is what the measurement
says, and warning an AOT host away from an API that works would be worse than not warning at all.

The attributes are on the `net8.0` and `net10.0` assets. The downlevel targets carry the same
annotations as internal polyfills, so they are invisible to a consumer - which costs nothing, since
Native AOT does not exist there either.

### 6.4 What you owe your own project

**Root the host types Jint reaches only by reflection.** A type nothing in your C# calls is trimmed,
and Jint then reports it as `undefined` or *not a function* rather than as an error - silently. There
is no diagnostic for this anywhere; it is the failure mode to plan for.

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="YourAssembly" />
</ItemGroup>
```

**Cast a delegate registration.** `engine.SetValue("f", new Func<int, int>(x => x * 2))` binds to
`SetValue<T>`, not to `SetValue(string, Delegate)`, and `SetValue<T>`'s `[DynamicallyAccessedMembers]`
then demands every public method of `Func<int, int>` - including the inherited,
`[RequiresUnreferencedCode]` `Delegate.CreateDelegate` overloads. That is three `IL2026` and three
`IL2111` in *your* build, as errors under `TreatWarningsAsErrors`. Casting clears them:

```csharp
engine.SetValue("f", (Delegate) new Func<int, int>(x => x * 2));
```

This is not fixable inside Jint: an overload constrained `where T : Delegate` would have the same
signature as `SetValue<T>` after substitution and make every delegate call site ambiguous.

**Array registrations need nothing.** `engine.SetValue("a", new[] { 1, 2, 3 })` used to cost four
`IL3050` through `Array.CreateInstance` for the same reason. A new `SetValue<T>(string, T[])` overload
now wins for any array and infers `T = int` rather than `T = int[]`, which removes them. It also fixes
a real trimming bug: `SetValue("items", companies)` used to preserve the public members of
`System.Array`, never `Company`'s, so `companies[0].name` was the one thing not preserved. Nothing in
your code changes; the overload is picked automatically.

**Expect Jint's own diagnostics in your build.** Jint's `NoWarn` is a property of Jint's compilation
and reaches nothing downstream - ILC re-derives every diagnostic over the closed program - so an AOT
publish reports the remaining trim-analysis warnings against Jint's files in *your* build. They are
tracked in [#3299](https://github.com/sebastienros/jint/issues/3299); until they are paid down, set
`<IlcTreatWarningsAsErrors>false</IlcTreatWarningsAsErrors>` or `NoWarn` the codes, and treat the run
as the evidence rather than the warning count.

### 6.5 The AOT-safe subset

An engine that never enables interop needs none of the above:

```csharp
var engine = new Engine(options => options.Interop.Enabled = false);
```

That is the floor Jint is prepared to promise. Everything above it is a matter of rooting what you
expose.

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
