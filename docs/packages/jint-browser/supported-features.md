# Supported features

`Jint.Browser` is intended for script-driven DOM automation and extraction.

## Documents and script

- HTML parsing with inline, external, `defer`, and `async` classic scripts
- Module scripts, import maps, and dynamic `import()`
- `document.write` during parsing
- External style sheets and a CSSOM through AngleSharp.Css
- `about:blank`, `data:text/html`, direct content, and HTTP(S) navigation
- Custom elements, shadow DOM, templates, ranges, traversal, selection, DOMParser, and XMLSerializer

## Runtime

- Timers, promises, microtasks, animation-frame callbacks, and `postMessage`
- `fetch`, `XMLHttpRequest`, WebSocket, EventSource, blobs, and FileReader
- Forms, validation, history, location, cookies, local/session storage
- Dedicated workers
- Mutation, intersection, and resize observers, with documented no-layout semantics

## Automation and reading

- CSS selectors and accessibility-snapshot `ref=` targets
- Click, focus, typing, key presses, select controls, form submission, and virtual scrolling
- Markdown, text, serialized HTML, and accessibility snapshots
- Request, console, page-error, response, and dialog inspection
- CDP connections for supported Puppeteer and Playwright operations

Feature support is intentionally narrower than a graphical browser. Check [Limitations](./limitations) before depending on layout, frames, media, or full client compatibility.
