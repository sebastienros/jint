# Agent instructions: the browser package

> **Read this when:** You are touching anything under `Jint.Browser/`, the binding generator under
> `tools/dom-bindings/`, or its override table.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is repeated there.
> The design this implements is [`docs/design/headless-browser.md`](../docs/design/headless-browser.md):
> §4 for the bindings, §3 for the page runtime, §5 for events, §6 for the parse.

### The principle this package is checked against

> Jint should add value to AngleSharp without competing too much.

That is the project founder's guidance for the whole headless-browser campaign, and it decides arguments here
rather than merely decorating them. **AngleSharp is the parser, the DOM and the CSSOM; nothing in this package
re-implements any of them.** What Jint owns is the binding layer — a projection built on the engine's own
shape and layout machinery instead of a reflection trampoline — and the output is deliberately shaped so that
[AngleSharp.Js](https://github.com/AngleSharp/AngleSharp.Js) could adopt it without adopting anything else
here. Three consequences bind every change:

- **Every AngleSharp behaviour that disagrees with the DOM standard is reported upstream and recorded below,
  never worked around silently.** A workaround in the binding hides a defect from the project that can fix it,
  and makes the next reader believe the standard says what AngleSharp does. The one thing a wrapper may do is
  keep *its own* contracts coherent — the `dataset` name filter below is the worked example, and it says so.
- **No document or README sentence positions this as a rival DOM stack.** It is "AngleSharp + Jint".
- **A seam that proves useful is offered, not hoarded.** The tree-aware event dispatcher the engine grew for
  this package (`Jint/WebApi/Events/EventDispatch.cs`) knows nothing about a node; it asks the target. The
  same is true of the generator: it reads AngleSharp's attributes and emits against Jint's public shape API.

### What is generated and what is hand-written

`tools/dom-bindings/Jint.Browser.BindingGenerator` loads the two pinned AngleSharp assemblies through
`System.Reflection.MetadataLoadContext` and emits `Jint.Browser/Dom/Generated/*.g.cs`. The output is
**checked in** for the reasons `tools/devtools-protocol` gives: reviewable diffs, an analyzer-free build, and
a swap to a source generator later is mechanical.

| Generated | Hand-written |
| --- | --- |
| One `JsObjectShape` per interface — operations, attributes, constants, the `constructor` slot, `@@toStringTag` | The wrapper classes (`DomObject`, `DomNodeObject`, `Collections/`) |
| The registry (`DomInterfaces`): every interface, its parent, whether it roots at `EventTarget`, its wrapper kind | `DomRealm` (per-engine prototypes, interface objects, the wrapper cache) |
| A `DomCollectionAccessor` per collection interface, from `[DomAccessor]` | `DomInterfaceObject`, `DomBindings`, `DomConvert`, `DomHostHooks` |
| `DomTypeMap`'s candidate list, most derived first | `DomManualShapes` — the shapes the generator cannot express |
| `DomEnums`, both directions, for the WebIDL string enumerations | `DomTypeMap.For` and its per-`Type` cache |
| — | `DomManualInterfaces` and `DomConstructors` — the interface AngleSharp has no `[DomName]` for (`HTMLFrameSetElement`) and the one WebIDL really does give a constructor (`Document`) |

**Never hand-edit a `.g.cs`.** `DomBindingsStalenessTests` runs the same emitter in memory and fails on any
difference; `JINT_DOM_BINDINGS=update` writes the difference back, which is also the shortest regeneration
path after an `overrides.json` edit. `tools/dom-bindings/README.md` has the command-line one and the rule that
**a pin bump is a code change**, not a configuration edit.

### The bindings have a file of their own

How AngleSharp's attributes are read as WebIDL, the override table, the conversion table and its divergences in
both directions, wrapper identity and the shape discipline are [`Dom/AGENTS.md`](Dom/AGENTS.md). The one rule to
carry across without opening it: **never hand-edit a file under `Dom/Generated/`**; regenerate with
`JINT_DOM_BINDINGS=update`, and report an AngleSharp divergence upstream rather than working around it.

### The events bridge lives with the runtime

Every script-visible event is a Jint `Event` dispatched through the engine's tree-aware dispatcher, at the
algorithm points the package owns (design doc §5) — never AngleSharp's own bus, which holds nothing a script
registered. What that costs the binding is the `skip` and `additions` rows above: `click`, `focus`, `blur`,
`form.reset`, `document.activeElement` and `document.hasFocus` are all AngleSharp members that do nothing
useful, so they are skipped and re-declared — and `document.createEvent`, whose AngleSharp `Event` must never
reach script, is re-declared against Jint's in `Events/LegacyEventCreation`. The behaviour behind them — which algorithm point raises which
event, what activation means with no layout, and why the handler content attributes need no notification from
AngleSharp — is [`Runtime/AGENTS.md`](Runtime/AGENTS.md#the-events-bridge).

### The page runtime is a file of its own

`Page`, the loop that owns its engine, the `Window` installer, navigation, forms, history, cookies, storage
and workers are [`Runtime/AGENTS.md`](Runtime/AGENTS.md). The one rule to carry across the boundary without
opening it: **one thread owns a page's engine and its DOM**, every public `Page` member is a mailbox request,
and nothing belonging to an engine — a `JsValue`, an AngleSharp node — may be in the task it answers.

### The observers, and when each of them delivers

`Observers/` holds three. **Each delivers on a different lane, and the lane is the design.**

- **`MutationObserver` delivers on the engine's job queue — the microtask checkpoint.** The *records* are
  AngleSharp's: `DocumentExtensions.QueueMutation` already walks a mutated node's inclusive ancestors, matches
  each against the registered observer list, honours `subtree` and `attributeFilter`, and clears `oldValue`
  for an observer that did not ask for it. What it has no answer for is *when*, and the reason is the
  [parser driver](Runtime/Parsing/AGENTS.md#the-parser-driver-and-the-baton): its `MutationHost` schedules through an
  `IEventLoop` service, **nothing in AngleSharp implements one**, and `EventLoopExtensions.Enqueue` on a null
  loop runs the action *inline* — so out of the box the callback fires synchronously inside `appendChild`.
  Registering an event loop to fix that would put a second scheduler under a parse whose hand-offs the baton
  already owns, and every one of its turns would land on whichever thread AngleSharp resumed on. So the
  inline call is used as the
  **arrival** of a record and nothing else: `JsMutationObserver` parks it and `MutationObserverLane` puts one
  job on the engine's queue per batch. Ordering then falls out of a plain enqueue, and
  `Observers/MutationObserverTests` pins it. Lifetime falls out too, and it is DOM's own rule: a *connected*
  observer is held by the document (AngleSharp's `MutationHost` holds the callback, which holds the wrapper),
  and a disconnected one is held only by the notify set until its records are taken — so an observer a page
  dropped with nothing queued collects together with its callback.
- **`IntersectionObserver` and `ResizeObserver` deliver as a *task*** — a zero-delay timer entry
  (`ObserverTask`) — because both belong to update-the-rendering and a microtask would run before the promises
  of the same turn. It also makes the delivery visible to `Page.WaitForIdleAsync`.
- **Both are stubs, and the shape of the lie is the point.** Each observed target is reported exactly once,
  fully intersecting or at its own size, and never again, because nothing here can change what a box is.
  "Never intersecting" would stop every lazy list and reveal-on-scroll animation dead, and the initial resize
  notification is the one a component uses to measure itself when it mounts. `root`, `rootMargin` and
  `thresholds` are parsed, validated and reflected exactly as the specification says and change nothing.
  **The rectangles are real numbers now** — the flat box model gives every element a row, and an entry
  reports the target's own box through the same `Layout/DomRects` factory `getBoundingClientRect` answers
  from, so the two agree. They are still **plain objects, not `DOMRectReadOnly` instances**: the eight
  members are there and the interface object is not, and `Layout/DomRects` says what adding one would cost.

None of the five interface objects is generated, so they are hand-written `JsObjectShape`s behind
`HostInterfaceObject`, and `Views/HostInterfaceDisciplineTests` holds them to the same two rules
`DomPrototypeTests` and `WebIdlPropertyAttributeTests` hold the generated ones to.

`Dom/Views/` is the same story for the interfaces a *browser* supplies rather than the DOM — `DOMParser`,
`XMLSerializer`, `Selection`, `MediaQueryListEvent` — plus the members that make the generated `Range`,
`TreeWalker` and `NodeIterator` usable. Three things there are worth knowing:

- **`DOMParser`'s XML half is `AngleSharp.Xml`**, referenced for that and nothing else; writing an XML parser
  here instead is the one thing this package is not for. It is deliberately **not** in `pin.json` — the
  generator reads two assemblies and projects no interface from this one. A failed parse answers the
  `parsererror` document the standard prescribes, which is what a page tests for.
- **A parsed document cannot run anything**: its parser gets a browsing context of its own with no scripting
  service and `IsScripting` false, so a `<script>` in the input is an element with text and nothing more.
- **`Selection` has no direction**, because direction comes from which end a user dragged from: the anchor is
  always the range's start.

`matchMedia` gained its other half. `PageRuntime.SetViewport` is the seam device emulation (campaign item C5)
drives, and every `MediaQueryList` the page holds recomputes and fires `change` — a real
`MediaQueryListEvent`, because `e => e.matches` is how the listener is written — only if its own answer moved.
No `resize` event fires at the window: HTML fires that from update-the-rendering, and there is none.

### The protocol layer

`DevTools/` is what makes a page drivable by Puppeteer, Playwright and their .NET ports. The public surface
is one method — `DevToolsServerExtensions.AddBrowser(server, browser)`. Read
[`Jint.DevTools/AGENTS.md`](../Jint.DevTools/AGENTS.md) first: the thread rule, the mailbox, the
target/runtime split and the manifest are there and none of it is repeated here.

- **A target is a page and a runtime is a document.** `PageTarget : DevToolsTarget` holds what a client keeps
  addressing — the identifier, the frame it names, the bindings, the new-document scripts, the emulation —
  and the engine under it is replaced on every navigation. The frame identifier **is** the target
  identifier: there is one scripted frame per page, and a client matching the frame it navigated against the
  context it evaluates in gets one string from both.
- **`IPageObserver` is the only seam, and a page has exactly one observer.** Every event a client hears is
  one of its calls turned into a protocol event. `DocumentCreated` runs after the window installer and
  before the parse, on the loop, which is what makes it the place to replace the engine, re-install the
  bindings and run `addScriptToEvaluateOnNewDocument` — all of which have to be in place before the
  document's first inline script.
- **Every command runs on the page loop**, so it may touch the DOM directly — and one that waits
  (`Page.navigate`) waits by `await`ing, never by blocking: the loop it is on runs the commit it waits for.
- **`DOM` and `Input` are where a client stops evaluating and starts driving.** A node reaches a client as a
  `RemoteObject` the `DomRemoteObjectDescriber` named — `subtype: "node"`, the interface, `div#id.cls` — and
  that subtype is what makes a client library build an *element* handle out of it. `DomNodeTracker` holds the
  two identifiers: a `nodeId` per document, thrown away and announced with `documentUpdated` on every commit,
  and a `backendNodeId` per node for the page's life, keyed in a `ConditionalWeakTable`. Both are shared by
  every attachment, the way the remote-object table is; what is **not** shared is which nodes an attachment
  has been *sent*, and that is what decides which mutation events reach it — Chrome's own rule, and the
  reason a client that never called `getDocument` hears nothing. The records are AngleSharp's, delivered on
  the engine's queue at the same checkpoint a page's own `MutationObserver` fires. Every box is the flat
  model's ([`Runtime/AGENTS.md`](Runtime/AGENTS.md)), and a node with no box is refused in Chrome's wording
  rather than answered with zeros. `Input` is `dispatchMouseEvent` and nothing else: the keyboard half is a
  later item, and every other command of the domain is honestly `-32601`.
- **A named isolated world is made again over every document.** Chrome does that, and Puppeteer and
  Playwright each create one utility world when they attach and then use it for the life of the page — so a
  world that ended with the first document leaves `$`, `$$` and `waitForSelector` waiting for a context that
  never arrives. `DevToolsTarget.Replace` re-mints it under the same name with a fresh identifier, after the
  default context is announced.
- **Tab targets exist because Puppeteer requires them.** Its browser-level `setAutoAttach` filter excludes
  `page`; it reaches a page by sending `setAutoAttach` again on the tab's session. `TabTarget` answers about
  the page's engine rather than one of its own. Found by driving the client, not by reading the protocol.
- **The `Jint` domain is ours, and it is described rather than invented.**
  `tools/devtools-protocol/jint_protocol.json` sits beside the vendored Chrome files in the same format, so
  `Jint.getMarkdown`, `Jint.getText` and `Jint.getAccessibilitySnapshot` are generated like any other
  command. They are what the screenshot refusal names, and `JintDomainTests` closes that loop by following
  the refusal to a command that answers.
- **Three divergences, each stated where it is made.** An isolated world is an alias for the document's own
  realm; a dialog does not block the page, so `handleJavaScriptDialog` sets the standing decision the next
  one reads; and `captureScreenshot` / `printToPDF` answer `-32000` with a sentence naming the text and
  markdown alternatives, because this browser renders no pixels.
- **`Network` and `Fetch` read the page's own request log; they are never a second observer.**
  `Runtime/PageNetworkRecorder` is the engine's `FetchObserver` and already sees the document, every
  subresource, every `fetch` and `XMLHttpRequest` and a worker's module loads, so `Page.Requests` and the
  protocol say the same thing about the same request and the two domains share identifiers. **The
  notifications and the interception run on the transport thread, not the page loop**, and
  [`Runtime/AGENTS.md`](Runtime/AGENTS.md#the-request-log-is-the-protocols-seam-too) argues why moving them
  would deadlock the one fetch a page cannot pump through. The document's request carries the `loaderId` as
  its `requestId`, which is how every client tells a navigation apart.
- **What is accepted and not yet effective says so, in place.** `Emulation`'s touch, focus and media
  overrides, `Network.setCacheDisabled` (there is no cache), `Performance.enable` and `Audits.enable` are
  answered because a refusal fails an ordinary connection, and each names the campaign item that makes it
  real. Three whole lanes are absent with a reason rather than pending: the `Fetch` **response stage** and
  with it `IO` (an observer cannot answer `OnResponse`, so a response-stage pause could only continue
  unchanged), the **WebSocket and EventSource** events (the engine deliberately does not observe those two
  handshakes), and `Network`'s **timing** document (no phase of a request is measured).

`Jint.Tests.Browser/DevTools/` holds four checks: the handshake replay of what four recorded clients send up
to and including their first click, `PageProtocolManifestTests` (the page half of the property
`Jint.Tests.DevTools` holds for the engine half), `DomDomainTests`, and the PuppeteerSharp suite, the only
test here that can claim client compatibility — it is what says `$`, `$$`, `click`, `waitForSelector`,
`hover` and a bounding box work through a library nobody here wrote.

### The seams promoted later

The package publishes the host API — `Browser`, `BrowserContext`, `BrowserOptions`,
`BrowserContextOptions`, `Page`, `Frame`, `Viewport`, `PageError`, `DialogEventArgs` and what a navigation
takes and answers — plus `DevToolsServerExtensions.AddBrowser`, and
`Jint.Tests.Browser/Verify/PublicApiTest.verified.txt` is the baseline that makes a change to it a reviewable
diff. **Nothing public takes or answers an AngleSharp node**, which is why `Page.SubmitFormAsync` takes a
selector; R2 reaches the same algorithm through the internal `FormSubmitter.Submit` from inside the loop.
Everything else is internal, and that is a decision with a date on it. `DomBindings`, `DomRealm`,
`DomInterfaceDefinition` and `DomHostHooks` are the four most likely to be promoted next, each with XML docs
and a `docs/v5-migration.md` row. Until then `Jint.Tests.Browser` is the only consumer, which is why it is
named in `InternalsVisibleTo` and why every test of the binding is written against the internal surface
rather than around it.

### Accessibility and extraction have no layout

`Accessibility/` computes an accessibility tree over AngleSharp's DOM and `Extraction/` renders the same
document as text or CommonMark. Both are pure C# over `IDocument`/`IElement`; neither touches an engine, and
that is why they were built before the page runtime existed. The consumers are the CDP `Accessibility` domain,
the custom `Jint.getMarkdown`/`getText`/`getAccessibilitySnapshot` domain, and the MCP server's `snapshot`.

**Three things a browser answers from its layout tree are answered from somewhere else, and every one is a
place where this can be wrong.**

- **Hidden** is `ElementVisibility`: the `hidden` content attribute, `aria-hidden="true"`, and `display:none`
  / `visibility:hidden|collapse` from the cascade — `IElement.ComputeCurrentStyle()`, which resolves author
  sheets and the UA sheet — falling back to the `style` content attribute alone when `AngleSharp.Css` is not
  registered. It cannot know that an element is off screen, clipped, covered or zero-sized. Two asymmetries
  are deliberate: `display:none` takes its subtree with it while `visibility:hidden` does not (CSS inherits
  `visibility`, so a `visibility:visible` descendant comes back), and `aria-hidden` removes a node from the
  accessibility tree while changing nothing about the rendering — so the extractors ask
  `RenderingReasonFor`, which ignores it, and only the tree asks `ReasonFor`, which does not.
- **Block-level** is `HtmlDisplay`, HTML's suggested rendering rather than a used display, and it is the
  table that decides — not the cascade. The cascade only wins where it *differs* from the table, which is
  what makes `<span style="display:block">` a block and stops AngleSharp's incomplete default sheet from
  calling every `<section>` inline.
- **`innerText`** is therefore the text of the document, not the text of a rendering of it: the required
  line breaks, the `<br>`s, the cell tabs and the white-space processing are all there, but nothing wraps,
  so a paragraph is one line however wide it would have been.

Three simplifications in the name computation are worth knowing before reading a wrong name as a bug: CSS
generated content (`::before`, `::after`, `::marker`) contributes nothing, `text-transform` is not applied,
and SVG `<title>`/`<desc>` children are not read. Everything else of accname 1.2 — 2A through 2I, the
recursion, the visited guard, the flattening — is the algorithm as written. HTML-AAM's mapping table is
implemented in full with one blanket simplification: where it names a computed role that is not a WAI-ARIA
role (`html-abbr`, `html-audio`, `keyboard`, `variable` and their kind) the element maps to `generic`.

`AccessibilityOptions` has three presets and they are not interchangeable: `Default` is the pruned tree,
`Snapshot` adds the text between the nodes (which is what `AccessibilitySnapshot.Render` needs to say
anything at all), and `Full` is what `Accessibility.getFullAXTree` answers with. A snapshot states each
string once — text that is already a node's accessible name is not published again as a text node.

The four fixture pages under `Jint.Tests.Browser/Accessibility/Golden/` are rendered three ways each and the
output is checked in. **`JINT_BROWSER_GOLDEN=update` rewrites them**, the same discipline `JINT_SPEC_ANCHORS`
and `JINT_DOM_BINDINGS` use: the diff is the artefact, so a change to what an agent reads has to be looked at.

Divergences that are **AngleSharp's**, found by this work, to be reported upstream rather than patched here:

| What | The standard | AngleSharp.Css 1.0.2 |
| --- | --- | --- |
| `el.ComputeCurrentStyle()` without the CSS services | an empty declaration, or a documented failure | throws `InvalidOperationException("Sequence contains no elements")`, which is why every call here is guarded and the guard latches |
| the default style sheet's `display` rules | HTML's rendering section gives `display: block` to `section`, `article`, `nav`, `aside`, `header`, `footer`, `main`, `figure`, `figcaption`, `details`, `summary`, `dialog`, `hgroup` | no rule at all, so every one of them computes to nothing and reads as inline |
| `[hidden] { display: none }` | in HTML's rendering section | absent, so `<div hidden>` computes `display: block` |
| `textarea { white-space: pre-wrap }` | in HTML's rendering section | absent, though `pre { white-space: pre }` is there |
