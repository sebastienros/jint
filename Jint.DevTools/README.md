# Jint.DevTools

A [Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/) server for
[Jint](https://github.com/sebastienros/jint), so a debugging client can attach to an engine your host is
already running. It speaks CDP over a WebSocket, and a Jint engine appears to a client the way a Node process
does — a target it can list, attach to and evaluate in — with no browser anywhere in the picture. `Runtime`,
`Debugger`, `Profiler`, `Console`, `Log`, `Target`, `Browser` and `Schema` answer; everything else is
honestly `-32601`, which is what a client feature-detects on.

```c#
using Jint.DevTools;

var engine = new Engine(options => options.UseDevTools());

await using var server = new DevToolsServer(new DevToolsServerOptions { Port = 9222 });
server.AddTarget(new EngineTarget(engine));
server.Start();

// The host's own loop is what runs the engine AND answers the protocol.
while (running)
{
    engine.Tasks.ProcessTasks();
    engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50));
}
```

Point Chrome's `chrome://inspect` at `127.0.0.1:9222`, or connect PuppeteerSharp with
`new ConnectOptions { BrowserURL = "http://127.0.0.1:9222" }`. **The endpoint is unauthenticated**, exactly as
it is in Chrome: anything that can reach it can evaluate arbitrary script in your process, so `Host` defaults
to `127.0.0.1` and should stay there.

Requires .NET 8 or later, and is Native AOT compatible — everything the protocol serializes goes through a
source-generated `System.Text.Json` context, and a published native binary is driven over a real socket in
CI. [`Jint.Browser`](https://www.nuget.org/packages/Jint.Browser) adds the page domains (`Page`, `DOM`,
`Network`, `Fetch`, `Input`, `Emulation`, `Storage`, `Accessibility`) on the same session core.

Which thread answers a command, what each domain answers, and how to try it from the REPL are in
[Jint's README](https://github.com/sebastienros/jint#chrome-devtools-protocol-opt-in-package).

Licensed under BSD-2-Clause, like the rest of Jint.
