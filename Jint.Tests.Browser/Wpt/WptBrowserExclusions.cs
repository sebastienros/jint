using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// The browser lane's three tables: what is deliberately not vendored, how many tests each case must at least
/// produce, and which tests do not pass and why.
/// </summary>
/// <remarks>
/// <para>
/// The vocabulary is the engine lane's — <see cref="WptExclusion"/> and <see cref="WptDivergence"/>, shared
/// through <c>InternalsVisibleTo</c> — because the two lanes run one corpus at one pin and a second set of
/// category names would be a second answer to the same question. Four categories exist for this lane alone
/// and say so on themselves: <see cref="WptDivergence.NeedsLayout"/>,
/// <see cref="WptDivergence.NeedsIframeScripting"/>, <see cref="WptDivergence.NeedsIndexedDb"/> and
/// <see cref="WptDivergence.NeedsTestDriver"/>.
/// </para>
/// <para>
/// The tables are separate from the runner because the runner is a driver and these are an inventory: what
/// changes when a fix lands, a document is vendored or the pin moves is here, and what enforces it is there.
/// <c>Wpt/README.md</c> is the same inventory in prose, with what each defect is.
/// </para>
/// </remarks>
internal static class WptBrowserExclusions
{
    /// <summary>
    /// Upstream documents this lane deliberately does not vendor, as globs over their path in the wpt tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Checked against what <i>is</i> vendored, so a re-vendor that pulls one back in without revisiting the
    /// reason fails rather than quietly adding a red document.
    /// </para>
    /// <para>
    /// <b>Almost every row here is a document that cannot produce a per-test report at all</b> — a harness
    /// <c>ERROR</c> or <c>TIMEOUT</c>, or a page the driver's own deadline had to end — which is what puts it
    /// here rather than in <see cref="All"/>: a harness error covers the whole file and no per-test exclusion
    /// can name it. The rest are the globs upstream's own markers and this lane's one-directory rule earn, and
    /// the helper files of documents nothing here runs. Where a reason is a defect rather than a missing
    /// environment, the same defect is also named in <see cref="All"/> by a document that <i>does</i> report,
    /// so nothing is only recorded here.
    /// </para>
    /// </remarks>
    internal static readonly (string Pattern, string Reason)[] NotVendored =
    [
        // ------------------------------------------------------------ dom/events: the whole-directory rules
        // A suite is one directory (WptCorpus.BrowserTestFiles never descends), and these two hold nothing
        // this browser could answer anyway: `scrolling/` is a scroll offset, a scrollend event and a wheel
        // transaction, and `non-cancelable-when-passive/` dispatches touch and wheel input at a rendered box.
        // Both are layout, and Jint.Browser renders nothing.
        ("dom/events/scrolling/*", "a scroll offset and a scrollend event, which need a rendering to scroll"),
        ("dom/events/non-cancelable-when-passive/*", "touch and wheel input dispatched at a rendered box"),

        // Not documents: a `.window.js` is a script wpt wraps in a generated `.window.html`, and this lane
        // generates only the `.any.html` wrapper — see WptServerWrappers for why the worker one is out. A
        // `.worker.js` is a classic worker's top-level script, which the engine lane's table already declines.
        ("dom/events/*.window.js", "a .window.js script, whose generated wrapper this lane does not synthesize"),
        ("dom/events/*.worker.js", "a classic worker's top-level script: importScripts at file scope"),
        ("html/webappapis/scripting/events/*.window.js", "a .window.js script, whose generated wrapper this lane does not synthesize"),
        ("html/webappapis/scripting/events/event-handler-processing-algorithm-error/*", "a frameset, a worker and a second global per file"),
        ("html/webappapis/scripting/processing-model-2/integration-*", "the agent formalism and the job queue: `.any.js` files and four documents that each drive an iframe"),
        ("html/webappapis/scripting/processing-model-2/unhandled-promise-rejections/*", "an iframe, a shared worker, a service worker and a wptserve `.py` handler; the object model itself is `dom/abort/` and `html/webappapis/microtask-queuing/` in the engine lane"),
        ("html/webappapis/scripting/event-loops/*", "not a suite this PR vendors; update-the-rendering, an iframe and a manual test"),

        // Upstream's own markers, declined for the reasons the engine lane's table gives them.
        ("dom/events/*.tentative.html", "tests a proposal the specification has not adopted"),
        ("html/webappapis/scripting/events/*.tentative.html", "tests a proposal the specification has not adopted"),
        ("*-manual.htm", "a manual test: it asks a human to do something"),
        ("*-manual.html", "a manual test: it asks a human to do something"),

        // One origin, so a `.sub.` document reads as same-origin and asserts nothing. The engine lane's table
        // says the same about `fetch/api/*/*.sub.any.js`, and Vendor/README.md's serving section argues it.
        ("dom/events/*.sub.html", "wptserve substitution into a *second* origin, which this server does not have"),

        // ------------------------------------------------------------ needs `document.createEvent` at file scope
        // The same defect the NeedsTriage rows below record, met before a test could be registered — so there
        // is no per-test result for it here and the file is a harness error. `Event-cancelBubble.html` and its
        // sixteen siblings name the defect from the other side, which is why nothing is lost by these rows.
        ("dom/events/Event-constants.html", "calls document.createEvent at file scope, which the bindings do not have"),
        ("dom/events/Event-propagation.html", "calls document.createEvent at file scope, which the bindings do not have"),
        ("dom/events/Event-dispatch-detached-click.html", "calls document.createEvent inside its one test, which never completes"),
        ("dom/events/keypress-dispatch-crash.html", "calls document.implementation.createDocument at file scope"),

        // ------------------------------------------------------------ needs a name this browser does not have
        ("dom/events/Event-stopPropagation-cancel-bubbling.html", "reads the legacy global `window.event`, which is a NeedsTriage row of event-global.html"),
        ("dom/events/EventTarget-add-listener-platform-object.html", "defines a custom element: window.customElements is absent"),
        ("dom/events/Event-dispatch-click.html", "follows a `javascript:` URL 87 times; a page here loads http, https, about: and data:"),

        // ------------------------------------------------------------ needs a rendering
        ("dom/events/webkit-animation-*-event.html", "waits for a CSS animation event; nothing animates without a rendering"),
        ("dom/events/webkit-transition-end-event.html", "waits for a CSS transition event; nothing animates without a rendering"),
        ("dom/events/EventListener-invoke-legacy.html", "waits for `animationend` and `transitionend`; nothing animates without a rendering"),
        ("dom/events/Event-timestamp-safe-resolution.html", "asserts that Event.timeStamp is *coarse*; PerformancePrototype records not coarsening as a deliberate divergence, which is why performance-timeline/webtiming-resolution.any.js is out of the engine lane too"),

        // ------------------------------------------------------------ one the corpus found and could not report
        // The one row that is a finding rather than a missing environment: the file's two tests wait for a
        // `focus` event that does not arrive, so it reports nothing and there is no per-test row to record it
        // under. What it would have asserted — that `relatedTarget` is retargeted to the shadow host — is
        // therefore untested here, which is why the reason says so rather than naming a category.
        ("dom/events/shadow-relatedTarget.html", "its two tests wait for a `focus` event that never reaches a capturing listener on the window, so the file reports nothing; focus retargeting across a shadow boundary is untested here as a result"),

        // ------------------------------------------------------------ needs testdriver.js (campaign item C4)
        // The harness file is vendored and upstream's testdriver-vendor.js is the empty hook a vendor
        // replaces, so every call rejects with "not implemented by testdriver-vendor.js" before a test can
        // report. Once C4 maps it onto InputDispatcher these become cases rather than rows.
        ("dom/events/click-on-absolute-pseudo.html", "drives input through testdriver.js, which is campaign item C4"),
        ("dom/events/focus-event-document-move.html", "drives input through testdriver.js, which is campaign item C4"),
        ("dom/events/handler-count.html", "drives input through testdriver.js, which is campaign item C4"),
        ("dom/events/no-focus-events-at-clicking-editable-content-in-link.html", "drives input through testdriver.js, which is campaign item C4"),
        ("dom/events/pointer-event-document-move.html", "drives input through testdriver.js, and reads getClientRects"),
        ("dom/events/Event-dispatch-on-disabled-elements.html", "drives input through testdriver.js, which is campaign item C4"),
        ("dom/events/Event-dispatch-redispatch.html", "drives input through testdriver.js, and reads getBoundingClientRect"),

        // ------------------------------------------------------------ needs a frame that runs script
        ("dom/events/Event-dispatch-throwing-multiple-globals.html", "needs a second global with a document in it"),
        ("dom/events/Event-timestamp-cross-realm-getter.html", "needs a second global with a document in it"),
        ("dom/events/replace-event-listener-null-browsing-context-crash.html", "a crash reproduction that removes an iframe mid-dispatch"),
        ("html/webappapis/scripting/events/compile-event-handler-settings-objects.html", "reads a handler compiled in an iframe's realm"),
        ("html/webappapis/scripting/events/onerroreventhandler.html", "drives an iframe"),
        ("html/webappapis/scripting/events/onerroreventhandler-frame.html", "the frame the file above loads; upstream keeps it beside the test rather than under resources/"),
        ("html/webappapis/scripting/events/resources/compiled-event-handler-settings-objects-support.html", "the iframe document of a test that is not vendored"),
        ("html/webappapis/scripting/events/resources/open-window.html", "the popup document of a test that is not vendored"),
        ("html/webappapis/scripting/events/resources/event-handler-body.js", "the helper of the idlharness-driven attribute tests below"),
        ("dom/events/resources/event-global-extra-frame.html", "the frame of event-global-extra.window.js, which is a .window.js"),
        ("dom/events/resources/large-dimension-document.sub.html", "a `.sub.` document, for a scrolling test that is not vendored"),

        // ------------------------------------------------------------ needs the WebIDL conformance harness
        // Every one of these opens with `idl_test([...])`, which is /resources/idlharness.js and
        // /resources/WebIDLParser.js — the framework the engine lane declines for the same reason. The failure
        // is `WebIDL2 is not defined` before any of them registers a test.
        ("html/webappapis/scripting/events/event-handler-all-global-events.html", "needs the WebIDL conformance harness"),
        ("html/webappapis/scripting/events/event-handler-attributes-*.html", "needs the WebIDL conformance harness"),

        // ------------------------------------------------------------ needs the timer's string handler
        // `setTimeout("{", 10)`, which TimerFunctions documents declining: compiling the string is `eval` by
        // another name and reachable even where a host disabled string compilation, so it is a TypeError here
        // as it is in Node. The engine lane declines html/webappapis/timers/evil-spec-example.any.js for
        // exactly this, and these four use the form to *raise* the error they are about.
        ("html/webappapis/scripting/processing-model-2/compile-error-in-set*.html", "setTimeout's string handler, which TimerFunctions documents declining"),
        ("html/webappapis/scripting/processing-model-2/runtime-error-in-set*.html", "setTimeout's string handler, which TimerFunctions documents declining"),

        // ------------------------------------------------------------ needs a second origin
        // `location.href.replace('://', '://www1.')` — a host this server is not, so the script never loads
        // and the file's whole subject, the muted "Script error." a cross-origin script reports, cannot arise.
        ("html/webappapis/scripting/processing-model-2/*-cross-origin*.html", "builds a second origin out of its own URL, and there is one origin here"),

        // ------------------------------------------------------------ helpers of documents that are not vendored
        ("html/webappapis/scripting/processing-model-2/support/*-in-set*.js", "the bodies of the string-handler tests above"),
        ("dom/events/resources/prefixed-animation-event-tests.js", "the body of the prefixed animation tests above"),
    ];

    /// <summary>
    /// How many tests each case must at least report, so a document that quietly stopped registering fails
    /// rather than passing with nothing in it.
    /// </summary>
    /// <remarks>
    /// Exact counts rather than floors, because every one of them was measured: a document registers its cases
    /// as its scripts run, and one that registers fewer has met something the driver should hear about.
    /// <c>EventTarget-dispatchEvent.html</c> is the reason this is not decorative — it reported <b>one</b> of
    /// its twenty-five until <c>dom/nodes/Document-createEvent.js</c>, the helper it loads by absolute path,
    /// was vendored beside it.
    /// </remarks>
    internal static readonly Dictionary<string, int> MinimumTests = new(StringComparer.Ordinal)
    {
        ["dom/events/AddEventListenerOptions-once.any.html"] = 4,
        ["dom/events/AddEventListenerOptions-passive.any.html"] = 5,
        ["dom/events/AddEventListenerOptions-signal.any.html"] = 11,
        ["dom/events/Body-FrameSet-Event-Handlers.html"] = 5,
        ["dom/events/CustomEvent.html"] = 3,
        ["dom/events/Event-cancelBubble.html"] = 8,
        ["dom/events/Event-constructors.any.html"] = 14,
        ["dom/events/Event-defaultPrevented-after-dispatch.html"] = 2,
        ["dom/events/Event-defaultPrevented.html"] = 8,
        ["dom/events/Event-dispatch-bubble-canceled.html"] = 1,
        ["dom/events/Event-dispatch-bubbles-false.html"] = 5,
        ["dom/events/Event-dispatch-bubbles-true.html"] = 5,
        ["dom/events/Event-dispatch-detached-input-and-change.html"] = 12,
        ["dom/events/Event-dispatch-handlers-changed.html"] = 1,
        ["dom/events/Event-dispatch-multiple-cancelBubble.html"] = 1,
        ["dom/events/Event-dispatch-multiple-stopPropagation.html"] = 1,
        ["dom/events/Event-dispatch-omitted-capture.html"] = 1,
        ["dom/events/Event-dispatch-order-at-target.html"] = 1,
        ["dom/events/Event-dispatch-order.html"] = 1,
        ["dom/events/Event-dispatch-other-document.html"] = 1,
        ["dom/events/Event-dispatch-propagation-stopped.html"] = 1,
        ["dom/events/Event-dispatch-reenter.html"] = 1,
        ["dom/events/Event-dispatch-single-activation-behavior.html"] = 132,
        ["dom/events/Event-dispatch-target-moved.html"] = 1,
        ["dom/events/Event-dispatch-target-removed.html"] = 1,
        ["dom/events/Event-dispatch-throwing.html"] = 2,
        ["dom/events/Event-init-while-dispatching.html"] = 5,
        ["dom/events/Event-initEvent.html"] = 12,
        ["dom/events/Event-isTrusted.any.html"] = 1,
        ["dom/events/Event-returnValue.html"] = 7,
        ["dom/events/Event-stopImmediatePropagation.html"] = 1,
        ["dom/events/Event-subclasses-constructors.html"] = 49,
        ["dom/events/Event-timestamp-high-resolution.html"] = 4,
        ["dom/events/Event-type-empty.html"] = 2,
        ["dom/events/Event-type.html"] = 3,
        ["dom/events/EventListener-handleEvent-cross-realm.html"] = 5,
        ["dom/events/EventListener-handleEvent.html"] = 6,
        ["dom/events/EventListenerOptions-capture.html"] = 4,
        ["dom/events/EventTarget-add-remove-listener.any.html"] = 1,
        ["dom/events/EventTarget-addEventListener.any.html"] = 1,
        ["dom/events/EventTarget-constructible.any.html"] = 3,
        ["dom/events/EventTarget-dispatchEvent-returnvalue.html"] = 2,
        ["dom/events/EventTarget-dispatchEvent.html"] = 25,
        ["dom/events/EventTarget-removeEventListener.any.html"] = 1,
        ["dom/events/EventTarget-this-of-listener.html"] = 6,
        ["dom/events/KeyEvent-initKeyEvent.html"] = 3,
        ["dom/events/event-disabled-dynamic.html"] = 1,
        ["dom/events/event-global-is-still-set-when-coercing-beforeunload-result.html"] = 1,
        ["dom/events/event-global-is-still-set-when-reporting-exception-onerror.html"] = 1,
        ["dom/events/event-global.html"] = 8,
        ["dom/events/event-handler-attribute-replace-preserves-passive.html"] = 2,
        ["dom/events/event-src-element-nullable.html"] = 1,
        ["dom/events/label-default-action.html"] = 1,
        ["dom/events/mouse-event-retarget.html"] = 1,
        ["dom/events/passive-by-default.html"] = 100,
        ["dom/events/preventDefault-during-activation-behavior.html"] = 1,
        ["dom/events/remove-all-listeners.html"] = 2,
        ["dom/events/window-composed-path.html"] = 1,
        ["dom/events/window-event-restored-after-throwing-onerror.html"] = 1,
        ["html/webappapis/scripting/events/body-onload.html"] = 1,
        ["html/webappapis/scripting/events/compile-event-handler-lexical-scopes-form-owner.html"] = 4,
        ["html/webappapis/scripting/events/compile-event-handler-symbol-unscopables.html"] = 3,
        ["html/webappapis/scripting/events/event-handler-handleEvent-ignored.html"] = 2,
        ["html/webappapis/scripting/events/event-handler-javascript.html"] = 1,
        ["html/webappapis/scripting/events/event-handler-non-content-document-idl-attributes.html"] = 6,
        ["html/webappapis/scripting/events/event-handler-onresize.html"] = 3,
        ["html/webappapis/scripting/events/event-handler-processing-algorithm.html"] = 7,
        ["html/webappapis/scripting/events/event-handler-sourcetext.html"] = 5,
        ["html/webappapis/scripting/events/eventhandler-cancellation.html"] = 1,
        ["html/webappapis/scripting/events/inline-event-handler-ordering.html"] = 3,
        ["html/webappapis/scripting/events/uncompiled_event_handler_with_scripting_disabled.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/addEventListener.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/body-onerror-compile-error-data-url.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/body-onerror-compile-error.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/body-onerror-runtime-error.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/compile-error-data-url.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/compile-error-in-attribute.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/compile-error-in-body-onerror.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/compile-error-same-origin-with-hash.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/compile-error-same-origin.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/compile-error.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/runtime-error-data-url.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/runtime-error-in-attribute.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/runtime-error-in-body-onerror.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/runtime-error-in-window-onerror.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/runtime-error-same-origin-with-hash.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/runtime-error-same-origin.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/runtime-error.html"] = 2,
        ["html/webappapis/scripting/processing-model-2/window-onerror-parse-error.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/window-onerror-runtime-error-throw.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/window-onerror-runtime-error.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-1.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-2.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-3.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-4.html"] = 1,
        ["html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-5.html"] = 1,
    };

    /// <summary>
    /// Every test that does not pass, with the category it belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry must match at least one failing test and no passing one, so a fix, a rename or a corpus bump
    /// makes the run fail until this table is brought back in line — which is what makes a <c>*</c> safe to
    /// write. A file whose every test fails for one cause is one row; a file where some pass is named test by
    /// test, or by a glob over a family the file generates.
    /// </para>
    /// <para>
    /// <b><see cref="WptDivergence.NeedsTriage"/> is eleven distinct defects, not eighty rows.</b>
    /// <c>Wpt/README.md</c> has one section per defect with what it is and which rows it earns; they are what
    /// this PR owes the engine and the package, recorded rather than fixed so that the change which first ran
    /// these suites is not also the change that moved them.
    /// </para>
    /// </remarks>
    internal static readonly WptExclusion[] All =
    [
        // ---------------------------------------------------------------- 1. document.createEvent
        // https://dom.spec.whatwg.org/#dom-document-createevent, the legacy creation surface DOM still
        // requires: `document.createEvent(alias)` then `initEvent`. Nothing in the bindings has it, and half
        // this corpus is written against it because it predates the constructors. `new Document()` and
        // `document.implementation.createHTMLDocument()` are the same section of the same gap — the two
        // documents that reach for one are Event-dispatch-bubbles-{true,false}.
        new("dom/events/CustomEvent.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-cancelBubble.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-defaultPrevented-after-dispatch.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-defaultPrevented.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-bubble-canceled.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-bubbles-false.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-bubbles-true.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-handlers-changed.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-multiple-cancelBubble.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-multiple-stopPropagation.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-omitted-capture.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-propagation-stopped.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-reenter.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-target-moved.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-target-removed.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/Event-initEvent.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/EventTarget-dispatchEvent-returnvalue.html", "*", WptDivergence.NeedsTriage),
        new("dom/events/EventTarget-dispatchEvent.html", "If the event's initialized flag is not set, an InvalidStateError must be thrown (*", WptDivergence.NeedsTriage),
        new("dom/events/EventTarget-dispatchEvent.html", "If the event's dispatch flag is set, an InvalidStateError must be thrown.", WptDivergence.NeedsTriage),
        new("dom/events/EventTarget-dispatchEvent.html", "Exceptions from event listeners must not be propagated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-returnValue.html", "initEvent should unset returnValue.", WptDivergence.NeedsTriage),
        new("dom/events/Event-type-empty.html", "initEvent", WptDivergence.NeedsTriage),
        new("dom/events/Event-type.html", "Event.type should initially be the empty string", WptDivergence.NeedsTriage),
        new("dom/events/Event-type.html", "Event.type should be initialized by initEvent", WptDivergence.NeedsTriage),
        new("dom/events/KeyEvent-initKeyEvent.html", "KeyboardEvent.initKeyEvent shouldn't be defined (created by createEvent(\"KeyboardEvent\")", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/compile-error-in-attribute.html", "window.onerror - compile error in attribute", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-in-attribute.html", "window.onerror - runtime error in attribute", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 3. a script's exception is not reported
        // https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception. An exception escaping a
        // classic `<script>` — a parse error, a runtime error, an external script's either — must be reported
        // at the global scope, which fires `error` and reaches `window.onerror` and `<body onerror>`. Here it
        // becomes a PageErrorKind.ScriptError on the page's recorder and nothing else: every one of these
        // documents fails on `assert_true: ran expected true got false`. The engine fires that event for a
        // timer callback, a listener and a microtask (GlobalEventTarget), so what is missing is the parser
        // driver's own report and not the mechanism.
        new("html/webappapis/scripting/processing-model-2/addEventListener.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/compile-error.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/compile-error-data-url.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/compile-error-same-origin.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/compile-error-same-origin-with-hash.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-data-url.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-same-origin.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-same-origin-with-hash.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-in-body-onerror.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-in-window-onerror.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/window-onerror-parse-error.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/window-onerror-runtime-error.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/window-onerror-runtime-error-throw.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/body-onerror-compile-error.html", "<body onerror> - compile error in <script>", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/body-onerror-compile-error-data-url.html", "<body onerror> - compile error in <script src=data:...>", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/body-onerror-runtime-error.html", "<body onerror> - runtime error in <script>", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 4. the error event carries no column
        // https://html.spec.whatwg.org/multipage/webappapis.html#erroreventinit: `colno`. The five-argument
        // `onerror` receives `undefined` where a number is owed. These two rows are the only ones that can
        // say so, because every other document that would asks it after the report of defect 3 that never
        // arrives.
        new("html/webappapis/scripting/processing-model-2/compile-error-in-attribute.html", "window.onerror - compile error in attribute (column)", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-in-attribute.html", "window.onerror - runtime error in attribute (column)", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 5. the compiled handler is not HTML's
        // https://html.spec.whatwg.org/multipage/webappapis.html#getting-the-current-value-of-the-event-handler.
        // The function a handler content attribute compiles to must be named for the attribute and have the
        // attribute's text as its body: `function onclick(event) {\nfoo\n}`. It is
        // `function anonymous(event\n) {\nwith (document) …`, which is the scope chain leaking into the
        // source text. `event-handler-sourcetext` asserts the text; the unscopables and cancellation
        // documents assert what the chain does; `-non-content-document-idl-attributes` asserts which members
        // are handlers at all; `inline-event-handler-ordering` asserts that an invalid one keeps its slot;
        // `-lexical-scopes-form-owner` asserts the form owner is in the chain (and needs custom elements for
        // its fourth test); and `uncompiled_event_handler_with_scripting_disabled` asserts that a document
        // with scripting disabled compiles none of them.
        new("html/webappapis/scripting/events/event-handler-sourcetext.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/events/event-handler-non-content-document-idl-attributes.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/events/inline-event-handler-ordering.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/events/compile-event-handler-symbol-unscopables.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/events/eventhandler-cancellation.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/events/uncompiled_event_handler_with_scripting_disabled.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/events/compile-event-handler-lexical-scopes-form-owner.html", "*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 6. two names HTML gives a body and a frameset
        // `<body>`'s handler attributes that HTML redirects to the Window are reflected as an object rather
        // than a function, and `HTMLFrameSetElement` — which owns the other half of that table — is not an
        // interface object at all.
        new("dom/events/Body-FrameSet-Event-Handlers.html", "Forward HTMLBodyElement.onblur to Window", WptDivergence.NeedsTriage),
        new("dom/events/Body-FrameSet-Event-Handlers.html", "Set HTMLFrameSetElement.onblur", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 7. the legacy init methods of the UI events
        // `initUIEvent`, `initMouseEvent` and `initKeyboardEvent`: deprecated, still in the UI Events
        // specification, and absent here. Beside them, `new UIEvent(type, {view: notAWindow})` must throw a
        // TypeError and does not.
        new("dom/events/Event-init-while-dispatching.html", "Calling initKeyboardEvent while dispatching.", WptDivergence.NeedsTriage),
        new("dom/events/Event-init-while-dispatching.html", "Calling initMouseEvent while dispatching.", WptDivergence.NeedsTriage),
        new("dom/events/Event-init-while-dispatching.html", "Calling initUIEvent while dispatching.", WptDivergence.NeedsTriage),
        new("dom/events/Event-subclasses-constructors.html", "UIEvent constructor (view argument with wrong type)", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 9. one click, one activation behaviour
        // https://dom.spec.whatwg.org/#eventtarget-activation-behavior: a dispatch runs the activation
        // behaviour of *one* element — the nearest ancestor in the event path that has one — and these rows
        // are the two ways that goes wrong here. A nested `<a>` or `<area>` records nothing at all, because
        // following a hyperlink to a fragment of the page's own URL is not what this activation host does;
        // and a `<form>` nested in a `<form>` submits **both**, because the walk does not stop at the first
        // behaviour it finds. 108 of the file's 132 shapes are right, which is what makes the two wrong ones
        // worth naming.
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <A></A> of parent *", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <AREA></AREA> of parent *", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <LABEL><INPUT type=checkbox></INPUT><SPAN></SPAN></LABEL> of parent *", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><BUTTON type=reset></BUTTON></FORM> of parent <FORM><INPUT type=reset></INPUT></FORM>, only child should be activated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><BUTTON type=submit></BUTTON></FORM> of parent <FORM><INPUT type=image></INPUT></FORM>, only child should be activated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><BUTTON type=submit></BUTTON></FORM> of parent <FORM><INPUT type=submit></INPUT></FORM>, only child should be activated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=image></INPUT></FORM> of parent <FORM><BUTTON type=submit></BUTTON></FORM>, only child should be activated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=image></INPUT></FORM> of parent <FORM><INPUT type=submit></INPUT></FORM>, only child should be activated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=reset></INPUT></FORM> of parent <FORM><BUTTON type=reset></BUTTON></FORM>, only child should be activated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=submit></INPUT></FORM> of parent <FORM><BUTTON type=submit></BUTTON></FORM>, only child should be activated.", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=submit></INPUT></FORM> of parent <FORM><INPUT type=image></INPUT></FORM>, only child should be activated.", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a frame that runs script
        // https://html.spec.whatwg.org/multipage/nav-history-apis.html#window: each of these needs a second
        // global with a document in it — a cross-realm listener, a `beforeunload` result coerced in the
        // frame's realm, an exception reported in the realm of the listener that threw. A page here parses
        // child frames and gives none of them an engine.
        //
        // The eleventh defect this lane found is visible in every one of them and is *not* what puts them
        // here: `window` has no named properties, so `<iframe name=x>` and `<div id=x>` do not reach script
        // as `x`. That is https://html.spec.whatwg.org/multipage/nav-history-apis.html#named-access-on-the-window-object,
        // and it is what these files meet first — but a fix for it would leave them needing the frame.
        new("dom/events/EventListener-handleEvent-cross-realm.html", "*", WptDivergence.NeedsIframeScripting),
        new("dom/events/event-global-is-still-set-when-coercing-beforeunload-result.html", "*", WptDivergence.NeedsIframeScripting),
        new("dom/events/event-global-is-still-set-when-reporting-exception-onerror.html", "*", WptDivergence.NeedsIframeScripting),
        new("html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-1.html", "*", WptDivergence.NeedsIframeScripting),
        new("html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-2.html", "*", WptDivergence.NeedsIframeScripting),
        new("html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-3.html", "*", WptDivergence.NeedsIframeScripting),
        new("html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-4.html", "*", WptDivergence.NeedsIframeScripting),
        new("html/webappapis/scripting/processing-model-2/window-onerror-with-cross-frame-event-listeners-5.html", "*", WptDivergence.NeedsIframeScripting),

        // ---------------------------------------------------------------- a rendering
        // `MouseEvent.offsetX` against a `body { margin: 8px }`, which is a used value and not a computed
        // one. It reaches the assertion by way of named access, so today it fails on that instead — but no
        // fix short of campaign item C4's flat renderer moves the row.
        new("dom/events/mouse-event-retarget.html", "*", WptDivergence.NeedsLayout),
    ];
}
