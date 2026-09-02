# Attaching the Chrome DevTools front end by hand

The front end is a user interface, and what it does with a target is its own business rather than this
server's. So this is the one claim in the package that a test cannot make on its own: everything under
`Jint.Tests.DevTools/` asserts what *this server answers*, and none of it asserts what *Chrome shows*.

Most of the walk below is now driven automatically by
[`tools/devtools-frontend-smoke/`](../../tools/devtools-frontend-smoke/README.md), which launches Chrome for
Testing, loads the hosted front end against a real Jint process and asserts against the front end's own
DOM. That tool is not in CI — it downloads a browser and reaches the network — so run it when the protocol
surface changes, and run this checklist when you want to see it with your own eyes, or when the tool says a
step could not be driven.

## Start something to attach to

```bash
dotnet run --project Jint.Repl -c Release -- --inspect -f script.js -t 60
```

The REPL prints three things: the WebSocket endpoint, the `devtools://` address of the front end, and what
to type into `chrome://inspect`. `--inspect=0` takes an ephemeral port and the banner says which one;
`--inspect-brk` runs nothing until a client has attached and sent `Runtime.runIfWaitingForDebugger`, which
is what you want when the interesting thing happens in the first line of the script.

A host embedding the package does the same with a `DevToolsServer` and an `EngineTarget`; the REPL is only
the shortest way to have one.

## Attach

Either address works and they are worth knowing apart:

- **`chrome://inspect`** — click **Configure…**, add `127.0.0.1:9229` (or whichever port the banner named),
  close the dialog, and the engine appears under **Remote Target** with the title the host gave it. Click
  **inspect**. This is the path that reads `/json/list` off the server, so a target that does not appear
  here is a discovery-document problem rather than a protocol one.
- **`devtools://devtools/bundled/js_app.html?experiments=true&v8only=true&ws=127.0.0.1:9229/devtools/page/<targetId>`**
  — paste it into the address bar of a Chrome tab. `v8only=true` is what makes the front end open its
  JavaScript-only layout rather than asking for a page that does not exist. Chrome refuses to navigate to
  `devtools://` from a link or from automation, so this one has to be typed.

## What each panel should show

| Panel | What answers, and what it proves |
| --- | --- |
| **Sources** → navigator | Every script the engine has parsed, by the name the host passed to `Execute`/`Evaluate` — except that a name which *is* an absolute filesystem path appears as a `file://` URL, so the tree groups it under a domain and names it by its file name. An empty tree means `Debugger.enable` was refused — check the engine was built with `UseDevTools()`. |
| **Sources** → editor | The script's own text, from `Debugger.getScriptSource`. Text that reads `// source not available` means `Options.RetainFunctionSourceText` is off. |
| **Sources** → gutter | Clicking a line number sets a breakpoint, and the marker snaps to the nearest position the engine will stop at. A marker that lands on a blank line or a comment is a `getPossibleBreakpoints` defect. |
| **Sources** → paused | Run something that reaches the line. Execution stops, the line highlights, and the **Call Stack**, **Scope** and **Watch** panes fill. The Scope pane is a snapshot: it lists the bindings but never calls a getter, and a binding still in its temporal dead zone is absent rather than `undefined`. |
| **Sources** → step and resume | The three step buttons and Resume all move the engine on. While it is paused the host process is held on the thread that owns the engine, which is why `DevToolsServerOptions.PauseTimeout` exists. |
| **Sources** → pause on exceptions | The ⏸ button stops the engine **at** the throw, before anything unwinds, with the thrown value in the sidebar; both "uncaught" and "caught" work. Two divergences from Chrome are the engine's rather than this package's: a throw crossing an `async` function's body boundary counts as uncaught whatever the caller wrapped it in, and `Promise.reject(value)` stops nothing because nothing was thrown. Filtering a throw out in `caught` mode also cancels a step that was in flight — [#3631](https://github.com/sebastienros/jint/issues/3631). |
| **Console** | What the script logged, as it logged it: an object arrives as an expandable preview rather than as a string, and expanding it asks the server for the properties. Every line carries an `app.js:NN` anchor on the right, and an error expands into its frames. Nothing here runs script that the script did not run. |
| **Console** → prompt | Typing an expression evaluates it in the engine — including while it is paused, where it evaluates in the selected call frame. The eager-evaluation preview above the prompt stays empty on purpose: it asks for `throwOnSideEffect`, which this server refuses rather than answers by running the code the front end asked not to run. |
| **Performance** | **Start profiling and reload** does not apply; use the record button. The profile is the engine's own sampler, so the flame chart is JavaScript frames and nothing else. |
| **Coverage** | Answers only if the engine was built with `UseDevTools(o => o.Coverage = true)`; the REPL's `--inspect` turns it on. A function the script never called is listed with a zero count rather than being absent. |
| **Memory** | Nothing. There is no `HeapProfiler` here and there deliberately never will be; the heap is the CLR's, and what a host wants instead is `engine.Diagnostics` and `Options.Constraints`. |

## What is expected to be missing

Not defects, and not worth reporting again: **Elements**, **Network**, **Application** and **Lighthouse**
have no page behind them — that is what `Jint.Browser` is for — and the front end shows them empty rather
than erroring. **Async call stacks** are absent because the engine retains no stack across a promise
reaction, and **blackboxing** and **source maps** are accepted and ignored. Editing a paused program
(**Save**, "Restart frame") is refused: the interpreter is standing inside an abstract syntax tree it has
already walked into. `Jint.DevTools/AGENTS.md` carries the full list and the reason for each.
