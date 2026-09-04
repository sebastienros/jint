# Jint.DevTools

`Jint.DevTools` exposes a Jint engine through the
[Chrome DevTools Protocol (CDP)](https://chromedevtools.github.io/devtools-protocol/).
Chrome DevTools, Puppeteer, PuppeteerSharp, and other CDP clients can inspect, evaluate, debug, and profile
scripts in an application that embeds Jint. No browser is required.

The package targets .NET 8 and .NET 10 and supports Native AOT.

::: warning Unauthenticated endpoint
The endpoint has no authentication. Any client that can reach it can evaluate JavaScript in your process.
Keep the default loopback host (`127.0.0.1`) and do not expose the port to an untrusted network.
:::

## Install

```bash
dotnet add package Jint.DevTools
```

[View Jint.DevTools on NuGet.org](https://www.nuget.org/packages/Jint.DevTools)

## Minimal host

```csharp
using Jint;
using Jint.DevTools;

var engine = new Engine(options => options.UseDevTools());
await using var target = new EngineTarget(engine);
await using var server = new DevToolsServer();

server.AddTarget(target);
server.Start();

Console.WriteLine(server.BrowserHttpUrl);

while (!stopping)
{
    target.Pump();
    engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50));
}
```

The loop is required: the default `HostOwned` target answers protocol commands only when the host pumps the
engine, on the engine's owning thread.

## Guide

- [Getting started](getting-started.md)
- [Hosting and thread modes](hosting.md)
- [Connecting clients](connecting.md)
- [Debugging scripts](debugging.md)
- [Profiling and coverage](profiling.md)
- [Supported domains](domains.md)
- [Native AOT](native-aot.md)
- [Security](security.md)
