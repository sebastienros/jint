# Connecting

Start the server before reading its addresses:

```csharp
await using var server = new DevToolsServer(new DevToolsServerOptions
{
    Port = 0
});

server.AddTarget(target);
server.Start();

Console.WriteLine(server.BoundPort);
Console.WriteLine(server.BrowserHttpUrl);
Console.WriteLine(server.BrowserWebSocketUrl);
```

The HTTP discovery endpoint exposes `/json/version`, `/json/list`, and `/json/protocol`. Clients normally use
`BrowserHttpUrl` or `BrowserWebSocketUrl` rather than constructing target URLs themselves.

## Chrome DevTools

1. Open `chrome://inspect`.
2. Select **Configure…**.
3. Add `127.0.0.1:<BoundPort>`.
4. Select **inspect** for the Jint target.

The target is reported as a Node-style JavaScript target, so DevTools opens its JavaScript-only interface.

## PuppeteerSharp

```csharp
using PuppeteerSharp;

await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
{
    BrowserURL = server.BrowserHttpUrl
});
```

Clients that accept a socket directly can use `server.BrowserWebSocketUrl`.

## Fixed and ephemeral ports

The default port is `0`, which avoids collisions and chooses an ephemeral port. Use a fixed loopback port only
when external configuration requires one:

```csharp
var options = new DevToolsServerOptions
{
    Host = "127.0.0.1",
    Port = 9222
};
```

An unpredictable or ephemeral port is not authentication. Apply the guidance in [Security](security.md)
regardless of the chosen port.

## Direct target sockets

`/json/list` includes a WebSocket URL for each engine target. A direct target connection carries `Runtime`,
`Debugger`, `Profiler`, `Console`, and `Log`. The browser connection carries target discovery and attachment
through `Target`, `Browser`, and `Schema`.

Only flattened target sessions are supported. Established client libraries request this automatically.
