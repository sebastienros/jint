# Network and storage

Each `BrowserContext` is a network and storage partition. Configure it when creating the context:

```csharp
var context = await browser.NewContextAsync(new BrowserContextOptions
{
    HttpClient = client,
    UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.Ordinal),
    BlockPrivateNetwork = true,
});
```

If you supply an `HttpClientHandler`, disable automatic redirects. Jint follows redirects itself so the URL filter and security rules are checked on every hop.

The context's cookie jar is shared by requests and `document.cookie`. Its storage partition keeps one `localStorage` store per origin. A page owns its own `sessionStorage`.

```csharp
var jar = context.CookieJar;
var storage = context.StoragePartition;
```

## HTTP caching

HTTP response caching is disabled by default. Enable a bounded private cache with
`options.HttpCache.Storage = BrowserHttpCacheStorage.Memory` before creating the context. Disk mode
also requires an explicit directory and visitor partition identity, or explicitly requested temporary
storage. Pages and workers in that context share the cache. `context.ClearHttpCache()` clears it.

See [HTTP caching](../../guide/browser-http-cache) for freshness, validation, request cache modes,
credential partitioning, disk ownership and DevTools controls. This cache is separate from script-visible
`CacheStorage` and its quotas.

## Request log

`Page.Requests` is a bounded, page-lifetime summary of document, script, subresource, `fetch`, and `XMLHttpRequest` activity:

```csharp
foreach (var request in page.Requests)
{
    Console.WriteLine($"{request.Method} {request.Url} {request.Status}");
}
```

Entries include initiator, final-hop URL and method, status, response headers, redirect count, body length, and failure details. Bodies and request headers are not retained. A request in flight has status `0`.

`FromCache`, `Revalidated` and `TransferredBodyLength` distinguish locally reused bodies from network
traffic. A validated response delivers the retained body while transferring zero body bytes.

References intentionally not fetched, such as a media element or a non-stylesheet `<link>`, still appear with `NotFetchedReason`. The log spans navigations and is bounded by `BrowserOptions.MaxRecordedEvents`.

Use `UrlFilter` for an application allow-list. `BlockPrivateNetwork` is a coarse additional defense; see [Untrusted content](./untrusted-content).

## Routing requests

`Page.WaitForResponseAsync(predicate, timeout)` waits for the first request sent after the call whose response
the predicate accepts, and returns its `PageRequest`, or `null` at the timeout. Start it before the action that
sends the request:

```csharp
var saved = page.WaitForResponseAsync(r => r.Url.EndsWith("/api/save", StringComparison.Ordinal), TimeSpan.FromSeconds(5));
await page.ClickAsync("#save");
PageRequest? response = await saved;
```

A request counts once its headers are in, or once it failed. It reads the same log as `Page.Requests`, so it has
no body and no request headers: a route that records them and continues answers those questions.

`Page.RouteAsync` and `BrowserContext.RouteAsync` answer a request in process — with a response of your own,
a network failure, or the request sent on, optionally rewritten:

```csharp
await context.RouteAsync(
    uri => uri.AbsolutePath.EndsWith("/track.js", StringComparison.Ordinal),
    request => request.FulfillAsync(contentType: "application/javascript", body: "window.__stubbed = true"));

await page.RouteAsync(uri => uri.Host == "analytics.example.org", request => request.AbortAsync("blockedbyclient"));

await page.RouteAsync(_ => true, async request =>
{
    Console.WriteLine($"{request.ResourceType} {request.Method} {request.Url}");
    await request.ContinueAsync();
});
```

Documents, child frames, scripts, style sheets, images, fonts, `fetch`, `XMLHttpRequest` and workers' loads
are routed, every redirect hop afresh. A page's routes are asked before its context's, newest first; a
handler that returns without deciding falls back to the next matching route, and a request no route decides
is sent unchanged. `UnrouteAsync` removes routes by the predicate they were registered with.

`UrlFilter` still runs first, so it stays the allow-list: a refused URL never reaches a route, and a
`ContinueAsync` URL rewrite is checked against it the way a redirect is. Matchers and handlers run on a
thread-pool thread, never on the page's loop, so a handler may block; one that waits on its own page can
still deadlock a fetch the page is itself blocked on. Decide before the handler's task completes — a later
decision throws — and note that a handler that throws fails the request.
