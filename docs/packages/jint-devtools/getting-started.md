# Getting started

## Requirements

- .NET 8 or .NET 10
- A Jint engine created with `UseDevTools()`
- A host loop that keeps servicing the engine, or a `LibraryOwned` target

Install the package:

```bash
dotnet add package Jint.DevTools
```

## Create and publish a target

```csharp
using Jint;
using Jint.DevTools;

var engine = new Engine(options => options.UseDevTools());

await using var target = new EngineTarget(engine, new EngineTargetOptions
{
    Title = "Rules engine",
    Url = "rules.js"
});

await using var server = new DevToolsServer(); // loopback, ephemeral port
server.AddTarget(target);
server.Start();

Console.WriteLine($"DevTools: {server.BrowserHttpUrl}");
```

With the default `Port = 0`, the operating system chooses a free port. After `Start()`, use `BoundPort`,
`BrowserHttpUrl`, or `BrowserWebSocketUrl` to discover it.

## Pump the engine

`EngineTarget` defaults to `ThreadMode.HostOwned`. Both script and DevTools commands must run on the engine's
owning thread:

```csharp
while (!stopping)
{
    target.Pump();
    engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50));
}
```

`engine.Tasks.ProcessTasks()` may be used instead of `target.Pump()`. If the host does not pump, clients receive
`Engine is not being pumped` after `CommandTimeout`.

## Run a named script

Source names make scripts easier to find and breakpoint in a client:

```csharp
engine.Execute(
    """
    function total(a, b) {
        return a + b;
    }
    globalThis.result = total(20, 22);
    """,
    "rules.js");
```

Next, [connect a client](connecting.md). For applications without an existing engine loop, see
[LibraryOwned hosting](hosting.md#libraryowned).
