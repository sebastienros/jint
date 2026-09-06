# Installation

Starting with Jint 5.0, install the global tool with the **.NET 10 SDK or later**:

```bash
dotnet tool install -g Jint.Browser.Tool
```

The SDK selects a Native AOT executable for Linux, macOS or Windows on x64 or arm64. No .NET runtime is
needed to run it. The browser libraries still target .NET 8 and .NET 10; the SDK requirement is for the
native tool package format.

## Without .NET

Download a binary from [GitHub Releases](https://github.com/sebastienros/jint/releases):

| Platform | x64 | arm64 |
| --- | --- | --- |
| Linux (glibc) | `jint-browser-linux-x64` | `jint-browser-linux-arm64` |
| macOS | `jint-browser-osx-x64` | `jint-browser-osx-arm64` |
| Windows | `jint-browser-win-x64.exe` | `jint-browser-win-arm64.exe` |

Compare its SHA-256 hash with `SHA256SUMS` on the same release. On Linux/macOS, mark it executable with
`chmod +x`, rename it to `jint-browser`, and put it on your `PATH`. On Windows, rename it to
`jint-browser.exe` and put it on your `PATH`. No Apple notarization or Windows Authenticode signature is provided.

Linux binaries target Ubuntu 22.04 (x64) and Ubuntu 24.04 (arm64) or compatible newer glibc systems.
They still need OS libraries such as ICU, OpenSSL and zlib. Alpine/musl is not included.
See [Native AOT and trimming](../../reference/native-aot.md) for the distinction between this executable
and using the browser libraries in another native application.

## Updating and running

Update it later with:

```bash
dotnet tool update -g Jint.Browser.Tool
```

Show help or the installed version:

```bash
jint-browser --help
jint-browser version
```

The commands are:

| Command | Purpose |
| --- | --- |
| `fetch <url\|file>` | Load one page and write HTML, text, markdown, or an accessibility outline |
| `eval <url\|file> <expression>` | Load one page and write an expression result as JSON |
| `serve` | Publish a persistent browser over Chrome DevTools Protocol |
| `mcp` | Publish one agent browsing session over standard input/output |
| `version` | Print the package version |

`fetch` and `eval` accept HTTP(S) URLs or local files. Local files are loaded as content with their own `file:` URL as the base, so relative references resolve from the file location.

Durations accept forms such as `30s`, `500ms`, `5m`, or a number of seconds. Sizes accept forms such as `256mb`, `512kb`, or a byte count.

Unknown options and options missing a required value are usage errors. Use `--` to end option parsing when an `eval` expression begins with `-`.
