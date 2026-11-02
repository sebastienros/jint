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

JavaScript URLs run their percent-decoded source in the active document. Canceling a link's click prevents
that execution. A non-string result leaves the document and URL unchanged; a string result replaces the
document as HTML at the existing URL, without fetching or adding a history entry. Scripting policy and
cross-page origin checks still apply.

Same-origin child windows support `location.assign()`, `location.replace()`, `location.reload()` and
`location.href` assignment through the existing frame loader. The iframe's `src` attribute and the parent
document are unchanged. Child session history and stable WindowProxy identity across navigation remain
unsupported; retain the iframe element and read its current `contentWindow` after loading.

## History

```csharp
await page.GoBackAsync(TimeSpan.FromSeconds(10));
await page.GoForwardAsync(TimeSpan.FromSeconds(10));
await page.ReloadAsync();
```

Fragment changes and `pushState` retain the engine and document. Cross-document traversal creates a new engine; there is no back/forward cache. `ReloadAsync` replaces the current document without adding a history entry.
