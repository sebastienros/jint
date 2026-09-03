# Agent instructions: sessions, targets and the engine thread

> **Read this when:** You are touching `Jint.DevTools/Session/`, a target (`DevToolsTarget`, `EngineTarget`,
> `TargetRuntime`), the mailbox a command crosses to reach the engine, or the pause-time message loop.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first, then [`Jint.DevTools/AGENTS.md`](../AGENTS.md) for the
> thread rule, the protocol pin and the manifest. Nothing below is repeated in either.

### The mailbox, which is how a command reaches the engine

`Session/EngineDispatcher.cs` is **the only path from a transport thread to the engine**, and every domain
that holds engine state is registered on a session that goes through it (`DevToolsSession.UseGateway`). The
mechanism, in order:

1. A transport thread parses the envelope, resolves the session, and — because that session has a gateway —
   hands the request to `EngineDispatcher.DispatchAsync`, which enqueues it and waits.
2. The enqueue calls `engine.Tasks.Post(Drain)`, the one engine entry a thread that does not own the engine
   may call. It wakes whichever thread is pumping.
3. `Drain` runs **on the engine thread, inside an ordinary event-loop job**, answers the command, and
   completes the waiting task with the finished JSON.
4. The transport thread writes that string. **No `JsValue` ever crosses.**

Four consequences that are not negotiable:

- **A command runs inside a job, so it may not drain.** `WaitForScheduledWork` and any promise unwrap that
  pumps are forbidden — the pump's re-entrancy guard refuses them. `Runtime.evaluate` with `awaitPromise`
  attaches reactions and completes from the job that runs them, which is V8's shape.
- **The drain must never throw.** It is a job on the host's own pump, so an exception erupts out of
  `ProcessTasks` into the host. Every item catches everything and answers with it.
- **A command that times out is answered, not cancelled.** `CommandTimeout` bounds the *client's* wait; the
  item stays queued and still runs when the engine is next pumped. The two messages are told apart
  deliberately — an item nothing dequeued says `Engine is not being pumped`, one that started and did not
  finish says `Command timed out` — because a host debugging the wrong one wastes an afternoon.
- **Host work and protocol commands share the queue**, so a host's `target.Post` runs in order with the
  commands around it. The single exception is `WaitForDebuggerOnStart`: host work is held and protocol
  commands are not, because otherwise the command that ends the wait could never be answered.

The two thread modes are `EngineTargetOptions.ThreadMode`. `HostOwned` (the default) is the host's own loop;
`EngineTarget.Pump` is a convenience over `engine.Tasks.ProcessTasks()`, not a second mechanism.
`LibraryOwned` starts one thread running drain → `ProcessTasks` → `WaitForScheduledWork`, and the host
submits work with `Post`/`PostAsync`. A host-owned target waits for a debugger by *pumping*
(`WaitForDebugger`), because the command that releases it is answered on that very thread.

### Targets and sessions

A `DevToolsSession` is a node. The **root** owns the connection and parses every envelope; a **child** is
what one attachment minted, carries the `sessionId` that reaches it, and writes through its root. A message
naming a `sessionId` nothing answers to is `-32001 "Session with given id not found."` — Chrome's wording,
pinned by test, and a different thing from `-32000`.

Two paths open a conversation, and `Transport/WebSocketServerTransport.cs` decides between them *before* the
upgrade, so a client that guessed is told rather than left holding a socket:

- `/devtools/browser/<browserId>` is a `BrowserSession`: `Schema`, `Browser`, `Target`. **None of it touches
  an engine**, so it has no gateway and answers on the transport thread.
- `/devtools/page/<targetId>` is a `TargetSession` on the root node itself — no `sessionId` on any message —
  carrying `Runtime` and no `Target` domain at all, because one engine has no target tree.

Three decisions worth not relitigating:

- **Flattened sessions only.** `Target.attachToTarget` and `Target.setAutoAttach` refuse `flatten: false`
  with `-32000 "Only flatten protocol is supported"`. The wrapped model routes every message through
  `Target.sendMessageToTarget`, and **no client in `tools/devtools-protocol/handshakes/` sends it** — so
  that command stays unimplemented (`-32601`) rather than buying a second routing path for nobody.
- **`setAutoAttach` on an attached session is a success that attaches nothing.** Clients walk the target
  tree by sending it on every session they are handed; an engine has no children, and a refusal there
  reads as a broken target.
- **A browser context is not something an engine target has.** With no `ITargetHost` registered,
  `getBrowserContexts` answers an empty list and `createBrowserContext` / `disposeBrowserContext` answer
  `-32000` with the reason, because minting an identifier that partitions nothing would tell a client its
  next target was isolated when it is not.

### A target outlives its engine

`DevToolsTarget` is the identity a client keeps addressing; `TargetRuntime` is one engine and everything that
dies with it. An `EngineTarget` has one runtime for its whole life. A page has one **per navigation**:
committing a document builds an engine and hands it to `DevToolsTarget.Replace`, which disposes the previous
runtime, installs the new one, re-installs the bindings and tells every domain through
`ITargetObserver.RuntimeReplaced`.

What is on which side is the whole of the split, and putting something on the wrong one is how a client is
answered about a document that has gone:

- **The target**: `TargetId`, `Type`, `Title`, `Url`, `BrowserContextId`, `OpenerId`, the observer list, the
  `Describer`, the `BindingRegistry` (a `Runtime.addBinding` name is re-installed into every new engine
  *before* any script of the new document runs), the `WaitingForDebugger` state, the command and pause
  timeouts, and the execution-context counter.
- **The runtime**: the `Engine`, the `EngineDispatcher`, the `RemoteObjectTable`, the `ScriptRegistry`, the
  `CompiledScriptRegistry`, the `ConsoleJournal`, the console-sink binding, the promise-rejection
  subscription, and the default execution context's identifier.

Four consequences:

- **A domain holds the target and reads `target.Runtime` per command.** A cached engine, object table or
  script registry is a domain still answering about the last document.
- **The gateway is the target, not a mailbox.** `TargetSession` calls `UseGateway(target)` and the target
  forwards to the current runtime's dispatcher, so a command is queued on the engine that is running now. A
  command already queued on the engine being replaced is answered `-32000 "Execution context was
  destroyed."` rather than left to the client's own timeout — **unless it never named a context**, which is
  every `Input` command and only those (`EngineDispatcher.IsAddressedToTheTarget`). A mouse event, a key
  event and an inserted string are delivered to the *page* and dispatched at whichever document is current
  when they run; a client sends the two halves of one key press as two commands and a navigation is free to
  happen between them, so `Abandon` hands such a command to the runtime that replaced this one instead of
  refusing it (#3723, found as a `Keyboard.PressAsync` flake whose `keyDown` submitted a form). It reaches
  the new document, or nothing at all when that document has not been parsed yet — never an error. Everything
  else names a context however indirectly — a handle, a script identifier, a realm — so the refusal is the
  truthful answer and stays.
- **The context counter is the target's**, so the second document's default context is `2` and never `1`
  again. That is what makes a stale `contextId` distinguishable, and `DevToolsTarget.RequireContext` answers
  one with `-32000 "Cannot find context with specified id"` — Chrome's text, which the recordings show
  PuppeteerSharp receiving and coping with. Every table is seeded from a process-wide serial for the same
  reason: an `objectId` or a `scriptId` from two documents ago must fail to resolve rather than land on a
  value of the new engine.
- **Isolated worlds are aliases.** `CreateWorldContext(name)` mints an extra context identifier over the
  *current* realm and announces it with `auxData: { isDefault: false, type: "isolated", frameId }`. There is
  one realm per document, so a world is a name for it and not an isolate; it buys a client its own
  identifier to address and none of the isolation the name promises, and it lasts until the next navigation.

**A target may have children, and one kind does.** `DevToolsTarget.Children` is empty for everything but a
`tab`: modern Chrome puts one between the browser and each page, Puppeteer's browser-level `setAutoAttach`
filter is *everything but a page*, and it reaches a page by sending `setAutoAttach` again on the tab's
session. So the nested `Target` domain attaches its own target's children rather than answering an empty
success. `Jint.Browser` is what publishes tabs; found by driving the client.

`ITargetHost` is what mints a target when a client asks for one. `DevToolsServer.UseHost` registers it, and
`Target.createTarget` / `closeTarget` / `createBrowserContext` / `disposeBrowserContext` / `getBrowserContexts`
route through it; with none registered they behave exactly as they did. A target created while the asking
session had `setAutoAttach(waitForDebuggerOnStart: true)` is created **waiting** — the flag rides
`TargetCreationRequest` rather than being applied afterwards, because a host that navigates as it creates
would otherwise have run the first document before anybody could hold it — and
`attachedToTarget.waitingForDebugger` tells the client it must send `Runtime.runIfWaitingForDebugger`.
`DevToolsTarget.RegisterDomains` is virtual so a subclass adds its own domains next to the built-in five, and
`Describe` is the one `TargetInfo` both `/json/list` and `Target.getTargets` are written from.

### The pause loop

When the debugger pauses, the engine thread is *inside* `DebugHandler`'s synchronous `Break`/`Step` handler.
It cannot return — returning is what resumes the script, and the *return value* is the next `StepMode`; there
is no resume token — and nothing posted through `engine.Tasks.Post` will ever run while it is there, because
that thread is the pump. So the handler services the client itself: `DebuggerDomain.RunPauseLoop` sends
`Debugger.paused`, then loops on `EngineDispatcher.DrainPaused` — the same mailbox, answered **inline on the
engine thread** — until something says what to do next, sends `Debugger.resumed`, and returns that decision.
V8's `runMessageLoopOnPause`, and the one piece here that cannot be ordinary asynchronous code.

- **Three things end a pause and every one of them has to.** A client command (`resume`, the three steps,
  `continueToLocation`); the client going away, which reaches `DebuggerDomain.Detach` on a transport thread
  and resumes with `StepMode.None`; and `DevToolsServerOptions.PauseTimeout`, reported through
  `Log.entryAdded`. **There is deliberately no infinite setting**: in `HostOwned` mode the thread a pause
  holds is the *host's own*, and a bound that could be switched off is a host a client wedges by walking
  away. A `LibraryOwned` target also watches its stopping token.
- **Almost everything is answered while paused.** A client that serialized on a command and never got it
  back would deadlock against its own pause, so the paused drain refuses exactly two — `Runtime.runScript`
  and `Profiler.*`, which hand a suspended engine a whole new script — with `-32000 "Not allowed while
  paused"`. `Runtime.evaluate` and `callFunctionOn` route through `DebugHandler.Evaluate` rather than a
  public engine entry, so they see the paused frame; `Runtime.awaitPromise` answers the promise as it
  stands, because settling one means running a reaction.
- **Host work waits.** `EngineTarget.Post` is work for a *running* engine; the paused drain leaves the host
  queue alone, so it runs in order after the resume.
- **The pause nests inside a command.** `Runtime.evaluate` of an expression that hits a breakpoint pauses
  while that command is being answered, which is why `EngineDispatcher.Drain` refuses to re-enter itself
  and `DrainPaused` is exempt: nesting *it* inside a running drain is the mechanism.
- **The socket keeps being read.** `WebSocketConnection.ReadAsync` awaits a handler only while it completes
  synchronously; one that does not is observed and the loop reads on. Otherwise the command that paused the
  engine would still be outstanding and `Debugger.resume` — which arrives afterwards on that same socket —
  could never be read. Replies may then leave out of order, which is what an `id` is for.
- **Nothing in the pause loop may `await` onto another thread and then touch the engine.**

`Debugger.pause` is not an interrupt: it arms a `DebugHandler.Skip` subscription and the *next execution
point* pauses on it, so an engine inside one long statement is unreachable and `Options.Constraints` bounds
that instead. The subscription is armed only while a pause is outstanding, because `Skip`'s return value
*sets the step mode* and a permanent one would have to answer with a mode it cannot read back.

A scope a client expands is a **snapshot**: environment records are not objects, so a declarative scope's
bindings are copied into one — getter-free, like every describing path here — while a global or `with`
scope is answered as the object it already is. A binding in its temporal dead zone is absent rather than
shown as `undefined`, and `Debugger.setVariableValue` writes through.

