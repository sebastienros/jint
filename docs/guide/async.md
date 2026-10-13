# Asynchronous execution

Use the async entry points when the evaluated JavaScript may return a promise:

```csharp
var engine = new Engine();

var value = await engine.EvaluateAsync("""
    (async () => {
        await Promise.resolve();
        return 42;
    })()
    """);

Console.WriteLine(value.AsNumber());
```

`ExecuteAsync` and `InvokeAsync` follow the same pattern. They await promise settlement without holding a
thread and accept cancellation tokens:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var value = await engine.EvaluateAsync(
    "(async () => compute())()",
    cancellationToken: cts.Token);
```

The token covers the whole call, not only the wait. One already cancelled cancels the task before any script
runs. One cancelled later fails the call with an `OperationCanceledException` carrying that token, and reaches:

- the interpreter, on the same amortized cadence as `ObserveCancellation`: the synchronous run, every
  continuation, a synchronous re-entry from a host callback, and an authorized host callback still running
  under the call;
- a blocking wait inside the call — `UnwrapIfPromise`, a blocking `Modules.Import`, `Tasks.WaitForScheduledWork`
  reached from host code — which wakes instead of waiting out its own bound;
- a `fetch`, `XMLHttpRequest`, `WebSocket` or `EventSource` the call starts, for as long as it runs, including
  after the call has returned: the request is abandoned and settles nothing;
- a module load the call starts: `AsyncModuleLoader` hands `LoadModuleContentsAsync` a token the call's token
  cancels, and a loader that fails with `OperationCanceledException` once it is cancelled fails the import with
  that exception rather than with a rejection script could catch.

`Modules.ImportAsync`, `JsValue.UnwrapIfPromiseAsync` on a promise, and `Engine.WebApi.InvokeFetchHandlerAsync`
behave the same.

The token does not reach a host callback that never returns, a host `Task` the script awaits — cancel that one
yourself — or script the engine runs outside the call, such as a timer callback run by a later
`Tasks.ProcessTasks()`. A token passed to every `*Async` call is therefore enough for those calls; register
`ObserveCancellation` as well when the engine also runs script outside them.

A cancelled call stops where it is: its queued jobs, timers and pending loads stay on the engine, and its next
pump — the next `Evaluate`, `*Async` call or `Tasks.ProcessTasks()` — runs them. Before reusing a pooled engine
after a cancelled call, call `engine.Advanced.RestoreGlobalSnapshot(snapshot)`, which discards them and fences
the completions still on their way, or discard the engine.

The engine's `Options.Constraints.PromiseTimeout` also bounds waits.

## Existing promise values

When a synchronous call returns a `JsValue` that may be a promise, unwrap it asynchronously:

```csharp
var pending = engine.Invoke("computeAsync");
var result = await pending.UnwrapIfPromiseAsync(cts.Token);
```

A non-promise is returned immediately. A rejected promise produces `PromiseRejectedException`.

## Failure channel

Parsing errors, script errors, rejected promises, and execution-limit failures arrive through the returned
`Task`; put the `try`/`catch` around `await`. Only usage errors throw before a task represents the operation:
null or invalid prepared arguments, or an `InvalidOperationException` because the engine is already in use.

Always await an async entry before returning an engine to a pool or disposing it.

## Host-created asynchronous work

`engine.Tasks.RegisterPromise()` returns a promise plus resolver and rejecter functions. They accept CLR values
and may be called from any thread; settlement is queued and conversion occurs on the engine's thread. Bound a
promise that might never settle with a cancellation token, promise timeout, or operation deadline.

Automatic conversion of CLR `Task` and `ValueTask` return values into JavaScript promises is experimental:

```csharp
var engine = new Engine(options =>
    options.ExperimentalFeatures = ExperimentalFeature.TaskInterop);
```

For timers, network operations, and host-controlled pumping, see [Web APIs](./web-apis.md).

## Carry host state across await

A host that runs several asynchronous flows in one engine often needs to know which flow a host call belongs
to. Host state set around a call is gone once the script reaches its first `await`, and several
continuations can resume in the same microtask drain, so restoring it when a host `Task` completes cannot work
either.

ECMAScript defines the seam for this: `HostMakeJobCallback` runs when script registers a callback and
`HostCallJobCallback` runs around it later. Implement `JobCallbackHooks` and install it with
`Options.Host.JobCallbacks`:

<!-- snippet: guide-job-callback-hooks -->
```csharp
public sealed class FlowHooks : JobCallbackHooks
{
    public static readonly AsyncLocal<string?> Current = new();

    // HostMakeJobCallback: runs when script registers a callback (then, await, ...).
    protected override object? Capture(Engine engine) => Current.Value;

    // HostCallJobCallback: runs around the callback, restoring even if it throws.
    protected override object? Enter(Engine engine, object hostDefined)
    {
        var previous = Current.Value;
        Current.Value = (string) hostDefined;
        return previous;
    }

    protected override void Exit(Engine engine, object? token) => Current.Value = (string?) token;
}
```
<!-- endSnippet -->

`Capture` runs once per `then`, `await`, thenable adoption or `FinalizationRegistry` construction, and the
engine runs that callback between `Enter` and `Exit`. With a host function that names a flow, every branch of
a concurrent fan-out keeps its own flow across each `await`:

<!-- snippet: guide-job-callback-usage -->
```csharp
var engine = new Engine(options => options.Host.JobCallbacks = new FlowHooks());

// Runs body inside a named flow; an async body returns at its first await.
engine.SetValue("scope", new Func<string, JsValue, JsValue>((name, body) =>
{
    var previous = FlowHooks.Current.Value;
    FlowHooks.Current.Value = name;
    try
    {
        return body.Call();
    }
    finally
    {
        FlowHooks.Current.Value = previous;
    }
}));

// Any host function can now ask which flow it was called from.
engine.SetValue("hostOperation", new Action<int>(item =>
    log.Add($"{item} belongs to {FlowHooks.Current.Value}")));

await engine.EvaluateAsync("""
    (async () => {
        await Promise.all([1, 2, 3].map(async (item) => {
            await scope(`Item ${item}`, async () => {
                await null;
                await hostOperation(item); // "2 belongs to Item 2", ...
            });
        }));
    })()
    """);
```
<!-- endSnippet -->

- Return `null` from `Capture` when there is nothing to carry. That callback then runs under whatever state is
  current when its job runs, without `Enter` or `Exit`. Return a sentinel instead to run it under an explicitly
  empty state.
- All three members run on the engine's thread and may call back into the engine. `Exit` runs even when the
  callback throws; none of the three should throw themselves.
- The captured object lives as long as the reaction holding it, so a promise that never settles keeps it alive.
- One instance may serve every engine built from the same `Options`; each member receives its `Engine`.
- With no hooks installed, promise reactions cost nothing extra.

Only callbacks the ECMAScript specification schedules are covered: promise reactions (including `await`,
`for await`, async generators and the `Promise` combinators), thenable adoption and `FinalizationRegistry`
cleanup. Web API callbacks such as `setTimeout` and `queueMicrotask` run under whatever state is current when
they fire.

These hooks are also where a future implementation of the TC39
[AsyncContext proposal](https://tc39.es/proposal-async-context/) would capture its mapping, as the
[design note](../design/async-context.md) describes. Jint does not expose `AsyncContext` to script; this is the
host-side seam only.
