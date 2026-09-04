# Limitations

The tool exposes the limits of `Jint.Browser`:

- No rendering, screenshots, PDFs, pixels, browser window, canvas, WebGL, or media.
- No visual layout; geometry used by automation is synthetic.
- No iframe scripting, IndexedDB, WebAssembly, CSP enforcement, SharedWorker, or ServiceWorker.
- No drag and drop, clipboard integration, or native input.
- Images are not downloaded for display.
- JavaScript is interpreted, so wall-clock execution can be slower than a native browser engine.

Command-specific limits:

- `fetch` and `eval` are one-shot sessions; state does not carry to another invocation.
- `fetch` exposes HTML, text, markdown, or accessibility output—not request-log output.
- `eval` evaluates one expression and does not implicitly await its result.
- `serve` supports a useful subset of CDP, not every Chrome behavior.
- `mcp` uses stdio only.
- CDP is unauthenticated and must not be exposed casually.
- `--main-content` and `--max-length` are invalid with `--dump html`.

Use `fetch` for extraction, `eval` for a focused value, `serve` for a persistent automation client, and `mcp` for a stateful agent session.
