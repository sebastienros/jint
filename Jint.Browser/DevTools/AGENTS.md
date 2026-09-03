# Agent instructions: the page-level protocol

> **Read this when:** You are touching `Jint.Browser/DevTools/` — a page target, a page-level domain (`Page`, `DOM`,
> `Network`, `Fetch`, `Input`, `Emulation`, `Accessibility`, `Jint`), or the request log the protocol reads.
>
> This is one of the co-located instruction files indexed from the repository-root [`AGENTS.md`](../../AGENTS.md).
> Read that first, then [`Jint.DevTools/AGENTS.md`](../../Jint.DevTools/AGENTS.md) for the thread rule and the
> manifest, and [`Jint.Browser/AGENTS.md`](../AGENTS.md) for the package's principle. Nothing below is repeated in
> any of them.

### The protocol layer

`DevTools/` is what makes a page drivable by Puppeteer, Playwright and their .NET ports. The public surface
is one method — `DevToolsServerExtensions.AddBrowser(server, browser)`. Read
[`Jint.DevTools/AGENTS.md`](../../Jint.DevTools/AGENTS.md) first: the thread rule, the mailbox, the
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
  model's ([`Runtime/AGENTS.md`](../Runtime/AGENTS.md)), and a node with no box is refused in Chrome's wording
  rather than answered with zeros. `performSearch` is Chrome's three arms in Chrome's order — a selector,
  an XPath expression (the same evaluator `document.evaluate` answers from), then a text substring — and a
  query that is none of them contributes nothing rather than failing, because a search box is typed into
  one character at a time. `Input` is `dispatchMouseEvent`, `dispatchKeyEvent`, `insertText` and an
  `imeSetComposition` that is accepted and changes nothing; touch, drag and the synthesized gestures are
  honestly `-32601`, and the public `Page.ClickAsync`/`TypeAsync`/`PressAsync` reach the same dispatcher
  rather than a second one. The keyboard's own rules are
  [above](../AGENTS.md#the-keyboard-and-the-editor-under-it).
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
  [`Runtime/AGENTS.md`](../Runtime/AGENTS.md#the-request-log-is-the-protocols-seam-too) argues why moving them
  would deadlock the one fetch a page cannot pump through. The document's request carries the `loaderId` as
  its `requestId`, which is how every client tells a navigation apart.
- **What is accepted and not effective says so, in place.** `Network.setCacheDisabled` (there is no cache)
  and `Audits.enable` are answered because a refusal fails an ordinary connection. Three whole lanes are
  absent with a reason rather than pending: the `Fetch` **response stage** and with it `IO` (an observer
  cannot answer `OnResponse`, so a response-stage pause could only continue unchanged), the **WebSocket and
  EventSource** events (the engine deliberately does not observe those two handshakes), and `Network`'s
  **timing** document (no phase of a request is measured). **`Emulation` is no longer among any of it**:
  every command of that domain is either effective or an accepted no-op whose summary says what there is
  none of.
- **`Accessibility` publishes the tree `Accessibility/` computes**, in Chrome's `AXNode` shape with the `DOM`
  domain's `backendNodeId` on every node — which is what makes a node a client found by role one it can then
  measure and click. It is computed per request and never maintained, which is why `loadComplete` and
  `nodesUpdated` are not emitted: an event stream would promise that the answer is being watched.
- **`Security`, `Overlay` and `CSS` answer what a front end sends while attaching and nothing more.** `CSS`
  has the two reads AngleSharp.Css can stand behind and every editing command is `-32601`; `Overlay` would
  draw on a surface that does not exist; `Security` has no certificate decision to report, the transport
  being the host's own `HttpClient`.

`Jint.Tests.Browser/DevTools/` holds two handshake replays — every *method* four recorded clients sent, and
every parameter **shape** they sent it with, the second built out of each call's own `paramsKeys` and typed
from the vendored protocol, because the two Playwright defects C7 found were shapes (`Target.getTargetInfo`
with no parameters, a default-context page naming no context) and a name-only pin cannot see one —
`PageProtocolManifestTests` (the page half of the property `Jint.Tests.DevTools`
holds for the engine half), a suite per domain — `DomDomainTests`, `NetworkDomainTests`, `FetchDomainTests`,
`EmulationDomainTests`, `AccessibilityDomainTests`, `FrontEndDomainTests` — and the two client suites, the
only tests here that can claim client compatibility, because they are the only ones that satisfy a library
nobody here wrote.

**Two clients rather than one, and the second is not a duplicate.** `PuppeteerSharpPageTests` and
`PuppeteerSharpCourseTests` say that `$`, `$$`, `click`, `waitForSelector`, a bounding box,
`emulateMediaFeatures`, `emulateTimezone`, `setJavaScriptEnabled` and `accessibility.snapshot()` work;
`PlaywrightCourseTests` drives the same pages over `connectOverCDP` and asks for different things at every
step — an HTTP discovery document rather than a socket address, a `browserContextId` on *every* page target,
`scrollIntoViewIfNeeded` + `getContentQuads` rather than `describeNode`, `Storage.getCookies` on the browser
session rather than the page's. Four defects came out of that difference alone (`Target.getTargetInfo` with
no identifier, the default context naming nothing, `Node.getRootNode`, browser-session `Storage`), and none
of them was reachable from the recorded handshake, which pins what a client *sends* rather than what it is
told. Playwright's suite is gated on `JINT_BROWSER_CLIENTS` because its driver is a Node process; the
`browser-clients` CI leg sets it, and also proves the gate switches.

The pages both suites drive are the obstacle course, `Jint.Tests.Browser/Fixtures/`: real vendored libraries
on a real origin, so what a client is driving is a page rather than a document written to be driven.

### The request log is the protocol's seam too

`Runtime/PageNetworkRecorder` is the page's `FetchObserver`, and it is now two things rather than one: the
log behind `Page.Requests`, and the seam the `Network` and `Fetch` domains read through
(`Runtime/IPageNetworkListener`). One observer, two consumers — a second observer on the transport would be
a second truth about the same request.

- **Every listener call runs on the thread the observation arrived on, which is a transport thread.**
  Delivering them through the page loop was considered and is wrong: `RequestWillBeSentAsync` is where a
  client's `Fetch` pause blocks, and the one fetch a page cannot pump through is a `<script src>` a running
  script inserted — `Parsing/AGENTS.md` says why it blocks the loop rather than pumping — so a pause
  delivered on the loop would deadlock exactly there. Nothing in the listener touches an engine or a node,
  which is what makes that safe: the frame identifier is a string, and the loader identifier and document
  URL are read off `Page`'s own volatile fields as the request goes out.
- **The listener may answer, so the recorder intercepts as well as watches.** The extra headers a client
  set, its user-agent override, its blocked URLs, its offline switch and a `Fetch` pause all come back as a
  `PageNetworkDecision`, which the recorder turns into the engine's own `FetchInterception`. With no
  listener registered it answers `null` to every hop and copies nothing, which is what a page with no client
  attached costs.
- **A navigation's request is addressed by its `loaderId`**, passed into `DocumentFetch.LoadAsync` rather
  than read from the page: the field still holds the document that is showing while the fetch is in flight,
  and a client tells the navigation apart from every other request by exactly that string.
- **`DocumentFetch` and `SubresourceFetch` hand their bytes to the observer.** Both read their own body, so
  neither raises `OnData` for free; without the call the log would have a response with no body and
  `Network.getResponseBody` would answer nothing for the document a client just navigated to. It is the same
  debt `FetchObservation.FinalResponse` names, and the body half of it.
- **The capture is bounded and off by default.** `BrowserOptions.MaxCapturedResponseBytes` bounds the total
  a page holds, the oldest capture is dropped to stay under it, and the copying is armed only while a client
  has the `Network` domain enabled.

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
