# Storage and Cache API

## Web Storage

Storage is excluded from `UseWebApis()` because it gives script state that outlives an evaluation:

```csharp
var engine = new Engine(options => options.UseStorage());
engine.Execute("localStorage.setItem('theme', 'dark')");
```

Without providers, each engine gets separate in-memory `localStorage` and `sessionStorage` stores that disappear
with the engine. The default stores enforce `Options.WebApi.Storage.MaxTotalBytes`, initially 5 MiB, and raise a
script-visible `QuotaExceededError` when full.

Supply providers to persist or share data:

```csharp
var engine = new Engine(options =>
    options.UseStorage(localStorageProvider, sessionStorageProvider));
```

`StorageProvider` decides the lifetime and partitioning. A provider shared by concurrent engines must be
thread-safe. Partition persistent providers per tenant; using one process-wide provider can leak state across
scripts. A custom provider reports quota failures with `StorageQuotaExceededException`; unrelated provider
exceptions propagate to the host.

## Cache API

`UseCacheApi` installs `caches`, `CacheStorage`, `Cache`, and the request/response interfaces they store:

```csharp
var engine = new Engine(options => options.UseCacheApi(cache =>
{
    cache.Provider = cacheStorageProvider;
}));
```

The default is a private, in-memory store per engine with **no quota**. It can grow until the process runs out of
memory, and its contents survive `RestoreGlobalSnapshot`. Untrusted or multi-tenant hosts should implement
`CacheStorageProvider` with size limits, eviction, atomic writes, and tenant partitioning. A
`CacheQuotaExceededException` becomes `QuotaExceededError`; other provider failures become rejected `TypeError`
values whose original exception remains available to the host.

The Cache API does not grant network access. `put`, `match`, `delete`, and key enumeration work without it.
`cache.add` and `cache.addAll` additionally require [fetch](./fetch-and-networking.md) and use that feature's
URL, size, concurrency, and timeout policy.

Review [untrusted-code guidance](../untrusted-code.md) before exposing either API to untrusted scripts.
