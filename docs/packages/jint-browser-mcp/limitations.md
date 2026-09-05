# Limitations

The MCP server inherits `Jint.Browser` limitations:

- No rendering, screenshots, PDFs, pixels, native window, canvas, WebGL, or media.
- No real visual layout; scrolling and action geometry are synthetic.
- No iframe scripting, IndexedDB, WebAssembly, CSP enforcement, SharedWorker, or ServiceWorker.
- No drag and drop or clipboard tools.
- Images are not fetched for display.
- Hover dispatches movement, not `mouseenter` or other pointer-boundary events.

Protocol and tool limits:

- The packaged command supports stdio only.
- `AddJintBrowser` is one process-level session and must not be reused unchanged for a shared HTTP service.
- Snapshots are capped at 40,000 characters by default.
- `wait_for` accepts a selector or text; it is not a general JavaScript condition.
- `evaluate` runs one host-supplied expression; page code still cannot call `eval` or `new Function` under the default untrusted profile.
- Cookie enumeration requires the default enumerable cookie jar.
- Element references expire on navigation.
- Tools cover one page per agent session.
- Network results are summaries, not complete request/response archives.

Use snapshots rather than asking for images, take a fresh accessibility snapshot after actions that navigate, and treat `done: false` as a recoverable miss rather than a transport failure.
