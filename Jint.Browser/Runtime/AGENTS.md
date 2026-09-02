# Agent instructions: the page runtime

> **Read this when:** You are touching `Jint.Browser/Page.cs`, `Jint.Browser/Page.Navigation.cs`, anything
> under `Jint.Browser/Runtime/` or `Jint.Browser/Workers/`, or a navigation, a form submission, the session
> history, cookies or storage.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first, and read
> [`Jint.Browser/AGENTS.md`](../AGENTS.md) beside it — it carries the package's principle, the binding layer
> these sections stand on, and the divergence tables. Nothing below is repeated in either.
> The design this implements is [`docs/design/headless-browser.md`](../../docs/design/headless-browser.md):
> §3 for the runtime model, §5 for events, §6 for navigation and the parse, §7 for the per-page constraints.

### The page loop, and the thread rule

**One thread per page owns its engine and its DOM, and nothing else may touch either.** `Runtime/PageLoop`
is that thread: it drains a `Channel<Action<Engine?>>` mailbox, calls `Tasks.ProcessTasks()`, and parks in
`Tasks.WaitForScheduledWork` for the shorter of `BrowserOptions.PumpIdle` and
`Tasks.TimeUntilNextScheduledWork`. The engine is built on it and disposed on it, because both are
engine-owning operations, and a navigation replaces the engine from inside a mailbox request rather than from
outside. Jint starts no thread of its own; this is what makes a page's timers fire at all.

Four rules follow, and each of them is a way to break the package silently:

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

### The window: why the global stays `GlobalObject`, and the trap in it

`Runtime/WindowInstaller` sets the global object's `[[Prototype]]` to
`Window.prototype → EventTarget.prototype → Object.prototype` and installs the per-document singletons
(`window`, `self`, `frames`, `top`, `parent`, `document`, `location`, `screen`) as own lazy globals through the
public `Engine.AddLazyGlobal`. Substituting a host global would forfeit the global-identifier inline cache and
mean re-installing every intrinsic; the prototype swap costs nothing, and
`GlobalEnvironment.HasBindingOnGlobalPrototype` already resolves a prototype member as a global.

**The trap: a shape *method* on `Window.prototype` cannot find its page.** A page calls `alert(…)` and
`requestAnimationFrame(…)` unqualified, and an unqualified call to a member of the global object passes
`undefined` as the receiver — the global environment record has no `with` base object — so a member body
reading `thisObject` has no engine to reach the runtime through and can only answer `Illegal invocation`.
Accessors are unaffected, because a bare identifier *read* goes through the global object's `[[Get]]` with the
global as receiver. So: **an operation that needs its page is a `PerRealmSlot` holding a `ClrFunction` bound to
the engine** (`WindowInstaller.Operation`), and only an operation that needs nothing — `scrollTo`, `blur`,
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

### The events bridge

**AngleSharp's event bus is neither observed nor driven by script** (design doc §5). Everything script-visible
is a Jint `Event` dispatched through the engine's tree-aware dispatcher, at the algorithm points this package
owns. `Events/` is that: the interfaces, the handler attributes, the activation behaviours, focus and the
input dispatcher. Three things are worth knowing before changing any of it.

**AngleSharp has no activation behaviour at all, so none of it can be delegated.** Measured against the pinned
1.7.2, with and without a browsing context: `IHtmlElement.DoClick()` dispatches a `click` on AngleSharp's own
bus and returns — a checkbox it clicks does not toggle, a radio group does not change, a `<summary>` does not
open its `<details>`, an `<a href>` does not navigate. `DoFocus()` never assigns `IDocument.ActiveElement`, so
that property answers `null` for the life of every document where HTML says the body element. Those two are
why `click`, `focus`, `blur`, `document.activeElement` and `document.hasFocus` are `skip`ped in the override
table and re-declared through `additions`, and they are recorded in the divergence table in
[`../AGENTS.md`](../AGENTS.md).

**Which algorithm point raises which event.** The table is the artefact — an event fired anywhere else is a
second bus:

| Point | Fires | Where |
| --- | --- | --- |
| the parse ends | `readystatechange`, `DOMContentLoaded` (document), `load` (window) | `Runtime/PageDocument.FireLifecycle` |
| a click's activation behaviour | `input` + `change` (checkbox, radio, `<option>`), `submit`, `reset`, `toggle`, a forwarded `click` | `Events/ActivationBehaviors` |
| a form submits or resets | `invalid` at each failing control, `submit` (cancelable, carrying the `submitter`), `reset` (cancelable) | `Events/FormSubmission` |
| focus moves | `blur`, `focusout`, `focus`, `focusin`, and `change` for a control the user edited | `Events/FocusController` |
| a key edits a text control | `keydown`, `keypress`, `beforeinput` (cancelable), `input`, `keyup` | `Events/InputDispatcher`, `Events/TextEditing` |

`toggle` is the one that is **queued** rather than fired in place, on the engine's own task queue, because
HTML's details notification task steps say so — a test that clicks a `<summary>` has to pump before it sees it.

**Activation without a layout is exact where it is state and a seam where it is not.** Checkedness,
`details.open` and selectedness are pure state, so they are implemented outright, legacy pre-activation
rollback included. A link to follow, a form to submit and a file chooser to open leave the DOM, so they go to
`Events/BrowserActivationHost`, whose default *records* rather than acting; the navigation layer (campaign
item R5) replaces it through `BrowserEventRealm.ActivationHost`. A colour or date picker has nothing to pick
with and is honestly nothing rather than a guessed value. Focusability is computed from the element's kind and
its `tabindex` content attribute rather than from AngleSharp's `TabIndex`, which answers 0 for every element
including a bare `<div>`. **Selector pseudo-classes are deliberately not wired to any of it**: `:focus`,
`:checked` and `:hover` in a `querySelector` go to AngleSharp's own selector engine, which knows nothing about
the focus this package tracks, so `el.matches(':focus')` is not an answer about it.

**Handler content attributes need no notification from AngleSharp, and that is a decision.** The attribute's
text *is* the state: a handler slot records which text it was last reconciled against, and any difference is
what HTML's "set the content attribute" step observes. Three points reconcile — wrapper construction (so a
markup handler is registered ahead of any listener a script can add), `DomNodeObject.GetParent`, which the
dispatcher calls exactly once per event path item and which costs one `GetAttribute`, and a read or write of
the IDL attribute. The alternatives — a document-wide `MutationObserver` (R4's lane) or AngleSharp's
`IAttributeObserver` service (a registration in the `IConfiguration` the page runtime builds, and one
AngleSharp uses internally) — would put a notification path in a file another campaign item owns to learn
something the attribute already says. The one case that needs more is `<body onload>`, because HTML redirects
it to the **window** and `load` never touches the body: `EventHandlerContentAttributes.InstallBodyHandlers`
builds that wrapper once when the parse ends.

**`isTrusted` is the line between a script and a client.** `element.click()` is untrusted — HTML's `click()`
says to fire the synthetic pointer event "with the not trusted flag set", and the activation behaviour still
runs, because trust decides what a page can *tell apart*, not whether the default action happens. Everything
`InputDispatcher` fires is trusted, because a protocol client driving a page stands in for a user.

**Form submission is split down the middle, and the middle is HTML's.** `Events/FormSubmission` is everything
up to and including "if the event was canceled, return" — the interactive validation that fires `invalid` at
each failing control and can refuse outright, then the `submit` event; `Runtime/FormSubmitter` is everything
after it — the entry list with its `formdata` event, the encoding, the request. The order is the
specification's and it is observable: validation is step 4.5 and `submit` is step 4.7, so a form whose
constraints fail never fires `submit` at all, and `form.submit()` skips both, which is the whole of what
distinguishes it from `form.requestSubmit()` and from a submit button. Constraint validation asks
`willValidate` before it asks `validity`, because that one member is what excludes a button, a disabled or
readonly control and a control inside a disabled fieldset — without it every `<button type=button>` in the
form would be examined.

### Navigation is a fetch and a new engine

`Page.NavigateAsync` runs off the page loop and commits onto it, and that split is the design:

- **The fetch is `FetchTransport`, the engine-free layer**, so the page goes on pumping timers and obeying a
  close while the response is on its way — and the engine the new document will run in does not exist yet, so
  there is none for it to be on. `Runtime/DocumentFetch` drives `SendForStreamAsync` (the redirect loop and
  the per-hop policy re-check) and reads the body itself under `BrowserOptions.MaxDocumentBytes`. It therefore
  owes the observer the final `OnResponse`/`OnCompleted`, which only `SendAsync` makes for itself — the same
  debt `XhrOperation` and `Workers/PageModuleLoader` pay for the same reason. The **first hop's `UrlFilter` is
  run by the page**, not the transport, which deliberately never runs a host filter twice per request.
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

**Everything the network touches is the context's** — `Runtime/PageNetwork`, one per `BrowserContext`: the
client, the composed `UrlFilter` (host filter and `BlockPrivateNetwork` combined once), the `CookieJar` and
the `StoragePartitionProvider`. Two pages of a site therefore share a session and two contexts are two
visitors, and one filter bounds the document, every subresource, `fetch`, `XMLHttpRequest` and a worker's
module loads alike.

**The URL is the runtime's.** `PageRuntime.DocumentUrl` is what `location`, `document.URL` and relative
resolution read, and `pushState` and a fragment navigation move it without reloading. Writing AngleSharp's
`ILocation` instead raises `Location.Changed`, answered with a fire-and-forget `IBrowsingContext.OpenAsync` on
the setter's own thread — the second thread in the DOM the divergence table warns about — so
`LocationInstaller` shadows the *whole* interface and the hazard is gone rather than dormant. One divergence
stays: `Options.WebApi.Fetch.BaseUrl` is read once per engine, so `fetch('./x')` after a `pushState` resolves
against the URL the document *loaded* from. Closing it is an engine seam, not a second URL kept here.

**The history entry is not the document.** Each loaded document gets an id and every entry carries one, so a
traversal within a cluster (`pushState` siblings, a fragment) is same-document — `popstate`, `hashchange`, no
fetch — and one across clusters is a navigation that reloads, there being no back/forward cache. The cluster
is rebound to the new id afterwards, or every step among its siblings would become a reload too. A traversal
is always queued, never inline; and navigating away from the initial `about:blank` is a **replace**, so
`history.length` counts what a browser counts.

### The parse, and the parser hop

`Runtime/PageDocument` opens the document through `IBrowsingContext.OpenAsync` on the loop thread and blocks on
it. AngleSharp's parse is an asynchronous method whose every `await` carries `ConfigureAwait(false)`, so a
genuinely asynchronous step anywhere in it would resume the parse — and the scripting hook with it — on a pool
thread while the loop sat blocked, and the engine would be entered from two threads with nothing to say so.
**Nothing in this configuration is asynchronous** (the source is in memory, no `IResourceLoader` is registered,
no `IEventLoop` is registered so `Document.QueueTaskAsync` completes inline, and the scripting hook answers
`Task.CompletedTask`), so the whole parse including script execution happens on the one thread. That is
checked, not believed: `PageScriptingService.Hopped` compares the thread it was called on against the loop's,
and a mismatch becomes a page error naming the fallback the design specifies — a fully synchronous parse on the loop
with blocking script fetches. **Anything that makes a step of the parse genuinely asynchronous — a resource
loader, an event loop service, a scripting hook that awaits — takes the fallback with it.**

What runs: a classic inline `<script>`, synchronously at its own `</script>`, in document order. What does not:
an external `src` (no network yet), a module, an import map. `SupportsType` answering `false` is what stops
AngleSharp preparing a module at all, and `PageDocument.Survey` walks the parsed document afterwards so that
every skipped script is named in `Page.UnsupportedScripts` rather than silently doing nothing.

`readystatechange`, `DOMContentLoaded` (bubbling) and `load` (at the window) are dispatched afterwards through
Jint's own dispatcher, because AngleSharp's own firing goes into its own listener lists, which hold nothing a
script registered. `document.readyState` already reads `"complete"` at all three: AngleSharp advances it during
the parse and `Document.ReadyState`'s setter is not reachable from outside its assembly.
