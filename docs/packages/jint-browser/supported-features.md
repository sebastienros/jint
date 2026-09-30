# Supported features

`Jint.Browser` is intended for script-driven DOM automation and extraction.

## Documents and script

- HTML parsing with inline, external, `defer`, and `async` classic scripts
- Module scripts, import maps, and dynamic `import()`
- `document.write` during parsing
- External style sheets and a CSSOM through AngleSharp.Css
- Image loading without a decoder: `complete`, `currentSrc`, `naturalWidth`/`naturalHeight`, the `load` and `error` events, `srcset`/`sizes`/`<picture>` selection, and `img.decode()`, all from the container header
- Emulated viewport, media type, and supported preferences such as colour scheme, reduced motion, contrast, and pointer capabilities in both `matchMedia` and stylesheet `@media` rules
- `about:blank`, `data:text/html`, direct content, and HTTP(S) navigation
- Custom elements, shadow DOM, templates, ranges, traversal, selection, DOMParser, and XMLSerializer
- Geometry Interfaces: `DOMPoint`, `DOMRect`, `DOMQuad` and `DOMMatrix` with their read-only parents and `WebKitCSSMatrix`, including transform-list strings; layout answers such as `getBoundingClientRect` are `DOMRect` instances
- CSS Font Loading: `FontFace` from a `src` string or a buffer, with its descriptors parsed and serialized, `load()` fetching `url()` sources in order, and `document.fonts` as a set-like `FontFaceSet` with `check()`, `load()`, `ready` and the `loading`/`loadingdone`/`loadingerror` events
- The HTML Sanitizer API: `Sanitizer`, `setHTML`/`setHTMLUnsafe`, `getHTML` and `Document.parseHTML`/`parseHTMLUnsafe`, including declarative shadow roots (sanitizing runs after parsing, see [the DOM divergences](../../../Jint.Browser/Dom/divergences.md#sanitizer-sanitize-after-parsing-not-while-parsing))

## Runtime

- Timers, promises, microtasks, animation-frame callbacks, and `postMessage`
- `fetch`, `XMLHttpRequest`, WebSocket, EventSource, blobs, and FileReader
- Forms, validation, history, location, cookies, local/session storage
- Dedicated workers
- Mutation, intersection, and resize observers, with documented no-layout semantics
- Constructible event interfaces beyond UI Events: `ToggleEvent` (fired by `<dialog>` and `<details>`), `CommandEvent`, `AnimationEvent`, `TransitionEvent`, `GamepadEvent`, `DragEvent`, `StorageEvent`, `TouchEvent` and the device events, plus the legacy `TextEvent` through `document.createEvent`; `ClipboardEvent` is absent
- The navigator's system state: HTML's compatibility constants, empty `plugins`/`mimeTypes`, `userAgentData` (UA Client Hints, following a CDP `userAgentMetadata` override), `permissions.query`, `storage.estimate`, `registerProtocolHandler` argument validation, `Notification`, `screen`/`screen.orientation` and `visualViewport` following the emulated viewport — see [Limitations](./limitations) for what they answer

## Automation and reading

- CSS selectors and accessibility-snapshot `ref=` targets
- Click, focus, typing, key presses, select controls, form submission, and virtual scrolling
- Markdown, text, serialized HTML, and accessibility snapshots
- Request, console, page-error, response, and dialog inspection
- CDP connections for supported Puppeteer and Playwright operations

Feature support is intentionally narrower than a graphical browser. Check [Limitations](./limitations) before depending on layout, frames, media, or full client compatibility.
