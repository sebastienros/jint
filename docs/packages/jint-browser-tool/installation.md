# Installation

Install the global .NET tool:

```bash
dotnet tool install -g Jint.Browser.Tool
```

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
