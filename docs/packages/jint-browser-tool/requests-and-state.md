# Requests and state

Each `fetch` or `eval` invocation creates a browser, context, and page, then disposes them. Cookies supplied with `--cookie` and storage created by the page therefore live only for that command.

Headers and cookies can seed a one-shot load:

```bash
jint-browser fetch https://example.org/account \
  --header 'Authorization: Bearer token' \
  --cookie theme=dark
```

Headers apply to every request unless the page or browser supplies that request's own value. A `User-Agent` header becomes the page's user agent, keeping `navigator.userAgent` and network requests consistent.

For a persistent browsing session, use:

- [`serve`](./serve-cdp) and inspect requests, cookies, storage, and history through a CDP client.
- [`mcp`](./serve-mcp), whose `network_requests`, `cookies`, `set_cookie`, back, forward, and reload tools expose session state.

There is no separate `jint-browser requests` command, and `fetch` output contains only the requested document representation. Page errors are written to standard error.

Images are not fetched because the browser does not render them. In persistent APIs, their references still appear in the request log with a reason. Request logs are summaries rather than traffic archives and do not retain arbitrary bodies or request headers.
