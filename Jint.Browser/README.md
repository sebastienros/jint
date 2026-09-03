# Jint.Browser

A headless browser in one .NET process, from [Jint](https://github.com/sebastienros/jint) and
[AngleSharp](https://anglesharp.github.io/). AngleSharp is the HTML parser, the DOM and the CSSOM; Jint runs
the document's scripts; this package is the binding layer between them and the page runtime on top of it. It
navigates, runs a page's scripts against a real DOM, follows its network, keeps its cookies and storage, and
answers what the page turned out to be. **It renders nothing** — no layout, no pixels, no screenshots — and
there is no browser binary to download or launch.

```c#
await using var browser = new Browser();
var page = await browser.NewPageAsync();

await page.NavigateAsync("https://example.org/login");
await page.SubmitFormAsync("#login");

var user = await page.EvaluateAsync<string>("document.querySelector('#user').textContent");
```

A browser can also be published on a [`Jint.DevTools`](https://www.nuget.org/packages/Jint.DevTools) server
with `server.AddBrowser(browser)`, which makes every page a Chrome DevTools Protocol `page` target that
Puppeteer, PuppeteerSharp, Playwright and Playwright for .NET drive over `connect` — in the same process,
with nothing to install. For the command line, see
[`Jint.Browser.Tool`](https://www.nuget.org/packages/Jint.Browser.Tool); for an agent,
[`Jint.Browser.Mcp`](https://www.nuget.org/packages/Jint.Browser.Mcp) serves the same page over the Model
Context Protocol.

Requires .NET 8 or later. Not trim- or AOT-compatible in this version, because AngleSharp is not
trim-annotated.

What a page can and cannot do, the per-page budgets, `ForUntrustedContent`, and how much of it is measured
rather than claimed are in
[Jint's README](https://github.com/sebastienros/jint#headless-browser-opt-in-package).

Licensed under BSD-2-Clause, like the rest of Jint.
