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
| `dom/events/` | 56 | 9 | 544 | 15 |
| `dom/nodes/` | 168 | 0 | 8,115 | 1,030 |
| `dom/collections/` | 8 | 0 | 43 | 0 |
| `dom/lists/` | 5 | 0 | 189 | 5 |
| `dom/traversal/` | 13 | 0 | 52 | 0 |
| `dom/ranges/` | 17 | 0 | 82 | 4 |
| `html/dom/` | 13 | 0 | 39,552 | 529 |
| `html/webappapis/scripting/events/` | 12 | 0 | 37 | 5 |
| `html/webappapis/scripting/processing-model-2/` | 25 | 0 | 44 | 12 |
| `custom-elements/` | 16 | 0 | 510 | 247 |
| `custom-elements/parser/` | 8 | 0 | 20 | 11 |
| `custom-elements/reactions/` | 14 | 0 | 255 | 52 |
| `custom-elements/upgrading/` | 2 | 0 | 7 | 3 |
| **total** | **357** | **9** | **49,450** | **1,913** |

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
2. **A URL's fragment is dropped between the element and the error report.** This entry used to be
   "`script.src` does not reflect a URL" and four rows; [#3770](https://github.com/sebastienros/jint/issues/3770)'s
   reflection machinery took the member over and two of the four are cases now. The two that remain load
   `<script src="support/syntax-error.js#">` and the URL `onerror` reports has lost the trailing `#`, so what
   goes missing is the (empty) fragment rather than the resolution — a re-serialization on the
   script-loading path, and a change to `Jint.Browser/Runtime/` rather than to the binding.
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
iframe's and an XHR-fetched one. A child frame has a document and a window here and **no realm**
([#3771](https://github.com/sebastienros/jint/issues/3771)), and that changed what is missing rather than
removing it: `create_window_in_test` resolves with a real window now — `ChildFrameTests` runs the helper's own
shape and pins it — but the window's constructors are the page's, because the realm is shared. So a file that
only needs a second *window* could report, and a file that compares an element against the frame's own
`HTMLElement`, or adopts a node between realms, still cannot. Thirty-seven documents stay in the not-vendored
table, now for the narrower reason: they are the ones about adoption, cross-realm constructors and the
reaction queue. Re-vendoring against the window is a change of its own, because it moves the census's
Documents and Tests columns.

What the rest found is six causes, and every exclusion in the four new suites is one of them:

1. **The parser upgrades a custom element where HTML constructs one.** AngleSharp creates a parser element
   with no notification to hook, so `<my-el>` in the markup is undefined until the driver's next script
   boundary. A page cannot see the difference — a script only ever sees the document at those boundaries —
   except in `parser/`, which is about exactly this: an element's attributes and children are already there
   when its constructor runs, and a constructor that constructs its own name before `super()` takes the
   element being upgraded rather than making a second one. That last file cannot report at all, so it is in
   the not-vendored table with the same reason.
2. **A namespaced attribute was not a namespace here, and is now**
   ([#3712](https://github.com/sebastienros/jint/issues/3712)). `getAttributeNS(null, name)` answered `null`
   where a browser answers the value, because the binding converted a `DOMString?` *parameter* with
   `TypeConverter.ToString` and `null` became the string `"null"`; the same conversion was why
   `createElementNS(null, …)` and `setAttributeNS` behaved as they did. It was the single biggest cause
   here — `attribute-changed-callback.html` asserts the callback's `actualValue` through `getAttributeNS`,
   so every one of its rows failed on it, and the reactions helper reads its recorded values the same way.
   The generator reads the nullability from AngleSharp's own metadata now, and `reactions/` went from 200
   rows not passing to 68 with it.
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

All **43 assertions in the eight `dom/collections/` documents pass**, with no exclusions.
`HTMLCollection-as-prototype.html` now permits an inheriting receiver to assign its own property over
a supported name; the collection's named reads remain live.

`dom/nodes/`, `dom/collections/`, `dom/lists/`, `dom/traversal/`, `dom/ranges/` and `html/dom/` are the DOM
standard's own suites and HTML's DOM half — the corpus every other suite in this lane is written on top of.
Across the six of them there are 224 documents and 48,033 tests, and **1,568 of those tests do not pass**.
Those three figures are live and checked against the census. They arrived together as 207 documents and
5,247 tests with 1,532 not passing; those arrival figures are historical and deliberately not re-derived.

The table names each distinct cause test by test. Its two numeric columns and its order are generated from the
same browser-lane run as the census: a cause is a named group in `WptBrowserExclusions.Causes`, and each row
carries that name in an HTML comment this page does not render. The prose remains hand-written.

`JINT_WPT_BROWSER_CENSUS=1` checks the table and `=update` rewrites the numbers and order. Every failing test
must belong to exactly one cause, and every cause that reaches these six suites must have exactly one row.
Unlike the census's `Not passing` ceiling, both columns are equalities: a cause growing or shrinking means the
table needs to be regenerated.

| Tests | Documents | What it is |
| ---: | ---: | --- |
| 506 | 2 | **An obsolete element interface the pinned assemblies do not have.** `<dl>`, `<dir>`, `<font>` and `<frame>` each get an interface of their own from HTML and a plain `HTMLElement` from AngleSharp, so `compact`, `color`, `src` and their kind have nowhere to be reflected onto — putting them on `HTMLElement` would give the member to every element. `<frameset>` is the one of the family that is *not* here: `DomManualInterfaces` declares it by local name, so its `cols` and `rows` pass. <!-- cause: 4. an obsolete element interface AngleSharp does not have --> |
| 362 | 7 | [#3766](https://github.com/sebastienros/jint/issues/3766) **An XML document, and the members that make one.** `DOMImplementation-createDocument.html` contributes 218 rows and `processing-instruction-attributes.html` 137; the rest cover XML metadata, identity and Range adoption. `NeedsXmlDocuments` is a scope decision rather than untriaged debt. <!-- cause: an XML document, and the two members that make one --> |
| 316 | 9 | [#3771](https://github.com/sebastienros/jint/issues/3771) **A frame is never given its own realm.** The 195 XHTML and 88 XML `Document-createElement*` rows reach `doc.defaultView.DOMException`; the rest are the `node-realm-*`, `node-creation-realm`, `createEvent` and connectivity cases. `NeedsIframeScripting` names that missing environment. <!-- cause: a frame that runs script --> |
| 88 | 3 | **The Selectors-API table and selector-only element states.** The three newly vendored documents cover selector-error contracts, no-namespace selectors and `::slotted`; all 88 rows are `NeedsTriage`. <!-- cause: the Selectors-API table and selector-only element states --> |
| 71 | 17 | **One assertion each or one small family per document.** These cover conversion order, import/clone identity, attribute selection and ordering, element-name identity, node equality and `accessKeyLabel`; each pattern is kept separate where neighboring rows pass. <!-- cause: one assertion each --> |
| 53 | 6 | [#3774](https://github.com/sebastienros/jint/issues/3774) **A name AngleSharp refuses that the standard allows, plus required refusals it does not make.** The rows cover element creation, namespace validation and document insertion. <!-- cause: a name AngleSharp refuses that the standard allows --> |
| 50 | 2 | [#3772](https://github.com/sebastienros/jint/issues/3772) **DOM's current name-validation rules differ from the XML productions.** `createDocumentType` contributes 45 rows and `name-validation.html` five. <!-- cause: DOM's validate-and-extract, and the XML name productions --> |
| 26 | 11 | **Collection matching, identity and liveness differ.** The remaining rows cover namespace-aware tag queries, null-namespace identity, child-node collections, empty IDs, quirks class matching and related live reads; `dom/collections/` itself now passes whole. <!-- cause: a collection's named and indexed properties, and its liveness --> |
| 22 | 3 | **Members of DOM interfaces are absent.** The rows cover `ProcessingInstruction` attributes, `ChildNode` unscopables and event aliases that have no constructor. <!-- cause: a member of a DOM interface the bindings do not have --> |
| 18 | 1 | **An event interface this browser does not build.** `Document-createEvent.https.html` reaches `DragEvent`, `StorageEvent`, `TouchEvent` and the two device-event interfaces. <!-- cause: an event interface this browser does not build --> |
| 16 | 1 | **AngleSharp.Css refuses an unparseable media query, from inside `Element.setAttribute`.** `<style>` registers an attribute observer that assigns the sheet's `MediaList.mediaText`, whose setter throws where Media Queries §2.1 requires `not all`; the sixteen rows are the values it cannot parse and the member's other thirty tests pass. `Dom/divergences.md` records it. <!-- cause: 8. AngleSharp.Css refuses an unparseable media query --> |
| 8 | 2 | **The selector engine's escapes, `:scope` and `:has` differ.** `ParentNode-querySelector-escapes.html` contributes five rows and `Element-closest.html` three. <!-- cause: the selector engine: escapes, :scope and :has --> |
| 7 | 3 | **A document with no browsing context still has a `location`**, `createHTMLDocument` builds a different skeleton, and its encoding-name aliases differ. <!-- cause: a document with no browsing context --> |
| 6 | 1 | **Members the standard removed are still here**, which is exactly what `html/dom/historical.html` exists to find. <!-- cause: a member the standard removed and this browser still has --> |
| 5 | 2 | [#3712](https://github.com/sebastienros/jint/issues/3712) **A nullable `DOMString` answers the string `"null"`.** The remaining rows are `CharacterData.data` and `Node.nodeValue` writes. <!-- cause: a nullable DOMString answers the string "null" --> |
| 5 | 1 | [#3767](https://github.com/sebastienros/jint/issues/3767) **`DOMTokenList` has five remaining interface-shape differences.** They are the legacy `DOMSettableTokenList` surfaces and two namespace-specific `relList` rows. <!-- cause: DOMTokenList: the token validation, the indexed access and the iteration --> |
| 4 | 2 | **`MutationObserver` records differ.** A document observer misses parser mutations, and an `outerHTML` replacement reports a different record set. <!-- cause: MutationObserver's records --> |
| 3 | 1 | [#3769](https://github.com/sebastienros/jint/issues/3769) **A `(Node or DOMString)` union parameter takes only a `Node`.** Three `ChildNode.before` rows still reject strings. <!-- cause: a (Node or DOMString) union parameter takes only a Node --> |
| 2 | 1 | **A Range whose shadow root was removed has the wrong boundary behavior.** These are the two remaining `Range-in-shadow-after-the-shadow-removed.html` rows. <!-- cause: Range's own algorithms --> |

**Eight of HTML's ten reflection documents are cases, and five of the eight pass whole.** HTML §2.6.1's
reflection algorithms are `Jint.Browser/Dom/ReflectedAttribute.cs` and the members that take them are
`overrides.json`'s `reflected` list, so `reflection-misc.html` (4,877 assertions, 1,866 of them failing
before), `reflection-text.html` (10,202, 3,360 failing before), `reflection-sections.html` (5,604, 2,189
failing before), `reflection-tabular.html` (6,116, 3,552 failing before) and
`reflection-forms-weekmonth.html` (1,579, 420 failing before) pass with **nothing** excluded, and
`reflection-grouping.html` (5,358, 2,006 failing before), `reflection-metadata.html` (3,110, 1,218 failing
before) and `reflection-obsolete.html` (2,621, 1,483 failing before) have one cause each — and none of the
three causes is reflection.

120 rows did it, and the first fifteen were mostly the **global** attributes every element carries —
`dir`, `lang`, `tabIndex`, `autofocus`, `inputMode`, `enterKeyHint` — which is why `text` needed only eleven
rows of its own for 3,360 assertions. The rest are element-specific and that is what the remaining two
documents need: `enctype`, `preload`, `decoding` and their kind, one `reflected` row each. `metadata` took seven of those —
`link`'s `as`, `crossOrigin`, `referrerPolicy`, `charset` and `target`, and `meta`'s `media` and `scheme` —
plus one member that is deliberately not reflection at all: **`nonce` answers HTML §2.5.3's
`[[CryptographicNonce]]` slot**, whose setter writes the slot and leaves the content attribute alone, so a
`script[nonce]` selector cannot read back a nonce a header-delivered policy issued
(`Jint.Browser/Dom/CryptographicNonce.cs`).

`sections` took thirteen and cost the row model two things it could not say. Six of its members are on
**`Document` and reflect an attribute of another element** — §3.2.6.4's `document.dir` off the `html`
element, and §16.3.3's `fgColor`, `linkColor`, `vlinkColor`, `alinkColor` and `bgColor` off the `body`
element — so a row names a `target` now, and a document with no such element reads exactly as an absent
attribute and does nothing on setting, which is what the standard says of `dir`. Ten of them are
**`[LegacyNullToEmptyString]`**, where `el.text = null` writes `""` and not `"null"`; that is a flag on the
`DOMString` row rather than a fourteenth algorithm, because the getter is unchanged.

`obsolete` took eight, all on `<marquee>` — and found one defect in the shared implementation that every
unsigned reflected integer had: HTML says a value outside the range 0 to 2147483647 writes the attribute's
**default**, and WebIDL's `unsigned long` conversion is modulo 2³², so `el.hspace = 4294967295` really does
arrive as that number rather than as −1. The setter wrote it verbatim. Eight rows of `obsolete` found it and
`col.span`, `td.colSpan`, `input.size` and `textarea.cols` would all have carried it.

`tabular` took thirty-nine, the largest table of the seven and the first with a **clamped unsigned long** in
it: `col.span` and `colgroup.span` clamp to [1, 1000], `td.colSpan` and `th.colSpan` to the same, and
`rowSpan` to [0, 65534] — three ranges, each with a default of 1, none of which any CLR signature carries.
It also needed one `skip`: AngleSharp splits `<th>` and `<td>` into two interfaces HTML does not have, and
the header cell's own `scope` shadowed the reflected `HTMLTableCellElement.scope`, so `<th>` answered the raw
attribute value while `<td>` answered the enumeration.

`forms-weekmonth` took eleven, all on `<input>`, and one of them is HTML's only exception to URL reflection:
**`formAction` answers the element's node document's URL when the content attribute is missing or empty**
(§4.10.18.6), which is what a form posting to itself reads. `form.action` is the other member with that rule
and the row model can say it now. Two more are `<input>`'s `width` and `height`, whose *getters* are the
rendered image dimensions and are not reflection at all — but whose setters are, in as many words, so the
rows fix the half that is.

**Neither of the two causes left is reflection**, and both are the dependency. Four obsolete elements —
`<dl>`, `<dir>`, `<font>` and `<frame>` — get an interface of their own from HTML and a plain `HTMLElement`
from the pinned assemblies, so there is nowhere for `compact`, `color`, `src` and their kind to be reflected
onto; the divergence table records it and 506 rows name the tests. `<frameset>` is the one of the family that
is not in it, because `DomManualInterfaces` declares `HTMLFrameSetElement` by local name and its `cols` and
`rows` are reflected members over that. And `<style>`'s `media` cannot be *written* at all when the value is
not a media query AngleSharp.Css can parse — the exception comes out of `Element.setAttribute` itself,
through the attribute observer AngleSharp core registers, where Media Queries §2.1 requires an unparseable
query to be replaced by `not all`. Those 522 rows are the only failures this lane's `html/dom/` figure
gained, against 34,590 assertions it did not have before.

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
into twenty-eight groups; the counts are rows rather than files, since several are globs. Ninety of
the rows belong to the six DOM suites, which is what a corpus about every member of every node interface
costs: half of them are one member reached at file scope. **A twenty-ninth answer is not in this table at
all**: `WptBrowserExclusions.FrameBodies` names the documents that are vendored and served and never run,
which is what a fixture sitting beside the cases that load it needs — see below.

| Why | How many | What it is |
| --- | --- | --- |
| a sub-directory that is not a suite | 2 | `dom/events/scrolling/` and `non-cancelable-when-passive/`, both layout |
| not a document, or a directory this PR does not vendor | 7 | `.window.js`, `.worker.js`, and four directories of the scripting tree |
| upstream's own markers | 5 | `.tentative.`, `-manual.`, and `.sub.html`, which needs a second origin to substitute into |
| a cause that has gone | 4 | they met `document.createEvent` before a test could report; it exists now, and vendoring them moves the census's Documents and Tests columns, so it is a change of its own |
| a name this browser does not have | 2 | a `javascript:` URL, and one that read `window.event` before it existed |
| a rendering | 6 | a CSS animation or transition event, a pseudo-element, and the coarse-clock assertion |
| a focus event that does not arrive | 1 | the one row that is a finding rather than an environment; see its reason |
| a frame that runs script | 11 | a second **realm** with a document in it — a frame has a window and a document here, and nothing in it runs — and the helper documents of those tests |
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
| HTML's reflection suite, seven of ten | 9 | [#3770](https://github.com/sebastienros/jint/issues/3770); `reflection-misc.html`, `-text.html` and `-grouping.html` are cases now and the other seven need the per-element attribute table each of them tests, one `reflected` row per content attribute. **Not** a time problem: 22.5 s for the whole set, 7.3 s for the largest. The exclusion table's own comment carries the per-family measurement |
| a DOM crash test or reftest | 5 | none loads `testharness.js` |
| a helper document beside its test | 4 | three frames and a fragment; a document under a suite would have to be a case, and the fourth answer is the frame-bodies table below |
| a DOM frame that runs script | 14 | listed when a frame had neither a document nor a realm; it has a document now ([#3771](https://github.com/sebastienros/jint/issues/3771)) and each row is owed a re-examination against the half that is left. Three have had it: the selector documents are cases |
| a member reached at file scope | 30 | `createCDATASection` (31 documents, 24 of them `dom/ranges/`, through `dom/common.js`), `createDocument` (5) and `setAttributeNode` (1) |
| one DOM file each | 3 | a `SyntaxError` no `error` event carries to the harness, and two `MutationObserver` documents waiting for a record that never comes |
| too slow to be a case | 2 | the six `NodeList-static-length-getter-tampered*` documents and their helper: a static `NodeList` re-reads its tampered `length` getter, so each spends between 5.9 s and 18.8 s and one of them crossed the driver's 30 s deadline on a loaded machine |

**A document can also be vendored, served and never run.** `WptBrowserExclusions.FrameBodies` is that
third answer, and it exists because a document directly under a suite is a case — `WptCorpus.BrowserTestFiles`
never descends, so a helper lives under `resources/` or `support/` and a case does not. Upstream does not
always agree. `ParentNode-querySelector-All-content.html` sits beside the three documents that load it into a
frame and is a fixture with no `testharness.js` in it, so the only two answers this lane had were to run it
and time out or to leave it out of the corpus and lose every case that loads it. Now it is vendored, served
and not a case, and the table is held from both ends like every other one here: a row must name a document
the corpus really holds, directly under a suite this lane claims, and no row may also be a `NotVendored`
pattern — a path cannot be absent and served at once. It takes no minimum-test entry and appears in no census
column, because neither counts anything about a document that reports nothing; what holds it to its job is
the three cases that load it, which fail loudly if the frame they wait for never arrives.

**And that is what let the selector table in.** `Element-matches.html`, `Element-webkitMatchesSelector.html`
and `ParentNode-querySelector-All.html` are the whole of wpt's Selectors-API suite, run three times over —
through `matches()`, through its prefixed alias, and through `querySelector`/`querySelectorAll` in five
contexts (a document, an in-document element, a detached element, an empty element and a fragment). They were not vendored for
two reasons at once: the frame body above, and the fact that a frame had no document to be
([#3771](https://github.com/sebastienros/jint/issues/3771)). Both are answered, and the three documents bring
**3,313 tests, of which 3,225 pass** — and `dom/nodes/` grows from 4,802 tests to 8,115. The 88 that do
not pass are bounded to the patterns in the exclusion table. Some are selector-error contract differences:
an undeclared namespace and a relative selector are accepted, while an unclosed attribute selector raises
the wrong script-visible error. The others are matching differences for no-namespace selectors and
`::slotted`. They are `NeedsTriage`, for the reason that category
exists: the change that first runs a suite is not also the change that moves the engine. The older branch
named 518 failures; current `main` fixed 390 of them before the corpus landed, and the two-sided exclusion
check removed every stale row rather than preserving that historical result.

**One of them was the machine's answer rather than the browser's, and that is fixed rather than excluded.**
`:lang(en)` on an element with **no** inherited language matched on a host whose culture is English and did
not on one whose culture is invariant — the Windows and the Linux CI leg exactly, so four rows of this
document passed on one and failed on the other and no exclusion could name them on both. AngleSharp resolves
such an element through the browsing context's culture, and the context had none, so it took
`CultureInfo.CurrentCulture` off whichever thread was parsing. `ParserDriver` gives the context the
**engine's** culture now (`Options.Culture`, which itself defaults to the current culture, so nothing moves
for a host that sets none), and this lane pins its own to the invariant culture — a gate whose answer depends
on the runner's locale is not a gate. All four pass everywhere now; scoping the exclusion to an operating
system would have encoded the coincidence instead of removing it.

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
