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
