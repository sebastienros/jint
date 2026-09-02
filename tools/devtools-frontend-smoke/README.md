# DevTools front-end smoke test

Everything else in this repository that claims Chrome DevTools works against Jint claims it from the
protocol side: a test sends a command and asserts the answer. That proves the server answers. It does not
prove that a person who opens DevTools sees anything.

This drives the other half. It launches `Jint.Repl --inspect`, loads the **real Chrome DevTools front
end** — unmodified, at the revision that shipped in the Chrome being driven — points it at that Jint
process, and then reads the panels: the Sources navigator, the editor, the Console, the Scope pane, the
call stack. Every assertion is made against what the front end rendered, through its own shadow roots.

It is [`Jint.DevTools/docs/manual-checklist.md`](../../Jint.DevTools/docs/manual-checklist.md) minus the
manual part. Run this when the protocol surface changes; walk the checklist by hand for the steps the table
below says are not driven.

## Running it

```bash
cd tools/devtools-frontend-smoke
npm install                 # also downloads Chrome for Testing (puppeteer's postinstall)
node smoke.mjs
```

Puppeteer is pinned to the same version [`tools/cdp-histogram`](../cdp-histogram/) pins, so both tools use
one Chrome for Testing download and neither one's evidence is a different browser's.

The Jint side has to be built first — this tool never builds anything, because a second MSBuild in a working
tree somebody else is building is its own kind of flake:

```bash
dotnet build -c Release Jint.Repl/Jint.Repl.csproj
```

It looks for `artifacts/bin/Jint.Repl/release/Jint.Repl.exe`, then the extensionless apphost, then
`Jint.Repl.dll` (run through `dotnet`), and says all three plus the build command when it finds none.

| Environment variable | What it does |
| --- | --- |
| `JINT_REPL` | Use this binary instead of the built one. A `.dll` is run through `dotnet`. |
| `CDP_FRONTEND_REVISION` | Load this devtools-frontend commit instead of the one Chromium's DEPS names. |
| `CDP_FRONTEND_URL` | Load this `js_app.html` instead, skipping the lookup entirely. |
| `SMOKE_HEADED=1` | Show the browser, which is how you watch it drive. |

The recording proxy is `tools/cdp-histogram/proxy.mjs`, **imported rather than copied**. Node resolves that
file's `import 'ws'` from *its* directory, not from this one, so it needs a copy of `ws` next door:

```bash
node -e "await (await import('node:fs/promises')).cp('node_modules/ws', '../cdp-histogram/node_modules/ws', { recursive: true })"
```

Both paths are gitignored, so this changes nothing that is checked in, and `smoke.mjs` prints exactly that
command if the import fails. A full `npm install` in `tools/cdp-histogram` works too and does more.

One thing is **not** imported: the devtools-frontend revision lookup. It lives in
`tools/cdp-histogram/scenarios/devtools-frontend.mjs`, which is a script that runs a whole capture on import
and exports nothing, so `frontend.mjs` re-derives it — same DEPS file, same rule, same two overrides.

## What it drives

`fixture/app.js` is what the engine runs, and it is short on purpose. It logs a string and an object, so the
Console has an object preview to render; it declares named functions, so the editor and the gutter have
something worth looking at; a `setTimeout` inside a one-second `setInterval` throws, so an uncaught exception
is reachable; and the function the breakpoint lands in holds a function-valued local, so the Scope pane has a
*script* function of its own to render rather than only the native ones on `Object.prototype`.

The heartbeat re-arms rather than firing once, and that is deliberate. The script itself finishes in
milliseconds while the front end needs seconds to download and connect, so a single `setTimeout` would have
thrown before anybody was listening. The same heartbeat is what makes a breakpoint hittable at all:
`computeTotal` is called once a second forever, so the front end can set a breakpoint whenever it likes and
be paused within a second of doing it.

## The steps, and what each one proves

Each step gets a 60-second budget, a screenshot, a DOM dump, and a row in `out/summary.json` carrying what it
observed. A step that could not be driven is recorded as `not-driven` with what was seen instead — never as a
pass, and never as a weaker assertion wearing the strong one's name.

| # | Step | What a pass means |
| --- | --- | --- |
| 1 | `frontend-connected` | The hosted front end loaded, opened a socket to Jint, and the handshake completed: `Runtime.enable` was answered and `Debugger.scriptParsed` arrived. |
| 2 | `sources-lists-script` | The Sources navigator has a **visible** entry named `app.js` under a **`file://` domain** — every folder down to it is expanded first, because a collapsed tree keeps its children in the document and asserting on one of those would be reporting the DOM rather than the panel. The domain is what says Jint published a `file://` URL rather than a bare path. |
| 3 | `sources-shows-source-text` | Clicking that entry opens the editor and the retained source is in it: `lineTotal`, `computeTotal`, `describeOrders`, `heartbeat` and `reconcile` are all read back out of the CodeMirror content. That is `Debugger.getScriptSource` end to end. |
| 4 | `console-shows-log-with-object-preview` | The Console shows `console.log('order book ready', …)` **with its object preview expanded into properties**, not as a bare `Object`. |
| 5 | `console-message-has-a-source-anchor` | That message carries an `app.js:NN` link on the right, which the front end draws from `Runtime.consoleAPICalled`'s `stackTrace`. A message with no anchor links to nothing. |
| 6 | `uncaught-exception-shows-its-stack` | The throw from inside the `setTimeout` callback is rendered with **its stack expanded, naming `reconcile`** — the callback itself, which is a frame only because a timer callback owns one. |
| 7 | `console-evaluates-expression` | `1+1` typed at the Console prompt and submitted comes back as `2` in a result row. |
| 8 | `breakpoint-set-in-the-gutter-is-hit` | A breakpoint set by clicking the editor gutter — the front end's own `Debugger.setBreakpointByUrl` — stops the engine, the front end says "Paused on breakpoint", and the Call Stack reads `computeTotal` / **`heartbeat`** / `(anonymous)` with an `app.js:NN` link on each row. `heartbeat` is the frame the engine was entered at. |
| 9 | `scope-pane-names-its-functions` | The Scope pane lists the frame's scopes and bindings, **no function renders as `ƒ undefined()`**, and the frame's own `describe` reads `ƒ describeLine(order)` — a name and a parameter that can only have come from the declaration Jint retained. |
| 10 | `resume-continues-execution` | The breakpoint is cleared from the gutter, Resume is clicked, and **the heartbeat in the Console advances past the tick it was paused on**. Execution continuing is what proves a resume; the paused indicator going away only proves the indicator went away. |

## How it reads a user interface

DevTools is built out of custom elements, and nothing interesting is reachable from
`document.querySelector`. `frontend.mjs` installs one helper, `window.__smoke`, before the page loads; it
walks every open shadow root and answers four questions — which elements match a selector, what they say,
their whole text, and where one is on screen. Everything else goes through it, and clicks are real mouse
events at the coordinates it returns rather than synthetic `el.click()` calls, because the gutter and the
tree both listen for the real thing.

The `UI` table at the top of `smoke.mjs` is every selector the tool bets on. When DevTools' markup moves,
that table is the list to read.

Three things make loading the front end possible at all, and all three are inherited from
[`tools/cdp-histogram`](../cdp-histogram/README.md#the-best-effort-capture):

- **Chrome refuses to navigate an ordinary page to `devtools://`**, so the front end comes from
  `chrome-devtools-frontend.appspot.com`.
- **That host serves by devtools-frontend commit and only serves commits that were rolled into a Chromium
  release**, so the revision is read from the DEPS file of the exact Chrome being driven rather than from the
  tip of the front-end repository.
- **Chrome blocks a request from a public origin to a loopback one**, and the front end's socket to Jint is
  exactly that request, so the browser runs with Local Network Access checks disabled. Without it the page
  loads, connects to nothing, and every assertion fails for a reason that has nothing to do with Jint.

## What a run leaves behind

`out/` is gitignored; a run is evidence for a human, not an artefact to check in.

- `out/NN-<step>.png` — a screenshot after every step, pass or fail.
- `out/dom/NN-<step>.txt` — a compact outline of the whole rendered tree, shadow roots included. This is what
  to read when a selector stops matching.
- `out/summary.json` — the environment (Chrome version, front-end revision, the front end's own console
  errors, Jint's stdout), every step's verdict and what it observed, and the protocol traffic sliced per step.
- `out/protocol.jsonl` — every frame between the front end and Jint, one JSON line each, written by
  `tools/cdp-histogram/proxy.mjs`. Step boundaries are in the same log, because each step marks itself on the
  proxy's `/__mark` endpoint before it runs.

## Honesty: what these passes are not

Every step in the checked-in run drove the real user interface and none was faked. What follows is the list
of places where a pass means something narrower than its name, and the things that were not attempted.

- **The navigator's tree is walked by expanding whatever folder it currently shows**, one round at a time,
  rather than by knowing the shape in advance: a `file://` domain has the folders of the path under it, and
  how many there are depends on where the checkout is.
- **The Console's message list is virtualized.** Only the visible tail is in the DOM, and the heartbeat
  pushes everything else off it, so the message logged *before* the front end connected cannot be read
  directly. Step 4 types it into the Console's Filter box first. That is still driving the UI, and it has a
  bonus: a filtered-in message that arrived before the socket existed is proof that the console journal Jint
  replays on `Runtime.enable` reached the front end.
- **The editor is virtualized too**, though not at this file's size. `deepTexts` truncates each entry at 400
  characters for readability; the editor is read with `deepFullText`, which does not. A step that asserts on
  a long text and uses the wrong one silently reads a prefix — that is how the first run of step 3 failed.
- **Step 9 asserts that no function renders as `ƒ undefined()` and that one script function renders with its
  own name and parameter; it does not assert that everything else in the pane is right.** What the pane was
  showing at the moment of the assertion is recorded in `summary.json` under `functionValuedBindings` either
  way. The fixture's `describe` local exists for this step: everything else visible at that pause is native,
  and a native function would only prove the `[native code]` half.
- **`--inspect-brk`, stepping (over / into / out), watch expressions, conditional breakpoints, `Debugger.pause`,
  the Profiler and coverage, and the Memory and Performance panels are not driven at all.** Nothing here says
  anything about them.
- **The screenshots are of a headless Chrome.** `SMOKE_HEADED=1` renders it in a window instead.
- **The protocol recording names methods, result keys and errors — not argument values.** That is
  `proxy.mjs`'s deliberate design, and it is enough to show that a command was sent, answered or refused. It
  is not enough to see *what* a command carried, so the defect notes below that turn on a value were
  confirmed with a direct protocol client rather than by this tool.
- **Reading a user interface by class name is a bet on somebody else's markup.** This is why the tool is not
  in CI: a DevTools release can break it without Jint changing, and a failure here is a prompt to look, not a
  regression by itself.

## What this run found in Jint's DevTools server, and what became of it

The first run of this tool found four defects that no protocol test had caught, plus one inconsistency that
needed a decision. All five are fixed, and each is now asserted by the step named beside it rather than only
described here.

- **A frame the engine was entered at had no name and no `functionLocation`** (step 8). The Call Stack pane
  showed `computeTotal` over an unnamed row where V8 shows the timer callback's own name. Two defects wore
  that one symptom: a host `Invoke` of an anonymous function produced a frame named with the empty string,
  and a `setTimeout` / `setInterval` callback had no frame *at all*, because the timer queue reached its
  callback through `ICallable.Call`, which pushes nothing onto the engine's call stack. The pane now reads
  `computeTotal` / `heartbeat` / `(anonymous)`, each row linking to its own line.
- **`RemoteObject.description` for a function was a rendered label where the front end expects source text**
  (step 9). Jint sent `ƒ computeTotal()`; the front end treats that field as `Function.prototype.toString`
  output and parses the name back out of it, so every function in the Scope pane and in
  `Runtime.getProperties` rendered as **`ƒ undefined()`** — eleven of them in the first run's screenshot. It
  now sends the declaration, and a `PropertyPreview` of a function carries `value: ""`, which is what a real
  Chrome sends and was recorded rather than assumed.
- **`Runtime.consoleAPICalled` carried no `stackTrace`** (step 5), so no console message had a source anchor
  where V8 puts the file and line the call came from. Every message now carries its call site, resolved back
  to a script identifier so the anchor is clickable.
- **The front end called `Runtime.getExceptionDetails` once per uncaught exception and was refused every
  time** (`-32601`, step 6). The command was listed as deliberately absent on the grounds that nothing
  retains an exception's details past the report that carried them — which turned out to be answering the
  wrong question: the front end asks about an error *object* it is holding, and the details are
  reconstructed from that.
- **`/json/list` reported the target's `url` as `file:///D:/…/app.js` while `Debugger.scriptParsed` reported
  the same file as `D:/…/app.js`** (step 2), which is why the navigator filed the script under "(no domain)"
  with its whole path for a name. The decision taken: the engine's source names stay exactly what the host
  passed — a stack trace prints them, and they are the host's — and the protocol maps a name that is an
  absolute filesystem path to a `file://` URL on the way out. Both documents now name one location.

Two refusals still show up in the recording and are working as designed, listed so a reader does not file
them twice: `Network.*` is `-32601` because an engine target has no Network domain, and `Runtime.evaluate`
with `throwOnSideEffect` is `-32000 "Side-effect free evaluation is not supported"`, which costs the Console
prompt its eager-evaluation preview and nothing else.

One thing a reader of a screenshot should not file either: the *text* of a rendered error in the Console
still carries the host's own source names — `at reconcile (D:/…/app.js:55:15)` — because that text is
`Error.prototype.stack`, which is the engine's and the host's. The clickable anchor beside it is the mapped
`app.js:55`.
