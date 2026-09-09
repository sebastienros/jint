# Limitations

`Jint.Browser` is not a rendering engine.

- No pixels, screenshots, PDF output, browser window, canvas rendering, WebGL, or media playback.
- No visual layout. Synthetic boxes are deterministic tree rows; text does not wrap and elements are never truly side by side.
- Geometry, hit testing, scrolling, intersection, and resize observations use that synthetic model.
- Images are not downloaded for display; their references are recorded in the request log.
- Child-frame documents can be fetched and parsed, but do not have a script realm. `contentWindow` is `null`.
- No IndexedDB, Cache Storage integration for page origins, WebAssembly, CSP enforcement, SharedWorker, or ServiceWorker.
- No drag and drop, clipboard API, touch event dispatch, or native input.
- No file picker: clicking an `<input type=file>` records that a page asked for one. `Page.SetInputFilesAsync` and `DOM.setFileInputFiles` make the selection instead.
- Hover dispatches movement but not mouse boundary events such as `mouseenter`.
- `contenteditable` support is intentionally limited; structural editing such as Enter-created blocks is absent.
- Isolated CDP worlds are aliases, not isolated realms.
- Playwright and Puppeteer compatibility covers supported public/client paths, not every browser or protocol feature.
- The package is not currently trim- or AOT-compatible.

Some CSS values come from AngleSharp's declared cascade rather than a computed layout. Stylesheet `@media` rules read the same emulated viewport, media type, and supported preferences as `matchMedia`, through AngleSharp.Css 1.1.0. Their evaluators still differ for negated conjunctions, boolean dimensions and colour features, malformed queries, and ordered gamut/dynamic-range preferences; see the [DOM divergence register](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/divergences.md).

Prefer feature detection and handle `NotSupportedException` or protocol errors. Never use synthetic geometry as evidence of real visual placement.
