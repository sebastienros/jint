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
| `dom/events/` | 56 | 9 | 548 | 11 |
| `dom/nodes/` | 168 | 0 | 8,115 | 735 |
| `dom/collections/` | 8 | 0 | 43 | 0 |
| `dom/lists/` | 5 | 0 | 189 | 1 |
| `dom/traversal/` | 13 | 0 | 52 | 0 |
| `dom/ranges/` | 17 | 0 | 84 | 2 |
| `html/dom/` | 15 | 0 | 56,745 | 18 |
| `html/infrastructure/common-dom-interfaces/collections/` | 1 | 0 | 41 | 0 |
| `html/obsolete/requirements-for-implementations/other-elements-attributes-and-apis/` | 1 | 0 | 2 | 0 |
| `html/webappapis/scripting/events/` | 12 | 0 | 37 | 2 |
| `html/webappapis/scripting/processing-model-2/` | 25 | 0 | 44 | 5 |
| `html/semantics/embedded-content/the-img-element/` | 4 | 0 | 99 | 0 |
| `html/semantics/selectors/pseudo-classes/` | 27 | 0 | 122 | 22 |
| `custom-elements/` | 16 | 0 | 513 | 9 |
| `custom-elements/parser/` | 8 | 0 | 20 | 11 |
| `custom-elements/reactions/` | 14 | 0 | 255 | 52 |
| `custom-elements/upgrading/` | 2 | 0 | 7 | 0 |
| **total** | **392** | **9** | **66,916** | **868** |

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

**A case is a document *at one variant*, and that is why `Tests` can move without `Documents` moving.**
A document may carry [`<meta name="variant" content="?query">`](https://web-platform-tests.org/writing-tests/testharness.html#variants)
elements; upstream's manifest then makes one test per declaration, at the document's path with that
string appended, and the document reads which one it is out of `location.search`. This lane enumerates
them the same way and names a case exactly as the manifest names it —
`dom/ranges/Range-in-shadow-after-the-shadow-removed.html?mode=open` — so an exclusion, a minimum-test
entry and a cause all key on the *case*, and there is deliberately no spelling that means every variant:
two variants are two runs, and a divergence measured in one is not evidence about the other. Two vendored
documents declare any, and both pass at every one of their five variants between them:
`dom/events/handler-count.html` (`?document`, `?window`, `?element`) and
`dom/ranges/Range-in-shadow-after-the-shadow-removed.html` (`?mode=closed`, `?mode=open`). The engine
lane's answer is the opposite one and is not a contradiction: `// META: variant=` sharding is ignored
there because one unsharded run of a `.any.js` file is the union of its `?1-1000` shards, while a query a
document branches on selects *different* tests that no single run is the union of.

## What this corpus says about this browser

**The eleven defects this lane first recorded are fixed.** They were filed as
[#3686](https://github.com/sebastienros/jint/issues/3686) to
[#3695](https://github.com/sebastienros/jint/issues/3695) — `document.createEvent` and the document
constructors, `window.event`, the report of a script's exception, the compiled handler's shape and scope, the
body's window-forwarded handlers and `HTMLFrameSetElement`, the legacy UI-event initializers, the default
passive value, one activation behaviour per click, the detached control's silent toggle, and named access on
the window — and the three seams two of them needed in the engine are
[#3696](https://github.com/sebastienros/jint/pull/3696).

**Two more left `NeedsTriage` with the script-loading path, and both were about a URL.** A `data:` URL is a
subresource now: `ParserDriver` answers the `data` arm of
[scheme fetch](https://fetch.spec.whatwg.org/#scheme-fetch) out of
[Fetch §5.2's processor](https://fetch.spec.whatwg.org/#data-url-processor) rather than over a socket, so
`<script src="data:text/javascript,…">` runs and the three `processing-model-2/` documents about it pass.
And a subresource's response URL carries its fragment, which is what
[Fetch](https://fetch.spec.whatwg.org/#concept-response-url) says a response URL *is* — the fragment is left
out of the request-target and of nothing else — so `<script src="support/syntax-error.js#">` reports the `#`
that the same element's `src` already reflected, instead of losing it to a re-serialization. Five rows and
two; the same processor also replaced the page's *second*, open-coded `data:` decoder, so a navigation to
one now reads its `charset` and takes a payload `Convert.FromBase64String` refuses.

What `NeedsTriage` holds now is one thing, and the exclusion table names it: **a form-associated custom
element has no form owner.** `window.customElements` exists now, and the one row of
`compile-event-handler-lexical-scopes-form-owner.html` that is left asserts that a compiled handler on an
`<x-foo static formAssociated>` sees the *form's* lexical scope — which needs the element to be a
form-associated element and take part in `form.elements`. This package records the flag and nothing
consults it: there is no `ElementInternals`. The file's other three rows pass.

**`@@unscopables` was another of them, and it is gone.** WebIDL puts one on the interface prototype object of
every interface with an `[Unscopable]` member, AngleSharp's metadata cannot say which members those are, and
the answer is an `unscopables` list in `overrides.json` — the standard's half of the table, the way
`reflected` is — rather than a hand-edited `.g.cs`. DOM §4.2.8 and §4.2.9 mark every member of `ChildNode` and
`ParentNode`, which is seven names on `Element` and three or four on each of the other four interfaces that
include one; `compile-event-handler-symbol-unscopables.html` and `dom/nodes/remove-unscopable.html` pass
whole, nine rows between them. It matters outside a conformance suite for one reason: HTML compiles an inline
event handler with the element, its form owner and the document on the scope chain, so without it
`<div onclick="remove()">` calls the element's `remove()` instead of the page's own global.

The twenty-two `<a>`/`<area>` shapes of `Event-dispatch-single-activation-behavior.html` were the last of
them, and they pass now. What kept them red was not the page loop's scheduling but the fragment arm being
gated on the page's own load having returned and on the navigation gate being free: this file's tests run
*during* the parse, where neither is true, so every one of their fragment moves was queued as a whole
navigation behind the gate and landed after the two turns the file allows. The move is a same-document one
exactly when the request came from the document the page is showing, which is the question
[`Jint.Browser/Runtime/AGENTS.md`](../../Jint.Browser/Runtime/AGENTS.md#navigation-is-a-fetch-and-a-new-engine)
now says it asks.

The five interfaces `document.createEvent`'s alias table named that this package did not build — `DragEvent`,
`StorageEvent`, `TouchEvent` and the two device events — are built now, and the category they had
(`NeedsMoreEventInterfaces`) is empty. What unlocked them was separating two questions the old reason ran
together: whether the runtime ever *fires* such an event, and whether a page can *construct and dispatch* one.
Only the second is what the corpus tests and what the alias table needs, so each interface is built from its
own dictionary in full — `DragEvent` over the `DataTransfer` this package already has for file inputs,
`StorageEvent` with the `initStorageEvent` Web Storage still carries, `TouchEvent` with `Touch` and
`TouchList`, and the two device events with the two readings a motion event carries — while nothing fires any
of them and each class says so. `EventTarget-dispatchEvent.html` passes whole, and the six rows of
`Document-createEvent.https.html` the file itself guards with `assert_implements_optional('ontouchstart' in
document)` pass too: touch detection belongs to a client here (`Runtime/TouchEmulation`), so the lane opens
that one document as a touch device (`WptBrowserExclusions.TouchDocuments`, an environment rather than an
exclusion) and the row left in the file is its `TextEvent` one. And eight rows of
`Event-dispatch-single-activation-behavior.html` moved to `AssertsWhatNothingRequires`: the file's
instrumentation is a `<form onsubmit>` handler and cannot tell an activation behaviour from an ordinary
bubble, and `submit` and `reset` both bubble.

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

What the rest found is five causes, and every exclusion in the four new suites is one of them:

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

**`builtin-coverage.html` is green now**, all four hundred and forty-four rows of it. Its `'new'` and
`createElement` halves were two hundred and twenty-two rows of one defect, and the defect was not the
customized-built-in path at all — its `innerHTML` and parser halves always passed. Every one of those rows
failed on `customized.cloneNode().constructor`: DOM creates a clone with the element's **is value**, which is
a slot that `createElement(tag, { is })` and `new XY()` set without adding an attribute, and AngleSharp's
clone copies attributes and nothing else. So the two halves that set the slot cloned into a plain built-in
and the two that write the `is` attribute in markup did not. `custom-elements/upgrading/`'s own row was the
same rule read from the other side — a clone must follow the slot even when the `is` attribute says
something else — and it went with it.

Two more things the corpus found are **not** defects and are recorded where they belong instead.
`Element.insertAdjacentText` is missing, which upstream's own result renderer calls — the overlay turns the
renderer off for its own reasons and `AGENTS.md` says so rather than letting that line hide it. And
`Event.timeStamp` is not coarsened, which `PerformancePrototype` records as a deliberate divergence; the one
document whose subject is that resolution is in the not-vendored table for the reason
`performance-timeline/webtiming-resolution.any.js` is out of the engine lane.

## The image element, and the four documents that fit here today

`html/semantics/embedded-content/the-img-element/` arrived with HTML §4.8.4.3's image request, and **all four
of its documents pass with no exclusions**. `Image-constructor.html`'s five tests are the
`[LegacyFactoryFunction]`'s shape — the name, the prototype it shares with `HTMLImageElement`, the descriptor
of `Image.prototype`. `nonexistent-image.html` could not have passed before the model existed, because it
waits for an `error` event this browser had no image request to fire. And the two the source set added are
the ones that made it worth writing: **`update-the-source-set.html` is 89 tests over §4.8.4.3.6 itself** —
every descriptor form, the `sizes` lengths, a `<source>`'s `media` and `type`, and the tokenizer's own corner
cases — and `img-picture-ancestor.html` is the four about which `<picture>` an `<img>` belongs to. Both are
written entirely in `data:` URLs, which is why they can run in a corpus that vendors no image.

`update-the-source-set.html` is also the one that found two defects nothing else had asked about. A media
query that does not match the grammar is `not all`, so `not all and !` is `false` — `MediaQuery` answered
`true`, and `matchMedia` said so too. And a `srcset` candidate's URL is delimited by white space rather than
by a comma: `srcset="data:,b"` is **one** candidate whose URL contains a comma, and a `Split(',')` turned it
into `data:` — which is a URL, so nothing downstream could have noticed.

**Four documents out of a hundred and sixty is deliberate, and the reason is what the directory is made of.**
Most of the suite is a *rendering* test: `image-loading-lazy-*` alone is thirty-odd documents about whether an
image is inside a scrolling area, `available-images.html` and its siblings are reference tests compared
pixel-by-pixel, and a dozen more draw the loaded image into a `<canvas>` and read the bytes back. Of what is
left, `img.complete.html` and `naturalWidth-naturalHeight-width-height.html` are the two whose subject is
exactly this model — and each needs upstream's photographs, a 91 kB and a 380 kB JPEG, plus
`?pipe=trickle(d1)` server-side throttling for the assertion that `complete` cannot change inside a task.
This corpus vendors no binary at all today, and adding half a megabyte of them is a change of its own rather
than a passenger on the one that made the model exist. `img.complete.html` would also fail that one
assertion honestly: a subresource fetch is synchronous with the parse here (`Runtime/Parsing/AGENTS.md`), so
`complete` really can move inside the task that set `src`. `update-media.html`, `adoption.html` and
`relevant-mutations.html` are the source set's own siblings and are held back by the same missing bytes:
each names `/images/green-2x2.png` or its kind.

Three more are held back by a gap this browser has rather than by a missing environment, and
[`Dom/divergences.md`](../../Jint.Browser/Dom/divergences.md) carries a row for each. `invalid-src.html` and
`null-image-source.html` both wait for the `error` an `<img src="">` fires, and AngleSharp asks the resource
loader for nothing when it selects no source, so there is no request processor to hang that event on.
`non-active-document.html` asserts that an image in a document nothing is showing performs no load, and its
`<template>` case fails here: AngleSharp gives a template's contents no owner document of their own, so an
`<img>` inside one has a fully active node document and really is fetched. Its other two cases — a
`DOMParser` document and `createHTMLDocument` — pass, and the document is left out rather than vendored with
a `NeedsTriage` row, because it also needs a binary this corpus does not hold.

## The two smallest suites, and `document.all`

`html/infrastructure/common-dom-interfaces/collections/htmlallcollection.html` and
`html/obsolete/requirements-for-implementations/other-elements-attributes-and-apis/document-all.html` are the
whole of what upstream asks about `document.all`, and **all 43 of their assertions pass, with no exclusions**.
Between them they cover HTML §4.13.2.3 — the supported names, the "all"-named elements, the named lookup that
answers a live `HTMLCollection` when several elements match, `item(nameOrIndex)`, `namedItem(name)` and the
legacy caller, including that it is not a constructor and ignores its `this` — and ECMAScript Annex B.3.6's
`[[IsHTMLDDA]]` slot, which is what makes `typeof document.all` answer `"undefined"` and
`if (document.all)` take its else branch. `Dom/AGENTS.md` says which wrapper carries the slot and how the
override table selects it.

**One document is vendored out of each directory**, because a suite is a directory here and each of these two
is one. Their siblings are about other interfaces — `HTMLFormControlsCollection`, `HTMLOptionsCollection`,
`RadioNodeList`, `DOMStringList` and HTML's obsolete document colours — and vendoring one moves the census's
`Documents` and `Tests` columns, so it is a change of its own rather than a passenger on this one.

## What the pseudo-classes suite says about this browser

`html/semantics/selectors/pseudo-classes/` is HTML §4.16.3's own suite: one document per selector, run
against a page's real selector engine rather than against a table of strings. **27 documents, 122 tests, 22
of which do not pass**, and every failure is one of five bounded things AngleSharp does — only one of which
is still its `DefaultPseudoClassSelectorFactory`. That is the reason the suite is here: the page owns
`:target`, `:link`/`:visited`/`:any-link`, `:enabled`/`:disabled`, `:default`, `:open`/`:closed`,
`:valid`/`:invalid`, `:in-range`/`:out-of-range`, `:read-only`/`:read-write`, `:placeholder-shown`,
`:indeterminate`, `:focus`/`:focus-within`, `:active`, `:checked` and `:required`/`:optional`
(`Runtime/Parsing/PagePseudoClassSelectorFactory`), and nothing until this suite arrived
measured any of them.

None of these five has a row in the cause table above, and that is by construction: the table counts the
six DOM suites, and every one of these is a failure of this suite alone.

| Tests | What it is |
| ---: | --- |
| 9 | **`:dir()` compares its argument with the `dir` content attribute of that element alone.** Directionality is inherited and its `auto` value is resolved from text, so an element declaring no `dir` matches neither keyword. |
| 8 | **An opaque colour is serialized as `rgba(r, g, b, 1)`**, and these eight rows read `getComputedStyle().color` against a literal. Each already gets the colour the selector should produce; `Dom/divergences.md` records why the process-global switch is not flipped. |
| 3 | **A reversed range is an underflow and an overflow at once.** §4.10.5.4 gives the time state a periodic domain, so `min` greater than `max` wraps midnight; `ValidityState` compares against both bounds unconditionally. `element.validity` says the same, so it is not the selector's. |
| 1 | **A selectedness that never asks for a reset.** §4.10.10 makes `option.selected = true` run the select's selectedness setting algorithm, which leaves only the last selected option selected; AngleSharp's setter runs neither, so every option of a one-choice `<select>` can be selected at once. `option.selected` says the same. |
| 1 | **A cloned control loses its dirty value flag**, so `maxlength`'s "too long" state does not survive `cloneNode`. `element.validity` says the same. |

**Five of the eleven the suite arrived with are gone, and the two that are left of them are named for what
they now hold.** The page owns `:in-range`/`:out-of-range` and `:valid`/`:invalid` — both ask HTML's
"candidate for constraint validation" question AngleSharp folds into `CheckValidity()`, `:in-range` also
asks for range limitations, and a `fieldset` is decided from its descendants — and `:read-only`/`:read-write`,
`:placeholder-shown` and `:indeterminate`, which ask whether the attribute they are about *applies* to the
type state, answer for a `<textarea>`, and know about a radio button group and about a `progress` attribute
that is absent rather than empty. That retired 34 rows of this suite and one of
`dom/nodes/Element-closest.html`. **Three type-change documents did not become green and moved instead**:
their selectors answer correctly now and their remaining assertion compares a computed colour against a
literal, so they sit in the `rgba()` row above beside the four that were always there.

**`:focus` is the sixth, and it needed the page rather than the predicate.** AngleSharp answers `:focus` and
`:focus-within` from `IElement.IsFocused`, a flag nothing in this package sets — its own `DoFocus()` assigns
neither that flag nor `ActiveElement`, which is why `Events/FocusController` is the page's focus model — so
the selector could not see a focus every event and `document.activeElement` already agreed about. The
factory now reads that model, and HTML's focusing steps stop being confined to the displayed document: an
`element.focus()` inside a child navigable takes the focus out of the page, which is exactly what
`focus.html`'s last case asserts and what kept it passing while the other four did not.
`:focus-visible` is deliberately still AngleSharp's and still matches nothing — Selectors §9.4 makes it a
decision about drawing a focus indicator, which a browser with no rendering cannot make — and
`Dom/divergences.md` records that.

**And the last three predicates went with them, which leaves `:dir()` as the only selector cause in the
table above.**
`:active`'s five categories are four *formal activation states* — a keyboard notion this browser has no
key-held state for — and **being actively pointed at**, which is pure input: `BrowserEventRealm.MousePressTarget`
is already the element a trusted pointer press landed on and is cleared by its release, so the predicate is
that element, its ancestors, and the labeled control of a `label` among them. Nothing asks whether the
element is disabled and the standard does not either, which is the whole subject of `active-disabled.html`.
`:checked` stops answering for the historical `<menuitem>` and starts asking an input for its type state.
`:required`/`:optional` ask §4.10.5.3.4 whether the attribute *applies*, so an input outside its fifteen type
states is in neither class — which is what `required-optional-hidden.html` is about, and that document's row
moved to the `rgba()` group above rather than turning green, exactly as three type-change documents did
before it. One row of `checked.html` moved too, and to a cause that is not a selector at all: it turned out
that only two of that file's three rows were the `<menuitem>`.

**Four of the directory's files are not vendored and none of the reasons is a defect.** `autofill.html`'s two
assertions are `test_valid_selector`, which lives in `/css/support/parsing-testcommon.js` — a helper root this
corpus does not hold — so the file throws at file scope and reports nothing; `indeterminate-radio-group.html`
is a reftest and its `-ref.html` is the reference it is judged against; and the two `.window.js` files are the
glob every suite here has. `checked-001-manual.html` is covered by the lane's own `-manual.` marker.
`focus-iframe.html` *is* vendored, as a frame body: `focus.html` loads it to assert that `:focus` does not
match a focused element inside a frame.

## What the DOM corpus says about this browser

All **43 assertions in the eight `dom/collections/` documents pass**, with no exclusions.
`HTMLCollection-as-prototype.html` now permits an inheriting receiver to assign its own property over
a supported name; the collection's named reads remain live.

**Four collection algorithms are the standard's here rather than AngleSharp's**, because AngleSharp's own
answer is reachable through no seam: `getElementById` refuses the empty key DOM §4.9 says no element can
have, `getElementsByClassName` parses the ordered set and folds ASCII case only in quirks mode,
`NodeList.item` answers `null` past the end the way an indexed getter must, and `HTMLCollection.namedItem`
takes the first element in tree order whose ID or *HTML-namespace* `name` is the key. `Dom/divergences.md`
has the upstream half of each, and `Dom/AGENTS.md` says which override list carries it.

`dom/nodes/`, `dom/collections/`, `dom/lists/`, `dom/traversal/`, `dom/ranges/` and `html/dom/` are the DOM
standard's own suites and HTML's DOM half — the corpus every other suite in this lane is written on top of.
Across the six of them there are 226 documents and 65,228 tests, and **756 of those tests do not pass**.
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
| 299 | 9 | [#3771](https://github.com/sebastienros/jint/issues/3771) **A frame is never given its own realm.** The 195 XHTML and 71 XML `Document-createElement*` rows reach `doc.defaultView.DOMException`; the rest are the `node-realm-*`, `node-creation-realm`, `createEvent` and connectivity cases. `NeedsIframeScripting` names that missing environment. <!-- cause: a frame that runs script --> |
| 137 | 1 | **Members of DOM interfaces are absent.** The rows cover `ProcessingInstruction` attributes, `ChildNode` unscopables and event aliases that have no constructor. <!-- cause: a member of a DOM interface the bindings do not have --> |
| 88 | 3 | **The Selectors-API table and selector-only element states.** The three newly vendored documents cover selector-error contracts, no-namespace selectors and `::slotted`; all 88 rows are `NeedsTriage`. <!-- cause: the Selectors-API table and selector-only element states --> |
| 83 | 6 | [#3774](https://github.com/sebastienros/jint/issues/3774) **A name AngleSharp refuses that the standard allows, plus required refusals it does not make.** The rows cover element creation, namespace validation and document insertion. <!-- cause: a name AngleSharp refuses that the standard allows --> |
| 50 | 2 | [#3772](https://github.com/sebastienros/jint/issues/3772) **DOM's current name-validation rules differ from the XML productions.** `createDocumentType` contributes 45 rows and `name-validation.html` five. <!-- cause: DOM's validate-and-extract, and the XML name productions --> |
| 49 | 12 | **One assertion each or one small family per document.** These cover conversion order, import/clone identity, attribute selection and ordering, element-name identity, node equality and `accessKeyLabel`; each pattern is kept separate where neighboring rows pass. <!-- cause: one assertion each --> |
| 19 | 6 | [#3949](https://github.com/sebastienros/jint/issues/3949) **A tag query's namespace and local-name identity is lost before the query runs.** `createElementNS(HTML, "ABC")` exposes a lower-case `localName` and a null-namespace `<body>` becomes an XHTML one on insertion, so the qualified-name, exact-namespace, empty-namespace and HTMLness assertions cannot be answered from the tree the query is given; `case.html` contributes ten of the rows and the two `getElementsByTagName`/`NS` pairs the rest. The queries themselves are DOM's. <!-- cause: a tag query's namespace and local-name identity --> |
| 16 | 1 | **AngleSharp.Css refuses an unparseable media query, from inside `Element.setAttribute`.** `<style>` registers an attribute observer that assigns the sheet's `MediaList.mediaText`, whose setter throws where Media Queries §2.1 requires `not all`; the sixteen rows are the values it cannot parse and the member's other thirty tests pass. `Dom/divergences.md` records it. <!-- cause: 8. AngleSharp.Css refuses an unparseable media query --> |
| 7 | 2 | **The selector engine's escapes, `:scope` and `:has` differ.** `ParentNode-querySelector-escapes.html` contributes five rows and `Element-closest.html` two. <!-- cause: the selector engine: escapes, :scope and :has --> |
| 4 | 2 | **`MutationObserver` records differ**, and both halves are AngleSharp's. Its HTML parser inserts nodes without queueing a record, so a document observer hears nothing about the parse; and its `OuterHtml` setter inserts the replacement and then removes the element, which a page sees as two `childList` records where HTML's "replace this with fragment within parent" is one. <!-- cause: MutationObserver's records --> |
| 2 | 1 | **A live range is not adjusted once its container moves to another document.** The two `Range-adopt-test.html` rows whose container is moved with `appendChild` — AngleSharp keeps its ranges on the document, so DOM's remove steps reach none of them. The two rows whose container never moves pass. <!-- cause: Range's own algorithms --> |
| 1 | 1 | **A `relList` on a MathML `<a>` that no standard defines.** The file's own `testAttr()` asks for a `DOMTokenList` in the MathML namespace beside the SVG one, and MathML Core's only interface is [`MathMLElement`](https://w3c.github.io/mathml-core/#dom-and-javascript), which declares neither `rel` nor `relList`; nothing else defines one on a MathML element either, so this is `AssertsWhatNothingRequires` rather than debt. The SVG row passes now — [SVG 2 §16.2](https://svgwg.org/svg2-draft/linking.html#InterfaceSVGAElement)'s `SVGAElement` is one of `DomManualInterfaces`' local-name interfaces, and `Dom/divergences.md` records what is still missing. <!-- cause: a relList on a MathML <a> that no standard defines --> |
| 1 | 1 | **A saved implementation detached from its document answers null**, which needs a frame that runs script of its own. Every other half of this cause is gone: a document with no browsing context has no `location`, `characterSet`/`charset`/`inputEncoding` answer the Encoding Standard's name, and `createHTMLDocument` builds DOM's skeleton. <!-- cause: a document with no browsing context --> |

**The XML-document cause is gone, and it was four different things.** It arrived as a scope decision —
"a page here parses HTML, AngleSharp builds no XML document" — and by the time it was re-measured that
sentence had stopped being true: a frame parses XML ([#3873](https://github.com/sebastienros/jint/issues/3873)),
`XMLDocument` has its interface ([#3893](https://github.com/sebastienros/jint/issues/3893)), and
`createDocument` registers its own table. Running its seven documents one at a time
([#3766](https://github.com/sebastienros/jint/issues/3766) is the history) split its 346 rows four ways.
**190 were metadata this package owns and now produces**: `location` is null for a document with no
browsing context, `characterSet` answers the Encoding Standard's name with `charset` and `inputEncoding`
beside it, `contentType` is what the algorithm that made the document gave it, and `createElement` on a
document that is not an HTML one keeps the name's case. **137 are not about XML documents at all** —
`processing-instruction-attributes.html` needs a `ProcessingInstruction` attribute surface no standard has
yet, and three of its four sources are an HTML-document PI or a `DOMParser` XML document, both of which
work. **42 are the name refusals the table already named**, reached three times each. **The rest are
AngleSharp's**: node equality compares base URLs, a live range is not adjusted across documents, and the
HTML element factory lower-cases a local name it is handed. `NeedsXmlDocuments` still names something —
`application/xhtml+xml` is routed to the HTML parser, so 244 rows of `Document-createElement*` never see
the XHTML document they are about — and that, not the absence of an XML document, is what the category
means now.

**`html/dom/historical.html` is the file that tells three different things apart**, and "remove it" is the wrong
answer for two of them. `HTMLAppletElement`, `HTMLTableDataCellElement` and `HTMLTableHeaderCellElement` are names
the standard removed: the first was never declared here, and the other two were AngleSharp's split of
`HTMLTableCellElement` into two interfaces HTML does not have, so both are `excludedInterfaces` rows now and the
one member the split carried — `scope` — comes back as a `reflected` row on the interface HTML puts it on.
`document.applets` is the opposite: HTML §16.3 *keeps* it and defines it to answer an `HTMLCollection` whose
filter matches nothing, which is a member the binding had to gain rather than lose. And `<applet>` is neither —
the element still parses, and takes the `HTMLUnknownElement` every unlisted HTML name takes, which AngleSharp
cannot say because it builds a real `HtmlAppletElement`. Four of the file's six failures went with those, and a
fifth with `document.all` becoming a real `HTMLAllCollection`, whose supported names take a `name` attribute only
from one of the fourteen "all"-named elements and `applet` is not among them. The one that is left is somebody
else's cause — `cssFloat` is not one of the ten properties `ResolvedStyle` answers an initial value for — so the
row this file used to have is gone rather than shrunk.
**Half of the `Range` row is gone, and that half was never about `Range`.**
`Range-in-shadow-after-the-shadow-removed.html` takes its shadow-root mode from
`<meta name="variant" content="?mode=open">` and its closed sibling, and the lane used to serve the bare
path — so `mode` was `null`, `attachShadow({mode: null})` raised the `TypeError` WebIDL's enum conversion
owes a browser too, and both of the file's tests failed before either reached a `Range` at all. Running a
document once per declared variant is what removed them: four assertions over two cases now, all passing,
nothing excluded, and what is left under that cause is the `Range-adopt-test.html` pair, which really is
the bookkeeping. It is worth naming as a shape — a share of a cause that turns out to be the *lane*
rather than the engine, whose fix is therefore a change to how a case is enumerated and not to the
subject the document was about.

**All ten of HTML's reflection documents are cases, and nine of the ten pass whole**
([#3770](https://github.com/sebastienros/jint/issues/3770)). HTML §2.6.1's reflection algorithms are
`Jint.Browser/Dom/ReflectedAttribute.cs` and the members that take them are `overrides.json`'s `reflected`
list, so `reflection-misc.html` (4,877 assertions, 1,866 of them failing before), `reflection-text.html`
(10,202, 3,360 failing before), `reflection-sections.html` (5,604, 2,189 failing before),
`reflection-tabular.html` (6,116, 3,552 failing before), `reflection-forms-weekmonth.html` (1,579, 420
failing before), `reflection-embedded.html` (8,922, 3,774 failing before), `reflection-grouping.html` (5,358,
2,006 failing before), `reflection-obsolete.html` (2,621, 1,483 failing before) and `reflection-forms.html`
(8,271, 2,160 failing before) pass with **nothing** excluded, and `reflection-metadata.html` (3,110, 1,218
failing before) has one cause left — and it is not reflection. **22,028 of 56,660
assertions failed when the suite was measured; 16 do now.**

191 rows did it, and the first fifteen were mostly the **global** attributes every element carries —
`dir`, `lang`, `tabIndex`, `autofocus`, `inputMode`, `enterKeyHint` — which is why `text` needed only eleven
rows of its own for 3,360 assertions. The rest are element-specific, one `reflected` row each.
`metadata` took seven of those —
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
rendered image dimensions and are not reflection at all — but whose setters are, in as many words, so what
the rows fix is the half that is. Both rows still take the whole accessor pair, and deliberately: this
package renders no image, so the getter answers the content attribute either way, and answering it through
HTML's rules for parsing non-negative integers rather than through AngleSharp's `DisplayWidth` is the
difference between `width="-5"` reading 0 and reading −5 out of an `unsigned long`.

`forms` took fifteen more — the rest of the form controls, and the two remaining numeric shapes: `textarea`'s
`cols` and `rows` are **limited unsigned longs with fallback** (a zero or out-of-range set writes 20 and 2
rather than throwing, which is what separates them from `input.size`, where it is an `IndexSizeError`), and
`progress.max` is a **limited double**, whose setter declines to write at all when the value is not greater
than zero. `select.size` defaults to 0 where `input.size` defaults to 20, and `button.formMethod` has a
`dialog` keyword `input.formMethod` does not — three facts about three members that no CLR signature carries
and the `reflected` list has to state.

Six more rows finished it, and they are the first that reflect **only a setter**. `<meter>`'s `value`, `min`,
`max`, `low`, `high` and `optimum` are the case a whole replacement would have made worse: their getters are
HTML §4.10.14's own algorithm — an absent `max` is 1, `optimum` is the midpoint, the value is constrained to
the range — which AngleSharp implements and no reflection type can express, while their setters are plain
§2.6.1 reflection and were writing `Double.ToString(InvariantInfo)` where HTML wants "the best representation
of the number as a floating-point number", which is ECMAScript's Number-to-String. So the row model grew a
`setterOnly` flag: the entry replaces the setter and the generator keeps the projected getter's body, and it
refuses — as a diagnostic, not as generated code — a row that asks for it where there is no projected getter
to keep or where a getter hook has already claimed the read.

`embedded` took the last fifty, over ten interfaces, and was the largest single table: `<img>`'s twelve,
`<object>`'s twelve, `<iframe>`'s nine, and the media elements' `preload`, `crossOrigin` and `loading` on
`HTMLMediaElement` rather than on `<video>` and `<audio>` separately, which is where HTML puts them.
`track.kind` is the suite's clearest case for stating **both** defaults — its missing value default is
`subtitles` and its invalid value default is `metadata` — and `preload`'s are implementation-defined among
its three states, which is why the row picks `auto` and the corpus asserts membership of an array rather than
one value.

**The one cause left is not reflection**, and it is the dependency. Two others were, and both are gone: four
obsolete elements — `<dl>`, `<dir>`, `<font>` and `<frame>` — get an interface of their own from HTML and a
plain `HTMLElement` from the pinned assemblies, so there was nowhere for `compact`, `color`, `src` and their
kind to be reflected onto, and 506 rows named the tests. `DomManualInterfaces` declares all four by local name
now, the way it already declared `HTMLFrameSetElement` — one shape and one prototype chain per interface
over the same AngleSharp element — so `reflection-grouping.html` and `reflection-obsolete.html` pass whole,
and the divergence table records what the pinned assemblies are still missing. `<meter>`'s six setters were
writing a `double` with .NET's number format, so `-0` kept its sign and an exponent was `1E-10` where HTML
wants ECMAScript's `1e-10` — three values per member, eighteen rows, and a whole `reflected` row would have
been a regression because those getters are HTML §4.10.14's and are right; `setterOnly` is what took the
half that was wrong, and `reflection-forms.html` passes whole. What is left is `<style>`'s `media`, which
cannot be *written* at all when the value is not a media query AngleSharp.Css can parse — the exception comes
out of `Element.setAttribute` itself, through the attribute observer AngleSharp core registers, where Media
Queries §2.1 requires an unparseable query to be replaced by `not all`. Those 16 rows are the only failures
this lane's `html/dom/` figure gained, against 51,783 assertions it did not have before.

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
into twenty-nine groups; the counts are rows rather than files, since several are globs. Ninety of
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
| HTML's reflection suite, the two files that are not the suite | 2 | [#3770](https://github.com/sebastienros/jint/issues/3770); all ten `reflection-*.html` documents are cases now, so what is left out is `reflection-original.html`, the same suite in the aggregating spelling, and the attribute table of a `.tentative.` document nothing here runs |
| a DOM crash test or reftest | 5 | none loads `testharness.js` |
| a helper document beside its test | 4 | three frames and a fragment; a document under a suite would have to be a case, and the fourth answer is the frame-bodies table below |
| a DOM frame that runs script | 14 | listed when a frame had neither a document nor a realm; it has a document now ([#3771](https://github.com/sebastienros/jint/issues/3771)) and each row is owed a re-examination against the half that is left. Three have had it: the selector documents are cases |
| a member reached at file scope | 30 | `createCDATASection` (31 documents, 24 of them `dom/ranges/`, through `dom/common.js`), `createDocument` (5) and `setAttributeNode` (1) |
| one DOM file each | 3 | a `SyntaxError` no `error` event carries to the harness, and two `MutationObserver` documents waiting for a record that never comes |
| the pseudo-classes suite's four non-cases | 4 | a helper root this corpus does not hold, a `.window.js` glob, and a reftest with its reference |
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

**There are two rows now.** `focus-iframe.html` is the second: `focus.html` frames it and asserts that
`:focus` does not match a focused element inside it, which is a document that has to be served and must
not be a case of its own.

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
`no-focus-events-at-clicking-editable-content-in-link.html` and `pointer-event-document-move.html`,
fourteen tests between them, all passing, none excluded — fourteen rather than ten because
`handler-count.html` declares three variants and is three cases. Two still cannot report, and neither
reason was ever the driver's:
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
