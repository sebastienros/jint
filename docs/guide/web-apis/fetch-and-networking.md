# Fetch and networking

Outbound networking is never part of `UseWebApis()`. Grant it explicitly and define a policy:

```csharp
var engine = new Engine(options => options.UseFetch(fetch =>
{
    fetch.AllowedSchemes.Remove("http");
    fetch.UrlFilter = uri =>
        uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    fetch.MaxResponseBytes = 1_048_576;
    fetch.MaxConcurrentRequests = 4;
    fetch.Timeout = TimeSpan.FromSeconds(5);
    fetch.HttpClient = httpClient;
}));
```

`fetch` exposes the host process's network position. Restrict destinations, response size, concurrency, and
time. The defaults allow HTTP and HTTPS, cap a decompressed response at 32 MiB, allow 10 concurrent requests,
and use a 30-second timeout; only the host can provide a meaningful destination allowlist.

Jint handles redirects so `AllowedSchemes` and `UrlFilter` are checked on every hop. If you provide an
`HttpClient`, disable its automatic redirects or they can bypass those checks. Filters, cookie jars, and
observers may run on transport threads: they must be thread-safe, non-blocking, and must never touch the engine
or a `JsValue`.

Set `BaseUrl` for relative URLs. Cookies are disabled unless a `CookieJar` is supplied; partition every jar by
tenant or browsing session. Fetch bodies are streams, and network progress reaches script only while the engine
is pumped. `MaxResponseBytes` applies after decompression and may error the body stream after headers have
already resolved the fetch promise.

## Other transports

Each transport is a separate grant, though all use the shared fetch policy:

```csharp
var engine = new Engine(options => options
    .UseXmlHttpRequest(network => network.UrlFilter = IsAllowed)
    .UseEventSource(network => network.UrlFilter = IsAllowed)
    .UseWebSocket(network => network.UrlFilter = IsAllowed));
```

Enable only what the script needs. Event streams reconnect through scheduled work; XHR progress and WebSocket
events also require pumping. Apply the same destination and tenant isolation rules to every transport.

## Hosting an inbound fetch handler

An engine can turn an `HttpRequestMessage` into an `HttpResponseMessage` without granting outbound networking:

```csharp
var engine = new Engine(options => options.UseWebApis(
    WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files));

engine.Execute("globalThis.handle = request => new Response('ok')");
engine.WebApi.SetFetchHandler(engine.GetValue("handle"));

using var response = await engine.WebApi.InvokeFetchHandlerAsync(
    new HttpRequestMessage(HttpMethod.Get, "https://example.org/"));
```

`SetFetchHandler` also accepts an object with a callable `fetch` member or a module namespace with a matching
default export. `WebApiFeatures.FetchEvents` is a distinct grant that lets script register an
`addEventListener("fetch", ...)` route; it does not enable outbound `fetch`.

Handler failures remain exceptions—the host decides how they map to HTTP. Apply
[constraints](../constraints.md), do not share an engine between concurrent requests, and restore a pooled
engine's snapshot between tenants. Ordinary constraints bound the initial engine entry, while later pump turns
are new entries; use `OperationDeadlineConstraint` to bound the whole request. For a host-owned thread, use
`InvokeFetchHandler` and pump its operation instead of the async convenience method.
