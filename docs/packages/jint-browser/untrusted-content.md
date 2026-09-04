# Untrusted content

Loading a URL grants page code the host process's network position. Harden browsers that load content nobody has reviewed:

```csharp
var options = new BrowserOptions
{
    MaxTaskDuration = TimeSpan.FromSeconds(2),
    MemoryLimit = 128 * 1024 * 1024,
}.ForUntrustedContent();

await using var browser = new Browser(options);
```

Call `ForUntrustedContent()` before constructing `Browser`.

The profile applies Jint's untrusted-code limits to every page engine and worker. Among other restrictions, it disables dynamic code evaluation (`eval` and `new Function`), CLR interop, the debugger, experimental features, and module loading, and bounds statements, time, allocation, recursion, arrays, regular expressions, promise waits, parsing, and result conversion.

It also enables `BlockPrivateNetwork` unless explicitly overridden. That rule refuses literal loopback, private, link-local, carrier-grade NAT, unique-local, and metadata addresses, plus `localhost` names.

Private-network blocking is not DNS rebinding protection: it cannot reject a public hostname solely because the socket later resolves it privately. Use a thread-safe, non-blocking `BrowserContextOptions.UrlFilter`, network isolation, or both:

```csharp
var context = await browser.NewContextAsync(new BrowserContextOptions
{
    UrlFilter = uri => uri.Scheme == "https" && uri.Host == "docs.example.org",
});
```

The filter runs for the first URL and every redirect, across documents, subresources, script networking, and workers.
