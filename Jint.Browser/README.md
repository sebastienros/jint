# Jint.Browser

A headless browser in one .NET process, from [Jint](https://github.com/sebastienros/jint) and
Jint.HtmlParser. The native parser supplies the document tree and CSS model; Jint runs
the document's scripts; this package supplies their bindings and the page runtime. It
navigates, runs a page's scripts against a real DOM, follows its network, keeps its cookies and storage, and
answers what the page turned out to be. Its flat box model supplies geometry; it paints no pixels and
produces no screenshots. There is no browser binary to download or launch.

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

Requires .NET 8 or later. The Browser library does not currently declare trimming or NativeAOT
compatibility. Native publication of the closed browser tool is a separate consumer contract.

Stylesheet installation loads native `@import` graphs in source order, resolves nested URLs against
each response's final URL, and bounds cycles without merging sibling sheet identities. CSSOM
`insertRule` and `deleteRule` queue import updates after the current script's microtasks; style reads
never fetch. Removed or replaced owners cannot receive stale imported sheets. Import failures are
reported and an initial linked-sheet failure raises the owner's `error` event before window load.
Encoding currently follows response headers and the inherited encoding; CSS BOM/`@charset` selection
and response MIME eligibility remain incomplete.

What a page can and cannot do, the per-page budgets, `ForUntrustedContent`, and how much of it is measured
rather than claimed are in
[Jint's README](https://github.com/sebastienros/jint#headless-browser-opt-in-package).

Licensed under BSD-2-Clause, like the rest of Jint.
