# Supported Platforms

## Jint

The core package targets:

- .NET Framework 4.7.2
- .NET Standard 2.0
- .NET Standard 2.1
- .NET 8
- .NET 10

This lets the interpreter run on legacy .NET Framework applications as well as current .NET.

Some APIs are available only on modern targets. The opt-in web API surface is compiled for .NET 8 and later.

## Optional packages

`Jint.DevTools`, `Jint.Browser`, and `Jint.Browser.Mcp` target
.NET 8 and .NET 10.

`Jint.Browser.Tool` is distributed as a native executable for Linux (glibc), macOS and Windows, on x64
and arm64. Installing its NuGet package requires the .NET 10 SDK; standalone release downloads require
no .NET installation. See [tool installation](../packages/jint-browser-tool/installation.md).

## Native AOT

The core `Jint` package and `Jint.DevTools` support Native AOT on compatible targets, with documented
limitations for reflection-based CLR interop. The browser libraries do not declare general trimming or
AOT compatibility, because AngleSharp is not trim-annotated. The closed `jint-browser` tool is published
and exercised as Native AOT separately.

See [Native AOT and trimming](../reference/native-aot.md).
