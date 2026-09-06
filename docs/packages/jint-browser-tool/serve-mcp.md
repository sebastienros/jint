# Serve MCP

Start a Model Context Protocol server:

```bash
jint-browser mcp
```

The transport is standard input/output. `--stdio` may be supplied explicitly. Standard output contains protocol messages only; diagnostics are sent to standard error.

Example client configuration:

```json
{
  "mcpServers": {
    "jint-browser": {
      "command": "jint-browser",
      "args": ["mcp"]
    }
  }
}
```

Useful options:

```bash
jint-browser mcp \
  --timeout 30s \
  --max-snapshot-length 40000
```

MCP pages are hardened by default. Use `--trusted` only when the content is trusted. `--allow-private-network` permits access to a local or private service while retaining the other hardened limits.

The process is the browsing session: cookies, storage, history, and the current page belong to that client process. The server serializes tool calls so a snapshot cannot race an action that replaces the document.

The command does not accept `--http`. Stdio gives one client one process and therefore one unambiguous stateful session. See the [`Jint.Browser.Mcp` documentation](../jint-browser-mcp/) for tools, resources, embedding, and HTTP session considerations.
