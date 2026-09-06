# Navigation

```csharp
var response = await page.NavigateAsync(
    "https://example.org/",
    new NavigationOptions
    {
        WaitUntil = WaitUntilState.DomContentLoaded,
        Timeout = TimeSpan.FromSeconds(20),
    });
```

`WaitUntil` can be:

- `Commit`: the response is committed as the new document.
- `DomContentLoaded`: parsing completed and `DOMContentLoaded` fired.
- `Load`: the window `load` event fired. This is the default.

The timeout covers fetching, parsing, and waiting. `NavigationFailedException` means no usable document was produced, such as a refused URL, transport failure, unsupported content type, or timeout. HTTP error statuses are not navigation failures: a `404` or `500` body is still loaded and returned as a `PageResponse`.

`PageResponse` exposes the final URL, status, status text, headers, redirect state, and `Ok`.

## Script-triggered navigation

Arm the wait before triggering a navigation that does not return its own task:

```csharp
var navigated = page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
await page.EvaluateAsync("location.href = '/next'");
await navigated;
```

`ClickAsync`, `PressAsync`, and `SubmitFormAsync` capture and await navigation they directly cause.

## History

```csharp
await page.GoBackAsync(TimeSpan.FromSeconds(10));
await page.GoForwardAsync(TimeSpan.FromSeconds(10));
await page.ReloadAsync();
```

Fragment changes and `pushState` retain the engine and document. Cross-document traversal creates a new engine; there is no back/forward cache. `ReloadAsync` replaces the current document without adding a history entry.
