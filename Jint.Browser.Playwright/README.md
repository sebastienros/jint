# Jint.Browser.Playwright

`Jint.Browser.Playwright` implements the public Microsoft.Playwright browser interfaces directly over
[`Jint.Browser`](https://github.com/sebastienros/jint/tree/main/Jint.Browser). It starts no browser or Node
process and uses no Chrome DevTools Protocol connection.

```csharp
using Jint.Browser.Playwright;
using Microsoft.Playwright;

IBrowserType browserType = JintPlaywright.BrowserType;

await using var browser = await browserType.LaunchAsync();
var page = await browser.NewPageAsync();

await page.SetContentAsync("<button id='save'>Save</button>");
await page.Locator("#save").ClickAsync();
```

The adapter is an evolving compatibility layer. Operations backed by `Jint.Browser` execute in-process;
features that require pixels, a native browser, or an operating-system window fail with
`NotSupportedException`. This includes screenshots, PDF generation, video, browser extensions and CDP
sessions.

Locator support currently covers CSS selectors and a first set of role locators, with strict matching,
waiting and trusted input dispatch. It does not yet reproduce Playwright's complete actionability,
accessibility-name or atomic locator-resolution semantics.

The package is built and tested against Microsoft.Playwright 1.62. Applications can use Playwright's public
browser, context, page, frame and locator interfaces, but Playwright features that downcast those interfaces
to its internal implementation, including its built-in assertions, cannot use a third-party implementation.

The package references Microsoft.Playwright for its public API contracts but excludes its build assets, so
the bundled Node runtime and driver are not copied to the consuming application.
