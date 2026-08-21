# Web Workers in Jint — design

**Status: design only. No implementation exists.** This document answers the five questions in
[sebastienros/jint#3167](https://github.com/sebastienros/jint/issues/3167) with one recommendation each, argued
from the engine as it stands at `80ecf3467` ("Wpt: vendor and run the WebCryptoAPI corpus …").

Everything normative here was read from the [HTML Standard](https://html.spec.whatwg.org/multipage/workers.html)
and [Web Messaging](https://html.spec.whatwg.org/multipage/web-messaging.html), not from secondary
documentation.

---

## 1. The premise, restated

Nearly everything `new Worker()` needs already exists in Jint:

| Need | What already does it |
| --- | --- |
| A second isolated global | A second `Engine` |
| A channel between the two | `Engine.Advanced.CreateMessagePortPair` (`Jint/Engine.Advanced.WebApi.cs:231`) |
| A value crossing without a `JsValue` crossing | `SerializationRecord` (`Jint/WebApi/StructuredClone/SerializationRecord.cs`) |
| Delivery on the receiver's own thread | `Engine.AddToEventLoop(Action, generation)` (`Jint/Engine.cs:1050`) |
| A cycle fence so a dead cycle cannot be posted into | `MessagePortEndpoint.Generation` (`Jint/WebApi/Messaging/MessagePortBridge.cs:80`) |
| `self`, `addEventListener`, `dispatchEvent` on the global | `WebApiFeatures.GlobalEvents` (`Jint/WebApi/WebApiRegistration.cs:195`) |
| Module loading driven by the host's own loop | `Engine.Modules.StartImport` (`Jint/Engine.Modules.cs:761`) |
| An error channel a host cannot lose | `DiagnosticsSink` (`Jint/WebApi/DiagnosticsSink.cs`) |
| A host-owned pump | `Advanced.ProcessTasks` + `Advanced.TimeUntilNextScheduledWork` |

The one thing that does not exist, and must not, is the thread. **Jint never starts a thread to run script** —
`Jint/Engine.Pump.cs:149`, `README.md:425`, and every timer/fetch/socket/stream doc comment in
`Jint/WebApi/`. So `new Worker()` is only implementable if the *host* supplies the execution resource.

That leaves exactly one shape: a **host-supplied worker provider**. The engine owns the spec-shaped parts
(port entanglement, the global scope, message and error plumbing, the ordering rules, `terminate()`
semantics). The host owns every thread, every pump, and the worker engine's own configuration.

---

## 2. Q1 — Factory shape

### Recommendation

**An options-held abstract class, read once at engine build, exactly like `StorageProvider` and
`BroadcastChannelBroker`. It receives a request object and returns an `Engine`.**

```csharp
namespace Jint;

public partial class Options
{
    public class WebApiOptions
    {
        /// Settings for the Workers feature, installed when Features contains WebApiFeatures.Workers.
        public WorkerOptions Workers { get; } = new();
    }

    public class WorkerOptions
    {
        /// The host's answer to `new Worker(...)`. Null — the default — leaves the `Worker` global
        /// uninstalled even when the feature flag is on, so `typeof Worker === 'undefined'`.
        public WorkerProvider? Provider { get; set; }

        /// How many live workers one engine may have. Defaults to 8; `new Worker` beyond it is a
        /// QuotaExceededError DOMException, the same refusal MaxActiveTimers makes.
        public int MaxWorkers { get; set; } = 8;
    }
}
```

```csharp
namespace Jint.WebApi;

/// The host's answer to `new Worker(...)`: it decides whether a worker may exist at all, builds the
/// engine that runs it, and — because Jint never starts a thread — decides which thread pumps it.
public abstract class WorkerProvider
{
    protected WorkerProvider() { }

    /// Builds the engine for one `new Worker(...)`, or returns null to refuse.
    /// Called on the PARENT's thread, synchronously, while the parent's script is suspended inside the
    /// constructor. It must not run script on the parent engine, must not block, and must not touch any
    /// engine another thread is currently running. It must NOT fetch the worker's script: that is the
    /// worker's own IModuleLoader's job, on the worker's own pump.
    public abstract Engine? CreateWorkerEngine(WorkerRequest request);

    /// The ports are entangled, the worker global scope is installed and the start job is queued on the
    /// worker's event loop. This is where the host starts pumping the worker engine.
    public virtual void OnWorkerStarted(WorkerConnection connection) { }

    /// The connection has ended — see WorkerEndReason. Called on whichever engine's thread ended it.
    /// This is where the host stops pumping and disposes the worker engine.
    public virtual void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason) { }
}

/// Everything a provider needs to decide, plus the pre-wired options it should usually start from.
public sealed class WorkerRequest
{
    /// The engine that ran `new Worker(...)`. Suspended mid-statement; read it, do not run it.
    public Engine Parent { get; }

    /// The first argument to `new Worker(...)`, verbatim and unresolved.
    public string Specifier { get; }

    /// Module.Location of the module the constructor was reached from, or null for a classic script.
    /// Pass it to ModuleFactory.LocationOf's counterpart in your own loader to resolve Specifier the
    /// way `import()` would have.
    public string? ReferencingLocation { get; }

    /// Always WorkerType.Module today; see §5.
    public WorkerType Type { get; }

    /// The `name` option, or the empty string.
    public string Name { get; }

    /// Cancelled by terminate(), by close(), by a restore on either side and by the parent's Dispose.
    /// CreateDefaultOptions registers it; a provider building its own Options must register it itself
    /// or terminate() will close the ports and stop nothing.
    public CancellationToken TerminationToken { get; }

    /// Fresh Options pre-wired for this worker — never the parent's instance. See §4 and §5.
    public Options CreateDefaultOptions();

    /// A sink that reports the worker's uncaught failures to the parent's `Worker` object, optionally
    /// chaining to the host's own sink. CreateDefaultOptions installs CreateDiagnosticsSink(null).
    public DiagnosticsSink CreateDiagnosticsSink(DiagnosticsSink? inner = null);
}

public enum WorkerType { Module }

/// One live parent↔worker pair, as the host sees it.
public sealed class WorkerConnection
{
    public Engine Parent { get; }
    public Engine Worker { get; }
    public string Name { get; }
    public bool IsEnded { get; }

    /// The host's own terminate(): identical to the script's, and idempotent.
    public void End();
}

public enum WorkerEndReason { Terminated, ClosedByWorker, ParentRestored, WorkerRestored, ParentDisposed }
```

```csharp
// The single extension that sets flag and provider together, so the two cannot get out of step.
public static Options UseWorkers(this Options options, WorkerProvider provider);
```

### Why an options-held abstract class

- **Every host-supplied capability in this subtree already lives on options and is read once at build**:
  `StorageProvider` (`Jint/WebApi/StorageProvider.cs:48`), `CacheStorageProvider`,
  `BroadcastChannelBroker` (`Jint/Options.WebApi.cs:132`), `ConsoleSink`, `DiagnosticsSink`,
  `FetchOptions.HttpClientFactory`. A worker provider is host code, knowable at options time, and a
  provider shared by every engine in a pool is the normal case — exactly what `BroadcastChannelBroker`
  is the precedent for.
- **`Engine.Advanced.SetFetchHandler` is not a counter-example.** It registers a *script value*, which
  cannot exist before the script has run; that is precisely why it is a live door rather than an option.
  Nothing about a worker provider needs a script to have run.
- The **live door already exists** for the pooled-engine case that discovers per request whether a tenant
  may spawn workers: `engine.Advanced.EnableWebApis(WebApiFeatures.Workers, w => w.Workers.Provider = …)`
  (`Jint/Engine.Advanced.WebApi.cs:159`). No new `Engine.Advanced` method is needed.
- **Abstract class, not an interface and not a `Func<>`**, for the reason `StorageProvider` gives verbatim:
  later revisions can add members without breaking the hosts that implement it today. A delegate can never
  grow a parameter.

### Why it returns an `Engine` and not an opaque handle

The host has to pump it, and pumping is `engine.Advanced.ProcessTasks()` bounded by
`engine.Advanced.TimeUntilNextScheduledWork` (`Jint/Engine.Pump.cs:226`). Every host-facing asynchronous
shape in the codebase — `ModuleImportOperation`, `FetchHandlerOperation`, `HostStreamCopyOperation` — is
documented as "the host calls `engine.Advanced.ProcessTasks()`". An opaque handle would have to re-export
both members and would be the first place a host is asked to pump something without holding the engine.

### Who pumps, and how

**The host, always. The engine never.** `OnWorkerStarted` is where the host learns a worker exists. Two
shapes are supported and neither is privileged:

```csharp
// (a) one thread per worker
sealed class ThreadPerWorker : WorkerProvider
{
    public override Engine? CreateWorkerEngine(WorkerRequest request)
        => new Engine(request.CreateDefaultOptions().EnableModules(_workerRoot));

    public override void OnWorkerStarted(WorkerConnection c)
    {
        var thread = new Thread(() =>
        {
            while (!c.IsEnded)
            {
                c.Worker.Advanced.ProcessTasks();
                var until = c.Worker.Advanced.TimeUntilNextScheduledWork ?? TimeSpan.FromMilliseconds(20);
                if (until > TimeSpan.Zero) Thread.Sleep(until < _ceiling ? until : _ceiling);
            }
        }) { IsBackground = true };
        thread.Start();
    }

    public override void OnWorkerEnded(WorkerConnection c, WorkerEndReason reason) => c.Worker.Dispose();
}

// (b) N workers cooperatively on the host's existing loop — a game loop, a message pump
foreach (var c in liveConnections) c.Worker.Advanced.ProcessTasks();
parent.Advanced.ProcessTasks();
```

Shape (b) is the one an embedder with a thread affinity wants, and it is why the provider returns an
`Engine` rather than owning the loop itself.

### Alternatives, one line each

- **A `Func<WorkerRequest, Engine?>` on options** — cannot grow a member, and the two lifecycle callbacks
  would have to become two more delegates.
- **`Engine.Advanced.SetWorkerProvider`** — a fetch *handler* is a script value and needs a live door; host
  code does not, and the existing `EnableWebApis` door already covers the pooled case.
- **Returning an opaque `WorkerHandle` with its own `Pump()`** — duplicates `ProcessTasks` and hides the one
  object the host must hold.
- **The engine creating the worker engine itself from a configuration object** — the whole point is that the
  host decides the module loader, the constraints, the network policy and the thread; a configuration object
  that could express all of that *is* an `Options`, and building it is what `CreateDefaultOptions` does.

---

## 3. Q2 — Lifecycle

### 3.1 Construction

`new Worker(specifier, options)` runs on the parent's thread, mid-evaluation, and does this in order:

1. **WebIDL conversion.** `specifier` is a `USVString`. `options.type` must be `'module'`; anything else is a
   `TypeError` naming the fix (§5). `options.name` is a `DOMString`. `options.credentials` is accepted and
   ignored — the convention `README.md:773` already records for `mode`, `referrer` and `integrity`.
2. **Quota.** More than `Options.WebApi.Workers.MaxWorkers` live connections is a `QuotaExceededError`
   `DOMException`, the same refusal `MaxActiveTimers` makes. A script that can spawn unbounded engines is a
   fork bomb, and no execution constraint describes engine *count* — the same argument the performance
   timeline's buffer bound makes.
3. **Mint the termination source.** A `CancellationTokenSource` per worker, created *before* the provider is
   called so its token can be registered as a constraint on options the provider has not built yet.
4. **`provider.CreateWorkerEngine(request)`**, synchronously, on the parent's thread.
   - `null` is a **refusal** and throws a `SecurityError` `DOMException` synchronously. A provider saying "no
     worker for this specifier" is a policy decision, not a fetch failure, and a script that must feature-test
     can `try`/`catch`. *(Alternative: an asynchronous `error` event, which hides a policy refusal behind a
     turn of the loop.)*
   - Anything the provider **throws** propagates unchanged. Only `StorageQuotaExceededException` is ever
     translated into a script-visible error anywhere in this subtree, and that rule is worth keeping.
5. **Validate the returned engine**, `InvalidOperationException` on each: it is not the parent, it is not
   already connected to another `Worker`, and it carries `WebApiFeatures.Messaging` — which
   `CreateMessagePortPair` requires on both engines anyway (`Jint/Engine.Advanced.WebApi.cs:245`). It must
   also be **quiescent**: building a port on it materializes its `MessagePort` intrinsics, which is engine
   mutation, and that is a host obligation the engine cannot check.
6. **Entangle.** `MessagePortBridge.CreatePair(parent, parentRealm, worker, workerRealm)`. Neither port is
   ever handed to script: the parent's `Worker` object *is* the parent half's façade and the worker global
   *is* the worker half's, which is exactly the two ends HTML names.
7. **Enable the parent half now, leave the worker half disabled.** See §3.2 — this is load-bearing.
8. **Install the worker global scope** on the worker engine (§5).
9. **Queue the start job on the *worker's* event loop**, carrying the worker's generation:
   `workerEngine.AddToEventLoop(() => StartWorkerModule(), workerGeneration)`. The job calls
   `workerEngine.Modules.StartImport(specifier)`, which starts the load and returns without running script;
   the module then loads, links and evaluates across later turns of the worker's own pump.
10. **`provider.OnWorkerStarted(connection)`**, still on the parent's thread — the host's cue to begin pumping.
11. Return the `Worker` object.

Step 9 is the single most important decision here: **no part of the worker's script ever runs on the parent's
thread.** Everything the engine does to the worker engine before it is handed over is engine mutation on a
quiescent engine; everything after is a generation-stamped job on the worker's own queue, which is the one
part of another engine any thread may touch (`MessagePortBridge`'s class remarks).

### 3.2 The worker's inner queue starts disabled — and why that matters

`JsMessagePort` already has exactly the mechanism HTML needs: a real message queue plus an `_enabled` flag,
with the drain job armed only when the queue is enabled (`Jint/WebApi/Messaging/JsMessagePort.cs:165`).

HTML enables a dedicated worker's *inside* port only after the initial script has run. So:

- The **parent** half is `start()`ed at construction (the parent is already running).
- The **worker** half stays disabled until the start job's module import has finished evaluating; the
  reaction that resolves `StartImport`'s operation is what calls `Start()`.

That is what makes the single most common worker idiom work:

```js
const w = new Worker('./worker.js', { type: 'module' });
w.postMessage(1);   // buffered in the worker's port queue, in order, until worker.js has evaluated
```

If the module **fails** to load or evaluate, the inner queue is never enabled, the buffered messages are
dropped, the connection ends with `WorkerEndReason.Terminated`, and the parent gets an `error` event — which
is HTML's "if the script failed … the worker is terminated".

### 3.3 Module vs classic

**Module only.** `new Worker(url)` without `{ type: 'module' }` is a `TypeError` whose message names the
option. Argument in §5, with `importScripts`, because it is the same argument.

Resolution goes through **the worker engine's own `IModuleLoader`**, which the provider set — so "through the
existing loaders" is honoured, on the correct side of the boundary. The parent's loader is deliberately not
consulted: a worker usually has a different base and a different permission set, and the parent's loader is
reached from a thread that is not the worker's. `WorkerRequest.ReferencingLocation` travels purely so the
provider can resolve a relative specifier the way `import()` would; `ModuleFactory.LocationOf` is public for
exactly this.

### 3.4 `terminate()` and `close()`

HTML's *terminate a worker*: abort the script, empty the queue, disentangle the ports, set the terminated flag.
Jint's mapping, in this order:

1. **Close both port endpoints** (`MessagePortEndpoint.Close()`). Engine-enforced, immediate, from whichever
   thread called it, because `_closed` is a `volatile bool` read by both sides. From this instant no
   `postMessage` in either direction enqueues anything, and the parent's `postMessage` after `terminate()` is
   a silent no-op — which is exactly what HTML's step 6 ("if targetPort is null … return") already says.
2. **Cancel the termination source.** *If* the provider registered the token — which `CreateDefaultOptions`
   does — the worker's `CancellationConstraint` throws `ExecutionCanceledException` within
   `AmortizedConstraintCheckInterval` (64) statements on the worker's own thread, and keeps throwing for every
   later entry, because `CancellationConstraint.Reset()` is a no-op
   (`Jint/Constraints/CancellationConstraint.cs:42`) and the countdown is engine state that
   `ResetConstraints()` deliberately never rewinds.
3. **`provider.OnWorkerEnded(connection, Terminated)`** — the host's cue to stop pumping and dispose.

`close()` on the worker global is the same three steps initiated from the worker side, with reason
`ClosedByWorker`.

**What `terminate()` deliberately does not do**, and cannot: it does not touch the worker's event loop, its
globals or any of its state. The worker may be running on another thread, and the only part of another engine
any thread may touch is its event-loop queue — the rule `MessagePortEndpoint.Post` obeys and the reason it does
nothing but "check the fences and enqueue". So HTML's "empty the queue" is the host's job, and the honest way
to do it is to stop pumping and drop the engine.

Two consequences to document rather than paper over:

- **Termination is cooperative and its latency is bounded, not zero.** 64 statements on the worker's thread,
  and *unbounded* while the worker is inside a host CLR call that does not return.
- **A provider that ignores `TerminationToken` still gets the port closure**, so the worker becomes deaf and
  mute immediately; it just keeps running until the host stops pumping it. That is the failure mode the
  documentation must name.

### 3.5 Worker exceptions reaching the parent

HTML: an uncaught worker error fires an `ErrorEvent` at the worker global; if *notHandled*, a task is queued at
the parent to fire an `ErrorEvent` at the `Worker` object; if that too is unhandled it reaches the parent's own
`error`.

Jint already has both ends:

- **Worker side.** `WebApiFeatures.GlobalEvents` fires `error` at the synthetic `GlobalEventTarget`
  (`Jint/WebApi/GlobalEvents/GlobalEventTarget.cs:81`), and `ErrorEventDetails.FromException` already reduces
  a `JavaScriptException` to `(message, filename, lineno, colno, error)`
  (`Jint/WebApi/GlobalEvents/JsErrorEvent.cs:80`).
- **The channel.** `WorkerRequest.CreateDiagnosticsSink(inner)` returns a sink that, on the *worker's* thread,
  drops the `JsValue` and enqueues a generation-fenced job on the **parent's** loop carrying only
  `(message, filename, lineno, colno)` — four CLR values, nothing engine-affine, crossing exactly the way a
  serialization record does.
- **Parent side.** That job fires a trusted `ErrorEvent` at the `Worker` object with **`error: null`**. If no
  listener called `preventDefault()`, it then goes to the parent's own `FireGlobalErrorEvent` and sink, which
  is HTML's third step.

**`error` is `null`, deliberately.** The thrown value belongs to the worker's realm and no `JsValue` may cross
engines. A structured *clone* is available (`ErrorInstance` is serializable —
`Jint/WebApi/StructuredClone/StructuredSerializer.cs:214`) and is still the wrong answer: identity is lost,
`instanceof` answers against the wrong realm's intrinsics, and the serializer flattens every custom subclass to
plain `Error` (`ErrorNameFor`, line 426). Browsers already report `null` there for a cross-origin worker script.
A worker that wants its parent to have the actual failure value does what workers already do: catch it and
`postMessage` it, where the serializer's `Error` support does the right thing on purpose.

**A constraint failure is not an error event.** `ExecutionCanceledException`, `TimeoutException`,
`MemoryLimitExceededException`, `StatementsCountOverflowException` and `RecursionDepthOverflowException` are all
`JintException` and none is a `JavaScriptException`, so the sink never sees them and they erupt from whatever is
pumping — the host's own worker loop. That is correct and must stay: a worker's budget is the *host's* to
observe, not the parent script's, and `terminate()`-driven cancellation surfacing as
`ExecutionCanceledException` out of the host's `ProcessTasks()` is precisely what the host asked for.

---

## 4. Q3 — Constraint inheritance

### Recommendation

**Factories are replayed, instances are never copied, and both are opt-out — `CreateDefaultOptions()` does it
and a provider that builds its own `Options` gets none of it.**

```csharp
public Options CreateDefaultOptions()
{
    var options = new Options();

    // The termination token, always.
    options.CancellationToken(TerminationToken);

    // Every FACTORY the parent registered, replayed so the worker gets its own instances.
    // ConstraintFactories is internal, so this is the only place it can be done at all.
    foreach (var factory in Parent.Options.Constraints.ConstraintFactories) options.Constraint(factory);

    // Never Parent.Options.Constraints.Constraints — see below.

    // The three value settings on the same group, which carry no per-execution state.
    options.Constraints.MaxRecursionDepth = Parent.Options.Constraints.MaxRecursionDepth;
    options.Constraints.MaxExecutionStackCount = Parent.Options.Constraints.MaxExecutionStackCount;
    options.Constraints.StackOverflowGuard = Parent.Options.Constraints.StackOverflowGuard;

    options.WebApi.Features = InheritedFeatures();      // §5
    options.WebApi.Workers.Provider = Parent.Options.WebApi.Workers.Provider;   // nesting, refusable
    options.WebApi.Diagnostics.Sink = CreateDiagnosticsSink();
    return options;
}
```

### Why

- **Copying constraint *instances* would be an outright bug**, not a policy choice.
  `OptionsExtensions.Constraint(Options, Constraint)` is documented single-engine-only precisely because
  constraints carry per-execution state — a statement counter, an allocation baseline, a deadline — and
  `Engine.BuildConstraints` (`Jint/Engine.Constraints.cs:30`) exists to give each engine its own instance from
  a factory for exactly this reason. Sharing one `MaxStatementsConstraint` between a parent and a worker on
  two threads shares one counter across two threads and lets either engine's `ResetConstraints()` rewind the
  other's in-flight execution.
- **Factories are the mechanism the codebase already built for "this engine, its own instance".** Replaying
  them is the only inheritance that is even meaningful, and it gives the semantics a host actually wants:
  `LimitMemory(4_000_000)` on the parent means *each* engine is bounded at 4 MB, which is right — allocation
  accounting is per engine. Note that `ConstraintFactories` is `internal`, so `CreateDefaultOptions()` is the
  *only* place this replay can happen at all: a provider cannot do it for itself, which is a second reason the
  request has to offer it.
- **A parent that registered an *instance* directly gets an unbounded worker**, and the documentation must say
  so in those words. The fix is one line (register a factory instead — every built-in constraint extension
  already registers one), and the alternative, silently fabricating a constraint the host never asked for, is
  worse.
- **The rule is: value settings on `Options.Constraints` copy, `Constraint` objects do not.** So
  `MaxRecursionDepth`, `MaxExecutionStackCount` and `StackOverflowGuard` come across — the last one is the
  only thing that covers an `eval`-shaped recursion at all (`AGENTS.md`'s `MaxRecursionDepth` gotcha), and a
  worker without it is a worker that can kill the process.
- **Nothing outside that group is inherited**: not `Strict`, not the module loader, not `Interop`, not
  `Modules`. Those are the provider's to decide, and a worker sharing the parent's CLR interop grant by
  default would be the same by-implication grant §5 refuses for the network.

### The cross-engine wall-clock budget

The thing hosts will ask for next is one deadline covering the parent *and* its workers.
`OperationDeadlineConstraint` (`Jint/Constraints/OperationDeadlineConstraint.cs:65`) is the in-box budget that
survives the per-entry reset, and its documentation is explicit that the instance "expects the same thread
discipline as the engine it is registered with". So:

**One instance per engine, armed from the same `(budget, token)`.** The provider does it in
`CreateWorkerEngine`:

```csharp
var deadline = new OperationDeadlineConstraint();
var options = request.CreateDefaultOptions().Constraint(deadline);
deadline.Begin(_remainingBudget, request.TerminationToken);
```

A genuinely shared instance is **out of scope**: `Begin` writes a `long` and a `CancellationToken` that
`Check` reads from another thread, which is not a contract that class makes and not one worth widening for
this. Say so, rather than letting a host discover it.

---

## 5. Q4 — The worker global scope

### Recommendation

**The global object Jint already builds, plus four names. No `WorkerGlobalScope` interface object, no
`importScripts`, no `location`.**

Installed by the engine on the worker engine at step 8 of §3.1:

| Name | What it is |
| --- | --- |
| `postMessage(message, transferOrOptions?)` | the worker half of the port |
| `onmessage`, `onmessageerror` | event-handler IDL attributes over that port's listener list |
| `close()` | §3.4, from the worker side |
| `name` | the `name` option, a string |

Already present under `WebApiFeatures.GlobalEvents`, and therefore **not** re-invented:
`self` (`Jint/WebApi/WebApiRegistration.cs:210`), `addEventListener`, `removeEventListener`, `dispatchEvent`,
`ErrorEvent`, `PromiseRejectionEvent`, and the synthetic listener list behind them.

**There is deliberately no `WorkerGlobalScope`/`DedicatedWorkerGlobalScope` interface object.**
`GlobalEventTarget`'s own remarks already argue why the global object is not made an `EventTarget` — it would
mean giving it a prototype chain it does not have and re-arguing every own-property and inline-cache promise it
makes to the engine and to hosts. A worker global in Jint is the existing global plus a port façade, which is
the same flat shape `README.md:1148` records for `FetchEvent` having no `ExtendableEvent` interface object.

### Which `WebApiFeatures`

```
InheritedFeatures() = parent.Advanced.WebApiFeatures
                    & ~(Fetch | EventSource | WebSocket | Storage | CacheApi | FetchEvents)
                    | Messaging | GlobalEvents | Workers
```

- **The subtraction is the whole point.** Those six are exactly the flags `Options.WebApi.cs` and `README.md`
  say are *only ever granted by name and never by a feature closure* — outbound network (`Fetch`,
  `EventSource`, `WebSocket`), persistent state (`Storage`, `CacheApi`) and inbound request routing
  (`FetchEvents`). A worker inheriting them would be the first place in this codebase where a grant arrives by
  implication, and "the parent could reach the network so the worker may too" is exactly the reasoning
  `WebApiFeatures.Default` was designed to refuse.
- **`Messaging | GlobalEvents` are forced on**, because the worker global's `postMessage` *is* a port and
  `CreateMessagePortPair` requires `Messaging` on both engines.
- **`Workers` is inherited together with the provider**, so nested workers work by default and a provider
  refuses them by clearing `options.WebApi.Workers.Provider` — the `MaxWorkers` quota is then per engine,
  which bounds a tree rather than a list.
- **The provider overrules all of it.** `CreateDefaultOptions()` is a proposal; a provider that assigns
  `options.WebApi.Features` afterwards gets what it asked for.

### `importScripts` — declined, and why

1. **It is synchronous fetch-and-execute**, and a synchronous network fetch from inside a script statement is
   the one thing this whole feature family refuses: `fetch` is a promise, `EventSource` is a stream, a module
   load is a promise, and `ModuleOperations.ThrowIfBlockedInsideJob` exists specifically to refuse a blocking
   load from a place where it could not progress. Implementing `importScripts` means either blocking the
   worker's thread on host I/O inside a statement, or lying about its synchrony.
2. **It only exists for classic workers, and Jint has no classic-script loader.** `IModuleLoader` loads
   modules. A classic loader would be a second, parallel pipeline with its own resolution, caching and naming
   rules, and `Module.Location`'s whole contract (`Jint/Runtime/Modules/ModuleFactory.cs`, and the gotcha in
   `AGENTS.md`) would have to be re-argued for a thing that is not a module.
3. **It is absent rather than present-and-throwing**, which is this repository's established convention for a
   declined capability — `process.exit`, `PerformanceObserver` — so `typeof importScripts === 'function'`
   feature detection takes its other branch.
4. **The replacement is already there and is better**: `import()` inside a module worker, resolved by the
   worker's own loader, driven asynchronously by the host's own pump, with `IAsyncModuleLoader` available for
   a host that fetches over the network.

The same four points are why `type: 'module'` is required (§3.3): a classic worker's two defining features are
`importScripts` and a sloppy-mode non-module global, and Jint would have to invent both. Browsers, Deno and
workerd have all converged on module workers as the shape new code is written in.

*Alternative, one line: a host that must run a legacy classic worker installs its own `importScripts` with
`Engine.Advanced.AddLazyGlobal` and owns the blocking read — which is the honest place for that decision.*

### Not provided, and each absent rather than faked

`SharedWorker`, `ServiceWorker`/`ServiceWorkerGlobalScope`, `location`
(`WorkerLocation` describes a document URL an embedded engine does not have),
`navigator.hardwareConcurrency` (`navigator` carries `userAgent` and nothing else, `README.md:529`),
`importScripts`, and `WorkerGlobalScope` itself.

---

## 6. Q5 — Ordering

Everything guaranteed here **falls out of machinery that already exists**; no new ordering mechanism is
proposed.

### Guaranteed

- **Per-direction FIFO.** `MessagePortEndpoint.Post` enqueues onto one `ConcurrentQueue`, and `JsMessagePort`
  keeps its own `Queue<SerializationRecord>` drained one message per event-loop job
  (`JsMessagePort.DrainOne`). Two messages posted by one engine arrive in that order.
- **A message is a task, never a microtask.** Every promise reaction already queued on the receiver runs
  first, and each message gets its own microtask checkpoint — the rule
  `TimerTests.EachTimerGetsItsOwnMicrotaskCheckpoint` pins for timers, inherited whole.
- **A message is a snapshot at post time.** Serialization is synchronous on the sender, so a later mutation
  cannot reach it and a `DataCloneError` is raised at the `postMessage` call.
- **The start job precedes every message.** Both go through the worker's own queue in order, and the worker's
  inner port queue is not even enabled until the module has evaluated (§3.2), so messages posted before the
  worker is ready are buffered in order rather than lost.
- **`terminate()` is an immediate fence in the parent→worker direction.** The endpoint's `volatile bool` is
  set on the calling thread, so nothing posted after it returns is ever enqueued.

### Explicitly not guaranteed

- **No ordering between two workers.** Two workers are two engines with two event loops that the *host*
  interleaves. A host that pumps A a thousand times before B has starved B, and the engine will not notice.
- **An `error` may be observed before a message the worker posted first.** A message costs two jobs on the
  receiver (receive-then-drain, which is what makes `start()` ordering right) and an error report costs one,
  so `postMessage(x); throw e;` in a worker delivers the error event first. This is inherent to the two-job
  port design and is not a bug to fix.
- **No ordering between a port message, a `BroadcastChannel` message and a timer.** Different queues, promoted
  by different rules.
- **No promise that a worker runs at all.** An engine nobody pumps never loads its script, never receives a
  message and never fires `error` — the family contract restated.
- **No delivery latency bound.** `TimeUntilNextScheduledWork` reports the engine's *own* schedule; a message
  arriving from another thread right after a `null` was handed back is the documented staleness, so a host
  must keep a cadence ceiling.
- **Not a memory model.** See §7.

---

## 7. structuredClone and transfer across the pair

- `worker.postMessage(v)` is StructuredSerializeWithTransfer on the sender and StructuredDeserialize on the
  receiver — already exactly what `JsMessagePort.PostMessage` does. The record between them is engine-neutral
  by construction, and `Jint.Tests.Runtime.WebApi.SerializationRecordTests` already pins that from the type
  declarations *and* from a walk of a real graph.
- **`{ transfer: [buffer] }` is a real cross-thread move.** The sender detaches the `ArrayBuffer` before the
  record leaves, and the receiver's `ArrayBuffer` is built over the very `byte[]` the record carries
  (`StructuredDeserializer`, line 263). A record is consumed exactly once, and a worker pair is a single
  destination, so the move is sound — unlike `BroadcastChannel`, which has to ask for the copying deserializer.
  This is the one place a worker gets zero copies, and it is worth naming in the README.
- **`SharedArrayBuffer` is refused** with a `DataCloneError`
  (`StructuredSerializer.cs:308`). So there is no shared memory between parent and worker, and `Atomics.wait`
  cannot synchronize them. **Two Jint engines are always two agent clusters**, whatever threads they run on.
  This is a deliberate non-goal, not an oversight: a cross-engine `SharedArrayBuffer` would need a cross-engine
  waiter list and would make `Atomics.wait` on the parent's thread block a thread the host owns.
- **Transferring a `MessagePort` is refused** (`StructuredSerializer.cs:460`), so `new MessageChannel()` cannot
  be handed to a worker and `worker.postMessage(msg, [port])` is a `DataCloneError`. **This is the largest
  observable gap against the web platform in the whole proposal** and should be named as such. It is also the
  natural follow-up: the endpoint machinery already spans engines, so what is missing is a serialized form for
  "an endpoint to re-entangle on the far side", not a new transport.
- `Error` objects clone as data (name flattened to the seven standard names, plus message and stack); functions
  and symbols do not.

---

## 8. Restore and dispose, on either side

| Event | What the connection does |
| --- | --- |
| `RestoreGlobalSnapshot` on the **parent** | End the connection: close **both** ports, cancel the token, `OnWorkerEnded(ParentRestored)` |
| `RestoreGlobalSnapshot` on the **worker** | Same, `WorkerRestored` |
| `Engine.Dispose()` on the **parent** | Same, `ParentDisposed` |
| `Engine.Dispose()` on the **worker** | The host's act. The engine never disposes a worker engine — it does not own it |

Both restore hooks hang off `ResetTransientEvaluationState` (`Jint/Engine.GlobalSnapshot.cs:257`) via
`WebApiEngineState.ResetTransientState` (`Jint/Engine.WebApi.cs:653`), beside the existing entries.

**Closing *both* ports rather than merely forgetting the local one is the point**, and the precedent is
verbatim: `ResetTransientState`'s own remarks say a `BroadcastChannel` is "closed rather than merely
forgotten … with one addition: the broker it joined may be the host's and may outlive this engine, so leaving
the subscription there would keep this engine reachable and go on costing every future sender a job the
generation fence then throws away." A worker connection is that, squared — the far side is a whole engine.

Two consequences to state plainly:

- **The generation fence would have handled it anyway, and is not sufficient.** `MessagePortEndpoint` captures
  the generation at *creation*, so a restore on either engine already stops delivery. What it does not stop is
  the *other* side going on serializing values, enqueuing jobs and holding the dead engine reachable. Closing
  is what stops that.
- **A parent restore leaves the `Worker` object alive but inert.** `postMessage` is a silent no-op (a closed
  port has no target), `terminate()` is idempotent, no further events. That is the same honesty
  `FetchHandlerOperation.ObserveAbandonment` provides, and for the same reason: a host must not poll forever.
- **Adding a live worker connection to `Engine.Dispose` is required, not optional.** `Dispose` already releases
  host abort bridges because "a long-lived token would otherwise keep every engine it was ever handed to
  reachable" (`Jint/Engine.cs:2860`). A live endpoint holds an entire second engine, which is strictly worse.

---

## 9. Test matrix

Split per the repository's rule: mechanism in `Jint.Tests`, third-party reachability in
`Jint.Tests.PublicInterface` (the only project without `InternalsVisibleTo`). Every pin below is chosen so that
removing the mechanism it names makes it fail — the mutation is written next to it.

### `Jint.Tests/Runtime/WebApi/WorkerTests.cs`

| Pin | Mutation it catches |
| --- | --- |
| `NoProviderMeansNoWorkerGlobal` | installing `Worker` on the flag alone |
| `AClassicWorkerRequestIsATypeError` | accepting `new Worker(url)` and running it as a module |
| `AProviderRefusalIsASecurityError` | turning a `null` return into an async `error` event |
| `AProviderExceptionPropagatesUnchanged` | wrapping provider failures in a `DOMException` |
| `MoreThanMaxWorkersIsAQuotaExceededError` | dropping the quota |
| `TheWorkerScriptDoesNotRunOnTheParentsThread` | running the start on the parent instead of queuing it on the worker's loop |
| `AMessagePostedBeforeTheWorkerEvaluatesIsBuffered` | enabling the inner port queue at construction |
| `MessagesArriveInPostOrder` | draining more than one message per job |
| `EachMessageGetsItsOwnMicrotaskCheckpoint` | promoting a message ahead of queued reactions |
| `AWorkerErrorReachesTheParentWithNullError` | passing the worker's `JsValue` across |
| `AnUnhandledWorkerErrorReachesTheParentsGlobalErrorEvent` | skipping HTML's third step |
| `AParentListenerCallingPreventDefaultStopsTheGlobalEvent` | ignoring *notHandled* |
| `APostMessageAfterTerminateIsNeverDelivered` | cancelling the token but not closing the ports |
| `TerminateStopsARunningWorkerWithinTheAmortizedInterval` | not registering the termination token |
| `TerminateIsIdempotent` | ending the connection twice |
| `CloseFromTheWorkerEndsTheConnection` | `close()` only closing the inner port |
| `AParentRestoreEndsTheConnection` | dropping the creation-time generation capture, or forgetting instead of closing |
| `AWorkerRestoreEndsTheConnection` | closing only the local half |
| `ParentDisposeEndsEveryConnection` | leaving a live endpoint holding the worker engine |
| `ATransferredArrayBufferIsMovedNotCopied` | copying the storage on the worker boundary |
| `ASharedArrayBufferIsADataCloneError` | admitting SAB and inventing a cross-engine agent cluster |
| `AWorkerCannotBeSentAMessagePort` | silently dooming instead of refusing |
| `TheWorkerGlobalHasNoImportScripts` | adding a throwing stub instead of leaving it absent |
| `TheWorkerGlobalHasNoWorkerGlobalScopeInterfaceObject` | growing the global's prototype chain |
| `AWorkerConstraintFailureEruptsFromTheHostsPump` | flattening a `JintException` into an `error` event |

### `Jint.Tests.PublicInterface/WebApiWorkerTests.cs`

| Pin | What it proves reachable by a third party |
| --- | --- |
| `AHostWorkerProviderIsReachable` | `WorkerProvider` is subclassable outside the assembly |
| `TheDefaultOptionsReplayTheParentsConstraintFactories` | factory replay, and each engine getting its own instance |
| `TheDefaultOptionsDoNotCopyConstraintInstances` | the parent's `MaxStatements` counter is not shared |
| `AWorkerDoesNotInheritNetworkAccess` | `Fetch`/`EventSource`/`WebSocket`/`Storage`/`CacheApi`/`FetchEvents` subtracted |
| `AWorkerInheritsWorkersAndTheProvider` | nesting works, and clearing the provider refuses it |
| `TwoWorkersPumpedFromOneLoopBothMakeProgress` | shape (b) of §2 |
| `OnWorkerStartedSeesAPumpableEngine` | the connection's engine is usable from the callback |
| `TheProviderCanReachPerRequestStateThroughHostDefined` | `request.Parent.Advanced.HostDefined` |
| `ADiagnosticsSinkChainedByTheProviderStillSeesWorkerErrors` | `CreateDiagnosticsSink(inner)` composition |

### Web platform tests

The `workers/` corpus becomes reachable but is **not** vendored in the first PR — the rule that "the change
which first ran a suite is not also the change that moved the engine" cuts the other way here too. When it is
vendored, `WptDivergence.NeedsMessageChannel` (`Jint.Tests/Wpt/WptExclusions.cs:42`, currently declared and
unused) has its wording rewritten: "Jint has no worker story" stops being true, and what remains true is
`MessagePort` transfer.

### Legs

Solution `dotnet build -c Release` (the net462/netstandard legs are where a leaked reference to a net8-only
type shows up), `Jint.Tests` and `Jint.Tests.PublicInterface` on both TFMs, plus the
`JINT_HOST_CONTRACT_VERIFICATION=1` leg — every file under `Jint/WebApi/Workers/` and every test file touching
it wrapped end to end in `#if NET8_0_OR_GREATER`, BCL only.

---

## 10. Implementation order, if this is approved

1. `WorkerProvider`, `WorkerRequest`, `WorkerConnection`, `WorkerEndReason`, `Options.WorkerOptions`,
   `UseWorkers`, `WebApiFeatures.Workers = 1 << 24`. No script surface yet; the pins are the options and
   closure ones.
2. The `Worker` interface object and constructor, entanglement, the deferred inner-queue enable, the start job.
   Messages both ways. The ordering pins.
3. The worker global scope's four names, `close()`, `terminate()`, the termination token.
4. The error channel: `CreateDiagnosticsSink`, the parent-side `ErrorEvent`, HTML's third step.
5. Restore/dispose on both sides, and the `Engine.Dispose` entry.
6. README section, beside "Channel messaging can span two engines" — which is the same picture with the
   plumbing done for the script.

## 11. Open questions

- **`MessagePort` transfer** (§7) is the one real gap. Worth its own issue: it needs a serialized endpoint
  form and a re-entangle step on the far side, and it unblocks `Comlink`-shaped code.
- **`SharedWorker`** is deliberately unaddressed. It needs a name registry across engines, which is
  `BroadcastChannelBroker`'s shape — plausible later, not now.
- Whether `WorkerConnection` should expose a `Faulted`/`Error` pair so a host can see the worker's startup
  failure without a `DiagnosticsSink`. Leaning yes; it is the `FetchHandlerOperation.Error` shape.
