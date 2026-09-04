# Jint.Browser.Tool

`Jint.Browser.Tool` installs the `jint-browser` command. It loads scripted pages, extracts text-first representations, evaluates JavaScript, or serves the browser over CDP or MCP.

```bash
dotnet tool install -g Jint.Browser.Tool --prerelease
```

[View Jint.Browser.Tool preview builds on Feedz](https://feedz.io/org/sebastienros/repository/jint/packages/Jint.Browser.Tool)

```bash
jint-browser fetch https://example.org/ --dump markdown
jint-browser eval https://example.org/ "document.title"
jint-browser serve --port 9222
jint-browser mcp
```

The tool is a single .NET process with no browser download. It **does not render** and cannot produce screenshots, PDFs, or visual layout.

Standard output is the command's answer; page and usage diagnostics go to standard error. This makes `fetch` and `eval` safe to pipe. The `mcp` command reserves standard output for protocol messages.

- [Installation](./installation)
- [Fetch](./fetch)
- [Evaluate](./evaluate)
- [Requests and state](./requests-and-state)
- [Serve CDP](./serve-cdp)
- [Serve MCP](./serve-mcp)
- [Untrusted content](./untrusted-content)
- [Exit codes](./exit-codes)
- [Limitations](./limitations)
