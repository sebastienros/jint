/*
 * Jint's `resources/testharnessreport.js`.
 *
 * This is *not* a vendored file, and it is deliberately the one harness file that is not. Upstream ships a
 * stub whose whole purpose is to be replaced: "this file is intended for vendors to implement code needed to
 * integrate testharness.js tests with their own test systems". Vendoring the stub and then overwriting it at
 * serve time would make `Vendor/` hold bytes the server never sends, which is the one thing the vendored-and-
 * byte-verified model is for. So the stub's behaviour is reproduced here instead, in Jint's own file, and
 * `Vendor/resources/` holds only the files the server really serves verbatim.
 *
 * The browser lane overlays this. `WptServer` takes an overlay string per instance and answers
 * `/resources/testharnessreport.js` with it, so the lane can install `add_result_callback` /
 * `add_completion_callback` handlers that post a page's results back to the driver without touching either
 * `Vendor/` or this file. Until that lane arrives nothing supplies an overlay, and what a page gets is what
 * upstream's stub does — which, in an engine with no `window.opener`, is nothing at all.
 *
 * Upstream: resources/testharnessreport.js at the commit named in Vendor/README.md.
 */

/* global setup */

/* If the parent window has a testharness_properties object, we use this to provide the test settings. This is
 * used by the default in-browser runner to configure the timeout and the rendering of results. There is no
 * opener here, so the guard is always false and the try/catch is what upstream's is: the property access
 * itself can throw across a browsing-context boundary.
 */
try {
    if (window.opener && "testharness_properties" in window.opener) {
        setup(JSON.parse(JSON.stringify(window.opener.testharness_properties)));
    }
} catch (e) {
}
