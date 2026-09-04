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
/// category names would be a second answer to the same question. Five categories exist for this lane alone
/// and say so on themselves: <see cref="WptDivergence.NeedsLayout"/>,
/// <see cref="WptDivergence.NeedsIframeScripting"/>, <see cref="WptDivergence.NeedsIndexedDb"/>,
/// <see cref="WptDivergence.NeedsTestDriver"/> and <see cref="WptDivergence.NeedsXmlDocuments"/>.
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
        // change that fixes an engine deliberately does not. `keypress-dispatch-crash.html` needed one more
        // thing — `document.implementation.createDocument`, which AngleSharp's IImplementation does not have
        // at all — and that is answered too now, from `additions` rather than from AngleSharp.
        ("dom/events/Event-constants.html", "not vendored: it called document.createEvent at file scope, which now exists"),
        ("dom/events/Event-propagation.html", "not vendored: it called document.createEvent at file scope, which now exists"),
        ("dom/events/Event-dispatch-detached-click.html", "not vendored: it called document.createEvent inside its one test, which now exists"),
        ("dom/events/keypress-dispatch-crash.html", "not vendored: it called document.implementation.createDocument at file scope, which now exists"),

        // ------------------------------------------------------------ needs a name this browser does not have
        ("dom/events/Event-stopPropagation-cancel-bubbling.html", "not vendored: it read the legacy global `window.event`, which now exists"),
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

        // ============================================================ custom-elements
        // The corpus of HTML §4.13, and the shape of what is missing from it is one sentence: **most of it
        // is written against a second global**. `resources/custom-elements-helpers.js` gives it
        // `create_window_in_test`, which loads an iframe and resolves with its window, and `document_types()`,
        // which walks the current document, `new Document()`, `createHTMLDocument()`, an iframe's document and
        // an XHR-fetched one. These were listed when a frame had neither a document nor a window, so a file
        // built on either waited for a load that never came and the harness reported TIMEOUT — a whole-file
        // error no per-test exclusion can name. #3771 gave a frame both, and `create_window_in_test` resolves
        // now (`ChildFrameTests` runs the helper's own shape), so what is missing is the narrower half: the
        // window's constructors are the page's, because a frame shares the page's realm. **Each of these rows
        // is therefore owed a re-derivation**, which takes vendoring the document — that moves the census's
        // Documents and Tests columns and is a change of its own.

        // ------------------------------------------------------------ the whole-directory and marker rules
        ("custom-elements/form-associated/*", "ElementInternals and form association, which this package has no ElementInternals for"),
        ("custom-elements/registries/*", "scoped custom element registries, a second registry per shadow root and per element"),
        ("custom-elements/state/*", "CustomStateSet and its `:state()` selector, which needs a selector engine that knows about it"),
        ("custom-elements/htmlconstructor/*", "both documents build their subject in an iframe; with those out the directory holds nothing"),
        ("custom-elements/reactions/customized-builtins/*", "a directory this PR does not vendor"),
        ("custom-elements/*.tentative.html", "tests a proposal the specification has not adopted"),
        ("custom-elements/reactions/*.tentative.html", "tests a proposal the specification has not adopted"),
        ("custom-elements/*.window.js", "a .window.js script, whose generated wrapper this lane does not synthesize"),
        ("custom-elements/*.xhtml", "an XML document; the server serves this corpus as text/html and AngleSharp parses the page as HTML"),
        ("custom-elements/parser/*.xhtml", "an XML document, for the same reason"),
        ("custom-elements/*.svg", "the SVG document a test frames, not a test"),
        ("custom-elements/parser/*.svg", "the SVG document a test frames, not a test"),

        // ------------------------------------------------------------ needs a frame that runs script
        ("custom-elements/Document-createElement.html", "document_types(): every assertion is made in five documents, one of them an iframe's"),
        ("custom-elements/Document-createElement-customized-builtins.html", "document_types(): the same five documents"),
        ("custom-elements/adopted-callback.html", "adopts nodes between an iframe's document and this one"),
        ("custom-elements/append-children-to-new-parent-cycle.html", "builds its cycle in a second window"),
        ("custom-elements/connected-callbacks.html", "document_types(): the same five documents"),
        ("custom-elements/connected-callbacks-html-fragment-parsing.html", "parses its fragments in a second window"),
        ("custom-elements/cross-realm-callback-report-exception.html", "a callback whose realm is an iframe's"),
        ("custom-elements/custom-element-reaction-queue.html", "create_window_in_test"),
        ("custom-elements/disconnected-callbacks.html", "document_types(): the same five documents"),
        ("custom-elements/enqueue-custom-element-callback-reactions-inside-another-callback.html", "create_window_in_test"),
        ("custom-elements/perform-microtask-checkpoint-before-construction.html", "create_window_in_test"),
        ("custom-elements/pseudo-class-defined.html", "create_window_in_test"),
        ("custom-elements/pseudo-class-defined-customized-builtins.html", "create_window_in_test"),
        ("custom-elements/throw-on-dynamic-markup-insertion-counter-construct.html", "create_window_in_test"),
        ("custom-elements/throw-on-dynamic-markup-insertion-counter-reactions.html", "create_window_in_test"),
        ("custom-elements/upgrading.html", "document_types(): the same five documents"),
        ("custom-elements/parser/parser-uses-registry-of-owner-document.html", "parses into a document an iframe owns"),
        ("custom-elements/reactions/Document.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLAnchorElement.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLOptionElement.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLOptionsCollection.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLOutputElement.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLSelectElement.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLTableElement.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLTableRowElement.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLTableSectionElement.html", "create_window_in_test"),
        ("custom-elements/reactions/HTMLTitleElement.html", "create_window_in_test"),
        ("custom-elements/reactions/NamedNodeMap.html", "create_window_in_test"),
        ("custom-elements/reactions/Range.html", "create_window_in_test"),
        ("custom-elements/reactions/ShadowRoot.html", "create_window_in_test"),
        ("custom-elements/reactions/with-exceptions.html", "create_window_in_test"),
        ("custom-elements/upgrading/Document-importNode.html", "imports from an iframe's document"),
        ("custom-elements/upgrading/Document-importNode-customized-builtins.html", "imports from an iframe's document"),
        ("custom-elements/upgrading/Node-cloneNode.html", "clones into an iframe's document"),
        ("custom-elements/upgrading/upgrade-custom-element-error-event.html", "create_window_in_test"),
        ("custom-elements/upgrading/upgrading-enqueue-reactions.html", "create_window_in_test"),

        // ------------------------------------------------------------ needs ElementInternals
        // `attachInternals()` is the whole subject of these, and this package has no ElementInternals: a
        // form-associated custom element records the flag and takes part in no entry list. The reference is
        // at file scope in each, so none of them registers a test.
        ("custom-elements/HTMLElement-attachInternals.html", "attachInternals, which this package does not have"),
        ("custom-elements/ElementInternals-accessibility.html", "attachInternals, which this package does not have"),
        ("custom-elements/ElementInternals-role.html", "attachInternals, and get_computed_role is testdriver.js"),
        ("custom-elements/element-internals-aria-element-reflection.html", "attachInternals, which this package does not have"),
        ("custom-elements/element-internals-shadowroot.html", "attachInternals, which this package does not have"),

        // ------------------------------------------------------------ not a testharness document at all
        // A crash test and a print reftest: neither loads testharness.js, so neither can report anything and
        // the driver's own deadline is what ends them.
        ("custom-elements/prevent-extensions-crash.html", "a crash test: it loads no harness and asserts nothing"),
        ("custom-elements/when-defined-reentry-crash.html", "a crash test: it loads no harness and asserts nothing"),
        ("custom-elements/pseudo-class-defined-print.html", "a print reftest, which needs a rendering to compare"),
        ("custom-elements/pseudo-class-defined-print-ref.html", "the reference of the reftest above"),

        // ------------------------------------------------------------ one finding, one whole-file error
        // `CustomElementRegistry.html` is the corpus's largest file and it reaches `customElements.whenDefined`
        // with an invalid name, which HTML makes a rejected promise. The file attaches a handler on the very
        // next line, so a browser raises nothing — and Jint used to report HostPromiseRejectionTracker at the
        // tracker's own cadence rather than at HTML's microtask checkpoint, so `unhandledrejection` fired
        // before the handler existed and testharness made it a file-wide ERROR. That was the engine's cadence
        // rather than anything about custom elements, and sebastienros/jint#3711 fixed it: the engine now
        // notifies from the checkpoint over the promises still unhandled at it, which is what
        // `Engine.NotifyAboutRejectedPromises` is. So this row's cause is spent, exactly like the four above,
        // and vendoring the file is the same change of its own that they are — it moves the census's
        // Documents and Tests columns, which the change that fixes an engine deliberately does not.
        ("custom-elements/CustomElementRegistry.html", "not vendored: an `unhandledrejection` the engine raised at the tracker's cadence made it a file-wide error, and the cadence is HTML's now"),
        // A constructor that constructs a *second* instance of its own name before calling `super()`. HTML has
        // the parser *construct* a custom element, so the nested construction starts with an empty construction
        // stack and makes an element of its own; here the parser creates the element and the driver upgrades
        // it, so the stack is not empty and the nested `super()` takes the element being upgraded. The outer
        // `super()` then finds the already-constructed marker, and the InvalidStateError is reported at the
        // global scope, which testharness makes a file-wide ERROR.
        ("custom-elements/parser/parser-uses-constructed-element.html", "the parser upgrades a custom element where HTML constructs one, so a constructor that constructs its own name before super() takes the element being upgraded"),

        // ============================================================ dom/nodes, dom/collections, dom/lists,
        // dom/traversal, dom/ranges and html/dom — the DOM standard's own corpus, and HTML's DOM half.

        // ------------------------------------------------------------ the whole-directory rule
        // A suite is one directory, because WptCorpus.BrowserTestFiles lists a directory's own files and never
        // descends. None of these is one, and each says what it would need.
        ("dom/nodes/Document-contentType/*", "generated documents, each served with a `.headers` sidecar naming a content type of its own"),
        ("dom/nodes/Document-createElement-namespace-tests/*", "XML and XHTML fixtures the namespace test frames"),
        ("dom/nodes/crashtests/*", "crash reproductions: none loads testharness.js, so none can report"),
        ("dom/nodes/insertion-removing-steps/*", "HTML's insertion and removing steps, which are about a rendering and a form owner"),
        ("dom/nodes/moveBefore/*", "moveBefore(), which this package's Node bindings do not have"),
        ("dom/ranges/crashtests/*", "crash reproductions, which load no harness"),
        ("dom/ranges/tentative/*", "tests a proposal the specification has not adopted"),
        ("dom/traversal/unfinished/*", "upstream's own name for tests it has not finished"),
        ("html/dom/directionality/*", "the directionality algorithm, which needs a rendering to observe"),
        ("html/dom/documents/*", "documents, document.open() and the resource metadata, which this change does not vendor"),
        ("html/dom/elements/*", "the per-element documents, which this change does not vendor"),
        ("html/dom/partial-updates/*", "a rendering: each compares what a partial update painted"),
        ("html/dom/render-blocking/*", "render-blocking, which needs a rendering to block"),

        // ------------------------------------------------------------ not a document, and upstream's markers
        ("dom/nodes/*.window.js", "a .window.js script, whose generated wrapper this lane does not synthesize"),
        ("dom/collections/*.window.js", "a .window.js script, whose generated wrapper this lane does not synthesize"),
        ("dom/traversal/*.window.js", "a .window.js script, whose generated wrapper this lane does not synthesize"),
        ("html/dom/*.tentative.html", "tests a proposal the specification has not adopted"),
        ("dom/nodes/*.sub.html", "wptserve substitution into a *second* origin, which this server does not have"),
        ("html/dom/*.sub.html", "wptserve substitution into a *second* origin, which this server does not have"),

        // ------------------------------------------------------------ an XML document
        // The server answers `.xhtml` with application/xhtml+xml and `.svg` with image/svg+xml, and a page here
        // parses HTML: AngleSharp builds no XML document. WptDivergence.NeedsXmlDocuments is the same fact from
        // the HTML side, and the exclusion table names that half test by test.
        ("dom/nodes/*.xhtml", "an XML document; a page here parses HTML and AngleSharp builds no XML document"),
        ("dom/nodes/*.xht", "an XML document, in upstream's older spelling"),
        ("dom/nodes/*.svg", "an SVG document a test frames, not a test"),
        ("dom/nodes/*-xml.xml", "an XML fixture of a document that is not vendored"),
        ("dom/nodes/Node-isEqualNode-iframe*.xml", "the two XML frames of a test that is not vendored"),
        ("dom/nodes/getElementsByClassName-1*.xml", "two XML fixtures of documents that are not vendored"),

        // ------------------------------------------------------------ needs the WebIDL conformance harness
        ("html/dom/idlharness.https.html", "idl_test([...]), which the engine lane declines for the same reason"),
        ("html/dom/usvstring-reflection.https.html", "needs webrtc/RTCPeerConnection-helper.js and a real RTCPeerConnection to reflect a USVString off"),

        // ------------------------------------------------------------ HTML's reflection suite
        // Ten generated documents, 56,660 assertions, of which 22,028 do not pass. This package projects
        // AngleSharp's properties and does not implement HTML's reflection algorithms — the limited-to-known-
        // values enumerations, the unsigned long clamping and defaulting, the URL reflection, the nullable
        // string cases — so the failures are spread over nearly every content attribute of nearly every
        // element. **They are not slow**: the whole set runs in 22.5 s and the largest of them
        // (reflection-embedded.html, 8,922 tests) in 7.3 s, well inside the driver's 30 s deadline. What stops
        // them being vendored is the artefact: the smallest table of patterns that covers those failures and
        // no passing test is over four thousand rows, and every one of them says the same thing. One issue
        // saying it once is the better record, and the day reflection is implemented these become cases.
        ("html/dom/reflection-*.html", "HTML's reflection suite: 22,028 of 56,660 assertions fail because reflection is not implemented, and no readable exclusion table can name them"),
        ("html/dom/reflection.js", "the body of the reflection suite above"),
        ("html/dom/elements-*.js", "the per-family attribute tables the reflection suite above runs"),
        ("html/dom/new-harness.js", "the reflection suite's own harness"),
        ("html/dom/original-harness.js", "the reflection suite's own harness, in the aggregating spelling reflection-original.html uses"),

        // ------------------------------------------------------------ not a testharness document at all
        ("dom/nodes/*crash.html", "a crash reproduction: it loads no harness and asserts nothing"),
        ("dom/collections/*crash.html", "a crash reproduction: it loads no harness and asserts nothing"),
        ("dom/ranges/*crash.html", "a crash reproduction: it loads no harness and asserts nothing"),
        ("dom/nodes/remove-from-shadow-host-and-adopt-into-iframe.html", "a reftest, which needs a rendering to compare"),
        ("dom/nodes/remove-from-shadow-host-and-adopt-into-iframe-ref.html", "the reference of the reftest above"),

        // ------------------------------------------------------------ a helper document upstream keeps beside its test
        // A document directly under a suite is a case (WptCorpus.BrowserTestFiles), so a helper vendored there
        // would have to report and none of these can: they are frames and fragments. Their tests are in the
        // group below, for the reason a frame is never given an engine here.
        ("dom/nodes/ParentNode-querySelector-All-content.html", "the iframe body of the three selector documents below"),
        ("dom/nodes/Node-parentNode-iframe.html", "the frame of Node-parentNode.html"),
        ("dom/nodes/getElementsByClassNameFrame.htm", "the frame of getElementsByClassName-31.htm"),
        ("dom/nodes/query-target-in-load-event.part.html", "the fragment query-target-in-load-event.html loads"),
        ("dom/ranges/Range-test-iframe.html", "the iframe body five Range documents evaluate their endpoints in"),
        ("dom/traversal/support/TreeWalker-acceptNode-filter-cross-realm-null-browsing-context-subframe.html", "the subframe of a test that is not vendored"),

        // ------------------------------------------------------------ a frame that runs script
        // Listed when a frame had neither a document nor a window, so an `iframe.onload` never arrived and a
        // file that waits for one reported TIMEOUT — a harness error covering the whole file. #3771 gave a
        // frame a document and a window and `load` fires now, so **the reason on each of these rows is owed a
        // re-derivation**; it takes vendoring the document, which moves the census's Documents and Tests
        // columns and is a change of its own.
        ("dom/nodes/Comment-constructor.html", "its last test waits for an iframe's load; the other fifteen do report, and all fifteen fail because `new Comment()` is an illegal constructor"),
        ("dom/nodes/Text-constructor.html", "the same file for Text, and the same refusal in its fifteen reported tests"),
        ("dom/nodes/Document-URL.html", "waits for an iframe that follows a redirect"),
        ("dom/nodes/Document-characterSet-normalization-1.html", "builds one iframe per encoding label and waits for each"),
        ("dom/nodes/Document-characterSet-normalization-2.html", "the same, for the second half of the label table"),
        ("dom/nodes/Document-createElement-namespace.html", "an iframe per XML fixture, each of which has to run script"),
        ("dom/nodes/Element-matches.html", "runs its whole table inside ParentNode-querySelector-All-content.html, which is a frame"),
        ("dom/nodes/Element-webkitMatchesSelector.html", "the same table, through the prefixed alias"),
        ("dom/nodes/ParentNode-querySelector-All.html", "the same table again, for querySelector and querySelectorAll"),
        ("dom/nodes/MutationObserver-cross-realm-callback-report-exception.html", "a callback whose realm is an iframe's"),
        ("dom/nodes/Node-parentNode.html", "its four reported tests pass and the fifth waits for a frame"),
        ("dom/nodes/Node-baseURI.html", "its four reported tests pass and the rest wait for an iframe's base URL"),
        ("dom/nodes/attach-shadow-realm-after-adoption.html", "reads `customElements` off an iframe's window at file scope"),
        ("dom/nodes/create-element-realm-after-adoption.html", "the same window, at file scope"),
        ("dom/nodes/getElementsByClassName-31.htm", "waits for getElementsByClassNameFrame.htm"),
        ("dom/nodes/query-target-in-load-event.html", "waits for the fragment it loads in a frame"),
        ("dom/traversal/TreeWalker-acceptNode-filter-cross-realm-null-browsing-context.html", "needs a frame it can then remove, so that the filter's realm has no browsing context"),

        // ------------------------------------------------------------ a member reached at file scope
        // Each of these asks for a member the bindings do not have before it has registered a test, so the
        // harness reports ERROR for the whole file. The member itself is the exclusion table's business and is
        // named there test by test by a document that does report; what is different here is only *when* it is
        // reached.
        //
        // **Thirty-one of them are here for a reason that has been spent**: `dom/common.js`, the shared
        // fixture builder of the Range and traversal suites, calls `createCDATASection` at file scope and
        // `document.implementation.createDocument` two lines later, and both exist now. Vendoring them is a
        // change of its own — it moves the census's Documents and Tests columns, which the change that fixes
        // an engine deliberately does not — which is the same standing this table already gives the four
        // `dom/events/` documents that were waiting on `document.createEvent`.
        ("dom/ranges/Range-mutations-*.html", "not vendored: dom/common.js called document.createCDATASection at file scope, which now exists"),
        ("dom/ranges/Range-cloneContents.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-cloneRange.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-collapse.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-commonAncestorContainer.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-compareBoundaryPoints.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-comparePoint.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-deleteContents.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-extractContents.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-insertNode.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-intersectsNode.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-isPointInRange.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-selectNode.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-set.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/ranges/Range-surroundContents.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/traversal/NodeIterator.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/traversal/NodeIterator-removal.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/traversal/TreeWalker.html", "not vendored: it called createCDATASection through dom/common.js at file scope, which now exists"),
        ("dom/nodes/Node-compareDocumentPosition.html", "not vendored: it called createCDATASection at file scope, which now exists"),
        ("dom/nodes/Node-contains.html", "not vendored: it called createCDATASection at file scope, which now exists"),
        ("dom/nodes/Node-properties.html", "not vendored: it called createCDATASection at file scope, which now exists"),
        ("dom/nodes/MutationObserver-textContent.html", "not vendored: it called createCDATASection in a promise whose rejection testharness makes a file-wide error; the member now exists"),
        ("dom/nodes/Document-createAttribute.html", "not vendored: it called document.implementation.createDocument at file scope, which now exists"),
        ("dom/nodes/DocumentType-remove.html", "not vendored: it called document.implementation.createDocument at file scope, which now exists"),
        ("dom/nodes/Node-textContent.html", "not vendored: it called document.implementation.createDocument at file scope, which now exists"),
        ("dom/nodes/append-on-Document.html", "not vendored: it called document.implementation.createDocument at file scope, which now exists"),
        ("dom/nodes/prepend-on-Document.html", "not vendored: it called document.implementation.createDocument at file scope, which now exists"),
        ("dom/nodes/Node-lookupNamespaceURI.html", "setAttributeNode at file scope, after seventy-two of its tests have reported"),
        ("html/dom/aria-element-reflection-labelledby.html", "reads firstElementChild off a null shadow root in a promise, which testharness makes a file-wide error"),

        // ------------------------------------------------------------ one file each
        ("dom/collections/domstringmap-supported-property-names.html", "an AngleSharp SyntaxError escapes its third test at file scope and no `error` event carries it to the harness, so the file reports three of its five and then times out"),
        ("dom/nodes/MutationObserver-attributes.html", "thirty-four of its tests report and one waits forever for a record the observer never delivers"),
        ("dom/nodes/MutationObserver-childList.html", "the same, after thirty-eight"),

        // ------------------------------------------------------------ too slow to be a case
        // A static NodeList's length is re-read rather than snapshotted, so a test that tampers with the getter
        // to answer a huge number is believed: the six documents take 5.9 s, 8.0 s, 9.3 s, 17.5 s, 17.9 s and
        // 18.8 s on an idle machine and one of them crossed the driver's 30 s deadline on a loaded one. A case
        // whose outcome depends on the machine is the thing the census exists to keep out, so they are rows here
        // with the measurement rather than a flake in the run. The re-read is the defect and it is worth fixing;
        // the two `-indexOf-` shapes also answer 0 where Array.prototype.indexOf should answer -1.
        ("dom/nodes/NodeList-static-length-getter-tampered-*.html", "a static NodeList re-reads its tampered length getter, so the document spends between 5.9 s and 18.8 s and one of the six crossed the driver's 30 s deadline under load"),
        ("dom/nodes/support/NodeList-static-length-tampered.js", "the helper the six documents above share"),
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
        ["custom-elements/CustomElementRegistry-constructor-and-callbacks-are-held-strongly.html"] = 5,
        ["custom-elements/CustomElementRegistry-getName.html"] = 4,
        ["custom-elements/Document-createElementNS-customized-builtins.html"] = 3,
        ["custom-elements/Document-createElementNS-prefix-timing.html"] = 3,
        ["custom-elements/Document-createElementNS.html"] = 4,
        ["custom-elements/HTMLElement-constructor-customized-builtins.html"] = 2,
        ["custom-elements/HTMLElement-constructor.html"] = 12,
        ["custom-elements/attribute-changed-callback.html"] = 13,
        ["custom-elements/builtin-coverage.html"] = 441,
        ["custom-elements/connected-callbacks-template.html"] = 1,
        ["custom-elements/customized-built-in-constructor-exceptions.html"] = 5,
        ["custom-elements/historical.html"] = 3,
        ["custom-elements/microtasks-and-constructors.html"] = 5,
        ["custom-elements/overwritten-customElements-global.html"] = 4,
        ["custom-elements/parser/parser-constructs-custom-element-in-document-write.html"] = 2,
        ["custom-elements/parser/parser-constructs-custom-element-synchronously.html"] = 1,
        ["custom-elements/parser/parser-constructs-custom-elements-with-is.html"] = 2,
        ["custom-elements/parser/parser-constructs-custom-elements.html"] = 2,
        ["custom-elements/parser/parser-custom-element-in-foreign-content.html"] = 1,
        ["custom-elements/parser/parser-fallsback-to-unknown-element.html"] = 4,
        ["custom-elements/parser/parser-sets-attributes-and-children.html"] = 5,
        ["custom-elements/parser/serializing-html-fragments-customized-builtins.html"] = 3,
        ["custom-elements/range-and-constructors.html"] = 2,
        ["custom-elements/reaction-timing.html"] = 3,
        ["custom-elements/reactions/Animation.html"] = 3,
        ["custom-elements/reactions/AriaMixin-element-attributes.html"] = 16,
        ["custom-elements/reactions/AriaMixin-string-attributes.html"] = 80,
        ["custom-elements/reactions/Attr.html"] = 2,
        ["custom-elements/reactions/CSSStyleDeclaration.html"] = 30,
        ["custom-elements/reactions/ChildNode.html"] = 7,
        ["custom-elements/reactions/DOMStringMap.html"] = 8,
        ["custom-elements/reactions/DOMTokenList.html"] = 19,
        ["custom-elements/reactions/Element.html"] = 47,
        ["custom-elements/reactions/ElementContentEditable.html"] = 2,
        ["custom-elements/reactions/HTMLElement.html"] = 22,
        ["custom-elements/reactions/Node.html"] = 14,
        ["custom-elements/reactions/ParentNode.html"] = 4,
        ["custom-elements/reactions/Selection.html"] = 1,
        ["custom-elements/upgrading/Node-cloneNode-customized-builtins.html"] = 1,
        ["custom-elements/upgrading/upgrading-parser-created-element.html"] = 6,
        ["dom/collections/HTMLCollection-as-prototype.html"] = 2,
        ["dom/collections/HTMLCollection-delete.html"] = 4,
        ["dom/collections/HTMLCollection-empty-name.html"] = 7,
        ["dom/collections/HTMLCollection-iterator.html"] = 6,
        ["dom/collections/HTMLCollection-own-props.html"] = 8,
        ["dom/collections/HTMLCollection-supported-property-indices.html"] = 7,
        ["dom/collections/HTMLCollection-supported-property-names.html"] = 6,
        ["dom/collections/namednodemap-supported-property-names.html"] = 3,
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
        ["dom/events/EventTarget-add-listener-platform-object.html"] = 1,
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
        ["dom/lists/DOMTokenList-Iterable.html"] = 6,
        ["dom/lists/DOMTokenList-coverage-for-attributes.html"] = 175,
        ["dom/lists/DOMTokenList-iteration.html"] = 6,
        ["dom/lists/DOMTokenList-stringifier.html"] = 1,
        ["dom/lists/DOMTokenList-value.html"] = 1,
        ["dom/nodes/Attr-prefix.html"] = 6,
        ["dom/nodes/CharacterData-appendChild.html"] = 9,
        ["dom/nodes/CharacterData-appendData.html"] = 14,
        ["dom/nodes/CharacterData-data.html"] = 16,
        ["dom/nodes/CharacterData-deleteData.html"] = 18,
        ["dom/nodes/CharacterData-insertData.html"] = 18,
        ["dom/nodes/CharacterData-remove.html"] = 12,
        ["dom/nodes/CharacterData-replaceData.html"] = 34,
        ["dom/nodes/CharacterData-substringData.html"] = 28,
        ["dom/nodes/CharacterData-surrogates.html"] = 8,
        ["dom/nodes/ChildNode-after.html"] = 45,
        ["dom/nodes/ChildNode-before.html"] = 45,
        ["dom/nodes/ChildNode-replaceWith.html"] = 33,
        ["dom/nodes/DOMImplementation-createDocument.html"] = 434,
        ["dom/nodes/DOMImplementation-createDocumentType.html"] = 82,
        ["dom/nodes/DOMImplementation-createHTMLDocument-with-saved-implementation.html"] = 1,
        ["dom/nodes/DOMImplementation-createHTMLDocument.html"] = 13,
        ["dom/nodes/DOMImplementation-hasFeature.html"] = 137,
        ["dom/nodes/Document-adoptNode.html"] = 4,
        ["dom/nodes/Document-constructor.html"] = 5,
        ["dom/nodes/Document-createCDATASection.html"] = 1,
        ["dom/nodes/Document-createComment.html"] = 6,
        ["dom/nodes/Document-createElement.html"] = 147,
        ["dom/nodes/Document-createElementNS.html"] = 596,
        ["dom/nodes/Document-createEvent.https.html"] = 279,
        ["dom/nodes/Document-createProcessingInstruction.html"] = 12,
        ["dom/nodes/Document-createTextNode.html"] = 6,
        ["dom/nodes/Document-createTreeWalker.html"] = 5,
        ["dom/nodes/Document-doctype.html"] = 2,
        ["dom/nodes/Document-getElementById.html"] = 18,
        ["dom/nodes/Document-getElementsByClassName.html"] = 1,
        ["dom/nodes/Document-getElementsByTagName.html"] = 18,
        ["dom/nodes/Document-getElementsByTagNameNS.html"] = 14,
        ["dom/nodes/Document-implementation.html"] = 2,
        ["dom/nodes/Document-importNode.html"] = 5,
        ["dom/nodes/DocumentFragment-constructor.html"] = 2,
        ["dom/nodes/DocumentFragment-getElementById.html"] = 5,
        ["dom/nodes/DocumentFragment-querySelectorAll-after-modification.html"] = 1,
        ["dom/nodes/DocumentType-literal.html"] = 1,
        ["dom/nodes/Element-childElement-null.html"] = 1,
        ["dom/nodes/Element-childElementCount-dynamic-add.html"] = 1,
        ["dom/nodes/Element-childElementCount-dynamic-remove.html"] = 1,
        ["dom/nodes/Element-childElementCount-nochild.html"] = 1,
        ["dom/nodes/Element-childElementCount.html"] = 1,
        ["dom/nodes/Element-children.html"] = 2,
        ["dom/nodes/Element-classlist.html"] = 1420,
        ["dom/nodes/Element-closest.html"] = 29,
        ["dom/nodes/Element-firstElementChild-namespace.html"] = 1,
        ["dom/nodes/Element-firstElementChild.html"] = 1,
        ["dom/nodes/Element-getElementsByClassName.html"] = 3,
        ["dom/nodes/Element-getElementsByTagName-change-document-HTMLNess.html"] = 1,
        ["dom/nodes/Element-getElementsByTagName.html"] = 19,
        ["dom/nodes/Element-getElementsByTagNameNS.html"] = 16,
        ["dom/nodes/Element-hasAttribute.html"] = 2,
        ["dom/nodes/Element-hasAttributes.html"] = 2,
        ["dom/nodes/Element-insertAdjacentElement.html"] = 6,
        ["dom/nodes/Element-insertAdjacentText.html"] = 6,
        ["dom/nodes/Element-lastElementChild.html"] = 1,
        ["dom/nodes/Element-matches-namespaced-elements.html"] = 6,
        ["dom/nodes/Element-nextElementSibling.html"] = 1,
        ["dom/nodes/Element-previousElementSibling.html"] = 1,
        ["dom/nodes/Element-remove.html"] = 4,
        ["dom/nodes/Element-removeAttribute.html"] = 2,
        ["dom/nodes/Element-removeAttributeNS.html"] = 1,
        ["dom/nodes/Element-setAttribute-crbug-1138487.html"] = 1,
        ["dom/nodes/Element-setAttribute.html"] = 2,
        ["dom/nodes/Element-siblingElement-null.html"] = 1,
        ["dom/nodes/Element-tagName.html"] = 6,
        ["dom/nodes/MutationObserver-callback-arguments.html"] = 1,
        ["dom/nodes/MutationObserver-characterData.html"] = 23,
        ["dom/nodes/MutationObserver-disconnect.html"] = 2,
        ["dom/nodes/MutationObserver-document.html"] = 4,
        ["dom/nodes/MutationObserver-inner-outer.html"] = 3,
        ["dom/nodes/MutationObserver-sanity.html"] = 16,
        ["dom/nodes/MutationObserver-takeRecords.html"] = 3,
        ["dom/nodes/Node-appendChild.html"] = 11,
        ["dom/nodes/Node-childNodes-cache-2.html"] = 1,
        ["dom/nodes/Node-childNodes-cache.html"] = 1,
        ["dom/nodes/Node-childNodes.html"] = 6,
        ["dom/nodes/Node-cloneNode-XMLDocument.html"] = 1,
        ["dom/nodes/Node-cloneNode-document-with-doctype.html"] = 3,
        ["dom/nodes/Node-cloneNode-svg.html"] = 4,
        ["dom/nodes/Node-cloneNode.html"] = 135,
        ["dom/nodes/Node-constants.html"] = 8,
        ["dom/nodes/Node-insertBefore.html"] = 40,
        ["dom/nodes/Node-isConnected-shadow-dom.html"] = 2,
        ["dom/nodes/Node-isConnected.html"] = 2,
        ["dom/nodes/Node-isEqualNode.html"] = 9,
        ["dom/nodes/Node-isSameNode.html"] = 9,
        ["dom/nodes/Node-mutation-adoptNode.html"] = 2,
        ["dom/nodes/Node-nodeName.html"] = 6,
        ["dom/nodes/Node-nodeValue.html"] = 7,
        ["dom/nodes/Node-normalize.html"] = 4,
        ["dom/nodes/Node-parentElement.html"] = 12,
        ["dom/nodes/Node-removeChild.html"] = 28,
        ["dom/nodes/Node-replaceChild.html"] = 29,
        ["dom/nodes/NodeList-Iterable.html"] = 8,
        ["dom/nodes/ParentNode-append.html"] = 25,
        ["dom/nodes/ParentNode-children.html"] = 1,
        ["dom/nodes/ParentNode-prepend.html"] = 22,
        ["dom/nodes/ParentNode-querySelector-case-insensitive.html"] = 2,
        ["dom/nodes/ParentNode-querySelector-escapes.html"] = 68,
        ["dom/nodes/ParentNode-querySelector-scope.html"] = 4,
        ["dom/nodes/ParentNode-querySelectorAll-removed-elements.html"] = 1,
        ["dom/nodes/ParentNode-querySelectors-exclusive.html"] = 1,
        ["dom/nodes/ParentNode-querySelectors-namespaces.html"] = 1,
        ["dom/nodes/ParentNode-querySelectors-space-and-dash-attribute-value.html"] = 2,
        ["dom/nodes/ParentNode-replaceChildren.html"] = 31,
        ["dom/nodes/Text-splitText.html"] = 6,
        ["dom/nodes/Text-wholeText.html"] = 1,
        ["dom/nodes/attributes-namednodemap.html"] = 8,
        ["dom/nodes/attributes.html"] = 67,
        ["dom/nodes/case.html"] = 285,
        ["dom/nodes/getElementsByClassName-01.htm"] = 1,
        ["dom/nodes/getElementsByClassName-02.htm"] = 1,
        ["dom/nodes/getElementsByClassName-03.htm"] = 1,
        ["dom/nodes/getElementsByClassName-04.htm"] = 1,
        ["dom/nodes/getElementsByClassName-05.htm"] = 1,
        ["dom/nodes/getElementsByClassName-06.htm"] = 1,
        ["dom/nodes/getElementsByClassName-07.htm"] = 1,
        ["dom/nodes/getElementsByClassName-08.htm"] = 1,
        ["dom/nodes/getElementsByClassName-09.htm"] = 1,
        ["dom/nodes/getElementsByClassName-12.htm"] = 1,
        ["dom/nodes/getElementsByClassName-13.htm"] = 1,
        ["dom/nodes/getElementsByClassName-14.htm"] = 2,
        ["dom/nodes/getElementsByClassName-15.htm"] = 1,
        ["dom/nodes/getElementsByClassName-16.htm"] = 1,
        ["dom/nodes/getElementsByClassName-17.htm"] = 1,
        ["dom/nodes/getElementsByClassName-18.htm"] = 1,
        ["dom/nodes/getElementsByClassName-19.htm"] = 1,
        ["dom/nodes/getElementsByClassName-20.htm"] = 1,
        ["dom/nodes/getElementsByClassName-21.htm"] = 1,
        ["dom/nodes/getElementsByClassName-22.htm"] = 1,
        ["dom/nodes/getElementsByClassName-23.htm"] = 1,
        ["dom/nodes/getElementsByClassName-24.htm"] = 1,
        ["dom/nodes/getElementsByClassName-25.htm"] = 1,
        ["dom/nodes/getElementsByClassName-26.htm"] = 1,
        ["dom/nodes/getElementsByClassName-27.htm"] = 1,
        ["dom/nodes/getElementsByClassName-28.htm"] = 1,
        ["dom/nodes/getElementsByClassName-29.htm"] = 1,
        ["dom/nodes/getElementsByClassName-30.htm"] = 1,
        ["dom/nodes/getElementsByClassName-32.html"] = 4,
        ["dom/nodes/getElementsByClassName-empty-set.html"] = 3,
        ["dom/nodes/getElementsByClassName-whitespace-class-names.html"] = 26,
        ["dom/nodes/insert-adjacent.html"] = 14,
        ["dom/nodes/name-validation.html"] = 5,
        ["dom/nodes/node-creation-realm.html"] = 13,
        ["dom/nodes/node-realm-adoption-after-frame-removal.html"] = 3,
        ["dom/nodes/node-realm-mixed-across-adoption.html"] = 4,
        ["dom/nodes/node-realm-preserved-across-adoption.html"] = 5,
        ["dom/nodes/node-realm-preserved-across-frameless-adoption.html"] = 4,
        ["dom/nodes/processing-instruction-attributes.html"] = 140,
        ["dom/nodes/querySelector-empty-id.html"] = 1,
        ["dom/nodes/querySelector-id-nth-child.html"] = 2,
        ["dom/nodes/querySelector-mixed-case.html"] = 1,
        ["dom/nodes/remove-next-sibling-during-replace-with.html"] = 1,
        ["dom/nodes/remove-unscopable.html"] = 6,
        ["dom/nodes/rootNode.html"] = 5,
        ["dom/nodes/svg-template-querySelector.html"] = 3,
        ["dom/ranges/Range-adopt-test.html"] = 4,
        ["dom/ranges/Range-attribute-nodes.html"] = 26,
        ["dom/ranges/Range-attributes.html"] = 1,
        ["dom/ranges/Range-cloneContents-in-ShadowRoot.html"] = 4,
        ["dom/ranges/Range-commonAncestorContainer-2.html"] = 6,
        ["dom/ranges/Range-comparePoint-2.html"] = 3,
        ["dom/ranges/Range-constructor.html"] = 1,
        ["dom/ranges/Range-deleteContents-in-ShadowRoot.html"] = 4,
        ["dom/ranges/Range-detach.html"] = 1,
        ["dom/ranges/Range-extractContents-dynamic-end.html"] = 1,
        ["dom/ranges/Range-extractContents-in-ShadowRoot.html"] = 4,
        ["dom/ranges/Range-in-shadow-after-the-shadow-removed.html"] = 2,
        ["dom/ranges/Range-intersectsNode-2.html"] = 1,
        ["dom/ranges/Range-intersectsNode-binding.html"] = 1,
        ["dom/ranges/Range-intersectsNode-shadow.html"] = 1,
        ["dom/ranges/Range-stringifier.html"] = 1,
        ["dom/ranges/StaticRange-constructor.html"] = 17,
        ["dom/traversal/NodeFilter-constants.html"] = 2,
        ["dom/traversal/NodeIterator-removal-during-filtering.html"] = 4,
        ["dom/traversal/TreeWalker-acceptNode-filter-cross-realm.html"] = 5,
        ["dom/traversal/TreeWalker-acceptNode-filter.html"] = 12,
        ["dom/traversal/TreeWalker-basic.html"] = 6,
        ["dom/traversal/TreeWalker-currentNode.html"] = 4,
        ["dom/traversal/TreeWalker-previousNodeLastChildReject.html"] = 1,
        ["dom/traversal/TreeWalker-previousSiblingLastChildSkip.html"] = 1,
        ["dom/traversal/TreeWalker-realm.html"] = 2,
        ["dom/traversal/TreeWalker-traversal-reject.html"] = 6,
        ["dom/traversal/TreeWalker-traversal-skip-most.html"] = 2,
        ["dom/traversal/TreeWalker-traversal-skip.html"] = 6,
        ["dom/traversal/TreeWalker-walking-outside-a-tree.html"] = 1,
        ["html/dom/access-key-label.html"] = 2,
        ["html/dom/aria-attribute-reflection.html"] = 41,
        ["html/dom/aria-element-reflection-disconnected.html"] = 2,
        ["html/dom/aria-element-reflection.html"] = 27,
        ["html/dom/historical.html"] = 13,
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
    /// <b><see cref="WptDivergence.NeedsTriage"/> is four distinct things, not eleven rows.</b> The eleven
    /// defects this lane first recorded were filed as
    /// https://github.com/sebastienros/jint/issues/3686 to 3695 and are fixed; what is left is named in
    /// <c>Wpt/README.md</c>, one section per cause, and every one of them is bounded — a scheme a subresource
    /// cannot fetch, a member AngleSharp reflects wrong, an <c>@@unscopables</c> object the binding does not
    /// emit, and a custom element.
    /// </para>
    /// <para>
    /// <b>The DOM suites made it much bigger, and every one of those causes is filed.</b> They arrived with
    /// 2,181 failing tests over 209 documents, and <c>Wpt/README.md</c>'s "What the DOM corpus says about this
    /// browser" names twenty-one causes with the count each accounts for. Ten are
    /// https://github.com/sebastienros/jint/issues/3765 to 3774 and one was already open as
    /// https://github.com/sebastienros/jint/issues/3712, so a row here that is not one of
    /// <see cref="WptDivergence.NeedsIframeScripting"/>, <see cref="WptDivergence.NeedsXmlDocuments"/> or
    /// <see cref="WptDivergence.NeedsMoreEventInterfaces"/> is a numbered debt rather than an unread one.
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
        // `eventhandler-cancellation.html` fires its events at `frames[0]`, which is an iframe's window. A
        // frame has a window since #3771 — but this file's frame is `<iframe>` with no `src`, and a frame
        // with no source is never asked for and so has no document and no window here, where HTML gives it an
        // initial `about:blank` one. `frames[0]` is therefore undefined and the file fails on it. Even given
        // one it would need the realm: the events it fires are meant to be cancelled in the frame's own
        // global. It is the NeedsIframeScripting group below by cause, and is here only because the file is
        // in another suite.
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

        // ---------------------------------------------------------------- a frame that runs script
        // https://html.spec.whatwg.org/multipage/nav-history-apis.html#window: each of these needs a second
        // *realm* with a document in it — a cross-realm listener, a `beforeunload` result coerced in the
        // frame's realm, an exception reported in the realm of the listener that threw. A frame has a window
        // and a document since #3771 and still no realm of its own, so nothing in it runs.
        //
        // Named access on the window is what these files used to meet first, and it is implemented now
        // (Runtime/WindowNamedProperties); the frame is what is left, and no fix short of one moves them.
        //
        // The five `window-onerror-with-cross-frame-event-listeners-*` files meet something before the realm,
        // and the run says so: `new frames[0].Function(...)` reads a member of `undefined`, because their
        // frames are `<iframe>` with no `src`. A frame with no source is never asked for here — the resource
        // loader answers a request AngleSharp makes, and it makes none — so it has no document and therefore
        // no window, where HTML gives every nested browsing context an initial `about:blank` document. That is
        // a gap of its own and not this category; opening a document into a context nobody navigated is not
        // something AngleSharp's public surface does.
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

        // ================================================================ custom-elements
        // What the custom element corpus found. Every one of these is a defect somebody owes a fix for:
        // `Wpt/README.md` groups them by cause and names each, and the groups below are that list in the
        // order the README gives it.

        // ---------------------------------------------------------------- the registry, the constructor and the two creation members
        new("custom-elements/CustomElementRegistry-constructor-and-callbacks-are-held-strongly.html", "adoptedCallback", WptDivergence.NeedsTriage),
        new("custom-elements/CustomElementRegistry-getName.html", "customElements.getName must throw when the element interface is not a constructor", WptDivergence.NeedsTriage),
        new("custom-elements/CustomElementRegistry-getName.html", "customElements.getName returns the name of the entry with the given constructor when there is a matching entry.", WptDivergence.NeedsTriage),
        new("custom-elements/Document-createElementNS.html", "autonomous: document.createElementNS should create custom elements with prefixes.", WptDivergence.NeedsTriage),
        new("custom-elements/Document-createElementNS-customized-builtins.html", "builtin: document.createElementNS should create custom elements with prefixes.", WptDivergence.NeedsTriage),
        new("custom-elements/Document-createElementNS-prefix-timing.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/HTMLElement-constructor.html", "HTMLElement constructor must throw a TypeError when NewTarget is equal to itself", WptDivergence.NeedsTriage),
        new("custom-elements/HTMLElement-constructor.html", "HTMLElement constructor must throw a TypeError when NewTarget is equal to itself via a Proxy object", WptDivergence.NeedsTriage),
        new("custom-elements/HTMLElement-constructor-customized-builtins.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/overwritten-customElements-global.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/range-and-constructors.html", "*", WptDivergence.NeedsTriage),

        // The eight rows of attribute-changed-callback.html that the whole-document entry used to cover are
        // green: create_attribute_changed_callback_log reads the value back with getAttributeNS(null, name),
        // which now answers the attribute rather than looking for a namespace spelled "null". These five are
        // what is left, and each is a different defect.
        new("custom-elements/attribute-changed-callback.html", "attributedChangedCallback must be enqueued for style attribute change by mutating inline style declaration", WptDivergence.NeedsTriage),
        new("custom-elements/attribute-changed-callback.html", "setAttributeNS and removeAttributeNS must enqueue and invoke attributeChangedCallback", WptDivergence.NeedsTriage),
        new("custom-elements/attribute-changed-callback.html", "setAttributeNode and removeAttributeNS must enqueue and invoke attributeChangedCallback for an SVG attribute", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- the callbacks and when they run
        new("custom-elements/microtasks-and-constructors.html", "Microtasks evaluate immediately when the stack is empty inside the parser", WptDivergence.NeedsTriage),
        new("custom-elements/microtasks-and-constructors.html", "Microtasks evaluate immediately when the stack is empty inside the parser, causing the checks on no attributes to fail", WptDivergence.NeedsTriage),
        new("custom-elements/reaction-timing.html", "*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- the customized built-in table
        new("custom-elements/builtin-coverage.html", "*: Operator 'new' should instantiate a customized built-in element", WptDivergence.NeedsTriage),
        new("custom-elements/builtin-coverage.html", "*: document.createElement() should instantiate a customized built-in element", WptDivergence.NeedsTriage),
        new("custom-elements/builtin-coverage.html", "dl: Define a customized built-in element", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- the parser
        new("custom-elements/parser/parser-constructs-custom-element-in-document-write.html", "HTML parser must instantiate custom elements inside document.write", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-constructs-custom-element-synchronously.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-fallsback-to-unknown-element.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-sets-attributes-and-children.html", "HTML parser must enqueue attributeChanged reactions", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-sets-attributes-and-children.html", "HTML parser must set the attributes or append children before calling constructor", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-sets-attributes-and-children.html", "HTML parser should call connectedCallback before appending child nodes.", WptDivergence.NeedsTriage),
        new("custom-elements/parser/serializing-html-fragments-customized-builtins.html", "\"is\" value should be serialized even for an undefined element", WptDivergence.NeedsTriage),
        new("custom-elements/parser/serializing-html-fragments-customized-builtins.html", "\"is\" value should be serialized if the custom element has no \"is\" content attribute", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- the upgrade
        new("custom-elements/upgrading/Node-cloneNode-customized-builtins.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/upgrading/upgrading-parser-created-element.html", "HTMLElement constructor must throw an TypeError when the top of the construction stack is marked AlreadyConstructed due to a custom element constructor constructing itself after super() call", WptDivergence.NeedsTriage),
        new("custom-elements/upgrading/upgrading-parser-created-element.html", "HTMLElement constructor must throw an TypeError when the top of the construction stack is marked AlreadyConstructed due to a custom element constructor constructing itself before super() call", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- one [CEReactions] member per file
        new("custom-elements/reactions/Animation.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/AriaMixin-element-attributes.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/CSSStyleDeclaration.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/ChildNode.html", "after on ChildNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/ChildNode.html", "before on ChildNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/ChildNode.html", "replaceWith on ChildNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/Element.html", "insertAdjacentElement on Element must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/Element.html", "insertAdjacentHTML on Element must enqueue a attributeChanged reaction for a newly constructed custom element", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/Element.html", "insertAdjacentHTML on Element must enqueue a connected reaction for a newly constructed custom element", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "draggable on HTMLElement must enqueue an attributeChanged reaction when adding draggable content attribute", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "draggable on HTMLElement must enqueue an attributeChanged reaction when replacing an existing attribute", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "hidden on HTMLElement must enqueue an attributeChanged reaction when replacing an existing attribute", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "outerText on HTMLElement must enqueue a disconnected reaction", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "popover on HTMLElement must enqueue an attributeChanged reaction when adding popover content attribute", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "popover on HTMLElement must enqueue an attributeChanged reaction when replacing an existing attribute", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "spellcheck on HTMLElement must enqueue an attributeChanged reaction when adding spellcheck content attribute", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/HTMLElement.html", "spellcheck on HTMLElement must enqueue an attributeChanged reaction when replacing an existing attribute", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/Node.html", "appendChild on ChildNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/Node.html", "insertBefore on ChildNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/Node.html", "replaceChild on ChildNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/ParentNode.html", "append on ParentNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),
        new("custom-elements/reactions/ParentNode.html", "prepend on ParentNode must enqueue a disconnected reaction, an adopted reaction, and a connected reaction when the custom element was in another document", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a frame that runs script
        // a second global with a document in it
        // Not the frame any more, and the twin of the same move in Document-createElementNS.html: a frame
        // has a window now, so what is left of these 49 is that `application/xhtml+xml` is parsed by the
        // HTML parser — the fixture never loads as XHTML and every row fails on that first assertion.
        new("dom/nodes/Document-createElement.html", "*XHTML document", WptDivergence.NeedsXmlDocuments),
        // Narrowed by the run: a frame has a window now, so the ten rows that only needed one pass.
        // What is left is the XML twins of the HTML rows above — the same arguments, refused or accepted
        // by the same two defects — so they are named the same way.
        new("dom/nodes/Document-createElement.html", "*(\":\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\":foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"FOO\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"\\ufffffoo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f1oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f::oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f::oo:\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f:o:o\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f:oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f<oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f\\uffffoo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo1\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:0\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:_\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:fooெ\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:ெ\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo\\uffff\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo}\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"fooெ\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"fooெ:foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f}oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"fெ\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"marK\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"math\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"svg\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xml\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xml:foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xmlfoo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xmlfoo:bar\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xmlns\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xmlns:foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"İnput\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"ınput\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"̀\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"̀foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(null) in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(undefined) in XML document", WptDivergence.NeedsTriage),
        // Not the frame any more: the frame has its document. `application/xhtml+xml` is routed to the
        // HTML parser even with the XML factory registered, so the XHTML fixture comes back as an HTML
        // document and all 195 fail on its first assertion — the trailing newline an HTML skeleton adds.
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in XHTML*", WptDivergence.NeedsXmlDocuments),
        // Narrowed by the run rather than by hand: the XML document is real now, so 56 of these pass.
        // What is left of them is the 110 rows that reach `doc.defaultView.DOMException`, and a frame
        // has a document here and no window — every one of those names ends in the exception it expects,
        // which is what separates them from the 56 that do not throw at all.
        new("dom/nodes/Document-createEvent.https.html", "*TextEvent.", WptDivergence.NeedsIframeScripting),
        new("dom/nodes/Node-isConnected.html", "*iframes", WptDivergence.NeedsIframeScripting),
        new("dom/nodes/node-creation-realm.html", "*", WptDivergence.NeedsIframeScripting),
        new("dom/nodes/node-realm-adoption-after-frame-removal.html", "*", WptDivergence.NeedsIframeScripting),
        new("dom/nodes/node-realm-mixed-across-adoption.html", "*", WptDivergence.NeedsIframeScripting),
        new("dom/nodes/node-realm-preserved-across-adoption.html", "*", WptDivergence.NeedsIframeScripting),
        new("dom/nodes/node-realm-preserved-across-frameless-adoption.html", "*", WptDivergence.NeedsIframeScripting),

        // ---------------------------------------------------------------- DOMTokenList: the token validation, the indexed access and the iteration
        // DOMTokenList's validation and its indexed access
        new("dom/lists/DOMTokenList-coverage-for-attributes.html", "a.relList in http://www.w3.org/1998*", WptDivergence.NeedsTriage),
        new("dom/lists/DOMTokenList-coverage-for-attributes.html", "a.relList in http://www.w3.org/2000*", WptDivergence.NeedsTriage),
        new("dom/lists/DOMTokenList-coverage-for-attributes.html", "iframe.sandbox*DOMTokenList.", WptDivergence.NeedsTriage),
        new("dom/lists/DOMTokenList-coverage-for-attributes.html", "link.sizes*DOMTokenList.", WptDivergence.NeedsTriage),
        new("dom/lists/DOMTokenList-coverage-for-attributes.html", "output.htmlFor*DOMTokenList.", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a member of a DOM interface the bindings do not have
        // a member of a DOM interface the bindings do not have
        new("dom/nodes/Document-createEvent.https.html", "createEvent('DEVICEMOTIONEVENT*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('DEVICEORIENTATIONEVENT*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('DRAGEVENT*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('DeviceMotionEvent*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('DeviceOrientationEvent*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('DragEvent*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('STORAGEEVENT*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('StorageEvent*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('devicemotionevent*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('deviceorientationevent*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('dragevent*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('storageevent*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-replaceChildren.html", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "*itself", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "*tests", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "*toggleAttribute)", WptDivergence.NeedsTriage),
        new("dom/nodes/remove-unscopable.html", "*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- an XML document, and the two members that make one
        // an XML document, and the members that make one
        // This document's table is built inside its first test, and the builder calls createDocument — so
        // while the member was absent the file reported *two* tests and the rest were never registered at
        // all. They are registered now, and what they say is that the document a browser gets back is an
        // XMLDocument with no location, an ASCII-upper-cased encoding name and a content type taken from the
        // namespace, and that none of the three is reachable from what AngleSharp exposes. See Wpt/README.md.
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: metadata for*", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: characterSet aliases for*", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *,null", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: null,\"\",DocumentType node*", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/Document-constructor.html", "*interfaces", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/Element-tagName.html", "*)", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/Node-cloneNode-XMLDocument.html", "*", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/Node-cloneNode.html", "*createDocument", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/Node-isEqualNode.html", "documents*", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/attributes.html", "*-HTML document", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/processing-instruction-attributes.html", "*)", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/processing-instruction-attributes.html", "Distinct attribute name (source: html*", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/processing-instruction-attributes.html", "Distinct attribute name (source: xml-dom*", WptDivergence.NeedsXmlDocuments),
        new("dom/nodes/processing-instruction-attributes.html", "Processing*", WptDivergence.NeedsXmlDocuments),
        // Range and an Attr node: the constructor and attribute nodes reach these tests now, and two of
        // the twenty-six still fail on which refusal an out-of-root Attr earns and on whether a point in
        // one is in the range at all — DOM §5.5's own answers, not AngleSharp's.
        new("dom/ranges/Range-attribute-nodes.html", "comparePoint() with an Attr node not sharing the range's root throws WrongDocumentError", WptDivergence.NeedsTriage),
        new("dom/ranges/Range-attribute-nodes.html", "isPointInRange() with an Attr node sharing the range's root", WptDivergence.NeedsTriage),
        new("dom/ranges/Range-adopt-test.html", "*appendChild: Removing the only element in the range must collapse the range", WptDivergence.NeedsXmlDocuments),

        // ---------------------------------------------------------------- a collection's named and indexed properties, and its liveness
        // a collection's named and indexed properties
        new("dom/collections/HTMLCollection-as-prototype.html", "*", WptDivergence.NeedsTriage),
        new("dom/collections/HTMLCollection-own-props.html", "*", WptDivergence.NeedsTriage),
        new("dom/collections/HTMLCollection-supported-property-names.html", "*later", WptDivergence.NeedsTriage),
        new("dom/collections/HTMLCollection-supported-property-names.html", "Object*", WptDivergence.NeedsTriage),
        new("dom/collections/namednodemap-supported-property-names.html", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementById.html", "*string argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByClassName.html", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagName.html", "*collection", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagName.html", "*name", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagName.html", "HTML*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagName.html", "hasOwnProperty*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagNameNS.html", "*collection", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagNameNS.html", "*namespace", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagNameNS.html", "BODY*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-getElementsByTagNameNS.html", "getElementsByTagNameNS('\\**", WptDivergence.NeedsTriage),
        new("dom/nodes/DocumentFragment-getElementById.html", "Empty*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-children.html", "*1", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByClassName.html", "*collection", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagName-change-document-HTMLNess.html", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagName.html", "*collection", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagName.html", "*name", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagName.html", "HTML*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagName.html", "hasOwnProperty*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagNameNS.html", "*collection", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagNameNS.html", "*namespace", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagNameNS.html", "BODY*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagNameNS.html", "getElementsByTagNameNS('\\**", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-childNodes.html", "*.", WptDivergence.NeedsTriage),
        new("dom/nodes/NodeList-Iterable.html", "*entries method.", WptDivergence.NeedsTriage),
        new("dom/nodes/NodeList-Iterable.html", "*forEach method.", WptDivergence.NeedsTriage),
        new("dom/nodes/NodeList-Iterable.html", "*values method.", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes-namednodemap.html", "*names", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes-namednodemap.html", "setting an attribute should not overwrite the methods*", WptDivergence.NeedsTriage),
        new("dom/nodes/case.html", "createElementNS http://www.w3.org/1999*ABC", WptDivergence.NeedsTriage),
        new("dom/nodes/case.html", "createElementNS http://www.w3.org/1999*Abc", WptDivergence.NeedsTriage),
        new("dom/nodes/case.html", "getElementsByTagName *", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-03.htm", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-05.htm", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-06.htm", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-13.htm", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-14.htm", "*)", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-20.htm", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-21.htm", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-22.htm", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/getElementsByClassName-25.htm", "*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- the ARIA mixin's element-reflection half
        // the ARIA mixin's element-reflection half
        new("html/dom/aria-element-reflection-disconnected.html", "*", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "*DOM.", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "*empty.", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "*reference.", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "*scope.", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "Cross*", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "If*", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "Moving*", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "Passing*", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "Setting*", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "aria-*", WptDivergence.NeedsTriage),
        new("html/dom/aria-element-reflection.html", "shadow*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a (Node or DOMString) union parameter takes only a Node
        // a (Node or DOMString) union parameter takes only a Node
        new("dom/nodes/ChildNode-after.html", "*null as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-after.html", "*string as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-after.html", "*text as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-after.html", "*text as arguments.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-after.html", "*the argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-after.html", "*undefined as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-before.html", "*null as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-before.html", "*string as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-before.html", "*text as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-before.html", "*text as arguments.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-before.html", "*the argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-before.html", "*undefined as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*with empty string as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*with null as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*with one element and text as arguments.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*with one sibling of child and text as arguments.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*with only text as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*with undefined as an argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-append.html", "*a child.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-append.html", "*null as an argument, on a parent having no child.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-append.html", "*text as an argument, on a parent having no child.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-append.html", "*undefined as an argument, on a parent having no child.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-prepend.html", "*a child.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-prepend.html", "*null as an argument, on a parent having no child.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-prepend.html", "*text as an argument, on a parent having no child.", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-prepend.html", "*undefined as an argument, on a parent having no child.", WptDivergence.NeedsTriage),
        new("dom/ranges/StaticRange-constructor.html", "Construct static range with DocumentFragment container", WptDivergence.NeedsTriage),
        new("dom/ranges/StaticRange-constructor.html", "Construct static range with endpoints in disconnected trees", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- DOM's validate-and-extract, and the XML name productions
        // DOM's validate-and-extract, and the XML name productions
        new("dom/nodes/DOMImplementation-createDocumentType.html", "*:\", \"\", \"\") should work", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "*@\", \"\", \"\") should work", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"\"*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"#*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"$*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"%*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"&*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"'*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"(*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\")*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"1foo*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"@*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"\\**", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"^*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"`*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"edi*work", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"f@*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"{*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"}*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocumentType.html", "createDocumentType(\"~*", WptDivergence.NeedsTriage),
        new("dom/nodes/name-validation.html", "*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a name AngleSharp refuses that the standard allows
        // a name AngleSharp refuses that the standard allows, and the two refusals it still does not make
        new("dom/nodes/Document-createElement.html", "*<oo\") in HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*uffff\") in HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*ufffffoo\") in HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*uffffoo\") in HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*}\") in HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*}oo\") in HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "createElement(\"̀* HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* HTML document: null,\"\\ufffffoo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* HTML document: null,\"f<oo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* HTML document: null,\"f\\uffffoo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* HTML document: null,\"foo\\uffff\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* HTML document: null,\"foo}\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* HTML document: null,\"f}oo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* HTML document: null,\";foo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*0:a\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*<o\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*̀:a\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*;\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*;:a\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-insertBefore.html", "*, must throw TypeError.", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-replaceChild.html", "*a doctype should throw a HierarchyRequestError.", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-replaceChild.html", "*node should throw a HierarchyRequestError.", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "Basic*.", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: \"http://example.com/\",\"0:a\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: \"http://example.com/\",\"a:̀\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: \"http://example.com/\",\"a:;\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: \"http://example.com/\",\"f:o:o\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: \"http://example.com/\",\"fo<o\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: \"http://example.com/\",\"̀:a\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: \"http://example.com/\",\";:a\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: null,\"\\ufffffoo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: null,\"f<oo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: null,\"f\\uffffoo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: null,\"foo\\uffff\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: null,\"foo}\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: null,\"f}oo\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "* XML document: null,\";foo\",null", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a nullable DOMString answers the string "null"
        // a DOMString? parameter or attribute answers the string "null"
        new("dom/nodes/CharacterData-data.html", "*null", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-nodeValue.html", "Comment*", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-nodeValue.html", "ProcessingInstruction*", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-nodeValue.html", "Text*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- Range's own algorithms
        // Range's remaining algorithms
        new("dom/ranges/Range-comparePoint-2.html", "*2", WptDivergence.NeedsTriage),
        new("dom/ranges/Range-in-shadow-after-the-shadow-removed.html", "*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- an event interface this browser does not build
        // an event interface this package deliberately does not build
        new("dom/nodes/Document-createEvent.https.html", "*DeviceMotionEvent.", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/nodes/Document-createEvent.https.html", "*DeviceOrientationEvent.", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/nodes/Document-createEvent.https.html", "*DragEvent.", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/nodes/Document-createEvent.https.html", "*StorageEvent.", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/nodes/Document-createEvent.https.html", "*TouchEvent.", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('TOUCHEVENT*", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('TouchEvent*", WptDivergence.NeedsMoreEventInterfaces),
        new("dom/nodes/Document-createEvent.https.html", "createEvent('touchevent*", WptDivergence.NeedsMoreEventInterfaces),

        // ---------------------------------------------------------------- a document with no browsing context
        // a document with no browsing context, and what createHTMLDocument makes
        new("dom/nodes/DOMImplementation-createHTMLDocument-with-saved-implementation.html", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createHTMLDocument.html", "*\",\"\"", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createHTMLDocument.html", "*aliases", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createHTMLDocument.html", "*metadata", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createHTMLDocument.html", "*null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-constructor.html", "*aliases", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-constructor.html", "*metadata", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- the selector engine: escapes, :scope and :has
        // the selector engine's escapes, :scope and :has
        new("dom/nodes/Element-closest.html", "*div > :scope'", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-closest.html", "*invalid'", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-closest.html", "*scope)'", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "\"ab*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "\"�\"*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "\"�surrogate\"*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "*D800\"", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "*\\\"", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "*ns\"", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- a member the standard removed and this browser still has
        // a member the standard removed and this browser still has
        new("html/dom/historical.html", "*interface is removed", WptDivergence.NeedsTriage),
        new("html/dom/historical.html", "*styled", WptDivergence.NeedsTriage),
        new("html/dom/historical.html", "<applet*", WptDivergence.NeedsTriage),
        new("html/dom/historical.html", "document.*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- MutationObserver's records
        // MutationObserver's records
        new("dom/nodes/MutationObserver-document.html", "*parsing", WptDivergence.NeedsTriage),
        new("dom/nodes/MutationObserver-document.html", "parser*", WptDivergence.NeedsTriage),
        new("dom/nodes/MutationObserver-inner-outer.html", "outerHTML*", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- one assertion each
        // one assertion each; see Wpt/README.md
        new("dom/nodes/ChildNode-after.html", "*positions.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-before.html", "*positions.", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "Upper-case HTML*", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*:o\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*̀\",null", WptDivergence.NeedsTriage),
        // The members #3768 added, and what the corpus says about them once they are reachable. Each is
        // AngleSharp's: an Attr write does not carry its new value to the attribute observer, a parser-
        // inserted namespaced attribute records no prefix, and IChildNode.Replace converts its arguments
        // before it checks whether the child has a parent at all.
        new("dom/nodes/Attr-prefix.html", "Attr.prefix present (SVG)", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*on a parentless child with two elements as arguments.", WptDivergence.NeedsTriage),
        new("dom/nodes/ChildNode-replaceWith.html", "*with one sibling of child and child itself as arguments.", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-importNode.html", "*'deep' argument.", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "Basic functionality of getAttributeNode/getAttributeNodeNS", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "Basic functionality of setAttributeNode", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "setAttributeNode doesn't have case-insensitivity even with an HTMLElement 2", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "toggleAttribute should set the first attribute with the given name", WptDivergence.NeedsTriage),
        // createDocument's own share of the refusal defects the table already names: DOM's
        // validate-and-extract makes an empty prefix or an empty local part an InvalidCharacterError, and
        // AngleSharp answers a NamespaceError or nothing at all.
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *,\":foo\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *,\"foo:\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: null,\":\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: \"http://example.com/\",\"a:0\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-removeAttribute.html", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-setAttribute.html", "*namespace", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-tagName.html", "*ownerDocument", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-tagName.html", "tagName should not*.", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-cloneNode-svg.html", "cloned <use>'*", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-cloneNode.html", "*(frame)", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-cloneNode.html", "*createHTMLDocument", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-cloneNode.html", "*createProcessingInstruction", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-cloneNode.html", "*dir)", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-cloneNode.html", "*dl)", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-cloneNode.html", "*font)", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-isEqualNode.html", "*ID", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-isEqualNode.html", "*data", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-isEqualNode.html", "*value", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-nodeName.html", "*tagName.", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-replaceChild.html", "*node", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-replaceChild.html", "If*work.", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "* HTML document", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "First*", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "Own property correctness*", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "Setting*", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "setAttribute*name", WptDivergence.NeedsTriage),
        new("html/dom/access-key-label.html", "*invalid", WptDivergence.NeedsTriage),
    ];
}
