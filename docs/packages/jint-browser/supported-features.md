# Supported features

`Jint.Browser` is intended for script-driven DOM automation and extraction.

## Documents and script

- HTML parsing with inline, external, `defer`, and `async` classic scripts
- Module scripts, import maps, and dynamic `import()`
- `document.write` during parsing
- External style sheets and native CSS parsing with a browser-owned CSSOM
- Image loading without a decoder: `complete`, `currentSrc`, `naturalWidth`/`naturalHeight`, the `load` and `error` events, `srcset`/`sizes`/`<picture>` selection, and `img.decode()`, all from the container header
- Emulated viewport, media type, and supported preferences such as colour scheme, reduced motion, contrast, and pointer capabilities in both `matchMedia` and stylesheet `@media` rules
- `about:blank`, `data:text/html`, direct content, and HTTP(S) navigation
- Custom elements, shadow DOM, templates, ranges, traversal, selection, DOMParser, and XMLSerializer
- Geometry Interfaces: `DOMPoint`, `DOMRect`, `DOMQuad` and `DOMMatrix` with their read-only parents and `WebKitCSSMatrix`, including transform-list strings; layout answers such as `getBoundingClientRect` are `DOMRect` instances
- CSS Font Loading: `FontFace` from a `src` string or a buffer, with its descriptors parsed and serialized, `load()` fetching `url()` sources in order, and `document.fonts` as a set-like `FontFaceSet` with `check()`, `load()`, `ready` and the `loading`/`loadingdone`/`loadingerror` events
- Web Animations: `document.timeline`, `DocumentTimeline`, `Animation`, `KeyframeEffect`, `element.animate()` and element/document `getAnimations()`, with frame-driven playback, timing and easing calculations, ready/finished promises, playback events, replacement/persistence, and discrete `commitStyles()`
- The HTML Sanitizer API: `Sanitizer`, `setHTML`/`setHTMLUnsafe`, `getHTML` and `Document.parseHTML`/`parseHTMLUnsafe`, including declarative shadow roots (sanitizing runs after parsing, see [the DOM divergences](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/divergences.md#sanitizer-sanitize-after-parsing-not-while-parsing))

## Canvas (non-rendering)

- `HTMLCanvasElement.getContext('2d')` and `OffscreenCanvas.getContext('2d')` return stable, per-canvas contexts. Other supported context modes return null; no renderer or GPU is involved.
- Drawing state, validated colors/styles and line dashes, `save`/`restore`/`reset`, and 2D transforms with independent `DOMMatrix` results. Assigning width or height resets the drawing state and save stack, including same-value and content-attribute writes.
- `Path2D`, gradients, patterns, the 2D path/drawing operations, and approximate `TextMetrics` let scripts prepare charts without rasterization.
- Real `ImageData` with `Uint8ClampedArray` storage; canvas reads return transparent-black data and writes validate without retaining pixels.
- Valid transparent PNG data URLs and asynchronous PNG `Blob` results from `toBlob` and offscreen `convertToBlob`. See [Limitations](./limitations) for text heuristics, encoding/allocation limits and deliberate no-rendering behavior.

## SVG DOM (non-rendering)

- SVG 2 element brands and prototype chains for roots, groups, basic shapes, text, gradients, patterns, clips, masks, markers, images, links, scripts, views and filters. Unknown SVG names remain `SVGElement`; names are case-sensitive.
- Live animated attributes, lengths, angles, numbers, rectangles, aspect ratios, points and transform lists. Base-value edits serialize into content attributes; retained objects observe attribute changes. Animated values mirror the base values through read-only objects, without SMIL.
- SVG list inspection and mutation methods, constants on both interface objects and prototypes, root value factories, and `SVGMatrix`/`SVGPoint`/`SVGRect` aliases of the Geometry interfaces.
- Attribute-derived basic-shape bounding boxes, lengths and points along outlines, plus ancestor-transform `getCTM`/`getScreenCTM`. See [Limitations](./limitations) for the synthetic geometry and text policies.

## Runtime

- Timers, promises, microtasks, animation-frame callbacks, and `postMessage`
- `fetch`, `XMLHttpRequest`, WebSocket, EventSource, blobs, and FileReader
- Forms, validation, history, location, cookies, local/session storage
- IndexedDB databases, transactions, indexes and cursors, shared by same-origin pages and dedicated workers within a context, including plain HTTP. In-memory data survives navigation; `BrowserOptions.MaxIndexedDbBytes` limits committed storage to 50 MiB per origin by default. An additional `BrowserOptions.MaxTotalStorageBytes` budget defaults to 100 MiB per context across origins, pages and workers, counting committed IndexedDB plus built-in localStorage and live Cache Storage; `BrowserContextOptions.MaxTotalStorageBytes` can override it. Custom local/cache partitions, session storage, cookies, empty partition maps and peak allocations are excluded. See [IndexedDB](../../guide/web-apis/indexeddb.md).
- Origin-partitioned `caches` in secure contexts (HTTPS and HTTP loopback/localhost), shared by a context's pages and dedicated workers and retained across same-origin navigations. The default in-memory partition enforces `BrowserOptions.MaxCacheStorageBytes` per origin (5 MiB); custom `StoragePartitionProvider.GetCacheStorage` implementations can persist or refuse storage.
- Resource Timing for completed `fetch`/XHR bodies, scripts/modules, stylesheets/CSS imports, images, frame documents, and explicit font/media loads. Entries support `PerformanceObserver`, buffered replay, the 250-entry resource buffer, resizing, clearing, and `resourcetimingbufferfull`.
- Navigation Timing for top-level documents, including fetch/body and DOM/load milestones, navigation type and same-origin redirect counts. Same-document navigations keep the existing entry.
- `window.open` and anchor/area/form targets open or reuse pages in the same context, including form POST bodies, window names, `noopener`/`noreferrer`, opener relationships, script closing and cross-page structured-cloned `postMessage`. Remote WindowProxy objects support the cross-origin window/location surface; see the isolation limits below.
- Navigation API: lazy `window.navigation`, history entries with stable keys/IDs and independently structured-cloned state, `navigate`/`reload`/traversal result promises, SPA interception, transitions and activation. Native history/location changes, anchors, forms and CDP history traversals share the page's session history and navigation events; cross-document loads preserve same-origin contiguous entries. Host `Page.NavigateAsync`/`ReloadAsync` and CDP `Page.navigate`/`Page.reload` are browser-UI navigations: as in HTML they fire no `navigate` event and a page cannot intercept or cancel them, but a script navigation before they commit still aborts them.
- Cookie Store: `window.cookieStore` with promise-based `get`, `getAll`, `set`, `delete`, `onchange` and `CookieChangeEvent`, sharing the context jar with `document.cookie`, network responses and CDP
- Dedicated module workers and classic/module `SharedWorker`, including entangled `MessagePort` connections, `onconnect`, classic `importScripts`, and context-scoped sharing by constructor origin, script URL and name. See [Workers](../../guide/web-apis/workers.md) for lifetime and limits.
- Mutation, intersection, and resize observers, with documented no-layout semantics
- Constructible event interfaces beyond UI Events: `ToggleEvent` (fired by `<dialog>` and `<details>`), `CommandEvent`, `AnimationEvent`, `TransitionEvent`, `GamepadEvent`, `DragEvent`, `StorageEvent`, `TouchEvent` and the device events, plus the legacy `TextEvent` through `document.createEvent`; `ClipboardEvent` is absent
- The navigator's system state: HTML's compatibility constants, empty `plugins`/`mimeTypes`, `userAgentData` (UA Client Hints, following a CDP `userAgentMetadata` override), `permissions.query`, `storage.estimate`, `registerProtocolHandler` argument validation, `Notification`, `screen`/`screen.orientation` and `visualViewport` following the emulated viewport — see [Limitations](./limitations) for what they answer

## Automation and reading

- CSS selectors and accessibility-snapshot `ref=` targets
- Click, focus, typing, key presses, select controls, form submission, and virtual scrolling
- Markdown, text, serialized HTML, and accessibility snapshots
- Request, console, page-error, response, and dialog inspection
- `Page.Popup` announces registered popups off the page loop, and `Page.Opener` exposes their opener. Popups join `BrowserContext.Pages` and CDP target discovery/attachment, with `openerId` when an opener exists.
- CDP connections for supported Puppeteer and Playwright operations

Feature support is intentionally narrower than a graphical browser. Check [Limitations](./limitations) before depending on layout, frames, media, or full client compatibility.
