# Limitations

`Jint.Browser.Playwright` is intentionally incomplete.

- It supports Microsoft.Playwright's public interfaces only. Features that downcast to Playwright's internal implementation types, including Playwright's built-in assertions, cannot use this provider.
- No renderer means no screenshots, PDFs, video, pixels, native windows, headed mode, browser extensions, or CDP sessions.
- No Node process or Playwright driver is started; APIs depending on either are unsupported.
- Locator support is CSS plus an initial role-locator implementation, not the complete selector engine.
- Accessible-name matching is partial.
- Locator resolution and actionability do not reproduce Playwright's full atomicity, stability, visibility, obstruction, and retry semantics.
- Only documented options are honored. Any other non-default option fails.
- Page events, routing, downloads, uploads, tracing, devices, permissions, geolocation, HAR, workers, and many other interfaces are not exposed by the direct adapter.
- Child frames are not scriptable through the adapter.
- Synthetic boxes are not visual layout and must not be used to assert placement.
- The proxy-based package is not AOT-compatible.

Unsupported async methods return faulted tasks; synchronous members throw `NotSupportedException`. This explicit failure is preferable to a silent no-op.
