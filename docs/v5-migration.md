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
| `Realm()` (public parameterless constructor) | nothing. A `Realm` only makes sense as the one an `Engine` built; get it from `Engine.HostDefined` or `Host.InitializeShadowRealm`. It was `public` only inside `#if DEBUG`, plus the implicit constructor in Release, so the shipped package's surface differed from a source build's | [#3304](https://github.com/sebastienros/jint/pull/3304) |
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
| `ManualPromise(JsValue, Action<JsValue>, Action<JsValue>)` → `internal` | `Engine.Tasks.RegisterPromise()`, the only thing that ever returned one. Settling requires the resolving functions the engine built for that particular promise, so a hand-constructed handle settled nothing. The properties, `with` and `var (promise, resolve, reject) = …` are unchanged | [#3320](https://github.com/sebastienros/jint/pull/3320) |
| `ICldrProvider.SelectPluralCategory` (and `DefaultCldrProvider`'s override) | nothing. It took a `double`, which cannot carry the CLDR plural operands — `1` and `1.00` are different categories in most languages and the same `double` — so it could not implement plural selection correctly even if the engine had asked. `Intl.PluralRules`, `Intl.NumberFormat`, `Intl.RelativeTimeFormat` and `Intl.DurationFormat` all use the engine's own operand-aware rules | [#3336](https://github.com/sebastienros/jint/issues/3336) |
| `ICldrProvider.GetLikelySubtags` (and `DefaultCldrProvider`'s override) | nothing. It expressed only the *maximize* half of the Unicode Add/Remove Likely Subtags algorithm, and `Intl.Locale.prototype.minimize` needs both halves plus subtag-level lookups the signature has no room for. The table is a Unicode constant, not a locale opinion | [#3336](https://github.com/sebastienros/jint/issues/3336) |
| `WeekInfo.MinimalDays` | nothing. ECMA-402 removed `minimalDays` from `getWeekInfo()`'s result, and test262 asserts the keys are exactly `firstDay` and `weekend`, so no caller could ever have reached it | [#3336](https://github.com/sebastienros/jint/issues/3336) |
| `ICldrProvider.GetCompactPatterns` and the `CompactPatterns` type (with `DefaultCldrProvider`'s override) | nothing. `CompactPatterns` is a magnitude-to-pattern map with no plural dimension and no digit count, so it cannot express what compact notation actually needs: CLDR keys these patterns by plural category as well as by power of ten (`ru` long is `0 тысяча` / `0 тысячи` / `0 тысяч`), and the zero count in a CLDR pattern (`00 Tsd.`) is the rounding. Compact suffixes come from the engine's embedded table | [#3354](https://github.com/sebastienros/jint/issues/3354) |
| `ICldrProvider.GetDateTimePatterns` and the `DateTimePatterns` type (with `DefaultCldrProvider`'s override) | nothing. It never had an implementation — the only one in the tree returned `null` unconditionally — so the syntax of its three pattern strings was never fixed, and .NET custom format strings and LDML skeletons disagree on the letters that matter (`yyyy` vs `y`, `tt` vs `a`, `ddd` vs `E`). Wiring it would have meant inventing that contract, and for the `dateStyle`/`timeStyle` pair alone: ECMA-402 resolves both out of the same `availableFormats` skeleton data the component options use, which three strings cannot carry | [#3354](https://github.com/sebastienros/jint/issues/3354) |
| `JsonSerializer.SerializeWithLimits(JsValue, ResultLimits)` | `new JsonSerializer(engine, limits).Serialize(value)` — the limits are the serializer's, the way `JsonParser`'s depth already was. See [2.6](#26-a-json-serializers-limits-are-its-own-not-an-argument-to-every-call) | [#3459](https://github.com/sebastienros/jint/pull/3459) |
| `JsonSerializer.Serialize(JsValue, JsValue, JsValue, ResultLimits)` | `new JsonSerializer(engine, limits).Serialize(value, replacer, space)` — see [2.6](#26-a-json-serializers-limits-are-its-own-not-an-argument-to-every-call) | [#3459](https://github.com/sebastienros/jint/pull/3459) |
| `JsonSerializer.Serialize(JsValue, IBufferWriter<byte>, ResultLimits)` | `new JsonSerializer(engine, limits).Serialize(value, writer)` — see [2.6](#26-a-json-serializers-limits-are-its-own-not-an-argument-to-every-call) | [#3459](https://github.com/sebastienros/jint/pull/3459) |
| `JsonSerializer.Serialize(JsValue, JsValue, JsValue, IBufferWriter<byte>, ResultLimits)` | `new JsonSerializer(engine, limits).Serialize(value, replacer, space, writer)` — see [2.6](#26-a-json-serializers-limits-are-its-own-not-an-argument-to-every-call) | [#3459](https://github.com/sebastienros/jint/pull/3459) |
| `ObjectInstance.GetOwnProperties()` → **no longer `virtual`** | nothing to call instead — the method is still there, and still public. What is gone is the ability to *override* it: it is derived from `GetOwnPropertyKeys` + `GetOwnProperty` now. A host that overrode it declares the same properties by overriding `GetOwnPropertyKeys` (and `ProbeOwnProperty` beside it), which is what every script-visible enumeration already read — see [2.6](#27-getownproperties-is-derived-not-overridden) | [#3461](https://github.com/sebastienros/jint/pull/3461) |
| `ICldrProvider.GetSupportedCalendars` (and `DefaultCldrProvider`'s override) | `ICalendarProvider.GetSupportedCalendars`. ECMA-402 has one list of calendars, not two, and defines it as the calendars the implementation can format — which in Jint means the ones it can *convert*, because that is what formatting a non-ISO calendar goes through. A calendar with conversions and no names still formats numerically; one with names and no conversions cannot be formatted at all. Adding a calendar was already three overrides on `ICalendarProvider`, and it now reaches `Intl` as well as `Temporal` | [#3404](https://github.com/sebastienros/jint/issues/3404) |

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

`Engine`'s facets — `Engine.AdvancedOperations`, `Engine.ConstraintOperations`,
`Engine.ModuleOperations`, and the three [§3.13](#313-engineadvanced-is-split-by-concern-3351) adds
(`Engine.TaskOperations`, `Engine.WebApiOperations`, `Engine.DiagnosticOperations`) — are `sealed`
([#3320](https://github.com/sebastienros/jint/pull/3320)). Each one's only constructor is `internal` and each
is handed out by an `Engine` that built it, so nothing outside the assembly could have derived from one; they
were the last nested public classes left unsealed.

### 2.2 The declared non-contracts now say so to the compiler

`Jint/AGENTS.md` has always said that a few public types are diagnostics rather than contracts — what they
report names an internal representation, so the answer may change in any release and the types may gain
members. Nothing in the type system said so. They now carry
`[Experimental("JINT0001")]`, which is a compiler **error** at the call site until it is acknowledged:

| Marked | |
| --- | --- |
| `ObjectRepresentation`, `Engine.Diagnostics.GetObjectRepresentation` | to *assert* an object is shaped, use `Engine.Advanced.HasSharedShape`, which **is** a contract and is not marked |
| `Engine.Diagnostics.GetMemoryReport` and the `Jint.Diagnostics` records (`EngineMemoryReport`, `HandlerTreeCacheReport`, `InteropCacheReport`, `PoolReport`, `ObjectCensusReport`) | |
| `InteropConversionDiagnostics`, `Engine.Diagnostics.GetInteropConversionDiagnostics` | |

```c#
#pragma warning disable JINT0001 // Jint diagnostic API, deliberately outside the compatibility contract
    var report = engine.Diagnostics.GetMemoryReport();
#pragma warning restore JINT0001
```

or, for a host that logs one on every request, `<NoWarn>$(NoWarn);JINT0001</NoWarn>` once in the project
file. The identifier is stable: a member marked `JINT0001` keeps that identifier for as long as it is marked,
so the suppression does not have to be revisited.

Two things this deliberately does *not* mark. `Engine.Tasks.ProcessTasks` is the canonical host loop —
every host with timers, promises or workers must call it — so its stale "this API may break and change
behavior!" line was corrected rather than promoted to an attribute. `Engine.Tasks.RegisterPromise` is a
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

### 2.6 A JSON serializer's limits are its own, not an argument to every call

`JsonSerializer` had eight ways to serialize: four that used `Options.ResultLimits`, three that took a
`ResultLimits` as a trailing argument, and `SerializeWithLimits`, which is the three-argument overload with
its replacer and space omitted. The trailing argument had to be repeated at every call site that wanted it,
and forgetting it silently fell back to the engine's limits — which default to unlimited.

The limits are now constructor state, the way `JsonParser`'s `maxDepth` already was, and the eight collapse
to four:

```c#
// 4.16.x
var serializer = new JsonSerializer(engine);
var a = serializer.SerializeWithLimits(value, limits);
var b = serializer.Serialize(value, replacer, space, limits);
var c = serializer.Serialize(value, writer, limits);
var d = serializer.Serialize(value, replacer, space, writer, limits);

// 5.x
var serializer = new JsonSerializer(engine, limits);
var a = serializer.Serialize(value);
var b = serializer.Serialize(value, replacer, space);
var c = serializer.Serialize(value, writer);
var d = serializer.Serialize(value, replacer, space, writer);
```

`new JsonSerializer(engine)` is unchanged and still takes `Options.ResultLimits`; the instance now reads it
once, at construction, instead of once per call. That is not observable — an engine's `Options` are frozen
by the time anything can serialize through it — but it is why one instance can no longer serve two policies.
A host that used the trailing argument to vary limits per call holds one serializer per policy instead, or
constructs one per call as `JSON.stringify` does.
### 2.7 `GetOwnProperties` is derived, not overridden ([#3461](https://github.com/sebastienros/jint/pull/3461))

`ObjectInstance.GetOwnProperties()` used to be a `virtual` that a host could override to declare the
properties it projects out of native state. It was the wrong one to reach for, and its name is why:
**nothing script-visible ever called it.** `Object.keys` / `values` / `entries`, `for..in`, object spread
and rest, `Object.assign`, `JSON.stringify` and `JsonSerializer` list keys through `GetOwnPropertyKeys` and
filter them with `ProbeOwnProperty`. A host that overrode `GetOwnProperties` alone therefore shipped an
object whose properties script could not enumerate — and a host that did the right thing and overrode the
key hooks alone shipped one that `GetOwnProperties`' own consumers could not see, of which the CLR
conversion behind `ToObject()` and the debugger are the two an embedder meets.

It is now non-virtual and derived: the keys come from `GetOwnPropertyKeys`, each descriptor from
`GetOwnProperty`, and a key whose descriptor is absent is skipped. One pair of overrides answers everything.

```c#
// 4.16.x — two independent declarations of the same fact, and only one of them was read by script
public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
{
    foreach (var field in _fields)
    {
        yield return new KeyValuePair<JsValue, PropertyDescriptor>(
            new JsString(field.Key),
            new PropertyDescriptor(field.Value, PropertyFlag.ConfigurableEnumerableWritable));
    }
}

// 5.x — declare the keys; GetOwnProperties, the CLR conversion and the debugger all follow
public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
{
    var keys = new List<JsValue>();
    if ((types & Types.String) != Types.Empty)
    {
        foreach (var field in _fields)
        {
            keys.Add(new JsString(field.Key));
        }
    }

    keys.AddRange(base.GetOwnPropertyKeys(types));
    return keys;
}

// and, so existence and enumerability cost no descriptor
protected override OwnPropertyProbe ProbeOwnProperty(JsValue property) => /* ... */;
```

Better still, do not write either: `NamedPropertyObject` (a named record) and `ArrayLikeObject` (a live
indexed collection) derive the whole coherence matrix from two or three members and seal the rest.

The in-box overrides are gone with it — `ArrayInstance`, `Function`, `JsRegExp`, `StringInstance`,
`ObjectWrapper`, `ArrayLikeObject`, `NamedPropertyObject` and six others each declared their keys twice, and
now declare them once. Two of those pairs did not agree; see [4.45](#445-getownproperties-reports-what-the-key-enumerations-report).

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


### 3.9 `ManualPromise` settles with a CLR value ([#3329](https://github.com/sebastienros/jint/issues/3329))

`Engine.Tasks.RegisterPromise()`'s resolve and reject callbacks took a `JsValue`. They now take an
`object?`, and the conversion runs inside the enqueued settlement job — on the engine's thread — rather than
wherever the callback was invoked.

```c#
var manual = engine.Tasks.RegisterPromise();

// 4.16.x - the conversion is on whatever thread the completion landed on
_ = Task.Run(async () => manual.Resolve(JsValue.FromObject(engine, await FetchAsync(url))));

// 5.x - hand over the CLR value; the engine converts it on its own turn
_ = Task.Run(async () => manual.Resolve(await FetchAsync(url)));
```

Most code needs no edit. `JsValue` converts to `object?` implicitly and `JsValue.FromObject` returns a
`JsValue` unchanged, so every `resolve(someJsValue)` call site compiles and behaves as before, and
`var (promise, resolve, reject) = engine.Tasks.RegisterPromise();` still deconstructs. What does need an
edit is a callback stored in an explicitly typed `Action<JsValue>` local, field or parameter — change it to
`Action<object?>`.

One behaviour is newly defined rather than changed: `Resolve(null)` settles with `JsValue.Null`, where it
used to put a null reference into a non-nullable `JsValue` parameter.

The reason for the change is that the old signature had no safe call site. A host settling from a `Task`
continuation holds a CLR value, and a `JsValue` belongs to the engine that built it — so converting where the
callback runs is a write into an engine another thread may be inside. Jint had the safe shape internally all
along (`RegisterPromiseWithClrValue`, used by `ExperimentalFeature.TaskInterop` for exactly this reason);
both registrations are now one.
### 3.10 `ArrayLikeObject` seals the three members a named getter used to override ([#3338](https://github.com/sebastienros/jint/pull/3338))

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

### 3.11 The two converters are abstract classes, and a registration says what it converts ([#3346](https://github.com/sebastienros/jint/pull/3346))

`IObjectConverter` is now `ObjectConverter` and `ITypeConverter` is now `ClrTypeConverter`, both public
abstract classes. The rename of the second one avoids an ambiguity that an interface never had: `TypeConverter`
would have collided with `Jint.Runtime.TypeConverter`, which is public and which hosts do call.

```c#
// 4.16.x
public sealed class MyConverter : IObjectConverter
{
    public bool TryConvert(Engine engine, object value, out JsValue? result) { … }
}

// 5.0
public sealed class MyConverter : ObjectConverter
{
    public override bool TryConvert(Engine engine, object value, out JsValue? result) { … }
}
```

Same shape for `ITypeConverter` → `ClrTypeConverter` (`Convert` and `TryConvert` become `override`), and for
anything typed as one of them: `Engine.TypeConverter`, `Options.Interop.ObjectConverters`,
`AddObjectConverter`, `SetTypeConverter`. A converter deriving from `DefaultTypeConverter` — the recommended
way to adjust one conversion — needs no change at all. If your converter already has a base class, wrap it: an
`ObjectConverter` subclass that forwards to it is four lines, and it is what `AddObjectConverter`'s typed
overload does internally.

What the classes buy is a member the interfaces could not carry, because the target frameworks include ones
without default interface members:

```c#
public sealed class TimeSpanConverter : ObjectConverter
{
    protected override Type[]? HandledTypes => [typeof(TimeSpan)];

    public override bool TryConvert(Engine engine, object value, out JsValue? result) { … }
}
```

**Why it matters, and this is the part that has no compile error.** A converter that declares nothing is
entitled to see every value, so registering one turns off compiled interop lanes engine-wide. The gate on the
type-converter side used to be `converter.GetType() == typeof(DefaultTypeConverter)` — an *exact* type test —
so `class Mine : DefaultTypeConverter`, written to change one conversion and inherit the rest, silently cost
the compiled member-write lane, the compiled method-invoker lane, and the engine's share of the process-wide
accessor cache. It no longer does, provided the converter says what it converts:

```c#
options.SetTypeConverter(engine => new MoneyConverter(engine), typeof(Money));
```

The declaration is a promise that for every *other* target type the converter produces exactly what
`DefaultTypeConverter` produces. Because a `ClrTypeConverter` is handed the target `Type` rather than a value,
the question is exact: a declared type claims itself and its subtypes and nothing above them. Run with
host-contract verification (`AppContext.SetSwitch("Jint.EnableHostContractVerification", true)`, set before the
first use of any Jint type) on to have the promise checked — the stock conversion
is run beside yours for every undeclared target, and a disagreement is reported instead of being silently
bypassed. `AddObjectConverter(converter, handledTypes)` is the same idea for the other direction and is
unchanged; its filter has to guess about a runtime value, so a member typed `object` can never be excluded
from it.

`Engine.TypeConverter` still hands back exactly the instance your factory produced, verification on or off.
`SetTypeConverter` now also raises `JINTSEC052` from `ValidateSecurityConfiguration`, as every other host CLR
callback already did.
### 3.12 The five members that took a `Realm` take an `Engine` (or are gone) ([#3345](https://github.com/sebastienros/jint/pull/3345))

`Realm` is a public type, but **no public member has ever returned one**: `Engine.Realm` is `internal`, and
`Realm`'s own constructor became `internal` in
[#3304](https://github.com/sebastienros/jint/pull/3304). Five signatures nevertheless asked a host for one,
which made them unreachable from outside the assembly — a compile error, not a subtle one:

```text
error CS1061: 'Engine' does not contain a definition for 'Realm'
error CS1729: 'Realm' does not contain a constructor that takes 0 arguments
```

Three of them are useful and now take the `Engine` a host actually holds. They are pure re-signatures:
same algorithm, same behaviour, and the realm they use is the engine's running one, which is what a host
calling from a callback wants.

| 4.16.x / early 5.0 previews | v5 |
| --- | --- |
| `TypeConverter.ToObject(Realm, JsValue)` | `TypeConverter.ToObject(Engine, JsValue)` |
| `TypeConverter.ToIndex(Realm, JsValue)` | `TypeConverter.ToIndex(Engine, JsValue)` |
| `PropertyDescriptor.ToPropertyDescriptor(Realm, JsValue)` | `PropertyDescriptor.ToPropertyDescriptor(Engine, JsValue)` |

```c#
// v5
var o = TypeConverter.ToObject(engine, value);
var i = TypeConverter.ToIndex(engine, value);
var d = PropertyDescriptor.ToPropertyDescriptor(engine, descriptorObject);
```

The other two are gone rather than re-signatured, because neither is something a host has a reason to do:

- **`BindFunction`'s public constructor is `internal`.** The way to bind a function is
  `Function.prototype.bind`, or `JsValue.Call` with the receiver you want; hand-assembling a bound-function
  exotic object is engine work. The **class** stays public with `BoundTargetFunction`, `BoundThis` and
  `BoundArguments` unchanged — those are what a host does with a bound function it is *handed*.
- **`protected Function(Engine, Realm, JsString?)` is gone.** It was the only accessible constructor on
  `Function`, so `Function` advertised `Call` as an extension point that no third party could reach. The
  replacement is [`HostFunction`](#55-a-host-function-is-a-class-you-can-derive-3345), which resolves the
  realm itself.

After this change `Realm` appears in exactly six places in the public surface, and a host can satisfy every
one of them: five `Host` virtuals, where the engine *hands* the realm to the host (`CreateRealm` is the only
one that has to produce one, and `base.CreateRealm()` produces it), and the `protected` `ModuleRecord._realm`
field, on a class whose only constructor is `internal` and which therefore cannot be derived from outside
Jint at all.

### 3.13 `engine.Advanced` is split by concern ([#3351](https://github.com/sebastienros/jint/pull/3351))

`Engine.Advanced` had grown to about forty members covering nine unrelated things, and the one that hurt is
the host loop. Jint never starts a thread, so `ProcessTasks` is the **only** way a `setTimeout` callback, a
settled `Atomics.waitAsync` or a worker message ever runs — a host using timers has no choice about calling
it — and it was item twenty-two of a facet whose name reads as *"you probably should not"*.

The members are unchanged: same signatures, same semantics, same exceptions. Only the address moved, and
every move is mechanical — this is a find-and-replace, not a redesign.

**The host loop is `engine.Tasks`.**

| 4.16.x / early 5.0 previews | v5 |
| --- | --- |
| `engine.Advanced.ProcessTasks()` | `engine.Tasks.ProcessTasks()` |
| `engine.Advanced.TimeUntilNextScheduledWork` | `engine.Tasks.TimeUntilNextScheduledWork` |
| `engine.Advanced.WaitForScheduledWork(…)` | `engine.Tasks.WaitForScheduledWork(…)` |
| `engine.Advanced.WaitForScheduledWorkAsync(…)` | `engine.Tasks.WaitForScheduledWorkAsync(…)` |
| `engine.Advanced.RegisterPromise()` | `engine.Tasks.RegisterPromise()` |
| `engine.Advanced.PromiseRejectionTracker` | `engine.Tasks.PromiseRejectionTracker` |

```c#
// v5 — the canonical host loop, and now it is called that on the engine too
while (running)
{
    engine.Tasks.ProcessTasks();
    engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50), token);
}
```

**The web-platform bridge is `engine.WebApi`**, and, like everything else under `Jint.WebApi`, the whole
facet exists only on `net8.0` and later. Two members are renamed as well as moved, because
`engine.WebApi.WebApiFeatures` and `engine.WebApi.EnableWebApis` stutter — and `EnableWebApis` was a second
spelling of `options.UseWebApis` under a different verb and a different return type.

| 4.16.x / early 5.0 previews | v5 |
| --- | --- |
| `engine.Advanced.EnableWebApis()` | `engine.WebApi.Enable()` |
| `engine.Advanced.EnableWebApis(features, configure)` | `engine.WebApi.Enable(features, configure)` |
| `engine.Advanced.WebApiFeatures` | `engine.WebApi.Features` |
| `engine.Advanced.HasFetchHandler` | `engine.WebApi.HasFetchHandler` |
| `engine.Advanced.SetFetchHandler(…)` | `engine.WebApi.SetFetchHandler(…)` |
| `engine.Advanced.InvokeFetchHandler(…)` | `engine.WebApi.InvokeFetchHandler(…)` |
| `engine.Advanced.InvokeFetchHandlerAsync(…)` | `engine.WebApi.InvokeFetchHandlerAsync(…)` |
| `engine.Advanced.CreateMessagePortPair(other)` | `engine.WebApi.CreateMessagePortPair(other)` |
| `engine.Advanced.CreateAbortSignal(token)` | `engine.WebApi.CreateAbortSignal(token)` |
| `engine.Advanced.CreateReadableStream(…)` | `engine.WebApi.CreateReadableStream(…)` |
| `engine.Advanced.CreateWritableStream(…)` | `engine.WebApi.CreateWritableStream(…)` |
| `engine.Advanced.StartReadableStreamCopy(…)` | `engine.WebApi.StartReadableStreamCopy(…)` |
| `engine.Advanced.CopyReadableStreamAsync(…)` | `engine.WebApi.CopyReadableStreamAsync(…)` |

`options.WebApi.Features` is unrelated and unchanged: it still reads back exactly what the host asked for,
where `engine.WebApi.Features` reports the expanded closure the engine actually carries.

**The reports and the instruments that produce them are `engine.Diagnostics`.** Nothing on it changes what a
script computes, which is the line between it and what stays on `Advanced`.

| 4.16.x / early 5.0 previews | v5 |
| --- | --- |
| `engine.Advanced.StackTrace` | `engine.Diagnostics.StackTrace` |
| `engine.Advanced.ValidateSecurityConfiguration(…)` | `engine.Diagnostics.ValidateSecurityConfiguration(…)` |
| `engine.Advanced.IsProfiling` | `engine.Diagnostics.IsProfiling` |
| `engine.Advanced.StartProfiling()` | `engine.Diagnostics.StartProfiling()` |
| `engine.Advanced.StopProfiling()` | `engine.Diagnostics.StopProfiling()` |
| `engine.Advanced.GetCoverage()` | `engine.Diagnostics.GetCoverage()` |
| `engine.Advanced.ResetCoverage()` | `engine.Diagnostics.ResetCoverage()` |
| `engine.Advanced.GetMemoryReport(…)` | `engine.Diagnostics.GetMemoryReport(…)` |
| `engine.Advanced.GetObjectRepresentation(…)` | `engine.Diagnostics.GetObjectRepresentation(…)` |
| `engine.Advanced.GetInteropConversionDiagnostics()` | `engine.Diagnostics.GetInteropConversionDiagnostics()` |

The last three are still the declared non-contracts and still carry `[Experimental("JINT0001")]`; see
[§2.2](#22-the-declared-non-contracts-now-say-so-to-the-compiler).

**Three members are on `Engine` itself**, because none of them is advanced and each is a peer of something
already there. `HostDefined` is the host's own slot on this engine, next to `Global` and `Intrinsics`;
`AddLazyGlobal` is the lazy `SetValue`, and belongs beside it; and `ConvertResult` is the **bounded** way to
get a CLR object out of the engine, while the unbounded `JsValue.ToObject()` is a method on every value a
host holds — a host that never found the bounded one was taking the unbounded route by default.

| 4.16.x / early 5.0 previews | v5 |
| --- | --- |
| `engine.Advanced.HostDefined` | `engine.HostDefined` |
| `engine.Advanced.AddLazyGlobal(name, factory, flags)` | `engine.AddLazyGlobal(name, factory, flags)` |
| `engine.Advanced.AddLazyGlobal(name, state, factory, flags)` | `engine.AddLazyGlobal(name, state, factory, flags)` |
| `engine.Advanced.ConvertResult(value, limits)` | `engine.ConvertResult(value, limits)` |

**What stays on `Advanced` is what the name should always have meant**: operations with no counterpart in the
ECMAScript object model. `ResetCallStack`, `CreateProxy`, `CreateRevocableProxy`, `HasSharedShape`,
`GetPropertyAccessSemantics`, `CaptureGlobalSnapshot`, `RestoreGlobalSnapshot` and `WithRestoredGlobals` are
unchanged and stay where they are.

One thing that is not observable but worth knowing if you build engines in a tight loop: `Advanced`, `Tasks`,
`WebApi` and `Diagnostics` are each materialized on first access, so an engine that never touches one never
allocates it. `engine.Constraints` and `engine.Modules` are unchanged.
### 3.14 `JsValue`'s vocabulary is on `JsValue`, and `JsValueExtensions` moved to `Jint.Native` ([#3353](https://github.com/sebastienros/jint/pull/3353))

`JsValue` is in `Jint.Native`. The extension methods that gave it its vocabulary were in `Jint`, so a file
that imported the namespace the type lives in and nothing else dotted a `JsValue` and saw `Get`, `Set`,
`ToObject` and `Type` — but not `IsString()`. Both halves are in `Jint.Native` now.

**The one thing to type.** A file that used the vocabulary and imported only `Jint` needs one more using:

```c#
using Jint;
using Jint.Native;   // <- add this
```

The compiler finds every site: `error CS1061: 'JsValue' does not contain a definition for 'AsInt32Array'`.
A file whose own namespace is nested under `Jint.Native` needs nothing at all.

**Twenty-five members moved onto the type itself** and no longer need any using:

| | |
| --- | --- |
| predicates | `IsUndefined`, `IsNull`, `IsString`, `IsNumber`, `IsBoolean`, `IsObject`, `IsArray`, `IsCallable`, `IsPromise`, `IsDate`, `IsRegExp`, `IsSymbol`, `IsBigInt` |
| accessors | `AsString`, `AsNumber`, `AsBoolean`, `AsObject`, `AsArray` |
| `TryGet` (new) | `TryGetString`, `TryGetNumber`, `TryGetBoolean`, `TryGetObject`, `TryGetArray` |
| promise | `UnwrapIfPromise()` and its two overloads, `UnwrapIfPromiseAsync` |

An instance member wins over an extension method, so a call site that compiled before compiles now and
binds to the same implementation. Two callers do change:

- an explicit static call — `JsValueExtensions.IsString(value)` becomes `value.IsString()`;
- a host's own extension method named after one of them, which the instance member now shadows. Rename it,
  or call it as a static.

**A `TryGet` for each promoted accessor** is the new part. `IsX()` followed by `AsX()` is two type tests and
a throw waiting to happen; one call answers both questions:

```c#
// 4.16.x
if (value.IsString())
{
    Use(value.AsString());
}

// 5.x
if (value.TryGetString(out var text))
{
    Use(text);
}
```

**What stayed an extension method**, and is reached by the same one using: the typed-array,
`ArrayBuffer` and `DataView` accessors, `AsDate`, `AsRegExp`, `AsFunctionInstance`, `IsPrimitive`,
`IsPrivateName`, `IsConstructor` and the six `Call` overloads. The line is what a value *is* in JavaScript's
own vocabulary versus what a value is in Jint's — the first is on the type, the second is beside it.

### 3.15 The module record is `ModuleRecord`, and `Module` is Acornima's AST node alone ([#3311](https://github.com/sebastienros/jint/issues/3311))

`Jint.Runtime.Modules.Module` is ECMA-262's
[Abstract Module Record](https://tc39.es/ecma262/#sec-abstract-module-records) — the loaded, linkable,
evaluatable module a loader hands the engine. Under its old name it collided with two types host code
imports as a matter of course: `Acornima.Ast.Module`, which is what `Engine.PrepareModule` returns inside a
`Prepared<>`, and `System.Reflection.Module`. Either import turned a bare `Module` into
`error CS0104: 'Module' is an ambiguous reference`, so a host had no way to name the type but to alias one
of the three. This repository carried seventeen such aliases plus a global one in `Directory.Build.props`,
and nine of the seventeen were in `Jint.Tests.PublicInterface` — the one suite without `InternalsVisibleTo`,
and so the one that sees what an integrator sees. All of them are gone.

| 4.16.x | 5.x |
| --- | --- |
| `Jint.Runtime.Modules.Module` | `Jint.Runtime.Modules.ModuleRecord` |
| `Jint.Runtime.Modules.CyclicModule` | `Jint.Runtime.Modules.CyclicModuleRecord` |

```c#
// 4.16.x — the alias is what makes the file compile
using Acornima.Ast;
using Jint.Runtime.Modules;
using Module = Jint.Runtime.Modules.Module;

sealed class MyLoader : IModuleLoader
{
    private readonly Dictionary<string, Prepared<AstModule>> _cache = new();  // ...and a second alias

    public Module LoadModule(Engine engine, ResolvedSpecifier resolved) => /* ... */;
    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest) => /* ... */;
}

// 5.x — no alias, and `Module` means what Acornima means by it
using Acornima.Ast;
using Jint.Runtime.Modules;

sealed class MyLoader : IModuleLoader
{
    private readonly Dictionary<string, Prepared<Module>> _cache = new();

    public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved) => /* ... */;
    public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest) => /* ... */;
}
```

Every `ModuleFactory.Build*Module` overload, `IModuleLoader.LoadModule`, `ModuleLoader.LoadModule`,
`ModuleLoadCompletion.SetModule`, `ModuleRecord.GetModuleNamespace`, and the `Host.GetImportMetaProperties`
/ `Host.FinalizeImportMeta` parameters move with it — the last two were already *named* `moduleRecord`.
The concept keeps its own word: `ModuleFactory`, `ModuleBuilder`, `IModuleLoader`, `ModuleRequest` and
`ResolvedSpecifier` are unchanged, and so is `Engine.PrepareModule`, which still returns
`Prepared<Acornima.Ast.Module>` — that type belongs to the parser, and renaming it is not Jint's to do.

**Nothing but the name changed.** Both types' only constructors were already `internal`, so no host has ever
derived from either; there is no override signature, no `base` call and no behaviour in the diff.

**If you would rather not touch your call sites**, one line keeps them all compiling — an alias beats a
namespace import, so it also settles the `System.Reflection` collision if you had one:

```c#
// GlobalUsings.cs
global using Module = Jint.Runtime.Modules.ModuleRecord;
```

That is the shim, not the recommendation: a file that spells `Module` for the record cannot also spell
`Module` for Acornima's AST node, which is the whole reason the type was renamed.

### 3.16 `UntrustedCodeLimits` and `ResultLimits` are named properties, and a preset is something you adjust ([#3459](https://github.com/sebastienros/jint/pull/3459))

This is [3.3](#33-options-is-configured-through-its-properties)'s rule — optional configuration is a
property, never a positional argument — applied to the two limit bags that still ignored it.

`UntrustedCodeLimits` took **fifteen constructor parameters, eight of them required and positional, four of
those adjacent `TimeSpan`s**. Every call site was a column of unlabelled values in an order nobody
remembers, and `regexTimeout` and `promiseTimeout` could be swapped without a diagnostic. Both types are now
records with `init` properties:

```c#
// 4.16.x — position is the only thing that says which TimeSpan is which
var limits = new UntrustedCodeLimits(
    TimeSpan.FromSeconds(1),
    100_000,
    16_000_000,
    64,
    10_000,
    TimeSpan.FromMilliseconds(250),
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(2));

// 5.x — every value names the dimension it bounds
var limits = new UntrustedCodeLimits
{
    TimeoutInterval = TimeSpan.FromSeconds(1),
    MaxStatements = 100_000,
    MemoryLimit = 16_000_000,
    MaxRecursionDepth = 64,
    MaxArraySize = 10_000,
    RegexTimeout = TimeSpan.FromMilliseconds(250),
    PromiseTimeout = TimeSpan.FromSeconds(1),
    MaxOperationDuration = TimeSpan.FromSeconds(2),
};

// ...or start from Jint's own conservative profile
var tuned = UntrustedCodeLimits.Default with { MaxStatements = 5_000 };
```

**The eight that were required are still required**, now as C# `required` members: omitting one is
`CS9035` at the call site rather than a weaker limit nobody notices. **The seven that had defaults keep the
same defaults** — `MaxSourceLength` 1,000,000, `MaxNodeCount` 250,000, `MaxModuleCount` 100,
`MaxTotalModuleSourceBytes` 10,000,000, `MaxModuleGraphDepth` 32, `MaxModuleResolutionHops` 1,000,
`ResultLimits` `ResultLimits.Conservative`. Nothing became looser: no dimension a host used to state
acquired a default, and no default moved. `UntrustedCodeLimits.Default` is new API, so nothing can regress
through it; it is the one place Jint picks the eight itself, and it is a starting point to measure against
rather than a value to accept unread.

`ResultLimits` gets the same treatment, and its five optional parameters become five `init` properties with
the same unlimited defaults:

```c#
// 4.16.x
var limits = new ResultLimits(maxDepth: 16, maxStringLength: 100_000);

// 5.x
var limits = new ResultLimits { MaxDepth = 16, MaxStringLength = 100_000 };
var tighter = ResultLimits.Conservative with { MaxStringLength = 4_096 };
```

Three consequences worth stating. Validation moved from the constructor to the property, so the
`ArgumentOutOfRangeException` a bad value raises now names the **property** (`MaxStatements`) rather than
the parameter (`maxStatements`) — and it still runs for a dimension a `with` expression changes. Both types
are records, so they have value equality and a `ToString` that prints every dimension. And
`UntrustedCodeLimits.BeginOperation` still requires the **instance** the engine was configured with: a
`with` expression produces a value-equal but different object, so configure the engine with the one the
scope will use.
### 3.17 The two raw-write helpers say what they do ([#3461](https://github.com/sebastienros/jint/pull/3461))

| 4.16.x | 5.x |
| --- | --- |
| `ObjectInstance.FastSetProperty(string, PropertyDescriptor)` | `ObjectInstance.DefineOwnPropertyUnchecked(string, PropertyDescriptor)` |
| `ObjectInstance.FastSetProperty(JsValue, PropertyDescriptor)` | `ObjectInstance.DefineOwnPropertyUnchecked(JsValue, PropertyDescriptor)` |
| `ObjectInstance.FastSetDataProperty(string, JsValue)` | `ObjectInstance.DefineOwnDataPropertyUnchecked(string, JsValue)` |

Same bodies, same behaviour — a mechanical rename, and the compiler finds every call site.

The old names claimed a speed the methods do not have and hid the four things they actually do. They are
`[[DefineOwnProperty]]` with the checks taken out, which is what the new names say:

- the write always creates an **own** property, so it *shadows* anything of that name on the prototype chain;
- no inherited setter runs, so a data write can end up shadowing an inherited accessor
  (`Error.prototype.stack` is the concrete one);
- no `[[DefineOwnProperty]]` validation runs — extensibility, an existing property's configurable/writable
  flags and the data/accessor compatibility rules are all ignored, so the call always succeeds and can never
  raise a `TypeError`;
- and storing a raw descriptor under a string key is a dictionary-mode operation, so a shape-mode receiver is
  permanently deoptimized and forfeits the shape inline cache.

"Fast" was the opposite of that last point: a loop of `FastSetDataProperty` calls is the *slow* way to project
a batch of host records, because every object gets its own descriptors and its own property dictionary and the
script reading them never keeps a monomorphic inline cache. `JsObject.Create` and `JsObject.CreateFromEntries`
are the fast ones, and they were already what the doc comment pointed at.

Use these for setup-time writes on an object you fully control; use `Set` for steady-state mutation.

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
| [#3046](https://github.com/sebastienros/jint/pull/3046) | host-side result conversion, JSON serialization, error rendering | `Options.ResultLimits`, `Engine.ConvertResult` | `ResultLimits.Unlimited` |

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

- **`Engine.WebApi.Enable(features, configure)` still works.** Its callback is the one sanctioned
  write to an engine's own options after construction, and it is the only place the freeze is suspended. The
  suspension covers that engine's own web-API group and its sub-groups, on the calling thread, for the
  duration of the callback — no other `Options` instance, no other group, and not the registries: a live
  enable sets a value, it never grows an `OptionsList<T>`. What it writes to is a copy of the web-API settings
  the engine takes for itself, so nothing the callback writes reaches another engine — see
  [§4.19](#421-webapienables-callback-configures-one-engine-not-every-engine-3359).
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

### 4.18 A host `Constructor` is now a `Function` ([#3345](https://github.com/sebastienros/jint/pull/3345))

`Constructor`'s host-facing constructor, `protected Constructor(Engine, string)`, never assigned the
object's `[[Prototype]]`, so it kept the one every `ObjectInstance` starts with — `Object.prototype`.
Every one of Jint's own constructors goes through a different, internal constructor and sets
`Function.prototype` itself, so the defect was reachable only by a host, and only by the shape this
repository's own `ConstructorTests` uses:

```js
// 4.16.x, for `engine.SetValue("Box", new BoxConstructor(engine))`
typeof Box                                         // "function"   — correct
Box instanceof Function                            // false        — wrong
Object.getPrototypeOf(Box) === Function.prototype  // false        — wrong
typeof Box.call                                    // "undefined"  — no call, apply or bind
```

`new Box()` worked, which is why this survived: the `[[Construct]]` path never consults the prototype
chain. Everything a script does to a constructor *as a function object* did not. It now inherits from the
principal realm's `Function.prototype`, the same rule `ClrFunction` and the new `HostFunction` follow, so
all four lines above answer as they do for a built-in.

**What could break:** a host that noticed and compensated — by calling `Object.setPrototypeOf` from script,
or by assigning `Prototype` after construction — is now doing it twice, harmlessly. A host that put its own
methods on the constructor object as own properties is unaffected: this changes what the object *inherits*,
never what it owns. A script asserting `Box instanceof Function === false` was asserting the bug.

### 4.19 An `Array.prototype` generic over a host collection with no index answers instead of throwing ([#3356](https://github.com/sebastienros/jint/pull/3356))

A wrapped CLR collection is array-like when it has a `Count`, and `ICollection` is a count-and-copy
contract with no index in it. `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `SortedSet<T>` and an embedder's own
`ICollection` therefore have a `length` and no element at index 0. Applying an `Array.prototype` generic to
one, or destructuring it, threw a raw `System.InvalidCastException` out of `Engine.Evaluate` — not a
`JavaScriptException`, so neither a host `catch` nor a script `try`/`catch` could see it:

```js
// 4.16.x, for engine.SetValue("q", new Queue<int>([1, 2, 3]))
q.length                            // 3
Array.prototype.join.call(q, '-')   // InvalidCastException: ... to type 'System.Collections.IList'
Array.prototype.indexOf.call(q, 2)  // InvalidCastException
var [...r] = q;                     // InvalidCastException
```

The indexed lane is now entered only for a target that actually has an indexer, so those collections behave
as `HashSet<T>` always has — it reaches the engine through the generic `ICollection<T>` and was never
admitted to that lane:

```js
// 5.x
Array.prototype.join.call(q, '-')   // "--"     — length honoured, three absent indices
Array.prototype.indexOf.call(q, 2)  // -1
[...q].join('-')                    // "1-2-3"  — iteration is unaffected
var [...r] = q; r.join('-')         // "1-2-3"
Array.prototype.push.call(q, 4)     // TypeError — "length" forwards to a read-only Count
```

A collection that *does* have an integer indexer keeps its elements, including a host `ICollection` that is
not an `IList`: `join` over one answers `"1-2-3"`, where 4.16 threw.

**What could break:** a `catch (InvalidCastException)` around `Evaluate` written to absorb this no longer
fires, and script that reached one of these generics now gets an answer where it used to abort. Array
destructuring of a `HashSet<T>` changes too — `var [...r] = set` yielded `[undefined, undefined, undefined]`
while `[...set]` yielded the elements, and both now yield the elements. Destructuring is *GetIterator* in
the specification, so an index-reading fast path may only stand in for the iterator where the two agree.
### 4.20 `AsArray()` and `IsArray()` are the same question ([#3353](https://github.com/sebastienros/jint/pull/3353))

`AsArray()` guarded with the *specification's* `IsArray` — which is true for `Array.prototype` and follows a
`Proxy` to its target — and then cast to `JsArray`, which neither of those is. The guard passed and the cast
threw:

```c#
// 4.16.x
engine.Evaluate("Array.prototype").AsArray();      // System.InvalidCastException
engine.Evaluate("new Proxy([], {})").AsArray();    // System.InvalidCastException

// 5.x
engine.Evaluate("Array.prototype").AsArray();      // ArgumentException: The value is not an array
engine.Evaluate("new Proxy([], {})").AsArray();    // ArgumentException: The value is not an array
```

`IsArray()` answers `false` for both, which it always did, and `TryGetArray` declines. A host that wants the
proxy-following answer asks script for it — `Array.isArray(value)` is unchanged, and still `true` for both.
### 4.21 `WebApi.Enable`'s callback configures one engine, not every engine ([#3359](https://github.com/sebastienros/jint/pull/3359))

`Engine.WebApi.Enable(features, configure)` used to hand the callback the very `Options`
instance the engine was built from. That instance is shared with every other engine built from it,
and an engine built by `new Engine()` shares one Jint keeps process-wide — so this, the sample from
the method's own documentation, set the tenant's client on every default-built engine in the process,
those built before the call included:

```csharp
var engine = new Engine();
engine.WebApi.Enable(WebApiFeatures.Fetch, w => w.Fetch.HttpClient = tenantClient);
```

The engine now takes a copy of the web-API settings for itself before the callback runs. The copy
starts as a copy, so whatever the host configured on the options up front is still in force, and what
the callback writes reaches that one engine.

**What could break:** a host that used the callback to configure a *pool* — writing through one
engine and expecting its siblings to pick the setting up — and a host reading a setting back off its
own `Options` afterwards, which now answers what the host put there rather than what the callback
wrote. Shared configuration is spelled by configuring the options before building:

```csharp
var options = new Options().UseWebApis(WebApiFeatures.Fetch);
options.WebApi.Fetch.HttpClient = sharedClient;   // every engine built from this
```

### 4.22 `Intl` reads the CLDR provider for currency symbols and week info ([#3336](https://github.com/sebastienros/jint/issues/3336))

`Intl.NumberFormat`'s currency symbols came from a hardcoded switch inside the engine, and
`Intl.Locale.prototype.getWeekInfo` read the embedded CLDR week data directly. Both now go through
`Options.Intl.CldrProvider` — `GetCurrencyData` and `GetWeekInfo` — so overriding either reaches script:

```c#
// 5.x — one override, and Intl.NumberFormat().format() shows it
sealed class MyCurrencies : DefaultCldrProvider
{
    public override CurrencyData? GetCurrencyData(string locale, string currencyCode)
        => currencyCode == "XCD"
            ? new CurrencyData { Symbol = "EC$", NarrowSymbol = "$", DisplayName = "East Caribbean dollars" }
            : base.GetCurrencyData(locale, currencyCode);
}
```

That switch is now `DefaultCldrProvider`'s data, so an engine that configures no provider — or one whose
provider derives from `DefaultCldrProvider` — formats exactly as it did in 4.16.

**What could break:** a host implementing `ICldrProvider` from scratch rather than deriving from
`DefaultCldrProvider`. Its `GetCurrencyData` and `GetWeekInfo` were dead code and are now read, so whatever
they return is what script sees. Returning `null` from `GetCurrencyData` formats the currency *code*
(`"USD12.50"`), which is what `currencyDisplay: "code"` has always produced; returning `null` from
`GetWeekInfo` keeps the embedded week data. `currencyDisplay: "code"` never consults the provider, because
the specification fixes that display to the currency code.
### 4.23 A value registered on a `ShadowRealm` belongs to that realm ([#3325](https://github.com/sebastienros/jint/issues/3325))

`ShadowRealm.SetValue` built its value against whichever realm the host called from — the principal one —
and then installed it on the shadow realm's global object. The wrapper therefore carried the principal
realm's `Object.prototype`, so script inside the realm saw an object that was not `instanceof Object`:

```csharp
var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
shadowRealm.SetValue("company", new Company("acme"));

// 4.16.x: false — the wrapper's prototype came from a realm this script cannot reach
// 5.x:    true
shadowRealm.Evaluate("company instanceof Object");
```

Every overload now registers with the shadow realm as the running realm, so the prototype comes from that
realm's intrinsics: an object gets its `Object.prototype`, a delegate its `Function.prototype`, a projected
array its `Array.prototype`, and a `Type` a prototype object chained to its `Object.prototype`.

**What could break:** a host comparing such a value's prototype against `engine.Intrinsics` — that is the
principal realm's, and it is no longer what the value inherits from. `Engine.SetValue` is unchanged, and so
is a `JsValue` the host built itself and passed to the `JsValue` overload: only what `SetValue` converts
moves.

### 4.24 `ShadowRealm.ImportValue` takes the engine's host-call reservation ([#3324](https://github.com/sebastienros/jint/issues/3324))

It loads, links and evaluates a module graph, and it used to claim the engine for none of that — only for
the continuation drain at the end, and then only for the length of the drain. A second thread reaching the
engine during a load was served instead of refused. It now claims the engine for the whole call, the same
way `ShadowRealm.Evaluate` and `ShadowRealm.SetValue` do.

Nothing changes for a host importing on the engine's own thread, or into an idle engine: the reservation
takes its re-entrant branch in the first case and claims an unowned engine in the second. What changes is an
import issued while the engine is busy elsewhere, which now fails with `InvalidOperationException` instead
of running concurrently.

### 4.25 A host-issued `JsonParser.Parse` is one run, with its own budget ([#3342](https://github.com/sebastienros/jint/issues/3342))

`JsonParser`'s scanner observes execution constraints, so a long document is interruptible. But `Parse` was
not itself a bracketed entry, and `ExecuteWithConstraints` re-arms a time limit as a run *ends* — so a
direct `Parse` was measured against a deadline belonging to whatever ran last, however long ago:

```csharp
var engine = new Engine(o => o.LimitExecutionTime(TimeSpan.FromMilliseconds(200)));
var json = "{\"a\":\"" + new string('x', 60_000) + "\"}";

new JsonParser(engine).Parse(json);          // 4.16.x: fine, on a never-used engine
engine.Evaluate("1 + 1"); Thread.Sleep(1000);
new JsonParser(engine).Parse(json);          // 4.16.x: TimeoutException
engine.Evaluate("JSON.parse(doc).a.length"); // 4.16.x: fine — the Evaluate is the bracket
```

The same document, the same engine, the same limit, and the answer depended on what the engine had done
earlier and how long ago. A `MemoryLimitConstraint` had the mirror-image hole: with no entry to attach to, a
host-issued parse was accounted against no operation at all, so `LimitMemory` did not bound it.

All three overloads now take the bracket their sibling `JsonSerializer.Serialize` has always taken. A parse
issued by the host is one run: this engine's constraints are armed on the way in and rewound on the way out,
and its allocations are charged.

**What could break:** a parse reached from *inside* script — `JSON.parse`, a JSON module, `response.json()`,
a JWK import — is unchanged, and deliberately so. It takes the nested branch, arms nothing, and goes on
spending the surrounding evaluation's budget, which is what bounds a large document a script hands to
`JSON.parse`. What changes is a parse the host issues itself: it now gets a budget instead of an arbitrary
one, and a `LimitMemory` that had never applied to it now does. A host relying on `Parse` being
unaccounted — parsing a document larger than its own `LimitMemory` between evaluations — has to raise the
limit or bracket the parse in a `MemoryLimitConstraint.Begin`/`End` window of its own.

### 4.26 A deep recursion no longer refuses the host that started it ([#3343](https://github.com/sebastienros/jint/issues/3343))

`Options.Constraints.MaxExecutionStackCount` selects a lane that continues a deep call chain on a fresh
thread-pool thread while the calling thread blocks. That hop did not transfer engine ownership, so
`Engine._ownerThreadId` went on naming the thread parked below it and every public entry made from the far
side took the wrong-owner path:

```csharp
var engine = new Engine(o => o.Constraints.MaxExecutionStackCount = 1_000_000);
engine.SetValue("probe", new Func<int, int>(d => { engine.Evaluate("1 + 1"); return d; }));
engine.Execute("function recurse(n) { return n === 0 ? probe(n) : recurse(n - 1) + 0; }");

engine.Evaluate("recurse(10)");    // 4.16.x: fine
engine.Evaluate("recurse(2000)");  // 4.16.x: InvalidOperationException, "already in use by another thread"
```

One host thread, one script, no concurrency — the engine refused itself, and the exception escaped the
callback and killed the evaluation. `JsValue.FromObject`, `JsonParser.Parse` and anything else behind the
ownership check failed the same way. Combining the setting with `LimitMemory` was worse still: the memory
constraint checks ownership on every statement, so a *plain* script with no host callback at all died at the
first hop.

The hop now hands ownership over for its duration and takes it back afterwards, carrying the memory-limit
segment with it so allocations are charged to the thread that makes them. It is a transfer, not a release: an
unrelated thread reaching a public entry mid-hop is refused exactly as it was before the hop started.

**What could break:** nothing an embedder configured. A test asserting that the exception is thrown was
asserting the bug. An engine left at the default `MaxExecutionStackCount` (`-1`) never took this lane and is
unaffected — `Options.Constraints.StackOverflowGuard`, the default, throws a `RangeError` without hopping
threads.

### 4.27 A long `+` returns a deferred string ([#3350](https://github.com/sebastienros/jint/issues/3350))

`s += x` and `s = s + x` mean the same thing and did not cost the same thing: the compound form builds into a
`StringBuilder`-backed value and is amortised linear, while a plain `+` produced a flat string per operation
and so copied the whole accumulated left operand on every iteration. Prepending (`s = x + s`) had no fast
path at all. From v5 a `+` whose result is at least 512 characters returns an *immutable* two-operand node
instead, and materializes the text on the first read that needs characters.

**What could break:** nothing a script can see — the value is the string it stands for, for equality,
hashing, property keys, `length`, every `String.prototype` method and `JSON.stringify`. Two things a host
might notice:

- `engine.Evaluate("a + b")` may hand back a `JsString` **subclass**. It always could — `+=` has returned one
  since long before v5 — so `is JsString`, `AsString()`, `ToString()` and `ToObject()` are unaffected, but an
  exact-type test (`result.GetType() == typeof(JsString)`) now fails for one more shape.
- The result keeps its two operands alive until something reads its text. A host that concatenates a large
  string and holds only the result, expecting the operands to become collectable immediately, gets that back
  by reading the result once (`AsString()` is enough) — the node then drops both references.
### 4.28 A read-only host collection refuses script with a JavaScript error ([#3382](https://github.com/sebastienros/jint/issues/3382))

A wrapped collection that declares itself read-only — `ReadOnlyCollection<T>`, `ImmutableList<T>`,
`ImmutableArray<T>`, `ArrayList.ReadOnly(…)`, a host `IList<T>` whose `IsReadOnly` is `true`, or anything
reaching the engine as `IReadOnlyList<T>` — raised the CLR's own `NotSupportedException` out of
`Engine.Evaluate` when script tried to change it. Not a `JavaScriptException`, so neither a script
`try`/`catch` nor a host `catch (JavaScriptException)` could see it:

```js
// 4.16.x, for engine.SetValue("ro", new ReadOnlyCollection<int>([1, 2, 3]))
//         with options.Interop.AllowWrite = true
ro.push(4)        // NotSupportedException: Collection is read-only.
ro.pop()          // NotSupportedException
ro.length = 5     // NotSupportedException
delete ro[0]      // NotSupportedException
```

Each of those now gets the answer the specification gives for the same operation on a frozen array-like,
which is not the same answer for all of them. `push`, `pop`, `splice`, `sort` and `reverse` are specified in
terms of `Set(O, k, v, true)` and `DeletePropertyOrThrow`, so they raise a `TypeError` in either mode; a bare
assignment is an ordinary `[[Set]]` returning `false`, which is a `TypeError` only in strict mode:

```js
// 5.x
ro.push(4)        // TypeError: Cannot assign to read only property '3' of object '#<Object>'
ro.length = 5     // sloppy: silently ignored;  strict: TypeError
ro[0] = 9         // sloppy: silently ignored;  strict: TypeError
delete ro[0]      // sloppy: false;             strict: TypeError
```

The collection is left untouched in every case, which it was not before: `pop` and `splice` reached the
target and mutated it part-way before the CLR refused.

`ArraySegment<T>` moves in the same change and to a different place. It reports
`ICollection<T>.IsReadOnly` as `true` to mean *cannot grow* — the same thing `T[]` reports through that
interface — so it is treated as **fixed-size**: length changes are refused with the `TypeError` a `T[]` live
view already gave, and element writes keep working.

**What could break:** a `catch (NotSupportedException)` around `Evaluate` written to absorb this no longer
fires. Script that relied on a length change silently succeeding never existed — it threw. Nothing changes
for a growable collection, for `Options.Interop.AllowWrite = false` (which refused these writes already), or
for a `T[]` exposed under `ArrayConversionMode.LiveView`.

### 4.29 `Intl` reads the CLDR provider for date names and numbering-system digits ([#3354](https://github.com/sebastienros/jint/issues/3354))

`Intl.DateTimeFormat` took its month, weekday and day-period names straight from .NET's `CultureInfo`, and
every formatter transliterated digits by looking the numbering system up in an embedded table. Both now go
through `Options.Intl.CldrProvider` — `GetMonthNames`, `GetWeekdayNames`, `GetDayPeriods` and
`GetNumberingSystemDigits` — so overriding any of them reaches script:

```c#
// 5.x — one override, and Intl.DateTimeFormat().format() shows it
sealed class MyMonths : DefaultCldrProvider
{
    public override string[]? GetMonthNames(string locale, string style, string? calendar)
        => style == "long" && locale.StartsWith("fr")
            ? ["Nivose", "Pluviose", "Ventose", "Germinal", "Floreal", "Prairial",
               "Messidor", "Thermidor", "Fructidor", "Vendemiaire", "Brumaire", "Frimaire"]
            : base.GetMonthNames(locale, style, calendar);
}
```

`GetNumberingSystemDigits` is now also what makes a numbering system *usable*: `Intl.NumberFormat`,
`Intl.RelativeTimeFormat` and `Intl.DurationFormat` accept exactly the systems it answers for, and
`Intl.DateTimeFormat` accepts those plus whatever `GetSupportedNumberingSystems` advertises. Before this,
a host could advertise a system through `Intl.supportedValuesOf('numberingSystem')` that every constructor
then rejected, with nothing able to transliterate it. The provider is asked once, while the formatter is
being constructed; `format()` reads the resolved digits and never calls the provider.

An engine that configures no provider — or one whose provider derives from `DefaultCldrProvider` — formats
exactly as it did before. The names `DefaultCldrProvider` answers with are read out of the same
`CultureInfo` the formatter would otherwise have used, and a provider whose names match .NET's writes
nothing at all.

**What could break:** a host implementing `ICldrProvider` from scratch rather than deriving from
`DefaultCldrProvider`. Its `GetMonthNames`, `GetWeekdayNames`, `GetDayPeriods` and
`GetNumberingSystemDigits` were dead code and are now read. The fallback is per member and per style:
`null`, or an array of the wrong length, keeps .NET's data, so a provider that answers only for `"long"`
months keeps .NET's abbreviated ones. Two lanes stayed out of reach when this shipped and are reached in
4.33: `month: "narrow"` / `weekday: "narrow"`, and the day period a `timeStyle` writes.

### 4.30 A calendar `ICalendarProvider` claims is a calendar `Temporal` accepts ([#3355](https://github.com/sebastienros/jint/issues/3355))

`ICalendarProvider` could correct one of the eleven non-ISO calendars Jint implements, and nothing more.
*Adding* one was documented as four overrides and was in fact impossible at any number: every `Temporal`
entry point canonicalizes the identifier through a fixed table of eighteen before a provider is consulted,
so `withCalendar('mayan')` was a `RangeError` that no provider could prevent. `ICalendarProvider.GetSupportedCalendars`
had no caller at all — `Intl.supportedValuesOf('calendar')` reads `ICldrProvider.GetSupportedCalendars`,
which is a different provider.

Three things changed, none of which an unconfigured engine can observe:

- **Canonicalization asks the provider.** An identifier Jint's own table does not name, that is a
  well-formed Unicode calendar type and that the configured provider claims, is now a valid calendar
  identifier — in `withCalendar`, in a `calendar` field and in a `[u-ca=…]` annotation.
- **`DefaultCalendarProvider.IsSupported` answers from `GetSupportedCalendars()`** instead of from its own
  hardcoded list, so the two cannot disagree and adding a calendar is three overrides rather than four. The
  two lists held exactly the same eleven identifiers, so the answer is unchanged for every input.
- **An unknown calendar reaching a conversion is a `RangeError`, not a `NotSupportedException`.** That
  applies to a provider that names a calendar and then hands its conversion back to the base class, and to
  calendar arithmetic — `add`, `subtract`, `until`, `since` — which is implemented per calendar inside the
  engine and is not routed through the provider. Both used to throw a CLR exception out of
  `Engine.Evaluate`, where neither script nor an embedder's `catch (JintException)` could see it.

```c#
// 5.x — three overrides, and Temporal accepts the identifier everywhere
sealed class WithMayan : DefaultCalendarProvider
{
    public override IReadOnlyCollection<string> GetSupportedCalendars() => [.. base.GetSupportedCalendars(), "mayan"];
    public override CalendarFields IsoToCalendarFields(string calendar, int isoYear, int isoMonth, int isoDay) => …;
    public override IsoDateFields? CalendarFieldsToIso(string calendar, int year, string? monthCode, int month, int day, string overflow) => …;
}
```

**What could break:** a provider whose `IsSupported` and `GetSupportedCalendars` disagreed. `IsSupported` is
now the list's answer for anything deriving from `DefaultCalendarProvider` without overriding it, and it is
the question the engine asks — so a calendar in the list is now claimed, and one absent from it is not.
A provider implementing `ICalendarProvider` from scratch is unaffected: both members are still its own.
Second, a host calendar reaches construction, the field accessors, `with`, `toString` and the
`PlainYearMonth`/`PlainMonthDay` conversions, but neither arithmetic nor `Intl.DateTimeFormat`; both refused
it with a `RangeError` when this shipped.
[§4.40](#440-one-list-of-calendars-and-icalendarprovider-owns-it-3404) gives it `Intl`, and
[§4.44](#444-calendar-arithmetic-is-answered-by-whoever-answers-for-the-calendar-3403) gives it arithmetic.

### 4.31 A module graph too deep to link raises an error instead of ending the process ([#3401](https://github.com/sebastienros/jint/issues/3401))

`CyclicModuleRecord.InnerModuleLinking` and `InnerModuleEvaluation` descend once per module, the way the
spec writes them. The load phase does not — it drives a work queue — so the depth of a graph an engine
can *load* is `Options.Modules.MaxModuleGraphDepth`, while the depth it can *link and evaluate* was the
calling thread's stack. Exceeding that is a native stack overflow: no exception, nothing in a `catch`,
no log, and the process gone. Measured at roughly 700 bytes of stack per module, a thousand-module import
needed 640–768 KB on `net8.0` and 768–896 KB on `net10.0` — inside a factor of two of an ordinary thread's
stack, which is why [#3308](https://github.com/sebastienros/jint/issues/3308) killed one CI leg and passed
on four.

Both algorithms now probe for headroom, under the same `Options.Constraints.StackOverflowGuard` that covers
script recursion ([4.5](#45-the-stack-overflow-guard-is-enabled-by-default-3057)), and raise
`RangeError: Maximum call stack size exceeded while linking module '…'` — or `while evaluating module '…'` —
naming the module the walk gave up on. It is an ordinary JavaScript error: a rejection for `import()`, a
`JavaScriptException` out of `Engine.Modules.Import`, and an engine that still runs script afterwards. A
graph that fails during evaluation is left *errored* — every module the walk had reached is marked evaluated
with that error — so importing the same root again fails the same way rather than handing back a namespace
for a graph whose bodies never ran.

Unlike the script lane, this half is **not** displaced by `Options.Constraints.MaxExecutionStackCount`: that
limit's own probe sits at the call expression, which no part of the module pipeline reaches, so honouring the
precedence there would leave nothing in its place.

**What could break:** a host that already sized its threads for its deepest graph sees no change — the probe
fires only where the process would otherwise have died. What changes is that "too deep" is now an outcome you
can observe, so a graph near the limit surfaces as a failed import rather than as an occasional lost process.
This is a catchable *failure*, not a raised ceiling: how deep a graph an engine can import is still decided by
the thread's stack rather than by `MaxModuleGraphDepth`. Making the two algorithms iterative, the way loading
already is, is tracked in [#3401](https://github.com/sebastienros/jint/issues/3401).

### 4.32 A host operator that throws reports what it threw ([#3408](https://github.com/sebastienros/jint/issues/3408))

With `Options.Interop.AllowOperatorOverloading = true`, an exception raised inside a host `operator` reached
the embedder as something else. Every other interop call site normalizes what it catches with
`e as TargetInvocationException ?? new TargetInvocationException(e)`; the operator path wrapped
`e.InnerException`, and by the time it catches, the invoke has already been unwrapped — so it reported the
host exception's *cause*, or, when there was none, an empty `TargetInvocationException`:

```csharp
// 4.16.x, for a host type whose operator+ throws InvalidOperationException("HOST BOOM")
//         and whose operator- throws NotSupportedException("…", new ArgumentException("nested cause"))
engine.Evaluate("a.Add(b)");   // InvalidOperationException: HOST BOOM
engine.Evaluate("a + b");      // TargetInvocationException: Exception has been thrown by the
                               //                            target of an invocation.  ← message gone
engine.Evaluate("a.Sub(b)");   // NotSupportedException: metres do not subtract
engine.Evaluate("a - b");      // ArgumentException: nested cause                       ← the cause, not the error
```

```csharp
// 5.x — the operator and the ordinary method are the same exception
engine.Evaluate("a + b");      // InvalidOperationException: HOST BOOM
engine.Evaluate("a - b");      // NotSupportedException: metres do not subtract
```

`Options.CatchClrExceptions()` moves with it: the `JavaScriptException` is now built from the host
exception, so `JintException.TryGetClrException` hands back that exception rather than a placeholder, and
`Options.Interop.ExposeDetailedExceptionMessages = true` shows the host's message rather than the
placeholder's.

**What could break:** a `catch` written around `Evaluate` to absorb the placeholder — `catch
(TargetInvocationException)`, or a `catch` for the exception type that happened to be the host exception's
`InnerException`. Catch the exception the host actually raises instead. There is no option to restore the
old shape; the old shape reported an exception nothing threw.

### 4.33 An overload the argument cannot bind to is no longer selected ([#3407](https://github.com/sebastienros/jint/issues/3407))

Overload scoring rates each argument against each parameter and had no verdict for a plain type mismatch:
anything it did not recognize scored "will rarely succeed", and a *positive* score is a match. So a candidate
the argument could never bind to was selected whenever it was the only one, and the call then died converting
the argument. Two places take the first match and stop, and both were affected.

**Operators.** For a host type whose only `+` is `(T, T)`, four spec-defined expressions threw instead of
evaluating. There is no overload for those operand pairs, so `ApplyStringOrNumericBinaryOperator` applies:
`ToPrimitive` both operands and, one of them being a string, concatenate.

```js
// 4.16.x, for engine.SetValue("v", new Vector2D(1, 2)) with AllowOperatorOverloading = true
'text ' + v        // InvalidCastException: Invalid cast from 'System.String' to 'Vector2D'
v + ' text'        // InvalidCastException
1 + v              // InvalidCastException: Invalid cast from 'System.Double' to 'Vector2D'
true + v           // InvalidCastException: Invalid cast from 'System.Boolean' to 'Vector2D'
```

```js
// 5.x
'text ' + v        // "text (1, 2)"
v + ' text'        // "(1, 2) text"
1 + v              // "1(1, 2)"
true + v           // "true(1, 2)"
```

An overload that *does* accept the pair still wins: declare `(T, double)`, `(double, T)` or `(string, T)`
and those expressions are operator calls exactly as before.

**Constructors.** `new Boxed('text')`, where the only constructor takes a type no string converts to, raised
the CLR's `InvalidCastException` out of `Evaluate` — not a `JavaScriptException`, so no script `catch` and no
host `catch (JavaScriptException)` could see it. It is now the resolution failure it always was, a `TypeError`
reading `Could not resolve a constructor for the specified arguments.`

Method calls are unchanged: `MethodInfoFunction` already asked the converter per candidate and moved on when
it declined, so a hopeless candidate was proposed and then rejected. It is no longer proposed.

**What could break:** a `catch (InvalidCastException)` around `Evaluate` written to absorb either shape no
longer fires — catch `JavaScriptException`, or stop writing the expression that threw. A host relying on
`'s' + v` throwing as a type check has to test explicitly instead. Nothing changes for
`AllowOperatorOverloading = false`, which took the concatenating path already.
### 4.34 A narrow date name is narrow, and a `timeStyle` day period is the locale's ([#3398](https://github.com/sebastienros/jint/issues/3398), [#3399](https://github.com/sebastienros/jint/issues/3399))

Two lanes of `Intl.DateTimeFormat` wrote something other than the locale data the specification names, and
both are the ones 4.29 could not reach.

**`month: "narrow"` and `weekday: "narrow"` rendered the abbreviated name.** Both options mapped onto the
abbreviated .NET pattern letter — `MMM`, `ddd` — because .NET has no narrow one. The narrow names now go
into that formatter's own `DateTimeFormatInfo`, so the same pattern letter writes them:

```js
// 5.0
new Intl.DateTimeFormat('en', { month: 'narrow' }).format(Date.UTC(2024, 0, 15));   // "Jan"
new Intl.DateTimeFormat('en', { weekday: 'narrow' }).format(Date.UTC(2024, 0, 15)); // "Mon"

// 5.x — what every browser writes
new Intl.DateTimeFormat('en', { month: 'narrow' }).format(Date.UTC(2024, 0, 15));   // "J"
new Intl.DateTimeFormat('en', { weekday: 'narrow' }).format(Date.UTC(2024, 0, 15)); // "M"
```

`ICldrProvider.GetMonthNames` and `GetWeekdayNames` are asked for `"narrow"` for the first time, and unlike
the other styles the shared `DefaultCldrProvider.Instance` is asked too, because .NET has nowhere to read
either answer from directly. Narrow **weekdays** start from `ShortestDayNames`, a slot no pattern letter
reaches and whose contents are a *platform* difference rather than a locale one: for `en` it holds CLDR's
narrow names on Windows (`S`, `M`, `T`, …) and its short ones on Linux (`Su`, `Mo`, `Tu`, …), so an array in
which no entry is a single text element is narrowed entry by entry and `Intl` writes the same seven either
way. Narrow **months** .NET does not carry at all, so they are derived the same way, from the first text
element of the abbreviated name, upper-cased in the locale's own casing rules. Both reproduce CLDR for
locales whose narrow form is an initial — Latin, Cyrillic and Greek — and neither does where it is not, such
as the numeric narrow months of `ja`, `ko`, `cs` and `he`; a host that needs those exactly overrides the one
member.

**`timeStyle` wrote English `AM`/`PM`.** The component lane (`hour` with `hour12`) rendered the locale's
designators and the `timeStyle` lane wrote two literals, so one formatter's two lanes disagreed:

```js
// 5.0
new Intl.DateTimeFormat('ar', { timeStyle: 'short' }).format(Date.UTC(2024, 0, 15, 15, 30));            // "3:30 PM"
new Intl.DateTimeFormat('ar', { hour: 'numeric', hour12: true }).format(Date.UTC(2024, 0, 15, 15, 30)); // "3 م"

// 5.x — both lanes, one source
new Intl.DateTimeFormat('ar', { timeStyle: 'short' }).format(Date.UTC(2024, 0, 15, 15, 30));            // "3:30 م"
```

**What could break:** any output containing a narrow month or weekday, and any `timeStyle` output in a
12-hour locale whose designators are not `AM`/`PM`. `en` is unchanged in both: its designators are those two
letters, and its narrow names are what the derivation produces. Formatting that asks for neither a narrow
style nor a day period is untouched, and so is the cost of constructing it — the narrow names are read only
when a narrow style was actually requested.
### 4.35 `Intl.DurationFormat` formats in the numbering system it reports ([#3400](https://github.com/sebastienros/jint/issues/3400))

`Intl.DurationFormat` resolved a numbering system, reported it from `resolvedOptions()`, and then wrote
Latin digits anyway. Its three siblings — `Intl.NumberFormat`, `Intl.DateTimeFormat`,
`Intl.RelativeTimeFormat` — all transliterated their output; this one stored the value and nothing read it.

```js
// 5.0
new Intl.DurationFormat('en', { numberingSystem: 'arab' }).resolvedOptions().numberingSystem; // "arab"
new Intl.DurationFormat('en', { numberingSystem: 'arab' }).format({ hours: 12, minutes: 30 }); // "12 hr, 30 min"

// 5.x
new Intl.DurationFormat('en', { numberingSystem: 'arab' }).format({ hours: 12, minutes: 30 }); // "١٢ hr, ٣٠ min"
new Intl.DurationFormat('en', { numberingSystem: 'arab', style: 'digital' }).format({ hours: 1, minutes: 2, seconds: 3 }); // "١:٠٢:٠٣"
```

Digits only: a unit name is not a number, so `hr` and `min` keep their own characters, which is what
`formatToParts` already said by giving them a `unit` type rather than an `integer` one. `format()` and
`formatToParts()` agree, because the specification defines the first as the concatenation of the second.

**What could break:** a duration formatted with an explicit `numberingSystem`, or a `-u-nu-` extension, that
is not `latn`. Nothing else moves — a formatter that resolves to `latn`, which is every formatter that does
not ask for something else, writes exactly what it wrote before.
### 4.37 A host collection exposed as `IList<T>` refuses a growing generic with a `TypeError` ([#3394](https://github.com/sebastienros/jint/issues/3394))

A host object handed to script under a collection *interface* — `ObjectWrapper.Create(engine, target,
typeof(IList<int>))`, which is what a `WrapObjectDelegate` writes — gets a plain wrapper whose `length` is
the target's read-only `Count`. `Array.prototype` generics read it through a reflected indexer, and one that
had to write past the end took the index straight to the collection:

```js
// 4.16.x, for engine.SetValue("host", ObjectWrapper.Create(engine, items, typeof(IList<int>)))
Array.prototype.push.call(host, 4)        // ArgumentOutOfRangeException out of Evaluate
Array.prototype.unshift.call(host, 0)     // ArgumentOutOfRangeException
```

Both are `Set(O, k, v, true)` over a position the view cannot hold, so both are now the `TypeError` that a
`false` `[[Set]]` raises — the same answer `Array.prototype.splice.call(host, 0, 0)` already gave for the
`length` write it makes. The collection is left untouched.

**What could break:** a `catch (ArgumentOutOfRangeException)` around `Evaluate` written to absorb this no
longer fires. Nothing changes for a target exposed under its own type or under a type that implements
`IList<T>` (both get a typed, growable wrapper), for reads, or for any generic that stays inside the
collection's bounds.
### 4.38 One list of calendars, and `ICalendarProvider` owns it ([#3404](https://github.com/sebastienros/jint/issues/3404))

Jint held the calendar identifiers in three places — `TemporalHelpers.CanonicalizeCalendar`,
`DateTimeFormatConstructor`'s own table and alias map, and `DefaultCldrProvider.GetSupportedCalendars` —
plus a fourth membership scan inside `Intl.DisplayNames`. They agreed on the sixteen for a default engine
and on nothing else: two different alias tables, and only one of them could be extended, so a calendar a
host added existed for `Temporal` and was invisible to `Intl`.

[AvailableCalendars](https://tc39.es/ecma402/#sec-availablecalendars) is one list, defined as the calendars
the implementation can format, and read by `Intl.supportedValuesOf('calendar')`, by `Intl.DateTimeFormat`'s
`calendar` option and — as its literal first step — by
[Temporal's `CanonicalizeCalendar`](https://tc39.es/proposal-temporal/#sec-temporal-canonicalizecalendar).
All four sites now read one internal `AvailableCalendars`, whose alias handling is the engine's existing
`CanonicalizeUValue("ca", …)` and whose extension point is `ICalendarProvider.GetSupportedCalendars`.

```js
// with a provider that adds "mayan" — 5.0 answered "gregory", false, and "gregory"
new Intl.DateTimeFormat('en', { calendar: 'mayan' }).resolvedOptions().calendar; // "mayan"
Intl.supportedValuesOf('calendar').includes('mayan');                            // true
new Intl.DateTimeFormat('en', { calendar: 'mayan', year: 'numeric', timeZone: 'UTC' })
    .format(Date.UTC(2024, 2, 5));                                               // "5138"
```

`islamic` and `islamic-rgsa` are the one place the two services still part, and they part because the two
specifications ask for different things: [CreateDateTimeFormat](https://tc39.es/ecma402/#sec-createdatetimeformat)
step 9 requires a formatter asking for one to resolve to another available calendar, and `Temporal` refuses
them outright. That is now stated once, beside the list, instead of being an accident of two switches
disagreeing.

Nothing an unconfigured engine does changes: the sixteen, the two deprecated identifiers and the aliases all
behave exactly as the four tables made them behave.

**What could break:** a host overriding `ICldrProvider.GetSupportedCalendars`. See [2. Removed API](#2-removed-api).
### 4.39 A host object's `Symbol.dispose`, `Symbol.asyncDispose` and `toJSON` belong to its own realm ([#3365](https://github.com/sebastienros/jint/issues/3365))

`ObjectWrapper` builds three members eagerly — `Symbol.dispose` for an `IDisposable` target,
`Symbol.asyncDispose` for an `IAsyncDisposable` one, and `toJSON` for a type that has one — and built them
as functions of the engine's **principal** realm. Every other member of the same object is resolved lazily
and takes the realm that is running, so inside a `ShadowRealm` the two disagreed on the same object:

```js
// 4.16.x, for shadowRealm.SetValue("handle", new Handle())  — a sealed class : IDisposable
handle.Dispose instanceof Function            // true
typeof handle[Symbol.dispose]                 // "function"
handle[Symbol.dispose] instanceof Function    // false
```

All three are now functions of the realm the wrapper is created in — the same realm its own prototype comes
from, and the one `ShadowRealm.SetValue` enters deliberately ([§4.23](#423-a-value-registered-on-a-shadowrealm-belongs-to-that-realm-3325)).

**What could break:** nothing that worked. Calling the member never consulted its prototype, so `using` and
`JSON.stringify` behaved correctly throughout; what changes is that a feature detection
(`x instanceof Function`), a reach for `call`/`apply`/`bind`, or an `Object.getPrototypeOf` inside a shadow
realm now gets the answer the realm's own intrinsics give. A host function the embedder builds itself
through `new ClrFunction(engine, …)` is unaffected and still belongs to the principal realm.
### 4.40 An index on a host collection is one property, however it is spelled ([#3384](https://github.com/sebastienros/jint/issues/3384))

`x[3]` and `x["3"]` are one property key, and a wrapped CLR collection answered them from two different
places: a number key went to the array-like view, a string key to the reflected indexer, which took whatever
index it parsed out of the key straight to the collection. So on a `List<int>` of three elements:

```js
// 4.16.x, for engine.SetValue("x", new List<int> { 1, 2, 3 })
//         with options.Interop.AllowWrite = true
x[3] = 9                    // ArgumentOutOfRangeException out of Evaluate
x["3"] = 9                  // ArgumentOutOfRangeException
x["3"]                      // ArgumentOutOfRangeException — a read
Object.assign(x, { 3: 9 })  // ArgumentOutOfRangeException
delete x[3]                 // ArgumentOutOfRangeException
x[-1] = 9                   // ArgumentOutOfRangeException
x.length = 5                // grows the list to 1,2,3,0,0
```

None of those is a `JavaScriptException`, so neither a script `try`/`catch` nor a host
`catch (JavaScriptException)` could see them. The view now answers every index-shaped key itself:

```js
// 5.x
x[3] = 9                    // 1,2,3,9  — what x.length = 4 already did
x["3"] = 9                  // 1,2,3,9
x[5] = 9                    // 1,2,3,0,0,9 — exactly what x.length = 6; x[5] = 9 gives
x["3"]                      // undefined
Object.assign(x, { 3: 9 })  // 1,2,3,9
delete x[3]                 // true, list untouched
x[-1] = 9                   // sloppy: silently ignored;  strict: TypeError
```

**A growable collection grows.** An index at or past the end is `CreateDataProperty` on an extensible
ordinary object, so it succeeds and makes room the way the `length` write does — including the default-valued
slots in between. A **fixed-size** target (`T[]` under `ArrayConversionMode.LiveView`, `ArraySegment<T>`,
`ArrayList.FixedSize`) keeps the `TypeError` #3381 gave it, and a **read-only** one keeps the refusal #3382
gave it.

**An index the view can never hold is refused, not handed to the collection.** A negative index, a
non-canonical one (`"08"`, `"+3"`), and one past what the target can address are all ordinary `[[Set]]`
refusals: silent outside strict mode, a `TypeError` inside it. `x[-1]` already read `undefined` and
`-1 in x` was already `false`, so there was no position to write to.

**A read-only exposed contract is now honoured by the view itself.** An array or list handed to script as
`IReadOnlyList<T>` refused element writes only when the index was string-spelled, because the refusal came
from that interface's get-only indexer rather than from the wrapper. It now refuses both spellings.

**What could break**, in each case only where the two spellings used to disagree:

- a `catch (ArgumentOutOfRangeException)` around `Evaluate` written to absorb an out-of-range index write no
  longer fires — the write succeeds on a growable collection and is refused on any other;
- `Object.freeze(list); list["0"] = 9` outside strict mode was a `TypeError` and is now silent, which is what
  `list[0] = 9` always did and what a frozen JavaScript array gives. `Reflect.set(list, "0", 9)` returns
  `false` instead of throwing, which is what `Reflect.set` is specified to do;
- `delete list["0"]` resets the slot instead of being refused, matching `delete list[0]`;
- a `T[]` live view accepts `x["2"] = 9`, which used to raise `ArgumentException` because the reflected
  indexer for an array is the `object`-typed `IList` one and bypassed item-type coercion.

Nothing changes for `Options.Interop.AllowWrite = false`, for a non-extensible or frozen wrapper, for a
dictionary-shaped target such as `JObject` (string keys still answer from its own keys), or for the
`Array.prototype` generics, which reached the view rather than the indexer already.

### 4.41 Two holes in the freeze, closed ([#3360](https://github.com/sebastienros/jint/issues/3360))

[§4.16](#416-options-is-configuration-until-an-engine-reads-it-and-frozen-afterwards-3327) says an `Options`
stops accepting changes once an engine has read it. Two things did not obey that, and both matter more now
that [§5.7](#57-a-host-can-read-back-the-configuration-of-an-engine-it-was-handed-3360) hands the frozen
instance to a host.

**`UseNodeBuiltinModules` on a frozen `Options` used to succeed silently.** It was the only public
configuration method that wrote a bare field rather than a guarded property, so it neither reached the engine
that already existed nor said so — while still changing every engine built from that instance afterwards,
including the process-wide instance behind `new Engine()`. It now throws
`InvalidOperationException` reading `Options.UseNodeBuiltinModules cannot be called…`, exactly as
`UseModules`, `AddLazyGlobal` and the rest already did.

**`Options.TimeSystem` memoized after the freeze.** The default `ITimeSystem` — a `DefaultTimeSystem` over
`Options.TimeZone` and `Options.Culture` — was built by the property's own getter and memoized into its
backing field with a plain `??=`. The engine's first read of it comes from `DateConstructor`, which is built
the first time a script touches `Date`, so the memoization landed *after* `MakeReadOnly`: reading a member of
a frozen `Options` wrote to it, and threads racing that read could be handed a clock each.

`MakeReadOnly()` now resolves it before it sets the flag, and the getter publishes with
`Interlocked.CompareExchange` for the unfrozen case. A frozen `Options` therefore has exactly one clock, and
reading it is a read — which is what makes §5.7 handing the instance back safe. Both instances were equal by
construction, so what `Date`, `Temporal.Now` and `Intl.DateTimeFormat` answer is unchanged.

**What could break:** code that called `UseNodeBuiltinModules` after building an engine now gets an exception
where it used to get silence — move the call before the first `new Engine(options)`. And freezing an
`Options`, which every engine build does, now allocates one `DefaultTimeSystem` even when nothing ever asks
for the clock, once per instance rather than once per engine; the default is built from `TimeZone` and
`Culture` as they stand at the freeze rather than at the first read, which on a frozen instance is the same
pair. Assigning `TimeSystem = null` before the freeze still gets the default back on the next read, as before.

**What is *not* closed, and is not a hole:** the freeze covers this object's settings, not the objects they
name. A `TypeResolver` (whose `Default` is a process-wide singleton with unguarded `MemberFilter`), a
`CultureInfo`, a module loader, an `HttpClient`, a storage or worker provider, and a `Constraint` instance the
host registered are all still the host's own mutable objects, reachable exactly as they were before they were
handed to Jint.

### 4.42 A regex built at run time is bounded by the configured `RegexTimeout` ([#3431](https://github.com/sebastienros/jint/issues/3431))

`Options.Constraints.RegexTimeout` reached only the regular expressions Jint's parser saw. A pattern built
while the script was running — `new RegExp(...)`, `RegExp(...)`, `RegExp.prototype.compile`, the coercion
behind `"x".match("...")`, and the sticky splitter `@@split` rebuilds even from a literal — was adapted
outside the parser and picked up the parser package's own default (5 seconds) instead, the same value for
every engine in the process.

```csharp
var engine = new Engine(options => options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(50));

// 4.16.x: RegexMatchTimeoutException after ~5 s, MatchTimeout == 00:00:05
// 5.x:    RegexMatchTimeoutException after ~50 ms, MatchTimeout == 00:00:00.05
engine.Evaluate("'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!'.match(new RegExp('^(a+)+$'))");
```

It moved in both directions: an engine that raised the timeout to 30 seconds was cut off at 5, and an
engine that lowered it — the reason a host sets it at all — ran for 5 seconds. An engine that never set
it was silently tightened from Jint's 10-second default to 5.

**What could break:** a run-time-built pattern that used to complete inside 5 seconds now has whatever
budget the engine was configured with, so an engine with a deliberately short `RegexTimeout` can raise
`RegexMatchTimeoutException` where it did not before. That is the setting doing what it says; raise
`Constraints.RegexTimeout`, or set `RegexTimeout` on the parsing options of the script in question, which
still outranks the constraint. A prepared script keeps carrying its own prepare-time value and now applies
it to the regexes it builds as well as the ones it declares.
### 4.43 A locale's own numbering system is what every `Intl` formatter resolves to ([#3418](https://github.com/sebastienros/jint/issues/3418))

Four formatters carry a `[[NumberingSystem]]`. Two of them asked `ICldrProvider.GetDefaultNumberingSystem`
when the caller named no system, and two returned `"latn"` unconditionally — so one engine gave one locale
two answers to the same question. All four now ask, through one helper, and the locale's default sits below
the locale's `-u-nu-` extension and below the `numberingSystem` option, where
[ResolveLocale](https://tc39.es/ecma402/#sec-resolvelocale) step 13 puts it.

```js
// 5.0 — with a CLDR-backed provider installed
new Intl.NumberFormat('ar-EG').resolvedOptions().numberingSystem;       // "arab"
new Intl.DurationFormat('ar-EG').resolvedOptions().numberingSystem;     // "latn"
new Intl.DurationFormat('ar-EG').format({ hours: 12 });                 // "12 hr"
new Intl.NumberFormat('ar-EG-u-nu-latn').resolvedOptions().numberingSystem;      // "arab"
new Intl.NumberFormat('ar-EG', { numberingSystem: 'nope' }).resolvedOptions().numberingSystem; // "latn"

// 5.x
new Intl.DurationFormat('ar-EG').resolvedOptions().numberingSystem;     // "arab"
new Intl.DurationFormat('ar-EG').format({ hours: 12 });                 // "١٢ hr"
new Intl.NumberFormat('ar-EG-u-nu-latn').resolvedOptions().numberingSystem;      // "latn"
new Intl.NumberFormat('ar-EG', { numberingSystem: 'nope' }).resolvedOptions().numberingSystem; // "arab"
```

The last two lines are `Intl.NumberFormat`'s half of the same defect: it read the locale's default into the
slot the `numberingSystem` option had just been read into, so the default outranked the `-u-nu-` extension
that must beat it, and a well-formed request for a system nothing can write dropped to Latin instead of
leaving the locale's default in place.

**What could break:** nothing on a default engine. The shipped `DefaultCldrProvider` has no per-locale
numbering data and answers `null` for every locale, so every formatter still resolves to `latn` and writes
exactly what it wrote before. It moves for an embedder who has installed a CLDR-backed
`ICldrProvider` — `Intl.RelativeTimeFormat` and `Intl.DurationFormat` now write that provider's digits for a
locale whose default is not Latin, the way `Intl.NumberFormat` and `Intl.DateTimeFormat` already did. To keep
Latin digits for such a locale, ask for them: `{ numberingSystem: 'latn' }`, or a `-u-nu-latn` subtag.
### 4.44 Calendar arithmetic is answered by whoever answers for the calendar ([#3403](https://github.com/sebastienros/jint/issues/3403))

`ICalendarProvider` supplies two conversions, ISO ↔ calendar fields, and the engine consulted them for the
field accessors, `from`, `with`, `toString` and the `PlainYearMonth`/`PlainMonthDay` conversions — but not
for `add`, `subtract`, `until` or `since`, which were written per calendar against the BCL. So a host that
**corrected** a calendar corrected half of it, and the two halves disagreed about the same date; a host that
**added** one got `RangeError: Calendar arithmetic is not implemented for 'mayan'`.

For a calendar the configured provider answers for, the year-and-month walk is now expressed in the two
conversions themselves: the same monthCode placed in the target year, ordinal months stepped across year
boundaries by the month count the conversion reports, and the day clamped to the month length it reports.
Weeks and days were always ISO days and still are.

```js
// with a provider that adds "mayan" — 5.0 raised RangeError for all of these
Temporal.PlainDate.from('2024-03-05').withCalendar('mayan').add({ months: 1 }).toString(); // "2024-04-05[u-ca=mayan]"
Temporal.PlainDate.from('2024-01-31').withCalendar('mayan').add({ months: 1 }).toString(); // "2024-02-29[u-ca=mayan]"
Temporal.PlainDate.from('2024-03-05').withCalendar('mayan')
    .until(Temporal.PlainDate.from('2025-07-19').withCalendar('mayan'), { largestUnit: 'month' }).toString(); // "P16M14D"
```

An engine that configures no provider reaches the per-calendar implementation it always did — proved, not
asserted: 774,158 `CalendarDateAdd` results over the eleven built-in calendars are byte-identical before and
after.

**What could break:** an engine that installs a provider for one calendar and relies on the engine's own
arithmetic for another. The provider is asked for every calendar it claims, and a derived
`DefaultCalendarProvider` claims all eleven by inheritance — which is the point, since its inherited
conversions are already what every field accessor reads. Over the same 774,158-case sweep the two paths
agree on 100.000% of cases within the BCL calendars' ranges. The 1.7% that differ are all `chinese` and
`dangi` dates whose arithmetic leaves `ChineseLunisolarCalendar`'s 1901–2101 or `KoreanLunisolarCalendar`'s
2051 ceiling, and out there the two part in kind rather than in detail:
[#3452](https://github.com/sebastienros/jint/pull/3452) made the engine's own arithmetic **refuse** a result
it cannot represent, while the provider path — which is written in the two conversions, and those still
answer — goes on answering. So
`Temporal.PlainDate.from('1950-01-01').withCalendar('chinese').subtract({ years: 100 })` is a `RangeError`
on a default engine and a date under any provider that claims `chinese`. To keep a calendar on the engine's
own arithmetic, do not claim it: override `IsSupported` to return `false` for it, or drop it from
`GetSupportedCalendars`.

What that answering path answers *with* changed after this section was written:
[4.50](#450-a-date-past-a-lunisolar-calendars-table-is-still-reckoned-in-that-calendar-3451) gave `chinese`
and `dangi` a real reckoning past the end of their tables, where the conversions used to fall back to
ISO-like fields. The subtraction above was `1849-11-13` — the ninth month, day 29, from a date in the
eleventh month, day 13 — and is now `1849-12-26`, which is the eleventh month, day 13.

A host provider is also free to clamp where the engine no longer does, and a conversion that answers with
its own boundary date every time is what made the month walk stand still forever. The walk keeps a
no-progress guard for that: a step that adds a month and does not move the date raises the same `RangeError`
an out-of-range addition raises, so a difference measured in a calendar the host supplied cannot hang the
engine either.

Two refusals that used to escape as CLR exceptions are now `RangeError`s a script can catch:
`DifferenceISODateTime` reached `CalendarDateUntil` with no realm to report against, and an out-of-range
difference threw out of `Engine.Evaluate`.


### 4.45 `GetOwnProperties` reports what the key enumerations report ([#3461](https://github.com/sebastienros/jint/pull/3461))

Deriving `GetOwnProperties` from `GetOwnPropertyKeys` ([2.6](#27-getownproperties-is-derived-not-overridden))
makes it agree with every other enumeration, and for three object shapes the two used to differ. Nothing in
script changes; what changes is what a host reading `GetOwnProperties`, converting an object with
`ToObject()`, or inspecting a scope in the debugger sees.

| Object | `GetOwnProperties()` in 4.16.x | in 5.x |
| --- | --- | --- |
| a function | `prototype`, `length`, `name`, [`arguments`, `caller`], own keys | `length`, `name`, `prototype`, [`arguments`, `caller`], own keys — the order `Object.getOwnPropertyNames` always reported, and the order the specification creates them in |
| a `String` object | own keys, symbols, `length` | `"0"`…`"n-1"`, `length`, own keys, symbols — the character indices are own properties of a `String` object, and every script-visible enumeration already listed them |
| a host object overriding only `GetOwnPropertyKeys` | the engine's own (usually empty) property tables | the keys the host declares |

A plain object with integer-like keys is also reported in `[[OwnPropertyKeys]]` order now — integer indices
ascending, then strings in insertion order, then symbols — rather than in storage order. Again, that is the
order `Object.keys` already used.

One in-box object gained a key rather than reordering its own: a lazily created `f.prototype` keeps
`constructor` in a field and used to declare it to `GetOwnProperties` alone, so
`Object.getOwnPropertyNames(f.prototype)` and `Reflect.ownKeys(f.prototype)` answered `[]` for a property
`hasOwnProperty` and `Object.getOwnPropertyDescriptor` both reported. Both now answer `["constructor"]`. It
stays non-enumerable, so `Object.keys` and `for..in` are unchanged.

Two smaller consequences of the same derivation, both debugger-only:

- `DebugScope.BindingNames` lists string keys only. A symbol-keyed own property of the binding object used to
  appear in that list as its `Symbol(...)` description; a symbol is not a binding name.
- A `with` scope over a host object whose properties live outside the engine's tables is no longer reported as
  an empty scope and dropped from `DebugInformation.CurrentScopeChain`.

### 4.46 `Intl` writes the numbering system's digits in the parts lane, and only in the number ([#3455](https://github.com/sebastienros/jint/issues/3455), [#3456](https://github.com/sebastienros/jint/issues/3456))

[FormatNumeric](https://tc39.es/ecma402/#sec-formatnumeric) is defined as the concatenation of exactly the
parts [FormatNumericToParts](https://tc39.es/ecma402/#sec-formatnumerictoparts) returns — both of them
[PartitionNumberPattern](https://tc39.es/ecma402/#sec-partitionnumberpattern) — so the two lanes cannot
legally disagree. `format` transliterated into `[[NumberingSystem]]` and `formatToParts` did not, and
`Intl.RelativeTimeFormat.prototype.formatToParts` inherited the Latin digits by copying those parts:

```js
const nf = new Intl.NumberFormat('en', { numberingSystem: 'arab' });
// 4.16.x / earlier 5.0
nf.format(1234.5);                                       // "١,٢٣٤٫٥"
nf.formatToParts(1234.5).map(p => p.value).join('');     // "1,234.5"
nf.formatRangeToParts(1, 5).map(p => p.value).join('');  // "1 - 5", against formatRange's "١-٥"

// 5.x — one number, one set of digits
nf.formatToParts(1234.5).map(p => p.value).join('');     // "١,٢٣٤٫٥"
nf.formatRangeToParts(1, 5).map(p => p.value).join('');  // "١-٥"
```

The same rewrite now applies to the number and not to the pattern it sits in.
`Intl.RelativeTimeFormat.prototype.format` transliterated its assembled string, so a `style: "short"`
abbreviation's full stop became the numbering system's decimal separator, and `Intl.NumberFormat.prototype.format`
did the same to a compact suffix in any locale whose own separator is not a full stop:

```js
// 4.16.x / earlier 5.0
new Intl.RelativeTimeFormat('en', { style: 'short', numberingSystem: 'arab' }).format(3, 'second');
// "in ٣ sec٫"  - U+066B ARABIC DECIMAL SEPARATOR, where the abbreviation's full stop belongs
new Intl.NumberFormat('de-DE', { numberingSystem: 'arab', notation: 'compact' }).format(1234567.891);
// "١,٢ Mio٫"

// 5.x
new Intl.RelativeTimeFormat('en', { style: 'short', numberingSystem: 'arab' }).format(3, 'second');  // "in ٣ sec."
new Intl.NumberFormat('de-DE', { numberingSystem: 'arab', notation: 'compact' }).format(1234567.891); // "١٫٢ Mio."
```

Two smaller things move with it. The `NumberFormat` an `Intl.RelativeTimeFormat` substitutes into its
patterns is now constructed with `[[NumberingSystem]]` in its options, as
[InitializeRelativeTimeFormat](https://tc39.es/ecma402/#sec-InitializeRelativeTimeFormat) requires — it had
been left to re-derive the system from a resolved locale the `numberingSystem` option had just stripped the
`-u-nu-` subtag from. And `formatRangeToParts` writes the separator `formatRange` writes rather than a fixed
`" - "`: the locale's tight form for a plain number, its spaced form for a currency, which is what test262's
`formatRange/en-US.js` and `formatRangeToParts/en-US.js` assert against each other.

**What could break:** nothing on a default engine — every formatter that resolves `latn` writes exactly what
it wrote before, and `latn` is what an unconfigured engine resolves for every locale. Output moves for a
formatter that asked for another numbering system, or for an embedder whose `ICldrProvider` gives a locale a
non-Latin default: the parts lane gains that system's digits, a pattern's own punctuation stops acquiring
them, and a locale whose decimal separator is a comma keeps its group separator as the full stop it is.
### 4.47 `Intl.DateTimeFormat`'s calendar default comes from the CLDR provider ([#3457](https://github.com/sebastienros/jint/issues/3457))

`ca` is a relevant extension key, and with no `calendar` option and no `-u-ca-` subtag its value is
`keyLocaleData[0]` — [ResolveLocale](https://tc39.es/ecma402/#sec-resolvelocale) step 13.c, the locale's own
calendar. That answer was read off `CultureInfo.Calendar` and mapped by .NET calendar *class*, which no host
could reach and which is coarser than the data: `HijriCalendar` and `UmAlQuraCalendar` are one .NET type each
and both answered `"islamic"`.

`ICldrProvider` gains a nineteenth member, `GetDefaultCalendar(string locale)`, and `DefaultCldrProvider`
answers it from CLDR's `calendarPreferenceData` — keyed by region, so a locale naming none is maximized
first. Four regions prefer something other than `gregory`: `AF` and `IR` (`persian`), `SA`
(`islamic-umalqura`) and `TH` (`buddhist`).

```js
// 4.16.x / earlier 5.0
new Intl.DateTimeFormat('ar-SA').resolvedOptions().calendar;             // "islamic"
new Intl.DateTimeFormat('ar-SA').format(new Date(Date.UTC(2026, 7, 27))); // "14/3/2026" - a Hijri day and month beside a Gregorian year

// 5.x
new Intl.DateTimeFormat('ar-SA').resolvedOptions().calendar;             // "islamic-umalqura"
new Intl.DateTimeFormat('ar-SA').format(new Date(Date.UTC(2026, 7, 27))); // "14/3/1448"
```

`islamic-umalqura` was never out of reach — it is in the supported list and an explicit
`{ calendar: 'islamic-umalqura' }` always resolved to it. It was only the *default* that could not name it,
and `islamic` is an identifier
[CreateDateTimeFormat](https://tc39.es/ecma402/#sec-createdatetimeformat) step 9 requires a formatter to
resolve away from rather than to.

Correcting the calendar a locale defaults to is now one override, the way correcting its numbering system is:

```c#
sealed class HebrewByDefault : DefaultCldrProvider
{
    public override string? GetDefaultCalendar(string locale) => "hebrew";
}
```

An answer the engine has no calendar for is discarded rather than resolved to — `keyLocaleData` is built from
the calendars the implementation supports — so a provider naming `"mayan"` gets `gregory`, as does one
answering `null`.

**What could break:** `ar-SA` and its region-mates now report and format in `islamic-umalqura`. Every other
locale reports exactly what it reported before, `.NET`'s answer and CLDR's having already agreed everywhere
else. A host implementing `ICldrProvider` **from scratch** rather than deriving from `DefaultCldrProvider`
has one more member to write; deriving costs nothing, and [5.2](#52-changing-one-locale-datum-is-one-override)
is why that is the shape the interface is documented for.
### 4.48 The blocking promise drain is bounded on the engine's clock, not on the wall clock ([#3406](https://github.com/sebastienros/jint/issues/3406))
### 4.49 `Duration.prototype.round` reckons in the calendar its `relativeTo` carries ([#3450](https://github.com/sebastienros/jint/issues/3450))

`round` read the calendar off a `PlainDate` `relativeTo` and then wrote `"iso8601"` into both calendar
operations it performs, so every rounding with a non-ISO `relativeTo` counted ISO years and ISO months under
that calendar's name. It now passes `relativeTo.[[Calendar]]`, which is what
[step 443](https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.round) says and what the
`ZonedDateTime` arm of the same method always did.

```js
const chinese = Temporal.PlainDate.from('1990-01-01').withCalendar('chinese');

new Temporal.Duration(0, 0, 0, 10000).round({ largestUnit: 'year', relativeTo: chinese }).toString();
// 5.0: "P27Y4M18D" — the ISO answer, which is also what round said for every other calendar
// 5.x: "P27Y4M19D" — what chinese.until(chinese.add({ days: 10000 }), { largestUnit: 'year' }) says

// thirteen months is a whole year in a lunisolar leap year, and the Chinese year 2023 is one
const leapYear = Temporal.PlainDate.from('2023-01-22').withCalendar('chinese'); // monthsInYear === 13
new Temporal.Duration(0, 13).round({ largestUnit: 'year', relativeTo: leapYear }).toString();
// 5.0: "P1Y1M"   5.x: "P1Y"
```

Past the end of a calendar's range it also stopped answering where its neighbours refuse:
`new Temporal.Duration(0, 0, 0, 200000).round({ largestUnit: 'year', relativeTo: chinese })` was `P547Y7M`
while `total` and `chinese.add({ years: 547 })` both raised
`RangeError: Date is outside the range supported by the 'chinese' calendar`. All three now say the same
thing, because all three now ask the same calendar.

**What could break:** any rounding of a duration against a non-ISO `relativeTo`. `round` was the only member
of `Temporal` still reckoning in ISO with another calendar's name attached — `until`, `since`, `add`,
`subtract` and `total` all already reckoned in the calendar — so a value that changes here was disagreeing
with all of them. An ISO `relativeTo` is unaffected, and so is a duration rounded without one: nothing
outside the calendar path moves.

### 4.50 A date past a lunisolar calendar's table is still reckoned in that calendar ([#3451](https://github.com/sebastienros/jint/issues/3451))

`ChineseLunisolarCalendar` spans ISO 1901-02-19 to 2101-01-28 and `KoreanLunisolarCalendar` 918-02-19 to
2051-02-10, while `Temporal.PlainDate` spans a quarter of a million years each way. Outside those two
tables the field accessors answered with the ISO year, `M{isoMonth}`, the ISO days-in-month and twelve
months a year — Gregorian fields wearing a lunisolar label, and nothing downstream could tell them from
real ones.

```js
const d = Temporal.PlainDate.from('1800-01-01').withCalendar('chinese');
d.year;         // 5.0: 1800    5.x: 1799
d.monthCode;    // 5.0: "M01"   5.x: "M12"
d.day;          // 5.0: 1       5.x: 7
d.daysInMonth;  // 5.0: 31      5.x: 30
d.daysInYear;   // 5.0: 365     5.x: 354
```

Refusing instead was not open to the engine.
[`NonISOCalendarISOToDate`](https://tc39.es/proposal-temporal/#sec-temporal-nonisocalendarisotodate) is
declared as returning *a Calendar Date Record* — not "either a normal completion … or a throw completion",
the phrasing its neighbour [`NonISODateAdd`](https://tc39.es/proposal-temporal/#sec-temporal-nonisodateadd)
carries and which is what lets the *arithmetic* refuse — and `get PlainDate.prototype.monthCode` reads it
without `?`. So the accessors have to answer, and `withCalendar` has no range to validate against either.

What answers now is the reckoning both calendars are defined by: months begin on the day of an
astronomical new moon in the calendar's own time zone, the eleventh month is the one containing the winter
solstice, and a year needing a thirteenth month takes it at the first month of its *sui* carrying no major
solar term. It is a fallback, not a replacement — inside a table that table still answers, byte for byte —
and it reproduces both tables exactly across every year over which they tabulate this calendar rather than
an older one: `chinese` from 1929, `dangi` from 1912, to the end of each.

**What could break:** anything reading `year`, `month`, `monthCode`, `day`, `dayOfYear`, `daysInMonth`,
`daysInYear`, `monthsInYear` or `inLeapYear` off a `chinese` or `dangi` date outside those tables, and
`from`, `with`, `equals`, `toPlainYearMonth`, `toPlainMonthDay` and `Intl` formatting, which all read the
same conversion. Every one of those values changes, because every one of them was the ISO value. Nine
other non-ISO calendars are untouched: each already had a reckoning of its own to fall back on past the end
of its BCL calendar, which is why only these two ever gave an ISO answer.

Two consequences worth naming:

- **A date the Korean table names but will not measure** is now reckoned too. `KoreanLunisolarCalendar`
  reports month 13 for 1189-01-09 and reports the year holding it as having no leap month, so
  `GetDaysInMonth` refuses the month `GetMonth` just named; the date used to come back as ISO fields for
  that reason rather than for being out of range.
- **Arithmetic past a table's end still refuses**, unchanged from
  [4.44](#444-calendar-arithmetic-is-answered-by-whoever-answers-for-the-calendar-3403) and
  [#3452](https://github.com/sebastienros/jint/pull/3452): `add`, `subtract`, `until` and `since` are
  written against the BCL calendar directly, and a result it cannot represent is still a `RangeError`.
  That is the same split `hebrew` and `persian` have always had — fields reckoned, arithmetic refused.
  What does change is the **provider** path: §4.44 said a provider claiming `chinese` walks the two
  conversions and so answers `1849-11-13` where a default engine raises `RangeError`, because those
  conversions fell back to ISO-like fields. They no longer do, so that walk is now the Chinese calendar
  rather than an ISO one wearing its name; the two paths still part in kind, but the answering one is no
  longer wrong.

Far past either table the series the reckoning is built on lose accuracy — beyond roughly ±70,000 years a
month can come back at 28 or 31 days — which is what implementation-defined permits and what every engine
computing these calendars does. Every search is bounded, so it degrades rather than failing to return.

### 4.54 Parsing a `Temporal` or `Intl` string cannot fail because the machine was busy ([#3485](https://github.com/sebastienros/jint/issues/3485))

The internal patterns that parse ISO 8601 strings for `Temporal` and validate BCP 47 tags for `Intl` each
carried a 100 ms `Regex.MatchTimeout`. That is a **wall-clock** deadline sampled while matching, so it
cannot tell a pattern that is backtracking apart from a thread that lost the CPU — and a descheduled
thread trips it having burned almost no CPU at all. A single match of the valid, 8-character string
`10:30:00` was measured taking 440 ms of wall clock on an ordinary loaded machine, 4.4x over that budget.

The failure that produced was not a JavaScript error. `RegexMatchTimeoutException` is one of the
exceptions Jint propagates to the host rather than converting, so it escaped `Engine.Evaluate` as a CLR
exception, straight through the script's own `try`/`catch`:

```csharp
// 4.16.x, on a loaded machine: throws RegexMatchTimeoutException out of Evaluate, uncatchable by
//                              the script, with a message blaming "very large inputs or excessive
//                              backtracking" for a 19-character valid string
// 5.x:     returns "2024-01-15"
engine.Evaluate("try { Temporal.PlainDate.from('2024-01-15T10:30:00').toString() } catch (e) { 'caught' }");
```

The patterns are fixed and internal — only the input is script-supplied, never the pattern — and their
cost is now bounded by construction instead: the fixed-width ones reject anything longer than the longest
string they could match without matching at all, and the annotation list is walked rather than scanned
with a pattern that was quadratic on input holding no `]`.

**What could break:** nothing an embedder configured. `Options.Constraints.RegexTimeout` never reached
these patterns and still does not — it bounds script-supplied regular expressions, which is unchanged
(see [§4.42](#442-a-regex-built-at-run-time-is-bounded-by-the-configured-regextimeout)). A host that
caught `RegexMatchTimeoutException` around `Engine.Evaluate` to absorb this can drop that handler; a host
that treated it as a signal the script was hostile was reading a scheduling artefact.
### 4.55 An index a wrapped collection does not have does not exist, in every lane ([#3423](https://github.com/sebastienros/jint/issues/3423))

On every array-like CLR wrapper, `[[HasProperty]]` and `[[GetOwnProperty]]` gave different answers for an
index outside the collection:

```js
// 4.16.x, for engine.SetValue("x", new List<int> { 1, 2, 3 })
3 in x                          // false
x[3]                            // undefined
x.hasOwnProperty(3)             // true
x.hasOwnProperty("3")           // true
x.hasOwnProperty(-1)            // true
x.propertyIsEnumerable("3")     // true
Object.getOwnPropertyDescriptor(x, 3)   // a descriptor
Object.getOwnPropertyNames(x)   // ["0","1","2"]
```

That is not a divergence between two lanes an implementation may choose.
[OrdinaryHasProperty](https://tc39.es/ecma262/#sec-ordinaryhasproperty) is defined in terms of
`[[GetOwnProperty]]`, so `3 in x` being `false` while `x.hasOwnProperty(3)` is `true` on the same object is a
contradiction. The cause: the view owned `Get`, `Set`, `HasProperty`, `Delete` and both key enumerations, and
left `[[GetOwnProperty]]` to `ObjectWrapper`, which resolves the reflected indexer and reports a descriptor
for **any** parseable index — including a negative or non-canonical one.

In 5.x the descriptor lane reads the same index range as the rest, so all four of `in`, `hasOwnProperty`, the
indexed read and `Object.getOwnPropertyNames` agree, and `hasOwnProperty`, `propertyIsEnumerable` and
`Object.getOwnPropertyDescriptor` answer `false`/`undefined` for `3`, `10`, `-1`, `"08"` and `"+3"` alike.
Positions the view **does** have are unchanged, in both spellings.

Two consequences worth naming:

- **A cached descriptor is no longer the answer after the collection shrinks.** Asking about an index stored
  the reflected indexer's descriptor on the wrapper, so `x.hasOwnProperty(2)` went on being `true` after
  `x.length = 1`. The range is read first, so it cannot be.
- **`Object.defineProperty(x, 5, …)` for a position the view does not have is refused** rather than storing a
  descriptor the other lanes then deny. That is the answer script already got — the reflected indexer's
  descriptor was non-configurable, so the redefinition was rejected — now given by the view itself.
  `Reflect.defineProperty` returns `false`, `Object.defineProperty` raises a `TypeError`.

**What could break:** a host or script relying on `hasOwnProperty` / `propertyIsEnumerable` /
`Object.getOwnPropertyDescriptor` reporting an out-of-range index as present. Nothing changes for in-range
indices, for named CLR members, or for a dictionary-shaped target such as Newtonsoft's `JObject`, whose
index-shaped string keys are dictionary keys and still answer from its own key set.
### 4.56 An index outside a wrapped collection is refused, not handed to the collection ([#3422](https://github.com/sebastienros/jint/issues/3422))

[§4.40](#440-an-index-on-a-host-collection-is-one-property-however-it-is-spelled-3384) gave the array-like
*view* ownership of every index-shaped key. A host collection that has a `Count` and an indexer of its own,
but none of the three interfaces that produce such a view, does not get one — and every index-shaped key on it
resolved the reflected indexer, which takes whatever index it parsed out of the key straight to the
collection:

```csharp
sealed class Window : IReadOnlyCollection<int>
{
    public int this[int index] { get => _items[index]; set => _items[index] = value; }
    public int Count => _items.Count;
    // ...
}
```

```js
// 4.16.x, three elements, options.Interop.AllowWrite = true
x[3]                    // ArgumentOutOfRangeException out of Evaluate — a read
x["3"] = 9              // ArgumentOutOfRangeException
x[-1] = 9               // ArgumentOutOfRangeException
Reflect.set(x, 3, 9)    // ArgumentOutOfRangeException
3 in x                  // true, for a position that cannot be read at all
```

None of those is a `JavaScriptException`, so neither a script `try`/`catch` nor a host
`catch (JavaScriptException)` could see them. In 5.x the wrapper answers such a key itself:

```js
// 5.x
x[3]                    // undefined
x["3"] = 9              // sloppy: silently ignored;  strict: TypeError
x[-1] = 9               // same
3 in x                  // false
x.hasOwnProperty(3)     // false
delete x[3]             // true — there was nothing to delete
```

The refusal is conditioned on **two** facts, because the same lane serves shapes where an out-of-range key is
the point:

- the target is **bounded** — it has a `Count` and is not a dictionary. `d[99] = "x"` on a
  `Dictionary<int, string>` is a legitimate add and is unchanged, in both spellings;
- the member being resolved is an **integer-keyed** indexer. A string-keyed indexer on a collection — a
  `NameValueCollection` asked for `x["3"]` — still answers for its own key, whatever the count is.

Positions the collection does have are unchanged, and so is every named CLR member.

**What could break:** a `catch (ArgumentOutOfRangeException)` around `Evaluate` written to absorb an
out-of-range index on such a target no longer fires — the read is `undefined` and the write is refused. Code
reading `n in x` or `x.hasOwnProperty(n)` as "the indexer would accept `n`" gets `false` for out-of-range `n`
instead of `true`.

### 4.57 A collection exposed as `IList<T>` or `IReadOnlyList<T>` gets the wrapper that contract names ([#3421](https://github.com/sebastienros/jint/issues/3421))

The type a host object is *exposed* under decides which wrapper the engine builds, and the wrapper decides
what script may do with it — whether elements are writable, whether the collection can grow, and which lane
`Array.prototype` takes. That exposure is public API (`ObjectWrapper.Create(engine, target, type)`), it is
what a `WrapObjectDelegate` supplies, and it is what the member lane passes for a property whose **declared**
type is a collection interface.

The resolution scanned the exposed type's `GetInterfaces()` for `IList<>` and `IReadOnlyList<>` and nowhere
else. An interface is not among its own — `typeof(IList<int>).GetInterfaces()` yields `ICollection<int>`,
`IEnumerable<int>` and `IEnumerable` — so exposing a collection *as* one of the two contracts the code is
written to recognize found nothing, and the engine fell back to a wrapper the exposure had not named:

| exposure | 4.16.x | 5.x |
| --- | --- | --- |
| a host `IList<T>` (not a non-generic `IList`) as `IList<T>` | plain `ObjectWrapper` — no `Array.prototype`, member names for keys, a read-only `length` | the typed writable, growable view |
| a `List<T>` as `IList<T>` | untyped `ListWrapper` (element type `object`) | the typed view, with `T`-typed element writes |
| a `List<T>` or `T[]` as `IReadOnlyList<T>` | `ListWrapper`, taking its writability from the target | the typed read-only view |
| a host `IReadOnlyList<T>` (not an `IList`) as `IReadOnlyList<T>` | plain `ObjectWrapper` | the typed read-only view |

What that changes for the first row, which is the one an embedder is most likely to have:

```js
// engine.SetValue("host", ObjectWrapper.Create(engine, items, typeof(IList<int>)))
Object.keys(host)                     // 4.16.x: ["IndexOf","Insert","RemoveAt"]   5.x: ["0","1","2"]
host[3] = 9                           // 4.16.x: ArgumentOutOfRangeException        5.x: grows to 1,2,3,9
Array.prototype.push.call(host, 4)    // 4.16.x: TypeError (§4.37)                  5.x: 4
Array.prototype.pop.call(host)        // 4.16.x: TypeError                          5.x: removes the last element
```

Reads are unchanged in every row: `host.length`, `host[1]`, the `Array.prototype` generics, `Array.from`,
spread and `JSON.stringify` all answered correctly before, through the plain wrapper's reflection lane.

**What could break.** An exposure under `IList<T>` becomes writable and growable — the contract says so, but a
host that was relying on the accidental refusal should expose the collection as `IReadOnlyList<T>` instead,
which now produces a view that refuses every mutation as a catchable JavaScript error. An exposure under
`IReadOnlyList<T>` gains `Array.prototype`, index-keyed enumeration and a `length` where the fallback wrapper
had given it the target's members. A contract with no index in it — `ICollection<T>`, `IEnumerable<T>` — names
no view and is unchanged. A type the target does **not** implement is not cast to: that exposure keeps the
plain wrapper it has always had rather than raising `InvalidCastException`.

[§4.37](#437-a-host-collection-exposed-as-ilistt-refuses-a-growing-generic-with-a-typeerror-3394) is narrowed
by this: the `ArrayOperations.IndexWrappedOperations` lane it describes is no longer reachable under a JIT,
because the exposure that reached it now gets a typed wrapper. The refusal it added is still what a runtime
with no code for the typed factory's instantiation gives — Native AOT over a value-type element — and
`Jint.AotExample` is what pins it there.

`Engine.DrainEventLoopUntil` — what `UnwrapIfPromise` blocks in, and what a synchronous `Modules.Import`
waits in — used to arm its deadline from `DateTime.UtcNow`. It now arms it from a monotonic timestamp, read
through `Options.Constraints.TimeProvider` on `net8.0` and later and from `Stopwatch` everywhere else. Three
consequences, none of which changes a signature:

- **A system-clock step no longer moves a promise budget.** An NTP correction forwards used to cut a
  `PromiseTimeout` short and one backwards used to stretch it; neither does now. This is the rule the pump's
  own ceiling has always followed (`Engine.Pump.ElapsedSince`), and the drain was the one wait that did not.
- **`Options.Constraints.TimeProvider` now governs `Options.Constraints.PromiseTimeout` as well as
  `LimitExecutionTime`.** A host that supplied a clock to make its execution-timeout tests exact will find
  the promise budget measured against the same clock — so a *frozen* clock now keeps a blocking unwrap
  pending until the host advances it, where before it timed out on real time. If that is not wanted, leave
  `TimeProvider` at `TimeProvider.System` (the default) and register `OperationDeadlineConstraint` with its
  own clock instead; it takes one through its own constructor. The web-API timers are unaffected — they keep
  their own clock in `Options.WebApi.Timers.TimeProvider`, because they are a WHATWG feature rather than an
  execution budget.
- **A `TimeProvider` reporting a non-positive `TimestampFrequency` is rejected when the engine is built**,
  rather than only when `LimitExecutionTime` happens to be registered. It was always rejected by
  `ConstraintClock.Resolve` with a named `ArgumentException`; that clock is now resolved for every engine, so
  the diagnostic arrives where the clock was supplied instead of at whichever budget first tried to use it.

### 4.58 A worker's time budgets are measured on the worker's own clock ([#3481](https://github.com/sebastienros/jint/issues/3481))

`Options.Constraints.TimeProvider` governs two budgets — `LimitExecutionTime`'s constraint and
`PromiseTimeout`'s blocking drain ([§4.48](#448-the-blocking-promise-drain-is-bounded-on-the-engines-clock-not-on-the-wall-clock)).
A worker inherited the second of those as a *value* and measured it on the system clock, while the first
arrived as a replayed constraint factory that had closed over the **parent's** options and so read the
parent's clock. One worker engine, two budgets, two clocks.

Two changes, and they are one decision:

* **A worker does not inherit the clock.** `Options.Constraints.TimeProvider` is now named in the
  classification list `WorkerRequest.CreateDefaultOptions` is built on, as something that deliberately stays
  behind. A worker starts on `TimeProvider.System` — which is what it already did — and that is now a
  decision on the record rather than a gap. Nothing script-visible reads this clock (`Date`, `Temporal.Now`
  and `Intl` read `Options.TimeSystem`; `performance.now()` and the web-API timers read
  `Options.WebApi.Timers.TimeProvider`), so inheriting it would not have hidden or revealed anything from
  script; what it would have decided is whether the budgets the worker *does* inherit can still fire, and a
  clock a parent had stopped would have left them unable to.
* **The execution timeout follows the engine, not the options it was configured on.** The constraint
  `LimitExecutionTime` registers now takes its clock from the engine being built. For an engine a host
  constructs directly this is the same object and nothing changes at all. It differs only where a constraint
  factory is replayed onto another `Options` instance, which is exactly and only what a worker is.

**What to do.** If you supply a `TimeProvider` and spawn workers, and you want the worker on your clock too,
assign it in your `WorkerProvider` to the options the request hands you:

```csharp
var options = request.CreateDefaultOptions();
options.Constraints.TimeProvider = myClock;   // opt in, per worker
var worker = new Engine(options);
```

Before this change that half happened by accident for `LimitExecutionTime` and never for `PromiseTimeout`.
If you do not supply a `TimeProvider` at all, nothing here is observable.

### 4.59 `Intl.NumberFormat` walks one pattern for both lanes, non-finite values included ([#3465](https://github.com/sebastienros/jint/issues/3465))

[PartitionNumberPattern](https://tc39.es/ecma402/#sec-partitionnumberpattern)'s NaN and infinity branches
choose the number's own text and nothing else, so the pattern
[GetNumberFormatPattern](https://tc39.es/ecma402/#sec-getnumberformatpattern) selects is still selected and
still walked. Neither lane did that: the parts lane returned the number alone, and the string lane handed a
non-finite value to .NET's saturating `double`-to-`long` conversion.

```js
const usd = new Intl.NumberFormat('en', { style: 'currency', currency: 'USD' });
// 5.0
usd.format(NaN);       // "$0.00"
usd.format(Infinity);  // "$9,223,372,036,854,775,807.9223372036854775807"  (and a different number on net472)
usd.formatToParts(NaN);                                            // [nan]
new Intl.NumberFormat('en', { style: 'percent' }).format(NaN);     // "NaN"
new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(NaN);  // "0,00 €"

// 5.x
usd.format(NaN);       // "$NaN"
usd.format(Infinity);  // "$∞"
usd.formatToParts(NaN);                                            // [currency "$"][nan "NaN"]
new Intl.NumberFormat('en', { style: 'percent' }).format(NaN);     // "NaN%"
new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' }).format(NaN);  // "NaN €"
```

Three narrower disagreements between the same two lanes go with it.

A CLDR unit pattern can put text on both sides of the number, and the parts lane reported only the trailing
side — so `formatToParts` described a string `format` never wrote. The leading side is a `unit` part of its
own, and the sign stands after it:

```js
const nf = new Intl.NumberFormat('ja-JP', { style: 'unit', unit: 'kilometer-per-hour', unitDisplay: 'long' });
nf.format(1);         // "時速 1 キロメートル", unchanged
nf.formatToParts(1);
// 5.0: [integer "1"][literal " "][unit "キロメートル"]
// 5.x: [unit "時速"][literal " "][integer "1"][literal " "][unit "キロメートル"]
```

`notation: "scientific"` and `"engineering"` of exactly zero fell back to plain decimal formatting, where
[PartitionNotationSubPattern](https://tc39.es/ecma402/#sec-partitionnotationsubpattern) writes `"0"` for an
exponent of zero like any other: `new Intl.NumberFormat('en', { notation: 'scientific' }).format(0)` was
`"0"` and is `"0E0"`, which is what its own parts lane already said. A non-finite value takes no notation
sub-pattern at all, so `format(Infinity)` stays `"∞"` under either notation.

And the currency string lane interpolated a literal `"-"` and `"+"` where the parts lane read
`NumberFormatInfo.NegativeSign` / `PositiveSign`. ECMA-402 calls it "the ILND String representing the minus
sign", so it is the locale's own datum:
`new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP' }).format(-5)` gains the U+061C ARABIC
LETTER MARK its parts lane always reported. Negative zero under a notation gains its sign for the same
reason — `format(-0)` is `"-0E0"`, not `"0E0"`.

**What could break:** any output that read a non-finite value through a `style`, a `formatToParts` walk over
a locale whose unit pattern has a prefix (`ja-JP`, `ko-KR`, `zh-TW` long units), scientific or engineering
notation of zero, and a currency's sign in a locale whose sign is not ASCII. A string that was already the
concatenation of its own parts does not move: this is the two lanes being brought onto one walk, and it is
that walk that decides.

### 4.60 `Intl.DateTimeFormat` formats in the calendar it reports ([#3467](https://github.com/sebastienros/jint/issues/3467))

[FormatDateTimePattern](https://tc39.es/ecma402/#sec-formatdatetimepattern) step 13 reads a pattern's field
values out of `dateTimeFormat.[[Calendar]]` — the resolved calendar, and the one `resolvedOptions().calendar`
reports — so the two cannot come apart. They did wherever a locale's own .NET `CultureInfo` carried a
non-Gregorian `Calendar` of its own: Jint applied the resolved calendar and .NET then applied the culture's on
top, so `ar-SA` asked for `gregory` answered `"gregory"` and wrote a Hijri date. The parts lane, which reads
the `DateTime` fields directly, applied only one of the two — which is why it disagreed with `format()` field
by field rather than agreeing on the wrong answer.

A formatter's `DateTimeFormatInfo` is now pinned to the culture's own Gregorian calendar, leaving the resolved
calendar the only one anything converts to.

```js
const d = new Date(Date.UTC(2026, 7, 27)); // 27 August 2026 = 14 Rabi' I 1448

// 5.0
new Intl.DateTimeFormat('ar-SA', { calendar: 'gregory' }).format(d);        // "14/3/1448" - the Hijri date
new Intl.DateTimeFormat('ar-SA', { calendar: 'gregory' }).formatToParts(d); // ...spells "14/3/2026"
new Intl.DateTimeFormat('th-TH', { calendar: 'gregory' }).format(d);        // "27/8/2569" - the Buddhist year
new Intl.DateTimeFormat('fa-IR', { calendar: 'gregory' }).format(d);        // "1405/6/5"  - the Persian date

// 5.x - each of the six writes the Gregorian date its resolvedOptions() names
new Intl.DateTimeFormat('ar-SA', { calendar: 'gregory' }).format(d);        // "27/8/2026"
new Intl.DateTimeFormat('th-TH', { calendar: 'gregory' }).format(d);        // "27/8/2026"
new Intl.DateTimeFormat('fa-IR', { calendar: 'gregory' }).format(d);        // "2026/8/27"
```

`-u-ca-gregory` is the same case and moves with it. The locale defaults are unchanged in both what they
resolve to and what they write — `ar-SA` still reports `islamic-umalqura` and still prints `14/3/1448`,
`th-TH` still prints `2569` and `fa-IR` still prints `1405` — but the conversion behind them is now Jint's
own, the one that already served `new Intl.DateTimeFormat('en-US', { calendar: 'islamic-umalqura' })`.

**What could break:** a formatter whose locale is one of the fifteen .NET cultures with a non-Gregorian
default calendar — `ar-SA`, `th`/`th-TH`, and the Persian-calendar group `fa`, `fa-AF`, `fa-IR`, `ps`,
`ps-AF`, `ckb-IR`, `lrc`, `lrc-IR`, `mzn`, `mzn-IR`, `uz-Arab`, `uz-Arab-AF` — *and* which asks for a
different calendar than the locale's own. Those formatters wrote a date in the wrong calendar and now write it
in the requested one. Every other locale is untouched, its culture having carried a Gregorian calendar all
along.

### 4.61 `Intl.DateTimeFormat` writes the numbering system's digits, and leaves the pattern's punctuation alone ([#3468](https://github.com/sebastienros/jint/issues/3468))

[FormatDateTimePattern](https://tc39.es/ecma402/#sec-formatdatetimepattern) splits the pattern with
`PartitionPattern` and copies every `literal` through untouched; `[[NumberingSystem]]` reaches a *field's*
value only, through the `FormatNumeric` calls in the `"numeric"` and `"2-digit"` branches. Those values are
integers, so no date field carries a decimal separator to rewrite. Both lanes rewrote the whole assembled
string instead — `format` the result, `formatToParts` every part including the literals — so a locale whose
date pattern separates its fields with a full stop came out with the numbering system's decimal separator in
place of its own punctuation:

```js
const f = new Intl.DateTimeFormat('de-DE', { numberingSystem: 'arab', dateStyle: 'short', timeZone: 'UTC' });

// 4.16.x / earlier 5.0
f.format(new Date(Date.UTC(2026, 7, 27)));  // "٢٧٫٠٨٫٢٠٢٦"  - U+066B ARABIC DECIMAL SEPARATOR for de-DE's full stops

// 5.x
f.format(new Date(Date.UTC(2026, 7, 27)));  // "٢٧.٠٨.٢٠٢٦"
```

This is [4.46](#446-intl-writes-the-numbering-systems-digits-in-the-parts-lane-and-only-in-the-number) one
constructor over: the same "a literal is pattern text and only a number is a number" split, applied to
`Intl.DateTimeFormat`'s two lanes rather than `Intl.NumberFormat`'s.

The one separator this formatter writes *itself* is unchanged. `fractionalSecondDigits` is a component
option, so no CLDR pattern supplies the character between the second and its fraction — the component lane
assembles the pattern around it, and both lanes write the numbering system's decimal separator there, as the
parts lane always did:

```js
new Intl.DateTimeFormat('en-US', { numberingSystem: 'arab', minute: '2-digit', second: '2-digit',
                                   fractionalSecondDigits: 3, timeZone: 'UTC' })
  .format(new Date(Date.UTC(2026, 7, 27, 1, 2, 3, 456)));  // "٠٢:٠٣٫٤٥٦" - unchanged
```

**What could break:** nothing on a default engine. Every formatter that resolves `latn` writes exactly what
it wrote before, and `latn` is what an unconfigured engine resolves for every locale. Output moves only for a
formatter that asked for another numbering system, or for an embedder whose `ICldrProvider` gives a locale a
non-Latin default — and it moves towards the locale's own punctuation.

### 4.62 A `dateStyle` writes the locale's own date shape, and `formatToParts` splits that same pattern ([#3469](https://github.com/sebastienros/jint/issues/3469))

[FormatDateTime](https://tc39.es/ecma402/#sec-formatdatetime) is the concatenation of the very list
[FormatDateTimeToParts](https://tc39.es/ecma402/#sec-formatdatetimetoparts) walks, so `format` and
`formatToParts` are two views of one partition. The styled lane had two: `format` rendered the pattern
[DateTimeStyleFormat](https://tc39.es/ecma402/#sec-date-time-style-format) takes from the locale's own data,
while `formatToParts` rebuilt the date from a hard-coded American field order with hard-coded literals. Both
lanes now split the locale's pattern, and `format` is the concatenation of the parts.

```js
const d = new Date(Date.UTC(2026, 7, 27));
const de = new Intl.DateTimeFormat('de-DE', { timeZone: 'UTC', dateStyle: 'short' });

de.format(d);                                          // "27.08.2026" — unchanged
de.formatToParts(d).map(p => p.value).join('');
// 5.0: "8/27/26"    5.x: "27.08.2026"

new Intl.DateTimeFormat('ja-JP', { timeZone: 'UTC', dateStyle: 'full' }).formatToParts(d)
    .map(p => p.type + '=' + p.value);
// 5.0: weekday=木曜日, literal=", ", month=8月, literal=" ", day=27, literal=", ", year=2026
// 5.x: year=2026, literal=年, month=8, literal=月, day=27, literal=日, weekday=木曜日
```

Two consequences fall out of there being one pattern:

- **`dateStyle: 'medium'` is the abbreviated form it always claimed to be.** `format` wrote the long month
  name — `en-US` "August 27, 2026" — while `formatToParts` wrote the short one. Both now write CLDR's `MMM`:
  `"Aug 27, 2026"`.
- **A non-ISO calendar's styled output is the calendar's date.** `format` already delegated to the parts lane
  for any calendar that is not `iso8601` or `gregory`, so it was inheriting that lane's blindness to the
  calendar: `new Temporal.PlainDate(2024, 3, 26, 'islamic-tbla').toLocaleString('en-u-ca-islamic-tbla',
  { dateStyle: 'long' })` was `"March 26, 2024"` and is now `"9 17, 1445"`. Month *names* for a calendar .NET
  is not counting the date in still need an `Options.Intl.CldrProvider` that answers `GetMonthNames` for that
  calendar; without one the month is written as a number.

**What could break:** any test pinning the exact string a `dateStyle` produces outside `en-US`, and any test
pinning `formatToParts` output for a styled formatter. `timeStyle` is untouched, and a formatter built from
component options (`year`/`month`/`day`/…) is untouched. There is no option that restores the old shape:
the two lanes disagreeing was the defect.

### 4.63 A collapsed number range is collapsed in both lanes ([#3466](https://github.com/sebastienros/jint/issues/3466))

[FormatNumericRange](https://tc39.es/ecma402/#sec-formatnumericrange) is the concatenation of exactly the
parts [FormatNumericRangeToParts](https://tc39.es/ecma402/#sec-formatnumericrangetoparts) returns — both of
them [PartitionNumberRangePattern](https://tc39.es/ecma402/#sec-partitionnumberrangepattern), whose last step
is [CollapseNumberRange](https://tc39.es/ecma402/#sec-collapsenumberrange). The collapse being
implementation-defined does not let the two lanes disagree about it: it happens *inside* the partition both
of them read.

Jint implemented it outside, by rewriting `formatRange`'s two already-formatted endpoints. The parts lane had
no way to say that an endpoint's own parts had been elided, so it wrote both ends in full:

```js
const nf = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
// 4.16.x / earlier 5.0
nf.formatRange(-5, -1);                                      // "-$5.00–1.00"
nf.formatRangeToParts(-5, -1).map(p => p.value).join('');    // "-$5.00 – -$1.00"

const de = new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'USD' });
de.formatRange(1, 5);                                        // "1,00 – 5,00 $"
de.formatRangeToParts(1, 5).map(p => p.value).join('');      // "1,00 $ – 5,00 $"

// 5.x — one partition, read two ways
nf.formatRangeToParts(-5, -1).map(p => p.value).join('');    // "-$5.00–1.00"
de.formatRangeToParts(1, 5).map(p => p.value).join('');      // "1,00 – 5,00 $"
```

The collapse itself is unchanged, and so is every string `formatRange` returned: a prefix-currency locale
whose two ends share a sign *and* a symbol writes them once at the front and tightens the separator, a
suffix-currency locale whose two ends share a trailing symbol writes it once at the back, and a shared symbol
with no shared sign is not collapsed at all. Those are the three shapes test262's `formatRange/en-US.js` and
`formatRange/pt-PT.js` assert, and neither file asserts the parts — which is what let the two drift.

**What could break:** a caller reading `formatRangeToParts` for a **currency** range whose two endpoints
share affixes now gets the elided list rather than two full endpoints, and the shared `literal` separator is
whichever one `formatRange` writes for that range rather than always the spaced form. Every non-currency
range is unchanged, and so is every `formatRange` string. A part that survives still names its
`source` — `"startRange"`, `"endRange"` or `"shared"` — so code that groups by `source` keeps working; code
that assumed the end always repeats the start's currency does not.
## 5. New in v5

Everything in the table below is opt-in: nothing in it is installed unless the host asks for it, so
none of it changes an engine that does not.

| Area | Enable with | Reference |
| --- | --- | --- |
| WHATWG web APIs — `console`, timers, `URL`, encoding, streams, `fetch`, storage, `WebSocket`, `EventSource`, crypto | `options.UseWebApis(...)` | [Web APIs (opt-in)](../README.md#web-apis-opt-in) |
| Web Workers, on a thread you supply | `options.UseWebApis().UseWorkers(provider)` | [`new Worker()`](../README.md#new-worker-with-the-thread-supplied-by-you) |
| Node compatibility — the `process` shim, `node:` builtin modules | `options.UseNodeProcess()`, `options.UseNodeBuiltinModules()` | [Node compatibility (opt-in)](../README.md#node-compatibility-opt-in) |
| Script profiling | `options.Profiling.Enabled = true` | [Profiling scripts (opt-in)](../README.md#profiling-scripts-opt-in) |
| Statement-level code coverage | `options.Coverage.Enabled = true` | [Code coverage (opt-in)](../README.md#code-coverage-opt-in) |
| `NamedPropertyObject` — one base class for a host object projecting *named* properties, the string-keyed sibling of `ArrayLikeObject` | derive from it instead of overriding `GetOwnProperty` / `ProbeOwnProperty` / `GetOwnPropertyKeys` / `TryGetOwnPropertyValue` / `GetOwnProperties` by hand | [Projecting host data](../README.md#embedding-performance) |
| Source-generated CLR interop for annotated types | `[JsAccessible]` on the type, plus one `JsAccessibleRegistration.RegisterAll()` call | [§5.1](#51-source-generated-clr-interop) |
| The three shipped locale-data providers are extensible — one datum is one override | derive from `DefaultCldrProvider` / `DefaultTimeZoneProvider` / `DefaultCalendarProvider` and assign the instance to the matching `Options` property | [5.2](#52-changing-one-locale-datum-is-one-override) |
| Writable named projections, and named members on an `ArrayLikeObject` | `IsNameWritable` / `TrySetNamedValue` / `TryDeleteName`, and the same `NameCount` / `NameAt` / `TryGetNamedValue` triple on both classes | [§5.3](#53-host-objects-one-hook-set-for-named-properties-3338) |
| `HostFunction` — one base class for a host-defined callable, the function sibling of `ArrayLikeObject` and `NamedPropertyObject` | derive from it and override `Invoke` | [§5.5](#55-a-host-function-is-a-class-you-can-derive-3345) |
| Host-contract verification catches a value built for an engine another thread is using | `AppContext.SetSwitch("Jint.EnableHostContractVerification", true)` | [§5.6](#56-host-contract-verification-also-checks-thread-affinity-3332) |
| `LazyJsString` — one base class for a host string whose text is expensive to produce | `class Field : LazyJsString { public Field(int len) : base(len) {} protected override string Materialize() => … }` | [Lazy strings](../README.md#embedding-performance) |

The last row is the only one that replaces an existing spelling rather than adding a capability, so it is
worth saying what happens to the old one. A lazy host string used to be written by deriving from `JsString`
and passing **`null`** to a constructor whose parameter is typed `string` — a suppression against a contract
that existed only in that class's `<remarks>` — and then overriding `ToString()`, `Length` and the indexer
and memoizing by hand in each host. **That still compiles and still works**, and Jint's own sliced and
concatenated strings are still built on it; nothing about `JsString(string)` changed. `LazyJsString` is the
supported spelling from v5 on: the length goes to the constructor and one `Materialize()` method replaces
the other three overrides, the base class memoizes it and seals `ToString()` so the memoization cannot be
bypassed, a `null` result is refused with a message that names the type instead of surfacing as a
`NullReferenceException` somewhere else, and host-contract verification checks the declared length against
the text that is eventually produced. Overriding the indexer is still worth doing when the backing store can
answer one character without decoding the whole value; overriding `Length` is not, since the constructor
takes it.

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

| shape | diagnostic | why |
| --- | --- | --- |
| an overloaded method name — counting a base type's and an implemented interface's | `JINT032` | overload resolution is what the reflected path exists for |
| a method parameter not typed `JsValue` | `JINT033` | its reflected binding is a conversion chain steered by engine options; reproducing it in emitted code is where a generated accessor stops being equivalent |
| an optional, `params`, `ref`/`out` parameter, a generic method, a `ref` return, a `static` method | `JINT033` | same, or the lane is an instance lane |
| a property with a non-public or `init` accessor, or a `ref` return | `JINT034` | reflection writes those and emitted C# cannot, so half a member would be worse than none |
| a member typed by a pointer or a ref struct | `JINT035` | emitted C# cannot name or box it |
| an indexer | `JINT036` | it is probed ahead of the declared members, so every name it answers for resolves through it |
| an `abstract`, `static`, generic, record or nested-private type, and any value type | `JINT030` | never the runtime type of a receiver, or unreachable from emitted code — and a value type's instance member would be written through a boxed copy |
| an annotated type where none of the above leaves anything | `JINT031` | the annotation registers nothing |

**Every one of those is now reported**, at `Info`, against the declaration that caused it — so annotating a
type tells you which of its members you did *not* buy the no-reflection claim for. A `static` property or
field, a `const`, and anything non-public are declined *silently*, because the default
`ObjectWrapperReported*BindingFlags` do not report them to script either and nothing was lost.

Promote whichever of them your build cares about in `.editorconfig`; there is no attribute property for it,
because whether a fallback is tolerable is a property of the build rather than of one annotated type:

```ini
[*.cs]
dotnet_diagnostic.JINT033.severity = error   # every method of mine must take the generated lane
dotnet_diagnostic.JINT032.severity = none    # ... but my overloads may stay reflected
```

**What still contains it.** `TypeResolver.MemberFilter`, `MemberNameCreator`, `MemberNameComparer` and
`Options.Interop.ObjectWrapperReported*BindingFlags` reach a generated member exactly as they reach a
reflected one: a member your filter hides stays hidden, a member your name creator renames answers only to
the new name, and flags that no longer report a member hide it from both. Installing one of them costs an
annotated type nothing but one reflected member lookup per member, after which reads, writes and calls run
through the generated code as before.

One shape stays reflected whatever you configure: an annotated type that also declares an **indexer**. An
indexer is probed before the member itself, and the generated accessors carry no such probe, so the whole
type keeps the reflection path — which is what makes its names resolve in the order an un-annotated type's
do.

**Nothing to install.** The generator ships inside the `Jint` package, under `analyzers/dotnet/cs`, so the
`PackageReference` you already have is the whole setup — annotate a type and `RegisterAll()` appears in your
assembly's root namespace. It needs a compiler at least as new as the **.NET 8 SDK**; on anything older the
compiler declines to load the analyzer (`CS9057`) and your call to `RegisterAll()` then fails to compile,
which is at least a failure you can see. It does nothing to a project that annotates nothing, and
`<PackageReference Include="Jint" ExcludeAssets="analyzers" />` keeps it out of your build entirely.

### 5.2 Changing one locale datum is one override ([#3335](https://github.com/sebastienros/jint/pull/3335))

`DefaultCldrProvider`, `DefaultTimeZoneProvider` and `DefaultCalendarProvider` were `sealed`, so a host that
disagreed with one currency name, one time zone alias or one calendar had to implement the whole interface —
19, 9 and 4 members — and hand-delegate every member it did not care about to the singleton. Miss one and the
engine silently loses a datum it used to have.

All three are now unsealed with `virtual` members, matching `DefaultTimeSystem`, which has always had that
shape. Nothing about an unconfigured engine changed: the `Options` properties still default to the same
`Instance` singletons, and `Options.Temporal.CalendarProvider` still recognizes that singleton by identity
and answers inline rather than going through the interface.

```c#
// 5.x — the other eighteen members are inherited
sealed class MyCldr : DefaultCldrProvider
{
    public override string? GetCurrencyDisplayName(string locale, string code)
        => code == "EUR" ? "Space Credits" : base.GetCurrencyDisplayName(locale, code);
}

var engine = new Engine(options => options.Intl.CldrProvider = new MyCldr());
```

`ICldrProvider` is now nineteen members and every one of them has a caller, so whatever a derived class
answers is what `Intl` shows — see [4.22](#422-intl-reads-the-cldr-provider-for-currency-symbols-and-week-info)
and [4.29](#429-intl-reads-the-cldr-provider-for-date-names-and-numbering-system-digits) for the two changes
that closed the gap, and section [2](#2-removed-api) for the two members that went instead of being wired.
On the calendar side, *correcting* a calendar Jint already knows is one override, while *adding* one it does
not know is three — `GetSupportedCalendars` and both conversions — per
[4.30](#430-a-calendar-icalendarprovider-claims-is-a-calendar-temporal-accepts).

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
empty defaults ([§3.10](#310-arraylikeobject-seals-the-three-members-a-named-getter-used-to-override-3338)).

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

### 5.4 Every error constructor is reachable from host code ([#3337](https://github.com/sebastienros/jint/pull/3337))

`Engine.Intrinsics` exposed `Error` and `TypeError` and kept the other five `internal`, so a host
function could not raise the error the specification would raise — a `RangeError` for an
out-of-range argument being the common case. All seven are now `public`; nothing else changed and
nothing was removed.

```c#
throw new JavaScriptException(engine.Intrinsics.RangeError, $"index {index} is out of range");
```

Additive, so no migration is required. If you reached one through the global instead, that keeps
working and can now be written directly:

```c#
// before
var rangeError = (ErrorConstructor) engine.GetValue("RangeError");
// after
var rangeError = engine.Intrinsics.RangeError;
```

The CLR name of `%URIError%` is `Intrinsics.UriError`; script still sees `URIError`. See [Raising an
error from host code](../README.md#raising-an-error-from-host-code).
### 5.5 A host function is a class you can derive ([#3345](https://github.com/sebastienros/jint/pull/3345))

Until v5 there was exactly one host-writable *plain* callable: `ClrFunction`, which is `sealed` and takes
its body as a delegate. (`Constructor` was derivable, but its `Call` throws *"requires 'new'"* unless the
subclass overrides it too, so it is the wrong base for something that is not a constructor.) `Function`
looked like the other option — it is `public abstract` with a `protected abstract Call` — but its only
accessible constructor asked for a `Realm`, and nothing public returns one, so nobody could derive it
from outside Jint ([§3.10](#312-the-five-members-that-took-a-realm-take-an-engine-or-are-gone-3345)).
A host whose callable had state, or was one member of a family sharing a base, had to close over that state
in a lambda.

`HostFunction` is that class. It follows the shape `ArrayLikeObject` and `NamedPropertyObject` established:
one abstract member, everything derived from it sealed.

```c#
public sealed class Translate : HostFunction
{
    private readonly ILocalizer _localizer;

    public Translate(Engine engine, ILocalizer localizer) : base(engine, "t", length: 1)
        => _localizer = localizer;

    protected override JsValue Invoke(JsValue thisObject, JsValue[] arguments)
        => new JsString(_localizer[TypeConverter.ToString(arguments.At(0))]);
}

engine.SetValue("t", new Translate(engine, localizer));
```

Script sees an ordinary built-in function: `typeof` is `"function"`, it inherits from `Function.prototype`
(so `call`, `apply` and `bind` work and `instanceof Function` is `true`), and it carries own `name` and
`length` properties with the attributes
[§10.3](https://tc39.es/ecma262/#sec-built-in-function-objects) gives a built-in —
`{ writable: false, enumerable: false, configurable: true }`. (`ClrFunction` makes `length`
non-configurable by default and takes a `lengthFlags` argument to change it; the new class simply follows
the specification, because it has no compatibility to keep.)

Four things decided deliberately:

- **`Call` is sealed.** The body is `Invoke`, and the base owns what surrounds it — today, routing a CLR
  exception that escapes the body through `Options.Interop.ExceptionHandler` exactly as `ClrFunction` does,
  so which of the two spellings a host chose is not observable from script. A `Call` a subclass could
  override would make that a per-subclass responsibility.
- **It is not a constructor.** `new hostFunction()` raises a `TypeError`, which is what the specification
  says for a built-in with no `[[Construct]]`. A host that wants `new` derives from `Constructor` instead,
  which is the same deal for the other half: supply `Construct`, the base supplies the rest. (`Constructor`
  had a defect of its own, fixed here — see
  [§4.18](#418-a-host-constructor-is-now-a-function-3345).)
- **The `arguments` array is borrowed.** The engine pools it and may hand the same instance to the next
  call, so copy anything that must outlive the call. Reading it during the call is always safe.
- **It belongs to the engine's principal realm**, the rule `ClrFunction` already follows, so a host
  function built after a `ShadowRealm` exists still inherits from the `Function.prototype` the surrounding
  script can reach.

Reach for `ClrFunction` when the body is a lambda, and for `HostFunction` when the callable has state,
wants a name a stack trace can show, or belongs to a family that shares a base.

### 5.6 Host-contract verification also checks thread affinity ([#3332](https://github.com/sebastienros/jint/issues/3332))

Jint guards *operations* against concurrent use and does not guard *value construction*. `Engine.Evaluate`,
`JsValue.FromObject`, `JsonSerializer.Serialize` and `JsonParser.Parse` all reject a second thread while the
engine is in use; `JsObject.Create`, `JsObject.CreateFromEntries` and the `JsArray` constructors — the ones
README's **Projecting host data** recommends in preference to subclassing `ObjectInstance` — build the value
anyway. They are per-object APIs on a bulk path, so an always-on claim there would be paid by every host that
never got this wrong.

That line has not moved, and construction stays as cheap as it was. What is new is that the violation is now
*visible*: with host-contract verification on, an engine-affine object built on one thread while a different
thread is inside that engine throws `InvalidOperationException` naming the type, at the point of construction:

```csharp
// once, before the first use of any Jint type — a [ModuleInitializer] in the test assembly is the usual place
AppContext.SetSwitch("Jint.EnableHostContractVerification", true);
```

The check is the narrowest question with no false positives — *another thread owns this engine right now, and
it is not me*. An **idle** engine answers no, so a host preparing values between turns, or building them
before handing an engine out of a pool, is never flagged. It costs a production build nothing: the gate is a
`static readonly bool` read once at type initialization, so the JIT folds the check and the branch out of a
process that never set the switch.

This is worth turning on precisely because the failure it catches is not local. An object built for an engine
another thread is using does not fail where it was built; it fails later, somewhere else, as a torn shape
table or a lost property, and the host sees a nondeterministic script result.

### 5.7 A host can read back the configuration of an engine it was handed ([#3360](https://github.com/sebastienros/jint/issues/3360))

`Engine.Options` was internal, so a component handed nothing but an `Engine` had no supported way to answer
"what is this engine allowed to do". It is now a public get-only property returning the frozen instance.

```csharp
static void Audit(Engine engine)
{
    if (engine.Options.Interop.Enabled && engine.Options.Interop.AllowWrite)
    {
        throw new InvalidOperationException("this engine may write to host objects");
    }
}
```

What it answers is what the engine actually runs under, which is not always what the host declared:

| built by | what `engine.Options` is |
| --- | --- |
| `new Engine(options)` | that very instance — reference-equal to the object the host still holds |
| `new Engine(options => …)` | a fresh instance per engine, carrying what the callback wrote |
| `new Engine()` | one process-wide instance of the defaults, shared by every engine built that way |
| `options.ForUntrustedCode(…)` | the engine's **private hardened copy**, so the grants the profile revoked read back revoked while the host's own object still reads back as the host wrote it |

`Engine.WebApi.Enable` replaces the instance with a copy owning its own web-API subtree
([§4.21](#421-webapienables-callback-configures-one-engine-not-every-engine-3359)), so read the property again
after that call rather than caching the reference across one.

**It is a read, not a second configuration channel.** The options are frozen once an engine has read them
([§4.16](#416-options-is-configuration-until-an-engine-reads-it-and-frozen-afterwards-3327)), so every setter
and every registry reached through this property throws — including the two that used not to
([§4.41](#441-two-holes-in-the-freeze-closed-3360)). What the freeze covers is the settings, not the objects
they name: a `TypeResolver`, a `CultureInfo`, a module loader, a provider or an `HttpClient` read back here is
still the host's own mutable object, exactly as it was before it was handed to Jint.

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
| a CLR array as a live view (`ArrayConversionMode.LiveView`) - reads, element writes, and the `TypeError` a resize attempt owes script | works |
| delegates in both directions (`Func<int, int>` to and from script) | works |
| extension methods, `TypeReference` construction from script | works |
| generic host methods with a **reference-type** argument | works |
| an engine with `options.Interop.Enabled = false` | works, and needs none of this section |

### 6.2 The one thing that does not: a generic instantiation over a value type

Native AOT shares one compiled body across every reference-type argument, so `Identity<string>` works.
A value-type argument needs a specific instantiation the compiler had to have seen, and eight places in
Jint build one at run time. Four have a non-generic answer to fall back on and take it; the other four
throw `NotSupportedException`, because nothing non-generic satisfies what was asked for and a wrong
answer is worse than a diagnosable throw:

| what throws | what to know |
| --- | --- |
| a host method taking `Func<double, Task<double>>` — any `Task<T>` / `ValueTask<T>` over a value type — called with a JS function | nothing but `Task.FromResult<double>` produces a `Task<double>` |
| a generic host method called with a number: `host.identity(7)` | `host.identity('hi')` is fine; declining instead would report *no matching overload* for a method that plainly exists |
| a host method taking `IEnumerable<long>`, `IList<short>`, `Collection<short>`, … called with a JS array | it works when the *closed* type appears in one of your own signatures, because that is what makes ILC compile it: a `List<int>` parameter is fine, an `IEnumerable<int>` parameter is not |
| a closed generic type the **script** names: `importNamespace('MyApp').Box(System.Int16)` | the type resolves; the failure lands on construction |

**Only you can close these, and you can close all four.** The type argument comes from your members and
your scripts, so no signature in Jint predicts it, and rooting a guessed set of value types would cost
every AOT consumer binary size for instantiations they never use. Make each instantiation reachable from
code ILC compiles, and Jint's run-time lookup then finds it:

```csharp
// Never called. It only has to be compiled, so put it in a method your program calls or - simpler -
// anywhere in the assembly you already root with TrimmerRootAssembly (§6.4).
internal static void RootAotInstantiations()
{
    _ = new MyHost().Identity(0d);   // one line per value type a script passes to a generic host method
    _ = new List<long>();            // for an IEnumerable<long> / IList<long> / Collection<long> parameter
    _ = new Box<short>(default);     // for a closed generic type a script names

    // Task.FromResult<double>, for Func<double, Task<double>>. A plain `Task.FromResult(0d)` is NOT
    // enough: it compiles the body, but Jint reaches the method through reflection and `Task` lives in an
    // assembly you have not rooted, so there is nothing to invoke. Naming it the way Jint does works,
    // because both tokens are constants and ILC folds the expression at compile time.
    _ = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(double));
}
```

The asymmetry is the rule to carry away: for **your own** types a compiled reference is enough, because
`TrimmerRootAssembly` keeps their metadata; for a **framework** type reached by reflection, root it the
way it will be looked up. Or avoid the shape entirely — give the host method a non-generic overload, take
`Task<object>` and box, or declare the parameter as the closed `List<T>` rather than as an interface
over it.

**Four shapes that used to throw here degrade instead**
([#3299](https://github.com/sebastienros/jint/issues/3299)): `int[]` under
`ArrayConversionMode.LiveView`, `List<int>`, `IReadOnlyList<double>` and an `IEnumerable<int>` under
`EnumerableConversionMode.Snapshot` fall back to an untyped wrapper and answer correctly — element
writes coerce to the element type, and a resize attempt on a fixed-size target raises the same
`TypeError` the typed wrapper raises rather than the collection's own `NotSupportedException`. The only
loss is that a collection reached through the last-resort fallback is not array-*like*:
`IReadOnlyList<double>` keeps `ro[0]` but loses `ro.length` and the `Array.prototype` generics unless
the type also implements `IList`.

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
[§6.6](#66-what-handing-jint-a-type-preserves-3396) says what that one shared annotation now covers.

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

**Expect some of Jint's own diagnostics in your build.** Jint's `NoWarn` is a property of Jint's
compilation and reaches nothing downstream - ILC re-derives every diagnostic over the closed program -
so an AOT publish reports Jint's remaining trim-analysis warnings against Jint's files in *your* build.
There are 67 of them, down from 113 ([#3305](https://github.com/sebastienros/jint/issues/3305)), and
they now divide into two kinds:

* **Nine are the gaps in [§6.2](#62-the-one-thing-that-does-not-a-generic-instantiation-over-a-value-type)**
  - six `IL3050` and their three `IL2060` twins, one per site that builds a generic instantiation over a
  type only known at run time. These are true, they are the shapes the native run confirms do not work,
  and they are not suppressed for exactly that reason. If your script never reaches one, the diagnostic
  costs you nothing; if it does, §6.2 says what to write instead.
* **The rest are dataflow**, and almost all root at one value: the runtime type of a host object,
  obtained through `object.GetType()`. No annotation can describe it, which is why the requirement is
  stated at the entry points instead ([§6.3](#63-apis-that-now-warn)) and why the answer is the rooting
  above rather than anything in the diagnostic.

Neither kind is actionable at the file it names, so set
`<IlcTreatWarningsAsErrors>false</IlcTreatWarningsAsErrors>` or `NoWarn` the codes, and treat the run as
the evidence rather than the warning count. What *is* actionable is any diagnostic pointing at **your**
files: those are the ones §6.3 and this section are about.

### 6.5 The AOT-safe subset

An engine that never enables interop needs none of the above:

```csharp
var engine = new Engine(options => options.Interop.Enabled = false);
```

That is the floor Jint is prepared to promise. Everything above it is a matter of rooting what you
expose.

### 6.6 What handing Jint a type preserves ([#3396](https://github.com/sebastienros/jint/issues/3396))

The five members [§6.3](#63-apis-that-now-warn) calls the AOT-friendly way to expose a type —
`Engine.SetValue(string, Type)`, `Engine.SetValue<T>`, the two `ShadowRealm` mirrors,
`TypeReference.CreateTypeReference` and `ModuleBuilder.ExportType` — share one
`[DynamicallyAccessedMembers]` set, because they are one promise. It has gained one entry, **the type's
interfaces**, alongside its public constructors, properties, methods, fields and events.

Jint walks `Type.GetInterfaces()` to find a member a class implements only through an interface, and to
decide whether a target is array-like, dictionary-shaped or enumerable. A trimmer may remove an
interface implementation nothing else uses, and Jint would then report a member that is there as
`undefined` — the failure mode [§6.4](#64-what-you-owe-your-own-project) warns about, with no
diagnostic anywhere. Nothing in your code changes; the annotation is what keeps the interface.

**It keeps the interface, not the interface's members**, and that distinction is measured rather than
assumed — see [§6.7](#67-the-annotation-contract-is-measured-3479), which also says which half of this
paragraph a published binary actually confirms.

`TypeReference.ReferenceType` carries the same set now, so a type handed to
`CreateTypeReference` is still annotated when the reference constructs it — through `Activator` and
through the resolved constructor set alike. It was a plain `Type` before, and the promise the caller
made stopped there.

**Public nested types are deliberately not in the set**, and the reason is worth knowing before you ask
for them. `Env.SpecialFolder.MyDocuments` works: a nested type is a member a script can name, and the
native run pins it. But adding `PublicNestedTypes` to the shared set marks every nested type with
`All`, and every nested **enum** then raises an `IL3050` in **your** build through the inherited
`[RequiresDynamicCode] Enum.GetValues(Type)` — two of them for a single
`engine.SetValue("Env", typeof(Environment))`. In exchange it preserves nothing under Native AOT: ILC
already keeps the nested types of a type whose metadata it emits, measured on the published binary for
six candidates with the annotation present and absent. So the requirement is stated inside Jint where
the lookup happens, and the two `IL2072` it raises against `TypeReference.cs` are two of the
diagnostics §6.4 counts. This is the `Delegate` trap in §6.4 again, and the choice was not to add a
second one.

### 6.7 The annotation contract is measured ([#3479](https://github.com/sebastienros/jint/issues/3479))

Everything above [§6.3](#63-apis-that-now-warn) says about `SetValue<T>` preserving what Jint reflects
over used to be a claim no run checked. `Jint.AotExample` roots both assemblies it publishes, so every
host type its probes registered was preserved by the root and no probe could tell the annotation from it.
There is now a second project, `Jint.AotExample.UnrootedHost`, that the AOT leg publishes and deliberately
does **not** root, and four probes over it. Nothing in your code changes; what changes is that the two
sentences below are now the output of a native binary rather than prose.

**What the annotation does buy you.** Registering an unrooted host type through `SetValue<T>` — the
overload C# picks whenever the static type at the call site is the host type — preserves its public
property, field and method, and preserves enough of an `IReadOnlyList<T>` implementation for `.length` and
the `Array.prototype` generics to work. Delete the `[DynamicallyAccessedMembers]` from `SetValue<T>` and
those probes fail with `undefined` and `0`, along with `Dictionary<string, object>` and
`Dictionary<string, int>`, which are framework types the example does not root either.

**What it does not, and there are two.** Registering the same object through `SetValue(string, object?)`
preserves nothing: the member reads `undefined`, script sees a property that is not there, and no
diagnostic anywhere reports it. That is the silent wrong answer §6.4 is about, and it is now pinned as an
executable entry rather than described. And a member reachable **only through an explicit interface
implementation** reads `undefined` too, with the annotation present: `Interfaces` asks for the implemented
interfaces, not for their members, so the walk that would find it has no metadata to read. Both close the
same way, and it is the way §6.4 already gives:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="YourAssembly" />
</ItemGroup>
```

If you cannot root the whole assembly, an explicit interface member is reachable again as soon as anything
in your program names it with a constant token — `typeof(IYourInterface).GetProperties()` is enough,
because that *is* a request to preserve them.

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

**Numbering is assigned at merge, not at authoring.** Several pull requests are usually in flight at
once, each picking what looked like the next free subsection number when it was written, so the
number is stale by the time it lands — and because two of them edit different parts of this file,
git merges them cleanly and the duplicate only shows up in the rendered document. Whoever merges
second renumbers, and repoints any `[§x.y](#xy-...)` link to the section. A collision that reaches
`main` is a docs fix, not a rewrite of the section.

**A rebase preserves a section's position, not its rank.** Renumbering the heading is only half of
it: git replays the section where it sat, so once a lower-numbered section lands on `main` underneath
a branch, that branch's entry ends up *above* it — distinct numbers, no conflict to resolve, and a
document whose numbers run backwards. This happened during the v5 campaign to a branch that rebased
with no conflict at all, which is exactly the case nothing draws attention to. So after **every**
rebase, not only after one that conflicted, move the section block to the end of its chapter and run
`dotnet test -c Release --filter "FullyQualifiedName~MigrationGuideTests"`; that is what
`SectionNumbersAscendInDocumentOrder` is there to catch.
