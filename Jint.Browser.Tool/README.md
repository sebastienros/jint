# jint-browser

A headless browser on the command line, from [Jint](https://github.com/sebastienros/jint) and
[AngleSharp](https://anglesharp.github.io/). It parses HTML, runs a page's scripts against a real DOM,
follows its network, and answers what the page turned out to be — as markdown, as text, as its accessibility
tree, or over the Chrome DevTools Protocol so that Puppeteer and Playwright can drive it.

**It renders nothing.** There is no layout, no pixels, no screenshots and no PDFs, and there is no browser to
download: it is a .NET tool that runs in one process on any platform .NET runs on. What it costs is a
fraction of Chromium's memory and CPU per page; what it costs you back is wall-clock time, because the
JavaScript is interpreted.

```bash
dotnet tool install -g Jint.Browser.Tool
```

## Reading a page

```bash
# The page as CommonMark, narrowed to its main content and capped
jint-browser fetch https://example.org/article --main-content --max-length 4000

# The accessibility tree: roles, names and states, one line per node
jint-browser fetch https://example.org/ --dump ax

# The document after its scripts have run
jint-browser fetch https://example.org/ --dump html

# A local file, with its own file: URL as the base
jint-browser fetch ./page.html --dump text
```

`--dump` is `markdown` (the default), `text`, `html` or `ax`. `--main-content` narrows the answer to the
first `<main>`, `[role=main]` or `<article>` the document has, and `--max-length` caps it at a word boundary
with a `[truncated]` marker so a short page and a cut one are told apart; both are refused with `--dump html`,
because a narrowed or truncated document is not a document. The answer goes to standard output as UTF-8 with
no byte order mark; everything the page's scripts got wrong goes to standard error.

## Waiting, headers and cookies

```bash
jint-browser fetch https://example.org/app \
  --wait-until networkidle \
  --timeout 60s \
  --header 'Authorization: Bearer …' \
  --cookie session=abc
```

`--wait-until` is `commit`, `domcontentloaded`, `load` (the default) or `networkidle` — the last being
`load` plus half a second in which the page made no request, which is what a single-page application needs
before it has anything to read. `--header` and `--cookie` may each be given more than once.

## Asking a question instead of reading the page

```bash
jint-browser eval https://example.org/ "document.querySelectorAll('article').length"
jint-browser eval https://example.org/ "[...document.links].map(a => a.href)"
```

The expression is evaluated in the loaded page and its result is serialized by `JSON.stringify` **in the
page**, so what comes out is what a script in that document would have got.

## Serving the protocol

```bash
jint-browser serve --port 9222
```

That is a browser on `http://127.0.0.1:9222` that Puppeteer, PuppeteerSharp, Playwright, Playwright for .NET,
chrome-remote-interface and the Chrome DevTools front end all connect to over CDP — `connect`, not `launch`:

```js
const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:9222' });
const page = await browser.newPage();
await page.goto('https://example.org/');
console.log(await page.title());
```

`--port 0` asks the operating system for a port and the banner says which. **The endpoint is
unauthenticated**, exactly as it is in Chrome: anything that can reach it can run script in this process, so
`--host` defaults to `127.0.0.1` and should stay there.

## Serving a browser to an agent

```bash
jint-browser mcp
```

That is a [Model Context Protocol](https://modelcontextprotocol.io/) server on standard input and output,
over one browsing session: an agent navigates, reads the page as its accessibility tree or as markdown, and
clicks, fills and types its way through it. In Claude Desktop's `claude_desktop_config.json`, VS Code's
`mcp.json`, or any client that starts a server as a child process:

```json
{
  "mcpServers": {
    "jint-browser": {
      "command": "jint-browser",
      "args": ["mcp"]
    }
  }
}
```

or, in Claude Code:

```bash
claude mcp add jint-browser -- jint-browser mcp
```

**`mcp` hardens the pages by default** — the opposite of the other commands, because a client is by
definition pointing this at content nobody vouched for. `--trusted` turns the profile off and
`--allow-private-network` lets an agent reach your own machine. The tools, the `ref=` handles that make a
snapshot actionable, and why stdio is the only transport are in
[`Jint.Browser.Mcp`'s README](https://github.com/sebastienros/jint/blob/main/Jint.Browser.Mcp/README.md).

## Loading content nobody vouches for

```bash
jint-browser fetch https://somewhere-unknown.example/ --untrusted --max-task-duration 2s --memory-limit 256mb
```

`--untrusted` hardens every page: no `eval`, no `new Function`, no CLR interop, no module loader, and bounds
on statements, wall-clock time, allocation, recursion and regular expressions — plus `--block-private-network`
on by default, so a page cannot reach `localhost`, a private address or a cloud metadata endpoint. Pass
`--allow-private-network` to load a page on your own machine under it anyway.

`--max-task-duration` and `--memory-limit` bound **one turn** of a page — one call the tool makes, one drain
of the event loop, one inline `<script>` — rather than the run as a whole, which is what a pumped event loop
allows anyone to bound.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | the page loaded and the command answered |
| `1` | the command line was wrong, or named a file or scheme that does not work |
| `2` | there was no document to show: a refused URL, a transport failure, or a timeout |
| `3` | the page loaded and something it ran exceeded its time or allocation budget |
| `4` | the expression `eval` was given threw |

A `404` or a `500` is exit code `0`: the error page is a document, and it is usually the one a caller
scraping it wants.

## What it cannot do

No rendering, so no screenshots, no PDFs, no `canvas` and nothing that depends on where a box really is. No
iframe scripting, no `WebAssembly`, no IndexedDB, no media. Images are never fetched; the reference is
recorded in the request log with the reason instead.

Full documentation of the package under it — what a page can and cannot do, and how much of it the
web-platform-tests measure rather than claim — is in
[Jint's README](https://github.com/sebastienros/jint#headless-browser-opt-in-package).

Licensed under BSD-2-Clause, like the rest of Jint.
