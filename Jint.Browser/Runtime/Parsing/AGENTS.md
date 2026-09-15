# Agent instructions: the parser driver

> **Read this when:** You are touching `Jint.Browser/Runtime/Parsing/` — how a document is parsed on a thread
> of its own and served by the page loop, how a script, a module, an import map or a style sheet loads, and
> what `document.write` may do.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../../AGENTS.md). Read that first, then [`Jint.Browser/Runtime/AGENTS.md`](../AGENTS.md)
> for the page loop, the thread rule and the budgets. Nothing below is repeated in either.

### The parser driver, and the baton

`Runtime/Parsing/` is the parse: `ParserDriver` owns it, `ParserBaton` is the hand-off, `PageResourceLoader`
is what AngleSharp asks for a subresource, `Runtime/PageScriptingService` is what it asks to run a script, and
`PageModuleScriptLoader` plus `ImportMap` are the module half AngleSharp does not have.

**Why there are two threads.** AngleSharp's parse is an asynchronous method whose every `await` carries
`ConfigureAwait(false)`, and `HtmlDomBuilder.ParseAsync` awaits the task a script's `RunAsync` produces. The
moment anything in that chain genuinely suspends — an external `<script src>` is the first thing that does —
the parse, and the scripting hook with it, resume on a pool thread. Driving it on the loop and blocking would
put the engine and the DOM in two threads' hands with nothing to say so. So the parse runs on a thread of its
own and hands a **baton** back to the loop for everything that needs the engine or the DOM.

**The hand-off is a blocking handshake.** `ParserBaton.RunOnLoop` queues the work, wakes the loop through
`engine.Tasks.Post`, and parks the parser thread on a `ManualResetEventSlim` until the loop has finished it.
That is a stronger form of the design's `RunContinuationsAsynchronously`: the parser cannot resume inline on
the loop thread because it cannot resume at all until the loop releases it. **The whole invariant is that one
property**, and it is why nothing else in the driver needs a lock.

**Timers fire exactly where a browser fires them.** While the parser is tokenizing it holds the baton and the
loop runs nothing, which is right — in a browser the parser *is* the task the event loop is running. While the
loop is fetching a parser-blocking script it holds the baton and pumps one task plus its microtasks, so timers, promise jobs
and animation frames run while the page waits for the network. `ParserBaton.PumpUntil` is that pump, and
`DocumentLoadTests.TimersFireWhileAParserBlockingScriptIsOnItsWay` is the proof (it reads zero without it).
What that pump swallows it reports: a job erupting out of `ProcessTask` must not end the load, and it must
not vanish either, so it goes to the page recorder — which is the only way an execution constraint aborting
a timer mid-parse is visible at all.

**A script that navigates does not cut the parse short.** `location.href = '…'` takes the navigation gate
the running load still holds and its commit is a mailbox request, and the whole parse is *one* such request
— so every remaining script of the outgoing document runs, the load finishes, and only then is the engine
replaced and its token cancelled. Nothing stops a document mid-parse except closing the page, which cancels
the token every subresource fetch is linked to: the fetch in flight fails, the parse runs out and
`ParserBaton.Serve` stops serving.

**Every turn inside the parse is a turn.** A document load is *one* mailbox request, so `PageLoop` opens one
budget turn around the whole of it — and that turn's deadline is wall-clock, so by the time a slow
`<script src>` has come back it is long gone. Each script therefore takes a nested turn of its own
(`ParserDriver.Execute`, `RunModule`), and so does **each task and its microtask checkpoint** the pump runs
(`ParserBaton.Drain`), through the engine's `PageBudget` hook, just as in `PageLoop.Pump`. Without
that last one a timer callback that fires late in a slow load is killed for the enclosing turn's exhaustion
rather than its own — `DocumentLoadTests.ATimerThatFiresLateInASlowLoadGetsABudgetOfItsOwn` is that case, and
it fails without the bracket. A budget running out is a `PageErrorKind.BudgetExceeded` and the load goes on,
which is what `PageBudget` promises and what makes a page whose first script is a loop still a page a host
can read.

**The residual, stated because it is the one thing a budget does not reach.** `Serve` holds the loop for the
whole parse, so while the baton is in the parser's hands the loop runs nothing and an execution constraint
cannot fire. Every fetch is bounded by `BrowserOptions.SubresourceTimeout` and every script and every task
takes a turn, which leaves AngleSharp's tokenizer as the only unbounded step. There is still no *total*
budget on the parse — `PageLoop`'s turn is the enclosing one, and nothing re-arms it for the parse as a whole
— so what a wedged parser thread costs is bounded by the page's own token instead: closing the page ends
`Serve`, abandons the parse and fails the parser's next hand-off, so `Page.CloseAsync` is never held by a
parse that will not finish.

**Every fetch the parser asks for finishes before the loader returns.** `PageResourceLoader.FetchAsync` hands
the baton over, the loop fetches while pumping, and AngleSharp receives an already-completed `IDownload` — so
the parse never suspends and the thread it runs on never changes. `ParserBaton.ParserHopped` is the check
rather than the belief: a change of parser thread means a step suspended where none was expected, and it
becomes a page error. **A fetch a *script* triggered blocks instead of pumping**, because pumping from inside
a running script would run the page's jobs in the middle of one; that is the price of an inserted
`<script src>`, and it is stated in `ParserDriver.Fetch`. One shape of inserted script does not run at all:
AngleSharp prepares one when it is *inserted*, so `el.src = '…'` **after** `appendChild` is never fetched —
set the source first, which is what a page does anyway.

**Who runs what.** A classic script — inline, external, `defer`, `async` — is prepared and ordered by
AngleSharp, which is what buys parser-blocking, document order, the deferred queue and the `document.write`
insertion point without re-implementing any of them. `PageScriptingService.SupportsType` answers `false` for
`module`, `importmap` and every unknown type, so AngleSharp never prepares them and the driver runs the
modules itself after the parse — which is where HTML puts them anyway. Four scheduling divergences follow and
each is deliberate: a `defer`/`async` script's *download* is not overlapped with the parse; an `async` script
executes in document order at the end of the parse rather than the instant its fetch lands; a deferred classic
script runs before a module script that precedes it in the document, because they are two queues rather than
HTML's one; and the first import map found anywhere applies to every module, because none of them could have
resolved before the parse ended anyway.

**Anything served an XML MIME type is read with the XML parser, the page's own document included.**
[HTML's *read XML*](https://html.spec.whatwg.org/multipage/document-lifecycle.html#read-xml) applies to every
[XML MIME type](https://mimesniff.spec.whatwg.org/#xml-mime-type), and three pieces make it so. `WithXml()`
is what supplies an XML document at all — without it `<foo>x</foo>` served as `text/xml` came back with
`documentElement.tagName === "HTML"` and every XML rule a page then asked about was the wrong document's.
`PageDocumentFactory` widens the mapping it leaves: `WithXml()` registers `text/xml`, `application/xml` and
`image/svg+xml` and leaves `application/xhtml+xml` on the **HTML** creator the base class seeded, with every
other `+xml` type unmapped, so the subclass takes AngleSharp's own XML creator back out of the table and
answers the rest from `CreateDefaultAsync`. And `Parse` states the *response's* content type rather than a
fixed `text/html`, which is what lets the page's own document reach it — `DocumentFetch` used to refuse an
XML navigation outright, so `DOMParser`, a frame and `Page.NavigateAsync` gave three answers for one sequence
of bytes and now give one. Three rules go with it:

- **What `Parse` states is the navigate rules' answer, not the response's header.** A text document arrives
  as the `<pre>` skeleton *read text* already produced, so it is `text/html` whatever the server called it;
  only markup and XML carry their own type through. The charset is always `utf-8` because `DocumentFetch`
  decoded the bytes, so a `<meta charset>` or an XML declaration naming another one is not believed twice.
- **A `<script>` in an XML document does not run.** AngleSharp.Xml's tree construction has no *prepare a
  script element* step and never asks for the scripting service, so the element is in the tree with its text
  and nothing else. That is what `DOMParser` requires and a divergence for a frame and for a page;
  `Dom/divergences.md` carries it, and it is why no XHTML wpt document is vendorable — each one loads
  `testharness.js` and would report nothing.
- **`image/svg+xml` stays where AngleSharp put it**, on the creator that builds an `SvgDocument` rather than
  a plain XML one, which is the document DOM §4.5.1 names for that namespace. Only a type AngleSharp has no
  answer for reaches the widened default.

**A document's culture is the engine's, not the thread's.** AngleSharp resolves `:lang()` on an element with
no inherited language through its browsing context's culture, and a context given none takes
`CultureInfo.CurrentCulture` off whatever thread is parsing — so the same document answered a selector
differently on two machines. The parse hands the context `Options.Culture`, which is what a host sets through
`ConfigureEngine` and which itself defaults to the current culture, so nothing moves for a host that sets
none; what moves is that the answer is now the *page's* rather than the host's.

**`document.readyState` is the page's shadow.** `PageRuntime.ReadyState` moves `loading` → `interactive` →
`complete` and `ParserDriver.SetReadyState` fires the `readystatechange` that goes with each, because
`Document.ReadyState`'s setter is protected and unreachable from outside AngleSharp's assembly. AngleSharp's
own value is read at exactly one point — `ObserveReadiness`, on the way into a script, which is the only way
to see the moment it starts the deferred queue. `DOMContentLoaded` (bubbling, at the document) follows the
module scripts; `complete` and then `load` and `pageshow` (at the window) follow every subresource, which is
the order HTML gives and the reason a `load` listener reads `"complete"`.

**What is not fetched is recorded, not skipped.** A media element, an `<embed>`, a non-stylesheet `<link>`:
there is no rendering to need them, so the reference goes into `Page.Requests` with a
`PageRequest.NotFetchedReason` and no socket is opened. A refusal and a failure are both a download that
completes with a `null` response, which is the shape AngleSharp's own processors already test for; the `load`
and `error` a *page* hears are dispatched through Jint's dispatcher, because AngleSharp's go into its own
listener lists. `integrity` and `crossorigin` are accepted and ignored, and say so here rather than in a
sentence nobody reads.

**An image *is* fetched, and what is read out of it is thirty bytes.** HTML §4.8.4.3's image request is what
`img.complete`, `currentSrc`, `naturalWidth`/`naturalHeight`, `width`/`height` and the `load`/`error` events
are answers about, and a page that has none of them is a page every lazy-loading library and every UI shell
waits on for ever. `ParserDriver.FetchImage` serves the request AngleSharp's own `ImageRequestProcessor`
makes — for an `<img>` and for an `<input type=image>` alike — and `Media/ImageHeader` reads the intrinsic
size out of the container header and **never a pixel**: PNG, JPEG, GIF, WebP, BMP, ICO and SVG state one, and
anything else is HTML's *broken* state with an `error` event rather than an available image of 0×0.
`Media/PageImages` holds the current-request state the four members answer from, because AngleSharp's own
`IsCompleted` is "an `IImageInfo` exists" and no `IResourceService<IImageInfo>` is registered — registering
one would mean decoding. Four things follow and each is load-bearing:

- **The bound is `BrowserOptions.MaxImageRequests`**, counted over the document, with `MaxSubresourceBytes`
  and `SubresourceTimeout` bounding each request as they do a script's. **Zero is the opt-out and is exactly
  what this browser did before**: the reference is recorded, no socket is opened, and no event is fired,
  because nothing was attempted.
- **An image's `load`/`error` waits for the tokenizer; a style sheet's does not.** Both are queued as element
  tasks through `QueueResourceEvent`, and both delay the window's `load`. But this browser yields to its loop
  while it *fetches* an image, where a browser would have carried on tokenizing — so delivering there would
  make `<img src>` followed by a `<script>` that installs `onload` miss the event, which no browser does. The
  parser really does wait for a style sheet, and `AStyleSheetLoadDuringAParserNetworkWaitSeesTheInstalledSheet`
  pins that its `load` arrives while it does.
- **`loading=lazy` loads eagerly**, because whether an image is within the lazy load root's scrolling area is
  a question about a layout there is none of. Never loading one would leave every image of an infinite-scroll
  page `complete === false` for ever, which is the state those libraries block on — the same argument
  `IntersectionObserver` makes for reporting every target as intersecting.
- **Which URL is fetched is `Media/ImageSourceSet`'s, not AngleSharp's.** HTML §4.8.4.3.6's source set reads
  the `x` and `w` descriptors, the `sizes` lengths and each `<source>`'s `media` and `type`; AngleSharp's
  `SourceSet.GetCandidates` reads none of them and yields the first candidate it finds, so the request it
  makes names the wrong image. The request stays AngleSharp's and only its URL is decided here, against the
  page's own viewport and media environment — the value `matchMedia` answers from, so a client that emulates
  a viewport moves the selection with it. `img.decode()` is `Media/ImageDecode` over the same state: it is
  the availability the current request already has rather than a bitmap, because there is no paint for a
  decode to be ahead of.
- **What no header can say is stated rather than guessed**: an animated GIF is its logical screen and has no
  frames, there is no colour and no EXIF orientation, a file whose header disagrees with its pixels is
  believed, and a broken container has no width at all. `Dom/divergences.md` carries the rows a page can see,
  including the two AngleSharp gaps this leaves — an `<img src="">` fires no `error` because AngleSharp asks
  the loader for nothing when it selects no source, and an `<img>` inside a `<template>` *is* fetched because
  AngleSharp gives a template's contents no owner document of their own.

**Two schemes reach no socket, and one of them carries a body.** `about:blank` is answered as the empty HTML
document a frame's `src` most often names. A `data:` URL is answered by
[Fetch §5.2's processor](https://fetch.spec.whatwg.org/#data-url-processor) — `Jint/WebApi/Fetch/DataUrl.cs`,
the *only* implementation of it in the repository, which `Page.Navigation` also uses so that a navigation and
a `<script src="data:…">` cannot disagree about the same URL. It runs before the network-scheme check and
therefore before the `UrlFilter`, the jar and the redirect budget, because there is nothing there for any of
them to decide — the same order `fetch`'s `blob` arm takes. **`MaxSubresourceBytes` still applies**: a page
may not escape a size ceiling by inlining, and nothing is written to `Page.Requests`, because a page that
opened no socket made no request. Forgiving-base64 and percent-decoding are what the processor uses and the
BCL's stricter pair is not it: `data:;base64,YQ` decodes in a browser and throws in `Convert`.

**A response URL carries its fragment, and that includes a script's.**
[Fetch's response URL](https://fetch.spec.whatwg.org/#concept-response-url) is the request's — the fragment
is left out of the request-target and of nothing else — so `ParserDriver.ResponseUrl` puts
`SubresourceFetch`'s separately-carried fragment back on every answer rather than only a nested document's.
It is what [report an exception](https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception)
names, so without it `<script src="a.js#">` reported a URL its own `src` did not reflect.

**A linked stylesheet completes after CSS processing, not after its fetch.** `PageStylingService` delegates
the parse to AngleSharp.Css, then hands the completion to the driver. Both `load` and `error` are engine
tasks: an inserting script and its microtasks finish first, and the processor has assigned `link.sheet`
before a load listener reads it. Parsing failures are reported and rethrown to AngleSharp's processor,
never converted into success. The driver drains outstanding stylesheet events before window `load`,
including sheets inserted by those handlers; each event keeps the task budget and document cancellation.
Queuing at the fetch instead would let a parser-time network pump deliver before the CSSOM exists.
`Engine.Execute` also drains tasks for nested script elements, so a delivery reached while `currentScript`
is set is deferred and re-posted after the outermost element restores it. Otherwise a stylesheet callback
could run in the middle of the AMD loader's inserting script even though it was queued as a task.

**A frame's document is fetched, and the nested browsing context is AngleSharp's.**
`HtmlFrameElementBase.SetupElement` already makes a child context per frame element and asks the loader for
the document to put in it; refusing that request was the whole of why `contentDocument` was `null` and
`iframe.onload` never arrived ([#3771](https://github.com/sebastienros/jint/issues/3771)). `ParserDriver.FetchFrame`
answers it instead — `about:blank` from nothing, everything else over the page's own network position under
`MaxSubresourceBytes` and `SubresourceTimeout` — and AngleSharp opens the response, parses it and, for a
frame's own frames, comes back here. Four things follow and each is load-bearing:

- **The child context copies the page's services.** Classic scripts are fetched under the existing resource
  limits, then `RunClassicScript` enters the child document's `RealmScope` on the loop. Inline, external,
  deferred and asynchronous classic scripts use that global. `CanRunFrame` restricts this to documents
  same-origin with the top page and with no sandbox attribute; cross-origin WindowProxy access and sandbox
  policies are not implemented. Module scripts and import maps remain principal-document services.
- **Each document has its own global, DOM brands, current script, readiness and window event target.**
  `FrameWindows.ForDocument` installs before a script runs, without needing the frame element's
  `ContentDocument` to have been published. A child global has its own intrinsics, DOM/UI constructors,
  Web APIs and Window handlers. It never inherits page-defined globals. Parent/top, defaultView, named
  elements and indexed frames resolve against that document's context and tree.
- **`FinishFrame` delivers readiness, DOMContentLoaded, nested frame loads, complete, load and pageshow.**
  Initial frames still finish after the principal DOMContentLoaded and before its load. Source-attribute
  notifications queue completion for frames created after page load through the same deferred resource
  event mechanism as images. Nothing touches the engine in the native attribute callback outside the baton.
  A document completes once; detached frames and frames with no document do not receive load.
- **`BrowserOptions.MaxFrameDocuments` is counted across requests**, because a page pointing a frame at
  itself would otherwise recurse until the parser thread's stack ran out. `srcdoc` has no resource request
  and remains outside that count. Native setup can reopen srcdoc in the same browsing context: each opened
  document gets a new realm association, while old documents and their nodes retain their creation brands.

**Remaining frame boundaries are explicit.** An empty iframe still has no native document: AngleSharp
exposes no nested-context initialization seam on its public iframe interface. Legacy frames, WindowProxy
navigation, child module loading, and realm-specific browser services (custom-element registries, observers,
selection, history, and network positioning) remain separate work. A child's Web API installation uses the
page's network configuration; its browser-specific globals contain only the implemented frame surface.
Location writes throw.

**`document.write` during the parse is AngleSharp's; after it, *which document* decides who performs it.**
During a parse it is AngleSharp's own call and it is right — its writable text source inserts at the parser's
index and the script processor restores the index afterwards, so the written markup is the next thing the
tokenizer reads. Afterwards HTML implies `document.open()`, which AngleSharp implements by unloading through
its own browsing context on the calling thread and rebuilding the document behind the page's back, so the
steps are owned here instead and `DomHostHooks.TargetOf` is what chooses. **The displayed document — the
page's, or any frame of it — keeps its no-op and its recorded page error**, because replacing it means
unloading a document, swapping the engine the page runs on and re-committing a navigation; do not implement
it here. A **secondary** document (`DOMParser`'s, `createHTMLDocument`'s) has no page loop, no engine to swap
and no navigation gate, so `Dom/DynamicMarkupInsertion` runs HTML's open, write and close steps against it in
place, the document object itself never being replaced. An XML document is `InvalidStateError` at step 1
whatever its readiness.
