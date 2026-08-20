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
- ✔ Proper tail calls in strict functions
- ✔ Typed arrays
- ✔ Unicode
- ✔ Weakmap and Weakset

Proper tail calls replace the calling strict-function frame, as required by ECMAScript. Consequently,
intermediate tail callers are intentionally absent from `error.stack` and host stack telemetry.

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


## Web APIs (opt-in)

Beyond ECMAScript, Jint is growing a WHATWG-faithful set of web platform APIs — the surface a script author
expects from a browser or from Node: `console`, timers, `TextEncoder`, `fetch` and friends. It follows the
published standards (`console.spec.whatwg.org`, `webidl.spec.whatwg.org`, `fetch.spec.whatwg.org`, …) rather
than inventing a Jint-shaped variant, so scripts written against those standards behave the way their authors
expect.

**Everything here is opt-in and nothing is installed by default.** An engine that does not ask for these APIs
is byte-for-byte the engine it was before they existed: no extra globals, no extra work at construction, no
behaviour change. That is deliberate — an engine embedded in a workflow runner or a template renderer has no
business exposing them, and outbound network access in particular is a decision a host has to make
explicitly.

**Requires .NET 8 or higher.** The whole surface is compiled only for `net8.0` and later; on `net462`,
`netstandard2.0` and `netstandard2.1` the options and types simply do not exist.

```csharp
// Everything except network access.
var engine = new Engine(options => options.UseWebApis());

// Or exactly what you want, with somewhere for console output to go.
engine = new Engine(options => options
    .UseWebApis(WebApiFeatures.Console)
    .UseConsole(Console.Out));

engine.Execute("console.log('hello %s', 'world')");
```

`console` output goes to a `ConsoleSink`, which defaults to `ConsoleSink.Null` and discards everything —
enabling the feature never starts writing to your process's standard output by surprise. `ConsoleSink.FromTextWriter`
covers the common case; implement the abstract class to route records to your own logger, where the
`ConsoleLogLevel` tells you how loud each one is. Every method emits exactly one `Write` call per record, so a
`console.table(rows)` reaches your sink as one multi-line ASCII table rather than as a line per row, and the
formatter never invokes a script-visible getter — an accessor renders as `[Getter]`, because a console must
not be a way to run arbitrary script.

A global you registered yourself always wins: the install is non-clobbering, so if your host already exposes
its own `console` (or any other name in the table below), enabling the feature leaves yours exactly as it is.

| API | Feature flag | Status |
| --- | --- | --- |
| `console` (`log`/`warn`/`error`/`group`/`count`/`time`/`assert`/`trace`/`dir`/`table`) | `WebApiFeatures.Console` | ✔ shipped |
| `DOMException` | *(no flag — installed whenever any feature is enabled)* | ✔ shipped |
| `setTimeout` / `setInterval` / `clearTimeout` / `clearInterval` / `queueMicrotask` | `WebApiFeatures.Timers` | ✔ shipped |
| `TextEncoder` / `TextDecoder` (UTF-8, UTF-16LE, UTF-16BE; `fatal`, `ignoreBOM`, streaming) | `Encoding` | ✔ shipped |
| `atob` / `btoa` | `Base64` | ✔ shipped |
| `structuredClone` (incl. `{ transfer }` of `ArrayBuffer`s) | `StructuredClone` | ✔ shipped |
| `crypto.getRandomValues` / `crypto.randomUUID` / `crypto.subtle` (SHA digests, HMAC, AES-GCM, `CryptoKey`) | `WebApiFeatures.Crypto` | ✔ shipped |
| `performance.now` / `timeOrigin` / `mark` / `measure` / `getEntries*` / `clearMarks` / `clearMeasures` | `WebApiFeatures.Performance` | ✔ shipped |
| `Event` / `EventTarget` / `CustomEvent` / `AbortController` / `AbortSignal` | `Events` | ✔ shipped |
| `URL` / `URLSearchParams` | `Url` | ✔ shipped |
| `Blob` (incl. `stream()`) / `File` / `FormData` | `Files` | ✔ shipped |
| `navigator.userAgent` | `Navigator` | ✔ shipped |
| `ReadableStream` / `WritableStream` / `TransformStream` / `ByteLengthQueuingStrategy` / `CountQueuingStrategy` | `Streams` | ✔ shipped |
| `scheduler.postTask` / `scheduler.yield` / `TaskController` / `TaskSignal` | `Scheduler` | ✔ shipped |
| `MessageChannel` / `MessagePort` / `MessageEvent` (incl. cross-engine ports) | `Messaging` | ✔ shipped |
| `reportError` (and the `DiagnosticsSink` behind it) | `Reporting` | ✔ shipped |
| `localStorage` / `sessionStorage` / `Storage` | `Storage` *(not in `Default` — see below)* | ✔ shipped |
| `fetch` / `Headers` / `Request` / `Response` | `Fetch` — **opt-in on its own, see below** | ✔ shipped |
| `EventSource` / `MessageEvent` (server-sent events) | `EventSource` — **opt-in on its own, see below** | ✔ shipped |

`WebApiFeatures.Default` — what `UseWebApis()` enables — is every non-network feature that has landed. It
grows as the table fills in, and it will never include `fetch`: network egress is always an explicit choice.
`Storage` is the other standing exception, for the reason in its own section below.

`crypto.subtle` carries **`digest`, `sign`, `verify`, `encrypt`, `decrypt`, `generateKey`, `importKey` and
`exportKey`**, over SHA-1/256/384/512 (for `digest` and as HMAC's inner hash), **HMAC**, and **AES-GCM** at
128, 192 and 256 bits. Keys are real `CryptoKey` objects with `type`, `extractable`, `algorithm` and
`usages`; the key material is never reachable from script except through `exportKey` on an extractable key,
as `raw` bytes or as a JSON Web Key (`kty: "oct"`). `deriveKey`, `deriveBits`, `wrapKey`, `unwrapKey` and
every asymmetric algorithm are *absent* rather than present-and-throwing, so
`typeof crypto.subtle.deriveBits` tells a library the truth and it takes its fallback path.

Nothing here ever throws: a promise-returning WebIDL operation converts every failure into a rejection. An
algorithm that is not registered for the operation is a `NotSupportedError` `DOMException`, a key used
against its own `usages` or against another algorithm is an `InvalidAccessError`, a malformed JWK is a
`DataError`, a usage list an algorithm does not support is a `SyntaxError`, and anything an argument
conversion refuses is a `TypeError`. An AES-GCM decryption that does not authenticate is one
`OperationError` carrying nothing about which part of the input was wrong. The work is synchronous, so the
promise is already settled when you get it.

Two limits of .NET's AES-GCM are visible, and both are reported as the `OperationError` the algorithm's own
steps end in: the `iv` must be the 96 bits NIST SP 800-38D recommends, and `tagLength` must be 96, 104, 112,
120 or 128 — the specification also lists 32 and 64, which `System.Security.Cryptography.AesGcm` will not
produce. The ciphertext is `ciphertext || tag`, exactly as the specification defines it, so a host reading a
script's output with `AesGcm` splits the last `tagLength / 8` bytes off itself.

### Timers fire only while the engine is being pumped

**Jint never starts a thread, and never a `System.Threading.Timer`, to run your script.** A `setTimeout`
callback runs on the first drain of the event loop at or after its due time, and a drain only happens where
one always happened: at the end of an `Execute`/`Evaluate`, inside a blocking `UnwrapIfPromise`, while
`await`ing `EvaluateAsync`, or when the host calls `engine.Advanced.ProcessTasks()` itself.

```csharp
var engine = new Engine(options => options.UseWebApis());

// Blocking: the drain waits out the timer and returns 42.
var answer = engine.Evaluate("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()")
    .UnwrapIfPromise();

// Or without holding a thread.
answer = await engine.EvaluateAsync("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()");

// Or from your own loop — a game loop, a message pump — where every turn provably runs on your thread.
engine.Execute("setTimeout(() => console.log('later'), 50);");
while (running) { engine.Advanced.ProcessTasks(); Thread.Sleep(5); }
```

An engine nobody pumps never fires a timer, which is what makes this safe in a request handler that returns
as soon as the script does — a scheduled callback cannot surprise you later, on a thread you did not expect,
against state you have moved on from. The corollary is that a timer outlasting the wait around it is simply
never reached: `new Promise(r => setTimeout(r, 15000))` under the default ten-second `UnwrapIfPromise` times
out, by design.

The engine's single job queue *is* the microtask queue: a due timer is promoted onto it only once it has run
dry, so `Promise.resolve().then(f)` always beats `setTimeout(g, 0)`, each timer gets its own microtask
checkpoint, and no interval can starve promise reactions. Timers are scheduled against
`Options.WebApi.Timers.TimeProvider`, so handing the engine a fake clock makes a suite that exercises them
deterministic and instant; `MaxActiveTimers` (1000 by default) bounds how many a script may register at once
and turns the excess into a catchable `QuotaExceededError` `DOMException`. A callback that throws erupts out
of whatever was pumping — the same contract a promise reaction has — and the rest of the queue runs on the
next pump, unless you installed a `DiagnosticsSink`, in which case it is reported to that instead and the
pump carries on.

### Prioritized tasks: `scheduler.postTask` and `scheduler.yield`

`scheduler.postTask(callback, { priority, signal, delay })` runs a callback as its own task and hands back a
promise for what it returned; `scheduler.yield()` hands back a promise that resolves in a fresh *continuation*
task, which is how a long piece of work breaks itself up and still resumes ahead of the work queued behind it.
Priorities are the standard's three — `user-blocking` > `user-visible` (the default) > `background` — and a
`TaskController` can abort a batch of tasks or reprioritize the ones still waiting, firing `prioritychange` at
its `TaskSignal` as it does. Neither method ever throws: a bad argument, an aborted signal or a callback that
throws all become a rejection of the promise you were handed.

Tasks run only while the engine is being pumped, exactly as timers do, and a `delay` rides the very same timer
queue. Three ordering guarantees, which the tests pin: every microtask runs before the next task, whichever
order the two were queued in; among the tasks pending together the highest priority wins, ties going to the
oldest; and every runnable task runs before any *due* timer — the one place this deliberately differs from a
browser, which is free to make the opposite choice for a `background` task and typically does.

### Events dispatch to one target, because there is no tree

`EventTarget` is constructible and `Event`, `CustomEvent`, `AbortController` and `AbortSignal` behave as the
DOM standard describes them, with one reduction: the specification's dispatch walks an *event path* built
from the target's ancestors in a node tree, and Jint has no node tree. The path is therefore always the
single item «target» — which is exactly what the algorithm produces for the tree-less `EventTarget`s the
standard itself says author code creates. Everything that survives that reduction is the real algorithm:
`capture` decides which of the two passes a listener runs in, `stopPropagation()` ends the dispatch after the
current pass and `stopImmediatePropagation()` also skips the rest of it, `once` listeners are removed before
they run, a duplicate `(type, callback, capture)` registration is ignored, and `removeEventListener` matches
on the object identity your script passed.

**A listener that throws erupts from `dispatchEvent` — unless you set a `DiagnosticsSink`.** The standard
says to *report* the exception and carry on to the next listener, which needs somewhere to report to.
Install a sink and that is exactly what happens; without one, swallowing the exception would lose it
entirely, so it propagates instead — the same contract a timer callback has. The dispatch state is unwound
either way, so the event and the target stay usable.

`AbortSignal.timeout(ms)` schedules on the same queue the timers use, so it aborts only while the engine is
being pumped and it counts against `MaxActiveTimers`; that queue exists whenever the `Events` feature does,
whether or not you also asked for `setTimeout`. `AbortSignal.any(signals)` builds a composite that is
retained by its sources until one of them aborts, at which point every link is dropped.

### The performance timeline is bounded

`performance.mark` and `performance.measure` behave as [User Timing](https://w3c.github.io/user-timing/)
describes them — the whole overload matrix, mark names or raw timestamps, `detail` deep-copied through the
same structured-clone algorithm `structuredClone` uses, and `PerformanceEntry` / `PerformanceMark` /
`PerformanceMeasure` as real interface objects so `entry instanceof PerformanceMark` works. Entries come back
from `getEntries()`, `getEntriesByType(type)` and `getEntriesByName(name, type?)` sorted by `startTime`, and
`clearMarks(name?)` / `clearMeasures(name?)` remove them.

```js
performance.mark('parse');
// ... work ...
const m = performance.measure('parse-to-now', 'parse');
console.log(m.duration);
```

One thing is deliberately not the browser's: the entry buffer holds **10,000 entries**, not an unbounded
number. A page's timeline dies with the page, and an engine embedded in a long-lived host has no such event —
`while (true) performance.mark('x')` would otherwise be a memory leak that no execution constraint describes.
Once the buffer is full, further entries are dropped exactly as the Performance Timeline standard says a full
buffer drops them: nothing throws, `mark()` and `measure()` still return the entry they built, and
`getEntries()` simply stops growing until you clear it. There is no `PerformanceObserver`, and it is absent
rather than present-and-throwing so feature detection takes its fallback path. Readings are **not coarsened** —
an embedded engine has no cross-origin data for a fine clock to help steal, and a host that wants a coarse one
supplies a coarse `TimeProvider`.

`navigator` carries `userAgent` and nothing else. It reports `Jint/<version>` — a single RFC 7231 product
token with no comment component, so nothing about your operating system or your application leaks into it —
and it is there because [WinterTC's Minimum Common API](https://min-common-api.proposal.wintertc.org/)
requires `globalThis.navigator.userAgent` of a conforming runtime. Everything else a browser's `Navigator`
carries describes a user agent with a user, a document and a network stack, so it is absent rather than faked.

### Channel messaging can span two engines

`new MessageChannel()` gives the usual entangled pair, and it behaves as the HTML Standard describes: a
message is structured-cloned *when it is posted* — so a `DataCloneError` is thrown synchronously at the
`postMessage` call and a later mutation of the value cannot reach the message — and delivered as an
event-loop task, so every already-queued promise reaction runs first. A port's message queue starts
**disabled**: nothing arrives until `start()` is called or `onmessage` is assigned, and
`addEventListener('message', …)` on its own does not start it. `{ transfer: [buffer] }` moves an
`ArrayBuffer` instead of copying it, exactly as `structuredClone` does; transferring a *port* is not
supported and is refused with a `DataCloneError`.

The same pair can also connect **two engines**, which is what makes a worker-style split possible without a
second process:

```csharp
var host = new Engine(o => o.UseWebApis());
var worker = new Engine(o => o.UseWebApis());

// Create the pair while neither engine is running, then give each half to its own engine.
var pair = host.Advanced.CreateMessagePortPair(worker);
host.SetValue("port", pair.Local);
worker.SetValue("port", pair.Remote);

worker.Execute("port.onmessage = e => port.postMessage(e.data.n * 2);");
host.Execute("port.onmessage = e => log(e.data); port.postMessage({ n: 21 });");

worker.Advanced.ProcessTasks();   // the worker's turn: it receives 21 and replies
host.Advanced.ProcessTasks();     // the host's turn: it receives 42
```

**No `JsValue` ever crosses.** `postMessage` serializes on the calling engine's thread into a record that
holds nothing engine-affine — primitives, byte arrays, lists, type tags — and enqueues a job on the receiving
engine's event loop, the one part of an engine any thread may touch. The deserialization, the `MessageEvent`
and the listeners all run on whichever thread pumps that engine, so nothing on the receiver is touched off
its own pump. The receiver must actually be pumped, exactly as for timers: an engine nobody pumps never
delivers a message. And a `RestoreGlobalSnapshot` on either engine ends that channel permanently — a port's
listeners are closures over the cycle it was created in — so a pooled engine wants a fresh pair per cycle.

### Uncaught script errors go to a `DiagnosticsSink`

A script's failures are not all catchable by the host: a promise rejects with nobody listening, a `setTimeout`
callback throws long after `Execute` returned, a script calls `reportError`. `Options.WebApi.Diagnostics.Sink`
is the one place all of that arrives.

```csharp
sealed class LogSink : DiagnosticsSink
{
    public override void Report(DiagnosticEvent report) =>
        logger.LogWarning("{Kind}: {Message}", report.Kind, report.Exception?.Message ?? report.Value.ToString());
}

var engine = new Engine(options => options.UseWebApis().UseDiagnostics(new LogSink()));
```

There are three kinds of report. `ReportedError` is what a script handed `reportError(e)`.
`UnhandledPromiseRejection` is `HostPromiseRejectionTracker`'s two operations, told apart by
`RejectionHandled` — and it is *additive*: the long-standing `engine.Advanced.PromiseRejectionTracker` event
still fires exactly as it did, first. `UncaughtCallbackError` is an exception that escaped a callback the
engine invoked for you.

**That last one is the reason a sink changes behaviour, so install one deliberately.** With no sink a timer
callback or an event listener that throws erupts out of whatever was running it, because the alternative
would be to lose the error entirely. With a sink there is somewhere for it to go, so the engine reports it
and carries on — which is what the standards actually specify (HTML invokes a timer handler with exception
behaviour `"report"`; DOM's *inner invoke* reports a throwing listener and moves to the next one). Errors that
exist to **bound** execution are never reported and always erupt: a timeout, a cancellation, the statement,
memory and recursion budgets. `DiagnosticsSink.Null` is therefore not the same as no sink — it means "carry
on, and tell me nothing".

A sink alone is enough; the `Reporting` feature flag only decides whether script can *call* `reportError`, and
`reportError` without a sink is a documented no-op that never throws. The sink is read once, when the engine is
built. It is called synchronously, on the engine's thread, and must be thread-safe if the same `Options` builds
engines that run concurrently. The `JsValue`s on a report belong to the reporting engine: read them inside the
call rather than stashing them.

**A `ShadowRealm` does not get these globals.** Only the principal realm's global object is touched, which is
deliberately more conservative than a browser (where these APIs are `[Exposed=*]`); a host that wants them
inside a shadow realm can install them through `Host.InitializeShadowRealm`.

### `fetch` is opt-in on its own

`UseWebApis()` never enables `fetch`, and `WebApiFeatures.Default` will never include it. Network egress from
script is a decision you make explicitly:

```csharp
var engine = new Engine(options => options.UseWebApis().UseFetch(fetch =>
{
    fetch.AllowedSchemes.Remove("http");                     // https only
    fetch.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    fetch.MaxResponseBytes = 1024 * 1024;
    fetch.Timeout = TimeSpan.FromSeconds(5);
    fetch.HttpClient = myClient;                             // or HttpClientFactory, for a per-tenant client
}));

var body = engine.Evaluate("fetch('https://api.example.org/x').then(r => r.json())").UnwrapIfPromise();
```

Enabling it also brings `Events`, `Url`, `Files` and `Streams`, because fetch's own surface is built out of
them: a `Request` always has an `AbortSignal`, its URL is a WHATWG URL, `response.blob()` answers with a
`Blob`, and `response.body` is a `ReadableStream`.

`Headers`, `Request` and `Response` are the standard's own classes — the full `Headers` (sorted, combined
iteration, `getSetCookie`), method normalization, `redirect` modes, `clone()`, `Response.error()`,
`Response.redirect()`, `Response.json()`. `formData()` is absent and a `FormData` request body is a
`TypeError` — the `multipart/form-data` serializer is a follow-up. `credentials`, `cache`, `mode`, `referrer`
and `integrity` are accepted and ignored, the same convention Node and workerd follow, because there is no
origin, cookie jar or HTTP cache here to honour them with.

### Response bodies stream

`response.body` is a real `ReadableStream`, and a network response is read **on demand**: the promise
resolves as soon as the headers are in, and the socket is only read when a consumer asks for the next chunk.

```js
const r = await fetch('https://api.example.org/big.ndjson');
for await (const chunk of r.body) {
    // one Uint8Array per read; the transport is not read again until this loop comes back for more
}
```

The stream's high water mark is zero, so a script that takes the response and never reads its body never
touches the socket again — and `body.cancel()`, or a reader's `cancel()`, drops the connection. Chunks are
delivered as generation-stamped event-loop jobs, exactly like every other cross-thread completion in Jint,
so nothing about a body arrives on a thread the host did not pump. `MaxResponseBytes` is enforced on the
running total per chunk; because the promise has already resolved by the time a body byte is read, a body
that breaks the cap **errors the stream** rather than rejecting the `fetch` promise (a `Content-Length` that
already exceeds it is still refused before the promise settles).

The `Body` mixin sits on top of that and is unchanged in behaviour: `text()`, `json()`, `arrayBuffer()`,
`bytes()` and `blob()` read the stream to the end and answer with the whole body, `bodyUsed` is the stream's
*disturbed* flag, and a body that is disturbed or locked makes the next consumer reject. `clone()` `tee()`s
the stream, so each half has its own queue and its own flags. A body built from bytes — `new Response('…')`,
a `Blob`, a `BufferSource` — keeps its bytes and only materializes a stream if something reads `body`, so the
common `new Response(x).text()` costs no stream at all.

`Blob.stream()` is there too, and a `ReadableStream` is accepted as a `BodyInit` for both `Request` and
`Response`. **Uploads are still buffered:** a request body given as a stream is read in full before anything
is sent, so the standard's `duplex: 'half'` streaming upload is out of scope and `duplex` is absent rather
than accepted and ignored.

Like every other web API, `fetch` settles only while the engine is being pumped: the promise resolves inside a
blocking `UnwrapIfPromise`, an `await` of `EvaluateAsync`, or your own `engine.Advanced.ProcessTasks()` loop.
The deadline is the one exception, and deliberately so — it is enforced CLR-side, so an engine nobody pumps
still lets go of its socket, and it covers the body as well as the headers. An in-flight request is cancelled
by `Engine.Advanced.RestoreGlobalSnapshot`, and its promise never settles into the restored engine; a body
still arriving when that happens has its connection dropped and its stream errored, so a host holding the
response is told rather than left waiting.

**Security.** Enabling `fetch` gives the script your process's network position: anything the worker can
reach, the script can reach. The defaults bound the resource questions — 32 MiB per response body (counted
after decompression, so a compression bomb is bounded too), 20 redirects, a 30-second deadline, 10 concurrent
requests — but they cannot know which *hosts* are legitimate. That is what `UrlFilter` is for, and a
deployment running untrusted script wants one. Jint follows redirects itself with `AllowAutoRedirect` off
precisely so that **every hop is re-checked** against the scheme list and your filter: a server you allow
answering `302 Location: http://169.254.169.254/…` does not get to launder the request past a first-hop check.
`Authorization`, `Cookie` and `Proxy-Authorization` are stripped across an origin change, header values may
not carry CR or LF, and a URL with credentials in it is refused. Every network-class failure is one
`TypeError` saying only `Failed to fetch`, so a script cannot map your internal network by reading the
failures apart; the real cause rides the error value and is readable by the host through
`JintException.TryGetClrException`. See [THREAT_MODEL.md](.github/THREAT_MODEL.md) TM-21 for the full
analysis, including what these controls do *not* cover.

### Streams are the default (non-byte) half of the standard

`ReadableStream`, `WritableStream` and `TransformStream` implement the WHATWG Streams Standard operation by
operation: queuing strategies and `desiredSize`, the `pull` reentrancy rules, `tee()` with its composite
cancellation, `pipeTo()` / `pipeThrough()` with `preventClose` / `preventAbort` / `preventCancel` and an
`AbortSignal`, asynchronous iteration of a readable stream (`for await…of`, and `values({ preventCancel })`),
and `ReadableStream.from()` for any sync or async iterable. Every promise they hand out is an ordinary engine
promise and every callback you supply — `start`, `pull`, `cancel`, `write`, `close`, `abort`, `transform`,
`flush`, `size` — runs on the engine's thread from the same job queue that runs promise reactions, so the
microtask ordering the standard prescribes is the ordering you get, and nothing here ever starts a thread.

Two deliberate reductions:

- **Byte streams are absent, not broken.** There is no `ReadableByteStreamController`, no BYOB reader and no
  `ReadableStreamBYOBRequest`, and `new ReadableStream({ type: 'bytes' })` raises a `TypeError` rather than
  handing back something that is not a byte stream. `getReader({ mode: 'byob' })` raises the same `TypeError`
  the standard gives for a stream that was not constructed with a byte source.
- **Only the five interfaces a script constructs by name are globals.** `ReadableStreamDefaultReader`,
  `WritableStreamDefaultWriter` and the three controllers exist as ordinary interface objects — a reader's
  `constructor` is the real thing, and `new` on it behaves as the standard says — but they are not installed
  on `globalThis`, where a browser would expose them.

### Storage is opt-in on its own, and you decide where the data lives

`localStorage` and `sessionStorage` are the one non-network feature `UseWebApis()` does **not** turn on. Every
other web API gives a script something to *do*; this one gives it somewhere to *keep* things, and a host that
asked for "the web APIs" has not agreed to that. Ask for it by name:

```csharp
// Two in-memory stores, per engine, gone when the engine is.
var engine = new Engine(options => options.UseStorage());

// Or your own store, which is what makes anything persist or be shared.
var tenantStorage = new MyDatabaseStorage(tenantId);
engine = new Engine(options => options.UseStorage(tenantStorage));
```

The whole `Storage` interface is there — `length`, `key`, `getItem`, `setItem`, `removeItem`, `clear` — and so
is the named property access the standard defines alongside it, so `storage.foo = 'bar'`, `delete storage.foo`,
`'foo' in storage` and `Object.keys(storage)` all work. `Storage` is a WebIDL *legacy platform object*, which
decides the one rule that surprises people: an interface member always wins over a stored key of the same
name, so `storage.getItem` is the method even after `storage.setItem('getItem', 'x')` — and the key is still
stored, and `storage.getItem('getItem')` still reads it back.

**Where the data lives is entirely `StorageProvider`'s business.** Implement it over a file, a database, a
per-tenant cache or a request-scoped dictionary; the engine implements the algorithms and never persists
anything itself. With no provider each engine gets its own `InMemoryStorageProvider` — nothing is shared
between engines, nothing survives one, and `Options.WebApi.Storage.MaxTotalBytes` (5 MB by default) bounds it,
turning an over-quota `setItem` into the catchable `QuotaExceededError` `DOMException` the standard names.
Your own provider raises the same error by throwing `StorageQuotaExceededException`; anything else it throws
reaches you unchanged rather than becoming a JavaScript error the script can swallow. A provider reached from
engines that run concurrently must be thread-safe.

**There is no `storage` event.** Every mutating step ends in "broadcast", which notifies *other* browsing
contexts sharing an origin — a multi-context feature, and an engine has one context. `StorageEvent` is absent
rather than present-and-never-firing, so feature detection sees the truth.

### `EventSource` is a second, separate grant

Server-sent events are their own opt-in: `UseFetch()` does not enable `EventSource`, `UseEventSource()` does
not enable `fetch`, and `UseWebApis()` enables neither.

```csharp
var engine = new Engine(options => options.UseWebApis().UseEventSource(net =>
{
    net.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    net.MaxResponseBytes = 64 * 1024;   // the largest single event, not the largest stream
    net.MaxConcurrentRequests = 2;      // at most two streams open at once
}));

engine.Execute("""
    const events = new EventSource('https://api.example.org/updates');
    events.onmessage = e => console.log(e.lastEventId, e.data);
    """);

while (running) { engine.Advanced.ProcessTasks(); Thread.Sleep(5); }   // your loop, your thread
```

It is the standard's own object: `url`, `readyState` with `CONNECTING`/`OPEN`/`CLOSED`, `onopen`/`onmessage`/
`onerror`, `close()`, and `addEventListener` for the custom types an `event:` field names. The stream is
parsed exactly as the specification writes it — UTF-8 with a leading BOM stripped, CRLF/CR/LF line endings,
`data`/`event`/`id`/`retry` fields, one leading space removed after the colon, comment lines as keep-alives,
`data` values joined with a newline, and an event dispatched only at a blank line. `withCredentials` is
accepted, remembered and ignored, the same treatment `fetch` gives `credentials`: there is no origin, cookie
jar or credential store here for it to select.

**It reads `Options.WebApi.Fetch`** — the same transport (`HttpClient` / `HttpClientFactory`) and the same
policy (`AllowedSchemes`, `UrlFilter`, `MaxRedirects`) that `fetch` uses, so a filter you have already written
covers both, and every redirect hop is re-checked exactly as it is for a fetch. Three of those settings mean
something different for a stream:

- **`Timeout` does not apply.** A connection that is idle for an hour is what an event stream is *for*.
- **`MaxResponseBytes` does not bound the stream** — nothing could — it bounds **one event**: the data buffer
  plus the line being read, which is what actually has to be held in memory. Exceeding it fails the connection.
- **`MaxConcurrentRequests` bounds the streams one engine may have open**, counted separately from the
  fetches in flight, because a stream holds its socket for as long as it lives.

**Reconnection rides the timer queue**, so it too happens only while you are pumping, and it counts against
`MaxActiveTimers`. A stream that ends, or a network error, sets `readyState` back to `CONNECTING`, fires
`error`, waits the server's `retry:` value (3 seconds by default) and connects again — re-running the URL
policy from scratch and sending `Last-Event-ID` so the server can resume. A response that is not `200
text/event-stream`, a URL your policy refuses and an event over the size cap all *fail* the connection
instead: `readyState` becomes `CLOSED` and nothing retries. `close()` cancels the request in flight, and so
does `Engine.Advanced.RestoreGlobalSnapshot` — after a restore the connection is gone, its pending
reconnection with it, and nothing from the ended cycle is ever dispatched into the restored engine.

**Security.** Everything TM-21 says about destinations applies here, and one thing more: a connection is
long-lived and reconnects *on a delay the server chooses*, with no deadline to end it. See
[THREAT_MODEL.md](.github/THREAT_MODEL.md) TM-22 — the short version is to bound the engine's own lifetime
for untrusted script rather than pooling an engine a script may leave streaming.

### Hosting a fetch handler

`Headers`, `Request` and `Response` work in both directions, so an engine can *be* a request handler: you
hand it an `HttpRequestMessage` and get an `HttpResponseMessage` back, with the script in between written
exactly the way a Cloudflare Workers or Deno script is.

```js
// worker.js
export default {
    async fetch(request) {
        const body = await request.json();
        return Response.json({ echoed: body, path: new URL(request.url).pathname });
    }
};
```

```csharp
var engine = new Engine(options => options.UseWebApis(
    WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files | WebApiFeatures.Timers));

engine.Advanced.SetFetchHandler(engine.Modules.Import("./worker.js"));

using var response = await engine.Advanced.InvokeFetchHandlerAsync(request);
```

`SetFetchHandler` accepts three shapes, tried in this order: a **function**; an **object with a callable
`fetch` property** (called with that object as `this`, so a handler written as a method can reach its
siblings); or an object with a **`default`** property matching either — which is what a module namespace is,
so the `export default { fetch }` convention above is registered in one call. A script that has no modules
registers its handler just as directly:

```csharp
engine.Execute("function handle(request) { return new Response('hi'); }");
engine.Advanced.SetFetchHandler(engine.GetValue("handle"));
```

**There is no feature flag for this, and registering a handler never grants network access.** What the object
model needs is the three features its own interfaces are built out of — a `Request` has an `AbortSignal`, its
URL is a WHATWG URL, `response.blob()` answers with a `Blob` — so an engine built without `Events | Url |
Files` refuses the registration with a message naming them. Registering the handler is itself what installs
the `Headers`, `Request` and `Response` globals, lazily and without replacing anything already under those
names; `fetch` is deliberately *not* installed, because handling an inbound request is no reason to let the
script make outbound ones. (`options.UseFetch()` satisfies the requirement too, and does grant them.) One
ordering consequence: those globals appear when you register the handler, so a module that builds a `Response`
at *top level* has to be evaluated after the registration — the ordinary shape, where the handler builds its
response when it runs, does not care.

Only the request is passed. A Workers handler takes `(request, env, ctx)`; per-request host state reaches this
one the way it always has, through `engine.Advanced.AddLazyGlobal(...)` or a host function reading
`engine.Advanced.HostDefined`.

**Two invoke shapes**, and the difference is who owns the thread. `InvokeFetchHandlerAsync` is the one an
ASP.NET Core host wants — `await` it and the continuations run wherever the await resumes.
`InvokeFetchHandler` returns a `FetchHandlerOperation` and runs nothing on its own: you pump the engine and
watch the operation, which is the shape a game loop or a UI thread needs, because then every turn provably
runs where you decided. It is the same pair `Engine.Modules.ImportAsync` / `StartImport` offers, for the same
reason.

```csharp
var operation = engine.Advanced.InvokeFetchHandler(request);
while (!operation.IsCompleted)
{
    engine.Advanced.ProcessTasks();     // your loop, your thread
}

using var response = operation.GetResult();
```

A handler answering a `Response` synchronously produces an operation that is already complete; one answering
a promise — every `async` handler, and anything that awaits a timer or an outbound `fetch` — needs turns. As
everywhere else in these APIs, a `setTimeout` inside a handler only fires while somebody is pumping.

**A failing handler is never turned into a 500 for you.** What a failure means on the wire is a policy
question only the host can answer, so it arrives as the exception it was: a `JavaScriptException` when the
handler threw, a `PromiseRejectedException` when its promise rejected, the constraint's own exception when an
execution constraint fired, an `InvalidOperationException` when the handler answered with something that is
not a `Response` (including `Response.error()`, which represents a network error and has no status line).
All of Jint's own exceptions derive from `JintException`, so that is the one type to catch:

```csharp
try
{
    using var handled = await engine.Advanced.InvokeFetchHandlerAsync(request, context.RequestAborted);
    // ... copy it onto the real response
}
catch (JintException ex)
{
    logger.LogWarning(ex, "the script handler failed");
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
}
```

With the polled shape the same failures arrive through the operation instead — `IsFaulted`, `Error`, or
rethrown by `GetResult()` — including a failure of the invoke call itself, so a host written to
poll-then-`GetResult` never has to guard the start call as well.

The handler runs under the engine's execution constraints like every other host entry, so `MaxStatements`,
`TimeoutInterval`, `LimitMemory` and any custom `Constraint` bound one invocation. Read
[Execution Constraints](#execution-constraints) before relying on that: the budget is per *entry* into the
engine, so each turn you pump afterwards gets a fresh one — a wall-clock bound over the whole request is what
`Jint.Constraints.OperationDeadlineConstraint` is for, bracketed around the invoke and the pump. The
`CancellationToken` taken by `InvokeFetchHandlerAsync` behaves exactly as `EvaluateAsync`'s does: it is
observed at event-loop continuation boundaries and does **not** preempt the interpreter, so bounding a handler
that never yields is a constraint's job.

Request bodies are read in full before the handler runs — bodies here are buffered, not streamed. The
awaitable shape reads them without blocking; the polled shape reads them on the calling thread, so buffer the
content yourself (a `ByteArrayContent`) if it is still arriving off a socket. On the way out, `Content-Length`
and `Transfer-Encoding` are dropped: those describe the message your HTTP stack is actually writing, and a
script's claim about a body it is not sending would be a response-splitting primitive. Everything else is
copied verbatim, with equally-named headers kept apart — several `Set-Cookie`s stay several.

<details>
<summary>ASP.NET Core: a sandboxed per-request script handler</summary>

A documented sample rather than a package: Jint takes no dependency on ASP.NET Core, and this is all the glue
there is. The engine comes from a pool the host owns — one engine per concurrent request, since an `Engine` is
not thread-safe — and every one of them has had `SetFetchHandler` called on it.

```csharp
var scriptOptions = new Options()
    .UseWebApis(WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files | WebApiFeatures.Timers)
    .MaxStatements(100_000)
    .LimitMemory(16 * 1024 * 1024)
    .TimeoutInterval(TimeSpan.FromSeconds(2));

app.Map("/{**path}", async (HttpContext context, EnginePool pool, ILogger<Program> logger) =>
{
    using var lease = pool.Rent();                       // an engine with the handler already registered

    var buffer = new MemoryStream();
    await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);

    var inbound = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.GetEncodedUrl())
    {
        Content = new ByteArrayContent(buffer.ToArray()),
    };

    foreach (var header in context.Request.Headers)
    {
        // A content header is refused by the request collection and accepted by the content's, which is how
        // the two halves of System.Net.Http's split are told apart. Jint merges them back into one list.
        if (!inbound.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>) header.Value))
        {
            inbound.Content.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>) header.Value);
        }
    }

    HttpResponseMessage handled;
    try
    {
        handled = await lease.Engine.Advanced.InvokeFetchHandlerAsync(inbound, context.RequestAborted);
    }
    catch (JintException ex)
    {
        // The host's policy, not Jint's: a script failure is a 500 here, a rendered error page elsewhere.
        logger.LogWarning(ex, "the script handler failed");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return;
    }

    using (handled)
    {
        context.Response.StatusCode = (int) handled.StatusCode;
        foreach (var header in handled.Headers.Concat(handled.Content.Headers))
        {
            context.Response.Headers[header.Key] = new StringValues(header.Value.ToArray());
        }

        await handled.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
});
```

Two things worth keeping when you adapt it. The engine must not be shared between concurrent requests, and a
pooled one should have `engine.Advanced.RestoreGlobalSnapshot(...)` called on it between leases so one
request's globals are not visible to the next. Take that snapshot **after** registering the handler, since
registering is what installs `Request`/`Response`/`Headers` and a restore returns the global object to its
state at capture; the handler itself is host state and survives the restore either way, so it never needs
re-registering. And bound the script: the constraints above are what stand between a rented engine and a
handler that decides to loop forever.

</details>


## Node compatibility (opt-in)

Not everything a script expects is a web standard. `NodeStyleModuleLoader`
([npm-style packages](#npm-style-packages-opt-in)) resolves bare specifiers the way Node does; `UseNodeProcess()`
adds the other thing a script written for Node reaches for, a `process` object — most often to read
`process.env.NODE_ENV` or to branch on `process.platform`.

```csharp
var engine = new Engine(options => options.UseNodeProcess(p =>
{
    p.EnvironmentVariableAllowlist = ["NODE_ENV"];
    p.EnvironmentOverrides = new Dictionary<string, string> { ["NODE_ENV"] = "production" };
}));

engine.Evaluate("process.env.NODE_ENV"); // "production"
```

**Not one environment variable is readable until you list it.** `EnvironmentVariableAllowlist` is empty by
default, so `process.env` starts out an empty object, and only the names on it are ever looked up — the engine
never enumerates the environment block, so a variable you did not name is not read, not copied and not
reachable from the engine at all. `EnvironmentOverrides` supplies values of your own, which win over the real
environment and are filtered by the same allowlist, so a test fixture cannot accidentally widen what a script
can see; an entry whose value is `null` hides the variable rather than falling through to the real one.

| Member | What it answers |
| --- | --- |
| `process.env` | The allowed variables, materialized **once** when `process` is first touched — not a live view. Writes, and `delete`, are script-local: they never reach the real environment. |
| `process.platform` | `"win32"`, `"darwin"` or `"linux"` for the platform you are actually on; settable, e.g. to one of Node's other values. |
| `process.version` | `"v0.0.0-jint"` by default — deliberately *not* a Node version. Settable for a dependency that insists on one. |
| `process.versions` | `{ jint: "<assembly version>" }`. There is no `node` key, so feature detection has something truthful to find. |
| `process.argv` | An empty array. Jint launched no process and the script has no path of its own. |
| `process.cwd()` | `WorkingDirectory`, `"/"` by default. **Never the real current directory**, which names a deployment layout and often a user account. |
| `process.nextTick(cb, ...args)` | Queues `cb` onto the engine's job queue with the arguments forwarded, so it runs after the current script and before any timer. |
| `process.hrtime.bigint()` | A monotonic reading in nanoseconds, for measuring elapsed time. The legacy tuple form `process.hrtime()` is absent. |

`exit`, `abort`, `kill` and `chdir` are **absent rather than throwing**: an embedded script must not be able to
act on the host process, and a missing function lets a script's own `typeof` check take its other branch where
a throwing one would not. `process` is a plain object, not Node's `EventEmitter`, so there is no `on`/`emit`
either. One divergence worth knowing: Node drains its next-tick queue ahead of the promise microtask queue,
while Jint has a single job queue, so a `nextTick` callback and a `.then()` reaction run in registration order.

The global is installed lazily and non-clobbering, exactly like the web APIs — a `process` you registered
yourself is left alone whichever order the calls were made in — and a `ShadowRealm` does not get it. Every
option is read once, when `UseNodeProcess` returns, so one `Options` instance stays safe to share across
engines. **Unlike the web APIs above, this needs no particular target framework**: it compiles for every one
Jint targets.

### `node:` builtin modules (opt-in)

After resolution, the next wall a package published for Node runs into is `import 'node:path'`.
`UseNodeBuiltinModules()` supplies the ones that are pure string utilities — no file system, no process, no
network, no clock — and nothing else.

```csharp
var engine = new Engine(options => options
    .EnableModules(new NodeStyleModuleLoader(@"C:\app"))
    .UseNodeBuiltinModules());

engine.Modules.Import("./main.js"); // main.js, and anything in node_modules, may import 'node:path'
```

| Module | What it provides |
| --- | --- |
| `node:path` | `resolve`, `normalize`, `isAbsolute`, `join`, `relative`, `dirname`, `basename`, `extname`, `format`, `parse`, `toNamespacedPath`, `sep`, `delimiter`, and both flavours as `posix` and `win32`. `matchesGlob` is absent — it is the one member that is not string arithmetic. |
| `node:path/posix`, `node:path/win32` | The two flavours as modules of their own. |
| `node:querystring` | `parse`/`decode`, `stringify`/`encode`, `escape`, `unescape`. `unescapeBuffer` is absent: it answers with a `Buffer`. |
| `node:url` | `URL` and `URLSearchParams` (the engine's own WHATWG implementations, re-exported), `fileURLToPath`, `pathToFileURL`, `domainToASCII`, `domainToUnicode`. The legacy `url.parse`/`url.resolve`/`url.format` API is absent. |

**Nothing that touches a platform resource is provided, and that is not a gap to be filled later.**
`node:fs`, `node:buffer`, `node:crypto`, `node:os`, `node:child_process`, `node:http` and their kind are
deliberately absent, so a script feature-detecting one takes its other branch instead of walking into a stub.
An unknown `node:` specifier fails with a message naming what *is* available.

Both spellings work — `import 'path'` as well as `import 'node:path'` — and both name one module, because a
builtin outranks a `node_modules` package of the same name exactly as it does in Node
([ESM_RESOLVE](https://nodejs.org/api/esm.html#resolution-algorithm-specification), PACKAGE_RESOLVE step 3).
Set `AllowUnprefixedSpecifiers = false` for a tree that really does depend on an npm package called `path`,
`url` or `querystring`.

A module you register yourself wins: `engine.Modules.Add("node:path", …)` — or `Add("path", …)`, which
resolves to the same key — replaces the builtin, and is also how you supply one of the modules Jint does not
provide. Everything that is not a builtin keeps going to whichever loader you configured, in whichever order
you called the two methods, and `options.Modules.ModuleLoader` still reads back exactly what you set.

Two options are worth setting. `Platform` decides which flavour `node:path` defaults to and defaults to the
platform you are on, the same answer `process.platform` gives. `WorkingDirectory` is what `path.resolve()` and
`path.relative()` use where Node reads `process.cwd()`; like the `process` shim's, it defaults to `"/"` and
**never** answers the real current directory.

`node:querystring` and `node:url` need .NET 8 or newer, because both build on the engine's WHATWG URL
implementation. `node:path` is available on every target framework Jint has.


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
engine-affine state — and where it would do nothing but capture, the overload taking the state,
`engine.Advanced.AddLazyGlobal(name, state, static (e, s) => ...)`, hands it to a `static` factory instead.
That matters only because this registration is per engine: a capturing factory costs a display class and a
delegate for every global on every engine you build, which on `FreshEngineGlobalsBenchmark`'s forty-global
row is 32 bytes per global. There is deliberately no `Options` counterpart — a registration made there is
recorded once for the process and replayed per engine, so its closure is already a one-off.

**Per-request state behind an engine.** Every host-facing factory in this API receives the engine and nothing
else, which is a problem when the value depends on the request rather than on process-wide configuration.
`engine.Advanced.HostDefined` closes that gap: an opaque `object?` the engine never reads or interprets — the
`[[HostDefined]]` field the specification reserves on a Realm Record — so a factory that captures nothing can
still reach the scope it is running in. The alternative embedders reach for is a
`static ConditionalWeakTable<Engine, IServiceProvider>`, which takes its internal write lock every time a
host associates state with an engine, across every tenant in the process.

```c#
// once per process — the factory captures nothing, so one Options serves every engine
var options = new Options()
    .AddLazyGlobal("user", static engine =>
        JsValue.FromObject(engine, ((RequestContext) engine.Advanced.HostDefined!).User));

// per request, before you run anything
engine.Advanced.HostDefined = requestContext;
```

It is the *principal* realm's field, not the current one, so it answers the same value inside a `ShadowRealm`
callback as outside. A shadow realm is a distinct realm and gets its own, empty by specification — deliberate,
since propagating the outer request's services into sandboxed code is exactly the ambient authority a shadow
realm exists to withhold; `Host.InitializeShadowRealm` is the hook if you do want to populate it. The value
dies with the engine, and nothing in the engine clears it: a restore (below) does not touch it, so a pooled
engine keeps it across one and you replace it yourself, typically right after restoring.

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

**Sharing a module graph across pooled engines.** A host whose templates or plugins are ES modules pays for
that graph per engine: `IModuleLoader.LoadModule` is called by every engine that imports a module, so the
obvious loader re-reads and re-parses the whole graph for every engine in the pool. Prepare each module once
per *build* instead — cache the `Prepared<AstModule>` (`AstModule` being Acornima's `Module`, which an alias
keeps apart from Jint's runtime `Module`) keyed by module location, and hand it to the overload that takes an
already prepared AST, `ModuleFactory.BuildSourceTextModule(engine, in prepared)`. Name the prepared module
exactly as the engine would: `Engine.PrepareModule(code, source)` takes the name up front, and it becomes
`Module.Location` and therefore the `referencingModuleLocation` echoed back into `IModuleLoader.Resolve` for
that module's own imports — so a name derived by hand that differs from the engine's breaks relative-import
resolution with nothing to point at. `ModuleFactory.LocationOf(resolved)` *is* that rule; call it rather than
reimplementing it.

Single-flight the cache. `ConcurrentDictionary.GetOrAdd` does not run its factory under a lock, so N engines
filling a pool concurrently each prepare the same module and N-1 results are prepared only to be discarded.
Wrapping the value in a `Lazy<T>` — whose default thread-safety mode is exactly "one factory run, everyone
else waits" — is enough:

```c#
using AstModule = Acornima.Ast.Module; // Jint.Runtime.Modules.Module is a different type

private readonly ConcurrentDictionary<string, Lazy<Prepared<AstModule>>> _prepared
    = new(StringComparer.Ordinal);

public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
{
    var location = ModuleFactory.LocationOf(resolved);
    var prepared = _prepared.GetOrAdd(location,
        static key => new Lazy<Prepared<AstModule>>(() => Engine.PrepareModule(ReadSource(key), key))).Value;

    return ModuleFactory.BuildSourceTextModule(engine, in prepared);
}
```

Invalidate by build, not by file. The prepared ASTs are derived from a compilation, so key the cache to the
same identity the rest of your host already uses for that compilation and let a rebuild drop the whole set at
once, rather than expiring entries per file and serving a graph half of which is stale.

Be clear about what the thread-safety of `Prepared<T>` buys. It is safe to share and safe to run concurrently,
which is what lets one cache serve the pool — it does not make the *engines* shareable (one engine, one
thread) and it does not make the module registry shared: every engine still builds, links and evaluates its
own module records from the shared ASTs, and `engine.Modules` is per engine. That per-engine cost is real, and
it is what the pool pays no matter how much preparation is shared.

`StaticAnalysis` is a trade with a break-even, not a speed-up. Preparing with
`new ModulePreparationOptions { StaticAnalysis = false }` skips the pass that pre-publishes interpreter state
onto the parsed tree, leaving preparation at roughly the cost of a plain parse; each engine then rebuilds
lazily what the pass would have published once for all of them. Measured on `ModuleGraphEmbeddingBenchmark`'s
ten-module graph, preparation cost -47.6% time and -27.5% allocation, while each engine materializing that
graph from the shared cache paid +4.8% time and +9.5% allocation — break-even, on that graph, at about ten
engines on time and about two on allocation. A long-lived pool should therefore keep the default (`true`);
the option is for preparation that sits on a latency-critical path — a cold start, a rebuild in a dev loop —
or for a host whose prepared programs outnumber the engines that ever run them.
`ScriptPreparationOptions.StaticAnalysis` is the same option for scripts.

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
separate engines. Note that surviving intrinsic pollution is a surviving *binding*, not just a surviving
property: bare identifiers resolve through the global's prototype chain, so `Object.prototype.leaked = 1` in
one evaluation makes `leaked` a name the next one can read with no qualifier. (The global's own
`[[Prototype]]` is captured and restored, so a `setPrototypeOf(globalThis, …)` is reverted.) A snapshot also
keeps its engine and every captured value strongly reachable, so do not cache one past that engine's
lifetime.

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

## Profiling scripts (opt-in)

When a script is slow and you want to know which of *its* functions is responsible, opt the engine in and
bracket the run. The profiler is **evented, not sampling** — it records at the call boundary on the engine's
own thread, because inspecting a running engine from another thread is not something Jint supports.

```csharp
var engine = new Engine(options => options.Profiling.Enabled = true);

engine.Advanced.StartProfiling();
engine.Execute(script);
var profile = engine.Advanced.StopProfiling();

using var file = File.Create("run.speedscope.json");
profile.WriteSpeedscopeJson(file);
```

Drop the file onto [speedscope.app](https://www.speedscope.app) — it is written in that tool's
[evented profile format](https://www.speedscope.app/file-format-schema.json), in nanoseconds. The
`ScriptProfile` is also readable directly: `Frames` (name, file, line, column), `Events` (open/close plus a
timestamp), `Truncated` and `Duration`. It holds strings and numbers only, so keeping a profile does not keep
the engine that produced it alive.

Worth knowing before you read a profile:

- **It costs.** Every recorded call pays a frame lookup and a 16-byte event, so a profiled run is slower than
  an unprofiled one. An engine that is not profiling pays one null test per call-stack push and pop.
- **`Options.Profiling.MaxEvents`** (default one million) caps a session. Reaching it stops recording, closes
  whatever was open and sets `Truncated`; the event stream stays balanced and replayable either way — through
  exceptions, through `ResetCallStack()`, and through a session stopped mid-call.
- **A tail call is a close followed by an open**, since it replaces its caller's frame rather than nesting in
  it. Unbounded tail recursion therefore profiles as a flat run of siblings, not as a bottomless tree.
- **Some calls are elided**, and always in a way that keeps the tree coherent rather than merely incomplete —
  the eliding call has no frame either, so anything it calls is still recorded at the depth the call stack
  really has. Those are: trivial built-ins taken through the frameless fast-call lane (`Math.abs(1)` on a warm
  call site), callbacks a built-in invokes per element (`arr.map(cb)` records `map`, not `cb`), and promise
  reaction handlers (`p.then(cb)` records `then`, not `cb`).

`StartProfiling()` throws `InvalidOperationException` when `Options.Profiling.Enabled` is false, which is the
default — an engine running untrusted script can refuse profiling outright.

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

### Bounding an operation that makes several calls into the engine

Every call into the engine — `Execute`, `Evaluate`, `Invoke`, `Call` — is a complete run, and the engine
resets the constraints around each one. `TimeoutInterval` therefore bounds *one* call, not a sequence of
them: a loop such as `foreach (var row in rows) engine.Invoke("render", row);` gives every row the full
interval to itself, so the loop as a whole is unbounded.

When the thing you need to bound is your own operation — a whole render, a whole request — rather than one
call, use `OperationDeadlineConstraint`. Its budget is started and stopped by you, and the engine's
per-call reset leaves it alone:

```c#
// one instance per engine, and you keep the reference
var deadline = new OperationDeadlineConstraint();
var engine = new Engine(options => options.Constraint(deadline));

deadline.Begin(TimeSpan.FromSeconds(2), cancellationToken);
try
{
    foreach (var row in rows)
    {
        engine.Invoke("render", row);
    }
}
finally
{
    deadline.End();
}
```

The whole loop shares the two seconds. When it runs out, the next call fails with a `TimeoutException` —
the same exception the built-in timeout throws. If the token is cancelled, the call fails with a real
`OperationCanceledException` carrying that token, so cancellation you asked for stays distinguishable from
a script failure. Outside a `Begin`/`End` pair the constraint is inert, which makes it safe to leave
registered on a pooled engine between operations. The two constraints are independent, so you can keep a
`TimeoutInterval` as a per-call ceiling alongside it.

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

### Surviving an unbounded recursion

A script that recurses without bound does not throw. It exhausts the native stack of the thread it is
running on, and that ends the host process: no exception, nothing to `catch`, nothing in the log but an
exit code. Every route into a function body can do it — a call, `new`, a getter or setter, a
`valueOf`/`toString` coercion, a Proxy trap, a callback a built-in invokes, a host delegate that calls
back into the engine.

```c#
var engine = new Engine(options =>
{
    options.Constraints.StackOverflowGuard = true;
});
```

With the guard on, every entry into a script function first checks that the native stack has not been used
up, and the same script raises `RangeError: Maximum call stack size exceeded` while there is still room to
unwind — an ordinary JavaScript error, catchable by the script itself and by your
`catch (JavaScriptException)`, with the engine still usable afterwards. It measures the remaining stack
rather than counting calls, so it covers all of those routes and adapts to the thread the engine runs on:
a host that provisions a larger stack gets proportionally more depth, rather than the frame count someone
had to guess in advance.

A proper tail call in a strict function is exempt, and does not need the guard: it replaces the caller's
frame instead of stacking one on top, so the recursion consumes no native stack however deep it runs. The
check sits on the entries that do add a frame, so a strict tail recursion neither pays for it nor is
stopped by it. Sloppy-mode recursion, a call out of tail position, and the non-call routes above are what
remain in scope.

It is off by default because the check runs at every entry into a script function and that is not free.
The benchmark gate measured recursion-heavy workloads (`Fib`, `DeepSum`, `Tak`) 1.7–2.3% slower with it
on, with hot shallow calls unaffected. Enable it when the engine runs script you did not write, where a
couple of percent on deep recursion in exchange for keeping your process is plainly the trade you want.
Leave it off when every script is yours and you can bound it yourself.

`options.LimitRecursion(n)` answers a different question and composes with it. The recursion limit counts
frames and is checked before the callee is entered, so where it is configured it is what fires; the guard
answers only when no limit was set, or when the limit was set higher than the thread's stack can actually
hold — which is easy to do by accident, since how many frames a stack holds is not something a host can
see. A third and older setting, `options.Constraints.MaxExecutionStackCount`, continues the call chain on
a fresh thread instead of throwing; it is checked at call expressions only, so it does not cover the other
routes above, and it takes precedence over the guard when both are set.

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

If you serve the same modules from a pool of engines, see **Sharing a module graph across pooled engines**
under [Embedding performance](#embedding-performance): a loader that caches prepared modules keeps every
engine but the first from parsing them again.

### How a module is named

Every module has a location — the string it knows itself by, `Module.Location` — and for a module a loader
produced, that string is `ModuleFactory.LocationOf(resolved)`: the `LocalPath` of an absolute `file:` uri, and
otherwise `ResolvedSpecifier.Key`, exactly as your `Resolve` wrote it. It is never null.

It is worth getting right because four things read it:

- your own `ModuleLoader.Resolve`, as `referencingModuleLocation`, whenever that module imports something
  relative;
- `error.stack`, and any `SyntaxError` a malformed module raises, through `SourceLocation.SourceFile`;
- the debugger, which keys breakpoints on it — `new BreakPoint(location, line, column)`;
- `import.meta.url`, if you report it. Jint leaves `import.meta` to the host, so it is your
  `Host.GetImportMetaProperties` override that puts the location there.

So a loader serving modules over a transport should key them by the whole url rather than by a path. A module
that knows itself as `/lib/entry.js` has no origin left for its own `./dep.js` to resolve against; one that
knows itself as `http://localhost/lib/entry.js` resolves it the way a browser would. The key is used verbatim
rather than `Uri.AbsoluteUri`, which canonicalizes — strips a default port, lowercases the host, collapses
dot segments, percent-encodes — and whose .NET Framework and .NET Core parsers disagree on parts of that, so
a canonicalized name would differ per target framework and could drift from the key the module map is cached
under. Loading from disk is unaffected: a `file:` uri keeps its path, which is what `DefaultModuleLoader`
resolves against a filesystem base path.

One consequence to weigh if the url carries anything sensitive. This string reaches script through
`new Error().stack`, so a loader keying modules by `https://svc:s3cr3t@internal.corp/lib/a.js?apikey=...`
hands untrusted script the credential and the api key. A browser's `import.meta.url` carries the query too,
so this follows from naming a module by its url at all; keep secrets out of the key, or out of the url.

If you name modules yourself — preparing each one once and sharing the `Prepared<AstModule>` across pooled
engines, per **Sharing a module graph across pooled engines** — call `ModuleFactory.LocationOf` for the name
rather than deriving it by hand. A name that differs from the engine's own answer breaks relative-import
resolution with nothing to point at.

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

## Code coverage (opt-in)

An engine can count what it executes, so a host can tell which parts of a script actually ran — a test
harness reporting coverage of embedded rules, a CI gate over business scripts, a dead-code audit.

```c#
var engine = new Engine(options => options.Coverage.Enabled = true);

engine.Execute("function f(n) { if (n > 0) { return 'positive'; } return 'other'; } f(1); f(2);", "rules.js");

foreach (var source in engine.Advanced.GetCoverage().Sources)
{
    foreach (var entry in source.Entries)
    {
        // rules.js  line 1  Statement  x2   ...
        Console.WriteLine($"{source.Name} line {entry.Start.Line} {entry.Kind} x{entry.HitCount}");
    }
}

engine.Advanced.ResetCoverage(); // start a fresh measurement
```

- `Options.Coverage.Granularity` is `Statements` by default, or `Functions` to report function-body entries
  only.
- A hit count is how many times the construct was *entered*: a statement in a loop counts once per iteration,
  a function body once per call (and once per resumption for a generator or an `await`).
- Only constructs that ran appear. The report is the covered set, not a ratio — derive the denominator by
  walking the AST you prepared if you need one. Block statements are never reported; the statements inside
  them are.
- Counters are per engine, so a `Prepared<Script>` shared across a pool of engines keeps a separate count per
  engine and the shared AST is untouched. `Options` stays shareable.
- `GetCoverage()` and `ResetCoverage()` throw `InvalidOperationException` on an engine that did not enable
  collection, rather than reporting an empty result that looks like a script which never ran.

Coverage is collected through the same per-statement path the debugger and the exact execution constraints
use, so an engine collecting it runs the instrumented interpreter path and its tight-loop optimizations are
disarmed — measured code is not byte-for-byte the code an uninstrumented engine runs. That is the normal
bargain for statement-level coverage, and the reason the option is off by default. An engine that leaves it
off pays nothing: the per-statement path the counting rides on is not armed at all.

## Security

Jint is an in-process interpreter, not an operating-system security boundary. Running
untrusted scripts safely requires explicit limits, a minimal host capability surface, fresh
engines across trust domains, and process-level isolation. See the
[threat model](.github/THREAT_MODEL.md) for the supported boundaries, known limitations, and
a hardened deployment baseline.

- Define memory limits, to prevent allocations from depleting the memory.
- Enable/disable usage of BCL to prevent scripts from invoking .NET code.
- Limit number of statements to prevent infinite loops.
- Limit depth of calls to prevent deep recursion calls.
- Define a timeout, to prevent scripts from taking too long to finish.
- Turn an unbounded recursion into a catchable error rather than a stack overflow that ends the process
  (see [Surviving an unbounded recursion](#surviving-an-unbounded-recursion); off by default).

## Branches and releases

- The recommended branch is __main__, any PR should target this branch
- The __main__ branch is automatically built and published on [MyGet](https://www.myget.org/feed/Packages/jint). Add this feed to your NuGet sources to use it: https://www.myget.org/F/jint/api/v3/index.json
- The __main__ branch is occasionally published on [NuGet](https://www.nuget.org/packages/jint)
