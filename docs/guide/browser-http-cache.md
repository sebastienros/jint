# Browser HTTP caching

`BrowserContextOptions.HttpCache` enables a private HTTP response cache shared by all pages and workers
in that context. The default is `BrowserHttpCacheStorage.Disabled`, preserving host-visible request
behavior. This cache is separate from JavaScript's `CacheStorage` and from its storage quotas.

```csharp
var options = new BrowserContextOptions();
options.HttpCache.Storage = BrowserHttpCacheStorage.Memory;
options.HttpCache.MaxBytes = 64 * 1024 * 1024;
options.HttpCache.MaxEntries = 1024;
options.HttpCache.MaxEntryBytes = 4 * 1024 * 1024;
await using var context = await browser.NewContextAsync(options);
```

All limits must be finite and positive. They are copied when the context is constructed; later changes
to the options do not change the context. The byte budget includes retained bodies, an estimate of
metadata and reservations for in-flight capture buffers. LRU eviction bounds the stored representations;
a response that exceeds the entry limit still streams to its consumer without being cached. Pending
captures can evict older entries or decline admission when the budget is full. `TimeProvider` supplies
HTTP freshness time and can be replaced with a thread-safe test clock.

`context.ClearHttpCache()` drops stored responses and retires pending captures. Closing the context
closes its pages and retires the cache; an abandoned response, cancelled read, incomplete transfer or
capture from before a clear never becomes a cache entry. Readers already holding a cached body can
finish independently. The cache adds no background tasks and does not coalesce concurrent misses: every
request still passes through interception and has its own cancellation and body lifetime.

## HTTP policy

The policy follows [RFC 9111](https://www.rfc-editor.org/rfc/rfc9111.html) and the Fetch Standard's
[HTTP-network-or-cache fetch](https://fetch.spec.whatwg.org/#concept-http-network-or-cache-fetch).
It stores only complete `200` responses to bodiless `GET` requests. `HEAD`, other status codes,
redirect responses, ranges, caller-supplied conditional requests and `text/event-stream` pass through.
A followed redirect's final `200` can be cached under its final URL; the redirect itself still contacts
the server. Unsafe successful requests invalidate all variants at their effective URL and same-origin
`Location` and `Content-Location` targets. Invalidation also retires older in-flight captures.

Freshness uses `max-age`, or `Expires` relative to `Date`, and accounts for apparent age, `Age`, response
delay and time in the cache. There is no heuristic freshness. `no-store` prevents storage; `no-cache`
requires validation; stale `must-revalidate` responses cannot be reused without validation. This is a
private cache, so `private` responses may be stored and `s-maxage` and `proxy-revalidate` do not impose
shared-cache rules. Stale entries use `ETag` and `Last-Modified`; without a validator they require a full
request. A `304` refreshes end-to-end metadata and returns the retained body with its original status.
Caller conditions are preserved and their responses are never combined with an unrelated cached body.
There is no stale-while-revalidate or stale-if-error support.

Matching uses the normalized URL without its fragment, the context, the requesting origin and credentials
mode, `Vary` request-header values, and cookie, authorization and origin headers even without `Vary`.
Navigations use the destination origin as their origin partition. Requests from opaque or missing
origins and URLs containing embedded credentials bypass caching. Header comparison is conservative:
values must match exactly, so semantically equivalent spellings can miss. `Vary: *` prevents storage.
New network responses update the cookie jar, including a validating `304`; stored `Set-Cookie` is discarded
and never applied again. Authentication challenges, redirects, URL filters, interception, cancellation
and the consuming page's resource-size limits retain their existing ordering.

Use a context-owned `HttpClient` when caching. Its default headers participate in matching. A cache
cannot observe credentials or representations a custom handler changes after lookup: such a handler
must use stable context credentials or caching must remain disabled. Combining caching with a
per-engine `HttpClientFactory` is rejected because its authentication boundary can change between pages.
An engine callback that replaces the context client bypasses cache reads and writes for that client,
while unsafe requests still invalidate context entries.
No custom storage provider is published without an embedding consumer requiring one.

## Fetch and protocol controls

`Request.cache` and `RequestInit.cache` accept `default`, `no-store`, `reload`, `no-cache`, `force-cache`
and `only-if-cached`. `reload` bypasses lookup and updates storage; `no-store` bypasses both; `no-cache`
validates even fresh responses. `force-cache` permits stale reuse except when response directives forbid
it. `only-if-cached` never contacts the network and rejects on a miss or a required validation. It requires
`mode: 'same-origin'`; same-origin requests are refused on an origin change, including redirects. Other
Fetch CORS and no-cors limitations remain those of the browser's existing Fetch implementation.
Request `Cache-Control` directives and legacy `Pragma: no-cache` can also request validation or bypass.
A disabled cache still honors the network-only behavior of the modes and rejects `only-if-cached`.

`Page.Requests` exposes `FromCache`, `Revalidated` and `TransferredBodyLength`, alongside delivered
`BodyLength`. Local hits and validated retained bodies transfer zero body bytes; a revalidation still has
a network timing. Resource timing reports zero transfer size for local hits and the existing estimated
header cost for revalidation. DevTools sends `Network.requestServedFromCache`, reports zero encoded body
bytes for reused bodies, honors page-wide `Network.setCacheDisabled`, and clears the context cache with
`Network.clearBrowserCache`. Disk persistence restores entries into memory, so these are memory hits and
`fromDiskCache` remains false. No socket phase or network body bytes are invented for a local hit.
`Page.reload(ignoreCache)` remains accepted and ignored; use `Network.setCacheDisabled` when a reload
must bypass the cache.

## Disk persistence

Disk mode persists the bounded memory cache; it is not an additional unbounded store. Reopening reads
valid entries into the same bounded memory budget, preserving freshness metadata. The parent directory
and a stable visitor identity must both be supplied for persistence:

```csharp
options.HttpCache.Storage = BrowserHttpCacheStorage.Disk;
options.HttpCache.Directory = "/var/cache/my-app";
options.HttpCache.PartitionKey = "visitor-42";
```

The identity selects a hashed subdirectory. Different identities isolate sessions even under the same
parent. Reusing an identity explicitly opts into that visitor's persisted representations and credentials:
do not reuse it across tenants. One context/process owns a partition at a time through an exclusive file
lock; concurrent use is refused rather than merged. The host owns directory access permissions and must
protect cached authenticated content. Entries are written to a temporary file and atomically renamed;
truncated or malformed entries and abandoned temporary files are discarded on reopen.
Writes do not force a durable filesystem flush for every response: a crash can lose recent cache entries.
Checksums reject incomplete stored data on reopening.

Alternatively set `Temporary = true`. A unique subdirectory is then created under `Directory`, or the
system temporary directory when none is supplied, and removed on context disposal. Disk storage is never
created implicitly in memory or disabled mode. Construction errors, including an unavailable directory
or lock, propagate to the host. Later write failures decline persistence while memory caching continues;
temporary-directory cleanup is best effort when the filesystem refuses deletion. Entry deletion failures
propagate, including during clear or eviction, so an invalidation is never silently reported successful
while leaving a persisted representation. Persistent storage requires a writable, reliable directory.

The [300-image workload report](../benchmarks/http-cache-2026-10/README.md) separates requests, transferred
bodies, allocation and timing. Cache hits still incur the browser consumers' body copies; this feature does
not resolve the image allocation work in [#4013](https://github.com/sebastienros/jint/issues/4013).

## Command-line use

`jint-browser serve`, `fetch`, `eval` and `mcp` expose this configuration:

```sh
jint-browser fetch https://example.com --http-cache memory
jint-browser serve --http-cache-dir ./http-cache --http-cache-partition visitor-42
jint-browser mcp --http-cache-temporary --http-cache-max-bytes 32mb
```

Disk mode requires an explicit persistent directory and visitor identity, or explicitly temporary storage.
The CLI also exposes `--http-cache-max-entries` and `--http-cache-max-entry-bytes`. For `serve`, the default
context owns the persistent identity; additional CDP contexts use isolated temporary disk partitions.
See the [CLI reference](../../Jint.Browser.Tool/README.md#http-response-caching) for defaults and validation.

Hosts that need to configure protocol-created contexts can register `BrowserOptions.ConfigureContext`.
It runs before each context is created, including the default context, after explicitly provided options.
Callbacks run on the creating caller's thread and must support concurrent creation and separate visitor
identities. MCP hosts configure `BrowserAgentOptions.HttpCache` for their session context.
