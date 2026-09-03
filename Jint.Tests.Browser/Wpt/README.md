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
| `dom/events/` | 50 | 9 | 533 | 42 |
| `html/webappapis/scripting/events/` | 12 | 0 | 37 | 5 |
| `html/webappapis/scripting/processing-model-2/` | 25 | 0 | 44 | 14 |
| **total** | **87** | **9** | **614** | **61** |

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

**The eleven defects this lane first recorded are fixed.** They were filed as
[#3686](https://github.com/sebastienros/jint/issues/3686) to
[#3695](https://github.com/sebastienros/jint/issues/3695) — `document.createEvent` and the document
constructors, `window.event`, the report of a script's exception, the compiled handler's shape and scope, the
body's window-forwarded handlers and `HTMLFrameSetElement`, the legacy UI-event initializers, the default
passive value, one activation behaviour per click, the detached control's silent toggle, and named access on
the window — and the three seams two of them needed in the engine are
[#3696](https://github.com/sebastienros/jint/pull/3696).

What `NeedsTriage` holds now is five things, each bounded and each named by the exclusion table:

1. **A `data:` URL is not fetched as a subresource.** A page navigates to one, so
   `<script src="data:text/javascript,…">` is the one shape of external script that never runs; three
   `processing-model-2/` documents are about exactly that script. The report site those documents test works,
   which their `<script src>` and inline siblings say.
2. **`script.src` does not reflect a URL.** HTML reflects it *as a URL*, so it answers the resolved absolute
   one; AngleSharp's `IHtmlScriptElement.Source` answers the raw attribute value, so four rows compare a
   correct absolute filename against the unresolved string the document wrote. It is AngleSharp's divergence
   and is recorded in [`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md).
3. **A DOM prototype carries no `@@unscopables`.** WebIDL puts one on the interface prototype object of every
   interface with an `[Unscopable]` member — `Element`'s and `Document`'s `append`, `prepend` and
   `replaceChildren` among them — and the generator emits none, because AngleSharp's metadata does not say
   which members are unscopable. `compile-event-handler-symbol-unscopables.html` never reaches its subject:
   it *writes* to `document[Symbol.unscopables]`.
4. **`window.customElements` is absent**, which one row of
   `compile-event-handler-lexical-scopes-form-owner.html` needs to define a form-associated custom element.
   The file's other three rows pass.
5. **A fragment navigation does not land inside two turns.** Following an `<a href="#x">` of the page's own
   URL now happens on the page loop and fires `hashchange` on the next turn — measured, and moved there from
   after the whole timer chain — but `Event-dispatch-single-activation-behavior.html` gives it two zero-delay
   turns and its twenty-two `<a>`/`<area>` shapes still do not see it. What is left is a question about the
   page loop's scheduling rather than about activation behaviour.

One further group left `NeedsTriage` for a category of its own. `document.createEvent`'s alias table names
five interfaces this package deliberately does not build — `DragEvent` and `ClipboardEvent` need a
`DataTransfer`, `StorageEvent` a storage area's change notification, `TouchEvent` a touch input, and the two
device events a sensor — so four rows of `EventTarget-dispatchEvent.html` are `NeedsMoreEventInterfaces`,
which names what would move them. And eight rows of `Event-dispatch-single-activation-behavior.html` moved to
`AssertsWhatNothingRequires`: the file's instrumentation is a `<form onsubmit>` handler and cannot tell an
activation behaviour from an ordinary bubble, and `submit` and `reset` both bubble.

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
| a cause that has gone | 4 | they met `document.createEvent` before a test could report; it exists now, and vendoring them moves the census's Documents and Tests columns, so it is a change of its own |
| a name this browser does not have | 3 | `customElements`, a `javascript:` URL, and one that read `window.event` before it existed |
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
