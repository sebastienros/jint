# Tools and resources

## Navigation and reading

| Tool | Purpose |
| --- | --- |
| `navigate` | Load an absolute URL; wait for commit, DOM content loaded, load, or network idle |
| `back`, `forward`, `reload` | Move through or reload session history |
| `snapshot` | Read `ax`, `markdown`, or `text` output |
| `close` | Close the current page and context; the next navigation starts clean |

`ax` is the default snapshot mode. It includes handles such as:

```text
- button "Save" [ref=42]
```

Pass `ref=42` to element actions. References belong to the current document; take another snapshot after navigation.

## Actions and waits

- `click(target)`
- `fill(target, text)`
- `type(target, text)`
- `press(key)`
- `select(target, value)`
- `hover(target)`
- `scroll(y)`
- `wait_for(selector?, text?, timeoutSeconds?)`
- `evaluate(expression)`

Targets are accessibility references or CSS selectors. `fill` replaces the value; `type` preserves it and dispatches each key. Prefer actions to evaluation when page event handlers should run.

## Network and cookies

- `network_requests()`
- `cookies(url?)`
- `set_cookie(name, value, url?)`

## Resources

- `jint://page/markdown`
- `jint://page/requests`

Tools return structured JSON. Recoverable misses, such as a selector matching nothing, return `done: false`. Failures return an MCP `isError` result with an actionable message instead of throwing an opaque transport exception.
