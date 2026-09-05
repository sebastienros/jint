# Native AOT and Trimming

## Jint

The core package is marked AOT-compatible on modern target frameworks and is exercised through a published native
application in CI.

Reflection-based CLR interop still depends on the host preserving the types and members that scripts reach.
Generic instantiations over value types discovered only at runtime are a known limitation. Prefer explicit host
types and generated accessors for a predictable native build.

## Jint.DevTools

`Jint.DevTools` is Native AOT compatible. Protocol JSON uses source-generated serialization.

## Browser packages

`Jint.Browser` and the browser libraries do not declare general trimming or AOT compatibility: AngleSharp
is not trim-annotated, and another host's reflection and interop requirements still need evaluation.

**The `jint-browser` executable is distributed as Native AOT**, with its complete closed dependency graph
published and exercised on Linux, macOS and Windows, for x64 and arm64. This includes DOM parsing and scripts,
CSS, XML, XPath, globalization, CDP and MCP. No browser assembly is rooted wholesale to make the probes pass.
The core engine's standing AOT diagnostics remain visible; diagnostics outside that inventory fail the
distribution build.

Build it with `dotnet publish Jint.Browser.Tool/Jint.Browser.Tool.csproj -c Release -f net10.0 -r <rid>
-p:BrowserToolNative=true`. Do not pass `PublishAot=true` globally: it also affects project references
targeting frameworks that cannot be compiled with Native AOT.

The NuGet tool requires the .NET 10 SDK to install its platform-specific package; the standalone release
downloads require neither the SDK nor a .NET runtime. See [tool installation](../packages/jint-browser-tool/installation.md).

See the [Jint 5 migration guide](../guide/migrating-to-v5.md) for the complete compatibility contract.
