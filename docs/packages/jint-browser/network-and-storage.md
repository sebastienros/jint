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

## Request log

`Page.Requests` is a bounded, page-lifetime summary of document, script, subresource, `fetch`, and `XMLHttpRequest` activity:

```csharp
foreach (var request in page.Requests)
{
    Console.WriteLine($"{request.Method} {request.Url} {request.Status}");
}
```

Entries include initiator, final-hop URL and method, status, response headers, redirect count, body length, and failure details. Bodies and request headers are not retained. A request in flight has status `0`.

References intentionally not fetched, such as images, still appear with `NotFetchedReason`. The log spans navigations and is bounded by `BrowserOptions.MaxRecordedEvents`.

Use `UrlFilter` for an application allow-list. `BlockPrivateNetwork` is a coarse additional defense; see [Untrusted content](./untrusted-content).
