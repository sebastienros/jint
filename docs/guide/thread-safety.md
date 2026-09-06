# Thread safety

An `Engine` serves one operation at a time and is not thread-safe. Concurrent public entries fail fast with
`InvalidOperationException`; Jint does not silently serialize them.

```csharp
var value = await engine.EvaluateAsync(script);
pool.Return(engine); // only after the operation has completed
```

Keep one engine exclusively assigned to one request or operation. Await `EvaluateAsync`, `ExecuteAsync`,
`InvokeAsync`, `Modules.ImportAsync`, and `UnwrapIfPromiseAsync` before returning the engine to a pool or
disposing it. `Dispose` also fails while an operation owns the engine.

Same-thread synchronous re-entry from a host callback is supported. Starting an async engine API from inside an
active engine call is not; the callback must return before ownership can transfer.

A JavaScript callback converted to a CLR delegate may be invoked on another host thread while the operation
that exposed it is still open, while an async engine operation or blocking event-loop drain is outstanding, or
while the host is waiting for scheduled work. Outside those admission windows, the call fails rather than
silently racing or deadlocking the engine.

## Queuing from another thread

`Tasks.Post` is the deliberate cross-thread exception. It only enqueues and never runs script on the caller:

```csharp
engine.Tasks.Post(() => engine.Invoke("onMessage", payload));
```

The action runs on whichever thread next calls `engine.Tasks.ProcessTasks()`. Background task and module
completions may likewise enqueue work, but they do not grant permission for unrelated host calls while an async
operation owns the engine.

## Values and construction

JavaScript objects are engine-affine. Build `JsObject`, `JsArray`, and other engine-owned values on the thread
that owns the engine, or while it is idle. Passing an object-valued `JsValue` to another engine is unsupported.

`Prepared<Script>` and `Prepared<Module>` are different: they are reusable, thread-safe, and may execute
concurrently on separate engines. A configured `Options` may also be shared by engines being constructed
concurrently, but finish configuring it before construction begins.

For a dedicated host loop, use `TimeUntilNextScheduledWork`, `WaitForScheduledWork`, and `ProcessTasks` as
described in [Web APIs](./web-apis.md). There must still be only one drainer per engine.
