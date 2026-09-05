# Hosting

An `Engine` and its `JsValue` instances are thread-affine. A transport thread carries protocol text, while
commands that inspect or execute JavaScript run on the engine thread. Choose that thread with
`EngineTargetOptions.ThreadMode`.

## HostOwned

`HostOwned` is the default. Use it when your application already owns the engine loop:

```csharp
var engine = new Engine(options => options.UseDevTools());
await using var target = new EngineTarget(engine);

while (!stopping)
{
    target.Pump(); // must run on the engine's owning thread
    engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50));
}
```

Keep pumping after the initial script finishes. Otherwise inspection, timers, promise reactions, and debugger
commands cannot progress.

If a `JavaScriptException` escapes host-owned scheduled work, call
`target.ReportUncaughtException(exception)` on the engine thread if attached clients should receive it.
Reporting does not handle or suppress the exception.

## LibraryOwned

`LibraryOwned` creates one background thread for the target. Submit all host work through the target; do not
access or pump the engine from another thread.

```csharp
var engine = new Engine(options => options.UseDevTools());
await using var target = new EngineTarget(engine, new EngineTargetOptions
{
    ThreadMode = ThreadMode.LibraryOwned
});

await target.PostAsync(engine =>
{
    engine.Execute("globalThis.answer = 6 * 7", "startup.js");
});
```

Convert results to CLR values inside a `PostAsync<T>` callback. Never return a `JsValue` to the awaiting thread.

## Wait for a debugger

`WaitForDebuggerOnStart` holds host work until a client sends `Runtime.runIfWaitingForDebugger`:

```csharp
await using var target = new EngineTarget(engine, new EngineTargetOptions
{
    WaitForDebuggerOnStart = true
});

server.AddTarget(target);
server.Start();

if (!target.WaitForDebugger(TimeSpan.FromMinutes(2)))
{
    throw new TimeoutException("No debugger attached.");
}
```

A host-owned target must call `WaitForDebugger`; it pumps while waiting so the release command can run.
A library-owned target pumps itself.

## Lifetimes and timeouts

The host owns the `Engine` lifetime. Disposing an `EngineTarget` stops its library-owned thread and protocol
access but does not dispose its engine.

- `CommandTimeout` bounds the client's wait, not queued execution. A timed-out command can still run later.
- `PauseTimeout` resumes a debugger pause whose client stopped responding.
- `MaxMessageBytes` limits accepted WebSocket message size.
