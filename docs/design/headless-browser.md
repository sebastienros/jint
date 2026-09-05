# A headless browser on Jint — design

**Status: finalized design; implementation under way.** The authoritative statement of this design is the body
of [sebastienros/jint#3575](https://github.com/sebastienros/jint/issues/3575); where this
document and that issue disagree, the issue wins and this file is brought back into line. What this file adds is
the longer form: the mechanisms each decision rests on, named so that a reader can find them. The protocol half
is [`devtools-protocol.md`](devtools-protocol.md), and
[§12](#12-what-shipped-and-where-it-differs) is the index of what was built against this design and the one
line in which each item differs from it. For what the package does rather than why, read
[the Jint.Browser package guide](../packages/jint-browser/index.md) instead of this file.

Everything normative here was read from the [DOM](https://dom.spec.whatwg.org/), [HTML](https://html.spec.whatwg.org/multipage/),
[Fetch](https://fetch.spec.whatwg.org/), [XMLHttpRequest](https://xhr.spec.whatwg.org/) and
[WebIDL](https://webidl.spec.whatwg.org/) living standards, and from the
[Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/).

> **On citations.** This file cites files and members, not line numbers.

---

## 1. The premise, and the one principle

Jint already carries the web platform's non-DOM half — thirty opt-in subtrees under `Jint/WebApi/` that cover
WinterTC's Minimum Common API except `WebAssembly` (declined by design), plus `fetch`, `WebSocket`,
`EventSource`, `Storage`, `caches`, `Worker` and the Streams family. The only missing layer between that and a
browser is the document: HTML parsing, the DOM, the CSSOM, and the runtime that ties navigation, script
scheduling, timers, network and storage to a `Window`.

**AngleSharp** already has the parser, the DOM and (with `AngleSharp.Css`) the CSSOM, and **AngleSharp.Js**
already binds Jint to that DOM, executes `<script>`s, loads modules and import maps, and runs Web Workers. The
project founder's guidance for this campaign is the principle every decision below is checked against:

> Jint should add value to AngleSharp without competing too much.

So: AngleSharp is the parser, the DOM and the CSSOM; nothing here re-implements any of them. What Jint owns is
what nobody else has — a binding layer built on Jint's own shape and layout machinery instead of a reflection
trampoline, a page runtime that wires Jint's timers, fetch, storage and workers into a `Window` under Jint's
execution constraints, and the automation protocol. The generated bindings and the tree-aware event dispatcher
are designed so that AngleSharp.Js can adopt them, and the offer is made as soon as they work; every AngleSharp
or AngleSharp.Js divergence the conformance lane finds is recorded (in the PR that found it and in
`Jint.Browser/AGENTS.md`'s divergence table) and presented to the maintainer, who decides what is raised
upstream — an agent never opens an issue on a neighbouring project on its own. In every document and README
sentence, `Jint.Browser` is "AngleSharp + Jint", never a rival DOM stack.

## 2. What it is, and what it is not

The shape is Lightpanda's, not Chromium's: **a browser that does not render.** DOM, JavaScript, network, storage,
workers, and the Chrome DevTools Protocol so that Puppeteer, Playwright, PuppeteerSharp, Playwright for .NET,
chrome-remote-interface and the Chrome DevTools frontend can drive it — in-process, with no native binaries and no
browser download, on any platform .NET runs on, with Jint's statement, time and memory constraints bounding what
a page may do. Kitesurf's framing of the trade is the honest one and it is exactly an interpreter's profile: a
fraction of Chromium's CPU and memory per page, at some multiple of its wall-clock time.

| v1 delivers | Out of v1 (absent, so feature detection is honest) |
| --- | --- |
| Full HTML5 parse with inline, external, `defer`, `async` and module scripts, import maps, `document.write`; generated DOM and CSSOM bindings; tree event dispatch; forms, history, cookies, storage, workers, `fetch`/`XMLHttpRequest`/`WebSocket`/`EventSource`; `MutationObserver`; stub `IntersectionObserver`/`ResizeObserver`; deterministic coordinate input; accessibility tree and markdown snapshots; CDP for Puppeteer/Playwright `connect`; a WPT lane; constraints per page | Layout-dependent APIs (`offsetWidth` is synthetic, `cssom-view`), rendering, screenshots, PDF, WebGL, canvas 2D, media, IndexedDB, `caches` (the engine has it; a page has no origin-partitioned provider to grant it on), WebAssembly, CSP enforcement, TLS-fingerprint stealth, iframe scripting (v1.1), `WindowProxy`, SharedWorker/ServiceWorker, drag and drop, real isolated worlds (v1.1) |

`Page.captureScreenshot` answers a CDP error with a sentence, the way Lightpanda does.

## 3. The runtime model

- **The global stays `GlobalObject`.** `Host.CreateGlobalObject(Realm)` is virtual and `GlobalEnvironment`
  tolerates a host-defined global, but substituting one forfeits the global-identifier inline cache
  (`JintIdentifierExpression` keys on the realm's `GlobalObject`) and would mean re-installing every intrinsic
  `GlobalObject.Properties` emits. Instead the global's `[[Prototype]]` becomes
  `Window.prototype → EventTarget.prototype → Object.prototype` (`GlobalEnvironment.HasBindingOnGlobalPrototype`
  already resolves prototype members as globals), and the per-document singletons — `document`, `location`,
  `history`, `navigator`, `screen`, `window`, `frames`, `top`, `parent`, `customElements` — are own lazy
  properties installed through the public `Engine.AddLazyGlobal`, which keeps them on the identifier cache.
  `window instanceof EventTarget` holds through the chain; `EventTarget.prototype.addEventListener.call(window, …)`
  works because `EventTargetPrototype.Brand` accepts the realm's global and maps it to the engine's
  `GlobalEventTarget` (engine PR B1).
- **One `Engine` per top-level navigation.** A navigation is a new realm in a browser; here it is a new engine
  built from the same `BrowserOptions`, so "per document" and "per engine" coincide and no `WindowProxy` is needed
  in v1. The previous engine receives `beforeunload`, `pagehide` and `unload`, its cancellation token is
  cancelled, its pending fetches abandoned, and it is disposed on the page loop.
- **One `PageLoop` thread per page**, owning the engine and the DOM: it drains a mailbox of host and protocol
  work, calls `Tasks.ProcessTasks()`, runs the animation-frame lane, and sleeps by
  `Tasks.TimeUntilNextScheduledWork` — the `WptHarness.PumpWorker` shape. Every public `Page` API and every CDP
  command posts to the mailbox and awaits a completion; nothing else touches the engine or the DOM. Workers come
  from a `ThreadPerWorkerProvider` (the package is a host, so it may start threads; the engine still never does).
- **Iframes parse but do not run script in v1**: frames are real in the frame tree and their documents are
  fetched and parsed, but `contentWindow` is `null`. v1.1 adds one realm per frame on the same engine through an
  `Engine.WebApi.InstallInRealm(Realm)` seam (today `WebApiRegistration.InstallGlobals` targets the main realm
  only); a second engine could never satisfy `parent.document`.

## 4. The binding layer

A curated generator, `tools/dom-bindings/`, reads `AngleSharp.dll` and `AngleSharp.Css.dll` at the pinned
version through `System.Reflection.MetadataLoadContext` and treats AngleSharp's `[DomName]`, `[DomAccessor]`,
`[DomConstructor]`, `[DomNoInterfaceObject]` and `[DomPutForwards]` attributes as the WebIDL they effectively
are. It emits checked-in `Jint.Browser/Dom/Generated/*.g.cs`: one `JsObjectShape` per interface (methods,
accessors, constants, a per-realm `constructor` slot, `@@toStringTag`), interface objects, the prototype chain
down to Jint's `EventTarget.prototype`, a `DomTypeMap` from AngleSharp runtime type to shape, and collections
over `ArrayLikeObject` / `NamedPropertyObject`. A curated `overrides.json` skips or replaces what the attributes
cannot express: event-handler content attributes, `Task`-returning members, navigation-shaped members,
`innerHTML`/`outerHTML` setters (routed through the parser driver so inserted scripts run), `[PutForwards]`.
`JINT_DOM_BINDINGS=update` regenerates; a staleness test fails on drift — the `JINT_SPEC_ANCHORS` /
`JINT_WPT_CENSUS` discipline. Checked-in rather than a live source generator for the same reasons as the
protocol layer: reviewable diffs, an analyzer-free build, and swapping later is mechanical.

Why generated on Jint shapes rather than AngleSharp.Js's reflection bindings: a shape-mode prototype per
interface is what the inline caches and the prototype-method cache want, member bodies are static lambdas that
call the AngleSharp interface member directly (interface dispatch, zero reflection), the output is AOT-safe, and
it is the answer to the one actionable question Starling's "we will not embed Jint" poses — host-object cost.
This is the piece offered upstream: AngleSharp.Js can adopt the generated bindings without adopting anything
else here.

**What was built also owns HTML §4.13**, which AngleSharp has nothing of: `Jint.Browser/CustomElements/` is
the `CustomElementRegistry`, the element state (a side table keyed on the AngleSharp element), the
construction stack that makes `super()` answer the element being created, and the reaction lane. Three of the
overrides above exist for it — `createElement`, `createElementNS` and `cloneNode` answer the *constructor's*
element for a defined name — and `DomInterfaceObject.Construct`, which refuses every other `new`, is HTML's
`HTMLElement` constructor. The one approximation is where a reaction runs: nothing here can see a generated
member return, so the element queue is drained as a reaction *arrives* rather than when the outermost
`[CEReactions]` operation returns, and a parser-created element is upgraded at the driver's script boundaries
rather than constructed by the parser. `Jint.Browser/AGENTS.md` states both and what they cost.

Wrapper identity: `DomNodeObject : JsEventTarget` holds an `INode`, the prototype picked from `DomTypeMap`, the
brand check in a generated member is the interface cast (`Illegal invocation` otherwise). One wrapper per node
through a per-page `ConditionalWeakTable<INode, DomNodeObject>`: a node in the tree keeps its wrapper and its
expandos alive (React and Vue rely on that), a node dropped by both the tree and script collects with its
wrapper — the browsers' wrapper-preservation rule for free. Short-lived views (`DOMTokenList`, `NamedNodeMap`,
`Range`, `CSSStyleDeclaration`) wrap with a strong reference.

## 5. Events: one bus, Jint's

**AngleSharp's event bus is neither observed nor driven by script.** Every script-visible event is a Jint `Event`
dispatched through the tree-aware dispatcher engine PR B1 adds to `JsEventTarget` (DOM §2.9 dispatch, get the
parent, the event path, retargeting, activation behaviour, over virtuals a DOM wrapper overrides), at the
algorithm points the package owns: navigation (`readystatechange`, `DOMContentLoaded`, `load`, `pageshow`,
`beforeunload`, `unload`), input (the `InputDispatcher`), forms (`submit`, `formdata`, `reset`, `invalid`),
history (`popstate`, `hashchange`), observers, scripts (`load`/`error` on `<script>`). AngleSharp's own
`Dispatch` calls run into AngleSharp's listener lists, which hold nothing script-registered, so they are
invisible, and whatever AngleSharp-internal listeners exist keep working untouched. `DomNodeObject.GetParent`
implements "get the parent": parent node or assigned slot; `ShadowRoot → host`; `Document → window` except for
`load`; `Window → null`.

## 6. Navigation and the parser baton

A document fetch goes through Jint's own fetch pipeline (`FetchTransport`, engine-free by design) with the
context's cookie jar, `Referer`/`Origin`, the `UrlFilter` re-checked per redirect hop and `MaxResponseBytes`
bounding the document (engine PR B3 adds the base URL, referrer, cookie jar and observer seams). Then the
`ParserDriver` runs AngleSharp's parser on a parser thread and hands a **baton** back and forth with the page
loop: `IScriptingService.EvaluateScriptAsync` and the resource loader park the parser and give the DOM to the
loop, which runs the script (or the fetch and any due tasks) and hands it back; completions use
a blocking handshake, so the parser never resumes inline on the loop thread — it cannot resume at all until the
loop releases it. Invariant: exactly one side holds the baton, and only the holder touches the DOM. That is
also what gives browser-correct timing — timers fire while the parser waits on a parser-blocking
`<script src>`, and run nowhere while it is tokenizing, which is what a browser does because there the parser
*is* the task the event loop is running.

**What shipped, measured rather than assumed** (campaign item R3). The question the design left open —
whether AngleSharp's parse suspends and resumes on a pool thread once a `<script src>` is present — is
answered in its source and by the driver's own check: `HtmlDomBuilder.ParseAsync` awaits the task
`HandleScript` produced with `ConfigureAwait(false)`, so **yes, it does**, and the baton rather than the
fallback is what the package implements. The refinement the implementation adds is that a subresource fetch
finishes *before* AngleSharp's `IResourceLoader` returns — the loader hands the baton over and the loop
fetches while pumping — so the parse never suspends at all and stays on the thread it started on;
`ParserBaton.ParserHopped` reports it as a page error if that ever stops being true. Registering an
`IResourceLoader` is itself what makes the parse asynchronous, so it and the baton arrive together.

Classic, `defer` and `async` scripts stay AngleSharp's to prepare and order — which is what buys
parser-blocking, document order, the deferred queue and the `document.write` insertion point without
re-implementing any of them — while module scripts and import maps are hidden from it (`SupportsType` answers
`false`) and run from the driver after the parse over Jint's `IAsyncModuleLoader` against the page's fetch,
which is where HTML puts them anyway. `document.readyState` is the page's own shadow, moved
`loading` → `interactive` → `complete` with the `readystatechange` events that go with them, because
AngleSharp's setter is protected; `DOMContentLoaded` fires after the deferred and module scripts and `load`
after every subresource. `document.write` during parsing writes into the live text source while the parser is
parked, and after it is refused with a page error rather than implying `document.open()`. A `<script>` a
script inserted runs (blocking the loop for its fetch rather than pumping, since pumping from inside a
running script would run the page's jobs in the middle of one); one `innerHTML` inserted does not, which is
HTML's rule and AngleSharp's behaviour already. `<link rel=stylesheet>` is fetched and cascaded, and so is a
frame's document — AngleSharp opens it into the nested browsing context it already made for the element, and
`load` fires at the frame before the window's, HTML's "delay the load event"; an `<img>` is recorded in
`Page.Requests` as not fetched, there being nothing to render it with. The scheduling divergences this shape costs are listed in `Jint.Browser/Runtime/AGENTS.md`.

## 7. Constraints per page

The constraints gotcha in the root `AGENTS.md` applies twice over: a page is a host-driven sequence of entries,
and its event loop is pumped. So a page's budget is built only from what survives the per-entry reset.
`BrowserOptions.MaxTaskDuration` brackets each **turn** with `OperationDeadlineConstraint.Begin`/`End`, and a
turn is one mailbox request, one `ProcessTasks` drain (every due timer callback, microtask, promise reaction
and animation-frame batch together) or one inline `<script>`. A request that runs out of budget fails its own
task with `TimeoutException`; a drain's and a script's are recorded as a `PageErrorKind.BudgetExceeded` entry
and the page survives. `BrowserOptions.MemoryLimit` arms a per-page `MemoryLimitConstraint` over the same turn,
and a worker's pump takes the same bracket over the constraint factories its parent replayed. `Page.Close` and
`Target.closeTarget` cancel the page token registered with `ObserveCancellation`; `LimitExecutionTime` still
governs each `Runtime.evaluate` / `Page.Evaluate` entry as well. `MaxDomNodes` is one number checked against two
quantities — the parse refuses a document holding more, and the wrapper factory refuses the projection that
would take one engine past the same number of node wrappers. `MaxActiveTimers`, `MaxResponseBytes` and `FetchTimeout` are the
page-sized names for their engine settings, applied to page and worker engines alike before any host callback;
one `UrlFilter` covers document, subresource, XHR, WebSocket, EventSource and worker loads.
`BrowserOptions.ForUntrustedContent` applies `Options.ForUntrustedCode` to page engines — from inside the
package's own construction callback, so it wins over a host's `ConfigureEngine` — and reaches their workers
through `CopySecurityPosture` and the replayed factories, with `BlockPrivateNetwork` on by default for every
context that did not assign it.

## 8. Input without layout

Every element gets a deterministic synthetic box — the "flat renderer" — so that `elementFromPoint`,
`getBoundingClientRect`, `DOM.getBoxModel`, `DOM.getContentQuads`, `DOM.getNodeForLocation` and
`Input.dispatchMouseEvent(x, y)` resolve without a layout engine.

**What shipped, in one sentence.** `Jint.Browser/Layout/FlatLayout` gives every *rendered* element an ordinal
in tree order and the row `[i·R, (i+1)·R)` with `R = 16`; its box starts at that row, is
`R × (1 + rendered descendants)` tall and the viewport wide. Boxes therefore nest exactly as the tree does and
never straddle, and the deepest box containing a point is always the owner of the row the point falls in — so
a hit test is a division, the centre of a leaf hits the leaf, the centre of a container hits a descendant as a
browser does, and the click bubbles back up. Rendered is HTML's set minus what a rendering would have needed:
`<head>` and its subtree, `<script>`/`<style>`/`<template>`/`<noscript>` wherever they sit, and whatever the
accessibility layer's `ElementVisibility` calls not rendered (`hidden`, `display: none`,
`visibility: hidden|collapse`); `aria-hidden` does not remove a box. An element with no box answers zeros in
script and `-32000` over the protocol, because a client reads zeros as a real box at the origin.

**Scrolling is virtual and is the only state the model keeps.** A page holds a `scrollY` clamped to its
document; `window.scrollTo`/`scrollBy`/`scroll`, `element.scrollIntoView`, `DOM.scrollIntoViewIfNeeded` and a
wheel event set it, `window.scrollY`/`pageYOffset` and `document.scrollingElement.scrollTop` read it, and
every client rectangle subtracts it. `scrollX` is always zero, because every box is exactly as wide as the
viewport. That is what lets a client whose click path insists on "scroll it into view, then check the box is
inside the viewport" — Playwright's does — succeed on a document taller than its window.

`dispatchMouseEvent` is the pointer/mouse event sequence with focus and click activation (`<a>` navigates,
submit buttons submit, checkbox/radio toggle with legacy pre-activation rollback, `<label>` forwards,
`<summary>` toggles, `<option>` selects), and `mouseWheel` is a scroll of `deltaY`.

**The keyboard shipped, and the protocol's four types are three questions.** `dispatchKeyEvent` dispatches at
whatever the page has focused — the body when nothing is. `keyDown` fires `keydown` and, for a key that
produces text, `keypress`, then runs the whole default action; `rawKeyDown` fires `keydown` and runs the
default action *without* the insertion, because it is what a client sends for a key whose character is coming
separately or not at all — which is every editing key; `char` is that character alone, a `keypress` and then
the insertion; `keyUp` fires `keyup` always, because it is part of no default action. Modifier state is the
client's: every one of them puts the bit field in each event. `insertText` is text at the caret with no key
events, which is an IME commit and Puppeteer's `sendCharacter`; `imeSetComposition` is accepted and changes
nothing, because a candidate string never reaches the value and the commit that does arrives as `insertText`.

Editing is a string and two offsets, which needs no rendering: insertion at the selection, `Backspace` and
`Delete`, `Home`/`End`/arrows with `Shift` extending from the anchor (so `selectionDirection` is real),
`ArrowUp`/`ArrowDown` as line moves computed from the newlines in a `<textarea>`'s value, select-all,
`maxlength`, `Enter` as a newline in a `<textarea>` and as HTML's implicit submission in an `<input>`,
`Tab`/`Shift`+`Tab` along the sequential focus order, and `beforeinput` (cancelable) / `input` / `change`
around all of it. `contenteditable` is deliberately light — text spliced in one text node, the caret kept in
the document's own `Selection` — so `Enter` there does nothing rather than something structural and wrong.

WPT's `testdriver.js` is mapped onto the same dispatcher through `testdriver-vendor.js`, the file upstream
ships empty for a vendor to replace: `click`, `send_keys` and `action_sequence` resolve a WebDriver origin to
a point in the page and post it to a host function that runs the same `InputDispatcher` the `Input` domain
runs. There is one implementation, so a wpt document and a Puppeteer client cannot disagree about what a
click does.

## 8a. The page-level protocol

A page is **one target with one engine per navigation**, and that is the whole shape. `PageTarget` is a
`Jint.DevTools.DevToolsTarget`: it holds the identifier a client keeps addressing, the frame that identifier
also names, the bindings `Runtime.addBinding` installed, the scripts
`Page.addScriptToEvaluateOnNewDocument` runs and the emulation a client set; the engine under it is replaced
on every commit, and with it every handle, every script identifier and the execution context. A client that
comes back with something from the document before is told so in Chrome's own words —
`Cannot find context with specified id`, `Could not find object with given id` — rather than answered about
a value of the document that replaced it.

`Jint.Browser` reaches all of that through one seam, `IPageObserver`, and a page has exactly one observer.
`DocumentCreated` fires after the window installer and before the parse, on the loop thread, which is what
makes it the place to replace the engine, re-install the bindings and run the new-document scripts: every one
of them has to be in place before the document's first inline script. `Phase`, `SameDocumentNavigated`,
`TitleChanged`, `DialogOpening`/`DialogClosed`, `NetworkIdle` and `Closed` are the rest.

The events of a cross-document navigation, in the order the recordings show Chrome sending them:
`frameStartedNavigating`, `frameStartedLoading`, `lifecycleEvent(init)`, `frameNavigated`, then the engine
swap (`Runtime.executionContextsCleared`, `executionContextCreated`), `lifecycleEvent(commit)`,
`domContentEventFired` + `lifecycleEvent(DOMContentLoaded)`, `loadEventFired` + `lifecycleEvent(load)`,
`frameStoppedLoading`, and — after half a second of quiet — `lifecycleEvent(networkAlmostIdle)` and
`lifecycleEvent(networkIdle)`. One divergence, and it comes from where the commit is announced: Chrome
interleaves `frameNavigated` between the two context events, and here the frame and the engine swap are one
`DocumentCreated` call — the moment the next document's engine exists and nothing of it has been parsed — so
`frameNavigated` precedes the swap. A same-document move — `pushState`, a fragment, a traversal — is
`navigatedWithinDocument`.

`DevToolsServerExtensions.AddBrowser(server, browser)` is the only public member the protocol layer adds.
Every page becomes a `page` target carrying its browser context, and the server's `Target` domain mints both
through a `BrowserTargetHost`: `createBrowserContext` opens a `BrowserContext`, `createTarget` opens a `Page`
in it, `closeTarget` closes the page, `disposeBrowserContext` closes the context. Each page is published
with a **`tab` target** in front of it, which is not decoration: modern Chrome puts one between the browser
and each page, Puppeteer's browser-level `setAutoAttach` filter is *everything but a page*, and it reaches a
page by sending `setAutoAttach` again on the tab's session. A server that published pages and no tabs is one
Puppeteer connects to, discovers a page on, and then waits forever for a session — which is how it was
found.

Three divergences are deliberate and stated where they are made. An **isolated world is an alias** for the
document's own realm: there is one realm per document, so a world buys a client its own
`executionContextId` and none of the isolation the name promises. A **dialog does not block the page** —
`alert` runs on the page loop, inside the script that called it, and that loop is the thread a client's
answer would be delivered on — so `Page.handleJavaScriptDialog` sets the standing decision the next dialog
reads, and `javascriptDialogOpening` and `javascriptDialogClosed` arrive together. And
**`captureScreenshot` and `printToPDF` answer `-32000`** with a sentence that says this browser renders no
pixels and names `Jint.getMarkdown`, `Jint.getText` and `document.documentElement.outerHTML` instead.

What is accepted and not yet effective is accepted because refusing it fails an ordinary connection, and
each says which campaign item makes it real: `Network.setCacheDisabled` (there is no cache to bypass) and
`Audits.enable` (nothing to report).

**The network domains are real, and what is still absent is absent with a reason rather than pending.**
`Network` reports every request the page makes, and `Fetch` pauses one at the **request** stage or the
**response** stage, all over the page's own request log, which is the engine's `FetchObserver`; the
document's request carries the `loaderId` as its `requestId`, which is what makes a client's `goto` answer a
response object. A page's `WebSocket` takes the four events the protocol gives a socket — its creation, both
handshakes and its close — over the engine's own `WebSocketObserver`, and is deliberately *not* in the
request log, because a socket stays open for as long as the page wants it and an entry would stop
`networkIdle` firing. What is not there: `Fetch.getResponseBody` and `takeResponseBodyAsStream` and with
them the `IO` domain, because a response-stage pause has the response's *headers* while its body is still on
the socket, so handing a client bytes means buffering them first — a budget decision, and
`Network.getResponseBody` is what answers a body here; the three `webSocketFrame*` events and
`eventSourceMessageReceived`, because the socket observer is never told about a frame and a stream is
observed as bytes rather than as the events they decode into; and `Network`'s **timing** document, because
no phase of a request is measured and a document of zeros reads as a page that loaded instantly. A paused
request holds the transport thread it is being sent on and never the page loop — the one exception is a
`<script src>` a running script inserted, which blocks the loop by design.

**`Emulation` is effective, and the question each command answers is *when*.** The viewport, the emulated
media type and its Level 5 preference features, touch, focus, geolocation, the user agent and the hardware
concurrency move the document that is loaded: `matchMedia` re-evaluates against a single
`PageMediaEnvironment` and every `MediaQueryList` whose own answer moved fires `change`, and `navigator`
answers differently on the next read. The time zone and the locale are `Options` an engine is *constructed*
from and a page constructs one per navigation, so those two — and script execution, which the parse refuses
— take effect on the next document, which is where every client sets them. The rest are accepted no-ops
whose summaries say what there is none of: no renderer for auto dark mode, a background colour, a scrollbar
or a CPU throttle; no idle detector; no touch event interface for a mouse event to be translated into. The
preference features are the page's own answer rather than AngleSharp.Css's, which models none of them, so
when that library grows them there is one table to delegate from. The user agent is one setting for two
commands — `Emulation`'s and `Network`'s — kept on the page, because `navigator.userAgent` has to answer the
same string every request carries.

`Accessibility` publishes the tree of §9's accessibility layer in Chrome's `AXNode` shape, with the `DOM`
domain's `backendNodeId` on every node — which is what makes `page.accessibility.snapshot()`, an `aria/`
selector and `getByRole` answer about a node the same client can then measure and click. `Security`,
`Overlay` and `CSS` answer what a DevTools front end sends while attaching and nothing more: `CSS` has the
computed style and the inline style, both AngleSharp.Css's, and every editing command is `-32601`.

## 9. The scoreboard

WPT is the conformance suite the way test262 is the language's, with the discipline `Jint.Tests/Wpt/AGENTS.md`
already enforces: the exclusion table is the artefact (an entry matches at least one failing test and no passing
one), `NeedsTriage` empty is the signal, the census is a ceiling that only lowers. `Jint.Tests.Browser` adds the
browser lane: the in-process `WptServer` serves `.html`, the real upstream `testharness.js`, `.headers` sidecars
and `.sub.html` substitution; a `testharnessreport.js` overlay posts results through a page binding; the existing
`.any.js` files run again inside a real `Window` realm through synthesized `.any.html` wrappers.

**What was built** is `Jint.Tests.Browser/Wpt/`, and four things about it differ from the paragraph above, each
for a reason its own [`AGENTS.md`](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Browser/Wpt/AGENTS.md)
argues. The corpus is **not** vendored
twice: `Jint.Tests.Browser` references `Jint.Tests` and runs the same tree at the same pin, so there is one
corpus and one pin. The overlay posts **strings** through a host function the driver installs on every page
engine, not values through a binding, because a page's engine belongs to a thread the driver is not on. Only the
**window** wrapper is synthesized — upstream's dedicated-worker wrapper builds a *classic* worker whose body
opens with `importScripts`, which Jint runs no lane for. And an uncaught exception is **upstream's harness's**
business rather than the driver's, because the engine fires a real `error` event at the global scope and
`testharness.js` listens for it, `setup({allow_uncaught_exception: true})` included. A second overlay fills
upstream's other vendor slot, `testdriver-vendor.js`, so a document that drives input through `test_driver`
reaches the same `InputDispatcher` a protocol client does — one implementation, so the two cannot disagree.
The suites arrive a PR at a
time; the first are `dom/events` and `html/webappapis/scripting`'s events and processing-model halves, and the
rest of the list above follows through the same lane.

**The nightly `wpt run` over CDP is built**, and the two numbers it leaves the project with are not two
measurements of the same thing — which is the sentence this paragraph used to leave implied. **The census**
is *ours*: our driver, in our process, over the vendored subset, with an exclusion table that names every
failure one at a time, and it **is a gate** — `Not passing` only ever goes down. **The scoreboard** is
*upstream's*: `wpt run` over `wptserve`, across the whole of ten suites — `dom/`, `html/dom/`,
`html/semantics/scripting-1/`, `html/webappapis/`, `html/browsers/history/`, `xhr/`, `url/`, `fetch/api/`,
`FileAPI/` and `custom-elements/` — including every wrapper the manifest generates for a global this engine
has no lane for, and it **gates nothing** — a failure there is a
number on a page. The census is the one an engine change has to keep; the scoreboard is the one that can be
compared with what another engine publishes, because wpt measured it. A suite in the scoreboard that the
census does not have is a suite nobody has vendored yet, never a disagreement.

`wpt run chrome` cannot produce it: that product requires a `--webdriver-binary`, launches that
`chromedriver`, and speaks WebDriver classic — its CDP is tunnelled through `chromedriver`'s
`goog/cdp/execute` extension command, and no `debuggerAddress` capability exists anywhere in the wpt tree.
Lightpanda ships a WebDriver front end beside its CDP for exactly this reason. What is here instead is a
**wptrunner product plugin**,
[`tools/wpt-scoreboard/`](https://github.com/sebastienros/jint/tree/main/tools/wpt-scoreboard), registered
through upstream's `wptrunner.products` entry-point group so that no fork of wpt is needed: its executor
navigates a page over CDP and reads the results upstream's own `testharnessreport.js` posts, through a
`Runtime.addBinding` binding, and every judgement about whether a subtest passed stays upstream's.
`.github/workflows/wpt-scoreboard.yml` runs it nightly and commits the page to a `wpt-scoreboard` branch;
it fails only when the runner could not reach the browser or produced no report. Next to it, an obstacle course
of offline fixtures (React, Vue, Preact and Svelte TodoMVC, SSR hydration, jQuery 3 with `async: false`, htmx,
Alpine, a `pushState` router, custom elements, modules with an import map, forms with redirects, a cookie login,
`localStorage` persistence, `IntersectionObserver` and `MutationObserver` widgets, dialogs) each asserting a DOM
end state and an empty error sink, and PuppeteerSharp / Playwright for .NET smoke suites over the in-process
WebSocket.

## 10. Packages and the engine seams

| Where | What |
| --- | --- |
| `Jint/` (engine, public, additive) | B1 tree event dispatch; B2 `XMLHttpRequest` (`WebApiFeatures.XmlHttpRequest`, sync supported as a blocking wait on the engine-free transport); B3 fetch for documents (`BaseUrl`, referrer policy, `Origin`, `CookieJar`, `FetchObserver` with interception), and C3's one addition to it — `FetchInitiator.XmlHttpRequest`, plus the body chunks an `XMLHttpRequest` never handed its observer; B4 `PerformanceObserver`, `FileReader`, blob URLs |
| `Jint.Browser/` (net8.0+, `InternalsVisibleTo` from Jint for now, promoted to public seams as they prove their shape) | the runtime of §3–§8 and the page-level CDP domains, plus the custom `Jint` domain (`getMarkdown`, `getText`, `getAccessibilitySnapshot`). **No engine seam is owed for the emulation**: a page's time zone and culture are `Options.TimeZone` and `Options.Culture`, already public and already fixed at construction, so the whole of `Emulation` is the package's own |
| `Jint.Browser.Tool/` | the `jint-browser` dotnet tool. **What shipped**: `serve [--port 9222] [--host]`, `fetch <url\|file> --dump html\|text\|markdown\|ax` with `--wait-until commit\|domcontentloaded\|load\|networkidle`, `--main-content`, `--max-length`, `--header`, `--cookie`, `eval <url> <expression>` and `version`; `--untrusted`, `--user-agent`, `--max-task-duration`, `--memory-limit` and the private-network switches on every command; five exit codes so a caller can tell its own mistake from the site's from the page's. It takes **no** `InternalsVisibleTo` grant, which is what makes it the standing proof that the published surface is enough — the three seams it needed (`Page.MarkdownAsync`/`TextAsync`/`AccessibilitySnapshotAsync`, `Page.WaitForNetworkIdleAsync`, `BrowserOptions.BlockPrivateNetwork`) were promoted rather than reached around |
| `Jint.Browser.Mcp/` | the Model Context Protocol server, and `jint-browser mcp` serving it. **What shipped**: `navigate`, `back`, `forward`, `reload`, `snapshot` (`ax` with `ref=` handles, `markdown`, `text`), `click`, `fill`, `type`, `press`, `select`, `hover`, `scroll`, `evaluate`, `wait_for`, `network_requests`, `cookies`, `set_cookie`, `close`, plus `jint://page/markdown` and `jint://page/requests` as resources; hardened by default; every tool answers a `CallToolResult`, and none throws — the SDK redacts an ordinary exception's message. **`--http` did not ship**, and its own `AGENTS.md` argues why: the protocol's 2026-07-28 revision removed the session header from streamable HTTP, so the SDK's transport is stateless, per-session state needs the `[Experimental]` `RunSessionHandler` and a negotiated downgrade, and `ModelContextProtocol.AspNetCore` would put a `Microsoft.AspNetCore.App` framework reference in a `dotnet tool`. The `ref=` handles are the accessibility tree's own identifiers rather than `backendNodeId`, which belongs to a protocol target there is none of here |
| `Jint.Tests.Browser/` | the WPT browser lane, the obstacle course, the client smoke suites |

`Jint.Browser` ships `IsAotCompatible=false` in v1 and says so: AngleSharp is not trim-annotated. An AOT
inventory is a v1.1 item once its warnings are counted.

## 11. Verification

Every PR keeps the repository's gates. Engine PRs run the affected WPT `.any.js` suites and, when they touch a hot
path, the full SunSpider/Dromaeo tables. The browser lane's exclusion table and census, the obstacle course on all
four CI legs, and the two .NET client smoke suites gate `Jint.Browser`. The benchmark that closes the campaign
runs the same PuppeteerSharp script against `Jint.Browser`, headless Chromium and, where installed, Lightpanda,
and records wall time, CPU and peak memory per page load in the honest framing of §2.

**What was built** is `Jint.Tests.Browser/Fixtures/` and the two client suites beside it, and three things
about them differ from the paragraph above. The course runs on all four legs, as promised — but the
**Playwright** suite does not: its driver is a Node process the package carries, so the suite reads
`JINT_BROWSER_CLIENTS` and a `browser-clients` CI leg sets it, while PuppeteerSharp's stays on every leg
because it costs nothing. A fixture that does not pass is a **`needs triage` row** in
[`Fixtures/README.md`](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Browser/Fixtures/README.md)
with the failing assertion and a
one-line diagnosis, and `FixtureInventoryTests` fails unless that set is exactly the set of cases marked
`[Explicit]` — the discipline the web-platform-tests exclusion table is under, for the same reason. Both
rows that were ever written have since been retired by the pull request that paid them: `htmx` was owed
DOM XPath and `custom-elements` was owed the registry. And the
course is a **gate on the engine as much as on the package**: the eighteen fixtures and the two clients found
seven defects between them, six of which were fixed in the pull request that added them — an
`insertBefore(node, null)` that was a `TypeError`, a selector refusal a page could not catch, a
`Target.getTargetInfo` with no identifier, a page target that named no browser context, a missing
`Node.getRootNode`, and `Storage.getCookies` answering on one session model of the two. The seventh was
recorded rather than fixed and then fixed on its own: `getComputedStyle` resolved no initial values, so
`visibility` was the empty string and Playwright's actionability check read every element of every page as
hidden — `Jint.Browser/Dom/Views/ResolvedStyle` is the ten-property exception that closed it.

## 12. What shipped, and where it differs

The sections above are the design, kept as the design. This table is the index of what was built against it:
one row per item, the pull request that built it, and the one line in which what shipped is not what was
planned. A blank last column means the section above describes what exists.

| § | What shipped | PR | Where it differs |
| --- | --- | --- | --- |
| 3 | One `PageLoop` thread per page, a new engine per navigation, and the global's `Window.prototype` chain | [#3648](https://github.com/sebastienros/jint/pull/3648) | — |
| 3 | Frames, parsed and listed; then given a document of their own ([#3771](https://github.com/sebastienros/jint/issues/3771)) | [#3667](https://github.com/sebastienros/jint/pull/3667) | A child frame has a document and no realm: `contentDocument` answers it same-origin and `load` arrives at the element, while `contentWindow` is `null` and nothing in the frame runs — so a document that needs a second global still cannot run here, which is what puts thirty-seven `custom-elements/` files in the not-vendored table |
| 4 | The generator over the two pinned AngleSharp assemblies, and the checked-in `Dom/Generated/` | [#3634](https://github.com/sebastienros/jint/pull/3634) | A DOM prototype carries no `@@unscopables`, because AngleSharp's metadata does not say which members are unscopable; and a nullable `DOMString` parameter converts `null` to the string `"null"` ([#3712](https://github.com/sebastienros/jint/issues/3712)) |
| 4 | HTML §4.13: the registry, the construction stack, the element state and the reaction lane | [#3709](https://github.com/sebastienros/jint/pull/3709) | The element queue is drained as a reaction *arrives* rather than when the outermost `[CEReactions]` operation returns, and a parser-created element is upgraded at the driver's next script boundary rather than constructed by the tokenizer. There is no `ElementInternals`, so `static formAssociated` is recorded and consulted by nothing |
| 5 | The UI event interfaces, HTML's handler content attributes and every activation behaviour a click has | [#3671](https://github.com/sebastienros/jint/pull/3671), engine seams [#3696](https://github.com/sebastienros/jint/pull/3696) | — |
| 6 | The `ParserDriver` and the baton: classic, `defer` and `async` scripts, modules through the import map, `document.write`, `<link rel=stylesheet>` | [#3676](https://github.com/sebastienros/jint/pull/3676) | The refinement §6 already records: a subresource fetch finishes *before* AngleSharp's `IResourceLoader` returns, so the parse never suspends and stays on the thread it started on |
| 6 | A navigation is a fetch and a new engine; forms, history, cookies, storage and workers | [#3667](https://github.com/sebastienros/jint/pull/3667) | Images are recorded in `Page.Requests` as *not* fetched, there being nothing to render them with; a frame's document is fetched and parsed and runs no script ([#3771](https://github.com/sebastienros/jint/issues/3771)) |
| 5, 6 | The observers and the DOM views: `MutationObserver`, `IntersectionObserver`, `ResizeObserver`, `Range`, `TreeWalker`, `NodeIterator`, `DOMParser`, `XMLSerializer`, `getSelection` | [#3669](https://github.com/sebastienros/jint/pull/3669) | The two stubs the design named now carry real numbers from §8's model — but with no layout nothing can stop intersecting, so an observed target is reported once, fully intersecting, and a lazy list loads every page at once |
| 7 | `PageBudget`: `MaxTaskDuration` and `MemoryLimit` over a turn, `ForUntrustedContent`, `MaxDomNodes` | [#3679](https://github.com/sebastienros/jint/pull/3679) | — |
| 8 | `Layout/FlatLayout`, the hit test, the virtual scroll, `dispatchMouseEvent` and the activation behaviours | [#3697](https://github.com/sebastienros/jint/pull/3697) | Boxes are rows, so an excluded element takes its subtree with it — right for `display: none` and wrong for `visibility: hidden`, whose `visibility: visible` descendant CSS lets escape |
| 8 | `dispatchKeyEvent`, `insertText`, the editor, and `testdriver.js` over the same dispatcher | [#3703](https://github.com/sebastienros/jint/pull/3703) | `imeSetComposition` is accepted and changes nothing; `contenteditable` splices one text node, so <kbd>Enter</kbd> there does nothing rather than something structural |
| 8a | `PageTarget`, `AddBrowser`, `BrowserTargetHost` and the lifecycle events of a navigation | [#3680](https://github.com/sebastienros/jint/pull/3680), the target split [#3678](https://github.com/sebastienros/jint/pull/3678) | A **`tab` target** is published in front of every page, found by driving Puppeteer rather than by reading the protocol; and `frameNavigated` precedes the engine swap where Chrome interleaves it between the two context events |
| 8a | A domain of Jint's own — `Jint.getMarkdown`, `getText`, `getAccessibilitySnapshot` — and the screenshot refusal that names it | [#3681](https://github.com/sebastienros/jint/pull/3681), the extractors [#3657](https://github.com/sebastienros/jint/pull/3657) | — |
| 8a | `Network` reporting every request, and `Fetch` pausing one at the request stage | [#3700](https://github.com/sebastienros/jint/pull/3700) | The notifications and the interception run on the **transport thread**, not the page loop: moving them would deadlock the one fetch a page cannot pump through. The three absent lanes are [#3701](https://github.com/sebastienros/jint/issues/3701) |
| 8a | `Emulation` effective rather than accepted, and `Accessibility` in Chrome's `AXNode` shape | [#3704](https://github.com/sebastienros/jint/pull/3704) | A page's `@media` rules are not re-evaluated against the emulated preferences, because the render device models none of them, so a themed page reads `matchMedia` rather than `getComputedStyle` ([#3707](https://github.com/sebastienros/jint/issues/3707)). The viewport and media-type half of that was closed by [#3731](https://github.com/sebastienros/jint/pull/3731) |
| 9 | The web-platform-tests browser lane, and the eleven defects it first recorded | [#3685](https://github.com/sebastienros/jint/pull/3685), fixes [#3699](https://github.com/sebastienros/jint/pull/3699) | The four differences §9 already records — one corpus and one pin, strings through a host function rather than values through a binding, only the window wrapper synthesized, and an uncaught exception left to upstream's harness |
| 9 | The obstacle course: eighteen offline fixtures through the `Page` API, three of them again over the protocol | [#3710](https://github.com/sebastienros/jint/pull/3710) | The Playwright suite is gated on `JINT_BROWSER_CLIENTS` because its driver is a Node process; PuppeteerSharp's runs on every leg. Two fixtures are `needs triage` rather than passing |
| 10 | The `jint-browser` command line: `serve`, `fetch`, `eval`, `version` | [#3715](https://github.com/sebastienros/jint/pull/3715) | It takes no `InternalsVisibleTo` grant, so the three seams it needed were promoted onto the package rather than reached around |
| 10 | `Jint.Browser.Mcp`, the Model Context Protocol server, and `jint-browser mcp` serving it on stdio | [#3717](https://github.com/sebastienros/jint/pull/3717) | **`--http` did not ship** — the protocol's 2026-07-28 revision removed the session header from streamable HTTP, and the two ways to hold per-session state either need an `[Experimental]` handler or put an ASP.NET Core framework reference in a `dotnet tool`; and a `ref=` is the accessibility tree's own identifier rather than a `backendNodeId`, which belongs to a protocol target an MCP session has none of |

Two decisions in the v1/not-v1 table of §2 turned out differently and are worth naming here rather than
leaving a reader to compare tables. §8's model gives both observers numbers to report.
`IntersectionObserver` still reports each target once, fully intersecting. `ResizeObserver` also reports
subsequent synthetic size changes, including hidden-to-visible transitions, from page-turn checkpoints;
its entries preserve the measured size. Callback-induced changes are deferred to another task rather than
processed through a depth-limited rendering loop. And `getComputedStyle` answers a *resolved* value for the ten
properties an automation client reads to decide whether an element can be interacted with
([#3716](https://github.com/sebastienros/jint/pull/3716)) — the smallest exception to the standing decision
against an initial-value table of our own, made because without it no supported client can drive a page.
