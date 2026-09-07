# Embedding

Install the package:

```bash
dotnet add package Jint.Browser.Mcp
```

Compose it with the MCP SDK:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateEmptyApplicationBuilder(null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .AddJintBrowser(options =>
    {
        options.Timeout = TimeSpan.FromSeconds(20);
        options.MaxSnapshotLength = 20_000;
        options.UrlFilter = uri =>
            uri.Scheme == "https"
            && uri.Host.EndsWith(".example.org", StringComparison.Ordinal);
    });

await builder.Build().RunAsync();
```

`AddJintBrowser` registers:

- one `Browser`
- one `BrowserAgent`
- `BrowserTools`
- `BrowserResources`

These are process-level registrations designed for one stdio client per process.

`BrowserAgentOptions` controls trust, private-network access, user agent, page-turn budgets, navigation/wait timeout, snapshot size, and a URL filter. Defaults are hardened: `Trusted` is `false`, and private-network access is blocked by the resulting profile.

The URL filter is checked on the first request and every redirect. It must be thread-safe and non-blocking.

If an HTTP host needs independent browsing sessions, do not use the singleton registration as-is. Build and dispose a `BrowserAgent` for each transport session using the SDK's session lifecycle; see [Sessions and transports](./sessions-and-transports).
