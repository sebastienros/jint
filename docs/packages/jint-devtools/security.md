# Security

::: danger Remote code execution endpoint
Jint.DevTools has no authentication. Anyone who can connect can use `Runtime.evaluate` to run JavaScript in
your process with the capabilities granted to that engine.
:::

`DevToolsServerOptions.Host` defaults to `127.0.0.1` for this reason. Do not bind the server to `0.0.0.0`, a
LAN address, a public interface, or another routable address.

```csharp
var options = new DevToolsServerOptions
{
    Host = "127.0.0.1",
    Port = 0,
    MaxMessageBytes = 1024 * 1024,
    PauseTimeout = TimeSpan.FromSeconds(15),
    CommandTimeout = TimeSpan.FromSeconds(15)
};

await using var server = new DevToolsServer(options);
```

## Deployment guidance

- Start the server only when debugging is intentionally enabled.
- Keep it on loopback and rely on operating-system access controls around the process.
- Do not treat an ephemeral port as authentication.
- Do not log or publish the WebSocket URL to untrusted users.
- Dispose the server and targets when the debugging session ends.
- Leave `EngineFactory` unset unless clients must be allowed to create additional engines.
- Set Jint constraints and host capabilities according to the scripts being evaluated.

`MaxMessageBytes` limits one inbound WebSocket message. `CommandTimeout` bounds how long the client waits, but
a timed-out queued command can still execute when a host-owned engine is pumped later. `PauseTimeout` prevents
an abandoned debugging client from holding the engine thread indefinitely. None of these settings authenticate
or authorize a client.

## Engine authority

The protocol does not add CLR access by itself, but evaluation receives whatever authority the configured Jint
engine already has: host objects, CLR access, module loaders, network APIs, storage, and other extensions.
Treat connection access as equivalent to permission to use all of those capabilities.

For production systems, prefer disabling the endpoint entirely. If remote debugging is unavoidable, provide
network isolation and an authenticated transport boundary outside the process; never expose the package's raw
listener directly.
