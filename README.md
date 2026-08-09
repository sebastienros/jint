[![Build](https://github.com/sebastienros/jint/actions/workflows/build.yml/badge.svg)](https://github.com/sebastienros/jint/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Jint.svg)](https://www.nuget.org/packages/Jint)
[![NuGet](https://img.shields.io/nuget/vpre/Jint.svg)](https://www.nuget.org/packages/Jint)
[![MyGet](https://img.shields.io/myget/jint/vpre/jint.svg?label=MyGet)](https://www.myget.org/feed/jint/package/nuget/Jint)
[![Join the chat at https://gitter.im/sebastienros/jint](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/sebastienros/jint)

# Jint

Jint is a __Javascript interpreter__ for .NET which can run on __any modern .NET platform__ as it supports .NET Standard 2.0 and .NET 4.6.2 targets (and later).

## Use cases and users

- Run JavaScript inside your .NET application in a safe sand-boxed environment
- Expose native .NET objects and functions to your JavaScript code (get database query results as JSON, call .NET methods, etc.)
- Support scripting in your .NET application, allowing users to customize your application using JavaScript (like Unity games) 

Some users of Jint include 
[RavenDB](https://github.com/ravendb/ravendb), 
[EventStore](https://github.com/EventStore/EventStore), 
[OrchardCore](https://github.com/OrchardCMS/OrchardCore), 
[ELSA Workflows](https://github.com/elsa-workflows/elsa-core),
[docfx](https://github.com/dotnet/docfx), 
[JavaScript Engine Switcher](https://github.com/Taritsyn/JavaScriptEngineSwitcher),
and many more.

## Supported features

#### ECMAScript 2015 (ES6)

- ✔ ArrayBuffer
- ✔ Arrow function expression
- ✔ Binary and octal literals
- ✔ Class support
- ✔ DataView
- ✔ Destructuring
- ✔ Default, rest and spread
- ✔ Enhanced object literals
- ✔ `for...of`
- ✔ Generators
- ✔ Template strings
- ✔ Lexical scoping of variables (let and const)
- ✔ Map and Set
- ✔ Modules and module loaders
- ✔ Promises (Experimental, API is unstable)
- ✔ Reflect
- ✔ Proxies
- ✔ Symbols
- ❌ Tail calls
- ✔ Typed arrays
- ✔ Unicode
- ✔ Weakmap and Weakset

#### ECMAScript 2016

- ✔ `Array.prototype.includes`
- ✔ `await`, `async`
- ✔ Block-scoping of variables and functions
- ✔ Exponentiation operator `**`
- ✔ Destructuring patterns (of variables)

####  ECMAScript 2017

- ✔ `Object.values`, `Object.entries` and `Object.getOwnPropertyDescriptors`
- ✔ Shared memory and atomics

#### ECMAScript 2018

- ✔ Asynchronous iteration
- ✔ `Promise.prototype.finally`
- ✔ RegExp named capture groups
- ✔ Rest/spread operators for object literals (`...identifier`)
- ✔ SharedArrayBuffer

#### ECMAScript 2019

- ✔ `Array.prototype.flat`, `Array.prototype.flatMap`
- ✔ `String.prototype.trimStart`, `String.prototype.trimEnd`
- ✔ `Object.fromEntries`
- ✔ `Symbol.description`
- ✔ Optional catch binding

#### ECMAScript 2020

- ✔ `BigInt`
- ✔ `export * as ns from`
- ✔ `for-in` enhancements
- ✔ `globalThis` object
- ✔ `import`
- ✔ `import.meta`
- ✔ Nullish coalescing operator (`??`)
- ✔ Optional chaining
- ✔ `Promise.allSettled`
- ✔ `String.prototype.matchAll`

#### ECMAScript 2021

- ✔ Logical Assignment Operators (`&&=` `||=` `??=`)
- ✔ Numeric Separators (`1_000`)
- ✔ `AggregateError`
- ✔ `Promise.any` 
- ✔ `String.prototype.replaceAll`
- ✔ `WeakRef` 
- ✔ `FinalizationRegistry`

#### ECMAScript 2022

- ✔ Class Fields
- ✔ RegExp Match Indices
- ✔ Top-level await
- ✔ Ergonomic brand checks for Private Fields
- ✔ `.at()`
- ✔ Accessible `Object.prototype.hasOwnProperty` (`Object.hasOwn`)
- ✔ Class Static Block
- ✔ Error Cause

#### ECMAScript 2023

- ✔ Array find from last
- ✔ Change Array by copy
- ✔ Hashbang Grammar
- ✔ Symbols as WeakMap keys

#### ECMAScript 2024

- ✔ ArrayBuffer enhancements - `ArrayBuffer.prototype.resize` and `ArrayBuffer.prototype.transfer`
- ✔ `Atomics.waitAsync` 
- ✔ Ensuring that strings are well-formed - `String.prototype.ensureWellFormed` and `String.prototype.isWellFormed`
- ✔ Grouping synchronous iterables - `Object.groupBy` and `Map.groupBy`
- ✔ `Promise.withResolvers`
- ✔ Regular expression flag `/v`

#### ECMAScript 2025

- ✔ 16-bit floating point numbers (float16), Requires NET 8 or higher, `Float16Array`, `Math.f16round()`
- ✔ Array.fromAsync
- ✔ Import attributes
- ✔ Iterator helper methods 
- ✔ JSON modules
- ✔ `Promise.try`
- ✔ `RegExp.escape()`
- ✔ Regular expression pattern modifiers (inline flags)
- ✔ Duplicate named capture groups
- ✔ Set methods (`intersection`, `union`, `difference`, `symmetricDifference`, `isSubsetOf`, `isSupersetOf`, `isDisjointFrom`)

#### ECMAScript proposals (no version yet)

- ✔ Await Dictionary (`Promise.allKeyed`, `Promise.allSettledKeyed`)
- ✔ Decorators (`@decorator` syntax for classes, methods, fields, and accessors)
- ✔ `Error.isError`
- ✔ `Error.prototype.stack` accessor (error-stack-accessor)
- ✔ Explicit Resource Management (`using` and `await using`)
- ✔ Immutable Arraybuffers
- ✔ Import Bytes (`import x from './file' with { type: 'bytes' }`)
- ✔ Iterator Chunking (`Iterator.prototype.chunks`, `Iterator.prototype.windows`)
- ✔ Iterator Includes (`Iterator.prototype.includes`)
- ✔ Iterator Join (`Iterator.prototype.join`)
- ✔ Iterator Sequencing
- ✔ Joint Iteration
- ✔ JSON.parse source text access
- ✔ `Math.sumPrecise`
- ✔ `ShadowRealm`
- ✔ `Temporal`
- ✔ `Uint8Array` to/from base64
- ✔ `Upsert`

#### Other

- Further refined .NET CLR interop capabilities
- Constraints for execution (recursion, memory usage, duration)


## Performance

- Because Jint neither generates any .NET bytecode nor uses the DLR it runs relatively small scripts really fast
- If you repeatedly run the same script, you should prepare it for execution using `Engine.PrepareScript` or `Engine.PrepareModule`, cache the returned `Prepared<...>` object and feed it to Jint instead of the content string
- You should prefer running engine in strict mode, it improves performance

You can check out [the engine comparison results](Jint.Benchmark), bear in mind that every use case is different and benchmarks might not reflect your real-world usage.

## Embedding performance

Notes for hosts that project their own objects into script, pool engines, or bound execution. Each of these
is a cost model rather than a rule; the XML documentation on the named APIs has the detail.

**Projecting host data.** Subclassing `ObjectInstance` is the most expensive way to expose data. Such a
receiver gets no own-property inline caching — every own read reaches your `GetOwnProperty` and allocates the
`PropertyDescriptor` it returns. Cheaper options, in order of preference:

- For fixed-shape records, do not subclass at all: `JsObject.Create(engine, layout, values)` and
  `JsObject.CreateFromEntries` build straight into the hidden-class representation, so every object sharing a
  `JsObjectLayout` shares one hidden class and a script reading a batch of them keeps a monomorphic inline
  cache. A record with expensive members most items never have read — a body that must be parsed, a field that
  must be decoded — declares them with `JsObjectLayout.CreateBuilder().AddLazy(name, factory)` and passes the
  raw payload as the `lazySlotState` argument of `JsObject.Create`: the factory runs on the first read that
  observes that member's value and the result is memoized on the object, while enumerating keys, `in` and
  `hasOwnProperty` never run it. The object stays a hidden-class object throughout.
- For CLR objects, `engine.SetValue(name, obj)` wraps them in `ObjectWrapper`, whose member resolution and
  compiled accessors are cached process-wide on the `TypeResolver`.
- For the *prototypes* those objects sit behind, declare the members once per process with `JsObjectShape` and
  create one object per engine with `Instantiate`: members materialize only when a script touches them, and
  because the engine stores and versions the whole member set itself, a shaped prototype can serve the
  prototype-method inline cache — which an `ObjectInstance` subclass used as a prototype can never do.
- For a **live indexed collection** — a DOM `NodeList`, a result window, any list computed on demand — derive
  from `ArrayLikeObject` rather than assembling the property model yourself. You implement two members,
  `uint Length` and `bool TryGetIndex(uint index, out JsValue value)`; the base class derives everything else
  (index and `length` descriptors, enumeration order, the existence and value hooks, WebIDL-shaped `delete` /
  `defineProperty` refusals) and the engine keys two lanes on the type, so `list[i]`, `Array.prototype`
  generics, `for-of` / spread / `Array.from` / destructuring and `JSON.stringify` each cost one
  `TryGetIndex` per element with no descriptor and no key allocation. It is array-*like*, not an array:
  `Array.isArray` stays `false` by design, the same answer a browser gives for a `NodeList`. If your backing
  store can test containment more cheaply than it can produce an element, also override
  `protected virtual bool HasIndex(uint index)`, and `in` / `hasOwnProperty` / `Object.keys` / `delete` stop
  projecting elements they only ever discard. Reach for it when the collection is live; when it is a snapshot,
  copying into a `JsArray` once is cheaper still — and give it a `JsObjectShape` prototype, per the bullet
  above, for the collection's own methods.
- If you must subclass, override `TryGetOwnPropertyValue` so an own read hands the value over with no
  descriptor at all, and `ProbeOwnProperty` so existence and enumerability questions (`in`, `Object.keys`,
  spread, `JSON.stringify`) are answered without materializing one either. Both carry an obligation to agree
  with `GetOwnProperty`, and neither is re-verified on the hot path — a `ProbeOwnProperty` that wrongly reports
  a key as absent drops it from every enumeration, silently. Run your integration suite once with
  **host-contract verification** on and every such disagreement throws instead, naming the type, the key and
  both answers:

  ```csharp
  // before the first use of any Jint type — the flag is read once, at type initialization
  AppContext.SetSwitch("Jint.EnableHostContractVerification", true);
  ```

  It also checks a declared `PropertyAccessSemantics.Ordinary`, an `ArrayLikeObject`'s `HasIndex`, and an
  `IObjectConverter` registered with `AddObjectConverter(converter, handledTypes)` converting a type it did not
  declare. Turn it on in a test or staging host, never in production: the checks deliberately redo the work the
  hooks exist to avoid. A Debug build of Jint has them on already and needs no switch.

**Lazy values.** `PropertyFlag.CustomJsValue` is the supported hook for a property whose value is *computed on
every read*: a `PropertyDescriptor` subclass overriding `CustomValue` keeps working under the read inline
caches, because every caching lane re-reads the flag on each hit and caches the descriptor reference rather
than a value snapshot. When the value is lazy only *once*, use `PropertyDescriptor.CreateLazy(state, factory)`
instead — it memoizes the produced value and then stops being custom-valued, which readmits the property to the
member-write fast path and the global-identifier cache that a permanently custom-valued descriptor is declined
by; store it wherever you store descriptors (`SetOwnProperty` or `GetOwnProperty` on a host subclass,
`FastSetProperty`, a hand-rolled global). It is the descriptor-shaped member of the same family as
`JsObjectLayout.AddLazy` (records) and `JsObjectShape` (prototypes), and it does not exempt you from the rule
above them: storing any raw descriptor under a string key still moves a shape-mode object to the dictionary
representation. For a whole global that may never be touched, `Options.AddLazyGlobal` defers building
the value until script reads the name, and `engine.Advanced.AddLazyGlobal` does the same on an engine that
already exists — which is what you need when the value comes from the request you are about to serve rather
than from process-wide configuration. Both install the property eagerly, so `in`, `hasOwnProperty` and
`Object.keys(globalThis)` see the name without building anything; only reading the value runs the factory,
once. The per-engine overload receives its engine, so unlike an `Options`-registered factory it may capture
engine-affine state.

**Sparse data.** Hosts that read deep chains off optional data — `input.Address.City.length`, where any link
may be absent — usually install an `IReferenceResolver` so a nullish base yields a value instead of throwing.
Register `NullPropagatingReferenceResolver.Instance` rather than writing that class yourself: the engine
recognizes the singleton and serves the propagation inline, with no interface call and no pooled `Reference`
per nullish read, which an equivalent hand-written resolver cannot get. Pass
`ReferenceResolverInterests.NullishPropertyBase` alongside it so every unrelated read lane stays armed. The
boundary is that *reads* propagate: a call on a nullish base still throws, and a host that needs callable
substitution or unresolvable-identifier handling writes its own resolver and forgoes the inline lane.

**Prepared scripts and engine reuse.** `Engine.PrepareScript` / `PrepareModule` return an object that is
reusable *and* thread-safe: prepare once at startup and feed the same `Prepared<T>` to as many engines, on as
many threads, as you like. The engine's own per-node caches are a separate matter — they are engine-owned and
engage only on the **second** evaluation of a given script on a given engine, so a host that builds a fresh
engine per operation never reaches them by design. Note the mirror image if you pool engines instead: a
warmed call site holds a reference to the last receiver it served until it caches a different one, so pooled
engines can keep host objects alive between runs.

**Registering only what a script uses.** When the ambient API is large and scripts touch little of it,
prepare with `ScriptPreparationOptions.CollectReferencedGlobals` and read `Prepared<T>.ReferencedGlobals`:
the free identifiers the program actually references, resolved per binding site, as an immutable set you can
intersect with your registry — including the CLR-side context you would otherwise build speculatively, which
lazy globals cannot defer. Honor `HasDirectEvalCall`: a program with direct `eval` can reference anything,
so install everything for those.

**Reusing a configured engine.** If you build a fresh engine per evaluation only because you need a clean
global, `engine.Advanced.CaptureGlobalSnapshot()` and `RestoreGlobalSnapshot(snapshot)` are the cheaper route:
capture once after your `SetValue` calls and module setup, then restore between evaluations. Restore reverts
the global object's own properties, its prototype and extensibility, and the top-level `let`/`const`/`class`
declarations (which nothing else can clear, so a script with a top-level `let` can otherwise only be run once
per engine); it also clears the `RegExp.$1`-style legacy statics and resets the interop wrapper caches. The
per-node caches above are deliberately kept, so the next run starts warm. Keep the snapshot in a field beside
the engine and put the restore in a `finally` — a script that throws still declared its globals, so restoring
only on the success path hands them to the next caller. `engine.Advanced.WithRestoredGlobals(snapshot, action)`
is exactly that `try`/`finally` in one call, so the restore cannot be left off the throwing path; it adds
nothing else, and in particular no isolation the restore does not already give you.

Choosing between this and `AddLazyGlobal` is a question of engine lifetime: a fresh-engine-per-evaluation
host wants lazy globals (nothing to restore — the win is never building what the script does not read); a
pooled host wants the snapshot. They compose: restore returns a global that was still lazy at capture to its
unmaterialized state, so a pooled engine keeps both benefits.

Restore also ends the previous cycle on the event loop. Queued jobs are discarded, and — because discarding
cannot reach work that has not been enqueued yet — any promise registered before the restore is dropped when
it settles instead of resuming its continuation, so a fire-and-forget `async` function suspended on a host
`Task` never wakes up against the restored globals. Register a promise that is meant to outlive a restore
*after* it. Restore refuses (`InvalidOperationException`) while an evaluation is in progress, including an
`EvaluateAsync`/`ExecuteAsync`/`InvokeAsync` whose `Task` you still hold. What it cannot fence is you calling
back in: invoking a function a previous evaluation handed you runs it against the restored surface.

**It is a configuration-reuse primitive, not an isolation boundary** — mutations of `Object.prototype` and
other intrinsics, of object graphs behind restored bindings, of host CLR state, plus `Symbol.for`
registrations and registered modules, all survive a restore, so mutually distrusting scripts still need
separate engines. A snapshot also keeps its engine and every captured value strongly reachable, so do not
cache one past that engine's lifetime.

**Constraints and options.** An `Options` instance is meant to be shared across engines, including concurrent
ones — the built-in constraint helpers register a *factory*, so each engine gets its own counter and its own
deadline. (Sharing it is not required: building an `Options` per scope is fine when your globals depend on
scoped state.) Watch for the sentinel trap: `MaxStatements(int.MaxValue)`, `LimitMemory(long.MaxValue)` and
`TimeoutInterval(TimeSpan.MaxValue)` register *no* constraint at all and remove any previously registered one
of that kind, so spelling "effectively unlimited" that way leaves you with no limit rather than a large one.

**Values do not cross engines.** A `JsValue` that is an `ObjectInstance` holds a hard reference to the engine
and realm that created it, and passing one to a different engine is not supported — it is neither validated
nor made safe. `Prepared<Script>` / `Prepared<Module>` and `ModuleBuilder` are the supported ways to share
work between engines; convert to CLR values (`ToObject()`) to move data.

## Discussion

Join the chat on [Gitter](https://gitter.im/sebastienros/jint) or post your questions with the `jint` tag on [stackoverflow](http://stackoverflow.com/questions/tagged/jint).

## Video

Here is a short video of how Jint works and some sample usage

https://docs.microsoft.com/shows/code-conversations/sebastien-ros-on-jint-javascript-interpreter-net

## Thread-safety

Engine instances are not thread-safe and they should not accessed from multiple threads simultaneously. 

## Examples

This example defines a new value named `log` pointing to `Console.WriteLine`, then runs
a script calling `log('Hello World!')`. 

```c#
var engine = new Engine()
    .SetValue("log", new Action<object>(Console.WriteLine));
    
engine.Execute(@"
    function hello() { 
        log('Hello World');
    };
 
    hello();
");
```

Here, the variable `x` is set to `3` and `x * x` is evaluated in JavaScript. The result is returned to .NET directly, in this case as a `double` value `9`. 
```c#
var square = new Engine()
    .SetValue("x", 3) // define a new variable
    .Evaluate("x * x") // evaluate a statement
    .ToObject(); // converts the value to .NET
```

You can also directly pass POCOs or anonymous objects and use them from JavaScript. In this example for instance a new `Person` instance is manipulated from JavaScript. 
```c#
var p = new Person {
    Name = "Mickey Mouse"
};

var engine = new Engine()
    .SetValue("p", p)
    .Execute("p.Name = 'Minnie'");

Assert.AreEqual("Minnie", p.Name);
```

You can invoke JavaScript function reference
```c#
var result = new Engine()
    .Execute("function add(a, b) { return a + b; }")
    .Invoke("add",1, 2); // -> 3
```
or directly by name 
```c#
var engine = new Engine()
   .Execute("function add(a, b) { return a + b; }");

engine.Invoke("add", 1, 2); // -> 3
```
## Accessing .NET assemblies and classes

You can allow an engine to access any .NET class by configuring the engine instance like this:
```c#
var engine = new Engine(cfg => cfg.AllowClr());
```

Then you have access to the `System` namespace as a global value. Here is how it's used in the context on the command line utility:
```javascript
jint> var file = new System.IO.StreamWriter('log.txt');
jint> file.WriteLine('Hello World !');
jint> file.Dispose();
```
And even create shortcuts to common .NET methods
```javascript
jint> var log = System.Console.WriteLine;
jint> log('Hello World !');
=> "Hello World !"
```

When allowing the CLR, you can optionally pass custom assemblies to load types from. 
```c#
var engine = new Engine(cfg => cfg
    .AllowClr(typeof(Bar).Assembly)
);
```

and then to assign local namespaces the same way `System` does it for you, use `importNamespace`
```javascript
jint> var Foo = importNamespace('Foo');
jint> var bar = new Foo.Bar();
jint> log(bar.ToString());
```    

adding a specific CLR type reference can be done like this
```csharp
engine.SetValue("TheType", TypeReference.CreateTypeReference<TheType>(engine));
```

and used this way
```javascript
jint> var o = new TheType();
```

Generic types are also supported. Here is how to declare, instantiate and use a `List<string>`:
```javascript
jint> var ListOfString = System.Collections.Generic.List(System.String);
jint> var list = new ListOfString();
jint> list.Add('foo');
jint> list.Add(1); // automatically converted to String
jint> list.Count; // 2
```

### Intercepting access to .NET objects

ECMAScript Proxy traps can be implemented in .NET by deriving from `ProxyHandler` and creating the proxy via `engine.Advanced.CreateProxy` (or `CreateRevocableProxy`). A trap returning `null` forwards the operation to the target, exactly like an absent trap on a JavaScript handler object, and all proxy invariants are enforced on trap results. Combine with `SetWrapObjectHandler` to transparently intercept every wrapped .NET object. Note that `proxy.method()` fires the `Get` trap (then calls the returned value) — the `Apply` trap only fires when the proxy itself is invoked, so intercepting method calls means returning a wrapping function from `Get`. `new Proxy(target, handlerObject)` from script remains the JavaScript-side equivalent.

```c#
class LoggingHandler : ProxyHandler
{
    public override JsValue? Get(ObjectInstance target, JsValue property, JsValue receiver)
    {
        Console.WriteLine($"get {property}");
        return null; // forward to the target
    }

    public override bool? Has(ObjectInstance target, JsValue property)
    {
        Console.WriteLine($"has {property}");
        return null;
    }
}

var engine = new Engine(options =>
{
    // wrap every interop object in a logging proxy
    options.SetWrapObjectHandler((e, obj, type) =>
        e.Advanced.CreateProxy(ObjectWrapper.Create(e, obj, type), new LoggingHandler()));
});
```

## Internationalization

You can enforce what Time Zone or Culture the engine should use when locale JavaScript methods are used if you don't want to use the computer's default values.

This example forces the Time Zone to Pacific Standard Time.
```c#
var PST = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
var engine = new Engine(cfg => cfg.LocalTimeZone(PST));
    
engine.Execute("new Date().toString()"); // Wed Dec 31 1969 16:00:00 GMT-08:00
```

This example is using French as the default culture.
```c#
var FR = CultureInfo.GetCultureInfo("fr-FR");
var engine = new Engine(cfg => cfg.Culture(FR));
    
engine.Execute("new Number(1.23).toString()"); // 1.23
engine.Execute("new Number(1.23).toLocaleString()"); // 1,23
```

### Extending Temporal and Intl with custom providers

Jint ships English-only / ISO-only defaults to keep the binary small. Non-English CLDR
data and full IANA timezone DST history are reachable via two pluggable providers:

| Extension point | Default | What it covers | Reusable example |
| --- | --- | --- | --- |
| `Options.Temporal.TimeZoneProvider` (`ITimeZoneProvider`) | `DefaultTimeZoneProvider` (BCL `TimeZoneInfo` + Windows↔IANA mapping) | UTC offsets, DST transitions, IANA canonicalization | [`NodaTimeZoneProvider.cs`](Jint.Tests.Test262/NodaTimeZoneProvider.cs) — uses NodaTime TZDB for full historical accuracy |
| `Options.Intl.CldrProvider` (`ICldrProvider`) | `DefaultCldrProvider` (English + .NET `CultureInfo`) | locale names, currencies, units, plural rules, calendar month/era names | [`IcuCldrProvider.cs`](Jint.Tests.Test262/IcuCldrProvider.cs) — uses ICU4N for full CLDR coverage |

The provider files in `Jint.Tests.Test262/` are **MIT-licensed copy-and-modify templates**,
not a stable API contract. They are also exactly how the test suite reaches its current
test262 conformance numbers.

To wire them into your project, add the same NuGet packages (`NodaTime`, `ICU4N`), copy
the provider files in, and register them on `Engine` options:

```c#
var engine = new Engine(options =>
{
    options.Temporal.TimeZoneProvider = new NodaTimeZoneProvider();
    options.Intl.CldrProvider          = new IcuCldrProvider();
});
```

A separate `ICalendarProvider` for non-ISO calendar arithmetic (Islamic UmAlQura, Persian
astronomical, Chinese/Dangi at extreme dates) is on the roadmap; until then non-ISO
calendars use `System.Globalization.Calendar` subclasses, which constrains some date
ranges (see [`Jint/Native/Temporal/NonIsoCalendars.cs`](Jint/Native/Temporal/NonIsoCalendars.cs)).

## Execution Constraints 

Execution constraints are used during script execution to ensure that requirements around resource consumption are met, for example:

* Scripts should not use more than X memory.
* Scripts should only run for a maximum amount of time.

You can configure them via the options:

```c#
var engine = new Engine(options => {

    // Limit memory allocations to 4 MB
    options.LimitMemory(4_000_000);

    // Set a timeout to 4 seconds.
    options.TimeoutInterval(TimeSpan.FromSeconds(4));

    // Set limit of 1000 executed statements.
    options.MaxStatements(1000);

    // Use a cancellation token.
    options.CancellationToken(cancellationToken);
}
```

You can also write a custom constraint by deriving from the `Constraint` base class:

```c#
public abstract class Constraint
{
    /// Called before script is run and useful when you use an engine object for multiple executions.
    public abstract void Reset();

    // Called before each statement to check if your requirements are met; if not - throws an exception.
    public abstract void Check();
}
```

For example we can write a constraint that stops scripts when the CPU usage gets too high:

```c#
class MyCPUConstraint : Constraint
{
    public override void Reset()
    {
    }

    public override void Check()
    {
        var cpuUsage = GetCPUUsage();

        if (cpuUsage > 0.8) // 80%
        {
            throw new OperationCancelledException();
        }
    }
}

var engine = new Engine(options =>
{
    options.Constraint(new MyCPUConstraint());
});
```

When you reuse the engine and want to use cancellation tokens you have to reset the token before each call of `Execute`:

```c#
var engine = new Engine(options =>
{
    options.CancellationToken(new CancellationToken(true));
});

var constraint = engine.Constraints.Find<CancellationConstraint>();

for (var i = 0; i < 10; i++) 
{
    using (var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
    {
        constraint.Reset(tcs.Token);

        engine.SetValue("a", 1);
        engine.Execute("a++");
    }
}
```

## Using Modules

You can use modules to `import` and `export` variables from multiple script files:

```c#
var engine = new Engine(options =>
{
    options.EnableModules(@"C:\Scripts");
});

var ns = engine.Modules.Import("./my-module.js");

var value = ns.Get("value").AsString();
```

By default, the module resolution algorithm will be restricted to the base path specified in `EnableModules`, and there is no package support. However you can provide your own packages in two ways.

Defining modules using JavaScript source code:

```c#
engine.Modules.Add("user", "export const name = 'John';");

var ns = engine.Modules.Import("user");

var name = ns.Get("name").AsString();
```

Defining modules using the module builder, which allows you to export CLR classes and values from .NET:

```c#
// Create the module 'lib' with the class MyClass and the variable version
engine.Modules.Add("lib", builder => builder
    .ExportType<MyClass>()
    .ExportValue("version", 15)
);

// Create a user-defined module and do something with 'lib'
engine.Modules.Add("custom", @"
    import { MyClass, version } from 'lib';
    const x = new MyClass();
    export const result = x.doSomething();
");

// Import the user-defined module; this will execute the import chain
var ns = engine.Modules.Import("custom");

// The result contains "live" bindings to the module
var id = ns.Get("result").AsInteger();
```

Note that you don't need to `EnableModules` if you only use modules created using `Engine.Modules.Add`.

### Loading modules asynchronously

`IModuleLoader.LoadModule` must return a parsed module immediately, so a loader that fetches module source over I/O — HTTP, a dev server, an asset pipeline — would have to block the calling thread. Implement `IAsyncModuleLoader` instead, or derive from the `AsyncModuleLoader` template, and no thread is held while the fetch is in flight:

```c#
internal sealed class HttpModuleLoader : AsyncModuleLoader
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("http://localhost:5173/") };

    // Resolution stays synchronous — only fetching an unseen module is allowed to take time
    public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var baseUri = new Uri(referencingModuleLocation ?? _client.BaseAddress!.ToString());
        var uri = new Uri(baseUri, moduleRequest.Specifier);
        return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
    }

    protected override Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        => _client.GetStringAsync(resolved.Uri);
}

var engine = new Engine(options => options.EnableModules(new HttpModuleLoader()));

var ns = await engine.Modules.ImportAsync("./main.js");
```

Static imports inside an asynchronously loaded module are fetched the same way, transitively, before anything is linked; a module wanted by several importers is fetched once. A loader failure — a faulted task, or `ModuleLoadCompletion.SetError` — becomes a rejected promise rather than an exception on the calling thread, and a dynamic `import()` in script keeps working with nothing blocked:

```c#
// Returns immediately; the promise settles on a later turn of the event loop
engine.Execute("import('./late.js').then(ns => log(ns.value));");
```

For full control over when and where the load finishes, implement `IAsyncModuleLoader` directly and settle the `ModuleLoadCompletion` whenever the content arrives, from any thread:

```c#
public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
{
    // e.g. a Unity coroutine, a callback-based transport, a worker queue
    StartCoroutine(Fetch(resolved.Uri, onSuccess: completion.SetSource, onError: completion.SetError));
}
```

The engine is single-threaded, and settling the completion does not change that: the outcome is queued and applied when the engine is next given a turn. On a thread the engine must not block — a game loop, a UI thread — drive the import from your own loop instead of awaiting it:

```c#
// once
var import = engine.Modules.StartImport("./main.js");

// every frame
engine.Advanced.ProcessTasks();
if (import.IsCompleted)
{
    var ns = import.GetResult();   // throws PromiseRejectedException if the load or evaluation failed
}
```

The synchronous `Engine.Modules.Import` still works with an async loader, but the calling thread is what drives the event loop while the loads are in flight — so it deadlocks if the loader's own completions need that same thread. Prefer `ImportAsync` or `StartImport` there. Existing synchronous `IModuleLoader` implementations are unaffected; async loading is opt-in.

A completion settled *before* `LoadModuleAsync` returns — a cache hit, source already in hand — is the exception to all of the above: the load finishes on the calling stack, no event loop involved, so a graph made entirely of such answers keeps the blocking `Import` fully synchronous, exactly as if a synchronous `IModuleLoader` had served it. `AsyncModuleLoader` does this automatically whenever `LoadModuleContentsAsync` returns an already-completed task.

## Asynchronous Execution

Jint supports non-blocking execution of JavaScript that involves `async`/`await` and Promises. This is important in ASP.NET Core and other environments where blocking a thread while waiting for I/O can cause thread-pool exhaustion.

### EvaluateAsync / ExecuteAsync / InvokeAsync

Use `EvaluateAsync` when you want to evaluate JavaScript code that may return a Promise (e.g., an `async` function call). The method awaits Promise settlement without blocking any thread — the calling thread is released back to the pool while I/O is in flight:

```c#
var engine = new Engine();

// Expose an async .NET method to JavaScript
engine.SetValue("fetchData", new Func<string, Task<string>>(async url =>
{
    using var client = new HttpClient();
    return await client.GetStringAsync(url);
}));

// EvaluateAsync properly awaits the Promise returned by the async IIFE
var result = await engine.EvaluateAsync("""
    (async () => {
        const data = await fetchData('https://example.com/api');
        return data;
    })()
    """);

Console.WriteLine(result.AsString());
```

`ExecuteAsync` and `InvokeAsync` follow the same pattern:

```c#
// Execute a script that may produce a Promise and await its completion
await engine.ExecuteAsync("async function init() { ... } init()");

// Invoke a named async function and await its result
var value = await engine.InvokeAsync("myAsyncFunction", arg1, arg2);
```

By default, async execution respects `Options.Constraints.PromiseTimeout`. You can also pass a `CancellationToken`:

```c#
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await engine.EvaluateAsync("(async () => await fetchData(url))()", cancellationToken: cts.Token);
```

### UnwrapIfPromiseAsync

When you already hold a `JsValue` that may be a Promise — for example returned from `engine.Invoke(...)`, `value.Call(...)`, or a property access — use `UnwrapIfPromiseAsync` to await it without blocking:

```c#
// Obtain a JsValue that may be a Promise
var jsValue = engine.Invoke("computeAsync", someArg);

// Await it asynchronously (non-blocking)
var result = await jsValue.UnwrapIfPromiseAsync();

// With cancellation support
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var result = await jsValue.UnwrapIfPromiseAsync(cts.Token);
```

If `jsValue` is not a Promise it is returned immediately. If it is a rejected Promise a `PromiseRejectedException` is thrown.

The synchronous `UnwrapIfPromise` is still available for scenarios where blocking is acceptable (e.g., CPU-bound scripts with no I/O), but `UnwrapIfPromiseAsync` should be preferred in any `async` call chain.

### Task/ValueTask to Promise Interop (Experimental)

When the `TaskInterop` experimental feature is enabled, .NET `Task` and `ValueTask` return values are automatically converted to JavaScript Promises. This allows JavaScript code to `await` or `.then()` the results of .NET async methods without any manual wrapping:

```c#
var engine = new Engine(options =>
{
    options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
});

engine.SetValue("fetchData", new Func<string, Task<string>>(async url =>
{
    using var client = new HttpClient();
    return await client.GetStringAsync(url);
}));

// .NET Task is automatically converted to a JavaScript Promise
var result = engine.Evaluate("fetchData('https://example.com/api').then(data => data)");
result = result.UnwrapIfPromise();
```

Without `TaskInterop`, .NET Tasks passed to JavaScript are exposed as opaque CLR objects. With it enabled, they become native Promises that support `await`, `.then()`, and `.catch()`.

You can configure the timeout for promise resolution via `Options.Constraints.PromiseTimeout`:

```c#
var engine = new Engine(options =>
{
    options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
    options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(10);
});
```

## .NET Interoperability

- Manipulate CLR objects from JavaScript, including:
  - Single values
  - Objects
    - Properties
    - Methods
  - Delegates
  - Anonymous objects
- Convert JavaScript values to CLR objects
  - Primitive values
  - Object -> expando objects (`IDictionary<string, object>` and dynamic)
  - Array -> object[]
  - Date -> DateTime
  - number -> double
  - string -> string
  - boolean -> bool
  - RegExp -> Regex
  - Function -> Delegate
- Extensions methods

## Error handling

### A CLR exception thrown by host code

By default an exception thrown by host code — a delegate you registered, a reflected member, a proxy trap —
is **not** converted into anything. It bubbles straight out of `Execute`/`Evaluate`/`Invoke` to you, with its
message, .NET stack trace and inner exceptions untouched, and the script gets no chance to catch it:

```c#
var engine = new Engine();
engine.SetValue("parse", new Action<string>(s => new XmlDocument().LoadXml(s)));

// throws System.Xml.XmlException, with its own stack trace
engine.Evaluate("parse('<not xml')");
```

`CatchClrExceptions` changes that: the exception becomes a JavaScript `Error` carrying its message, which the
script can `try`/`catch` like any other. The predicate overload decides per exception, so you can let the
script handle what it should handle and keep the rest fatal.

```c#
var engine = new Engine(options => options.CatchClrExceptions(e => e is not OperationCanceledException));
```

### Getting the original exception back

Once an exception has been turned into a JavaScript error, the error is what travels — so when the script does
not catch it and it reaches you as a `JavaScriptException`, the message alone is rarely enough to diagnose
anything. `JintException.TryGetClrException` gives you the exception itself:

```c#
try
{
    engine.Evaluate(script);
}
catch (JavaScriptException e) when (JintException.TryGetClrException(e, out var clrException))
{
    logger.LogError(clrException, "host call failed while running {Script}", name);
}
```

It survives nested frames, a script catching and rethrowing the same error, module evaluation and a rejected
promise (`PromiseRejectedException`), and it follows the standard `cause` chain, so a script that rewraps with
`throw new Error(msg, { cause: err })` keeps it too. A script that throws something unrelated instead keeps
nothing, which is correct — it discarded the original.

The exception is host-only. It is CLR state on the error object rather than a JavaScript property, so a script
can neither read it nor strip it, and it does not appear in `Object.getOwnPropertyNames`, `Reflect.ownKeys` or
`JSON.stringify`. Note that the error object holds the exception, and everything the exception's object graph
reaches, for as long as the script keeps the error reachable.

If you would rather your logging pipeline find it without asking, `ChainClrExceptions` also hangs it in the
`InnerException` chain, so `ToString()` and any chain-walking logger render your own frames alongside the
JavaScript ones. It is off by default because it puts host stack traces into whatever consumes that string —
treat it the way an ASP.NET application treats developer exception details.

```c#
var engine = new Engine(options => options
    .CatchClrExceptions()
    .ChainClrExceptions());
```

Two related accessors work the same way: `JintException.TryGetJavaScriptLocation` and
`TryGetJavaScriptCallStack` give the JavaScript position for both `JavaScriptException` and a bubbled CLR
exception, and `TryGetClrType`/`TryGetClrMemberName` report the type and member behind a failed interop
resolution. To shape what the *script* sees — rewrite the message, attach an error code —
use `DecorateClrExceptionErrors`.

### Raising an error from host code

To fail a host function in a way the script can catch, throw a `JavaScriptException`. The overload taking a
CLR exception records it for `TryGetClrException`, so the script sees an ordinary `Error` while you keep the
original:

```c#
engine.SetValue("parse", new Action<string>(s =>
{
    try
    {
        new XmlDocument().LoadXml(s);
    }
    catch (XmlException e)
    {
        throw new JavaScriptException(engine.Intrinsics.Error, "XML parsing failed", e);
    }
}));
```

Do not throw the exception projected into the script instead
(`new JavaScriptException(JsValue.FromObject(engine, e))`). That value is not an `Error` — it has no `stack`
and fails `instanceof Error` — and it hands the running script the exception's members, including its .NET
stack trace and inner exceptions.

## Security

The following features provide you with a secure, sand-boxed environment to run user scripts.

- Define memory limits, to prevent allocations from depleting the memory.
- Enable/disable usage of BCL to prevent scripts from invoking .NET code.
- Limit number of statements to prevent infinite loops.
- Limit depth of calls to prevent deep recursion calls.
- Define a timeout, to prevent scripts from taking too long to finish.

## Branches and releases

- The recommended branch is __main__, any PR should target this branch
- The __main__ branch is automatically built and published on [MyGet](https://www.myget.org/feed/Packages/jint). Add this feed to your NuGet sources to use it: https://www.myget.org/F/jint/api/v3/index.json
- The __main__ branch is occasionally published on [NuGet](https://www.nuget.org/packages/jint)
