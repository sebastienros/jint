# Jint.Browser.Mcp

A [Model Context Protocol](https://modelcontextprotocol.io/) server over a headless browser, from
[Jint](https://github.com/sebastienros/jint) and [AngleSharp](https://anglesharp.github.io/). An agent
navigates, reads a page as its accessibility tree or as markdown, and clicks, fills and types its way
through it — in one .NET process, with no browser to download and no native binary anywhere.

**It renders nothing.** There are no screenshots and no PDFs, because there is no layout: what it answers
instead is the page — its accessibility tree, its prose, its text. For an agent that is usually the better
answer anyway, and it costs a fraction of the tokens a picture would.

## Using it

The quickest way is the `jint-browser` command line, which is this server plus a transport:

```bash
dotnet tool install -g Jint.Browser.Tool
```

Then, in Claude Desktop's `claude_desktop_config.json`, VS Code's `mcp.json`, or any client that starts a
server as a child process:

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

**stdio is the transport, and the only one.** A client starts the program, drives it and ends it, so the
process is the session and its browsing context — its cookies, its storage, its history — is that session's
alone. See *Sessions and HTTP* below.

## In your own server

```csharp
// dotnet add package Jint.Browser.Mcp   (net8.0 and later)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateEmptyApplicationBuilder(null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .AddJintBrowser(browser =>
    {
        browser.Timeout = TimeSpan.FromSeconds(20);
        browser.UrlFilter = url => url.Host.EndsWith(".example.org", StringComparison.Ordinal);
    });

await builder.Build().RunAsync();
```

`AddJintBrowser` registers the browser, the session and every tool and resource below. `BrowserAgentOptions`
is what a deployment decides: the security posture, the budgets, the ceiling on a snapshot, and the
`UrlFilter` that pins an agent to one site.

## The tools

| Tool | What it does |
| --- | --- |
| `navigate(url, waitUntil?)` | Loads a URL; answers its final URL, title and status. |
| `snapshot(mode?, mainContentOnly?, maxLength?)` | Reads the page. `ax` (the default) is the accessibility tree with a `ref=` on every element; `markdown` is the page as prose; `text` is the same without formatting. |
| `click`, `fill`, `type`, `press`, `select`, `hover`, `scroll` | Drive the page. Each takes a `ref=` from an `ax` snapshot or a CSS selector. |
| `back`, `forward`, `reload` | The three buttons above the page. |
| `evaluate(expression)` | Runs one JavaScript expression and answers its JSON, serialized by the page. |
| `wait_for(selector?, text?, timeoutSeconds?)` | Waits for the page to catch up with what an action started. |
| `network_requests()` | Every request the page made, with the status each answered. |
| `cookies(url?)`, `set_cookie(name, value, url?)` | Read and seed the session's cookies. |
| `close()` | Ends the session; the next `navigate` starts a clean one. |

Two resources answer about the same session: `jint://page/markdown` and `jint://page/requests`.

**A `ref=` is the handle that makes a snapshot actionable.** An `ax` snapshot prints
`- button "Save" [ref=42]`, and `click("ref=42")` reaches exactly that element — which is what an agent has,
where a CSS selector is something it would have to invent. A reference belongs to the document it was
printed from, so take a fresh snapshot after anything that navigates.

**No tool throws through the transport.** Each answers its result as JSON, or `isError` with one sentence
saying what could not be done and what to try instead.

## The security posture

**Hardened by default**, because a client is by definition pointing this at content nobody vouched for — a
page a model chose, from a search result, from a link in an email. Every page runs Jint's
`ForUntrustedContent()` profile: no `eval`, no `new Function`, no CLR interop, no module loader, and bounded
statements, wall-clock time, allocation, recursion and regular expressions. Loopback, private, link-local and
cloud-metadata addresses are refused.

`BrowserAgentOptions.Trusted` turns the profile off and `BlockPrivateNetwork` decides the network rule on its
own, so a deployment pointing an agent at its own staging server says so once, out loud. `UrlFilter` is the
narrower tool and the one to reach for: it is checked on the first hop and on every redirect.

## Sessions and HTTP

A browsing session is state — cookies, storage, history, an open page — and the protocol's 2026-07-28
revision removed the session header from streamable HTTP, so the SDK's HTTP transport is stateless by
default and one server answers every caller. `AddJintBrowser` therefore registers **one** browser and one
session for the process, which is exactly right for stdio and wrong for a shared HTTP endpoint.

A host that serves HTTP and needs a session each does it through the SDK's own
`HttpServerTransportOptions.ConfigureSessionOptions` and `RunSessionHandler`: build a `BrowserAgent` per
session there, bind `BrowserTools` and `BrowserResources` instances to it, and dispose it in that handler's
`finally`. That is the SDK's session-ended hook, and it is what `AddJintBrowser` deliberately does not
assume on your behalf.

## What it cannot do

No screenshots, no PDFs, no layout — so nothing that depends on where a box really is, and no drag and drop.
No iframe scripting, no `WebAssembly`, no IndexedDB, no media. Images are never fetched; the reference is in
the request log with the reason instead. A hover fires `mousemove` but not `mouseenter`, so a menu written
against the latter does not open.

The engine underneath is an interpreter, so a page costs a fraction of Chromium's memory and CPU and some
multiple of its wall-clock time. What the whole stack can and cannot do — and how much of it the
web-platform-tests measure rather than claim — is in
[Jint's README](https://github.com/sebastienros/jint#jintbrowser-opt-in-package-in-progress).

Licensed under BSD-2-Clause, like the rest of Jint.
