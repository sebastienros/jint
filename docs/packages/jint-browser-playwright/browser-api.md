# Browser API

`JintPlaywright.BrowserType` exposes the default `IBrowserType`. `CreateBrowserType` lets you configure every launched `Jint.Browser.Browser`.

## Browser and context

```csharp
await using var browser = await JintPlaywright.BrowserType.LaunchAsync();
await using var context = await browser.NewContextAsync();
var page = await context.NewPageAsync();
```

Supported browser operations include:

- `BrowserType`, `Contexts`, `IsConnected`, and `Version`
- `NewContextAsync`, `NewPageAsync`, `CloseAsync`, and async disposal
- context and disconnect events

Supported context operations include:

- `Browser`, `Pages`, `BackgroundPages`, and `IsClosed`
- `NewPageAsync`, `CloseAsync`, and async disposal
- page and close events
- `SetDefaultTimeout` and `SetDefaultNavigationTimeout`

Context options are not currently supported. Passing any non-default option to `NewContextAsync` or `NewPageAsync` fails rather than being ignored.

## Page and frame

Pages expose context, main frame, frames, URL, closed state, close events, content, title, evaluation, navigation, locators, timeouts, and closure. The frame surface is intentionally small: page, URL, name, parent/child frame metadata, detached state, CSS locators, content, and title.

`BringToFrontAsync` completes successfully because there is no window or tab UI to reorder.
