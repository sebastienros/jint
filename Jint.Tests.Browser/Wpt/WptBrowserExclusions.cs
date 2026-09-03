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

        // ------------------------------------------------------------ not vendored, and the cause has gone
        // These four were harness errors because `document.createEvent` did not exist and each of them reaches
        // for it before a test could report. It exists now, so the reason these are not vendored is spent and
        // vendoring them is a change of its own: it moves the census's Documents and Tests columns, which the
        // change that fixes an engine deliberately does not. `keypress-dispatch-crash.html` needs one more
        // thing — `document.implementation.createDocument`, which AngleSharp's IImplementation does not have
        // at all (`createHTMLDocument` and `new Document()` are the two this package answers).
        ("dom/events/Event-constants.html", "not vendored: it called document.createEvent at file scope, which now exists"),
        ("dom/events/Event-propagation.html", "not vendored: it called document.createEvent at file scope, which now exists"),
        ("dom/events/Event-dispatch-detached-click.html", "not vendored: it called document.createEvent inside its one test, which now exists"),
        ("dom/events/keypress-dispatch-crash.html", "calls document.implementation.createDocument at file scope, which AngleSharp's IImplementation does not have"),

        // ------------------------------------------------------------ needs a name this browser does not have
        ("dom/events/Event-stopPropagation-cancel-bubbling.html", "not vendored: it read the legacy global `window.event`, which now exists"),
        ("dom/events/EventTarget-add-listener-platform-object.html", "defines a custom element: window.customElements is absent"),
        ("dom/events/Event-dispatch-click.html", "follows a `javascript:` URL 87 times; a page here loads http, https, about: and data:"),

        // ------------------------------------------------------------ needs a rendering
        ("dom/events/Event-dispatch-on-disabled-elements.html", "five of its nine tests wait for CSS transition and animation events on a disabled control, and nothing animates without a rendering, so the file never completes; its testdriver-driven test is the last one and is never reached"),
        ("dom/events/click-on-absolute-pseudo.html", "reads `event.pseudoTarget` and `element.pseudo('::after')`; there is no pseudo-element model without generated content, and the assertion throws out of the click listener into a harness ERROR"),
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
        ["dom/events/Body-FrameSet-Event-Handlers.html"] = 48,
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
        ["dom/events/Event-dispatch-redispatch.html"] = 4,
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
        ["dom/events/focus-event-document-move.html"] = 1,
        ["dom/events/handler-count.html"] = 2,
        ["dom/events/label-default-action.html"] = 1,
        ["dom/events/mouse-event-retarget.html"] = 1,
        ["dom/events/no-focus-events-at-clicking-editable-content-in-link.html"] = 2,
        ["dom/events/passive-by-default.html"] = 100,
        ["dom/events/pointer-event-document-move.html"] = 1,
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
    /// <b><see cref="WptDivergence.NeedsTriage"/> is five distinct things, not eleven rows.</b> The eleven
    /// defects this lane first recorded were filed as
    /// https://github.com/sebastienros/jint/issues/3686 to 3695 and are fixed; what is left is named in
    /// <c>Wpt/README.md</c>, one section per cause, and every one of them is bounded — a scheme a subresource
    /// cannot fetch, a member AngleSharp reflects wrong, an <c>@@unscopables</c> object the binding does not
    /// emit, a custom element, and a fragment navigation this lane's own timing does not wait for.
    /// </para>
    /// </remarks>
    internal static readonly WptExclusion[] All =
    [
        // ---------------------------------------------------------------- 1. an event interface this browser has not built
        // https://dom.spec.whatwg.org/#dom-document-createevent's alias table names five interfaces
        // Jint.Browser deliberately does not build, and this file is the only place a page meets them all at
        // once: it asks each of them for an uninitialized event and dispatches it. `createEvent` refuses the
        // alias with the NotSupportedError the standard gives one it does not list, which is what these four
        // rows say. See WptDivergence.NeedsMoreEventInterfaces.
        new("dom/events/EventTarget-dispatchEvent.html", "If the event's initialized flag is not set, an InvalidStateError must be thrown (DeviceMotionEvent).", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/events/EventTarget-dispatchEvent.html", "If the event's initialized flag is not set, an InvalidStateError must be thrown (DeviceOrientationEvent).", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/events/EventTarget-dispatchEvent.html", "If the event's initialized flag is not set, an InvalidStateError must be thrown (DragEvent).", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/events/EventTarget-dispatchEvent.html", "If the event's initialized flag is not set, an InvalidStateError must be thrown (StorageEvent).", WptDivergence.NeedsMoreEventInterfaces),

        // ---------------------------------------------------------------- 2. a `data:` URL subresource
        // A page navigates to a `data:` URL and cannot fetch one as a subresource, so a
        // `<script src="data:text/javascript,…">` is never run — which is what "ran expected true got false"
        // says here. The report site these documents are about works; what is missing is the scheme, and
        // adding it is `Runtime/SubresourceFetch`'s change rather than this one.
        new("html/webappapis/scripting/processing-model-2/compile-error-data-url.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-data-url.html", "*", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/body-onerror-compile-error-data-url.html", "<body onerror> - compile error in <script src=data:...>", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 3. `script.src` does not reflect a URL
        // HTML: the `src` IDL attribute of a `<script>` reflects the content attribute **as a URL**, so it
        // answers the resolved absolute URL. AngleSharp's `IHtmlScriptElement.Source` answers the raw
        // attribute value, so the four rows below compare the report's filename — which is correct, and
        // absolute — against the unresolved string the document wrote. It is AngleSharp's divergence and it
        // is recorded in `Jint.Browser/Dom/AGENTS.md`; working around it in the binding is the thing that
        // file says not to do.
        new("html/webappapis/scripting/processing-model-2/compile-error-same-origin.html", "window.onerror - compile error in <script src=...>", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/compile-error-same-origin-with-hash.html", "window.onerror - compile error in <script src=...> with hash", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-same-origin.html", "window.onerror - runtime error in <script src=...>", WptDivergence.NeedsTriage),
        new("html/webappapis/scripting/processing-model-2/runtime-error-same-origin-with-hash.html", "window.onerror - runtime error in <script src=...> with hash", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 4. a DOM prototype has no @@unscopables
        // WebIDL puts an `@@unscopables` object on the interface prototype object of every interface with an
        // `[Unscopable]` member — `Element`'s and `Document`'s `append`, `prepend` and `replaceChildren`
        // among them — and this binding emits none, because AngleSharp's metadata does not say which members
        // are unscopable. The three rows below never reach their subject: they *write* to
        // `document[Symbol.unscopables]`, which is undefined here.
        new("html/webappapis/scripting/events/compile-event-handler-symbol-unscopables.html", "*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 4b. a custom element
        // The file's other three rows pass. This one defines a form-associated custom element, and
        // `window.customElements` is a name this browser does not have — the same reason
        // `EventTarget-add-listener-platform-object.html` is not vendored.
        new("html/webappapis/scripting/events/compile-event-handler-lexical-scopes-form-owner.html", "form-associated <x-foo> has a form owner", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- 5. a frame that runs script
        // `eventhandler-cancellation.html` fires its events at `frames[0]`, which is an iframe's window; a
        // page here parses child frames and gives none of them an engine. It is the NeedsIframeScripting
        // group below by cause, and is here only because the file is in another suite.
        new("html/webappapis/scripting/events/eventhandler-cancellation.html", "*", WptDivergence.NeedsIframeScripting),

        // ---------------------------------------------------------------- 6. a bubbling `submit` the file counts as an activation
        // `Event-dispatch-single-activation-behavior.html` builds 132 nesting shapes and asserts that exactly
        // one activation behaviour runs. Its instrumentation is the *handler* — `<form onsubmit="activated(this)">`
        // — and for eight of the shapes that cannot tell an activation behaviour from an ordinary bubble:
        // the child form is a descendant of the parent form (the file appends it into the parent's `<input>`),
        // and https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#concept-form-submit
        // fires `submit` "with the bubbles and cancelable attributes initialized to true", as
        // https://html.spec.whatwg.org/multipage/forms.html#dom-form-reset does `reset`. So the parent's
        // handler runs because the child's event reached it, and no implementation may stop it.
        //
        // The shape of the eight says the same thing from the other side: they are exactly the pairs whose
        // two forms listen for the *same* event. A submitting child inside a resetting parent passes, because
        // the parent has no `onsubmit` for the bubble to find.
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><BUTTON type=reset></BUTTON></FORM> of parent <FORM><INPUT type=reset></INPUT></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><BUTTON type=submit></BUTTON></FORM> of parent <FORM><INPUT type=image></INPUT></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><BUTTON type=submit></BUTTON></FORM> of parent <FORM><INPUT type=submit></INPUT></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=image></INPUT></FORM> of parent <FORM><BUTTON type=submit></BUTTON></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=image></INPUT></FORM> of parent <FORM><INPUT type=submit></INPUT></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=reset></INPUT></FORM> of parent <FORM><BUTTON type=reset></BUTTON></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=submit></INPUT></FORM> of parent <FORM><BUTTON type=submit></BUTTON></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <FORM><INPUT type=submit></INPUT></FORM> of parent <FORM><INPUT type=image></INPUT></FORM>, only child should be activated.", WptDivergence.AssertsWhatNothingRequires),

        // ---------------------------------------------------------------- 7. a fragment navigation the file does not wait for
        // The same file's twenty-two `<a>`/`<area>` shapes click a link to a fragment of the page's own URL
        // and give it two zero-delay turns to produce a `hashchange`. The navigation happens — a unit test
        // measures it arriving on the *next* turn, and this change moved it there from after the whole timer
        // chain by keeping a same-document fragment move on the page loop instead of sending it round the
        // navigation gate — but in this document it still does not land inside the file's two turns. What is
        // left is a scheduling question about the page loop rather than anything about activation behaviour,
        // and it is the one row of #3693 this change does not retire.
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <A></A> of parent *", WptDivergence.NeedsTriage),
        new("dom/events/Event-dispatch-single-activation-behavior.html", "When clicking child <AREA></AREA> of parent *", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a frame that runs script
        // https://html.spec.whatwg.org/multipage/nav-history-apis.html#window: each of these needs a second
        // global with a document in it — a cross-realm listener, a `beforeunload` result coerced in the
        // frame's realm, an exception reported in the realm of the listener that threw. A page here parses
        // child frames and gives none of them an engine.
        //
        // Named access on the window is what these files used to meet first, and it is implemented now
        // (Runtime/WindowNamedProperties); the frame is what is left, and no fix short of one moves them.
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
        // one. Named access now carries it as far as the assertion, which is where no fix short of campaign
        // item C4's flat renderer moves it.
        new("dom/events/mouse-event-retarget.html", "*", WptDivergence.NeedsLayout),
    ];
}
