# CDP client handshake histogram

What does an automation client *actually* send? Not what the protocol describes, and not what a
compatibility table claims — what Puppeteer, PuppeteerSharp, Playwright, Playwright for .NET and the Chrome
DevTools frontend put on the wire when they drive a real Chrome through one ordinary scenario.

This tool answers that by recording it. It launches Chrome, puts a man-in-the-middle proxy in front of its
DevTools endpoint, points each client at the proxy, walks all of them through the *same* twenty-five step
scenario, and writes one file per client plus a cross-client matrix into
[`tools/devtools-protocol/handshakes/`](../devtools-protocol/handshakes/). Those files are the
implementation manifest for `Jint.DevTools` and `Jint.Browser`: a method that appears in the matrix is a
method some client will call, and a method that appears in the **minimum must-answer set** is one nothing
works without.

The results are checked in. Re-recording is a deliberate act — a client release changes them.

## Running it

```bash
cd tools/cdp-histogram
npm install                 # also downloads Chrome for Testing (puppeteer's postinstall)
node run-all.mjs            # records every client, then rebuilds matrix.md
```

Other entry points:

```bash
node run-all.mjs --only=playwright-node,puppeteer-node   # a subset
node run-all.mjs --skip=devtools-frontend                # everything but the best-effort capture
node run-all.mjs --matrix-only                           # rebuild matrix.md from the checked-in results
node fixture/serve.mjs                                   # just the fixture site, to look at it
```

Needs Node 20+ and, for the two .NET clients, the .NET 10 SDK; `run-all.mjs` builds those two projects
itself. **There is exactly one browser**: the Chrome for Testing that `npm install` downloaded. Playwright
connects over CDP to that same binary rather than to a browser of its own, so no `playwright install` is
needed and no client's numbers are a different Chrome's numbers.

`raw/` holds the per-run JSON-lines logs and the scenario metadata. It is gitignored; only the aggregated
results are checked in.

## The proxy

`proxy.mjs` starts an HTTP server on an ephemeral loopback port and does two things.

**It serves the discovery documents**, `/json/version`, `/json/list`, `/json` and `/json/protocol`, by
fetching Chrome's and rewriting every `ws://host:port` — and every `ws=host:port` inside a
`devtoolsFrontendUrl` — to point at itself. Without that rewrite a client that connects by URL
(`connect({ browserURL })`, `connectOverCDP(url)`) reads the endpoint out of `/json/version` and connects
straight to Chrome, and the recording is empty.

**It relays WebSockets**, accepting a connection on any path and opening a matching one to Chrome on the
same path, so both the browser endpoint (`/devtools/browser/<id>`) and any page endpoint work. Frames are
relayed byte for byte: the proxy never rewrites a frame, never answers a method itself, and never reorders
anything.

Every frame becomes one JSON line:

```json
{"seq": 41, "t": 1832, "direction": "c2b", "sessionId": "…", "id": 12,
 "method": "Runtime.callFunctionOn", "paramsKeys": ["functionDeclaration", "objectId", "arguments"],
 "paramsValues": {"awaitPromise": true, "returnByValue": false}}
```

- **Requests record parameter *keys*.** Values are recorded only where the value is itself the finding:
  the session model (`Target.setAutoAttach`'s `flatten`, `waitForDebuggerOnStart`, `autoAttach`, `filter`),
  the shape of a result (`Runtime.evaluate` / `callFunctionOn`'s `awaitPromise`, `returnByValue`,
  `generatePreview`, `userGesture`), and the names a client expects an implementation to accept
  (`Page.createIsolatedWorld`'s `worldName`, `Runtime.addBinding`'s `name`). No script source, no page
  content, no cookie values, no URLs.
- **Responses are correlated to requests by `(sessionId, id)`**, so each request row carries the
  `resultKeys` it got back, or the `{code, message}` it failed with. The pair is the key, not the id alone:
  a browser-level id and a session-level id collide otherwise.
- **A frame carried inside `Target.sendMessageToTarget` / `Target.receivedMessageFromTarget` is unwrapped
  and recorded too**, so the pre-flattened session model would show up as protocol traffic rather than as
  one opaque command. No client recorded here uses it — every one of them asks for flattened sessions —
  but a client that did would not go unrecorded.

Scenario scripts mark step boundaries with `GET /__mark?name=<step>` on the same server, which puts the
mark in the same log as the frames; slicing per step is then a matter of reading the log in order, with no
clock comparison. Each mark waits 250 ms first, so the events a step *caused* are attributed to that step
rather than to the one after it.

## The scenario

`fixture/index.html` is deliberately ordinary: a heading, three links (one `#hash`, one to a second page,
one external-looking one that is never followed), a form with a text input, a checkbox, a `<select>` and a
`<textarea>`, a button that sets `document.title` and appends `#late` after 300 ms, an interval that
mutates a `<div>`, a cookie, a `localStorage` write, a `console.log` and a `console.error`, and a
`fetch('/api.json')` on load. `page2.html` is a plain second document so that `goBack` crosses a real
navigation.

Every client walks the same twenty-five steps in the same order (`steps.mjs` is the list). A client that
cannot do a step records it as skipped rather than substituting something else — the per-step columns of
the matrix only mean something if they compare like with like.

### Which steps are not what their names suggest

This is the single most important caveat in the output, and it is what makes the matrix readable:

- **`$`, `$$`, `waitForSelector`, `type`, `select`, `content`, `title`, `evaluate` and the `localStorage`
  read are all `Runtime.callFunctionOn`.** Both client families ship a JavaScript "injected script" into an
  isolated world and call *into* it; the query, the wait loop and the text extraction happen in the page,
  not in the protocol. `DOM.*` never appears for any of them. So a `DOM` domain that only implements
  `getDocument`/`querySelector` satisfies nobody, and a `Runtime.callFunctionOn` that handles object
  handles, `returnByValue` both ways and `awaitPromise` satisfies almost everybody.
- **`DOM` appears only to turn a JavaScript handle into something clickable.** `DOM.describeNode` +
  `DOM.resolveNode` for Puppeteer; `DOM.scrollIntoViewIfNeeded` + `DOM.getContentQuads` for Playwright.
  Neither client ever sends `DOM.enable` or `DOM.getDocument`.
- **`click` and `type` are `Input.dispatchMouseEvent` / `Input.dispatchKeyEvent` at coordinates**, resolved
  by the step above. That is why the campaign's flat-box renderer has to answer a box for every element.
- **`cookies` is `Storage.getCookies` / `Storage.setCookies`**, not `Network.getCookies`, for all four
  clients.
- **`goBack` is `Page.getNavigationHistory` + `Page.navigateToHistoryEntry`.** It is also the step where a
  waiter has to be careful: Chrome restores the previous page from the back/forward cache, which fires no
  `load` event, so Playwright's default `waitUntil: 'load'` times out and the scenario asks for `'commit'`.
- **`pdf` is `Page.printToPDF` returning a *stream handle*, then `IO.read` until done and `IO.close`.**
  Both client families take the stream, not the inline base64.
- **`screenshot` is `Page.captureScreenshot` for Puppeteer, and additionally `Page.getLayoutMetrics` plus
  two evaluations for Playwright.**
- **Request interception is `Fetch`, not `Network`.** `Fetch.enable` with URL patterns, `Fetch.requestPaused`,
  `Fetch.continueRequest` — plus `Network.setCacheDisabled` on the way in.
- **`newContext` is `Target.createBrowserContext`, and every page lives in a flattened session.** No client
  recorded here ever sends `Target.attachToTarget`; all four use `Target.setAutoAttach` with
  `flatten: true` and let the browser attach for them.

## The best-effort capture

`devtools-frontend.json` is marked `bestEffort` and is **not** the canonical scenario. The Chrome DevTools
frontend is a user interface, not a client library: it has no `goto`/`click`/`evaluate` API to drive. It is
also pointed at a **Node inspector** target rather than a page, because a Node-flavoured target is what
`Jint.DevTools` will look like. What is recorded is its passive handshake plus whatever the Sources panel
asks for, obtained by loading the frontend with `&panel=sources`; no UI was driven and no breakpoint was
set, because driving DevTools' own shadow roots would be a brittle test of its markup rather than of the
protocol.

Two things make it work at all, and both are worth knowing before re-running it:

- Chrome refuses to navigate an ordinary page to `devtools://`, so the frontend is loaded from
  `chrome-devtools-frontend.appspot.com`. That host serves by devtools-frontend commit and only serves
  revisions that were rolled into a Chromium release, so the revision is read from the **DEPS file of the
  exact Chrome being driven** (`devtools_frontend_revision` at that version tag) rather than from the tip
  of the frontend repository, which the host does not serve. `CDP_FRONTEND_REVISION` or
  `CDP_FRONTEND_URL` override the lookup.
- Chrome 148 blocks a request from a public origin to a loopback one
  (`net::ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS`), and the frontend's socket to the proxy is exactly
  that request, so the throwaway browser that hosts the frontend page runs with Local Network Access
  disabled. Without it the recording is silently empty and reads as "the frontend sends nothing".

If no frontend build answers, the file says so in a `notRecorded` field instead of holding a fake
recording.

## The .NET harnesses

`dotnet/Histogram.PuppeteerSharp` and `dotnet/Histogram.Playwright` are two console projects that walk the
same scenario from .NET. They are **deliberately outside `Jint.slnx`** — they need a browser and a network,
which is not what the solution builds — and the `dotnet/` folder carries its own empty
`Directory.Build.props`/`.targets` and a `Directory.Packages.props` that turns central package management
off, so the MSBuild walk stops before it reaches Jint's own build and neither project can be broken by a
change to it. The client package version lives in one MSBuild property per project, is used by the
`PackageReference` and is stamped into the assembly, because both packages ship an assembly whose version
reads `1.0.0` and "which client version sent this" is the whole point.

Playwright for .NET needs no browser install: `Playwright.CreateAsync()` starts the Node driver bundled in
the package, and `ConnectOverCDPAsync` then attaches to the Chrome the proxy fronts. If that driver cannot
start, the harness writes the reason into its metadata and exits rather than reporting an empty client.

## Reading the results, and their shelf life

`tools/devtools-protocol/handshakes/<client>.json` holds, per client: the client and Chrome versions, the
scenario sliced into steps with per-method counts, parameter keys, result keys and errors, `allMethods` and
`allEvents` in order of first appearance, and the `sessionModel`.
`tools/devtools-protocol/handshakes/matrix.md` is generated from those files and holds the cross-client
table, the minimum must-answer set, what the element path adds on top, the commands Chrome answered with an
error, and the per-step breakdown.

- **Counts are indicative; the method sets are the answer.** A count moves with timing — how many
  `Page.lifecycleEvent`s a load produces, whether a flaky `Inspector.targetCrashed` shows up — and a diff
  in the counts alone is not a finding. A method appearing or disappearing is.
- **A client version bump changes the files**, sometimes visibly: Puppeteer's isolated world is named
  `__puppeteer_utility_world__<its own version>`, so the recorded `worldName` carries the version number.
- **Step attribution is best-effort at the edges.** The 250 ms settle before each mark catches most trailing
  events, but an event that arrives later lands in the next step. Responses do not have this problem: they
  are correlated back to the request's row by `(sessionId, id)` no matter when they arrive.
- **The proxy is not a security boundary.** It is a loopback recorder for a browser this tool launched, with
  no authentication, and it exists for the duration of one scenario.
