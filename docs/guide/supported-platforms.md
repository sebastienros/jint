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

`Jint.DevTools`, `Jint.Browser`, `Jint.Browser.Playwright`, `Jint.Browser.Tool`, and `Jint.Browser.Mcp` target
.NET 8 and .NET 10.

## Native AOT

The core `Jint` package and `Jint.DevTools` support Native AOT on compatible targets, with documented
limitations for reflection-based CLR interop. `Jint.Browser` and the packages built on it are not currently
trim- or AOT-compatible because AngleSharp is not trim-annotated.

See [Native AOT and trimming](../reference/native-aot.md).
