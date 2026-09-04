# Budgets

`BrowserOptions` bounds page work and network resources:

```csharp
var options = new BrowserOptions
{
    MaxTaskDuration = TimeSpan.FromSeconds(2),
    MemoryLimit = 128 * 1024 * 1024,
    MaxDomNodes = 100_000,
    MaxDocumentBytes = 8 * 1024 * 1024,
};
```

Important defaults:

| Option | Default | Scope |
| --- | ---: | --- |
| `MaxTaskDuration` | 5 s | One page turn |
| `MemoryLimit` | Unlimited | Managed allocation in one turn |
| `MaxActiveTimers` | 1,000 | One page engine |
| `MaxResponseBytes` | 32 MiB | One script `fetch`/XHR response |
| `FetchTimeout` | 30 s | One script fetch redirect chain |
| `MaxDocumentBytes` | 32 MiB | One navigation document |
| `MaxSubresourceBytes` | 8 MiB | One script, module, style, or frame resource |
| `SubresourceTimeout` | 30 s | One subresource |
| `MaxDomNodes` | Unlimited | Document nodes and projected wrappers |
| `MaxFrameDocuments` | 16 | Fetched child-frame documents per load |
| `MaxRedirects` | 20 | One redirect chain |

A turn is one posted page call, one event-loop drain, or one inline script. A host call exceeding its turn budget faults with `TimeoutException`; a runaway script or queued callback becomes a `PageErrorKind.BudgetExceeded`, and the page remains usable.

`NavigationOptions.Timeout` separately bounds a whole navigation. `WaitForIdleAsync` and `WaitForNetworkIdleAsync` also take caller-supplied ceilings.
