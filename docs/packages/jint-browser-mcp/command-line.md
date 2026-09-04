# Command line

Install and run the packaged stdio server:

```bash
dotnet tool install -g Jint.Browser.Tool
jint-browser mcp
```

Client configuration:

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

Or register it with a client command:

```bash
claude mcp add jint-browser -- jint-browser mcp
```

Options:

| Option | Meaning |
| --- | --- |
| `--stdio` | Explicitly select the only command-line transport |
| `--trusted` | Disable the default untrusted-content profile |
| `--timeout <duration>` | Navigation and wait ceiling; 30 s by default |
| `--max-snapshot-length <n>` | Snapshot ceiling; 40,000 characters by default |
| `--user-agent <value>` | Set script and request user agent |
| `--max-task-duration <duration>` | Override one-turn time budget |
| `--memory-limit <size>` | Override one-turn allocation budget |
| `--block-private-network` | Explicitly block private destinations |
| `--allow-private-network` | Allow them even under the hardened profile |

Standard output is protocol traffic only. Diagnostics go to standard error. The command prints no banner.

`--http` is not supported. See [Sessions and transports](./sessions-and-transports).
