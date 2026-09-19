# Enabling Web APIs

Web APIs require .NET 8 or later and are absent from a default engine.

Enable the standard non-network, non-persistent set:

```csharp
var engine = new Engine(options => options.UseWebApis());
```

Or select only the required features:

```csharp
var engine = new Engine(options => options.UseWebApis(
    WebApiFeatures.Console |
    WebApiFeatures.Timers |
    WebApiFeatures.Encoding));
```

`WebApiFeatures.Default` includes console, timers, encoding, base64, structured clone, crypto, performance,
events, URLs, files, navigator, streams, scheduling, messaging, reporting, compression, idle callbacks,
global error events, and `WebLocks`. It deliberately excludes:

- `Fetch`, `EventSource`, `WebSocket`, and `XmlHttpRequest`
- `Storage` and `CacheApi`
- `FetchEvents`, which lets script claim inbound requests
- `Workers`, which asks the host to create engines and execution resources

Dedicated helpers such as `UseFetch`, `UseStorage`, `UseCacheApi`, `UseWorkers`, and `UseWebLocks` enable their
flags and configure the associated options. Feature dependencies are expanded automatically; for example, fetch
brings the events, URL, files, and streams surfaces it uses, and `WebLocks` brings navigator and events.

Globals are installed lazily and do not replace an own global already registered by the host. Only the principal
realm is changed; a `ShadowRealm` receives none of these globals unless the host installs them.

## Enabling an existing engine

For a pooled engine whose needs are known per request:

```csharp
WebApiFeatures added = engine.WebApi.Enable(
    WebApiFeatures.Timers | WebApiFeatures.Events);
```

`Enable` is additive. Features cannot be removed, and requesting an installed feature is a no-op. The optional
configuration callback receives a private copy of that engine's web options.

Enable features before capturing a reusable global snapshot. Restoring an older snapshot removes globals
installed later but does not reset the feature record, so another `Enable` call cannot reinstall them.

Continue with [Console and timers](./console-and-timers.md) or review
[Fetch and networking](./fetch-and-networking.md) before granting network access. `WebLocks` is in the default
set and grants nothing on its own; [Web Locks](./locks.md) covers the one decision it does ask a host to make,
which is whether several engines share a lock space.
