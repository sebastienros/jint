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
something worth looking at; and a `setTimeout` inside a one-second `setInterval` throws, so an uncaught
exception is reachable.

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
| 2 | `sources-lists-script` | The Sources navigator has a **visible** entry for the running script — the folder is expanded first, because a collapsed tree keeps its children in the document and asserting on one of those would be reporting the DOM rather than the panel. |
| 3 | `sources-shows-source-text` | Clicking that entry opens the editor and the retained source is in it: `lineTotal`, `computeTotal`, `describeOrders`, `heartbeat` and `reconcile` are all read back out of the CodeMirror content. That is `Debugger.getScriptSource` end to end. |
| 4 | `console-shows-log-with-object-preview` | The Console shows `console.log('order book ready', …)` **with its object preview expanded into properties**, not as a bare `Object`. |
| 5 | `console-evaluates-expression` | `1+1` typed at the Console prompt and submitted comes back as `2` in a result row. |
| 6 | `breakpoint-set-in-the-gutter-is-hit` | A breakpoint set by clicking the editor gutter — the front end's own `Debugger.setBreakpointByUrl` — stops the engine, and the front end says "Paused on breakpoint". |
| 7 | `scope-pane-populated` | The Scope pane lists the frame's scopes and the bindings inside them, with values. |
| 8 | `resume-continues-execution` | The breakpoint is cleared from the gutter, Resume is clicked, and **the heartbeat in the Console advances past the tick it was paused on**. Execution continuing is what proves a resume; the paused indicator going away only proves the indicator went away. |

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

- **The navigator lists the script by its whole filesystem path, not by `app.js`.** Step 2 asserts "an entry
  ending in `fixture/app.js`", and records the text it actually read. The path is there because Jint
  publishes a bare filesystem path as `Debugger.scriptParsed.url`; see the defects below.
- **The Console's message list is virtualized.** Only the visible tail is in the DOM, and the heartbeat
  pushes everything else off it, so the message logged *before* the front end connected cannot be read
  directly. Step 4 types it into the Console's Filter box first. That is still driving the UI, and it has a
  bonus: a filtered-in message that arrived before the socket existed is proof that the console journal Jint
  replays on `Runtime.enable` reached the front end.
- **The editor is virtualized too**, though not at this file's size. `deepTexts` truncates each entry at 400
  characters for readability; the editor is read with `deepFullText`, which does not. A step that asserts on
  a long text and uses the wrong one silently reads a prefix — that is how the first run of step 3 failed.
- **Step 7 asserts that the Scope pane is populated; it does not assert that everything in it is right.** The
  same pane renders eleven function-valued bindings as `ƒ undefined()`. That is recorded in `summary.json`
  under `functionValuedBindings` rather than asserted, and it is a Jint defect — below.
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

## What this run found in Jint's DevTools server

All four are visible in the checked-in screenshots and reproduce on demand.

- **A frame the engine was entered at has no name and no `functionLocation`.** The Call Stack pane shows
  `computeTotal` over `(anonymous)`, where V8 would show the timer callback's own name. In
  `Debugger.paused`, the outermost call frame — the one a `setInterval` / `setTimeout` callback entered —
  comes back with `functionName: ""` and no `functionLocation` at all, whether the callback is a function
  declaration, a named function expression or an inline one. Every inner frame is named correctly, including
  named function expressions, so the name is being taken from the call site rather than from the function.
  Without `functionLocation` that call-stack row is also not clickable.
- **`RemoteObject.description` for a function is a rendered label where the front end expects source text.**
  Jint sends `description: "ƒ computeTotal()"`. The front end treats that field as
  `Function.prototype.toString` output and parses the name back out of it, so every function in the Scope
  pane and in `Runtime.getProperties` renders as **`ƒ undefined()`** — 47 of them in one screenshot.
  `console.log`'s *previews* are unaffected, because a preview's value string is shown verbatim; it is the
  full property path that breaks. The fix is to send the function's source text.
- **`Runtime.consoleAPICalled` carries no `stackTrace`**, so no console message has a source anchor. In the
  screenshots the uncaught errors link to `app.js:48` (from `exceptionDetails`) while every `console.log`
  line has nothing on the right, where V8 puts the file and line the call came from.
- **The front end calls `Runtime.getExceptionDetails` once per uncaught exception, and is refused every
  time** (`-32601`). `Jint.DevTools/AGENTS.md` lists that command as deliberately absent on the grounds that
  nothing retains an exception's details past the command that reported them — which is a decision, not an
  oversight, but the recorded handshakes had no client asking for it and this one does, continuously. The
  console still renders the error, so it degrades rather than fails.

Two more refusals show up in the recording and are working as designed, listed so a reader does not file them
twice: `Network.*` is `-32601` because an engine target has no Network domain, and `Runtime.evaluate` with
`throwOnSideEffect` is `-32000 "Side-effect free evaluation is not supported"`, which costs the Console
prompt its eager-evaluation preview and nothing else.

One inconsistency worth a decision rather than a fix: `/json/list` reports the target's `url` as
`file:///D:/…/app.js` while `Debugger.scriptParsed` reports the same file as `D:/…/app.js`. The second is
what `Jint.Repl` passes as the source name; V8 emits a `file://` URL for both. It is why the Sources
navigator files the script under "(no domain)" with its whole path as the name, and why a breakpoint's
identifier reads `1:29:0:D:/Work/…/app.js`.
