# Jint.Browser.Mcp

`Jint.Browser.Mcp` exposes `Jint.Browser` through the Model Context Protocol. An agent can navigate, read DOM-derived snapshots, act on controls, inspect requests and cookies, and keep one browsing session.

It runs in one .NET process with no native browser or browser download. It **does not render** and offers no screenshots or PDFs. Agents read accessibility, markdown, or text snapshots instead.

[View Jint.Browser.Mcp on NuGet.org](https://www.nuget.org/packages/Jint.Browser.Mcp)

The quickest entry is the command-line tool:

```bash
dotnet tool install -g Jint.Browser.Tool
jint-browser mcp
```

Applications can embed the server with `AddJintBrowser`:

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .AddJintBrowser();
```

Pages are hardened for untrusted content by default. Stdio is the supported command-line transport and gives each client process its own browsing state.

- [Command line](./command-line)
- [Embedding](./embedding)
- [Tools and resources](./tools-and-resources)
- [Sessions and transports](./sessions-and-transports)
- [Security](./security)
- [Limitations](./limitations)
