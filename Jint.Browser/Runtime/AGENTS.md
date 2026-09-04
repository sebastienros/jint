# Agent instructions: the page runtime

> **Read this when:** You are touching `Jint.Browser/Page.cs`, `Jint.Browser/Page.Navigation.cs`, anything
> under `Jint.Browser/Runtime/`, `Jint.Browser/Layout/` or `Jint.Browser/Workers/`, or a navigation, a form
> submission, the session history, cookies, storage, a box or the scroll offset.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first, and read
> [`Jint.Browser/AGENTS.md`](../AGENTS.md) beside it — it carries the package's principle, the binding layer
> these sections stand on, the divergence tables, and the emulation section that argues
> `PageMediaEnvironment`, `EmulationState`, `NavigatorInstaller`, and `TouchEmulation`.
> Nothing below is repeated in either.
> The design this implements is [`docs/design/headless-browser.md`](../../docs/design/headless-browser.md):
> §3 for the runtime model, §5 for events, §6 for navigation and the parse, §7 for the per-page constraints.

### The page loop, and the thread rule

**One thread per page owns its engine and its DOM, and nothing else may touch either.** `Runtime/PageLoop`
is that thread: it drains a mailbox of requests, calls `Tasks.ProcessTasks()`, and parks in
`Tasks.WaitForScheduledWork` for the shorter of `BrowserOptions.PumpIdle` and
`Tasks.TimeUntilNextScheduledWork`. The engine is built on it and disposed on it, because both are
engine-owning operations, and a navigation replaces the engine from inside a mailbox request rather than from
outside. Jint starts no thread of its own; this is what makes a page's timers fire at all.

Five rules follow, and each of them is a way to break the package silently:

- **Every public `Page` member is a mailbox request, and the request is what holds the engine.** A new member
  is `_loop.PostAsync(engine => …)`, never a field read that reaches into the runtime. A caller is on some
  other thread by definition.
- **Nothing that belongs to an engine may be in the returned task.** A `JsValue` belongs to the engine that
  made it *and* to the thread that owns it, and an AngleSharp node is safe to read only while the loop is not
  mutating the tree. Convert inside the request — `JsValue.ToObject()`, a `string`, a `PageError` rendered by
  `ValueInspector` — and let the task carry the conversion. `PageTests` pins that what comes back is not from
  Jint's assembly, which is the cheapest possible check and worth keeping.
- **Posting wakes the park.** `PostAsync` writes to the mailbox and then calls `engine.Tasks.Post` with an
  empty action, purely for its documented side effect of ending a `WaitForScheduledWork`. Without it a request
  waits out `PumpIdle`; the channel write happens first, so the wake can never be lost.
- **A request that arrives after the loop stopped fails with `ObjectDisposedException`.** It is never left to
  hang, and the pending queue is failed the same way on the way down.
- **Teardown is not a request.** `PageLoop.CloseAsync` takes the action that releases the document and the
  browsing context and runs it in the loop's own shutdown, because a request would queue behind whatever is
  running — and what is running may be the very wait the close is ending. Posting it instead deadlocks.

`WaitForIdleAsync` runs its whole wait *inside* one request rather than polling from outside, because the wake
poke is itself an engine job: a probe posted from another thread would find the queue non-empty and never see
idle. Idle is `TimeUntilNextScheduledWork is null`. It holds the loop for its whole duration, so it takes the
page's cancellation token: a wait that closing could not end would make closing wait out its ceiling.

### Budgets: what a turn is, and which constraints can bound one

Read the repository-root [`AGENTS.md`](../../AGENTS.md#gotchas) constraints gotcha and
[`Jint/Constraints/AGENTS.md`](../../Jint/Constraints/AGENTS.md#bounding-a-host-driven-sequence) before
changing anything here; neither is restated. What they mean *for a page* is that the trap applies twice over,
so almost nothing an embedder would reach for bounds one. A page is a host-driven sequence of entries, so
`LimitExecutionTime` arms a fresh deadline for every `Page` call and bounds no sequence of them; and a page's
event loop is **pumped**, so a job chain under `Tasks.ProcessTasks` never reaches `ExecuteWithConstraints` at
all and a per-entry timeout there never fires even once. The two whose window the *host* owns are the two that
survive both — `OperationDeadlineConstraint` and `MemoryLimitConstraint` — and `Runtime/PageBudget` is where a
page arms them.

**A turn is one unit of work the page's thread does with the engine**, and there are exactly three kinds:
one mailbox request (`PageLoop.PostAsync`), one `ProcessTasks` drain — which is every due timer callback,
microtask, promise reaction and animation-frame batch together — and one inline `<script>`
(`PageScriptingService`, and the parser driver after it). Each takes `BrowserOptions.MaxTaskDuration` and,
where one is configured, `BrowserOptions.MemoryLimit`. Four consequences are easy to get wrong:

- **A request's bracket is outside `PostAsync`'s own `try`/`catch`**, which is what makes a budget failure the
  *caller's*: `Page.EvaluateAsync` faults with `TimeoutException`. A drain's erupts into the pump, becomes a
  `PageErrorKind.BudgetExceeded` entry, and the loop goes on — a page survives its scripts.
- **A request that pumps brackets its own turns and is posted `bracketed: false`.** `WaitForIdleAsync` is the
  one; bracketing the whole wait would charge every drain *and every park* to one budget and fail a wait
  longer than `MaxTaskDuration` for no reason. A `Page` member added later wants the default.
- **`ReplaceEngine` closes the outgoing engine's turn and opens one on the incoming engine.** A constraint
  belongs to one engine, so the turn a navigation request opened cannot be closed on the engine that replaced
  it — and doing it this way is also what stops an engine construction from spending the budget the new
  document's first script will meet.
- **A nested turn re-arms the deadline and hands the enclosing turn a full budget back**, so a document with
  many scripts is not failed for having many, while each script is bounded. The *allocation* budget is not
  re-armed: `MemoryLimitConstraint.Begin` refuses to start while the engine is executing, which is exactly
  where the parser driver will open a nested turn from.

A worker's pump takes the same bracket over its own engine's constraints, which it has because
`WorkerRequest.CreateDefaultOptions` replays the parent's constraint **factories**. What that replay does
*not* carry is a web-API setting, so `MaxActiveTimers`, `MaxResponseBytes`, `FetchTimeout` and the page's
user agent are named again in `ThreadPerWorkerProvider`; a new page-sized limit needs the same second call or a worker keeps the engine
default.

`BrowserOptions.ForUntrustedContent` applies `Options.ForUntrustedCode` from inside the factory's own
construction callback, so the profile is expanded before the engine builds anything and re-expanded over the
host's `ConfigureEngine` callbacks. It registers the same two constraints, which is why `PageBudget` *finds*
them on the engine rather than creating them: one code path arms a turn under either profile. Two things it
costs a page and both are deliberate — there is no module loader (a page has none anyway; a worker's is
installed by the provider afterwards and is untouched) and `RetainFunctionSourceText` is off, so a recorded
stack names less.

`MaxDomNodes` is one number checked against two quantities, and that is deliberate rather than a compromise.
The parse counts the *document* and refuses a navigation over the ceiling; `DomRealm` counts the *node
wrappers* one engine has made and refuses the projection that would pass it. Making them share a count — the
parse seeding the wrapper counter — was tried and is wrong: it makes merely **walking** a document of the
permitted size a refusal, which is a limit no real page survives, and a framework touching most of its own
tree is the ordinary case rather than the abusive one. So the two together bound a page at roughly twice the
number, and a host setting it should read it as "this big a document, and this many nodes handed to script".
Script-driven growth is bounded by `MemoryLimit` first — an `innerHTML` assignment materializes a subtree with
no wrapper of its own, so nothing counts it — and by this second.

### The window: why the global stays `GlobalObject`, and the trap in it

`Runtime/WindowInstaller` sets the global object's `[[Prototype]]` to
`Window.prototype → WindowProperties → EventTarget.prototype → Object.prototype` and installs the
per-document singletons (`window`, `self`, `frames`, `top`, `parent`, `document`, `location`, `screen`) as own
lazy globals through the public `Engine.AddLazyGlobal`. Substituting a host global would forfeit the
global-identifier inline cache and mean re-installing every intrinsic; the prototype swap costs nothing, and
`GlobalEnvironment.HasBindingOnGlobalPrototype` already resolves a prototype member as a global.

**`WindowProperties` is HTML's named access, and where it sits is the whole of why it is free.**
`Runtime/WindowNamedProperties` makes `<div id=x>` and `<iframe name=x>` reach script as `x`, and WebIDL puts
the *named properties object* below the interface prototype object rather than on the global — so a name the
global owns and a member `Window.prototype` declares are both found before the document is consulted, and what
reaches the lookup is a **miss**. Nothing about an ordinary global read changes, structurally rather than by
hope: `JintIdentifierExpression.TryRememberGlobalBinding` admits only a descriptor the global object *owns*,
because a prototype mutation bumps no global version, so a name answered there is never cached and never
displaces one that is. Two divergences are stated on the class: a name several elements answer to gives the
first rather than an `HTMLCollection`, and an `<iframe name=x>` gives the frame element rather than a nested
`WindowProxy`, there being no engine in a child frame.

**`window.event` is the one member that is an own property of the global** rather than an accessor on the
shaped prototype, because WebIDL's `[Global]` puts an interface's members on the global object itself and
`event` is the one a page can tell apart (`assert_own_property(window, "event")`). The slot it reads is the
engine's — DOM's *current event*, maintained by the dispatch — and the engine maintains it only because the
installer sets `GlobalEventTarget.IsWindow`, which is also what turns on DOM's default passive value.

**The trap: a shape *method* on `Window.prototype` cannot find its page.** A page calls `alert(…)` and
`requestAnimationFrame(…)` unqualified, and an unqualified call to a member of the global object passes
`undefined` as the receiver — the global environment record has no `with` base object — so a member body
reading `thisObject` has no engine to reach the runtime through and can only answer `Illegal invocation`.
Accessors are unaffected, because a bare identifier *read* goes through the global object's `[[Get]]` with the
global as receiver. So: **an operation that needs its page is a `PerRealmSlot` holding a `ClrFunction` bound to
the engine** (`WindowInstaller.Operation`), and only an operation that needs nothing — `stop`, `blur`,
`open` — stays a `Method`. Adding a window operation the other way compiles, passes `window.foo()`, and fails
`foo()`. `getSelection` crossed that line the moment it had a selection to answer.

Several members are own properties of their object rather than accessors on a shaped prototype, and each says
why in place. On `document`: `defaultView` (the binding excludes AngleSharp's `IWindow`), `currentScript`
(AngleSharp answers the wrong thing — see the divergence table), `cookie` (the jar is the context's, and
`HttpOnly` has to be enforced against script) and `URL`/`documentURI`/`baseURI`/`referrer` (the URL is the
page's — see the next section). On a **form wrapper**: `submit` and `requestSubmit`, installed by
`DomHostHooks.WrapperCreated`, because neither is generated and neither could be — AngleSharp's `Submit()`
returns a `Task`, there is no `requestSubmit` at all, and its own submission navigates on the calling thread
through its own event bus. And the whole of **`Location`**, which `Runtime/LocationInstaller` owns outright.
**A member installed this way shadows the prototype and is visible to `Object.getOwnPropertyNames`**; it is
the right tool for one object and the wrong one for a class, because a shaped prototype that takes an
undeclared property loses its shape and its inline caching with it.

### The events bridge has a file of its own

**AngleSharp's event bus is neither observed nor driven by script** (design doc §5). Everything
script-visible is a Jint `Event` dispatched through the engine's tree-aware dispatcher, at the algorithm
points this package owns. Which point raises which event, what activation without a layout can and cannot be,
why the handler content attributes need no notification from AngleSharp, where form submission is cut in half,
and the keyboard and the editor under it are [`../Events/AGENTS.md`](../Events/AGENTS.md). Two rules to carry
across without opening it: **every listener the loop fires returns to a microtask checkpoint**, because the
point that fires it is a turn of this loop rather than a script — `AnimationFrameLane` owes the same cleanup
by hand, since one frame is one job over many callbacks — and `Runtime/FormSubmitter` is the second half of a
submission whose first half is `Events/FormSubmission`, cut where HTML cuts it.

### The expression wait, and why it is not a pump

`Page.WaitForAsync(expression, timeout)` is the general form of `Page.Input.cs`'s two other waits, and it
is built the same way rather than the obvious way: the page is looked at from **off** the loop, every
`WaitPoll`, through one mailbox request per look. Holding the loop and pumping inside a single request —
which is what `WaitForIdleAsync` does — would be a wait for something the page could never do, because the
loop it is holding is the one a navigation commits on and the one a mailbox request runs on. One wait
mechanism, three public members over it.

**An expression that throws is not yet true**, so a condition written against an element a framework has
not rendered is usable; the last failure is kept, and a timeout rethrows it rather than answering
`false`, so a typo in the expression is a failure with a reason.

### The flat box model, and the one number it is built from

`Layout/FlatLayout` is the whole of what stands in for a layout engine, and its rule is one sentence: **every
rendered element gets an ordinal in tree order and owns the row `[i·R, (i+1)·R)`, with `R = 16`**. Its box
starts at that row and is `R × (1 + rendered descendants)` tall and the viewport wide, so boxes nest exactly
as the tree does and never straddle. Design doc §8 is the statement of intent; this is what was built.

**One model answers both sides, and that is the point.** `Element.getBoundingClientRect`,
`document.elementFromPoint`, `DOM.getBoxModel`, `DOM.getContentQuads`, `DOM.getNodeForLocation` and
`Input.dispatchMouseEvent(x, y)` are all this class, so a client that reads a box, clicks its centre and asks
what was hit is told one consistent story rather than three approximations that disagree. Three consequences
fall out of the row rule and every one of them is load-bearing:

- **The hit test is a division.** The deepest box containing a point is always the owner of the row the point
  falls in, because a descendant's rows all come after its ancestor's first one. So the centre of a leaf hits
  the leaf, the centre of a container hits a descendant — as a browser does — and the click bubbles back up.
- **The rendered set is HTML's minus what a rendering would have needed.** `<head>` and its subtree, a
  `<script>`, `<style>`, `<template>` or `<noscript>` wherever it sits, and whatever R7's `ElementVisibility`
  calls not rendered — the `hidden` content attribute, `display: none`, `visibility: hidden|collapse` from the
  cascade. `aria-hidden` deliberately does **not** remove a box, which is why the question asked is
  `RenderingReasonFor` and not `ReasonFor`. An element with no box answers zeros, no client rectangles, and
  `-32000` rather than a box of zeros over the protocol: a client reads zeros as a real box at the origin.
- **An excluded element takes its subtree with it**, which is right for `display: none` and wrong for
  `visibility: hidden`, whose `visibility: visible` descendant CSS lets escape. A model whose boxes are rows
  cannot give a descendant a row inside a parent that has none, and the nesting is what the hit test rests on.

**It is recomputed per query and never cached.** A cache needs an invalidation signal, and the only one
available is an AngleSharp `MutationObserver` over the whole document — which would make every DOM mutation on
every page pay for mutation records whether or not anything ever asks for a box. The walk is linear in the
size of the document and touches no engine state.

**The scroll is virtual, and it is the only state.** `Layout/PageLayout` holds a `scrollY` clamped to the
document, and every viewport-relative answer subtracts it; `scrollX` is zero and stays zero, because every box
is exactly as wide as the viewport. `window.scrollTo`/`scrollBy`/`scroll`, `element.scrollIntoView`,
`DOM.scrollIntoViewIfNeeded` and a wheel event all set it, and `window.scrollY`, `pageYOffset` and
`document.scrollingElement.scrollTop` read it. That is what lets a client whose click path insists on "scroll
it into view, then check the box is inside the viewport" — Playwright's does — succeed on a long page. A
change queues one `scroll` at the document per turn, on the engine's own queue.

**Only the scrolling element scrolls**: `scrollTop` on `document.scrollingElement` is the page's offset and
writing it moves the page; on anything else it reads zero and a write is ignored. `scrollIntoView` aligns an
element's **first row** and never its whole box, because a container's box spans its subtree and centring
*that* would scroll past everything in it.

**The DOM-side members are `overrides.json` `additions` entries**, with their bodies in `Layout/LayoutMembers`
— `getBoundingClientRect`, `getClientRects`, the `client*`/`scroll*` metrics, `scrollIntoView`, `HTMLElement`'s
`offset*` family, `document.elementFromPoint`/`elementsFromPoint`/`scrollingElement`. **Never hand-edit a
`.g.cs`**; regenerate with `JINT_DOM_BINDINGS=update`. A rectangle is a plain object shaped like `DOMRect`
rather than an instance of one (`Layout/DomRects` says why), and `IntersectionObserver` and `ResizeObserver`
entries now carry real numbers through the same factory — which is what
[`../AGENTS.md`](../AGENTS.md)'s observer section promised when they were zeros. `Range.getBoundingClientRect`
stays zeros: this model gives an *element* a row, and a range is a pair of positions inside text nothing here
measures.

### Navigation is a fetch and a new engine

`Page.NavigateAsync` runs off the page loop and commits onto it, and that split is the design:

- **The fetch is `FetchTransport`, the engine-free layer**, so the page goes on pumping timers and obeying a
  close while the response is on its way — and the engine the new document will run in does not exist yet, so
  there is none for it to be on. `Runtime/DocumentFetch` drives `SendForStreamAsync` (the redirect loop and
  the per-hop policy re-check) and reads the body itself under `BrowserOptions.MaxDocumentBytes`. It therefore
  owes the observer the final response and completion, which only `SendAsync` makes for itself; every caller
  of `SendForStreamAsync` pays that debt through the engine's own `FetchObservation.FinalResponse`, and
  `Runtime/SubresourceFetch`, `Workers/PageModuleLoader` and the engine's `XhrOperation` and
  `EventSourceConnection` are the other four.
  The **first hop's `UrlFilter` is run by the page**, not the transport, which deliberately never runs a host
  filter twice per request.
- **A subresource is `Runtime/SubresourceFetch`**, `DocumentFetch`'s sibling: same transport, same per-hop
  re-check, same jar, bounded by `BrowserOptions.MaxSubresourceBytes` and `SubresourceTimeout`. Two rules
  differ, because a subresource is fetched on someone else's behalf: its credentials mode is `same-origin`
  rather than the `include` a top-level navigation gets, and a status the server calls an error is a failure
  rather than a document to show.
- **The commit is one mailbox request**, in order: unload (`beforeunload`, `pagehide`, `unload`), cancel that
  engine's `CancellationTokenSource`, `PageLoop.ReplaceEngine` (which disposes the old one), parse. **The
  token is what makes abandonment real**: `FetchOperation` and `XhrOperation` read
  `Constraints.Find<CancellationConstraint>()`, so a document's requests die with its engine only because
  `BrowserEngineFactory` registered one linked to the page's own.
- **`WaitUntil` is three signals, not three implementations** — `PageDocument.Load` reports
  `NavigationPhase.Committed`, `DomContentLoaded` and `Loaded`, and the caller awaits the one it asked for
  racing the whole request. All three land in one turn while the parse is synchronous; the parser driver makes
  them separate moments.
- **One navigation at a time**, behind a `SemaphoreSlim`. A script's is `Task.Run` plus a page error on
  failure, because the document that script is in is the one being replaced and there is nobody to throw to.
- **A same-document fragment move never leaves the loop.** It neither fetches, replaces the engine nor can
  fail, so there is nothing for the off-loop half to do — and going there anyway is observable:
  `Page.RequestNavigation` used to send an `<a href="#x">` round the thread pool and the gate, which put the
  commit behind every timer already due, so a page that clicked a link and then spun zero-delay timers waiting
  for `hashchange` waited out the whole chain (measured at turn 20 of 20; turn 1 now). It is a job on the
  engine's own queue instead. **The one question it asks is whose document is moving**, and the engine
  answers it: HTML's *navigate to a fragment* is a task on the event loop of the document whose URL changes,
  so it is a same-document move exactly when the request came from the engine the page is showing. Neither
  `_load` being set nor the gate being free is that question — `_load` is null for the whole of a document's
  parse and the gate is held by the navigation that produced it, so gating on either refused the fragment arm
  to every script that runs during its own parse and queued a whole navigation behind the gate instead
  (#3693, and twenty-two rows of `dom/events/Event-dispatch-single-activation-behavior.html`). A commit
  already on its way is ordered by the queue rather than by a refusal: a job posted first runs first, and one
  the commit overtakes dies with the engine it was posted to.

**Everything the network touches is the context's** — `Runtime/PageNetwork`, one per `BrowserContext`: the
client, the composed `UrlFilter` (host filter and `BlockPrivateNetwork` combined once), the `CookieJar` and
the `StoragePartitionProvider`. Two pages of a site therefore share a session and two contexts are two
visitors, and one filter bounds the document, every subresource, `fetch`, `XMLHttpRequest` and a worker's
module loads alike.

### The request log is the protocol's seam too

How `PageNetworkRecorder` serves the `Network` and `Fetch` domains as well as `Page.Requests`, and on which
thread a listener is told, is in [the package file](../DevTools/AGENTS.md#the-request-log-is-the-protocols-seam-too).

### The one observer, and what it is owed

`Runtime/IPageObserver` is the seam the protocol layer hangs off, and **a page has exactly one observer**,
set through `Page.Observe` from the loop thread so that a navigation cannot slip between the registration and
the first call. Everything a client is told about a page is one of its calls; nothing else reads it.

- **`DocumentCreated` is the load-bearing one.** It fires in `LoadInto`, after the engine is built and its
  window installed and **before `ParserDriver.Load`**, on the loop — the only moment at which a watcher can
  replace its engine, re-install what a client added and run what a client asked to run on every new
  document, all of which must be in place before the document's first script.
- **A `loaderId` names one committed document**, minted when the navigation starts so that
  `NavigationStarted` and every `Phase` of that load agree, and carried by a same-document move because the
  document did not change. Each is prefixed per page, so two pages never mint the same.
- **`Phase` is the driver's three, composed with the caller's.** `LoadInto` wraps whatever `onPhase` a
  navigation passed, so a watcher hears `Committed`, `DomContentLoaded` and `Loaded` at exactly the points
  `WaitUntilState` answers at — on the loop, because that is where the driver raises them.
- **`DialogOpening` runs before the host's own `Page.DialogOpened` handler and `DialogClosed` after it.** A
  watcher answers from a decision it already holds — the page has no thread to block, and the thread a
  protocol client's answer would arrive on is the one inside the script that called `alert` — and the host,
  which owns the page, overrides it.
- **`NetworkIdle` is timed off the loop**, on a timer of the page's own, because a page with nothing
  scheduled never turns its loop and would never notice the quiet. Armed at `Loaded`, cancelled by the next
  navigation or by closing.
- **Nothing an observer is handed may be kept.** A `PageRuntime` belongs to the document that is loading and
  a `DialogEventArgs` to the call that raised it.

### The parser driver has a file of its own

The baton between the parser thread and the page loop, which thread runs what, the divergences that shape
costs, and how scripts, modules, import maps and style sheets load are
[`Parsing/AGENTS.md`](Parsing/AGENTS.md). The one rule to carry across without opening it: exactly one
holder touches the engine and the DOM at a time, and a fetch a *script* triggered never pumps.

