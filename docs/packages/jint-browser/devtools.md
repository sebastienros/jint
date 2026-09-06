# DevTools Protocol

Publish a browser through `Jint.DevTools`:

```csharp
using Jint.Browser;
using Jint.DevTools;

await using var browser = new Browser();
await using var server = new DevToolsServer(
    new DevToolsServerOptions { Port = 9222 });

await server.AddBrowser(browser);
server.Start();
```

Puppeteer and Playwright attach with their **connect** APIs, not launch APIs. Existing and newly opened pages become CDP page targets; target commands can create contexts and pages.

The implementation covers the page, DOM, input, network, fetch interception, storage, accessibility, and selected emulation paths needed by supported clients. Unsupported protocol commands report protocol errors rather than silently succeeding.

The custom `Jint` domain provides DOM-based answers where a graphical browser would provide pixels:

- `Jint.getMarkdown`
- `Jint.getText`
- `Jint.getAccessibilitySnapshot`

`Page.MarkdownAsync`, `TextAsync`, and `AccessibilitySnapshotAsync` use the same extractors in-process.

There is one browser per server. The CDP endpoint is unauthenticated: any client that reaches it can execute page script and access what its pages can access. Bind to loopback unless a separately secured environment requires otherwise.

Screenshot and PDF commands fail explicitly because this browser has no renderer. Isolated worlds are aliases for the document's single realm, not security boundaries.
