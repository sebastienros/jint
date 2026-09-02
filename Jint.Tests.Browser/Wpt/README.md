# The web-platform-tests browser lane

The `.any.js` lane hands a file to an engine. This one **loads a document**: a vendored `.html` is served by
`WptServer` at a real URL, a `Browser` navigates a fresh `Page` to it, and the document pulls in upstream's own
`resources/testharness.js` through a `<script src>` exactly as a browser does. The realm is a real `Window`
with a `document`, a `<div id=log>` and a `load` event, so the harness deciding what passed is upstream's and
there is nothing of Jint's between the corpus and the verdict.

**The corpus is vendored once.** Everything here runs out of `Jint.Tests/Wpt/Vendor/`, at the commit
[`Vendor/README.md`](../../Jint.Tests/Wpt/Vendor/README.md) names, byte-verified the way that file describes;
this project holds no copy of a vendored file. How the lane works — the results overlay, the synthesized
wrappers, the environment a document runs in, where a divergence goes — is
[`AGENTS.md`](AGENTS.md) beside this file. What follows is what the lane *found*.

## The lane, suite by suite

| Suite | Documents | Synthesized | Tests | Not passing |
| --- | --- | --- | --- | --- |
| `dom/events/` | 50 | 9 | 490 | 181 |
| `html/webappapis/scripting/events/` | 12 | 0 | 37 | 23 |
| `html/webappapis/scripting/processing-model-2/` | 25 | 0 | 38 | 34 |
| **total** | **87** | **9** | **565** | **238** |

*Measured on Windows.* **Documents** are `.html` files in this repository; **Synthesized** are the
`<name>.any.html` wrappers `WptServerWrappers` manufactures for a suite's `.any.js` files, which are bytes
nowhere. **Not passing** is a **ceiling**: a rise fails as a regression naming the suite and the size of it, a
fall fails as staleness, and the rewrite refuses to write a larger figure. Take the census with

```bash
# check the table (about ten seconds; it runs every document)
JINT_WPT_BROWSER_CENSUS=1 dotnet test Jint.Tests.Browser -c Release

# rewrite it from what the lane measures, then commit the diff
JINT_WPT_BROWSER_CENSUS=update dotnet test Jint.Tests.Browser -c Release
```

`JINT_WPT_BROWSER_CENSUS=update-raising-the-ceiling` is the one spelling that may write a *larger*
not-passing figure, for a corpus bump that genuinely arrives with new failures.

## What this corpus says about this browser

Eleven distinct defects, each recorded rather than fixed, because the change that first runs a suite must not
also be the change that moves the engine — otherwise nobody can tell which of the two a number came from.
Every one of them is a `NeedsTriage` row in `WptBrowserExclusions.All`, and **a non-zero count there is a list
somebody owes a fix for**.

1. **`document.createEvent` does not exist**, and it is the largest single cause in the lane: seventeen
   documents fail entirely on it and seven more in part.
   [DOM still requires it](https://dom.spec.whatwg.org/#dom-document-createevent) — deprecated, not removed —
   and half of `dom/events/` is written against it because the file predates the constructors. `new Document()`
   and `document.implementation.createHTMLDocument()` are the same gap seen from two more angles, and four
   further documents cannot report at all because they reach for one before a test could register —
   `Event-constants.html`, `Event-propagation.html` and `keypress-dispatch-crash.html` at file scope, and
   `Event-dispatch-detached-click.html` inside its only test.
2. **`window.event` does not exist.** [The legacy global](https://dom.spec.whatwg.org/#dom-window-event) is set
   for the duration of a dispatch and restored afterwards, and an inline handler that reads a bare `event`
   reads it. `event-global.html` fails all eight of its tests on it, and
   `Event-stopPropagation-cancel-bubbling.html` cannot report at all.
3. **An exception escaping a classic `<script>` is not reported at the global scope.**
   [Report an exception](https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception) fires an
   `error` event that reaches `window.onerror` and `<body onerror>`; here a script's parse error or runtime
   error becomes a `PageErrorKind.ScriptError` on the page's recorder and nothing else. Seventeen documents of
   `processing-model-2/` say so, every one on `assert_true: ran expected true got false`. The engine *does*
   fire that event for a timer callback, a listener and a microtask
   (`Jint/WebApi/GlobalEvents/GlobalEventTarget.cs`), so what is missing is the parser driver's own report and
   not the mechanism.
4. **The `error` event carries no column number.**
   [`ErrorEventInit.colno`](https://html.spec.whatwg.org/multipage/webappapis.html#erroreventinit) reaches the
   five-argument `onerror` as `undefined`. Only two rows can say so — every other document that would asks for
   it after the report of defect 3 that never arrives.
5. **A compiled handler is not the function HTML describes.** The function a handler content attribute
   [compiles to](https://html.spec.whatwg.org/multipage/webappapis.html#getting-the-current-value-of-the-event-handler)
   must be named for the attribute with the attribute's text as its body — `function onclick(event) {\nfoo\n}`
   — and is `function anonymous(event\n) {\nwith (document) …`, which is the scope chain leaking into the
   source text. Seven documents in `scripting/events/` assert around it: the text itself, what the chain
   resolves, which IDL members are handlers at all, that an invalid handler keeps its position, that a form
   owner is in the chain, and that a scripting-disabled document compiles none of them.
6. **`<body>`'s window-forwarded handlers reflect as an object**, not a function, and
   **`HTMLFrameSetElement` is not an interface object** — the other half of the same table.
7. **The legacy UI-event init methods are absent**: `initUIEvent`, `initMouseEvent`, `initKeyboardEvent`.
   Beside them, `new UIEvent(type, {view: notAWindow})` must throw a `TypeError` and does not.
8. **Passive is not the default for `touchstart`, `touchmove`, `wheel` and `mousewheel`** on the Window, the
   Document, the document element and the body
   ([DOM's default passive value](https://dom.spec.whatwg.org/#default-passive-value)), so their
   `preventDefault()` still cancels. Thirty-two rows of `passive-by-default.html`; the explicit
   `{passive: true}` and `{passive: false}` spellings pass, so the rule is what is missing and not the
   mechanism.
9. **One click runs more than one activation behaviour.**
   [A dispatch runs the behaviour of one element](https://dom.spec.whatwg.org/#eventtarget-activation-behavior),
   the nearest ancestor in the path that has one. A `<form>` nested in a `<form>` submits **both** here; and a
   nested `<a>` or `<area>` records nothing at all, because following a hyperlink to a fragment of the page's
   own URL is not something this activation host does. 108 of that file's 132 shapes are right, which is what
   makes the two wrong ones worth naming.
10. **A detached checkbox or radio fires `input` and `change` on `click()`**, where the pre-click activation
    steps fire them only for a connected control. The eight connected cases of the same file pass.
11. **`window` has no named properties.**
    [Named access on the Window object](https://html.spec.whatwg.org/multipage/nav-history-apis.html#named-access-on-the-window-object)
    is what makes `<iframe name=x>` and `<div id=x>` reach script as `x`, and a surprising number of wpt
    documents are written that way. It is not a category of its own because every document that meets it needs
    something else as well — a scripted frame, or a rendering — so it is recorded here and named in the
    `NeedsIframeScripting` comment of the exclusion table.

Two more things the corpus found are **not** defects and are recorded where they belong instead.
`Element.insertAdjacentText` is missing, which upstream's own result renderer calls — the overlay turns the
renderer off for its own reasons and `AGENTS.md` says so rather than letting that line hide it. And
`Event.timeStamp` is not coarsened, which `PerformancePrototype` records as a deliberate divergence; the one
document whose subject is that resolution is in the not-vendored table for the reason
`performance-timeline/webtiming-resolution.any.js` is out of the engine lane.

## What is not vendored, and why

`WptBrowserExclusions.NotVendored` is the enforced list; the shape of it is worth knowing because it is
different from the engine lane's. **Almost every row is a document that cannot produce a per-test report at all**
— a harness `ERROR` or `TIMEOUT` — which is what puts it there rather than in the exclusion table: a harness
error covers the whole file and no per-test exclusion can name it. The rest are the globs upstream's own
markers and this lane's directory rule earn, and the helper files of documents nothing here runs. They fall
into thirteen groups; the counts are rows rather than files, since several are globs.

| Why | How many | What it is |
| --- | --- | --- |
| a sub-directory that is not a suite | 2 | `dom/events/scrolling/` and `non-cancelable-when-passive/`, both layout |
| not a document, or a directory this PR does not vendor | 7 | `.window.js`, `.worker.js`, and four directories of the scripting tree |
| upstream's own markers | 5 | `.tentative.`, `-manual.`, and `.sub.html`, which needs a second origin to substitute into |
| `document.createEvent` and its kin | 4 | defect 1 above, met before a test could report |
| a name this browser does not have | 3 | `window.event`, `customElements`, a `javascript:` URL |
| a rendering | 4 | a CSS animation or transition event, and the coarse-clock assertion |
| a focus event that does not arrive | 1 | the one row that is a finding rather than an environment; see its reason |
| `testdriver.js` | 7 | campaign item C4 maps it onto the same dispatcher `Input.dispatchMouseEvent` reaches |
| a frame that runs script | 11 | a second global with a document in it, and the helper documents of those tests |
| the WebIDL conformance harness | 2 | `idl_test([…])`, which the engine lane declines for the same reason |
| the timer's string handler | 2 | `setTimeout("{", 10)`, which `TimerFunctions` documents declining |
| a second origin | 1 | `location.href.replace('://', '://www1.')`, and there is one origin here |
| a helper of a document above | 2 | the bodies two of those tests load |

The `testdriver.js` group is the one with an owner: seven documents become cases the day campaign item C4
lands, and that is what recording them by name is for.

## What runs, and what it costs

The whole lane is **about seven seconds** — one `WptServer`, one `Browser`, and a fresh `BrowserContext` and
`Page` per document. Nothing in it waits on a real clock except upstream's own harness timeout, and no document
reaches it: every case reports, which is the property `EveryVendoredDocumentIsAccountedFor` and the
minimum-test table together keep true.
