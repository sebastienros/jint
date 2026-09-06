# Browser Compatibility

`Jint.Browser` combines AngleSharp's HTML, DOM, and CSSOM with Jint's JavaScript runtime.

## Measured conformance

The gated in-process browser corpus passes **9,691 of 11,541 web-platform-tests (84.0%)** across 341 documents
and nine synthesized wrappers. This is the repository's selected vendored corpus, not a whole-browser score.
The separate nightly
[upstream WPT scoreboard](https://github.com/sebastienros/jint/blob/wpt-scoreboard/docs/wpt-scoreboard.md)
runs a broader set with the upstream runner and is informational rather than a merge gate.

## Included

- HTML parsing and script execution;
- DOM and CSSOM bindings;
- navigation, history, forms, cookies, and storage;
- fetch, XHR, WebSocket, EventSource, and workers;
- deterministic input dispatch;
- accessibility, text, HTML, and Markdown extraction;
- Chrome DevTools Protocol integration.

## Not included

- layout and rendering;
- screenshots and PDF;
- canvas, WebGL, media, or browser extensions;
- WebAssembly or IndexedDB;
- full iframe scripting;
- pixel-accurate input or layout-dependent actionability.

Feature detection remains honest: unsupported protocol and API operations fail rather than silently succeeding.
See [Supported Features](../packages/jint-browser/supported-features.md) and
[Limitations](../packages/jint-browser/limitations.md).
