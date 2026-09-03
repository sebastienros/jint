# DevTools protocol for Jint — design

**Status: finalized design; implementation under way.** The authoritative statement of this design is the body
of [sebastienros/jint#3575](https://github.com/sebastienros/jint/issues/3575); where this
document and that issue disagree, the issue wins and this file is brought back into line. What this file adds is
the longer form: the engine mechanisms each decision rests on, named so that a reader can find them.
[§9](#9-what-shipped-and-where-it-differs) is the index of what was built against this design and the one line
in which each item differs from it. For what the package does rather than why, read
[the README](../../README.md#chrome-devtools-protocol-opt-in-package) instead of this file.

Everything normative here was read from the [Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/)
(the pinned `js_protocol.json` / `browser_protocol.json` under `tools/devtools-protocol/`), from V8's inspector
design (`v8_inspector::V8InspectorClient::runMessageLoopOnPause`), and from what the clients actually send,
recorded by `tools/cdp-histogram/` into `tools/devtools-protocol/handshakes/`.

> **On citations.** This file cites files and members, not line numbers; a wrong line number still reads like a
> fact. `grep` finds a member.

---

## 1. Why a protocol, and why this one

Jint has had a debugger since 3.x (`Jint/Runtime/Debugger/`): breakpoints with conditions, step in/over/out,
return-point stepping, a call stack with per-frame `this` and a scope chain whose `DebugScopeType` was written to
mirror the protocol's `Debugger.Scope` types, expression evaluation in the paused context, and a `BeforeEvaluate`
event per script. What it never had was a way for a tool to reach it. Two community attempts exist and both are
dead: `Jither/Jint.DevToolsProtocol` (CDP `Debugger`/`Runtime`, built against a private branch, never worked,
last push 2024-02) and `Jither/Jint.DebugAdapter` (Debug Adapter Protocol, "not ready for production use — at
all", last push 2024-01). Their author's notes list the engine gaps that stopped them: no pause on exception, no
way to enumerate breakpoint locations, a fresh `DebugInformation` per execution point. Those gaps are closed by
the engine work in §5, inside Jint, where they belong.

The protocol is CDP because every client that matters speaks it — Puppeteer, Playwright on Chromium,
PuppeteerSharp, Playwright for .NET, chrome-remote-interface, chromedp, and the Chrome DevTools frontend itself.
WebDriver BiDi is the W3C draft and Firefox's default; it is a later façade over the same session core (Lightpanda
ships both from one core), not a first target. A Debug Adapter Protocol adapter for VS Code is likewise a thin
layer over the same core, deferred. Nothing here invents a protocol.

There are two layers because the two audiences differ:

- **`Jint.DevTools`** is engine-level. It serves *every* embedder — a workflow engine, a CMS, a game — that wants
  to attach Chrome DevTools to the scripts it runs: `Runtime`, `Debugger`, `Profiler`, `Console`, `Log`,
  `Schema`, `Browser`, and `Target` for engine targets. A Jint engine appears to the frontend as a Node-flavoured
  target (`js_app.html?v8only=true`), so the frontend never asks it for a page.
- **`Jint.Browser`** adds the page-level domains (`Page`, `DOM`, `Network`, `Fetch`, `Input`, `Emulation`,
  `Storage`, `Accessibility`, page `Target`s) on top of the same session core. It is the subject of
  [`headless-browser.md`](headless-browser.md).

## 2. The thread rule

Every `JsValue` and every `Engine` is thread-affine: `Engine.EnterHostCall` and
`Engine.VerifyValueConstructedOnOwningThread` fail fast when another thread touches them, and *Jint never starts a
thread to run script* is load-bearing across the web-API family (see `docs/design/web-workers.md` §1). The
protocol package is a *host*, so it may own threads, but the rule it lives by is:

> A `JsValue` never leaves the engine thread. Transport threads move strings. Every domain method runs on the
> engine thread. Every event is serialized on the engine thread before it is handed to the writer.

The mechanism is one mailbox per target, `EngineDispatcher`, drained in exactly two places:

1. **Running.** `Post(item)` enqueues and wakes the engine's loop through the public, thread-safe
   `engine.Tasks.Post(Action)` (engine PR E1, a one-line public door over the internal
   `Engine.AddToEventLoop(Action, generation)`). The drain therefore runs as an ordinary event-loop job on
   whichever thread pumps the engine, wakes `Tasks.WaitForScheduledWork`, and interleaves with microtasks. A
   command never calls `WaitForScheduledWork` or `UnwrapIfPromise` from inside a job (the pump's re-entrancy guard
   forbids it); `Runtime.evaluate` with `awaitPromise` attaches reactions to the promise and answers when they
   fire, which is V8's shape too.
2. **Paused.** The debugger's pause is synchronous: `DebugHandler.Pause` invokes the `Break`/`Step` delegates
   inline and the delegate's *return value* is the next `StepMode`. There is no resume token. So the protocol
   traffic that a paused page generates — `Runtime.getProperties` for the Scope pane, `evaluateOnCallFrame` for
   the console, `Debugger.resume` — is serviced by a message loop running **inside the paused handler on the
   engine thread**, exactly `runMessageLoopOnPause`: send `Debugger.paused`, then wait on the mailbox signal,
   drain pause-safe items, until a resume or step command sets the mode and ends the loop; send
   `Debugger.resumed`; return the mode. Commands that would re-enter a public engine entry while paused
   (`Runtime.runScript`, `Profiler.start`) are answered `-32000 "Not allowed while paused"`.

Hosts integrate through `EngineTargetOptions.ThreadMode`:

- `HostOwned` (default): the host's thread runs script and pumps; commands are serviced when it calls
  `engine.Tasks.ProcessTasks()` or `target.Pump()`. A command waiting longer than `CommandTimeout` fails with
  `-32000 "Engine is not being pumped"`, which is the diagnostic a host that forgot to pump needs.
- `LibraryOwned`: the target starts one thread running drain → `ProcessTasks` → `WaitForScheduledWork`, the host
  submits work with `target.Post(Action<Engine>)`, and the engine's single-drainer rule fail-fasts any host thread
  that touches the engine directly.
- `WaitForDebuggerOnStart` (`--inspect-brk`): the first posted work is held until a session sends
  `Runtime.runIfWaitingForDebugger`.

Client disconnect mid-pause detaches the session, which enqueues a control item; the pause loop resumes with
`StepMode.None`, that session's breakpoints are removed, its exception mode and skip-all flag reset, its object
groups released — V8's implicit `Debugger.disable`.

## 3. Targets and sessions

`/json/version` answers `webSocketDebuggerUrl: ws://host:port/devtools/browser/<id>`; `/json/list` lists targets
with `type: "node"`, a direct `ws://…/devtools/page/<targetId>` endpoint (no `sessionId` on that socket), and
`devtoolsFrontendUrl` in Node's form. Puppeteer requires flattened sessions: `Target.setAutoAttach(flatten: true)`
emits `attachedToTarget` for existing targets (with `waitingForDebugger`), `Target.attachToTarget(flatten: true)`
mints a `sessionId` that then rides every message; `flatten: false` is refused (no client sends it). One
`Debugger`-enabled session per target at a time; `Runtime` is shared. This is a documented divergence from V8's
per-session breakpoints.

**A page target is a target that outlives its engine.** `DevToolsTarget` carries the identity a client keeps
addressing and `TargetRuntime` carries one engine and everything that dies with it — the mailbox, the
remote-object table, the script registry, the console journal, the execution context — so committing a
document replaces the lot through `DevToolsTarget.Replace` and every domain hears about it. The
execution-context counter is the target's, so a second document's default context is `2` and never `1` again
and a stale identifier is refused rather than resolved. `ITargetHost` is what mints a target when a client
sends `Target.createTarget`; with none registered every target command behaves as it did.
`Jint.Browser`'s `AddBrowser` registers one, and publishes a **`tab`** target in front of each page because
Puppeteer's browser-level `setAutoAttach` filter excludes pages and reaches one through its tab.

The transport is `TcpListener` + an HTTP/1.1 upgrade + `WebSocket.CreateFromStream` — no `HttpListener`, no
ASP.NET dependency — behind `IDevToolsConnection`, with an `InProcessConnection` (string in, string out) for tests
and embedding.

## 4. The protocol layer is generated and checked in

`tools/devtools-protocol/` vendors `js_protocol.json` and `browser_protocol.json` at a pinned commit
(`pin.json`; a bump is a code change, as it is for test262 and WPT) and a `manifest.json` naming the domains
whose types are generated and the methods and events that are implemented. A .NET generator emits
`Jint.DevTools/Protocol/Generated/`: DTO records, per-domain abstract dispatch bases with one virtual per
command, event factories, the `System.Text.Json` source-generation context, and the manifest tables
`Schema.getDomains` answers from. Anything not in the manifest answers `-32601 "'X.y' wasn't found"`, Chrome's
text — never a silent success.

Checked-in output rather than a Roslyn generator, for four reasons: the JSON serializer context must be generated
*over* the DTOs and generators cannot chain; `Jint.SourceGenerators` is netstandard2.0 and must carry no JSON
dependency; generated code in the tree is reviewable, greppable and debuggable, which matters for a surface people
read to learn what is implemented; and `tools/whatwg-encoding/` set the precedent. A currency test re-runs the
emitter in memory and fails on drift. CDP enums are emitted as string constants (`JsonStringEnumMemberName` is
.NET 9+; net8.0 must compile) so the whole layer is AOT-safe.

`SpecCitationTests` only scans `tc39.es` URLs; CDP citations use the form
`https://chromedevtools.github.io/devtools-protocol/tot/<Domain>/#method-<name>` and are verified offline against
the vendored JSON by `ProtocolCitationTests`.

## 5. Domains and the engine seams they ride

| Domain | What it maps onto | Engine change |
| --- | --- | --- |
| `Runtime` | `engine.Evaluate` (running) or `DebugHandler.Evaluate` (paused); `getProperties` over `GetOwnPropertyKeys` + descriptors, accessors reported and never invoked; `awaitPromise` by reactions; `consoleAPICalled`; `exceptionThrown`/`exceptionRevoked` from `Tasks.PromiseRejectionTracker`; `getHeapUsage` from `Diagnostics.GetMemoryReport` | **E5** ([#3597](https://github.com/sebastienros/jint/pull/3597)) structured `ConsoleRecord` sink overload + public `ValueInspector` (previews that never run script, promoted from `ConsoleFormatter`) |
| `Debugger` | `ScriptRegistry` on `DebugHandler.BeforeEvaluate` keyed by `Program` identity (a cached `Prepared<Script>` is announced once); breakpoints as `DevToolsBreakPoint : BreakPoint` (the class is unsealed for this); `pause` as a flag checked in `Skip`; scopes from `DebugScopeType` 1:1 | **E1** ([#3587](https://github.com/sebastienros/jint/pull/3587)) `TryGetSourceText(Program)` for `getScriptSource`; **E2** ([#3614](https://github.com/sebastienros/jint/pull/3614)) `GetStepLocations(Program)` for `getPossibleBreakpoints` and column snapping; **E3** ([#3623](https://github.com/sebastienros/jint/pull/3623)) pause on exceptions with caught/uncaught; **E4** ([#3622](https://github.com/sebastienros/jint/pull/3622)) evaluate on any call frame; **E6** ([#3632](https://github.com/sebastienros/jint/issues/3632)) `CallFrame.Program`, a profile frame's `Program` and `CoverageSource.Program`, so a position is matched to a script by identity rather than by source name ; **E8** ([#3631](https://github.com/sebastienros/jint/issues/3631)) `ExceptionPauseMode.Caught` and `StepMode.Unchanged`, so the four protocol states are four engine modes and declining a pause cannot cancel a step |
| `Profiler` | `Profiler.Profile` synthesized behind an `IProfileSource` seam from either of the engine's profilers (`Jint/Profiling/`) — the sampler by default, the evented one when the host is already sampling — one weighted sample per interval between two activations, so the time deltas add up to the recording rather than approximating it; precise/best-effort coverage from `CoverageReport` (`Jint/Runtime/Coverage/`), with the uncovered set derived from the script registry's abstract syntax tree so an unused function is reported with `count: 0` | **E7** ([#3630](https://github.com/sebastienros/jint/issues/3630)) public read-only accessors on `SampledProfile`'s sample, stack, frame and function tables, so the sampler of [#3608](https://github.com/sebastienros/jint/pull/3608) is consumable without writing its document and parsing it back |
| `Console`, `Log` | the structured record | E5 |
| `Target`, `Browser`, `Schema` | the session core and the manifest | none |

**All eight engine seams are merged**, and every one is additive and public — so that a third party (AngleSharp.Js,
a DAP adapter, a host's own tooling) can build the same thing without `InternalsVisibleTo`. `Jint.DevTools`
itself consumes only public Jint API; that is deliberate, and it is what proves the seams are reachable.

Every seam the build wanted it now has; the three it was missing at design time are
[#3630](https://github.com/sebastienros/jint/issues/3630),
[#3631](https://github.com/sebastienros/jint/issues/3631) and
[#3632](https://github.com/sebastienros/jint/issues/3632), and each is named in the table above under the
domain it was blocking.

## 6. Costs, stated up front

- `Options.Debugger.Enabled`, `Options.Profiling.Enabled` and `Options.Coverage.Enabled` are construction-time;
  `UseDevTools` sets them, and a target added on an engine without them degrades per domain with an explicit
  `-32000`.
- Debug mode disarms the interpreter's tight-loop lane (`Jint/Constraints/AGENTS.md`), and the `Skip`
  subscription adds a delegate call per execution point; the subscription is attached only while a session has
  `Debugger` enabled, and benchmarks never run with it.
- Source text is retained for `getScriptSource` through the same switch as `Function.prototype.toString`
  (`RetainFunctionSourceText`), one weak-table entry per `Program`, not a second copy.
- Remote-object handles are strong until `releaseObject`, `releaseObjectGroup` or session detach.

## 7. Deliberately absent

Async call stacks, source maps, blackboxing, `HeapProfiler`, per-session breakpoints, and any always-on mode.
Each is a follow-up issue if a client turns out to need it.

## 8. Verification

All of it is in place, and each layer claims something the one below it cannot:

- **In-process protocol tests** over `InProcessConnection`, no sockets, asserting the envelope as text — `id`,
  `error.code` and the wording a client feature-detects on.
- **Socket tests**, which are not a duplicate of those: the upgrade handshake, the frame handling, the single
  writer, the close ordering, and whether the read loop keeps reading while a command is outstanding.
- **Recorded handshake fixtures** of PuppeteerSharp, Puppeteer, Playwright, Playwright for .NET and the DevTools
  front end, in `tools/devtools-protocol/handshakes/`, replayed as tests: a manifest method must be answered,
  and every other must be *exactly* `-32601`.
- **A PuppeteerSharp suite over a real WebSocket**, the only test that can claim client compatibility: connect,
  handles, bindings, console, breakpoints, exception pauses, a profile, a coverage round trip, and the Jint
  REPL's `--inspect` spawned as a separate process and reached through the port its banner named.
- **The Chrome DevTools front end, driven.** `tools/devtools-frontend-smoke/` launches Chrome for Testing,
  loads the hosted front end against a real Jint process and asserts against the front end's own DOM;
  `Jint.DevTools/docs/manual-checklist.md` is the same walk by hand. Neither runs in CI — one downloads a
  browser, the other is a person — which is why the layers above it exist.
- **A Native AOT probe in `Jint.AotExample`**, which references `Jint.DevTools` *without* rooting it and drives
  the published native binary over a real socket: attach, evaluate, break, resume. The same CI leg fails if any
  `IL2xxx`/`IL3xxx` diagnostic is attributed to a file in the package.
- **The host-contract verification leg** with `JINT_HOST_CONTRACT_VERIFICATION=1`, which makes the thread rule
  exact rather than asserted: a `JsValue` touched from a transport thread fails loudly under it.

## 9. What shipped, and where it differs

The sections above are the design, kept as the design. This table is the index of what was built against it:
one row per item, the pull request that built it, and the one line in which what shipped is not what was
planned. A blank last column means the section above describes what exists.

| § | What shipped | PR | Where it differs |
| --- | --- | --- | --- |
| 4 | The package itself: the pinned vendored protocol JSON, the manifest, the emitter and `Protocol/Generated/` | [#3586](https://github.com/sebastienros/jint/pull/3586) | — |
| 4 | The handshakes four automation clients and the front end actually send, recorded rather than assumed | [#3599](https://github.com/sebastienros/jint/pull/3599) | — |
| 2, 3 | `EngineDispatcher`, the two drains, `ThreadMode`, the transport and flattened sessions | [#3602](https://github.com/sebastienros/jint/pull/3602) | `flatten: false` is refused outright with `-32000` rather than supported, and breakpoints are the **target's** rather than the session's — V8 keeps them per session |
| 3 | A target that outlives its engine: the `DevToolsTarget` / `TargetRuntime` split, and `ITargetHost` for a client's `Target.createTarget` | [#3678](https://github.com/sebastienros/jint/pull/3678) | `ITargetHost` is `internal`. `Jint.Browser` is its only implementer today, and it is promoted when there is a second — the seam a third party needs meanwhile is [#3684](https://github.com/sebastienros/jint/issues/3684) |
| 5 | `Runtime`: handles, `getProperties`, `callFunctionOn`, `awaitPromise`, bindings | [#3605](https://github.com/sebastienros/jint/pull/3605), engine seam [#3597](https://github.com/sebastienros/jint/pull/3597) | — |
| 5 | `Console` and `Log`: what a script logged and what it threw, values still attached | [#3606](https://github.com/sebastienros/jint/pull/3606) | — |
| 5 | `Debugger`: breakpoints by URL or script, the three steps, `continueToLocation`, frames, scopes, `setVariableValue` | [#3620](https://github.com/sebastienros/jint/pull/3620), engine seams [#3587](https://github.com/sebastienros/jint/pull/3587) and [#3614](https://github.com/sebastienros/jint/pull/3614) | — |
| 5 | Pausing where a script throws, and evaluating in any frame of the stack | [#3625](https://github.com/sebastienros/jint/pull/3625), engine seams [#3623](https://github.com/sebastienros/jint/pull/3623), [#3622](https://github.com/sebastienros/jint/pull/3622) and [#3662](https://github.com/sebastienros/jint/pull/3662) | — |
| 5 | `Profiler`: a sampled CPU profile, and precise and best-effort coverage | [#3629](https://github.com/sebastienros/jint/pull/3629), engine seams [#3608](https://github.com/sebastienros/jint/pull/3608) and [#3654](https://github.com/sebastienros/jint/pull/3654) | — |
| 5 | Describing a value: a function's declaration site, a console message's anchor, an error's details, a path published as a URL | [#3640](https://github.com/sebastienros/jint/pull/3640) | A function value does not publish the `Program` it was parsed in, so `[[FunctionLocation]]` still resolves its script by name ([#3666](https://github.com/sebastienros/jint/issues/3666)) |
| 5 | A position resolves to its script by identity rather than by source name | [#3652](https://github.com/sebastienros/jint/pull/3652), engine seam [#3651](https://github.com/sebastienros/jint/pull/3651) | — |
| 8 | The REPL's `--inspect`/`--inspect-brk`, the driven front end, and the native binary as a client | [#3633](https://github.com/sebastienros/jint/pull/3633) | Neither `tools/devtools-frontend-smoke/` nor `docs/manual-checklist.md` runs in CI — one downloads a browser and the other is a person — which is why the four layers below them exist |

Two things a reader of §5 should know are decisions rather than omissions. The eight engine seams the table
in that section names all merged, and every one is public and additive, so a third party can build the same
server without an `InternalsVisibleTo` grant — `Jint.DevTools` consumes only Jint's published API, and that is
what proves it. And the page domains (`Page`, `DOM`, `Network`, `Fetch`, `Input`, `Emulation`, `Storage`,
`Accessibility`) are in the same manifest but are implemented in `Jint.Browser`; on an engine target every one
of them is `-32601`.
