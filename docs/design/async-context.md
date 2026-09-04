# AsyncContext in Jint — design

**Status: design only. No implementation exists, and none should land before the maturity decision in
[§6.3](#6-3-proposal-maturity) is taken.**

This document maps the TC39 [AsyncContext proposal](https://tc39.es/proposal-async-context/) onto Jint's async
machinery: where the agent-level state lives, what the mapping looks like, every point at which the mapping is
captured or restored, what it costs an engine that never uses it, how a .NET host participates, and what has to
be true before any of it is worth merging.

Everything normative here was read from the proposal's own specification source
(`tc39/proposal-async-context@master`, `spec.html`) and from
[`WEB-INTEGRATION.md`](https://github.com/tc39/proposal-async-context/blob/master/WEB-INTEGRATION.md), not from
secondary documentation. Jint line references are against `upstream/main` at
`46615b663` ("Add opt-in WHATWG URL and URLSearchParams (#3094)").

---

## 1. What the proposal is, and why an embedder would want it

`AsyncContext` is implicit, flow-following state: a value set before an asynchronous operation starts is still
readable inside the callback that eventually runs, without threading a parameter through every frame in
between. It is the standardisation of Node's `AsyncLocalStorage`.

```js
const requestId = new AsyncContext.Variable({ name: "requestId", defaultValue: null });

requestId.run("req-42", () => {
  setTimeout(() => {
    console.log(requestId.get());   // "req-42" — a different event-loop turn entirely
  }, 100);
});

console.log(requestId.get());       // null — the mapping is restored when run() returns
```

The surface is two classes on one namespace object
([`#sec-asynccontext-object`](https://tc39.es/proposal-async-context/#sec-asynccontext-object)):

| Member | Spec anchor |
| --- | --- |
| `new AsyncContext.Variable({ name, defaultValue })` | [`#sec-asynccontext-variable`](https://tc39.es/proposal-async-context/#sec-asynccontext-variable) |
| `AsyncContext.Variable.prototype.run(value, func, ...args)` | [`#sec-asynccontext-variable.prototype.run`](https://tc39.es/proposal-async-context/#sec-asynccontext-variable.prototype.run) |
| `AsyncContext.Variable.prototype.get()` | [`#sec-asynccontext-variable.prototype.get`](https://tc39.es/proposal-async-context/#sec-asynccontext-variable.prototype.get) |
| `get AsyncContext.Variable.prototype.name` | [`#sec-asynccontext-variable.prototype.name`](https://tc39.es/proposal-async-context/#sec-asynccontext-variable.prototype.name) |
| `new AsyncContext.Snapshot()` | [`#sec-asynccontext-snapshot`](https://tc39.es/proposal-async-context/#sec-asynccontext-snapshot) |
| `AsyncContext.Snapshot.prototype.run(func, ...args)` | [`#sec-asynccontext-snapshot.prototype.run`](https://tc39.es/proposal-async-context/#sec-asynccontext-snapshot.prototype.run) |
| `AsyncContext.Snapshot.wrap(fn)` | [`#sec-asynccontext-snapshot.wrap`](https://tc39.es/proposal-async-context/#sec-asynccontext-snapshot.wrap) |

**Why an embedder cares.** Jint's answer to "which request is this engine serving" is already good:
`Engine.HostDefined` (`Jint/Engine.Globals.cs`) holds the principal realm's `[[HostDefined]]`, and
a pooled engine keeps it across a `RestoreGlobalSnapshot`. What it cannot answer is "which *logical operation
within* this request is running right now", because a pooled engine serving one request may still interleave
several script-initiated flows across event-loop turns — a `setTimeout` callback, a promise reaction, a resumed
`await`. A host `ConsoleSink` (`Jint/WebApi/ConsoleSink.cs`) writing a correlation id today can only read the
per-engine `HostDefined`; with AsyncContext it can read the per-*flow* value that the script — or the host —
installed. That is the same motivation the campaign issue records, and it is the reason the host-facing surface
in [§4](#4-host-facing-surface) is part of the design rather than an afterthought.

---

## 2. Spec mapping

### 2.1 Where the agent slot lives

The proposal adds one field to the **Agent Record**
([`#sec-agents`](https://tc39.es/proposal-async-context/#sec-agents)):

> `[[AsyncContextMapping]]` — an Async Context Mapping — "A map from the AsyncContext.Variable instances to the
> saved ECMAScript language value. The map is initially empty."

Jint already models the Agent Record, and it already stores exactly one other Agent Record field there:

```csharp
// Jint/Agent.cs
/// <summary>
/// https://tc39.es/ecma262/#sec-agents , still a work in progress, mostly placeholder
/// </summary>
internal sealed class Agent
{
    private readonly List<JsValue> _keptAlive = new();   // the Agent Record's [[KeptAlive]]
    ...
}
```

`Engine._agent` is a readonly field, one `Agent` per `Engine`, reached through `Engine.AddToKeptObjects`
(`Jint/Engine.cs:1044`) and `Engine.ClearKeptObjects` (`Jint/Engine.cs:1744`). So the mapping belongs on
`Jint.Agent`, next to `[[KeptAlive]]`, and not on a new `Engine` field:

```csharp
internal sealed class Agent
{
    /// <summary>
    /// The Agent Record's [[AsyncContextMapping]]. <see langword="null"/> *is* the empty mapping — see
    /// AsyncContextMapping — which is what every engine carries until script first calls
    /// AsyncContext.Variable.prototype.run, and what it goes back to as soon as that call unwinds.
    /// </summary>
    internal AsyncContextMapping? AsyncContextMapping;
}
```

One `Engine` is one agent. `ShadowRealmConstructor` calls `_engine._host.CreateRealm()`
(`Jint/Native/ShadowRealm/ShadowRealmConstructor.cs:38`) — a new Realm Record inside the *same* Engine — so a
shadow realm shares the mapping, which is what the spec requires and is analysed in [§6.2](#6-2-shadowrealm).

The engine-level accessors are two `AggressiveInlining` methods on `Engine`, so that no call site outside
`Jint/Native/AsyncContext/` ever touches `_agent` directly:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal AsyncContextMapping? AsyncContextSnapshot() => _agent.AsyncContextMapping;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal AsyncContextMapping? AsyncContextSwap(AsyncContextMapping? mapping)
{
    var agent = _agent;
    var previous = agent.AsyncContextMapping;
    agent.AsyncContextMapping = mapping;
    return previous;
}
```

Those are literally
[`AsyncContextSnapshot()`](https://tc39.es/proposal-async-context/#sec-asynccontextsnapshot) and
[`AsyncContextSwap(snapshotMapping)`](https://tc39.es/proposal-async-context/#sec-asynccontextswap), two steps
each, so keeping the spec names is free and makes every call site checkable against the algorithm it implements
(the **Spec references** convention in `AGENTS.md`).

### 2.2 The mapping representation: copy-on-write array, `null` for empty

The spec type is a *List* of records with unique keys
([`#sec-asynccontext-mapping-record-specification-type`](https://tc39.es/proposal-async-context/#sec-asynccontext-mapping-record-specification-type)),
and `Variable.prototype.run` builds a **new** list on every call — steps 4-7 copy every record whose key is not
this variable, then append one:

> 4. Let _asyncContextMapping_ be a new empty Async Context Mapping.
> 5. For each Async Context Mapping Record _p_ of _previousContextMapping_, do
>    1. If SameValueZero(_p_.[[AsyncContextKey]], _asyncVariable_) is *false*, then … Append _q_ to _asyncContextMapping_.
> 6. Let _p_ be the Async Context Mapping Record { [[AsyncContextKey]]: _asyncVariable_, [[AsyncContextValue]]: _value_ }.
> 7. Append _p_ to _asyncContextMapping_.

Copy-on-write over a flat array *is* that algorithm, and the expected size supports it. The champions'
[`MEMORY-MANAGEMENT.md`](https://github.com/tc39/proposal-async-context/blob/master/MEMORY-MANAGEMENT.md) says a
context is expected to hold "a very limited number of entries (a single-digit amount in most cases)". A
persistent hash-array-mapped trie only starts to pay for itself well above that, and below it costs an
allocation per node plus pointer-chasing on a lookup that a linear scan of four to eight references answers from
one cache line.

```csharp
/// <summary>
/// An Async Context Mapping: https://tc39.es/proposal-async-context/#async-context-mapping
/// Immutable — every mutation produces a new instance, which is what makes a captured mapping a snapshot
/// rather than a live view.
/// </summary>
internal sealed class AsyncContextMapping
{
    private readonly AsyncContextEntry[] _entries;   // never empty: the empty mapping is represented by null

    internal static AsyncContextMapping With(AsyncContextMapping? mapping, JsAsyncContextVariable key, JsValue value);
    internal bool TryGet(JsAsyncContextVariable key, out JsValue value);
}

[StructLayout(LayoutKind.Auto)]
internal readonly record struct AsyncContextEntry(JsAsyncContextVariable Key, JsValue Value);
```

Three properties this shape gives us, each load-bearing later:

- **`null` is the empty mapping.** Nothing allocates an empty mapping object; an engine that never touched
  AsyncContext holds `null` on the agent, in every captured field, and in every job. This is the same disarm
  shape as `Engine._atomicsWaiterDeadlines` (`Jint/Engine.Pump.cs:26`) and `Engine._webApi`
  (`Jint/Engine.WebApi.cs:17`), and for the same reason: the field being null is the whole cost of the check.
- **Keys compare by reference.** `SameValueZero` on an `AsyncContext.Variable` instance — always an object — is
  reference equality, so a lookup is `ReferenceEquals` over an array with no hashing and no `JsValue`
  comparison semantics involved.
- **A snapshot is one reference.** Capturing is a field read; there is no copy, no defensive clone, and no
  lifetime concern beyond the retention analysed in [§6.4](#6-4-retention).

`Variable.prototype.get` on a `null` mapping is one null test and a return of
`[[AsyncVariableDefaultValue]]`.

### 2.3 The swap: a null-mapping fast path, and no `try`/`finally` on it

Every restore point has the same shape, and the shape matters more than it looks. Wrapping the reaction-job body
in an unconditional `try`/`finally` would put a protected region on Jint's hottest asynchronous path for the
benefit of a feature almost no engine uses. The fix is to split the arm:

```csharp
// PromiseOperations.RunReactionJob
var mapping = reaction.AsyncContextMapping;
if (mapping is null && engine.AsyncContextSnapshot() is null)
{
    RunReactionJobCore(engine, reaction, value);      // byte-identical to today's body, no protected region
    return;
}

RunReactionJobWithAsyncContext(engine, reaction, value, mapping);   // [MethodImpl(NoInlining)], has the try/finally
```

Two predictable null tests on an engine that never uses the feature; the slow arm is out of line so it does not
inflate the caller's frame or inhibit inlining. The same two-arm split applies at each of the restore points in
[§3](#3-capture-and-restore-inventory).

The restore must be a **`finally`**, not a `catch`, and not a post-call statement. The spec expresses this as
`Completion(Call(...))` followed by an unconditional `AsyncContextSwap` — an abrupt completion still swaps back.
Jint has exception classes the spec does not model and which must not be allowed to leave a mapping installed:
`TimeoutException`, `ExecutionCanceledException`, `RecursionDepthOverflowException`, `MemoryLimitExceededException`,
and `PromiseRejectedException`. Note also that `RunReactionJob` already *swallows* `JavaScriptException` on two
of its three arms (`Jint/Native/Promise/PromiseOperations.cs:44-49,60-64`); the swap-back has to happen for the
swallowed case too, which a `finally` gives for free and a `catch` would not.

### 2.4 The one-way arming question, and why we do not need a flag

An obvious optimisation is a one-way `Engine._asyncContextArmed` bool, set the first time a `Variable` is
constructed, so that capture sites can skip their field read entirely. It is not worth it: the capture is a
single load of a field that is already in cache next to `[[KeptAlive]]`, and the flag would buy nothing at the
restore sites, which must test the reaction's own field regardless — a job created while a mapping was installed
must restore it even though the agent field has since gone back to `null`.

Where "only pay when it is in use" *does* buy something real is the storage question in
[§7.2](#7-2-the-eight-bytes-on-promisereaction), and there the lever is allocation shape rather than a flag.

---

## 3. Capture and restore inventory

This is the exhaustive list. Each row names the spec clause, the Jint site, and whether the point is a capture
(snapshot taken and stored) or a restore (swap in, run, swap back).

### 3.1 Points the spec defines

| # | Spec clause | Capture / restore | Jint site |
| --- | --- | --- | --- |
| 1 | [`PerformPromiseThen`](https://tc39.es/proposal-async-context/#sec-performpromisethen) step *"Let mapping be AsyncContextSnapshot()"* | capture, onto both reaction records | `PromiseOperations.PerformPromiseThen` — **both** overloads, `Jint/Native/Promise/PromiseOperations.cs:136` and `:195` |
| 2 | [`NewPromiseReactionJob`](https://tc39.es/proposal-async-context/#sec-newpromisereactionjob) step *"Let previousContextMapping be AsyncContextSwap(reaction.[[PromiseAsyncContextMapping]])"* | restore | `PromiseOperations.RunReactionJob`, `Jint/Native/Promise/PromiseOperations.cs:33` |
| 3 | [`NewPromiseResolveThenableJob`](https://tc39.es/proposal-async-context/#sec-newpromiseresolvethenablejob) | capture at job creation, restore inside the job | `PromiseOperations.NewPromiseResolveThenableJob`, `Jint/Native/Promise/PromiseOperations.cs:99` — the returned `Action` closes over the mapping |
| 4 | [`GeneratorStart`](https://tc39.es/proposal-async-context/#sec-generatorstart) | capture onto the generator | `GeneratorInstance.GeneratorStart`, `Jint/Native/Generator/GeneratorInstance.cs:128` |
| 5 | [`GeneratorResume`](https://tc39.es/proposal-async-context/#sec-generatorresume) / [`GeneratorResumeAbrupt`](https://tc39.es/proposal-async-context/#sec-generatorresumeabrupt) | restore | `GeneratorInstance.GeneratorResume` `:142`, `GeneratorResumeAbrupt` `:170` |
| 6 | [`AsyncGeneratorStart`](https://tc39.es/proposal-async-context/#sec-asyncgeneratorstart) / [`AsyncGeneratorResume`](https://tc39.es/proposal-async-context/#sec-asyncgeneratorresume) | capture / restore | `Jint/Native/AsyncGenerator/AsyncGeneratorInstance.cs` |
| 7 | [`YieldExpression` evaluation](https://tc39.es/proposal-async-context/#sec-generator-function-definitions-runtime-semantics-evaluation) | restore the generator's own mapping after the yield returns | **Nothing needed in Jint** — see [§3.4](#3-4-jint-shapes-that-change-the-answer) |
| 8 | [`ExecuteModule`](https://tc39.es/proposal-async-context/#sec-source-text-module-record-execute-module) — *"Let moduleContextMapping be a new empty list"* | swap to the **empty** mapping for the whole evaluation | `SourceTextModuleRecord.ExecuteModule`, `Jint/Runtime/Modules/SourceTextModuleRecord.cs:396` |
| 9 | [`FinalizationRegistry ( cleanupCallback )`](https://tc39.es/proposal-async-context/#sec-finalization-registry-cleanup-callback) — *"Set finalizationRegistry.[[FinalizationRegistryAsyncContextMapping]] to AsyncContextSnapshot()"* | capture at construction | `FinalizationRegistryInstance` ctor, `Jint/Native/FinalizationRegistry/FinalizationRegistryInstance.cs:16` — **blocked, see [§3.3](#3-3-finalizationregistry-is-blocked-in-jint)** |
| 10 | [`HostEnqueueFinalizationRegistryCleanupJob`](https://tc39.es/proposal-async-context/#sec-host-cleanup-finalization-registry) | restore the construction-time mapping around the cleanup | same file, `Observer` finalizer at `:64` — **blocked** |
| 11 | [`HostPromiseRejectionTracker`](https://tc39.es/proposal-async-context/#sec-host-promise-rejection-tracker) requirements list | snapshot at the call, swap around the notification | `Host.HostPromiseRejectionTracker` `Jint/Runtime/Host.cs:473` → `Engine.OnPromiseRejectionTracker`. Host territory; see [§4.4](#4-4-hostpromiserejectiontracker) |
| 12 | The API itself: `Variable.run`, `Snapshot` ctor, `Snapshot.prototype.run`, `Snapshot.wrap` | capture and restore | new `Jint/Native/AsyncContext/` |

**Row 9 corrects a widespread misreading.** Earlier accounts of the proposal — and more than one summary still
in circulation — say the finalization-registry cleanup callback runs under an *empty* mapping. The current draft
does not: it captures at construction and restores that.

> 1. Let _previousContextMapping_ be AsyncContextSwap(_finalizationRegistry_.[[FinalizationRegistryAsyncContextMapping]]).
> 1. Let _cleanupResult_ be Completion(CleanupFinalizationRegistry(_finalizationRegistry_)).

Empty-mapping semantics appear exactly once in the proposal, and it is **module evaluation** (row 8), not the
finalization registry.

**Row 1 is the whole `await` story.** Jint implements `await` through the engine-internal
`PerformPromiseThen(Engine, JsPromise, IPromiseContinuation)` overload
(`Jint/Native/Promise/PromiseOperations.cs:195`), which is the spec's `Await` steps 3-10 with the observable
handler closures elided. Because the mapping rides on the `PromiseReaction` record and `RunReactionJob` swaps it
in around `continuation.Invoke(...)`, the async function resumes under the mapping that was current at its
`await` — with no change at all to `JintAwaitExpression`, `AsyncFunctionInstance` or `AsyncFunctionResume`. That
also composes for a function with several awaits: the reaction job installs the mapping, `AsyncFunctionResume`
(`Jint/Runtime/Interpreter/Expressions/JintAwaitExpression.cs:218`) runs the body forward under it, the next
`await`'s `PerformPromiseThen` captures the then-current mapping, and the job's `finally` swaps back.

`AsyncFunctionStart` / `AsyncBlockStart` (`Jint/Runtime/Interpreter/JintFunctionDefinition.cs:241,281`) need
nothing: the proposal does not patch them, so the synchronous prefix of an async function inherits the caller's
mapping, which is what running it on the caller's stack already gives.

### 3.2 Points the spec deliberately does **not** define

Getting these wrong is worse than missing a capture, because an unwanted capture silently pins state and
produces plausible-but-wrong values.

- **`addEventListener` / `dispatchEvent` do not touch the mapping.** From
  [`WEB-INTEGRATION.md`](https://github.com/tc39/proposal-async-context/blob/master/WEB-INTEGRATION.md), verbatim:

  > `.addEventListener` does _not_ take any snapshot, it just stores the callback as it's already doing today;
  > `.dispatchEvent` does _not_ read or set the current active context.

  For synchronous dispatch "the context that will be active when the event listeners are called is the same as
  the one active when the event is dispatched", which falls out of doing nothing. So the `JsEventTarget` work
  in the still-open events PR (#3096) must add **no** AsyncContext code. Where the web platform wants the
  originating context available it exposes it explicitly as an `originSnapshot` property on the event, which is
  a per-event-type decision and out of scope here. This is worth a comment in the events code so that a later
  agent does not "fix" the omission.

- **`HostEnqueuePromiseJob` does not capture.** The spec captures per *scheduler call site*
  (`PerformPromiseThen`, `NewPromiseResolveThenableJob`), never generically at enqueue.

- **Therefore `Engine.AddToEventLoop(Action)` must not capture.** The generic `Action` job stays
  mapping-transparent. `Jint/Engine.cs:995` is used both by spec-driven paths that capture at their own site and by
  engine-internal bookkeeping that must stay transparent; a capture there would be wrong for the second group
  and redundant for the first. **`EventLoopJob` and `EventLoop` are unchanged by this design** — nothing is
  added to the struct, the queue, the generation fence or `RunAvailableContinuations`. A caller that needs a
  mapping carried closes over it in its own delegate, exactly as `NewPromiseResolveThenableJob` does. That is a
  property worth protecting: the event loop is shared by every engine and this feature should not appear in it.

- **`CreateResolvingFunctions`, the `Promise` executor, and `Promise.prototype.finally`** are not patched. The
  executor runs synchronously and inherits the ambient mapping; `finally` is defined in terms of `then` and
  inherits row 1.

### 3.3 FinalizationRegistry is blocked in Jint

Rows 9 and 10 cannot be implemented as specified, and the reason is pre-existing:

```csharp
// Jint/Native/FinalizationRegistry/FinalizationRegistryInstance.cs:64
~Observer()
{
    try { _callable.Callback.Call(Undefined); }
    catch { /* ... */ }
}
```

Jint runs the cleanup callback from the **CLR finalizer thread**, directly in a finalizer, rather than as a job
on the event loop; `CleanupFinalizationRegistry` at `:22` is an empty stub. Writing
`_agent.AsyncContextMapping` from that thread would race the engine thread with no synchronisation, and the
`finally` that restores it would race the engine thread's own restore — a mapping torn in half is exactly the
class of bug that would be undebuggable.

So the design's position is: **implement rows 9 and 10 only once FinalizationRegistry cleanup runs as an
event-loop job on the engine thread**, which is a separate correctness fix worth doing on its own merits. When
it is done, the capture has an obvious home — `JobCallback` (`Jint/Runtime/Host.cs:492`) already carries an
`object? HostDefined` slot, and `Host.MakeJobCallBack` (`:461`) is already the single construction point. Until
then the registry simply does not participate, which is a documented gap and not a silent one.

### 3.4 Jint shapes that change the answer

Four places where Jint's implementation strategy makes the naive translation wrong.

**(a) `yield` needs no swap of its own.** The spec's `YieldExpression` steps snapshot before `Yield(...)` and
swap back after, because in the specification's model the generator's execution context is *suspended in place*
and resumed in place, so the resumer's mapping would still be installed when control returns into the body.
Jint's generators are replay-driven: control re-enters the body from `GeneratorResume` /
`GeneratorResumeAbrupt` (`Jint/Native/Generator/GeneratorInstance.cs:142,170`), which are row 5's restore
points. The swap there already covers "the body runs under the generator's mapping", so
`JintYieldExpression` (`Jint/Runtime/Interpreter/Expressions/JintYieldExpression.cs`) needs nothing. **This
equivalence must be pinned by a test rather than asserted** — see [§8](#8-test-strategy), case G3.

**(b) Jint has extra event-loop hops with no spec counterpart, and each one drops the mapping.** These are
places where Jint enqueues a *continuation of an ongoing spec algorithm* through the raw `AddToEventLoop(Action)`
path. The reaction job that scheduled them has already restored the previous mapping by the time the queued
delegate runs, so without an explicit carry the flow is silently severed mid-algorithm:

| Site | What it continues |
| --- | --- |
| `Jint/Runtime/Interpreter/Statements/JintForInForOfStatement.cs:1227` | `for await` resumption inside an async generator — `ForAwaitGeneratorContinuation.Invoke` runs inside a reaction job and then re-enqueues |
| `Jint/Native/Array/ArrayConstructor.cs:451`, `:476` | `Array.fromAsync` loop step after an element resolves |
| `Jint/Native/Array/ArrayConstructor.cs:350`, `:624`, `:640` | the same, for the async-iterator and array-like arms |

The remedy is one internal helper, used only at these sites, that closes over the mapping current at enqueue:

```csharp
// Engine
internal void AddToEventLoopPreservingAsyncContext(Action continuation);
```

It must not be spelled as an overload of `AddToEventLoop` and must not become the default, for the reason in
[§3.2](#3-2-points-the-spec-deliberately-does-not-define). Each use site gets a comment naming the spec algorithm
whose continuation it is.

`Atomics.waitAsync`'s settle paths (`Jint/Native/Atomics/AtomicsInstance.cs:346,815`) are deliberately **not**
on this list: they resolve a promise, and the reactions attached to that promise carry their own mappings from
row 1. Resolving a promise is not a capture point in the spec and must not become one here.

**(c) The replay interpreter can run `run()` twice.** `JintStatementList.Execute` resumes at a saved statement
index (`Jint/Runtime/Interpreter/JintStatementList.cs:139`) and **re-executes that whole statement**; the await
inside it short-circuits to its cached value (`AsyncFunctionInstance._completedAwaits`), but sibling
sub-expressions evaluated before the suspension are evaluated again. So in

```js
async function f() { const x = v.run(1, sideEffect) + await g(); }
```

`sideEffect` runs twice. This is a pre-existing Jint quirk with nothing to do with AsyncContext, but AsyncContext
gives scripts a new and very visible way to trip over it, and a bug report will arrive phrased as an AsyncContext
bug. The swaps themselves stay balanced (each `run` restores in its own `finally`), so the *mapping* cannot be
corrupted by the replay — only the user callback is re-entered. Document it; do not attempt to fix it here.

**(d) `run` must not be a tail-call site.** `AsyncContext.Variable.prototype.run` and
`Snapshot.prototype.run` invoke a user function and must regain control to restore. They are built-in
(`ClrFunction`) frames, so `ScriptFunction.ContinueTailCalls` returns through them normally, but the
implementation must not be "cleverly" routed through the tail-call trampoline.

---

## 4. Host-facing surface

The proposal anticipates hosts needing this. `CreateAsyncContextSnapshotObject`
([`#sec-createasynccontextsnapshotobject`](https://tc39.es/proposal-async-context/#sec-createasynccontextsnapshotobject))
exists solely for them:

> This abstract operation is meant for hosts to use, and it is not used in this specification.

### 4.1 The minimal pair

Two members on `Engine.AdvancedOperations`, modelled on the existing
`WithRestoredGlobals(GlobalSnapshot, Action)` shape in `Jint/Engine.GlobalSnapshot.cs` — which restores in a
`finally` and documents that "skipping it on the throwing path is precisely the mistake this method exists to
prevent":

```csharp
/// <summary>
/// Captures the engine's current async context — the Agent Record's [[AsyncContextMapping]] — so that a
/// host-initiated callback can later be run under it.
/// </summary>
public AsyncContextSnapshot CaptureAsyncContext();

/// <summary>
/// Runs <paramref name="action"/> with <paramref name="snapshot"/> installed as the engine's async context,
/// restoring the previous one afterwards — including when <paramref name="action"/> throws.
/// </summary>
public void RunWithAsyncContext(AsyncContextSnapshot snapshot, Action action);
public T RunWithAsyncContext<T>(AsyncContextSnapshot snapshot, Func<T> func);
```

`AsyncContextSnapshot` is a `public readonly struct` wrapping the internal `AsyncContextMapping?` — opaque, with
no members beyond equality, so the internal representation stays free to change. `default(AsyncContextSnapshot)`
is the empty mapping, which makes "run this callback in a clean context" spellable without capturing first.

The motivating shape, which is what an embedder actually writes:

```csharp
// script registered a callback while inside requestId.run("req-42", ...)
var snapshot = engine.Advanced.CaptureAsyncContext();

// ... later, from the host's own loop, in a different turn
engine.Advanced.RunWithAsyncContext(snapshot, () => callback.Call(JsValue.Undefined, args));
```

Without this, a host that invokes a stored JS callback itself — the single most common embedding shape, and the
one `Jint.Tests.PublicInterface/HostCallLoopConstraintTests.cs` already documents for constraints — runs it
under whatever mapping happens to be installed, which is the empty one. AsyncContext's whole value proposition
fails at exactly the boundary embedders live on.

### 4.2 Materialising a snapshot for script

For a host that wants to hand the captured context *to script* — so the script can `snapshot.run(...)` itself —
the spec's host operation maps to one more member:

```csharp
/// <summary>
/// https://tc39.es/proposal-async-context/#sec-createasynccontextsnapshotobject
/// </summary>
public JsValue CreateAsyncContextSnapshotObject(AsyncContextSnapshot snapshot);
```

This is the only member that materialises an intrinsic, so it is also the only one that would force
`%AsyncContext.Snapshot%` to be built on an engine that never mentioned `AsyncContext`. Keep it in a later
phase; the pair in §4.1 covers the correlation use case on its own.

### 4.3 Relationship to `HostDefined`

They answer different questions and compose rather than compete:

| | `Engine.HostDefined` | AsyncContext |
| --- | --- | --- |
| Scope | per **engine** (principal realm's `[[HostDefined]]`) | per **flow**, within one engine |
| Set by | the host, in CLR | the script, or the host through §4.1 |
| Survives `RestoreGlobalSnapshot` | yes, deliberately | no — reset, see [§6.1](#6-1-restoreglobalsnapshot-and-generations) |
| Readable from | any CLR code holding the `Engine` | script through `Variable.get()`; CLR through §4.1 |

A `ConsoleSink` correlating log lines reads the request identity from `HostDefined` and the operation identity
from an AsyncContext variable the host installed. Neither replaces the other.

### 4.4 `HostPromiseRejectionTracker`

The spec states the requirement as three obligations on the host rather than as algorithm steps:

> - It must perform AsyncContextSnapshot() at the call of HostPromiseRejectionTracker,
> - It must perform AsyncContextSwap before the event notification, with the result of the AsyncContextSnapshot operation,
> - It must perform AsyncContextSwap after the event notification, with the result of the earlier AsyncContextSwap operation.

Jint's implementation raises a CLR event (`Engine.OnPromiseRejectionTracker`, reached from
`Jint/Runtime/Host.cs:473`), which is not a queued notification — it fires synchronously at the call, so the
ambient mapping is already the one the spec asks for and the obligation is satisfied by construction. Record
that reasoning in a comment; if the tracker is ever made asynchronous, the obligation becomes real.

### 4.5 What is *not* in the host surface

No `Options` knob. AsyncContext is a language feature, not an opt-in web API: nothing goes in
`Options.WebApi`, `WebApiFeatures` gets no bit, and `Jint/WebApi/WebApiRegistration.cs` is untouched. It is also
**not** TFM-gated — the whole-file `#if NET8_0_OR_GREATER` rule applies to web-API code, and the only file in
this design that carries one is the timers integration, which is inside the already-gated
`Jint/WebApi/Timers/` subtree.

---

## 5. Cost analysis

The rule this design is written to: **an engine that never uses AsyncContext must pay one predictable null test
per affected site and nothing else** — the same bar `Engine._webApi` and `Engine._atomicsWaiterDeadlines` are
held to (`Jint/Engine.Pump.cs:44-59`).

| Site | Cost with no AsyncContext in use | Notes |
| --- | --- | --- |
| `EventLoop.Enqueue` / `EventLoopJob` / `RunAvailableContinuations` | **zero** | untouched by design ([§3.2](#3-2-points-the-spec-deliberately-does-not-define)) |
| `Engine.AddToEventLoop(Action)` | **zero** | untouched |
| `PerformPromiseThen` (both overloads) | 1 field load + 1 field store per reaction | plus the storage question in [§7.2](#7-2-the-eight-bytes-on-promisereaction) |
| `RunReactionJob` | 2 null tests, **no protected region** on the fast arm | the two-arm split in [§2.3](#2-3-the-swap-a-null-mapping-fast-path-and-no-try-finally-on-it) |
| `GeneratorStart` / `GeneratorResume` / `GeneratorResumeAbrupt` | 1 load + 1 store on start; 2 null tests on resume | + 8 B on `GeneratorInstance`, which is not a per-call allocation |
| `AsyncGeneratorStart` / `AsyncGeneratorResume` | as above | |
| `setTimeout` / `setInterval` registration | 1 load + 1 store on `TimerEntry` | +8 B per timer, capped by `MaxActiveTimers` |
| `TimerEntry.Fire` | 2 null tests | `Jint/WebApi/Timers/TimerQueue.cs:300` |
| `queueMicrotask` | 1 load, captured into the existing closure | the closure already exists (`TimerFunctions.cs:180`) |
| `SourceTextModuleRecord.ExecuteModule` | 1 swap pair per module evaluation | not a hot path |
| `Variable.get()` | 1 null test → return `[[AsyncVariableDefaultValue]]` | only reachable if script has a `Variable` |
| Engine construction | **zero** | `Agent` gains a null reference field; nothing is allocated |

**The disarm story.** Unlike the timer queue, which needs an explicit `Clear()`, the mapping *self-disarms*:
every installer restores its predecessor in a `finally`, so once the outermost `run` unwinds the agent field is
`null` again, and the next reaction job takes the fast arm. There is no accumulating state and no "used it once,
pays forever" residue. The one place an explicit reset is still required is the restore fence
([§6.1](#6-1-restoreglobalsnapshot-and-generations)).

**What must be measured before merging.** The reaction-record change is on the promise hot path, so the
implementing PR carries the standing perf gate: a paired A/B (`Jint.Benchmark/measure-paired.ps1`, `gate` mode)
on the promise/async rows plus the full SunSpider and Dromaeo tables. A row is a regression only when its
bootstrap CI excludes zero. No numbers are quoted anywhere in this document: nothing here has been measured yet,
and a design-stage guess quoted once tends to be cited forever.

---

## 6. Risks

### 6.1 `RestoreGlobalSnapshot` and generations

Three interactions, two of which are already handled by machinery that exists.

- **Queued work.** A reaction or job created before a restore carries its mapping, but it is dropped anyway:
  `EventLoop.Clear()` discards what is queued and the generation stamp discards what arrives afterwards
  (`Jint/Runtime/EventLoop.cs:294,358`). No new fence is needed, and none may be added — the existing rule that
  `RestoreGlobalSnapshot` "bumps version counters, it never restores them" is unaffected because the mapping is
  not a counter.
- **The installed mapping.** `Engine.ResetTransientEvaluationState` (`Jint/Engine.GlobalSnapshot.cs:257`) must
  null the agent field, alongside `DiscardAtomicsWaiterDeadlines()` and `_webApi?.ResetTransientState()`. In a
  balanced program the field is already `null` at that point, but a restore can be initiated from inside a
  running job (the method's own comments note a job may call back into host code that restores), and an
  unbalanced mapping surviving into the restored engine is precisely the cross-cycle channel the fence exists to
  prevent.
- **Suspended `EvaluateAsync`.** A settlement loop sitting in its `await` is invisible to
  `_activeEvaluationContext` and to the execution-context depth; only `Engine._pendingAsyncOperations` sees it
  (`Jint/Engine.Async.cs:188`). Nothing about the mapping changes that, but any future guard reading "is a
  mapping installed" as "is the engine busy" would be wrong for the same reason.

### 6.2 ShadowRealm

The mapping is **per agent, not per realm** — confirmed from the Agent Record table, and the proposal has no
per-realm mapping anywhere. Jint's `ShadowRealm` creates a Realm Record inside the same `Engine`
(`Jint/Native/ShadowRealm/ShadowRealmConstructor.cs:38`), so it shares the mapping, which is spec-correct and
requires no work.

It is also not a leak. A shadow realm gets its own `%AsyncContext.Variable%` intrinsic, so its variables are
different object identities from the outer realm's; shadow-realm code cannot name an outer `Variable` and so
cannot read an outer value, even though the entry is sitting in the shared mapping while it runs. The
ShadowRealm callable boundary only lets primitives and (wrapped) functions across, which does not admit a
`Variable` object.

The residual risk is retention, and the champions flag it: a mapping entry keyed by a shadow realm's `Variable`
keeps that realm's objects alive for as long as the mapping is reachable. That is the cross-realm case
`MEMORY-MANAGEMENT.md` cites as the argument for weak keying. See [§6.4](#6-4-retention).

### 6.3 Proposal maturity

**AsyncContext is Stage 2.** Verified against `tc39/proposals@main`'s Stage 2 table (row: *Async Context*,
author Chengzhong Wu, champions Andreu Botella, Chengzhong Wu, Justin Ridgewell) and against the proposal
The proposal's own `Status: Stage 2`. It is not in the Stage 2.7 table.

`AGENTS.md` says proposal built-ins are registered unconditionally, with no per-feature option and no ES-version
gate, and that rule should stay. But it is worth being precise about what Jint has actually been shipping under
it. **Not one of the proposal features summarized in the ECMAScript reference appears in the Stage 2 table** — the whole
table was read, and Jint's set sits at 2.7 or above:

| Jint's shipped proposals | Stage |
| --- | --- |
| Decorators, Decorator Metadata, ShadowRealm, Immutable ArrayBuffers, Import Bytes | 2.7 |
| Await Dictionary (`Promise.allKeyed`), Iterator Chunking, Joint Iteration | 3 |
| Temporal, Explicit Resource Management | 3 / 4 |

AsyncContext would be the **first Stage 2 proposal Jint ships**, and it is an unusually poor candidate to be the
first:

- **It is not additive.** Every other proposal in that list adds built-ins that existing code paths never touch.
  This one patches promise reactions, generator start and resume, async generator start and resume, module
  evaluation and the finalization registry — the deepest and hottest machinery in the engine — and it does so on
  a path every embedder pays for.
- **Stage 2 explicitly does not promise the algorithm is settled.** The capture points are exactly what is still
  being debated — `tc39/proposal-async-context#124` ("Focus on What Hurts Most: AsyncContext for `await` as a
  First Step") argues for shipping the `await` integration first and deferring the rest — and the
  event-listener rules have a competing proposal of their own (`mmocny/proposal-async-event-listeners`). A
  capture point Jint ships and the committee later moves is a silent behaviour change for every embedder.
- **test262 has zero coverage.** Verified two ways: there is no `test/built-ins/AsyncContext` directory in
  test262 `main`, and no `async-context` entry in `features.txt` at Jint's pinned SHA
  (`3655e7464de3d52643ecddd4b5f9f4f3e7f62398`) or in the working checkout. The proposal repository carries a
  TypeScript polyfill (`src/`) and a single mocha file (`tests/async-context.test.ts`), and nothing else. Jint's
  usual tie-breaker — "where the prose and test262 disagree, test262 at the pinned SHA wins" — has nothing to
  say here, so every conformance claim would rest on our own reading of a draft.

**Recommendation.** Do not merge a JS-visible `AsyncContext` global while the proposal is at Stage 2.
Concretely:

1. **Now:** keep this document, and treat the [§3.4](#3-4-jint-shapes-that-change-the-answer) findings as
   independently valuable — the extra event-loop hops and the finalizer-thread FinalizationRegistry are
   pre-existing issues worth their own fixes regardless of whether AsyncContext ever lands.
2. **At Stage 2.7:** land phases 1-4 of [§7](#7-implementation-plan). 2.7 is exactly the gate that fixes this:
   it means the spec text is complete and, in current practice, that test262 tests are written — which is the
   thing Jint conforms *to*.
3. **If the maintainer wants it sooner:** ship it **unconditionally**, per the existing rule, and not behind a
   new per-feature switch. Inventing an ES-version or feature gate for one proposal would create a precedent
   that has to be honoured for every future one and would contradict `AGENTS.md` for a worse reason than the
   thing it is trying to avoid. Accept the maturity risk explicitly rather than hiding it behind a flag.

There is one thing worth doing in *any* of those branches: the mechanism can be built and tested without the
global. Phases 1-3 could land with `%AsyncContext%` created but **not installed on the global object**, reachable
only from `Jint.Tests` through `InternalsVisibleTo`. That gets the risky machinery soaked in CI, keeps every cost
in this document paid and measured, and leaves a one-line change to expose it when the proposal advances. It is
the option this design recommends if there is pressure to start early.

### 6.4 Retention

A mapping strongly holds its keys and values (`MEMORY-MANAGEMENT.md`: values are "strongly held (not weak
references)"). A captured mapping is reachable from a `PromiseReaction`, a `TimerEntry`, a `GeneratorInstance` or
a host `AsyncContextSnapshot`, so anything in it lives at least that long. Two Jint-specific amplifiers:

- **A warmed call site retains its last callee**, and a `ScriptFunction` retains the environment it closed over
  (`JintCallExpression._regCallee`). If that closure was created inside a `run`, nothing about AsyncContext makes
  this worse — but a host debugging a retention report will now find mappings in the graph and needs the
  documentation to say they are bounded per site, not accumulating.
- **A pooled engine keeps its `Agent`.** The mapping field self-disarms, so a pooled engine does not accumulate;
  but a `Snapshot` a host holds in CLR does keep its mapping alive for as long as the host holds it. The
  `AsyncContextSnapshot` doc comment must say so.

Weak keying (which the proposal permits but does not require) is **out of scope**: it would need a
`ConditionalWeakTable`-shaped mapping whose cost lands on every capture, and the hybrid strong-then-weak scheme
the champions sketch is not specified. Revisit only if a real embedder reports a leak.

### 6.5 Host-contract verification

The one invariant that is cheap to check and expensive to debug is *balance*: every job must leave the agent
mapping as it found it. That is a natural fit for the existing gate
(`Jint/Runtime/HostContractVerification.cs`), checked at the call site per `AGENTS.md`:

```csharp
if (HostContractVerification.Enabled)
{
    // in EventLoop.RunAvailableContinuations, around job.Run(engine)
    // assert the agent's mapping reference is unchanged across the job
}
```

Release cost when the switch is off: zero, because the flag is a `static readonly bool` the JIT folds. This is
the only new verifier the design proposes, and it is what makes the two-arm fast path safe to trust.

---

## 7. Implementation plan

PR-sized, each independently reviewable, each rebased on latest `main`, each targeting
`sebastienros/jint` `main`.

### 7.1 Phases

| Phase | Content | Depends on |
| --- | --- | --- |
| **0** | *Prerequisites, independently valuable.* Fix the extra event-loop hops in [§3.4(b)](#3-4-jint-shapes-that-change-the-answer) to be spec-shaped; move FinalizationRegistry cleanup off the finalizer thread onto the event loop ([§3.3](#3-3-finalizationregistry-is-blocked-in-jint)). Neither mentions AsyncContext. | — |
| **1** | `Agent.AsyncContextMapping`, `AsyncContextMapping` + `AsyncContextEntry`, `Engine.AsyncContextSnapshot/Swap`, `Jint/Native/AsyncContext/` (`AsyncContextInstance`, `VariableConstructor`, `VariablePrototype`, `SnapshotConstructor`, `SnapshotPrototype`, `JsAsyncContextVariable`, `JsAsyncContextSnapshot`), `Intrinsics.AsyncContext.cs`. **Promise reaction capture and restore** (rows 1-3). Global registration decided per [§6.3](#6-3-proposal-maturity). | 0 (not strictly) |
| **2** | Generators and async generators (rows 4-7), module evaluation's empty mapping (row 8). | 1 |
| **3** | Web-API integration: `setTimeout`/`setInterval` capture at registration and restore in `TimerEntry.Fire`, `queueMicrotask` capture. Whole-file `#if NET8_0_OR_GREATER` is already in force in that subtree. A comment in the events code recording that `addEventListener` deliberately does not capture. | 1, and #3096 for the events comment |
| **4** | Host surface: `Engine.Advanced.CaptureAsyncContext` / `RunWithAsyncContext`, the `AsyncContextSnapshot` struct, and `Jint.Tests.PublicInterface/HostAsyncContextTests.cs` — the `Host*Tests.cs` family, per the generically-named-file convention. | 1 |
| **5** | `Engine.Advanced.CreateAsyncContextSnapshotObject`, README entry under "ECMAScript proposals (no version yet)", `HostContractVerification` balance check. | 4 |
| **6** | *Deferred:* FinalizationRegistry rows 9-10, once phase 0's second half has landed. | 0 |

Phase 1 carries the perf gate ([§5](#5-cost-analysis)). Phases 2-5 touch no default-engine hot path and need no
benchmark unless they modify a shared file.

### 7.2 The eight bytes on `PromiseReaction`

`PromiseReaction` (`Jint/Native/Promise/PromiseTypes.cs:16`) is an `internal sealed record` with four fields.
Adding `AsyncContextMapping? Mapping` costs 8 bytes on every reaction — two per `.then()`, two per `await`,
allocated on Jint's hottest asynchronous path, paid by every embedder whether or not AsyncContext is used.

The design's default is to **add the field and measure**, because it is by far the clearer code. If the gate
refuses it, the fallback is a subclass:

```csharp
internal record PromiseReaction(...);                                   // unsealed
internal sealed record PromiseReactionWithAsyncContext(..., AsyncContextMapping Mapping) : PromiseReaction(...);
```

allocated only when the ambient mapping is non-null, read with `reaction is PromiseReactionWithAsyncContext c`.
That is zero bytes and one type test when unused, on a path that already performs two type tests
(`reaction.Continuation is { }`, `reaction.Handler is ICallable`). The cost is unsealing the record, which
forfeits nothing measurable here — no virtual member of `PromiseReaction` is called on the hot path — but does
make the type extensible inside the assembly, so it needs a comment saying why.

Decide with the paired benchmark, not by argument.

---

## 8. Test strategy

There is **no test262 coverage to inherit** ([§6.3](#6-3-proposal-maturity)), so every case is hand-written in
`Jint.Tests/Runtime/AsyncContextTests.cs` (plus `Jint.Tests/Runtime/WebApi/AsyncContextTimerTests.cs` under a
whole-file `#if NET8_0_OR_GREATER`, since timers are net8-only), with the host-facing pins in
`Jint.Tests.PublicInterface/HostAsyncContextTests.cs`. The proposal's own
`tests/async-context.test.ts` and its `src/` polyfill are useful as a source of cases and as a reference
implementation to diff behaviour against; they are not a conformance suite.

Per the repository's standing rule, every one of these must be **shown failing against unfixed code**, with the
pre-fix error quoted in the PR.

| # | Case | Bites when |
| --- | --- | --- |
| A1 | `v.run(1, f)`; inside `f`, `v.get() === 1`; after, `v.get() === default` | the swap or the restore is missing |
| A2 | `run` restores after the callback **throws** | the restore is a post-call statement rather than a `finally` |
| A3 | `run` restores after a `TimeoutException` / cancellation unwinds through it | the restore is a `catch (JavaScriptException)` |
| A4 | nested `run` on the same variable; inner shadows, outer survives | `With` mutates instead of copying |
| A5 | `Snapshot.wrap(fn)` called from a different context sees the wrap-time value, and the result carries `"wrapped "` + `fn.name` and `fn.length` per `CopyNameAndLength(wrapped, fn, "wrapped")` | the name/length copy is missing — note the only `CopyNameAndLength` in the tree today is `WrappedFunction`-typed and private (`Jint/Native/ShadowRealm/ShadowRealm.cs:235`), so one has to be generalised |
| P1 | `v.run(1, () => p.then(() => log(v.get())))` logs `1` | row 1 capture missing |
| P2 | value at `.then()` registration wins over the value at settle time | the capture is taken at job dequeue instead of at `PerformPromiseThen` |
| P3 | `await` across two suspensions keeps the value | the reaction-carried mapping does not compose across resumes |
| P4 | thenable resolution (`NewPromiseResolveThenableJob`) keeps the value | row 3 missing |
| P5 | reaction job leaves the agent mapping exactly as it found it (assert via the internal accessor) | the fast/slow arm split leaks |
| G1 | generator body sees its creation-time value across `yield` | row 4/5 missing |
| G2 | a mutation made by the *resumer* between two `next()` calls is not visible inside the generator | the resume swap is missing |
| G3 | **`yield` needs no swap of its own** — a `run` performed by the resumer around `gen.next()` does not leak in | [§3.4(a)](#3-4-jint-shapes-that-change-the-answer)'s equivalence claim; this is the test that proves it rather than asserting it |
| G4 | the same for async generators and `for await` — including the extra-hop site at `JintForInForOfStatement.cs:1227` | [§3.4(b)](#3-4-jint-shapes-that-change-the-answer) |
| M1 | a module body sees the **default** value even when imported from inside a `run` | row 8's empty mapping missing |
| T1 | `setTimeout` callback sees the registration-time value | the timer capture in phase 3 is missing |
| T2 | `setInterval` sees the *registration-time* value on **every** firing, not the value at the firing turn | the entry re-captures on `Reschedule` |
| T3 | `queueMicrotask` sees the call-time value | |
| T4 | a timer registered inside `run`, then `RestoreGlobalSnapshot`, then a pump — nothing fires and no mapping survives | [§6.1](#6-1-restoreglobalsnapshot-and-generations) |
| E1 | **`addEventListener` does not capture**: a listener registered inside a `run` and dispatched outside it sees the *dispatch*-time value | [§3.2](#3-2-points-the-spec-deliberately-does-not-define) — this test exists to stop a future agent "fixing" the omission |
| S1 | a shadow realm's `AsyncContext.Variable` is a different key; outer values are invisible inside | [§6.2](#6-2-shadowrealm) |
| H1 | `Engine.Advanced.CaptureAsyncContext` + `RunWithAsyncContext` round-trips a value into a host-invoked callback | [§4.1](#4-1-the-minimal-pair) |
| H2 | `RunWithAsyncContext` restores when the action throws | |
| H3 | `default(AsyncContextSnapshot)` runs the callback under the empty mapping | |
| H4 | `Options` shared by two engines: mappings are independent | the mapping is stored anywhere but the per-engine `Agent` |
| V1 | with `JINT_HOST_CONTRACT_VERIFICATION=1`, a deliberately unbalanced internal job trips the verifier | [§6.5](#6-5-host-contract-verification) |

Timer cases use `Options.WebApi.Timers.TimeProvider` with a fake provider, as the existing timer tests do, so
none of them are wall-clock flaky.

---

## 9. Open questions

1. **Phase 0 sequencing.** The extra-hop fixes ([§3.4(b)](#3-4-jint-shapes-that-change-the-answer)) are
   observable today only as microtask-ordering detail. Are they worth a standalone PR before there is an
   AsyncContext to motivate them? This design says yes — they are correctness debt either way — but it is the
   maintainer's call.
2. **FinalizationRegistry on the event loop.** Moving cleanup off the CLR finalizer thread is a behaviour change
   with its own risk profile (`CleanupFinalizationRegistry` is currently an empty stub, so today's behaviour is
   already non-conformant in more ways than one). Worth confirming that nobody depends on the current shape.
3. **Whether phase 1 lands with the global installed.** [§6.3](#6-3-proposal-maturity) recommends building the
   mechanism and withholding the global until Stage 2.7; that is a policy decision, not an engineering one.
4. **`PromiseReaction` storage.** Field or subclass ([§7.2](#7-2-the-eight-bytes-on-promisereaction)) — decided
   by the paired benchmark, which has not been run.

---

## Appendix: sources read

**Normative.** `tc39/proposal-async-context@master` `spec.html`, read in full — specifically the clauses
`sec-asynccontext-mapping-record-specification-type`, `sec-agents`, `sec-host-cleanup-finalization-registry`,
`sec-createbuiltinfunction`, `sec-generator-function-definitions-runtime-semantics-evaluation`,
`sec-source-text-module-record-execute-module`, `sec-newpromisereactionjob`,
`sec-newpromiseresolvethenablejob`, `sec-performpromisethen`, `sec-host-promise-rejection-tracker`,
`sec-generatorstart`, `sec-generatorresume`, `sec-generatorresumeabrupt`, `sec-asyncgeneratorstart`,
`sec-asyncgeneratorresume`, `sec-asynccontextsnapshot`, `sec-asynccontextswap`,
`sec-createasynccontextsnapshotobject`, `sec-asynccontext-snapshot`, `sec-asynccontext-snapshot.wrap`,
`sec-asynccontext-snapshot.prototype.run`, `sec-asynccontext-variable`,
`sec-asynccontext-variable.prototype.run`, `sec-asynccontext-variable.prototype.name`,
`sec-asynccontext-variable.prototype.get`, `sec-finalization-registry-cleanup-callback`.

**Informative.** The proposal's `README.md` (stage), `WEB-INTEGRATION.md` (schedulers, event listeners),
`MEMORY-MANAGEMENT.md` (retention, expected mapping size), `src/` (TypeScript polyfill), `tests/` (one mocha
file). `tc39/proposals@main` `README.md` Stage 2 and Stage 2.7 tables.

**Jint.** `Agent.cs`, `Engine.cs`, `Engine.Advanced.cs`, `Engine.Async.cs`, `Engine.GlobalSnapshot.cs`,
`Engine.Pump.cs`, `Engine.WebApi.cs`, `Options.WebApi.cs`, `Runtime/EventLoop.cs`, `Runtime/Host.cs`,
`Native/Promise/*`, `Native/Generator/GeneratorInstance.cs`,
`Native/AsyncFunction/AsyncFunctionInstance.cs`, `Native/FinalizationRegistry/FinalizationRegistryInstance.cs`,
`Native/ShadowRealm/*`, `Native/Array/ArrayConstructor.cs`, `Runtime/Interpreter/JintStatementList.cs`,
`Runtime/Interpreter/Expressions/JintAwaitExpression.cs`,
`Runtime/Interpreter/Statements/JintForInForOfStatement.cs`, `Runtime/Interpreter/JintFunctionDefinition.cs`,
`WebApi/Timers/*`, `AGENTS.md`.
