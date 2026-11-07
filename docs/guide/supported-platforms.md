# Supported Platforms

## Jint

The core package targets:

- .NET Framework 4.7.2
- .NET Standard 2.0
- .NET Standard 2.1
- .NET 10
- .NET 11

This lets the interpreter run on legacy .NET Framework applications as well as current .NET.

The .NET 11 target is compiled with runtime async enabled; older targets keep compiler-generated async state machines.

Some APIs are available only on modern targets. The opt-in web API surface is compiled for .NET 10 and later.

## Optional packages

`Jint.HtmlParser`, `Jint.DevTools`, `Jint.Browser`, and `Jint.Browser.Mcp` target
.NET 10 and .NET 11.

`Jint.Browser.Tool` is distributed as a native executable for Linux (glibc), macOS and Windows, on x64
and arm64. Installing its NuGet package requires the .NET 10 SDK; standalone release downloads require
no .NET installation. See [tool installation](../packages/jint-browser-tool/installation.md).

## Native AOT

The core `Jint` package and `Jint.DevTools` support Native AOT on compatible targets, with documented
limitations for reflection-based CLR interop. The experimental `Jint.HtmlParser` library is also marked
AOT-compatible and verified through a packed native consumer. The browser libraries do not declare
general trimming or AOT compatibility. The closed `jint-browser` tool is published
and exercised as Native AOT separately.

See [Native AOT and trimming](../reference/native-aot.md).
