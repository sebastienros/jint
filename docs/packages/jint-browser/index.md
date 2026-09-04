# Jint.Browser

`Jint.Browser` is a headless browser for .NET built from AngleSharp and Jint. AngleSharp supplies the HTML parser, DOM, and CSSOM; Jint runs the document's JavaScript. The package adds navigation, an event loop, network and storage state, input, extraction, and automation seams.

It runs in-process and has no browser binary to install. It also **does not render**: there are no pixels, screenshots, PDFs, or browser windows.

```bash
dotnet add package Jint.Browser --prerelease
```

[View Jint.Browser preview builds on Feedz](https://feedz.io/org/sebastienros/repository/jint/packages/Jint.Browser)

With `using Jint.Browser;` in scope:

<!-- snippet: package-browser-first-page -->
```csharp
await using var browser = new Browser();
var page = await browser.NewPageAsync();

await page.NavigateAsync("https://example.org/");
Console.WriteLine(await page.MarkdownAsync());
```
<!-- endSnippet -->

The package targets .NET 8 and later. It is not currently trim- or AOT-compatible because AngleSharp is not trim-annotated.

## Choose a topic

- [Getting started](./getting-started)
- [Browser, context, and page lifecycle](./lifecycle)
- [Navigation](./navigation)
- [DOM and evaluation](./dom-and-evaluation)
- [Input and forms](./input-and-forms)
- [Network and storage](./network-and-storage)
- [Extraction](./extraction)
- [Events and workers](./events-and-workers)
- [DevTools Protocol](./devtools)
- [Budgets](./budgets)
- [Untrusted content](./untrusted-content)
- [Supported features](./supported-features)
- [Limitations](./limitations)
