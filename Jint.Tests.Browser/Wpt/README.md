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

**This is not the only web-platform-tests number, and the other one is not a rival.** The census below is
*ours* — our driver, a vendored subset, an exclusion row per failure — and it is a **gate**. The
[scoreboard](https://github.com/sebastienros/jint/blob/wpt-scoreboard/docs/wpt-scoreboard.md) is
*upstream's*: a nightly `wpt run` over `wptserve`, across ten whole suites, and it gates nothing. It runs
the same corpus at the same pin, so a suite it reports that this table does not have is a suite nobody has
vendored here yet. Its plugin is [`tools/wpt-scoreboard/`](../../tools/wpt-scoreboard/README.md).

## The lane, suite by suite

| Suite | Documents | Synthesized | Tests | Not passing |
| --- | --- | --- | --- | --- |
| `dom/events/` | 56 | 9 | 544 | 20 |
| `dom/nodes/` | 159 | 0 | 4,796 | 1,465 |
| `dom/collections/` | 8 | 0 | 43 | 25 |
| `dom/lists/` | 5 | 0 | 189 | 5 |
| `dom/traversal/` | 13 | 0 | 52 | 9 |
| `dom/ranges/` | 17 | 0 | 82 | 25 |
| `html/dom/` | 5 | 0 | 85 | 31 |
| `html/webappapis/scripting/events/` | 12 | 0 | 37 | 5 |
| `html/webappapis/scripting/processing-model-2/` | 25 | 0 | 44 | 14 |
| `custom-elements/` | 16 | 0 | 510 | 257 |
| `custom-elements/parser/` | 8 | 0 | 20 | 11 |
| `custom-elements/reactions/` | 14 | 0 | 255 | 200 |
| `custom-elements/upgrading/` | 2 | 0 | 7 | 3 |
| **total** | **340** | **9** | **6,664** | **2,070** |

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

What `NeedsTriage` holds now is four things, each bounded and each named by the exclusion table:

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

The twenty-two `<a>`/`<area>` shapes of `Event-dispatch-single-activation-behavior.html` were the fifth, and
they pass now. What kept them red was not the page loop's scheduling but the fragment arm being gated on the
page's own load having returned and on the navigation gate being free: this file's tests run *during* the
parse, where neither is true, so every one of their fragment moves was queued as a whole navigation behind
the gate and landed after the two turns the file allows. The move is a same-document one exactly when the
request came from the document the page is showing, which is the question
[`Jint.Browser/Runtime/AGENTS.md`](../../Jint.Browser/Runtime/AGENTS.md#navigation-is-a-fetch-and-a-new-engine)
now says it asks.

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
3. **Two attribute writes reach neither notification channel**, and both are AngleSharp's: `setAttributeNS`
   and a write through an `Attr` node. `reactions/Attr.html` and part of `reactions/Element.html` are that,
   and [`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md) records each. `classList` was the
   third and is not any more: DOM §7.1's update steps are a plain set-an-attribute-value now, the same door
   `setAttribute` already came through, so `reactions/DOMTokenList.html`'s two "must not enqueue" rows pass.
4. **Members the binding does not have.** `Element.animate` and the whole ARIA reflection mixin: a test that
   reaches for one fails with `Property '…' of object is not a function` before it can say anything about a
   reaction. `reactions/AriaMixin-*.html` is ninety-six rows of exactly that, and `reactions/HTMLElement.html`
   is twenty-one. `toggleAttribute`, `setAttributeNode`, `getAttributeNode`, `insertAdjacentElement` and
   `replaceWith` were here too and are not any more ([#3768](https://github.com/sebastienros/jint/issues/3768)):
   seventeen rows of `reactions/ChildNode.html`, `Element.html` and `Node.html` pass with them, and the four
   that are left are an `Attr` write reaching the attribute observer without its value.
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

## What the DOM corpus says about this browser

`dom/nodes/`, `dom/collections/`, `dom/lists/`, `dom/traversal/`, `dom/ranges/` and `html/dom/` are the DOM
standard's own suites and HTML's DOM half — the corpus every other suite in this lane is written on top of.
They arrived together, 207 documents and 4,815 tests, and **2,024 of those tests do not pass**. That is a
much worse ratio than any suite already here, and it should be: `dom/events/` is one interface's dispatch,
where these are every member of every node interface.

The failures are **eighteen distinct causes**, and the table names each of them test by test. Ten of them
were filed as [#3765](https://github.com/sebastienros/jint/issues/3765)–[#3774](https://github.com/sebastienros/jint/issues/3774), and one was already open as [#3712](https://github.com/sebastienros/jint/issues/3712); the counts move as those are fixed.
Ordered by how many tests each accounts for:

| Tests | Documents | What it is |
| ---: | ---: | --- |
| 5 | 1 | [#3767](https://github.com/sebastienros/jint/issues/3767) **`DOMTokenList`** was the largest single cause this corpus found — 661 rows across six documents, more than a quarter of everything the DOM suites reported. DOM §7.1's mutating half is `Dom/Collections/DomTokenListMembers` now: the validation steps, `toggle`'s given-versus-not-given `force`, `replace`, `supports`, `item`'s `null`, the update steps and WebIDL's value iterator. What is left is five rows of one document: `sandbox`, `link.sizes` and `output.htmlFor` land on `DOMSettableTokenList`, an interface the standard merged away in 2016 and AngleSharp still carries a `[DomName]` for, and two `a.relList` rows are in namespaces HTML does not reflect `rel` in. `Element-classlist.html` passes all 1,420. |
| 540 | 13 | [#3771](https://github.com/sebastienros/jint/issues/3771) **A frame is never given an engine.** `iframe.contentDocument` is `null` and `iframe.onload` never arrives, so 488 of these are one line — `Document-createElement*.html` runs its whole table three times, once per document, and two of the three are frames. `NeedsIframeScripting`, the category this lane already had. |
| 50 | 6 | [#3768](https://github.com/sebastienros/jint/issues/3768) **Members the bindings do not have.** Ten of the eleven are there now — `replaceWith`, the five `Attr`-node spellings, `insertAdjacentElement`/`insertAdjacentText`, `toggleAttribute`, `hasAttributes`, `isSameNode`, `getAttributeNames`, `webkitMatchesSelector` and `NodeList`'s iterator helpers — and what is left of the 182 rows is not the members: it is `IChildNode.Replace`'s own algorithm (six rows of `ChildNode-replaceWith.html`) and four `Attr` writes whose reaction is handed a `null` value. **`replaceChildren` is the eleventh and is deliberately still absent**, and `XMLDocument` is still not a name; `Dom/AGENTS.md` argues both. |
| 165 | 17 | [#3766](https://github.com/sebastienros/jint/issues/3766) **An XML document, and the two members that make one.** `DOMImplementation.createDocument` is not on AngleSharp's `IImplementation` at all and `Document.createCDATASection` is not projected, so a processing instruction is not an element and `XMLDocument` is not a name. `NeedsXmlDocuments`, which is a scope decision rather than debt — and the most expensive one in this table, because `dom/common.js` calls `createCDATASection` at file scope and **31 documents therefore register no test at all**. |
| 107 | 28 | [#3772](https://github.com/sebastienros/jint/issues/3772) **A collection's named and indexed properties, and its liveness.** An empty name is a supported property name (`HTMLCollection-empty-name.html`, 7 rows), `getElementsByTagName` matches where the standard matches nothing (23 rows), and `namednodemap-supported-property-names.html` sees names a browser does not. The six `NodeList-static-length-getter-tampered*` documents are the same interface from a seventh angle and are not vendored, because a static `NodeList` re-reads its tampered `length` getter and each of them takes between 5.9 s and 18.8 s. |
| 65 | 3 | [#3773](https://github.com/sebastienros/jint/issues/3773) **The ARIA reflection mixin is not there.** `element.role`, `ariaLabel`, `ariaDescribedByElements` and the rest are `undefined`, which `custom-elements/reactions/AriaMixin-*.html` already said 96 times and `html/dom/aria-*.html` now says from the reflection side. |
| 80 | 5 | [#3769](https://github.com/sebastienros/jint/issues/3769) **A `(Node or DOMString)` union parameter takes only a `Node`.** `before`, `after`, `append`, `prepend` and `replaceWith` all accept a string in DOM §4.2.7; here a string is "parameter 1 is not of the expected type". `replaceWith` joined the row when the member arrived: eighteen of its twenty-four remaining assertions are the union and nothing else. |
| 50 | 13 | One assertion each: `Node.isEqualNode` compares data it should not, `Element.removeAttribute` removes one attribute of two, an attribute's order in `element.attributes` differs, `cloneNode` copies a `value` a browser leaves behind. |
| 49 | 2 | [#3772](https://github.com/sebastienros/jint/issues/3772) **The XML name productions are wrong.** `createDocumentType("edi:root", …)` and 43 of its siblings are refused as "Invalid character detected" where DOM's Name production allows them, and `name-validation.html` finds five code-point ranges refused in both directions. |
| 40 | 5 | [#3774](https://github.com/sebastienros/jint/issues/3774) **A refusal the standard requires and AngleSharp does not make.** `createElementNS(null, "a:b")` is a `NamespaceError` in DOM's validate-and-extract and no error at all here — `Dom/AGENTS.md` records it — and `createElement` refuses eight names DOM allows. |
| 21 | 4 | [#3712](https://github.com/sebastienros/jint/issues/3712) **A nullable `DOMString` answers the string `"null"`.** `createElementNS(null, …)` gives an element whose `namespaceURI` is `"http://www.w3.org/1999/xhtml"` and `node.nodeValue = null` reads back `"null"`, because the binding converts a `DOMString?` parameter with `TypeConverter.ToString`. It is the same conversion the custom-element corpus records for `getAttributeNS`, from the other side. |
| 20 | 5 | [#3772](https://github.com/sebastienros/jint/issues/3772) **`StaticRange` is not a name**, which is eleven rows of `StaticRange-constructor.html`, plus what is left of `Range`'s own algorithms: `comparePoint`, `extractContents` over a dynamic end, and a range whose shadow root has been removed. |
| 18 | 1 | **An event interface this browser does not build**, which is `NeedsMoreEventInterfaces` and the alias table `dom/events/EventTarget-dispatchEvent.html` already names: `DragEvent`, `StorageEvent`, `TouchEvent` and the two device events. |
| 11 | 4 | **A document with no browsing context still has a `location`**, `createHTMLDocument` gives it one child too few, and `characterSet` answers `"utf-8"` where the standard's encoding name is `"UTF-8"`. |
| 29 | 3 | [#3774](https://github.com/sebastienros/jint/issues/3774) **A refusal carries the wrong `DOMException`.** Ten `createElementNS` rows expect `NamespaceError` and get `InvalidCharacterError`, eighteen `createDocument` rows expect one for an empty prefix or an empty local part and get a `NamespaceError` or nothing at all, and `attributes.html` expects one for `b:` and gets none. This is the half [#3732](https://github.com/sebastienros/jint/pull/3732) did not reach: the name crosses correctly now, and *which* name a refusal picks is a separate question. |
| 10 | 2 | **The selector engine's escapes.** `#eof\` and a surrogate escape are refused as invalid selectors where CSS Syntax §4.3.7 defines them, and `:scope` inside `:has()` resolves to the wrong element. |
| 6 | 1 | **Members the standard removed are still here**, which is exactly what `html/dom/historical.html` exists to find. |
| 4 | 2 | **A `MutationObserver` record too few, or too many.** `classList.add` of an existing token reports one record where two are due, and an observer of the document itself never fires — both already recorded as AngleSharp divergences. |
| 2 | 1 | [#3774](https://github.com/sebastienros/jint/issues/3774) **`NodeIterator` retargets the wrong node.** `NodeIterator-removal-during-filtering.html` finds the pre-removing steps moving the reference to the removed node's sibling rather than to its predecessor. |

**Four documents did not terminate at all, and that was the finding this campaign put first.**
`TreeWalker-currentNode.html`, `TreeWalker-previousNodeLastChildReject.html`, `TreeWalker-traversal-reject.html`
and `TreeWalker-traversal-skip.html` each spun forever: AngleSharp's `TreeWalker.ToPrevious` never advanced the
sibling it was reading and never climbed to a parent, so `previousNode()` looped the moment the previous
sibling was not accepted outright — a filter answering `FILTER_REJECT` or `FILTER_SKIP`, or a `currentNode`
pointed outside the root beside a node `whatToShow` excludes. Nothing in this lane could bound that —
`BrowserOptions.MaxTaskDuration` is deliberately infinite, the driver's own 30 s deadline cannot interrupt a
page thread that never yields, and a node `whatToShow` excludes is `FILTER_SKIP` *without the page's filter
being called*, so the loop never re-entered the engine for a constraint to fire in — which is why an embedder
should read [#3765](https://github.com/sebastienros/jint/issues/3765) as a denial of service rather than as a
conformance gap. DOM §6.1's seven traversals are `Jint.Browser`'s own now
(`Dom/Views/DomTreeWalker`, and its file argues each loop's termination); the four documents are cases, all
seventeen of their tests pass, and `TreeWalker-basic.html`'s "Walk over nodes." passed with them.

**And two missing members were worth thirty-six documents.** `dom/common.js` is the fixture builder the
whole of `dom/ranges/` and half of `dom/traversal/` load; it calls `document.createCDATASection` on a
`new Document()` and `document.implementation.createDocument` two lines later, both before a single
`test()` runs, so twenty-four Range documents, three traversal documents and nine more under `dom/nodes/`
and `dom/events/` reported nothing at all. **Both members exist now**
([#3766](https://github.com/sebastienros/jint/issues/3766)), their not-vendored rows say so, and vendoring
the thirty-six is a change of its own: it moves this table's Documents and Tests columns, which the change
that fixes an engine deliberately does not — the same standing the four `dom/events/` documents that were
waiting on `document.createEvent` already have.

**Adding `createDocument` raised the ceiling, deliberately and once.** `DOMImplementation-createDocument.html`
builds its own table of 434 cases *inside its first test*, and the builder called the missing member — so
the file reported **two** tests and the other 432 were never registered at all. They are registered now,
348 of them fail, and `dom/nodes/`'s not-passing figure went from 2,000 to 2,333 with them. That is what
`JINT_WPT_BROWSER_CENSUS=update-raising-the-ceiling` is for, and it is used here for exactly that: the
failures are not new, only newly *counted*.

## What is not vendored, and why

`WptBrowserExclusions.NotVendored` is the enforced list; the shape of it is worth knowing because it is
different from the engine lane's. **Almost every row is a document that cannot produce a per-test report at all**
— a harness `ERROR` or `TIMEOUT` — which is what puts it there rather than in the exclusion table: a harness
error covers the whole file and no per-test exclusion can name it. The rest are the globs upstream's own
markers and this lane's directory rule earn, and the helper files of documents nothing here runs. They fall
into twenty-eight groups; the counts are rows rather than files, since several are globs. Ninety-four of
the rows belong to the six DOM suites, which is what a corpus about every member of every node interface
costs: half of them are one member reached at file scope.

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
| a `custom-elements/` finding, and one whose cause is spent | 2 | the parser's upgrade-instead-of-construct, and the file whose `unhandledrejection` the engine used to raise at the tracker's cadence rather than at the checkpoint (fixed; vendoring it is a change of its own) |
| a DOM sub-directory that is not a suite | 13 | `Document-contentType/`, `moveBefore/`, `insertion-removing-steps/`, `crashtests/`, `tentative/`, `unfinished/`, and five of `html/dom/`'s |
| a DOM marker, or not a document | 6 | `.window.js`, `.tentative.html` and `.sub.html` under the six new suites |
| an XML document | 6 | `.xhtml`, `.xht`, `.svg` and the three `.xml` fixture globs: a page here parses HTML |
| the WebIDL conformance harness, again | 2 | `html/dom/idlharness.https.html`, and one that needs an `RTCPeerConnection` |
| HTML's reflection suite | 5 | [#3770](https://github.com/sebastienros/jint/issues/3770); ten documents and their four helpers, 22,028 of 56,660 assertions fail because reflection is not implemented, and the smallest table that names them and no passing test is over four thousand rows. **Not** a time problem: 22.5 s for the set, 7.3 s for the largest |
| a DOM crash test or reftest | 5 | none loads `testharness.js` |
| a helper document beside its test | 5 | three frames, a fragment and an iframe body; a document under a suite would have to be a case |
| a DOM frame that runs script | 17 | `iframe.contentDocument` is `null` and `iframe.onload` never arrives, so each of these times out |
| a member reached at file scope | 30 | `createCDATASection` (31 documents, 24 of them `dom/ranges/`, through `dom/common.js`), `createDocument` (5) and `setAttributeNode` (1) |
| one DOM file each | 3 | a `SyntaxError` no `error` event carries to the harness, and two `MutationObserver` documents waiting for a record that never comes |
| too slow to be a case | 2 | the six `NodeList-static-length-getter-tampered*` documents and their helper: a static `NodeList` re-reads its tampered `length` getter, so each spends between 5.9 s and 18.8 s and one of them crossed the driver's 30 s deadline on a loaded machine |

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

The whole lane is **about forty seconds** — one `WptServer`, one `Browser`, and a fresh `BrowserContext` and
`Page` per document. It was ten seconds before the DOM suites, which multiplied the documents by two and a
half and the assertions by four. **No case comes within a factor of three of the driver's 30 s deadline**,
and keeping that true is why the six `NodeList-static-length-getter-tampered*` documents are not vendored:
the largest of them took 18.8 s idle and crossed 30 s on a loaded machine, and a case whose outcome depends
on the machine is exactly what the census exists to keep out. Nothing in it waits on a real clock except upstream's own harness timeout, and no document
reaches it: every case reports, which is the property `EveryVendoredDocumentIsAccountedFor` and the
minimum-test table together keep true.
