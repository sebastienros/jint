# Jint.Browser.Playwright

`Jint.Browser.Playwright` implements a focused subset of Microsoft.Playwright's public .NET interfaces directly over `Jint.Browser`.

It starts no browser, Node process, driver, or CDP connection. Operations run in the application process:

[View Jint.Browser.Playwright preview builds on Feedz](https://feedz.io/org/sebastienros/repository/jint/packages/Jint.Browser.Playwright)

With `using Jint.Browser.Playwright;` in scope:

<!-- snippet: package-playwright-first-page -->
```csharp
await using var browser = await JintPlaywright.BrowserType.LaunchAsync();
var page = await browser.NewPageAsync();

await page.SetContentAsync("<button id='save'>Save</button>");
await page.Locator("#save").ClickAsync();
```
<!-- endSnippet -->

The adapter is built against Microsoft.Playwright 1.62. It references Playwright for public API contracts while excluding its build assets, so Playwright's bundled Node runtime and browser driver are not copied into the consuming application.

This is an evolving compatibility layer, not a complete Playwright implementation. Unsupported members and non-default options fail explicitly. It does not render and cannot provide screenshots, PDF output, video, browser extensions, or native-browser behavior.

- [Getting started](./getting-started)
- [Browser API](./browser-api)
- [Locators and actions](./locators-and-actions)
- [Waiting and navigation](./waiting-and-navigation)
- [Supported API](./supported-api)
- [Limitations](./limitations)
