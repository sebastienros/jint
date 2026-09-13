using Jint.Tests.Wpt;

namespace Jint.Tests.Browser.Wpt;

/// <summary>
/// The browser lane's four tables: what is deliberately not vendored, what is vendored and served but is
/// never a case, how many tests each case must at least produce, and which tests do not pass and why.
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
        // The fourth table is FrameBodies, and the two are opposites: a row here is a path the corpus does
        // not hold, and a row there is a path it holds and never runs. Nothing may be in both.
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
        // **All ten generated documents are cases**, 56,660 assertions, because HTML §2.6.1's reflection
        // algorithms are implemented (Jint.Browser/Dom/ReflectedAttribute.cs) and 191 `reflected` rows in
        // overrides.json state, per member, which of them it takes. Nine of the ten pass whole; the 16 rows
        // the tenth still needs are not reflection at all but the dependency — <style>'s `media`, which
        // AngleSharp.Css refuses from inside setAttribute.
        //
        // The issue that vendored them is #3770, and what kept them out was never that they are slow: the
        // whole set runs in about 22 s, the largest (reflection-embedded.html, 8,922 tests) in 7.3 s, well
        // inside the driver's 30 s deadline. It was the artefact — the smallest table of patterns covering
        // 22,028 failures was over four thousand rows, every one of them saying the same thing — and the
        // answer was to write the attribute tables one family at a time instead.
        //
        // Two files here are not part of that and stay out.
        ("html/dom/reflection-original.html", "the same suite in the aggregating spelling, which reports only failures rather than one test per assertion — a second answer to what reflection-*.html already say"),
        ("html/dom/elements-aria-enumerated.js", "the attribute table of aria-attribute-reflection-enumerated.tentative.html, which tests a proposal the specification has not adopted"),

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

        // ============================================================ html/semantics/selectors/pseudo-classes
        // HTML §4.16.3's own suite. Four of its files are out, and none of the reasons is a defect.
        ("html/semantics/selectors/pseudo-classes/autofill.html", "its two assertions are test_valid_selector, which is /css/support/parsing-testcommon.js - a helper root this corpus does not vendor - so the file throws at file scope and reports nothing at all"),
        ("html/semantics/selectors/pseudo-classes/*.window.js", "a .window.js script, whose generated wrapper this lane does not synthesize"),
        ("html/semantics/selectors/pseudo-classes/indeterminate-radio-group.html", "a reftest: it loads no testharness.js and is judged against a reference rendering, which this browser has no way to produce"),
        ("html/semantics/selectors/pseudo-classes/indeterminate-radio-group-ref.html", "the reference of the reftest above"),
    ];

    /// <summary>
    /// Documents this lane vendors and serves and never runs: the body a case loads into a frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A document directly under a suite is a case</b> — that is <c>WptCorpus.BrowserTestFiles</c>, which
    /// never descends, so a helper lives under <c>resources/</c> or <c>support/</c> and a case does not.
    /// Upstream does not always agree: <c>ParentNode-querySelector-All-content.html</c> sits beside the three
    /// documents that load it, and it is a fixture with no <c>testharness.js</c> in it, so running it as a
    /// case is a page that registers nothing and times out. Before this table the only two answers were
    /// exactly those — run it and time out, or leave it out of the corpus and lose every case that loads it.
    /// </para>
    /// <para>
    /// <b>So the third answer is this one, and its rule is a two-sided one like every other table here:</b> a
    /// row must name a document the corpus really holds, under a suite this lane claims, and no row may also
    /// be a <see cref="NotVendored"/> pattern — a path cannot both be absent and be served.
    /// <c>WptBrowserTestRunner.EveryVendoredDocumentIsAccountedFor</c> is what holds all three.
    /// </para>
    /// <para>
    /// It takes no <see cref="MinimumTests"/> entry and appears in no census column, because neither counts
    /// anything about a document that reports nothing. What holds it to its job is the case that loads it:
    /// the three selector documents fail loudly if the frame they wait for never arrives.
    /// </para>
    /// </remarks>
    internal static readonly (string Path, string Reason)[] FrameBodies =
    [
        ("dom/nodes/ParentNode-querySelector-All-content.html", "the fixture Element-matches.html, Element-webkitMatchesSelector.html and ParentNode-querySelector-All.html each load into a frame and run their whole table against"),
        ("html/semantics/selectors/pseudo-classes/focus-iframe.html", "the frame focus.html loads to assert that :focus does not match a focused element inside it"),
    ];

    /// <summary>
    /// The documents this lane opens on a <b>touch device</b>, because what they are about needs one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An environment, not an exclusion.</b> Touch detection is a client's decision here
    /// (<c>Jint.Browser/Runtime/TouchEmulation</c>), so a page is not a touch device unless somebody says so
    /// — and a document that guards its rows with
    /// <c>assert_implements_optional('ontouchstart' in document)</c> is asking exactly that question. Left
    /// alone it declines and reports <c>PRECONDITION_FAILED</c>, which is a row that measures nothing; run on
    /// a touch device it reports what it is really about. This is the lane's counterpart of upstream's own
    /// per-test preferences, and it is the same page seam a client has:
    /// <c>Page.SetTouchEmulationAsync</c>, applied before the navigation so the document parses in it.
    /// </para>
    /// <para>
    /// <b>Its rule is the two-sided one every table here has:</b> a row must name a document that is a case
    /// of a suite this lane claims, and — because a document run in the wrong environment is worse than one
    /// nobody configured — the row is only worth having while the document's rows really need it, which the
    /// exclusion discipline enforces from the other side: turn the emulation off and the six rows this
    /// retires go back to <c>PRECONDITION_FAILED</c> with nothing naming them, and the run fails.
    /// <c>WptBrowserTestRunner.EveryVendoredDocumentIsAccountedFor</c> holds the first half.
    /// </para>
    /// </remarks>
    internal static readonly (string Path, string Reason)[] TouchDocuments =
    [
        ("dom/nodes/Document-createEvent.https.html", "its six TouchEvent rows are guarded by assert_implements_optional(\"ontouchstart\" in document), which is the question touch emulation answers"),
    ];

    /// <summary>Whether this lane opens <paramref name="path"/> as a touch device.</summary>
    internal static bool NeedsTouchEmulation(string path)
    {
        foreach (var (document, _) in TouchDocuments)
        {
            if (string.Equals(document, path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

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
        ["html/semantics/embedded-content/the-img-element/Image-constructor.html"] = 5,
        ["html/semantics/embedded-content/the-img-element/nonexistent-image.html"] = 1,
        ["html/semantics/embedded-content/the-img-element/img-picture-ancestor.html"] = 4,
        ["html/semantics/embedded-content/the-img-element/update-the-source-set.html"] = 89,
        ["custom-elements/CustomElementRegistry-constructor-and-callbacks-are-held-strongly.html"] = 5,
        ["html/infrastructure/common-dom-interfaces/collections/htmlallcollection.html"] = 41,
        ["html/obsolete/requirements-for-implementations/other-elements-attributes-and-apis/document-all.html"] = 2,
        ["custom-elements/CustomElementRegistry-getName.html"] = 4,
        ["custom-elements/Document-createElementNS-customized-builtins.html"] = 3,
        ["custom-elements/Document-createElementNS-prefix-timing.html"] = 3,
        ["custom-elements/Document-createElementNS.html"] = 4,
        ["custom-elements/HTMLElement-constructor-customized-builtins.html"] = 2,
        ["custom-elements/HTMLElement-constructor.html"] = 12,
        ["custom-elements/attribute-changed-callback.html"] = 13,
        ["custom-elements/builtin-coverage.html"] = 444,
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
        // Three variants, three cases: the file reads `location.search` and hangs its two tests
        // off the target the query names.
        ["dom/events/handler-count.html?document"] = 2,
        ["dom/events/handler-count.html?element"] = 2,
        ["dom/events/handler-count.html?window"] = 2,
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
        ["dom/nodes/NodeList-static-length-getter-tampered-1.html"] = 1,
        ["dom/nodes/NodeList-static-length-getter-tampered-2.html"] = 1,
        ["dom/nodes/NodeList-static-length-getter-tampered-3.html"] = 1,
        ["dom/nodes/NodeList-static-length-getter-tampered-indexOf-1.html"] = 1,
        ["dom/nodes/NodeList-static-length-getter-tampered-indexOf-2.html"] = 1,
        ["dom/nodes/NodeList-static-length-getter-tampered-indexOf-3.html"] = 1,
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
        ["dom/nodes/Element-matches.html"] = 669,
        ["dom/nodes/ParentNode-querySelector-All.html"] = 1975,
        ["dom/nodes/Element-nextElementSibling.html"] = 1,
        ["dom/nodes/Element-previousElementSibling.html"] = 1,
        ["dom/nodes/Element-remove.html"] = 4,
        ["dom/nodes/Element-removeAttribute.html"] = 2,
        ["dom/nodes/Element-removeAttributeNS.html"] = 1,
        ["dom/nodes/Element-setAttribute-crbug-1138487.html"] = 1,
        ["dom/nodes/Element-setAttribute.html"] = 2,
        ["dom/nodes/Element-siblingElement-null.html"] = 1,
        ["dom/nodes/Element-tagName.html"] = 6,
        ["dom/nodes/Element-webkitMatchesSelector.html"] = 669,
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
        ["dom/ranges/Range-in-shadow-after-the-shadow-removed.html?mode=closed"] = 2,
        ["dom/ranges/Range-in-shadow-after-the-shadow-removed.html?mode=open"] = 2,
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
        ["html/dom/reflection-embedded.html"] = 8922,
        ["html/dom/reflection-forms-weekmonth.html"] = 1579,
        ["html/dom/reflection-forms.html"] = 8271,
        ["html/dom/reflection-grouping.html"] = 5358,
        ["html/dom/reflection-metadata.html"] = 3110,
        ["html/dom/reflection-misc.html"] = 4877,
        ["html/dom/reflection-obsolete.html"] = 2621,
        ["html/dom/reflection-sections.html"] = 5604,
        ["html/dom/reflection-tabular.html"] = 6116,
        ["html/dom/reflection-text.html"] = 10202,
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
        ["html/semantics/selectors/pseudo-classes/active-disabled.html"] = 5,
        ["html/semantics/selectors/pseudo-classes/checked.html"] = 3,
        ["html/semantics/selectors/pseudo-classes/checked-type-change.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/default.html"] = 2,
        ["html/semantics/selectors/pseudo-classes/dir.html"] = 3,
        ["html/semantics/selectors/pseudo-classes/dir-dynamic.html"] = 4,
        ["html/semantics/selectors/pseudo-classes/dir-html-input-dynamic-text.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/dir01.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/disabled.html"] = 7,
        ["html/semantics/selectors/pseudo-classes/enabled.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/focus.html"] = 5,
        ["html/semantics/selectors/pseudo-classes/focus-autofocus.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/indeterminate.html"] = 6,
        ["html/semantics/selectors/pseudo-classes/indeterminate-radio.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/indeterminate-type-change.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/inrange-outofrange.html"] = 6,
        ["html/semantics/selectors/pseudo-classes/inrange-outofrange-time-reversed.html"] = 4,
        ["html/semantics/selectors/pseudo-classes/inrange-outofrange-type-change.html"] = 2,
        ["html/semantics/selectors/pseudo-classes/invalid-after-clone.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/link.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/placeholder-shown-type-change.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/readwrite-readonly.html"] = 25,
        ["html/semantics/selectors/pseudo-classes/readwrite-readonly-type-change.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/required-optional.html"] = 6,
        ["html/semantics/selectors/pseudo-classes/required-optional-hidden.html"] = 1,
        ["html/semantics/selectors/pseudo-classes/valid-invalid.html"] = 30,
        ["html/semantics/selectors/pseudo-classes/valid-invalid-fieldset-disconnected.html"] = 2,
    };

    // ---------------------------------------------------------------- 8. AngleSharp.Css refuses an unparseable media query
    private static readonly WptExclusion[] _8AngleSharpCssRefusesAnUnparseableMediaQuery =
    [
        // HTML §4.2.6 makes <style>'s `media` a plain reflected DOMString, and DOM gives setAttribute no
        // failure mode at all for a name that is already valid. AngleSharp core registers an
        // IAttributeObserver for `media` on a <style> element that calls HtmlStyleElement.UpdateMedia, which
        // assigns the sheet's MediaList.MediaText, and AngleSharp.Css's setter is SetMediaText(value,
        // throwOnError: true) -- a DomException(Syntax) for a query list Media Queries §2.1 says must be
        // treated as `not all` instead. So a document that writes an unparseable media query gets an
        // exception out of `Element.setAttribute` itself, which is why the IDL half of the member fails with
        // it: the reflected setter is one setAttribute. Nothing in this package can move it without
        // swallowing a DOMException the binding is meant to surface; `Dom/divergences.md` records it.
        //
        // Sixteen rows and no `style.media: *` glob, because thirty of the member's tests pass: every value
        // AngleSharp.Css can parse -- "", " foo ", "true", "false", "NaN", "Infinity", "null" and the rest --
        // is written and read back correctly.
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to \" \\0*", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to 7", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to 1.5", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to \"5%\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to \"+100\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to \".5\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to object \"[object Object]\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: setAttribute() to \"\\0\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to \" \\0*", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to 7", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to 1.5", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to \"5%\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to \"+100\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to \".5\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to object \"[object Object]\"", WptDivergence.NeedsTriage),
        new("html/dom/reflection-metadata.html", "style.media: IDL set to \"\\0\"", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- 6. a frame that runs script
    private static readonly WptExclusion[] _6AFrameThatRunsScriptTheScriptingSuites =
    [
        // `eventhandler-cancellation.html` fires its events at `frames[0]`, which is an iframe's window. A
        // frame has a window since #3771 — but this file's frame is `<iframe>` with no `src`, and a frame
        // with no source is never asked for and so has no document and no window here, where HTML gives it an
        // initial `about:blank` one. `frames[0]` is therefore undefined and the file fails on it. Even given
        // one it would need the realm: the events it fires are meant to be cancelled in the frame's own
        // global. It is the NeedsIframeScripting group below by cause, and is here only because the file is
        // in another suite.
        new("html/webappapis/scripting/events/eventhandler-cancellation.html", "*", WptDivergence.NeedsIframeScripting),
    ];

    // ---------------------------------------------------------------- 7. a bubbling `submit` the file counts as an activation
    private static readonly WptExclusion[] _7ABubblingSubmitTheFileCountsAsAnActivation =
    [
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
    ];

    // ---------------------------------------------------------------- a frame that runs script
    private static readonly WptExclusion[] _aFrameThatRunsScriptCustomElements =
    [
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
    ];

    // ---------------------------------------------------------------- a rendering
    private static readonly WptExclusion[] _aRendering =
    [
        // `MouseEvent.offsetX` against a `body { margin: 8px }`, which is a used value and not a computed
        // one. Named access now carries it as far as the assertion, which is where no fix short of campaign
        // item C4's flat renderer moves it.
        new("dom/events/mouse-event-retarget.html", "*", WptDivergence.NeedsLayout),

        // ================================================================ custom-elements
        // What the custom element corpus found. Every one of these is a defect somebody owes a fix for:
        // `Wpt/README.md` groups them by cause and names each, and the groups below are that list in the
        // order the README gives it.
    ];

    // ---------------------------------------------------------------- the registry, the constructor and the two creation members
    private static readonly WptExclusion[] _theRegistryTheConstructorAndTheTwoCreationMembers =
    [
        // DOM's create-an-element sets the namespace prefix on the element the constructor produced, after
        // it returns; AngleSharp's `Prefix` has no setter, so the element is created carrying it instead and
        // a constructor reading `this.prefix` sees it one step early. That is the whole of what is left of
        // this document — its third test, which is about the prefix not leaking between two constructions,
        // passes. `Dom/divergences.md` records the trade, and the alternative was an element that lost its
        // prefix and its qualified `tagName` for good.
        new("custom-elements/Document-createElementNS-prefix-timing.html", "Autonomous custom element prefix is set after constructor returns", WptDivergence.NeedsTriage),
        new("custom-elements/Document-createElementNS-prefix-timing.html", "Reentrant construction does not leak prefix between instances", WptDivergence.NeedsTriage),

        new("custom-elements/HTMLElement-constructor-customized-builtins.html", "*", WptDivergence.NeedsTriage),

        // The eight rows of attribute-changed-callback.html that the whole-document entry used to cover are
        // green: create_attribute_changed_callback_log reads the value back with getAttributeNS(null, name),
        // which now answers the attribute rather than looking for a namespace spelled "null". These five are
        // what is left, and each is a different defect.
        new("custom-elements/attribute-changed-callback.html", "attributedChangedCallback must be enqueued for style attribute change by mutating inline style declaration", WptDivergence.NeedsTriage),
        new("custom-elements/attribute-changed-callback.html", "setAttributeNS and removeAttributeNS must enqueue and invoke attributeChangedCallback", WptDivergence.NeedsTriage),
        new("custom-elements/attribute-changed-callback.html", "setAttributeNode and removeAttributeNS must enqueue and invoke attributeChangedCallback for an SVG attribute", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the callbacks and when they run
    // Both rows are the parser's, and they are the same cause README.md's first custom-element cause names:
    // AngleSharp builds a parser element with no notification to hook, so an element written in the markup is
    // upgraded at the driver's next boundary instead of constructed with an empty JavaScript stack. The two
    // things this document asks about are what only that difference can answer -- a microtask checkpoint that
    // runs inside the constructor, and the HTMLUnknownElement a failed *synchronous* construction leaves.
    private static readonly WptExclusion[] _theCallbacksAndWhenTheyRun =
    [
        new("custom-elements/microtasks-and-constructors.html", "Microtasks evaluate immediately when the stack is empty inside the parser", WptDivergence.NeedsTriage),
        new("custom-elements/microtasks-and-constructors.html", "Microtasks evaluate immediately when the stack is empty inside the parser, causing the checks on no attributes to fail", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the parser
    private static readonly WptExclusion[] _theParser =
    [
        new("custom-elements/parser/parser-constructs-custom-element-in-document-write.html", "HTML parser must instantiate custom elements inside document.write", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-constructs-custom-element-synchronously.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-fallsback-to-unknown-element.html", "*", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-sets-attributes-and-children.html", "HTML parser must enqueue attributeChanged reactions", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-sets-attributes-and-children.html", "HTML parser must set the attributes or append children before calling constructor", WptDivergence.NeedsTriage),
        new("custom-elements/parser/parser-sets-attributes-and-children.html", "HTML parser should call connectedCallback before appending child nodes.", WptDivergence.NeedsTriage),
        new("custom-elements/parser/serializing-html-fragments-customized-builtins.html", "\"is\" value should be serialized even for an undefined element", WptDivergence.NeedsTriage),
        new("custom-elements/parser/serializing-html-fragments-customized-builtins.html", "\"is\" value should be serialized if the custom element has no \"is\" content attribute", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- one [CEReactions] member per file
    private static readonly WptExclusion[] _oneCEReactionsMemberPerFile =
    [
        new("custom-elements/reactions/Animation.html", "*", WptDivergence.NeedsTriage),
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
    ];

    // ---------------------------------------------------------------- a frame that runs script
    private static readonly WptExclusion[] _aFrameThatRunsScript =
    [
        // a second global with a document in it
        // Not the frame any more, and the twin of the same move in Document-createElementNS.html: a frame
        // has a window now, so what is left of these 49 is that `application/xhtml+xml` is parsed by the
        // HTML parser — the fixture never loads as XHTML and every row fails on that first assertion.
        new("dom/nodes/Document-createElement.html", "*XHTML document", WptDivergence.NeedsXmlDocuments),
        // Narrowed by the run: a frame has a window now, so the ten rows that only needed one pass.
        // What is left is the XML twins of the HTML rows above — the same arguments, refused or accepted
        // by the same two defects — so they are named the same way.
        new("dom/nodes/Document-createElement.html", "*(\"\\ufffffoo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f::oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f::oo:\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f:o:o\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f:oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f<oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f\\uffffoo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:0\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:_\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:fooெ\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo:ெ\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo\\uffff\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"foo}\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"fooெ:foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"f}oo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xml:foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xmlfoo:bar\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xmlns\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"xmlns:foo\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"̀\") in XML document", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElement.html", "*(\"̀foo\") in XML document", WptDivergence.NeedsTriage),
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
    ];

    // ---------------------------------------------------------------- a relList on a MathML <a> that no standard defines
    private static readonly WptExclusion[] _aRelListOnAMathMLAThatNoStandardDefines =
    [
        // The SVG half of this pair is gone: SVG 2 16.2 gives SVGAElement a rel/relList pair
        // (https://svgwg.org/svg2-draft/linking.html#InterfaceSVGAElement) and DomManualInterfaces declares
        // the interface by local name over AngleSharp's bare SvgElement, so
        // "a.relList in http://www.w3.org/2000/svg namespace should be DOMTokenList." passes.
        //
        // The MathML half is the test's own divergence, which is why it is AssertsWhatNothingRequires and
        // not debt. MathML Core's only interface is MathMLElement (https://w3c.github.io/mathml-core/#dom-and-javascript),
        // which includes GlobalEventHandlers, HTMLOrSVGElement and ElementCSSInlineStyle and declares no
        // rel and no relList; nothing else defines a member on a MathML <a> either. The file's own
        // testAttr() lists the MathML namespace beside the SVG one and asserts a DOMTokenList for both, so
        // it asks for a member no specification requires of anybody -- and on wpt.fyi the only browser with
        // recorded results for this file is 174 of 175, one row short, with every other row of the file
        // passing here. Answering it would mean this package inventing a MathML member.
        new("dom/lists/DOMTokenList-coverage-for-attributes.html", "a.relList in http://www.w3.org/1998*", WptDivergence.AssertsWhatNothingRequires),
    ];

    // ---------------------------------------------------------------- a member of a DOM interface the bindings do not have
    private static readonly WptExclusion[] _aMemberOfADOMInterfaceTheBindingsDoNotHave =
    [
        // a member of a DOM interface the bindings do not have
        // ProcessingInstruction has no attributes at all. The whole of
        // processing-instruction-attributes.html is the attribute surface WICG's declarative partial
        // updates proposal (https://github.com/WICG/declarative-partial-updates, which the document's own
        // <link rel=help> names) puts on a ProcessingInstruction: getAttribute, setAttribute,
        // removeAttribute, hasAttribute, hasAttributes, getAttributeNames and toggleAttribute, over a
        // parse of `data` that re-serializes on every write. AngleSharp models none of it and neither do
        // the bindings. It is not about XML documents, which is where these rows used to be: three of its
        // four sources are an HTML-document PI and a DOMParser XML document, and both work.
        new("dom/nodes/processing-instruction-attributes.html", "*)", WptDivergence.NeedsTriage),
        new("dom/nodes/processing-instruction-attributes.html", "Distinct attribute name (source: html*", WptDivergence.NeedsTriage),
        new("dom/nodes/processing-instruction-attributes.html", "Distinct attribute name (source: xml-dom*", WptDivergence.NeedsTriage),
        new("dom/nodes/processing-instruction-attributes.html", "Processing*", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- a tag query's namespace and local-name identity
    private static readonly WptExclusion[] _aTagQuerySNamespaceAndLocalNameIdentity =
    [
        // https://github.com/sebastienros/jint/issues/3949 - AngleSharp 1.8.1 preserves HTML local-name
        // case. The remaining three assertions concern a null-namespace <body> becoming an XHTML one
        // when appended to an HTML document; a query cannot recover the lost namespace.
        new("dom/nodes/Document-getElementsByTagNameNS.html", "Empty string namespace", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagName-change-document-HTMLNess.html", "*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-getElementsByTagNameNS.html", "Empty string namespace", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- DOM's validate-and-extract, and the XML name productions
    private static readonly WptExclusion[] _dOMSValidateAndExtractAndTheXMLNameProductions =
    [
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
        new("dom/nodes/name-validation.html", "Valid and invalid characters in createElement.", WptDivergence.NeedsTriage),
        new("dom/nodes/name-validation.html", "Valid and invalid characters in createElementNS and createDocument.", WptDivergence.NeedsTriage),
        new("dom/nodes/name-validation.html", "Valid and invalid characters in createDocumentType.", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- a name AngleSharp refuses that the standard allows
    private static readonly WptExclusion[] _aNameAngleSharpRefusesThatTheStandardAllows =
    [
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
        // The same fourteen argument tuples through createDocument, where each one is three rows rather
        // than one: the file also runs a "metadata for" and a "characterSet aliases for" test per tuple
        // whose expected exception is null, and all three die on the same refusal before any metadata is
        // read. The glob is the tuple, which is what makes it name exactly those three.
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *null,\";foo\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *null,\"f}oo\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *null,\"foo}\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *null,\"\\ufffffoo\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *null,\"f\\uffffoo\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *null,\"foo\\uffff\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *null,\"f<oo\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *\"http://example.com/\",\"fo<o\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *\"http://example.com/\",\"f:o:o\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *\"http://example.com/\",\"0:a\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *\"http://example.com/\",\"a:;\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *\"http://example.com/\",\"a:̀\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *\"http://example.com/\",\"̀:a\",null*", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *\"http://example.com/\",\";:a\",null*", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- Range's own algorithms
    private static readonly WptExclusion[] _rangeSOwnAlgorithms =
    [
        // A live range is adjusted by DOM's own remove steps whatever document the removed node is in.
        // AngleSharp keeps its ranges on the document, so a container moved into another document with
        // appendChild leaves the range behind and removing its only child no longer collapses it. The two
        // rows whose container never moves pass, which is what says the algorithm is right and the
        // bookkeeping is not.
        new("dom/ranges/Range-adopt-test.html", "*appendChild: Removing the only element in the range must collapse the range", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- a document with no browsing context
    private static readonly WptExclusion[] _aDocumentWithNoBrowsingContext =
    [
        // A document with no browsing context. The one row left is an implementation saved from an iframe
        // whose element has since been removed, which needs a frame that runs script of its own.
        new("dom/nodes/DOMImplementation-createHTMLDocument-with-saved-implementation.html", "*", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the selector engine: escapes, :scope and :has
    private static readonly WptExclusion[] _theSelectorEngineEscapesScopeAndHas =
    [
        // the selector engine's escapes, :scope and :has
        new("dom/nodes/Element-closest.html", "*div > :scope'", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-closest.html", "*scope)'", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "\"ab*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "\"�\"*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "*\\\"", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-escapes.html", "*ns\"", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the Selectors-API table and selector-only element states
    private static readonly WptExclusion[] _theSelectorsAPITableAndSelectorOnlyElementStates =
    [
        // Selectors-API runs the same table through matches(), its prefixed alias, and
        // querySelector/querySelectorAll in five contexts. Most of its old syntax divergences now pass;
        // these patterns are the remaining current-main failures, grouped only where the test names state
        // the same selector and outcome. The runner holds every pattern against passing and failing rows.
        new("dom/nodes/Element-matches.html", "*Undeclared namespace: ns|div*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-matches.html", "*Undeclared namespace: :not(ns|div)*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-matches.html", "*Attribute value selector, matching align attribute with value, unclosed bracket*", WptDivergence.NeedsTriage),

        new("dom/nodes/Element-webkitMatchesSelector.html", "*Undeclared namespace: ns|div*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-webkitMatchesSelector.html", "*Undeclared namespace: :not(ns|div)*", WptDivergence.NeedsTriage),
        new("dom/nodes/Element-webkitMatchesSelector.html", "*Attribute value selector, matching align attribute with value, unclosed bracket*", WptDivergence.NeedsTriage),

        new("dom/nodes/ParentNode-querySelector-All.html", "*Undeclared namespace: ns|div*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-All.html", "*Undeclared namespace: :not(ns|div)*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-All.html", "*Attribute value selector, matching align attribute with value, unclosed bracket*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-All.html", "*Namespace selector, matching div elements in no namespace only*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-All.html", "*Namespace selector, matching any elements in no namespace only*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-All.html", "*Slotted selector: ::slotted(foo)*", WptDivergence.NeedsTriage),
        new("dom/nodes/ParentNode-querySelector-All.html", "*Slotted selector (no matching closing paren): ::slotted(foo*", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- MutationObserver's records
    private static readonly WptExclusion[] _mutationObserverSRecords =
    [
        // MutationObserver's records
        new("dom/nodes/MutationObserver-document.html", "*parsing", WptDivergence.NeedsTriage),
        new("dom/nodes/MutationObserver-document.html", "parser*", WptDivergence.NeedsTriage),
        new("dom/nodes/MutationObserver-inner-outer.html", "outerHTML*", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- one assertion each
    private static readonly WptExclusion[] _oneAssertionEach =
    [
        // one assertion each; see Wpt/README.md. What is left after DOM 4.4's node equality, the two IDL
        // `deep = false` defaults and NamedNodeMap's supported property names moved into the bindings is
        // named below by what each row actually is.

        // ChildNode.before/after/replaceWith with the context object itself among the arguments. DOM
        // computes the viable sibling before it converts the nodes into one, so the removal the conversion
        // performs cannot invalidate it; AngleSharp's IChildNode members work the other way round and raise
        // NotFoundError. The remaining `before` rows belong to the union-parameter cause.
        // one assertion each; see Wpt/README.md
        // `getComputedStyle(applet, "").cssFloat` is "" where the standard requires the initial value
        // "none": `float` is not one of the ten properties Dom/Views/ResolvedStyle answers an initial value
        // for, and the cascade reports only what a sheet declared. Nothing about `<applet>` - the same read
        // of any element answers the same way, and Jint.Browser/AGENTS.md argues which ten.
        new("html/dom/historical.html", "*styled", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*:o\",null", WptDivergence.NeedsTriage),
        new("dom/nodes/Document-createElementNS.html", "createElementNS test in HTML*̀\",null", WptDivergence.NeedsTriage),
        // The members #3768 added, and what the corpus says about them once they are reachable. Each is
        // AngleSharp's: a parser-inserted namespaced attribute records no prefix, and IChildNode.Replace
        // converts its arguments before it checks whether the child has a parent at all.
        new("dom/nodes/Attr-prefix.html", "Attr.prefix present (SVG)", WptDivergence.NeedsTriage),
        // AngleSharp's: an Attr write does not carry its new value to the attribute observer, and a parser-
        // inserted namespaced attribute records no prefix.
        new("dom/nodes/Attr-prefix.html", "Attr.prefix present (SVG)", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "*itself", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "Basic functionality of getAttributeNode/getAttributeNodeNS", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "Basic functionality of setAttributeNode", WptDivergence.NeedsTriage),
        new("dom/nodes/attributes.html", "setAttributeNode doesn't have case-insensitivity even with an HTMLElement 2", WptDivergence.NeedsTriage),
        // createDocument's own share of the refusal defects the table already names: DOM's
        // validate-and-extract makes an empty prefix or an empty local part an InvalidCharacterError, and
        // AngleSharp answers a NamespaceError or nothing at all.
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *,\":foo\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: *,\"foo:\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: null,\":\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        new("dom/nodes/DOMImplementation-createDocument.html", "createDocument test: \"http://example.com/\",\"a:0\",null,\"INVALID_CHARACTER_ERR\"", WptDivergence.NeedsTriage),
        // Attribute selection and ordering. DOM keys the attribute list on (namespace, local name) and
        // selects for setAttribute/removeAttribute/getAttribute on the *qualified* name, so an element can
        // hold two attributes spelling the same qualified name in different namespaces and the first one
        // wins. AngleSharp collapses them, which is one defect showing up as a dozen assertions.

        // An SVG attribute must keep its prefix through a clone.
        new("dom/nodes/Node-cloneNode-svg.html", "cloned <use>'*", WptDivergence.NeedsTriage),

        // Four element interfaces the pinned assemblies declare no [DomName] for, so nothing could be
        // generated: <dir>, <dl>, <font> and <frame> are all plain IHtmlElement to AngleSharp, and each row
        // is `assert_true(typeName in window)`. The HTMLDListElement half of it is the cause the table
        // already names for reflection-grouping.html.

        // replaceChild: the pre-insert validity checks DOM makes before it touches the tree, and replacing
        // a node with itself.
        new("dom/nodes/Node-replaceChild.html", "*node", WptDivergence.NeedsTriage),
        new("dom/nodes/Node-replaceChild.html", "If*work.", WptDivergence.NeedsTriage),

        // The rest of the attribute-list defect above: an element cannot hold two attributes whose qualified
        // names are equal, so the first-set-wins reads and the own-property lists are short by one.

        // accessKeyLabel: AngleSharp answers the raw accesskey content attribute, where HTML's is a label
        // for the element's *assigned* access key -- a key combination this browser has no keyboard to
        // decide. `accesskey="s 0"` is two valid one-code-point tokens by the specification's own reading,
        // so the rule that makes this row pass is not one the standard states, and Chromium answers
        // undefined for the member altogether. Recorded rather than guessed at.
        new("html/dom/access-key-label.html", "*invalid", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the pseudo-classes suite: :dir()
    private static readonly WptExclusion[] _thePseudoClassesSuiteDir =
    [
        // :dir() asks for HTML §3.2.6.6's *directionality*: an inherited property whose `auto` value is
        // resolved from the first strong character of the element's text - of an input's or textarea's value for
        // those two. AngleSharp's DirFunctionState compares the argument with IHtmlElement.Direction, which
        // reflects the `dir` content attribute of that element alone, so an element declaring none matches
        // neither keyword: dir01.html asks for every element of an iso-8859-8 document and gets an empty list.
        new("html/semantics/selectors/pseudo-classes/dir.html", "':dir(rtl)' matches all elements whose directionality is 'rtl'.", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir.html", "':dir(ltr)' matches all elements whose directionality is 'ltr'.", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir.html", "':dir(ltr)' doesn't match elements not in the document.", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir01.html", "direction doesn't affect :dir()", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir-dynamic.html", "Dynamically changing dir, text on input element", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir-dynamic.html", "Dynamically changing dir, text on textarea element", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir-dynamic.html", "Dynamically changing dir, text on div element", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir-dynamic.html", "Dynamically changing dir, text on pre element", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/dir-html-input-dynamic-text.html", ":dir on <input> isn't altered by text children", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the pseudo-classes suite: a reversed range
    private static readonly WptExclusion[] _thePseudoClassesSuiteAReversedRange =
    [
        // What is left of the :in-range group once the selector asks for a candidate that has range
        // limitations and stops believing an unclamped range value. §4.10.5.4 gives the time state a
        // *periodic* domain: when min is greater than max the range wraps midnight, so a value is in range
        // when it is at or after min OR at or before max. AngleSharp's ValidityState compares against both
        // bounds unconditionally, so the whole of a reversed range reads as an underflow and an overflow at
        // once. That is its constraint-validation arithmetic rather than the selector's category test, and
        // element.validity reports it the same way, so it is not something this predicate can correct.
        new("html/semantics/selectors/pseudo-classes/inrange-outofrange-time-reversed.html", "':in-range' matches time inputs whose value is within a reversed range (>= min OR <= max)", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/inrange-outofrange-time-reversed.html", "':out-of-range' matches time inputs whose value is in the gap of a reversed range (> max AND < min)", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/inrange-outofrange-time-reversed.html", "Dynamic update from out-of-range to in-range in a reversed time range", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the pseudo-classes suite: a cloned constraint state
    private static readonly WptExclusion[] _thePseudoClassesSuiteAClonedConstraintState =
    [
        // What is left of the :valid/:invalid group once the pair asks whether the element is a candidate for
        // constraint validation and answers a fieldset from its descendants. This row is neither: the file
        // types into a control and sets maxLength to 0, and HTML §4.10.5.5's "suffering from being too long"
        // is conditional on the element's *dirty value flag*, which cloneNode has to copy along with the
        // value. AngleSharp's HtmlTextFormControlElement clones the custom validity error and not that flag,
        // so the clone reports valid where the original does not. element.validity says the same, so it is a
        // constraint-validation state rather than anything a selector can decide.
        new("html/semantics/selectors/pseudo-classes/invalid-after-clone.html", "Cloned invalid inputs / textareas with interactive changes get their validity state copied correctly", WptDivergence.NeedsTriage),
    ];

    // ---------------------------------------------------------------- the pseudo-classes suite: an opaque colour serialized as rgba()
    private static readonly WptExclusion[] _thePseudoClassesSuiteOpaqueColour =
    [
        // Not a selector at all: these seven rows read getComputedStyle(...).color and every one of them already
        // gets the colour the selector should produce. CSSOM serializes an opaque colour as rgb(r, g, b) and
        // AngleSharp.Css writes rgba(r, g, b, 1); Dom/divergences.md records why the process-global
        // CssColorValue.UseSpecSerialization switch is not flipped on every AngleSharp consumer's behalf. The
        // style rows that compare two computed values rather than a literal are unaffected and pass.
        new("html/semantics/selectors/pseudo-classes/checked-type-change.html", "Evaluation of :checked changes on input type change.", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/indeterminate-radio.html", ":indeterminate and input type=radio", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/indeterminate-type-change.html", "Evaluation of :indeterminate changes on input type change.", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/inrange-outofrange-type-change.html", "Evaluation of :out-of-range changes for input type change.", WptDivergence.NeedsTriage),

        // These three moved here from the selector groups whose predicates now answer correctly: the text
        // input is no longer :in-range, the submit button is no longer :placeholder-shown and the hidden
        // input is no longer :read-write, so each row gets the colour it asks for in the spelling it does
        // not - "rgba(255, 0, 0, 1)" where it compares against the literal "rgb(255, 0, 0)".
        new("html/semantics/selectors/pseudo-classes/inrange-outofrange-type-change.html", "Evaluation of :in-range changes for input type change.", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/placeholder-shown-type-change.html", "Evaluation of :placeholder-shown changes for input type change.", WptDivergence.NeedsTriage),
        new("html/semantics/selectors/pseudo-classes/readwrite-readonly-type-change.html", "Evaluation of :read-write and :read-only changes for input type change.", WptDivergence.NeedsTriage),

        // And a fourth, for the same reason and from the :required group: the hidden input is in neither
        // class now, so its sibling gets the "rgb(255, 0, 0)" the file asks for, spelled "rgba(255, 0, 0, 1)".
        // The document's other three assertions, which are what the predicate was wrong about, pass.
        new("html/semantics/selectors/pseudo-classes/required-optional-hidden.html", "Evaluation of :required and :optional changes for input type change.", WptDivergence.NeedsTriage),
    ];

    /// <summary>The causes this corpus found, each one the exclusions that are it.</summary>
    /// <remarks>
    /// A cause was a comment until it became a value a run could count. Its unique name is the hidden key
    /// used by the corresponding README row; the row's prose remains hand-written.
    /// </remarks>
    internal static readonly WptCause[] Causes =
    [
        new("8. AngleSharp.Css refuses an unparseable media query", _8AngleSharpCssRefusesAnUnparseableMediaQuery),
        new("6. a frame that runs script: the scripting suites", _6AFrameThatRunsScriptTheScriptingSuites),
        new("7. a bubbling `submit` the file counts as an activation", _7ABubblingSubmitTheFileCountsAsAnActivation),
        new("a frame that runs script: custom elements", _aFrameThatRunsScriptCustomElements),
        new("a rendering", _aRendering),
        new("the registry, the constructor and the two creation members", _theRegistryTheConstructorAndTheTwoCreationMembers),
        new("the callbacks and when they run", _theCallbacksAndWhenTheyRun),
        new("the parser", _theParser),
        new("one [CEReactions] member per file", _oneCEReactionsMemberPerFile),
        new("a frame that runs script", _aFrameThatRunsScript),
        new("a relList on a MathML <a> that no standard defines", _aRelListOnAMathMLAThatNoStandardDefines),
        new("a member of a DOM interface the bindings do not have", _aMemberOfADOMInterfaceTheBindingsDoNotHave),
        new("DOM's validate-and-extract, and the XML name productions", _dOMSValidateAndExtractAndTheXMLNameProductions),
        new("a name AngleSharp refuses that the standard allows", _aNameAngleSharpRefusesThatTheStandardAllows),
        new("a tag query's namespace and local-name identity", _aTagQuerySNamespaceAndLocalNameIdentity),
        new("Range's own algorithms", _rangeSOwnAlgorithms),
        new("a document with no browsing context", _aDocumentWithNoBrowsingContext),
        new("the selector engine: escapes, :scope and :has", _theSelectorEngineEscapesScopeAndHas),
        new("the Selectors-API table and selector-only element states", _theSelectorsAPITableAndSelectorOnlyElementStates),
        new("MutationObserver's records", _mutationObserverSRecords),
        new("one assertion each", _oneAssertionEach),
        new("the pseudo-classes suite: :dir()", _thePseudoClassesSuiteDir),
        new("the pseudo-classes suite: a reversed range", _thePseudoClassesSuiteAReversedRange),
        new("the pseudo-classes suite: a cloned constraint state", _thePseudoClassesSuiteAClonedConstraintState),
        new("the pseudo-classes suite: an opaque colour serialized as rgba()", _thePseudoClassesSuiteOpaqueColour),
    ];

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
    /// <b><see cref="WptDivergence.NeedsTriage"/> records bounded causes, not a count of exclusion rows.</b> The eleven
    /// defects this lane first recorded were filed as
    /// https://github.com/sebastienros/jint/issues/3686 to 3695 and are fixed; what is left is named in
    /// <c>Wpt/README.md</c>, one section per cause, and every one of them is bounded — a member AngleSharp
    /// reflects wrong, an <c>@@unscopables</c> object the binding does not emit, and a form-associated custom
    /// element.
    /// </para>
    /// <para>
    /// <b>The DOM suites made it much bigger, and every one of those causes is bounded.</b> They hold 226
    /// documents and 65,228 tests, of which 757 do not pass -- three figures <c>Wpt/README.md</c> generates
    /// and checks rather than states, and whose split <c>Wpt/README.md</c>'s "What the DOM corpus says about
    /// this browser" gives as thirteen causes with the count each accounts for. Ten families were filed as
    /// https://github.com/sebastienros/jint/issues/3765 to 3774 and one was already open as
    /// https://github.com/sebastienros/jint/issues/3712, so a row here that is not one of
    /// <see cref="WptDivergence.NeedsIframeScripting"/> or <see cref="WptDivergence.NeedsXmlDocuments"/> is a
    /// numbered debt rather than an unread one.
    /// </para>
    /// <para>
    /// <b>The Selectors-API table adds one bounded group.</b> Its 88 failing rows cover the selector-error
    /// contract, no-namespace selectors and <c>::slotted</c>. The
    /// runner still proves each pattern matches a failure and no passing test.
    /// </para>
    /// </remarks>
    internal static readonly WptExclusion[] All = Flatten();

    private static WptExclusion[] Flatten()
    {
        var all = new List<WptExclusion>();

        foreach (var cause in Causes)
        {
            all.AddRange(cause.Exclusions);
        }

        return all.ToArray();
    }

}
