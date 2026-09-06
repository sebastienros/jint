# Native AOT

Jint.DevTools supports Native AOT on its supported target frameworks. Protocol serialization uses generated
`System.Text.Json` metadata, so consumers do not need to root protocol types or enable reflection for the
package.

Use the same public API in an AOT application:

```csharp
var engine = new Engine(options => options.UseDevTools());
await using var target = new EngineTarget(engine, new EngineTargetOptions
{
    ThreadMode = ThreadMode.LibraryOwned
});
await using var server = new DevToolsServer();

server.AddTarget(target);
server.Start();

await target.PostAsync(engine =>
{
    engine.Execute("globalThis.ready = true", "startup.js");
});
```

A typical project enables AOT in its project file:

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

Then publish for the required runtime:

```bash
dotnet publish -c Release -r linux-x64
```

The package's AOT compatibility is exercised in CI by publishing a native application and driving a real
socket through target discovery, attachment, evaluation, breakpoint pause, and resume.

Native AOT does not change the [threading requirements](hosting.md) or the
[unauthenticated endpoint risk](security.md). Keep the listener on loopback and submit library-owned engine
work through `Post` or `PostAsync`.
