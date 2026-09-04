# Getting started

Install the adapter:

```bash
dotnet add package Jint.Browser.Playwright
```

Create a browser through the familiar public interfaces:

```csharp
using Jint.Browser.Playwright;
using Microsoft.Playwright;

IBrowserType browserType = JintPlaywright.BrowserType;
await using var browser = await browserType.LaunchAsync();
var page = await browser.NewPageAsync();

await page.GotoAsync("https://example.org/");
Console.WriteLine(await page.TitleAsync());
```

Only headless launch is meaningful. `Headless = false` fails because there is no native window.

Configure the underlying `BrowserOptions` by creating a browser type:

```csharp
var browserType = JintPlaywright.CreateBrowserType(options =>
{
    options.MaxTaskDuration = TimeSpan.FromSeconds(2);
    options.ForUntrustedContent();
});

await using var browser = await browserType.LaunchAsync();
```

The returned objects implement public Playwright interfaces through runtime proxies. Use the public browser, context, page, frame, locator, response, and JavaScript-handle members documented in this section.

Unknown operations fail. Async members return faulted tasks, so catch failures around `await`:

```csharp
try
{
    await page.ScreenshotAsync();
}
catch (NotSupportedException)
{
    // Jint.Browser has no pixels.
}
```
