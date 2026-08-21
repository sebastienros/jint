# Web Workers in Jint — design

**Status: finalized design; implementation under way.** The authoritative statement of this design is the body
of [sebastienros/jint#3167](https://github.com/sebastienros/jint/issues/3167), finalized 2026-08-21 after a
cross-runtime survey, an HTML-spec fidelity audit, an engine red-team and an embedder walkthrough. **Where this
document and that issue disagree, the issue wins**, and this file is brought back into line rather than argued
against. What this file adds is the longer form: the engine mechanisms each decision rests on, named so that a
reader can find them.

Everything normative here was read from the [HTML Standard](https://html.spec.whatwg.org/multipage/workers.html),
[Web Messaging](https://html.spec.whatwg.org/multipage/web-messaging.html) and
[WebIDL](https://webidl.spec.whatwg.org/), not from secondary documentation.

> **On citations.** An earlier revision of this file cited `File.cs:123` throughout, and within a handful of
> commits most of those numbers pointed at the wrong lines — a failure mode that is silent, because a wrong line
> number still reads like a fact. This revision cites **files and members** instead. They survive an edit, and
> `grep` finds them.

**What has landed so far**: step 1 of §14 — the host-facing types (`Jint/WebApi/Workers/`), the options group,
`WebApiFeatures.Workers`, `UseWorkers`, and `Options.CopySecurityPosture` with its reflective pin. There is
**no script surface yet**: `typeof Worker` is `undefined` on every engine, whatever the flags say.

---

## 1. The premise

Nearly everything `new Worker()` needs already exists in Jint, and each piece is already load-bearing for
something else:

| What a worker needs | What already does it |
| --- | --- |
| A second isolated global | a second `Engine` |
| A channel between the two | `Engine.Advanced.CreateMessagePortPair` (`Jint/Engine.Advanced.WebApi.cs`) |
| A value crossing without a `JsValue` crossing | `SerializationRecord` — engine-neutral, pinned by `SerializationRecordTests`; since #3197 it may carry `MessagePortEndpoint`s, which its class remarks argue safe |
| Delivery on the receiver's own thread | `Engine.AddToEventLoop(action, generation)` — internal, so the feature needs no new public enqueue API |
| A fence so a dead cycle cannot be posted into | the generation each `JsMessagePort` captures **at its own construction** (`JsMessagePort._generation`) — the fence is per **port**, not per endpoint, which is exactly what lets a *transferred* side rejoin the receiving engine's current cycle |
| A queue that buffers while disabled and drains in order on enable | `MessagePortEndpoint`'s queue plus `JsMessagePort`'s deferred enable, built for transfer by #3197 |
| `self` / `addEventListener` / `dispatchEvent` on the global | `WebApiFeatures.GlobalEvents` (`Jint/WebApi/WebApiRegistration.cs`) |
| Module loading driven by the host's own loop | `Engine.Modules.StartImport` — legal from inside a job on the worker's own loop; it never reaches `ModuleOperations.ThrowIfBlockedInsideJob`, which guards the *blocking* import |
| An error channel the host cannot lose | `DiagnosticsSink` |
| A host-owned pump | `Advanced.ProcessTasks` + `Advanced.TimeUntilNextScheduledWork` |
| Refusing a second thread inside one engine | the #3035 admission check — *"This Engine is already in use by another thread…"* |

The one thing that does not exist, and must not, is **the thread**. *Jint never starts a thread to run script*
is load-bearing across the whole web-API family, so `new Worker()` is only implementable if the host supplies
the execution resource. That leaves exactly one shape: a **host-supplied worker provider**. The engine owns the
spec-shaped parts (port entanglement, the worker global, message and error plumbing, ordering, `terminate()`
semantics); the host owns every thread, every pump, and the worker engine's configuration.

**This layering is the universal one, not an improvisation.** `deno_core::JsRuntime` is thread-pinned and not
`Send` while `deno_runtime::WebWorker` above it spawns the OS thread; QuickJS's `os.Worker` lives in
`quickjs-libc.c` (the optional host library, behind `USE_WORKER` + pthreads) while `quickjs.c` carries no
runtime mutex; V8 admits one thread per isolate and Chromium supplies thread-plus-isolate per worker — **the
browser is itself a host-supplied worker factory, with Chromium in the role of the provider**; JSC locks at the
`JSVirtualMachine`; GraalJS offers workers only in its Node distribution; ClearScript has no `Worker` at all.
WASI standardized the same split as [`thread-spawn`](https://github.com/WebAssembly/wasi-threads), whose
rationale is verbatim ours: the portable layer "avoided specifying how thread spawning should occur… This
allows other uses… to specify their own mechanism for spawning threads." The only novelty here is making the
boundary a *public extension point*, which is the correct consequence of Jint being a library and not a runtime.

**WinterTC blesses the posture.** The
[Minimum Common Web Platform API](https://min-common-api.proposal.wintertc.org/) §5.3: *"This Standard does not
require runtimes to support web workers"* — and where a global maps to `WorkerGlobalScope`, it shall expose
`onerror`, `onunhandledrejection`, `onrejectionhandled` and `self`. §6 explicitly permits firing those events
"through a suitable alternative mechanism available at the global scope" where the global cannot be an
`EventTarget`, which is the standing blessing for `GlobalEventTarget`'s synthetic listener list and for
shipping no `WorkerGlobalScope` interface object. `MessageChannel`/`MessagePort`/`MessageEvent`/
`structuredClone` *are* in the minimum API, and Jint already ships that mandatory half. `Worker` is the
optional half.

---

## 2. The API

Landed, in `Jint/WebApi/Workers/`:

```csharp
namespace Jint.WebApi;

/// The host's answer to `new Worker(...)`: it decides whether a worker may exist at all, builds the
/// engine that runs it, and — because Jint never starts a thread — decides which thread pumps it.
public abstract class WorkerProvider
{
    protected WorkerProvider() { }

    /// Builds the engine for one `new Worker(...)`, or returns null to refuse.
    /// Runs on the PARENT's thread, synchronously, while the parent's script is suspended in the
    /// constructor: it must not run script, must not block, and must NOT fetch the worker's script —
    /// that is the worker's own IModuleLoader's job, on the worker's own pump.
    public abstract Engine? CreateWorkerEngine(WorkerRequest request);

    /// Ports entangled, worker global installed, start job queued on the WORKER's loop.
    /// Runs on the parent's thread; this is where the host starts pumping. Register the connection
    /// BEFORE starting the pump — OnWorkerEnded may be invoked concurrently, before this returns.
    public virtual void OnWorkerStarted(WorkerConnection connection) { }

    /// The connection ended — a SIGNAL ONLY, on whichever thread ended it (frequently NOT the
    /// worker's). Do not touch connection.Worker from it beyond reading immutable properties: no
    /// Dispose(), no ProcessTasks(). Signal your pump loop; it observes IsEnded (or wakes on
    /// TerminationToken), leaves, and disposes the engine on the thread that was pumping it.
    public virtual void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason) { }
}

public sealed class WorkerRequest            // internal constructor: the engine hands it over
{
    public Engine Parent { get; }                 // suspended mid-statement; read it, don't run it
    public string Specifier { get; }              // verbatim, unresolved
    public string? ReferencingLocation { get; }   // the calling module's Module.Location, or null
    public WorkerType Type { get; }               // Module — the only value, see §8
    public string Name { get; }
    public int Depth { get; }                     // 0 for a top-level worker; >0 only under opted-in nesting
    public int LiveWorkerCount { get; }           // the parent engine's current live connections
    public CancellationToken TerminationToken { get; }

    /// Fresh Options each call, pre-wired for this worker (§7). Never the parent's instance.
    public Options CreateDefaultOptions();
}

public sealed class WorkerConnection          // internal constructor; fully thread-safe
{
    public Engine Parent { get; }
    public Engine Worker { get; }
    public string Name { get; }
    public bool IsEnded { get; }
    public WorkerEndReason? EndReason { get; }
    public bool IsFaulted { get; }
    public Exception? Error { get; }              // always a CLR exception, never a worker-realm JsValue
    public CancellationToken TerminationToken { get; }
    public object? HostState { get; set; }        // the engine never reads it
    public void End();                            // the host's own terminate(); safe from any thread
}

public enum WorkerType { Module }
public enum WorkerEndReason
{
    Terminated, ClosedByWorker, StartupFailed,
    ParentRestored, WorkerRestored, ParentDisposed, WorkerDisposed,
}
```

```csharp
// Options.WebApi.Workers.{Provider, MaxWorkers = 16, MaxQueuedMessages = 16384}, plus one extension
// that sets flag and provider together so the two cannot get out of step:
public static Options UseWorkers(this Options options, WorkerProvider provider);
// WebApiFeatures.Workers = 1 << 24 (1 << 23 = FetchEvents was the highest bit), never in Default.
```

`End()` is **idempotent under concurrent callers**, not merely repeated ones: a lock decides the single winner,
the ended flag is written last and volatile so a reader that sees it also sees the reason and the error, and the
end callback runs *outside* the lock, so no host code ever runs under one.

**Why options-held**: every host capability in this subtree lives on options and is read once at build
(`StorageProvider`, `BroadcastChannelBroker`, `ConsoleSink`, `DiagnosticsSink`,
`FetchOptions.HttpClientFactory`); host code is knowable at options time. `Engine.Advanced.SetFetchHandler` is
not a counter-example, because it registers a *script value*. **Why abstract class**: `StorageProvider`'s
reasoning verbatim — later revisions can add members; a `Func<>` can never grow a parameter. **Why it returns
an `Engine`**: the host has to pump it, and pumping is `ProcessTasks()` bounded by `TimeUntilNextScheduledWork`;
an opaque handle would re-export both members while hiding the object the host must hold. `Provider = null` (the
default) leaves the `Worker` global uninstalled even with the flag on, so `typeof Worker === 'undefined'` — the
family's absent-rather-than-throwing convention.

**Pooled hosts: one provider per process, per-request policy from `HostDefined`.** Everything that varies per
request — tenant, loader root, budget — is read inside `CreateWorkerEngine` from
`request.Parent.Advanced.HostDefined`, which is per engine, never read by the engine, and survives
`RestoreGlobalSnapshot`. Do **not** set the provider through
`engine.Advanced.EnableWebApis(…, w => w.Workers.Provider = …)` on a pooled host: `ApplyLive` hands the callback
the engine's `Options.WebApi` group, and a shared `Options` is shared there too — that write is a cross-tenant
leak. A host needing *asynchronous* per-request policy puts the lookup in the worker's own
`IAsyncModuleLoader`, so the check runs on the worker's pump and a refusal becomes `StartupFailed` plus a parent
`error` event.

---

## 3. New core engine API: the pump wait (its own PR, before Workers)

Thread-per-worker hosts would otherwise poll `ProcessTasks` + `Thread.Sleep(min(TimeUntilNextScheduledWork,
ceiling))`, and a message posted from the parent while the worker thread sleeps waits out the ceiling — with a
20 ms ceiling that is a ~20 ms RPC to an *idle* worker, worse than loopback HTTP. `TimeUntilNextScheduledWork`
is documented as describing only the engine's *own* schedule; for a worker, cross-thread arrivals are the
*entire* traffic. The internals are already complete: `EventLoop`'s work-arrived event, `WaitForWork`
(reset-then-check closes the producer race) and `WaitForEventAsync`, assembled in `DrainEventLoopUntil`.

```csharp
// Engine.AdvancedOperations — every target framework, no web-API flag, no #if.
public bool WaitForScheduledWork(TimeSpan timeout, CancellationToken cancellationToken = default);
public Task<bool> WaitForScheduledWorkAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
```

Contract: `true` when there is (probably) work — a job arrived from any thread, or the engine's own next
scheduled work came due (bounded internally by `TimeUntilNextPumpScheduledWork`, so a worker's
`setTimeout(f, 1)` fires in ~1 ms rather than at the ceiling); `false` on timeout; `OperationCanceledException`
on the token. **It does not pump** — the caller then calls `ProcessTasks()`, preserving "there is deliberately
no third method that drains for a budget". Single-drainer only (it resets the work-arrived event); spurious
wakes are expected. It ships ungated and unconditionally because it serves every existing host that awaits an
interop `Task` from its own loop — so **Workers itself adds zero new core-engine API**, which is the stronger
story.

---

## 4. The thread contract

The merged #3035 admission check is what turns this section from etiquette into hard rules. `Engine` claims
`_ownerThreadId` per host operation; a second thread's entry throws
`InvalidOperationException: "This Engine is already in use by another thread or has an asynchronous operation in
progress."` Same-thread re-entry is fine. `ProcessTasks`, `Dispose`, `RestoreGlobalSnapshot`, `Constraints.*`,
every `Modules.*` entry and every `ModuleImportOperation` getter are guarded; `AddToEventLoop` (a
`ConcurrentQueue`), `CreateMessagePortPair` and `TimeUntilNextScheduledWork` are not — and the last reads the
timer heap, so **only the pumping thread may read it**.

1. **No part of the worker's script ever runs on the parent's thread.** Everything the engine does to the worker
   engine before hand-off is mutation of a quiescent engine; everything after is a generation-stamped job on the
   worker's own queue — the one part of another engine any thread may touch.
2. **Quiescence is validated, not trusted.** Construction steps 6–10 (§5) run under `EnterHostCall` on the
   worker engine, released before `OnWorkerStarted`. A provider that hands back an engine another thread is
   pumping (the pre-warmed-pool bug) gets the engine's own admission error at `new Worker()`, not silent
   corruption.
3. **`OnWorkerEnded` is a signal.** It may run on either thread, concurrently with `OnWorkerStarted`, even
   before `OnWorkerStarted` returns. It must not `Dispose()` or `ProcessTasks()` the worker engine — in the
   thread-per-worker shape a `terminate()` ends the connection on the *parent's* thread while the worker thread
   sits inside `ProcessTasks`, and a `Dispose()` from the callback is exactly the admission exception, thrown
   out of the middle of the parent's script. The pump thread disposes, after its loop observes `IsEnded`.
4. **The hand-off has a memory-ordering edge.** Everything the engine wrote happens-before `OnWorkerStarted`
   returns; `Thread.Start()` or any concurrent collection gives the host its edge to the first pump. A
   connection stashed in a plain field for an existing loop to pick up does not. (No other runtime has this
   hazard, because no other runtime hands the boundary to the host — which is why it is documented.)
5. **`End()` touches only endpoints, a `CancellationTokenSource`, and interlocked bookkeeping.** That is what
   makes it any-thread-safe. The live-worker count is an `Interlocked` counter plus a locked list — an explicit,
   commented carve-out from `WebApiEngineState`'s engine-thread-only rule, the same carve-out
   `MessagePortEndpoint` documents.

The two pump shapes:

```csharp
// (a) one thread per worker — parks on the wait, wakes on post or terminate, disposes after the loop
sealed class ThreadPerWorker : WorkerProvider
{
    public override Engine? CreateWorkerEngine(WorkerRequest request)
        => new Engine(request.CreateDefaultOptions().EnableModules(_workerRoot));

    public override void OnWorkerStarted(WorkerConnection c)
        => new Thread(() =>
        {
            while (!c.IsEnded)
            {
                c.Worker.Advanced.ProcessTasks();
                try { c.Worker.Advanced.WaitForScheduledWork(_ceiling, c.TerminationToken); }
                catch (OperationCanceledException) { }   // terminate() — fall out via IsEnded
            }
            c.Worker.Dispose();                          // on the pumping thread, after the loop
        }) { IsBackground = true }.Start();

    // OnWorkerEnded: nothing to do — the loop above observes IsEnded/token itself.
}

// (b) N workers cooperatively on the host's existing loop (a game frame), each on a bounded slice.
// ProcessTasks drains until empty, so an unbounded worker handler would eat the frame; the slice is an
// OperationDeadlineConstraint (per engine, held in connection.HostState), the one constraint built for
// exactly this: no-op Reset, IsAmortizable, erupts as TimeoutException from ProcessTasks.
foreach (var c in _live)
{
    var slice = (OperationDeadlineConstraint) c.HostState!;
    slice.Begin(_frameSliceForWorkers, c.TerminationToken);
    try { c.Worker.Advanced.ProcessTasks(); }
    catch (TimeoutException) { /* overran its slice; resumes next frame */ }
    finally { slice.End(); }
}
parent.Advanced.ProcessTasks();
```

`OperationDeadlineConstraint.Begin` from `CreateWorkerEngine` on the parent's thread is safe only because
`Thread.Start()` publishes it; never call `Begin`/`End` on a worker's deadline after its pump has started.

---

## 5. Construction

`new Worker(specifier, options)` on the parent's thread, in order:

1. **WebIDL.** `type` must be `'module'` — anything else is a `TypeError`; an unknown value is the WebIDL
   enum-conversion `TypeError`, `'classic'` (the spec's own default!) is Jint's refusal, and only the latter's
   message names the fix. `name` is read; `credentials` is validated as the `RequestCredentials` enum and then
   ignored, because it only parameterizes the fetch, which the worker's loader owns. Jint does not URL-parse the
   specifier, so the spec's `SyntaxError` for an unparseable URL becomes whatever the worker's loader reports
   later.
2. **Quota.** More than `MaxWorkers` (default **16** — Chrome allowed 16/tab, Firefox 20, Opera threw
   `QUOTA_EXCEEDED_ERR` at 17) live connections throws `QuotaExceededError` with `quota`/`requested` — the
   `MaxActiveTimers` refusal shape, which HTML's *Killing scripts* section explicitly licenses. It is a
   **per-engine backstop, not the policy**: the provider is the policy (it refuses anything by returning
   `null`) and a process-wide budget is the provider's own `Interlocked` counter, which `Depth` and
   `LiveWorkerCount` exist to feed. Chrome's alternative (queue the 17th until one exits) is deliberately not
   adopted: queuing needs a scheduler the engine does not own.
3. **Mint the `CancellationTokenSource`** before calling the provider, so the token can be registered on options
   the provider has not built yet.
4. **`provider.CreateWorkerEngine(request)`.** `null` is a refusal → synchronous `SecurityError` `DOMException`
   (a policy decision, not a fetch failure; the constructor already throws synchronously in the spec, and Chrome
   throws `SecurityError` from `new Worker()` for a disallowed script). Anything the provider throws propagates
   unchanged.
5. **Validate the returned engine**, `InvalidOperationException` on each: not the parent; not already connected;
   carries `WebApiFeatures.Messaging`; and **carries a `CancellationConstraint` registered with
   `request.TerminationToken`**. `CreateDefaultOptions()` writes that line, so a hand-rolling provider owes
   exactly one, and a forgotten token stops being this design's most dangerous silent failure — a deaf-mute
   worker still burning a thread, a state no other runtime can even be in — and becomes a construction-time
   error.
6. **Entangle** (`MessagePortBridge.CreatePair`), under `EnterHostCall` on the worker engine (§4 rule 2) from
   here through step 10. Neither port is handed to script: the `Worker` object *is* the parent half's façade and
   the worker global *is* the worker half's — HTML's two unexposed ports, exactly (`Worker` and
   `DedicatedWorkerGlobalScope` both `include MessageEventTarget`; there is no `worker.port`, that is
   `SharedWorker`'s).
7. **Enable the parent half now; leave the worker half disabled.** Divergence, documented: HTML enables *both*
   queues only after the initial script evaluates, so a message the worker posts during its own evaluation may
   reach the parent sooner than a browser would deliver it. The deferred *inner* queue is what makes
   `const w = new Worker(u, {type:'module'}); w.postMessage(1)` buffer in order until the module has evaluated —
   which is also why there is no `workerData`: the browser idiom already works.
8. **Install the worker global scope** (§8).
9. **Queue the start job on the worker's event loop**, carrying the worker's generation. The job calls
   `Modules.StartImport(specifier)` — legal inside a job — and attaches reactions to the operation's promise:
   fulfil enables the inner port; reject takes the `StartupFailed` path (§6). Never poll
   `ModuleImportOperation`; its getters are admission-guarded and polling needs a pump the connection may no
   longer get.
10. **`provider.OnWorkerStarted(connection)`** (ownership released first), then return the `Worker` object.

Resolution goes through **the worker engine's own `IModuleLoader`**, never the parent's: a worker usually has a
different base and permission set, and the parent's loader belongs to a thread that is not the worker's.
`ReferencingLocation` travels so the provider can resolve a relative specifier the way `import()` would;
`ModuleFactory.LocationOf` is public for exactly this.

**The façade needs one new internal mechanism.** `JsMessagePort` today dispatches `message` events at *itself*,
and the hidden ports' listener lists are unreachable. HTML: "All messages received by that port must immediately
be retargeted at the `Worker` object." So `JsMessagePort` gains an internal retarget target consulted in
dispatch — the parent's hidden port retargets at the `Worker` object, the worker's at the global (via
`GlobalEventTarget`). And both façades **enable eagerly and do not reuse the `onmessage`-implies-`start()`
rule**: that rule is scoped to the `MessagePort` interface, not the `MessageEventTarget` mixin, so on a worker
`addEventListener('message', …)` alone must receive — the exact opposite of `MessageChannel`, where `start()` is
required.

---

## 6. Lifecycle: terminate, close, errors

### 6.1 `terminate()` — hard stop, both directions

HTML's *terminate a worker* is four steps: set the closing flag; discard the worker's queued tasks; abort the
running script; **empty the port message queue of the port entangled with the worker's implicit port**. That
last step is the spec's own licence for discarding parent-side messages already posted, which is what stops the
both-ends close from reading as over-aggressive. Jint's mapping, in order:

1. **Close both endpoints.** Engine-enforced, immediate, any-thread (`volatile` + lock on the endpoint — always
   the *endpoint*, never `JsMessagePort.Close()`, whose fields are engine-thread-only). From this instant
   nothing is enqueued in either direction. `postMessage` after `terminate()` still **serializes** before
   discovering there is no target (HTML step 5 precedes step 6): an unserializable value still throws
   `DataCloneError`, and a transfer-listed buffer is still detached. "Silent no-op" is true only for
   serializable values.
2. **Cancel the termination token.** The worker's `CancellationConstraint` throws within 64 statements on its
   own thread (no-op `Reset()`, engine-held amortized countdown) and keeps throwing for every later entry.
3. **`OnWorkerEnded(connection, Terminated)`** — the signal (§4 rule 3).

Two honest statements. Termination is **cooperative** — ≤64 statements on the worker's thread, unbounded while
inside a host CLR call. That published bound is *stronger than the field's*: V8's `TerminateExecution` is also
only frame-bounded ("the isolate cannot resume execution until all JavaScript frames have propagated" the
exception — it does not unwind an embedder's native call), so Node's and Deno's "as soon as possible" carries
the same unbounded tail, undocumented. And Jint's cancellation skips JS `finally` blocks — which is *exactly*
what the spec's *abort a running script* prescribes ("emptying the JavaScript execution context stack without
triggering any of the normal mechanisms like `finally` blocks"), so it is pinned rather than left for somebody
to "fix" later.

### 6.2 `close()` — not terminate, and spec-exact

HTML's *close a worker* is two steps: discard the tasks on the *worker's* queues; set the closing flag. **The
current script runs to completion, and nothing empties the parent-side queue** — terminate's step 4 deliberately
has no counterpart here. So `close()` must not reuse terminate's three steps (an earlier draft's mistake: it
would kill `close(); flushMetrics();` within 64 statements, and lose `postMessage(result); close()` whenever the
parent had not pumped in between — the single most common `close()` idiom).

| | `terminate()` (parent) | `close()` (worker) |
|---|---|---|
| worker-side endpoint (worker stops receiving) | closed immediately | closed immediately |
| parent-side endpoint (worker stops sending) | closed immediately, queue discarded | **drain-then-close**: refuses new posts, already-queued messages stay deliverable, closes when drained or at teardown |
| termination token | **cancelled** | **not cancelled** — the current turn finishes |
| `OnWorkerEnded` | immediately, `Terminated` | after the current job (one `close()` enqueues on the worker's own loop), `ClosedByWorker` |

Pins: `CloseDoesNotAbortTheCurrentlyRunningScript`, `AFinalPostMessageBeforeCloseIsDelivered`,
`TerminateDiscardsAMessageTheWorkerAlreadyPosted` — that last one must stay a discard. The drain-then-close
endpoint state interacts with the stranded-transfer cascade `MessagePortEndpoint.Close()` runs, and with the
`IsChannelExhausted` predicate transferable streams (#3215) consult: a half-closed (draining) endpoint must
**not** read as exhausted while its queue still holds the stream's own `close` message.

### 6.3 Errors — three distinct channels, two spec-mandated shapes

**Load/parse failure** (the specifier did not resolve, the fetch failed, the module graph did not instantiate):
the spec fires **a plain `Event` named `error`** at the `Worker` object — no `ErrorEvent`, no `message`.
Libraries branch on exactly this. The connection ends as **`StartupFailed`**, not `Terminated` — "the specifier
was wrong" reported as "somebody called terminate()" is the most misleading log line the feature could ship —
and `IsFaulted`/`Error` are set on the connection, always a CLR exception (a `ModuleResolutionException`, or a
summary exception carrying message and location strings), never the worker-realm `JsValue`. So a host sees a
startup failure **without wiring any sink**. Order: close the inner endpoint (buffered records discarded,
carried port sides stranded), queue the parent-side event job, close the outer endpoint, `OnWorkerEnded`.

**Runtime error** (an evaluation throw, an uncaught error in a callback): HTML's *report an exception*,
faithfully — fire `error` at the worker's global; **only if *notHandled*** queue a task at the parent firing a
trusted `ErrorEvent` at the `Worker` object with `message`/`filename`/`lineno`/`colno` and **`error: null`**; if
that too is unhandled, re-report one level up to the parent's own global `error` and sink. Three load-bearing
points:

- **`error: null` is the spec, verbatim** — *report an exception* step 5.1 sets it to null before the `Worker`
  object ever sees it, for every worker error, same-origin included. (The realm-identity and
  subclass-flattening arguments are true but secondary.) Node deliberately ships the opposite — a clone through
  a 7-constructor allowlist, unknown subclasses degraded to an inspected string — and that is the
  server-runtime trade, named here so nobody thinks it was overlooked. Jint implements HTML's `Worker`, and a
  worker that wants its parent to have the real failure catches it and `postMessage`s it, where the
  serializer's `Error` support is intentional.
- **The relay is an internal hook, not the `DiagnosticsSink`.** The sink is deliberately unsuppressible by
  script ("a host's diagnostics channel is not something the script it is running may switch off"), and both
  `FireError` consumption sites discard the *notHandled* bool today. HTML's propagation is gated on exactly that
  bool, so a worker-side `preventDefault()` (or a global `onerror` returning `true`) must stop the parent from
  being told. The two consumption sites therefore keep the bool and hand it to an internal per-connection hook
  that sits **beside** the sink: the host's own sink still sees every report, its documented contract, while
  parent propagation honours *notHandled*.
- **Unhandled promise rejections never propagate.** `unhandledrejection` fires at the worker's own global and
  reaches the parent through nothing at all; the relay filters kinds. Pinned, because the sink wiring makes the
  opposite easy to fall into.

`CreateDefaultOptions()` installs `DiagnosticsSink.Null`, which is what flips the worker's callbacks to
report-and-continue (a throwing `message` listener must not kill the pump — HTML's model) and what feeds the
relay. A provider that installs its own sink chains transparently; a provider that clears it gets a worker whose
callback errors erupt from the host's pump and never reach the parent — documented, not papered over.
**Constraint failures are never error events**: `ExecutionCanceledException`, `TimeoutException` and friends are
`JintException` but not `JavaScriptException`, so they erupt from whatever is pumping — the worker's budget is
the *host's* to observe, not the parent script's. And the parent-side event job carries the parent generation
captured at connection creation, so a parent restore drops a queued error event.

For nested workers (where enabled), step 5.2.3's recursion is what produces the spec's up-the-chain
propagation, including its disentangled-port clause — "act as if the `Worker` object had no `error` event
handler" — which is also the exact description of §10's alive-but-inert state.

---

## 7. Constraints, budgets, and the hardened-profile rule

### 7.1 Factories replay; instances never copy

`CreateDefaultOptions()` replays every constraint **factory** the parent registered — each engine then gets its
own instance, which is Node's `resourceLimits` and Moddable's per-worker-budget semantics — and never touches
constraint **instances**. `Options.Constraints.ConstraintFactories` is `internal`, so the request is the only
place the replay can happen at all: a provider cannot do it for itself, which is one reason the request offers
it.

An instance carries per-execution state (a statement counter, an allocation baseline, a deadline) and is
documented single-engine-only; a shared `MaxStatementsConstraint` across two threads shares one counter and lets
either engine's reset rewind the other. Since #3036 that is no longer only a documented rule for the constraint
where sharing corrupts an *accounting* rather than a count: `MemoryLimitConstraint.Attach` throws
`InvalidOperationException` when one instance meets a second `Engine` ("register a constraint factory when
Options is shared"), so an implementation that copied instances here would fail loudly rather than silently
share a budget. A parent that registered an instance directly gets an **unbounded** worker — said in those
words, with the one-line fix (register a factory instead; every built-in constraint extension already does).

The token itself is registered first, through `options.CancellationToken(TerminationToken)`, which is also a
factory registration — so `Constraints.Find<CancellationConstraint>()` on the built engine answers with *this*
token, and a cancellation constraint the parent registered is replayed beside it (a parent that cancels its own
token stops its workers too, which is a restriction travelling and not a grant).

### 7.2 The truth about a pumped engine

`ProcessTasks` runs jobs raw — no `ExecuteWithConstraints`, so **no `ResetConstraints()`**. On a worker that is
only ever pumped, `TimeoutInterval` **never fires** (its deadline stays at the unarmed sentinel) and
`MaxStatements` is a **lifetime** budget that eventually throws forever.

**Memory is the exception since #3036 landed.** Every event-loop job now runs inside an allocation segment and
is `Check()`ed as it completes (`Engine.RunEventLoopJob`), with the operation state captured at *registration*
and carried across continuations and thread hops — so a replayed `LimitMemory` factory genuinely bounds each job
chain on a pumped worker, and `MemoryLimitConstraint.Begin`/`End` spans a whole multi-entry operation exactly as
`OperationDeadlineConstraint` does for wall-clock.

The worker budget is therefore a **pair**: `OperationDeadlineConstraint` for time, `MemoryLimitConstraint` for
allocations — both armed once, both surviving the per-entry reset — while cancellation-shaped constraints handle
termination. Bracketing every pump turn in `ExecuteWithConstraints` was considered and rejected in writing: it
changes behaviour for every existing pumping host and re-creates the per-entry-budget trap in a new place.
Pinned honestly by `APumpedWorkersTimeoutIntervalNeverFires` and `AReplayedMemoryLimitBoundsAWorkerJob`.

### 7.3 Security posture inherits; grants do not

The rule, in Deno's words: *"the permissions of a worker can't be extended beyond its parent's permissions
reach."* The web enforces the same shape structurally — a worker inherits its creator's origin, sandboxing
flags, embedder policy and cross-origin-isolated capability (HTML even floors the last at the owner's, never
raises it). **Withholding a grant and dropping a restriction are opposites**, and an earlier draft's "nothing
outside `Options.Constraints` is inherited" treated them as one rule. Corrected:

- **Grants never travel by implication.** `WebApiFeatures` inherits *minus*
  `Fetch | EventSource | WebSocket | Storage | CacheApi | FetchEvents` — the flags documented as granted only by
  name — **and minus `Workers`: nesting is off by default** (§7.4). `Strict`, `Interop` and the module loader
  are the provider's to decide. A provider that deliberately grants the worker more (assigning `Features`,
  enabling interop) is host code exercising the same authority it has when it builds any engine; that door stays
  open on purpose. The monotonicity is the default, not a cage.
- **Restrictions always travel.** `Options.CopySecurityPosture` copies the parent's whole restrictive posture:
  all **seven** `Options.Constraints` value settings (`MaxRecursionDepth`, `MaxExecutionStackCount`,
  `StackOverflowGuard` — the only cover for `eval`-shaped recursion — plus `RegexTimeout`, `PromiseTimeout`,
  `MaxArraySize`, `MaxAtomicsPauseIterations`), `Host.StringCompilationAllowed` (otherwise `new Worker()` is a
  documented **eval-escape** from a hardened parent), `AgentCanSuspend`, `Json.MaxParseDepth`, the parser bounds
  (`Parsing.MaxSourceLength`, `Parsing.MaxNodeCount`), the four module-graph limits (`Modules.MaxModuleCount`,
  `MaxTotalModuleSourceBytes`, `MaxModuleGraphDepth`, `MaxModuleResolutionHops` — the loader is a grant and
  stays behind, the limits are restrictions and travel) and `Options.ResultLimits`. It lives in
  `Options.cs`, **beside the options it names** rather than in the feature that consumes it, so that a settings
  PR adding a new restriction has the classification in front of it.
- **The classification is pinned reflectively.** `Options.SecurityPostureInherited`,
  `SecurityPostureNotInherited` and `SecurityPostureExcludedGroups` state it in code, and
  `Jint.Tests/Runtime/OptionsSecurityPostureTests.cs` fails unless every value-typed public settable property on
  `Options`, `Constraints`, `Host`, `Json`, `Parsing` and `Modules` is in one of the first two lists, and every option group is either
  scanned or named in the third. `Interop`, `Modules` and `WebApi` are excluded wholesale as grant-shaped, and
  each exclusion carries its reason in code. So the sebros security stack cannot silently become a
  `new Worker()` escape hatch on the day each part lands; if a hardened profile arrives as one switch, the
  worker inherits the *profile*, which is strictly better than inheriting its expansion.
- **The residual hole is named**: a provider that builds its own `Options` from scratch is the one place a
  hardened parent can be un-hardened — deliberate, because the provider is host code. One sentence in the
  `WorkerProvider` docs: *`CreateDefaultOptions()` is a convenience, not a security boundary; a host with a
  hardened profile builds the worker's `Options` from the same hardening helper it built the parent's from.*

### 7.4 Nesting and the fork bomb

An earlier claim that a per-engine `MaxWorkers` "bounds a tree rather than a list" was arithmetically wrong: it
bounds the branching factor of an unbounded-*depth* tree, so a three-line self-spawning module was an unbounded
engine fork bomb with the shipped defaults. It also contradicted this design's own no-grant-by-implication rule,
since inheriting `Workers` + the provider is precisely a grant, by implication, of the capability that
manufactures engines. **Default nesting OFF**: `CreateDefaultOptions()` neither sets `WebApiFeatures.Workers`
nor copies the provider. A provider that wants nesting sets both — one visible line, by which it accepts the
accounting, with `Depth` and `LiveWorkerCount` (plus its own process-wide counter) as the tools to bound the
tree. QuickJS refuses nesting outright; browsers bound the tree only with a *global* cap; Deno's answer is
monotone capability. Off-by-default is the shape all three agree on for a library.

---

## 8. The worker global scope

The global object Jint already builds, plus the worker names. **No
`WorkerGlobalScope`/`DedicatedWorkerGlobalScope` interface objects** — the global is not an `EventTarget` and
has no such prototype chain, so an interface object would make `self instanceof WorkerGlobalScope` lie; absence
is the coherent half, WinterTC §6 blesses the mechanism, and this is the **same ruling as #3195's**
interface-globals decision (`Crypto`/`SubtleCrypto`/`Performance`) — one ruling, not two, with the worker
wrinkle (the canonical "am I in a worker" sniff) decided alongside it.

Installed at construction (step 8):

| Name | What it is |
| --- | --- |
| `postMessage(message, transferOrOptions?)` | the worker half of the port (both WebIDL overloads) |
| `onmessage`, `onmessageerror` | event-handler IDL attributes over that port's listener list |
| `close()` | §6.2 |
| `name` | the `name` option (plain writable data property — the `[Replaceable]` simplification `self` already uses) |
| `onerror` | HTML's legacy shape, spec-exact: invoked with «message, filename, lineno, colno, error», **returning `true` cancels** — this attribute is what decides *notHandled* on the worker side |
| `onunhandledrejection`, `onrejectionhandled` | plain event-handler attributes (the events already fire under `GlobalEvents`; WinterTC §5.3 names these plus `onerror` and `self`) |
| `importScripts(...)` | **present and throwing `TypeError`** — the spec's own step 1 for a module worker prescribes the throw, so `typeof importScripts === 'function'` answers `true` exactly as in a browser. The rare place this repository's absent-rather-than-throwing convention and the spec disagree, and the spec wins: it is prescribing the throw itself. |

Already there under `GlobalEvents` and not re-invented: `self`, `addEventListener`/`removeEventListener`/
`dispatchEvent`, `ErrorEvent`, `PromiseRejectionEvent`. On the parent side the `Worker` object follows the
`JsEventTarget` interface-object pattern (`EventSource`/`WebSocket` precedent) with `onmessage`,
`onmessageerror` and `onerror` — the *plain* `EventHandler` shape (`AbstractWorker`): invoked with the event,
`preventDefault()` or returning `false` cancels. `messageerror` exists on both façades because the interfaces
have it and can never fire, for the reason `JsMessagePort` already records — present, never fired, the same as
`MessagePort` and `BroadcastChannel`.

**Feature mask** (nesting off per §7.4):

```
parent.Advanced.WebApiFeatures
  & ~(Fetch | EventSource | WebSocket | Storage | CacheApi | FetchEvents | Workers)
  | Messaging | GlobalEvents
```

`Messaging | GlobalEvents` are forced on (the worker global's `postMessage` *is* a port, and
`CreateMessagePortPair` requires `Messaging` on both engines). The mask is computed from
`parent.Advanced.WebApiFeatures` — the engine's own closure — rather than from the parent's options, so a live
`Advanced.EnableWebApis` call is accounted for. The provider overrules all of it by assigning `Features`
afterwards.

**Module-only is the settled post-browser default**: Deno requires `type: "module"` ("Currently Deno supports
only `module` type workers"); QuickJS's `os.Worker` takes a module filename; Moddable takes a module name; Node
and Bun have no classic-script concept; **no non-browser runtime ships a working `importScripts`**. The refusal
of `'classic'` is Jint's policy — the spec's default is `'classic'` and nothing licenses refusing it — taken for
Deno's reasons plus two of Jint's own: there is no classic-script loader (`IModuleLoader` loads modules, and
`Module.Location`'s contract would need re-arguing for a non-module), and a synchronous fetch-and-execute inside
a statement is the one thing this family refuses. A host that must run a legacy classic worker installs its own
`importScripts` with `AddLazyGlobal` and owns the blocking read.

Declined, absent (not faked): `location` (`WorkerLocation` — the worker's script name is `Module.Location`,
host-exposable via `import.meta`; revisit on demand, §15), `navigator` beyond what the parent's `Navigator` flag
inherited (`hardwareConcurrency` stays absent — the host owns the threads), `SharedWorker`, `ServiceWorker`,
`caches` (subtracted with `CacheApi`, so a Jint worker never gets it even when its parent has it),
`onlanguagechange`/`onoffline`/`ononline` (the engine has no network-state or language-change notion).

---

## 9. Messaging, ordering, transfer

**Guaranteed** — all of it falls out of existing machinery:

- **Per-direction FIFO** — one queue on the endpoint, drained one message per event-loop job.
- **A message is a task, never a microtask** — every queued reaction runs first, and each message gets its own
  microtask checkpoint (the timers rule, inherited whole).
- **A snapshot at post time** — serialization is synchronous on the sender; `DataCloneError` is raised at the
  `postMessage` call.
- **The start job precedes every message**, and the inner queue is not enabled until the module evaluates, so
  early messages buffer in order rather than being lost. Messages posted before a worker that *fails* to start
  are lost, and a transferred `ArrayBuffer` is lost with them — it was detached on the parent the moment
  `postMessage` returned (spec-consistent).
- **`terminate()` is an immediate parent→worker fence** the instant it returns.

**Explicitly not guaranteed**: no ordering between two workers (the host chooses the interleaving; a host that
pumps A a thousand times before B has starved B and the engine will not notice); **an `error` may arrive before
a message the worker posted first** — HTML puts them on different task sources and orders neither, so this is
spec-shaped rather than a wart; no ordering across port/`BroadcastChannel`/timer; no promise a worker runs at
all (an engine nobody pumps never loads its script); no delivery-latency bound beyond the host's own wait
ceiling (§3); not a memory model (§11).

**Transfer**: `{ transfer: [buffer] }` is a real zero-copy cross-thread move — the sender detaches, and the
receiver's `ArrayBuffer` is built over the very `byte[]` the record carries; sound because a record is consumed
exactly once and a worker pair is a single destination, unlike `BroadcastChannel`, which has to ask for the
copying deserializer.

**`MessagePort` transfer works** (#3197), so `worker.postMessage(msg, [chan.port2])` hands a worker a private
channel — the shape Comlink-style RPC is built from. It needed no new transport: the endpoint machinery already
spanned engines, so what was missing was a serialized form for "a channel side to re-entangle on the far side".
Two consequences for the worker design: the port message queue lives on `MessagePortEndpoint` rather than on
`JsMessagePort` (which is what makes it travel, and is what HTML's transfer steps describe), and
`WebApiEngineState.ResetTransientState` **closes** this engine's ports rather than relying on the generation
fence alone — a transfer in flight to an engine that restores would otherwise leave its peer posting into a
queue nothing can ever drain. Note that this is *not* the "close both ends" rule §10 argues for workers; see
there.

**Transferring a stream works too**, and it needed no new transport either: `ReadableStream`, `WritableStream`
and `TransformStream` are transferable, and the Streams Standard's transfer steps are a `MessagePort` pair plus
a pipe, so they ride the port transfer above — the façades pass the transfer list through verbatim, which is
what lets #3199 (shipped in #3215) work through a worker untouched. For the worker design that means
`worker.postMessage(rs, [rs])` hands a worker a *pipeline* rather than a value, with the same rule as everything
else here: both engines have to be pumped, because a chunk is a task on the receiver. Both sides register in
their engines' port lists, so restore and dispose already close them. The one addition beyond the standard is
that a channel whose far end has been ended **errors the stream** instead of being written into forever, which
is what makes §10's "restore or dispose on either side" also end a pipe that was crossing the pair. That
predicate — `JsMessagePort.IsChannelExhausted` — asks about the queue as well as the far side, which is why
§6.2's drain-then-close state must keep it truthful.

`Error` objects clone as data (the name flattened to the standard names, plus message and stack); functions and
symbols do not.

**`SharedArrayBuffer` is refused** with `DataCloneError` — reframed, because the earlier "two Jint engines are
always two agent clusters" is false as a spec claim (HTML puts a dedicated worker in its creator's cluster,
`canBlock` true) and "it would need a cross-engine waiter list" is false as an engine claim (Jint's waiter lists
are already process-wide, keyed weakly on the backing `byte[]`, and the test262 agent harness already shares one
buffer between engines on real threads). The true statements are three. **A Jint worker pair behaves exactly
like a page that is not cross-origin isolated**, where `postMessage` throws for a SAB too — the serializer's own
comment already says so. workerd — the runtime hosting the most untrusted third-party code in the world —
forbids shared memory outright as a Spectre defence ("multi-threading and shared memory are not permitted in
Workers"), which is the argument Jint's hardening-minded users will find persuasive. And ECMA-262 makes
`Atomics.wait` a `TypeError` on an agent that cannot block, which a host-pumped parent thread is —
`Atomics.waitAsync`, which Jint already runs on the pump, is the sanctioned replacement. What is missing for a
future opt-in is a *policy* (a host declaring two engines one cross-origin-isolated cluster, plus the
`AgentCanSuspend` answer), not architecture: a small, self-contained future decision, deliberately out of scope
(§15).

**Queue bound**: nothing else caps the endpoint queue, and a parent posting to a worker whose host never pumps
grows the parent's live set by one `SerializationRecord` per message, indefinitely, with the worker engine
reachable from it. `Options.WebApi.Workers.MaxQueuedMessages` (default **16384**) bounds each direction of a
worker connection with a `QuotaExceededError` — the family's cap-every-unbounded-script-driven-growth rule. It
is a generous backstop, not flow control: a program that reaches it has a stuck receiver, not a fast sender, and
a host that wants back-pressure builds it out of messages of its own.

---

## 10. Restore and dispose, on either side

| Event | What the connection does |
| --- | --- |
| `RestoreGlobalSnapshot` on the **parent** | end it: close **both** endpoints, cancel the token, `OnWorkerEnded(ParentRestored)` |
| `RestoreGlobalSnapshot` on the **worker** | same, `WorkerRestored` |
| `Engine.Dispose()` on the **parent** | same, `ParentDisposed` |
| `Engine.Dispose()` on the **worker** | closes that engine's ports too — the far side ends as `WorkerDisposed` rather than staying a `Worker` object that looks alive while every `postMessage` pays a full serialization into a queue nothing will ever drain |

Both restore hooks hang off `ResetTransientEvaluationState` via `WebApiEngineState.ResetTransientState`, beside
the existing entries.

**Closing *both* endpoints is a worker-specific rule, argued on its own merits.** An earlier draft claimed it as
the shipped port precedent, and the shipped rule is the **opposite**: `ResetTransientState` closes this engine's
ports and says in as many words that "the peer is deliberately *not* closed: disentangling is one-sided, and a
peer on another engine is in a cycle of its own that this restore has no business ending." The real argument for
a worker is different, and it is about *cost rather than delivery*: a one-sided close stops delivery, but the
surviving side still pays a full structured clone per `postMessage`, still detaches its own transfer-listed
buffers, still throws `DataCloneError` for unserializable values — forever. A worker connection is one object
the engine created spanning two engines, not two independent host peers, so closing both is what stops the
survivor's work. The restore-path hook does only the thread-safe part (endpoints, token, flags);
`OnWorkerEnded` is invoked after `ResetTransientEvaluationState` finishes, so a host exception cannot erupt from
the middle of a half-finished restore.

A parent restore leaves the `Worker` object alive but **inert** — `postMessage` a no-op (post-serialization),
`terminate()` idempotent, no further events — which is not a concession but conformance: the spec's
disentangled-port clause describes exactly this state ("act as if the `Worker` object had no `error` event
handler … but must otherwise act as described above").

Adding a live worker connection to `Engine.Dispose` is required rather than optional: `Dispose` already releases
host abort bridges because a long-lived token would otherwise keep every engine it was handed to reachable, and
a live endpoint holds an entire second engine, which is strictly worse.

---

## 11. Sharing state between parent and worker

**No production JavaScript runtime lets two threads share one JS heap.** ECMA-262 §9.6: an agent's constituents
"belong exclusively to that agent"; V8 admits one thread per isolate and objects from one isolate must not be
used in another; JSC locks the whole `JSVirtualMachine`; GraalJS forbids concurrent `Context` access; QuickJS's
`JSRuntime` carries no lock to make it possible. The one serious attempt — WebKit's 2017
["Concurrent JavaScript: It can work!"](https://webkit.org/blog/7846/concurrent-javascript-it-can-work/) — never
shipped. In Jint the refusal is enforced rather than aspirational: since #3035 a second thread entering an
engine gets `InvalidOperationException`, because the engine's speed *is* its unsynchronized state (property
maps, inline caches, pools), and locking it is the same as deleting it.

What a host has, in order of how much is shared:

1. **Move a buffer** — `postMessage(v, { transfer: [buf] })`, genuinely zero-copy, genuinely one-way.
2. **Move a channel** — `MessagePort` transfer (#3197), the Comlink shape; transferable streams (#3215) follow.
3. **Share a CLR object — the real "context sharing", and it already works.** GraalJS states the norm for
   embeddings: *"Concurrent access to Java objects is allowed: any Java object can be accessed by any Java or
   JavaScript thread, concurrently."* Jint's counterpart: hand the *same* .NET object to both engines with
   `SetValue` — each engine builds its own `ObjectWrapper` (wrapper caches are per engine; the shared
   `TypeResolver` cache holds only engine-independent accessors), so no `JsValue` crosses, while the underlying
   object is genuinely one instance whose mutations both sides see immediately. Strictly more than
   `SharedArrayBuffer` offers (arbitrary types, not bytes), while every JS heap stays single-threaded. Three
   obligations: the object's thread-safety is entirely the host's (the same obligation GraalJS puts on a Java
   object); there is no reactivity, which is what the port in (2) is for; and the shared object must be *data*,
   not a bridge — calling into an engine another thread is inside is the admission exception, so worker→parent
   calls go through the port. Do **not** declare such a type in `ImmutableCrossingTypes` (that promise trades
   mutability away for memoized reads and would serve stale values), and remember the worker does not inherit
   the parent's interop grant: the provider grants CLR access deliberately, per side, and can hand the worker a
   read-only view of what the parent writes.
4. **`SharedArrayBuffer`** — refused today (§9); a policy-shaped future opt-in, not an architectural
   impossibility.

One more shape deserves naming because "context-sharing threads" sometimes means it: several host threads
*taking turns* on one engine — no parallelism, no copying. ClearScript, JSC and GraalJS offer that via internal
locks; Jint deliberately **throws** instead, because a lock turns a design error into a hang. If it were ever
offered it would be a separate opt-in feature, and it is not part of Workers.

---

## 12. Divergence ledger

| # | Divergence | Anchor |
|---|---|---|
| 1 | Jint enables the parent-half queue at construction where HTML enables both only after the initial script; a message the worker posts during its own evaluation may reach the parent sooner than a browser would deliver it. | `workers.html#run-a-worker` (onComplete 11–12) |
| 2 | `type: 'classic'` is the spec's default and Jint refuses it — Jint policy, for Deno's reasons, not a licence the standard grants. | `workers.html#dom-workeroptions-type` |
| 3 | Termination is cooperative: ≤64 statements on the worker's thread, unbounded inside a host CLR call; the spec's abort is immediate. (Matching the spec: `finally` blocks do not run.) | `webappapis.html#killing-scripts` |
| 4 | Jint does not URL-parse the specifier, so the constructor's `SyntaxError` becomes whatever the worker's loader later reports. | `workers.html#dedicated-workers-and-the-worker-interface` (step 4) |
| 5 | No `WorkerGlobalScope`/`DedicatedWorkerGlobalScope` interface objects, so `self instanceof WorkerGlobalScope` answers false — an interface object without the prototype chain would make `instanceof` lie. Ruled together with #3195. | `workers.html#the-workerglobalscope-common-interface` |
| 6 | No `location`; the worker's script name is its `Module.Location`, exposable via `import.meta`. | `workers.html#dom-workerglobalscope-location` |
| 7 | `navigator` exists only when inherited via `WebApiFeatures.Navigator`; `hardwareConcurrency` deliberately absent — the host owns the threads. | `workers.html#the-workernavigator-object` |
| 8 | `SharedArrayBuffer` cannot cross, exactly as in a page that is not cross-origin isolated; a browser's dedicated worker *is* in its creator's agent cluster, so this is Jint's isolation policy, not a fact about agents. | `structured-data.html#structuredserializeinternal` |
| 9 | A worker gets strictly *fewer* capabilities than its creator (network/storage/routing/nesting subtracted); HTML gives it the creator's, floored never raised. Deno's monotonicity rule, applied more strictly. | `workers.html#run-a-worker` |
| 10 | `messageerror` is present on both façades and never fires, for `JsMessagePort`'s documented reason. | `web-messaging.html#message-port-post-message-steps` (7.4) |
| 11 | The worker-count cap is a resource limitation the standard explicitly permits with `QuotaExceededError`. | `webappapis.html#killing-scripts` |
| 12 | A provider refusal is a synchronous `SecurityError` — a precedented shape (Chrome does the same for a disallowed script), not a spec step. | — |

---

## 13. Test matrix

Mechanism in `Jint.Tests/Runtime/WebApi/WorkerTests.cs`; third-party reachability in
`Jint.Tests.PublicInterface/WebApiWorkerTests.cs` (the only project without `InternalsVisibleTo`). Each pin is
chosen so that removing the mechanism it names makes it fail.

**Landed with step 1** — `Jint.Tests`: `TheDefaultOptionsRegisterTheTerminationTokenAsAConstraint` ·
`TheDefaultOptionsReplayTheParentsConstraintFactories` · `TheDefaultOptionsDoNotCopyConstraintInstances` ·
`TheDefaultOptionsCopyTheParentsSecurityPosture` (a theory, one case per copied setting) ·
`TheDefaultOptionsSubtractNetworkStorageRoutingAndWorkers` · `TheDefaultOptionsForceMessagingAndGlobalEvents` ·
`TheDefaultOptionsDoNotCopyTheProvider` · `TheDefaultOptionsInstallTheNullSink` ·
`EveryCallToCreateDefaultOptionsReturnsAFreshInstance` · `UseWorkersSetsFlagAndProviderTogether` ·
`WorkerConnectionEndIsIdempotentUnderConcurrentCallers` ·
`AConnectionThatEndedFaultedCarriesTheCLRErrorAndStaysEnded` · `HostStateIsCarriedAndTheEngineNeverReadsIt`;
plus `Jint.Tests/Runtime/OptionsSecurityPostureTests.cs`, which outlives this feature.
`Jint.Tests.PublicInterface`: `AHostWorkerProviderIsSubclassableOutsideTheAssembly` ·
`UseWorkersIsReachableAndSetsFlagAndProviderTogether` ·
`TheWorkerOptionsGroupIsReachableAndCarriesTheDocumentedDefaults` ·
`TheWorkersFlagIsNotPartOfTheDefaultFeatureSet` · `TypeofWorkerIsUndefinedEvenWithTheFlagAndProvider`.

**Construction & refusals** — `NoProviderMeansNoWorkerGlobal` · `AClassicWorkerRequestIsATypeError` ·
`AProviderRefusalIsASecurityError` · `AProviderExceptionPropagatesUnchanged` ·
`MoreThanMaxWorkersIsAQuotaExceededError` · `AnEngineWithoutTheTerminationTokenIsRefused` ·
`AnUnQuiescentWorkerEngineIsRefused` · **`TheWorkerScriptDoesNotRunOnTheParentsThread`**

**Messaging & ordering** — **`AMessagePostedBeforeTheWorkerEvaluatesIsBuffered`** · `MessagesArriveInPostOrder` ·
`EachMessageGetsItsOwnMicrotaskCheckpoint` · `AMessageSentBeforeTheParentAssignsOnmessageIsNotLost` ·
`AWorkerDeliversToAddEventListenerWithoutStart` · `ATransferredArrayBufferIsMovedNotCopied` ·
`ASharedArrayBufferIsADataCloneError` · **`APortTransferredToAWorkerIsReEntangledOnTheWorkerSide`** ·
`MoreThanMaxQueuedMessagesIsAQuotaExceededError`

> The earlier `AWorkerCannotBeSentAMessagePort` pin is **deleted**: it contradicted #3197 and would have blocked
> #3199.

**Errors** — **`ALoadFailureFiresAPlainEventNotAnErrorEvent`** ·
`ALoadFailureEndsTheConnectionAsStartupFailedWithACLRError` · **`AWorkerErrorReachesTheParentWithNullError`** ·
`AnUnhandledWorkerErrorReachesTheParentsGlobalErrorEvent` ·
**`AWorkerSidePreventDefaultStopsPropagationToTheParent`** ·
`TheWorkerGlobalOnErrorTakesFiveArgumentsAndCancelsByReturningTrue` ·
`TheWorkerObjectOnErrorTakesTheEventAndCancelsByReturningFalse` ·
**`AnUnhandledRejectionInAWorkerDoesNotReachTheParent`** · `AWorkerConstraintFailureEruptsFromTheHostsPump` ·
`AParentRestoreDropsAQueuedErrorEvent`

**terminate / close** — **`APostMessageAfterTerminateIsNeverDelivered`** ·
`APostMessageAfterTerminateStillThrowsDataCloneError` ·
`TerminateStopsARunningWorkerWithinTheAmortizedInterval` · `TerminateDoesNotRunFinallyBlocks` ·
`TerminateDiscardsAMessageTheWorkerAlreadyPosted` · `TerminateIsIdempotent` ·
**`CloseDoesNotAbortTheCurrentlyRunningScript`** · **`AFinalPostMessageBeforeCloseIsDelivered`** ·
`CloseFromTheWorkerEndsTheConnection`

**Threading & lifecycle** — **`EndingTheConnectionFromTheParentWhileTheWorkerPumpsDoesNotThrow`** ·
`AParentRestoreEndsTheConnection` · `AWorkerRestoreEndsTheConnection` · `ParentDisposeEndsEveryConnection` ·
`DisposingTheWorkerEngineEndsTheConnectionAsWorkerDisposed` · `TerminateIsObservableOnTheConnection`

**Scope & constraints** — `TheWorkerGlobalHasNoWorkerGlobalScopeInterfaceObject` ·
**`ImportScriptsThrowsTypeError`** (present and throwing, not absent) · `WorkerOnMessageErrorExists` ·
**`NestedWorkersAreRefusedByDefault`** · **`APumpedWorkersTimeoutIntervalNeverFires`** (the honest pin — it
stops a later change silently unbounding workers a different way) · `AReplayedMemoryLimitBoundsAWorkerJob`
(#3036's job-segment accounting, proven on the worker shape)

**`Jint.Tests.PublicInterface`, later steps** — `AWorkerDoesNotInheritNetworkAccess` ·
`EnablingNestingIsTwoVisibleLines` · `TwoWorkersPumpedFromOneLoopBothMakeProgress` ·
`OnWorkerStartedSeesAPumpableEngine` · `TheProviderCanReachPerRequestStateThroughHostDefined` ·
`WaitForScheduledWorkWakesOnACrossThreadPost`

**WPT**: the `workers/` corpus becomes reachable but is not vendored in the same PR (the change that first runs
a suite is not the change that moved the engine). Expect a large *permanent*-exclusion block rather than a
`NeedsTriage` one — the corpus leans on `location`, classic workers and `SharedWorker`, all declined. When it is
vendored, `WptDivergence.NeedsMessageChannel`'s wording is rewritten: its "Jint has no worker story" is already
stale post-#3197.

**Legs**: solution `dotnet build -c Release` (the net462/netstandard legs catch a leaked net8-only reference),
both test projects on both TFMs, and the `JINT_HOST_CONTRACT_VERIFICATION=1` leg. Everything under
`Jint/WebApi/Workers/` and every test file touching it is wrapped end to end in `#if NET8_0_OR_GREATER`, BCL
only.

---

## 14. Implementation order

0. **`Engine.Advanced.WaitForScheduledWork` + async variant** — its own PR, before Workers, ungated, all-TFM
   (§3). Serves existing hosts on its own.
1. ✅ Types + options + `WebApiFeatures.Workers = 1 << 24` + `UseWorkers` + `Options.CopySecurityPosture` with
   its reflective pin. No script surface; this document synced in the same PR.
2. The `Worker` interface object and constructor, entanglement under `EnterHostCall`, the retarget hook, the
   deferred inner-queue enable, the start job, messages both ways, the ordering pins.
3. The worker global scope, `close()` (drain-then-close), `terminate()`, the termination token,
   `MaxQueuedMessages`.
4. The error channels: plain-`Event` load failure + `StartupFailed`, the *notHandled*-gated relay,
   `error: null`, the global `onerror` family.
5. Restore/dispose on both sides, including `Engine.Dispose` closing this engine's ports.
6. README (beside "Channel messaging can span two engines"), then the WPT `workers/` corpus as its own PR.

## 15. Settled and open

**Settled**: `MessagePort` transfer (shipped, #3197); the `WorkerConnection` failure surface (**yes** —
`IsFaulted`/`Error`); nesting default (**off**); `close()` vs `terminate()` (spec-split); load-failure event
shape (plain `Event`); `importScripts` (present-and-throwing); security-posture inheritance (restrictions
travel, grants do not); `MaxQueuedMessages` default (16384).

**Still open, deliberately**:

- `SharedWorker` — needs a cross-engine name registry (`BroadcastChannelBroker`'s shape); plausible later.
- Cross-engine `SharedArrayBuffer` as a host-declared opt-in (one cluster plus the `AgentCanSuspend` answer) — a
  policy decision for its own issue, not architecture.
- `location` as a read-only `WorkerLocation` over the resolved module location — declined for v1, cheap to add
  if porting pressure shows up.
- `Engine.Advanced.SetWorkerProvider` (a per-engine live door for pooled hosts that want per-tenant
  `typeof Worker` to differ) and `Engine.Advanced.InstallWorkerGlobalScope(port, name)` (the low-level door that
  would let a host build a `SharedWorker`, classic or host-evaluated worker without the constructor) — both
  considered, neither ships blind; the `HostDefined` pattern and `CreateMessagePortPair` cover the known cases.
