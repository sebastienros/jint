/*
 * The browser lane's `resources/testharnessreport.js`.
 *
 * Upstream ships a stub whose whole purpose is to be replaced — "this file is intended for vendors to
 * implement code needed to integrate testharness.js tests with their own test systems" — so it is the one
 * harness file `Jint.Tests/Wpt/Vendor/` deliberately does not hold. `WptServer` takes an overlay string per
 * instance and answers `/resources/testharnessreport.js` with it; this is what the browser lane passes, and
 * `Jint.Tests/Wpt/Prelude/testharnessreport.js` — which reproduces upstream's stub — is what every other
 * caller gets.
 *
 * Three things about it are worth knowing before editing.
 *
 * 1. IT POSTS STRINGS, NEVER VALUES. `__jintWptReport` is a CLR delegate the driver installs on every page
 *    engine, and everything it is handed is one JSON string. A page's engine and its DOM belong to that
 *    page's own thread (Jint.Browser/Runtime/AGENTS.md), so a `JsValue` handed to the driver would be a value
 *    belonging to a thread the driver is not on. One `JSON.stringify` here is what makes the boundary
 *    obvious rather than a rule somebody has to remember.
 *
 * 2. IT REMOVES ITS OWN GLOBAL, AND HIDES THE OTHER ONE. The report function is captured into this closure
 *    and then deleted from the global object, before the test file is fetched and long before any of it runs.
 *    A wpt test may enumerate `window`, and this lane's own plumbing must not be one of the names it finds.
 *    The driver installs a second hook, `__jintWptInput`, which belongs to `testdriver-vendor.js` — loaded
 *    later, and only by a document that drives input — so this file cannot delete it. What it does instead is
 *    take it off the *enumerable* surface, which is what that rule actually asks for; `testdriver-vendor.js`
 *    deletes it outright when it runs.
 *
 * 3. IT DOES NOT REPORT UNCAUGHT EXCEPTIONS, AND THAT IS UPSTREAM DOING IT. `testharness.js` registers its
 *    own `error` and `unhandledrejection` listeners at the global scope, and Jint fires both — see
 *    `Jint/WebApi/GlobalEvents/GlobalEventTarget.cs` — so an exception escaping a listener, a timer callback
 *    or a microtask becomes a harness ERROR (or the file's one test failing, or nothing at all under
 *    `setup({allow_uncaught_exception: true})`) by upstream's own code and at upstream's own rules. The
 *    `.any.js` lane synthesizes that rule in its driver because its shim has no global event target to
 *    listen at; here it would double-count.
 *
 * Upstream: resources/testharnessreport.js at the commit named in Jint.Tests/Wpt/Vendor/README.md.
 */

(function () {
    "use strict";

    var report = self.__jintWptReport;
    delete self.__jintWptReport;

    // The input hook, hidden rather than removed: see point 2 of the header.
    if (typeof self.__jintWptInput === "function") {
        var input = self.__jintWptInput;
        delete self.__jintWptInput;
        Object.defineProperty(self, "__jintWptInput", { value: input, configurable: true });
    }

    if (typeof report !== "function") {
        // Nothing to report to. A page loaded by something other than this driver — `common/blank.html`
        // framed by a test, say — gets upstream's stub behaviour, which in an engine with no `window.opener`
        // is nothing at all.
        return;
    }

    /*
     * The harness renders its results into `<div id=log>` unless a vendor says otherwise, and this is the
     * place the vendor says it: `Output.prototype.setup` reads the property with `this.enabled = this.enabled
     * && …` and comments in as many words that "if output is disabled in testharnessreport.js the test
     * shouldn't be able to override that", so one call here settles it for every file. wpt's own runner makes
     * the same call — through `window.opener.testharness_properties`, which is what upstream's stub exists to
     * read — because a driver that takes the results programmatically has no use for a rendering of them.
     *
     * It is not free of consequence and the consequence is worth stating: the renderer is a few hundred lines
     * of DOM building, and one of the members it reaches for, `Element.insertAdjacentText`, is not in this
     * package's bindings. With the renderer on, that threw out of the harness's own completion callback —
     * which is registered before this file's, so a throw there took every result with it and every document
     * in the lane timed out with no report. Turning the output off is the right call for its own reason, but
     * the missing member is real and is recorded in `Wpt/AGENTS.md` rather than left as a thing this line
     * quietly hides.
     */
    setup({ output: false });

    /*
     * One result per subtest, as `Test.statuses` numbers them: PASS 0, FAIL 1, TIMEOUT 2, NOTRUN 3,
     * PRECONDITION_FAILED 4. The name is what the driver's exclusion table matches against, so it is sent
     * exactly as the test carries it.
     */
    add_result_callback(function (test) {
        report(JSON.stringify({
            kind: "result",
            name: test.name,
            status: test.status,
            message: test.message === null || test.message === undefined ? "" : String(test.message),
            stack: test.stack === null || test.stack === undefined ? "" : String(test.stack)
        }));
    });

    /*
     * The file is over. `TestsStatus.statuses` numbers this one differently — OK 0, ERROR 1, TIMEOUT 2,
     * PRECONDITION_FAILED 3 — and anything but OK is a harness error covering the whole file, which is the
     * same unit of report the `.any.js` lane uses.
     *
     * `asserts_run` is deliberately not sent. It counts assertions rather than tests, it is only populated
     * when the harness is asked to record them, and the census counts what the driver was told about.
     */
    add_completion_callback(function (tests, status) {
        report(JSON.stringify({
            kind: "completion",
            status: status.status,
            message: status.message === null || status.message === undefined ? "" : String(status.message),
            stack: status.stack === null || status.stack === undefined ? "" : String(status.stack),
            count: tests.length
        }));
    });
})();
