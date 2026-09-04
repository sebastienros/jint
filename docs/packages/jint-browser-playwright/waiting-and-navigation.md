# Waiting and navigation

Supported navigation methods are `GotoAsync`, `ReloadAsync`, `GoBackAsync`, and `GoForwardAsync`.

```csharp
var response = await page.GotoAsync(
    "https://example.org/app",
    new PageGotoOptions
    {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = 20_000,
    });
```

Supported wait states are commit, DOM content loaded, load, and network idle. Network idle means load completed followed by half a second without request activity.

`GotoAsync` supports `Timeout`, `WaitUntil`, and `Referer`. Reload and history navigation support their timeout and wait-state options. Unsupported non-default options fail instead of being ignored.

Set defaults at context or page scope:

```csharp
context.SetDefaultTimeout(5_000);
context.SetDefaultNavigationTimeout(15_000);
page.SetDefaultTimeout(2_000);
```

`WaitForFunctionAsync` polls an expression and supports `Timeout` and `PollingInterval`:

```csharp
await page.WaitForFunctionAsync(
    "() => document.querySelectorAll('.row').length >= 3");
```

Its returned `IJSHandle` supports JSON-value extraction, `AsElement` returning `null`, and disposal.

Locator waits poll while the page event loop continues to process timers, promises, and network completions. Timeouts are reported as `TimeoutException`.
