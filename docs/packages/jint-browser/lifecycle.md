# Browser, context, and page lifecycle

The ownership tree is:

```text
Browser
└── BrowserContext (cookies, localStorage, network position)
    └── Page (document, sessionStorage, engine, thread)
```

`Browser.NewPageAsync()` uses the default context. Use separate contexts for isolated sessions:

```csharp
await using var browser = new Browser();
await using var alice = await browser.NewContextAsync();
await using var bob = await browser.NewContextAsync();

var alicePage = await alice.NewPageAsync();
var bobPage = await bob.NewPageAsync();
```

Pages in one context share cookies and origin-partitioned `localStorage`. Different contexts share neither. `sessionStorage` belongs to a page.

Each page starts on `about:blank`. A top-level navigation creates a new Jint engine and document on the same page thread. The previous document receives `beforeunload`, `pagehide`, and `unload`; pending document work is cancelled and the old engine is disposed. Page-level history, request history, and emulation state survive the engine swap.

Closing a page stops its workers and thread. Closing a context closes all its pages. Closing the browser closes every context. Calls made after closure fail rather than hanging.

```csharp
await page.CloseAsync();
Console.WriteLine(page.IsClosed);
```

Prefer `await using` or explicit `CloseAsync()` so shutdown waits for page and worker threads to finish.
