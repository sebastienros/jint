# The web-platform-tests browser lane

The `.any.js` lane hands a file to an engine. This one **loads a document**: a vendored `.html` is served by
`WptServer` at a real URL, a `Browser` navigates a fresh `Page` to it, and the document pulls in upstream's own
`resources/testharness.js` through a `<script src>` exactly as a browser does. The realm is a real `Window`
with a `document`, a `<div id=log>` and a `load` event, so the harness deciding what passed is upstream's and
there is nothing of Jint's between the corpus and the verdict. A document that drives input reaches
`test_driver`, and that goes through the same `InputDispatcher` the `Input` domain does — the
`testdriver-vendor.js` slot upstream ships empty is where the two meet.

**The corpus is vendored once.** Everything here runs out of `Jint.Tests/Wpt/Vendor/`, at the commit
[`Vendor/README.md`](../../Jint.Tests/Wpt/Vendor/README.md) names, byte-verified the way that file describes;
this project holds no copy of a vendored file. How the lane works — the results overlay, the synthesized
wrappers, the environment a document runs in, where a divergence goes — is
[`AGENTS.md`](AGENTS.md) beside this file. What follows is what the lane *found*.

## The lane, suite by suite

| Suite | Documents | Synthesized | Tests | Not passing |
| --- | --- | --- | --- | --- |
| `dom/events/` | 56 | 9 | 544 | 42 |
| `html/webappapis/scripting/events/` | 12 | 0 | 37 | 5 |
| `html/webappapis/scripting/processing-model-2/` | 25 | 0 | 44 | 14 |
| `custom-elements/` | 16 | 0 | 510 | 257 |
| `custom-elements/parser/` | 8 | 0 | 20 | 11 |
| `custom-elements/reactions/` | 14 | 0 | 255 | 219 |
| `custom-elements/upgrading/` | 2 | 0 | 7 | 3 |
| **total** | **133** | **9** | **1,417** | **551** |

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
4. **A form-associated custom element has no form owner.** `window.customElements` exists now, and the one
   row of `compile-event-handler-lexical-scopes-form-owner.html` that is left asserts that a compiled handler
   on an `<x-foo static formAssociated>` sees the *form's* lexical scope — which needs the element to be a
   form-associated element and take part in `form.elements`. This package records the flag and nothing
   consults it: there is no `ElementInternals`. The file's other three rows pass.
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

## What the custom element corpus says

`custom-elements/` and its `parser/`, `reactions/` and `upgrading/` sub-directories arrived with the
implementation of HTML §4.13, and **the shape of what is missing from that corpus is one sentence: most of
it is written against a second global.** `resources/custom-elements-helpers.js` gives it
`create_window_in_test`, which loads an iframe and resolves with its window, and `document_types()`, which
makes every assertion in five documents — this one, a `new Document()`, a `createHTMLDocument()`, an
iframe's and an XHR-fetched one. A page here parses child frames and gives none of them an engine, so a file
built on either waits for a load that never comes and reports nothing at all; thirty-seven documents are in
the not-vendored table for that reason alone, and they are the ones about adoption, cross-realm constructors
and the reaction queue.

What the rest found is six causes, and every exclusion in the four new suites is one of them:

1. **The parser upgrades a custom element where HTML constructs one.** AngleSharp creates a parser element
   with no notification to hook, so `<my-el>` in the markup is undefined until the driver's next script
   boundary. A page cannot see the difference — a script only ever sees the document at those boundaries —
   except in `parser/`, which is about exactly this: an element's attributes and children are already there
   when its constructor runs, and a constructor that constructs its own name before `super()` takes the
   element being upgraded rather than making a second one. That last file cannot report at all, so it is in
   the not-vendored table with the same reason.
2. **A namespaced attribute is not a namespace here.** `getAttributeNS(null, name)` answers `null` where a
   browser answers the value, because the binding converts a `DOMString?` *parameter* with
   `TypeConverter.ToString` and `null` becomes the string `"null"`; the same conversion is why
   `createElementNS(null, …)` and `setAttributeNS` behave as they do. It is the single biggest cause here:
   `attribute-changed-callback.html` asserts the callback's `actualValue` through `getAttributeNS`, so all
   thirteen of its rows fail on it, and `Document-createElementNS*.html` is the same conversion from the
   other side. Nothing about custom elements would move it; a nullable-string parameter in the generator
   would move all of it.
3. **Three attribute writes reach neither notification channel**, and all three are AngleSharp's:
   `classList`, `setAttributeNS` and a write through an `Attr` node. `reactions/DOMTokenList.html`,
   `reactions/Attr.html` and part of `reactions/Element.html` are that, and
   [`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md) records each.
4. **Members the binding does not have.** `toggleAttribute`, `setAttributeNode`, `getAttributeNode`,
   `insertAdjacentElement`, `replaceWith`, `DOMTokenList.replace`, `Element.animate` and the whole ARIA
   reflection mixin: a test that reaches for one fails with `Property '…' of object is not a function`
   before it can say anything about a reaction. `reactions/AriaMixin-*.html` is ninety-six rows of exactly
   that, and `reactions/HTMLElement.html` is twenty-one.
5. **AngleSharp's CSS serialization**, already recorded as a divergence: `reactions/CSSStyleDeclaration.html`
   compares the style attribute the reaction reported against `"color: blue;"` and gets
   `"color: rgba(0, 0, 255, 1)"`. The reaction fired; the value did not match.
6. **`builtin-coverage.html`'s two hundred and twenty rows** are the `'new'` and `createElement` halves of a
   table over every HTML local name. Its `innerHTML` and parser halves pass for all one hundred and eight
   tags, which is what says the customized-built-in path itself works.

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
into twelve groups; the counts are rows rather than files, since several are globs.

| Why | How many | What it is |
| --- | --- | --- |
| a sub-directory that is not a suite | 2 | `dom/events/scrolling/` and `non-cancelable-when-passive/`, both layout |
| not a document, or a directory this PR does not vendor | 7 | `.window.js`, `.worker.js`, and four directories of the scripting tree |
| upstream's own markers | 5 | `.tentative.`, `-manual.`, and `.sub.html`, which needs a second origin to substitute into |
| a cause that has gone | 4 | they met `document.createEvent` before a test could report; it exists now, and vendoring them moves the census's Documents and Tests columns, so it is a change of its own |
| a name this browser does not have | 2 | a `javascript:` URL, and one that read `window.event` before it existed |
| a rendering | 6 | a CSS animation or transition event, a pseudo-element, and the coarse-clock assertion |
| a focus event that does not arrive | 1 | the one row that is a finding rather than an environment; see its reason |
| a frame that runs script | 11 | a second global with a document in it, and the helper documents of those tests |
| the WebIDL conformance harness | 2 | `idl_test([…])`, which the engine lane declines for the same reason |
| the timer's string handler | 2 | `setTimeout("{", 10)`, which `TimerFunctions` documents declining |
| a second origin | 1 | `location.href.replace('://', '://www1.')`, and there is one origin here |
| a helper of a document above | 2 | the bodies two of those tests load |
| a `custom-elements/` directory or marker | 12 | `form-associated/` and `registries/` and `state/` (`ElementInternals` and scoped registries), `htmlconstructor/` (both of its documents build their subject in an iframe), the `.tentative.`/`.window.js`/`.xhtml`/`.svg` globs |
| a `custom-elements/` frame that runs script | 37 | `create_window_in_test` and `document_types()`; see the section above |
| `custom-elements/` needs `ElementInternals` | 5 | `attachInternals()` at file scope, so none of them registers a test |
| a `custom-elements/` crash test or reftest | 4 | neither loads `testharness.js`, so the driver's own deadline is what ends them |
| two `custom-elements/` findings | 2 | an `unhandledrejection` the engine raises at the tracker's cadence rather than at the checkpoint, and the parser's upgrade-instead-of-construct |

**The `testdriver.js` group is gone, which is what recording it by name was for.** Campaign item C4 mapped
upstream's automation API onto the same `InputDispatcher` the `Input` domain reaches, through the
`testdriver-vendor.js` slot upstream ships empty for a vendor to fill (`AGENTS.md` has the rules). Its seven
documents were then re-examined one at a time, and **five are cases now** —
`Event-dispatch-redispatch.html`, `focus-event-document-move.html`, `handler-count.html`,
`no-focus-events-at-clicking-editable-content-in-link.html` and `pointer-event-document-move.html`, ten tests
between them, all passing, none excluded. Two still cannot report, and neither reason was ever the driver's:
`Event-dispatch-on-disabled-elements.html` spends five of its nine tests waiting for CSS transition and
animation events on a disabled control, so it never completes and never reaches its testdriver-driven test at
all; and `click-on-absolute-pseudo.html` reads `event.pseudoTarget` and `element.pseudo('::after')`, which
need a pseudo-element model. Both are `a rendering` rows now. **The mapping found no new defect**, which is
the outcome running documents through an existing dispatcher should have.

The `customElements` row that used to sit in the "a name this browser does not have" group has gone the same
way: `window.customElements` exists, and `EventTarget-add-listener-platform-object.html` is a case again.

## What runs, and what it costs

The whole lane is **about ten seconds** — one `WptServer`, one `Browser`, and a fresh `BrowserContext` and
`Page` per document. Nothing in it waits on a real clock except upstream's own harness timeout, and no document
reaches it: every case reports, which is the property `EveryVendoredDocumentIsAccountedFor` and the
minimum-test table together keep true.
