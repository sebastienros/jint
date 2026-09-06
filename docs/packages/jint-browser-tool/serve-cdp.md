# Serve CDP

Start a Chrome DevTools Protocol endpoint:

```bash
jint-browser serve --host 127.0.0.1 --port 9222
```

`--port 0` requests an available port; the banner prints the selected HTTP and WebSocket endpoints. The server starts with one blank page so clients that enumerate targets immediately see a browser-like target set.

Connect rather than launch:

```js
const browser = await puppeteer.connect({
  browserURL: "http://127.0.0.1:9222"
});
const page = await browser.newPage();
await page.goto("https://example.org/");
```

Playwright clients use `ConnectOverCDPAsync` or the equivalent API. No browser executable or `playwright install` step is needed for the served browser.

The endpoint is unauthenticated, as CDP endpoints normally are. A client can execute page JavaScript and access anything pages can reach. Keep the default loopback binding unless network-level controls protect the endpoint.

Browser options accepted by `serve` include `--untrusted`, `--user-agent`, per-turn time and memory budgets, and private-network switches.

The command runs until interrupted. It cannot serve screenshot or PDF operations because the underlying browser has no renderer; those calls return explicit protocol errors.
