# Agent instructions: the Chrome DevTools Protocol server

> **Read this when:** You are touching anything under `Jint.DevTools/`, the vendored protocol under
> `tools/devtools-protocol/`, or an engine seam this package consumes.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply everywhere. Nothing below is repeated there.

`Jint.DevTools` speaks the Chrome DevTools Protocol to any embedder's engine. It is the *engine-level*
half — sessions, `Runtime`, `Debugger`, `Profiler`, `Console`, `Log`, `Schema`, `Target`, `Browser` — and it
works for a host with no pages at all: an Orchard or Elsa host can attach Chrome DevTools to its scripts.
The page-level domains belong to `Jint.Browser`, which is AngleSharp plus Jint and is never described as a
DOM stack of its own.

The package is `net8.0;net10.0`, the same floor `Jint/WebApi/` sits on and for the same reason: host surface
built on the modern BCL, not engine surface a `netstandard` consumer resolves. Unlike the web APIs there is
**no `#if` anywhere in it**, and `PublicApiTest.NoSourceFileIsGatedByTargetFramework` says so — that is what
lets one public-API baseline cover both assets.

### The thread rule

**A `JsValue` never leaves the engine thread. A transport thread only ever moves strings.**

That is the whole reason `IDevToolsConnection` is as narrow as it is: `SendAsync(string)`, a
`Func<string, CancellationToken, ValueTask>` for received messages, and a closed callback. Whatever thread a
WebSocket receive loop happens to be on, the only thing it may hand a `DevToolsSession` is text.

Everything downstream of that follows:

- **Every domain method runs on the engine thread.** A `DevToolsDomain` may hold engine state — a
  `RemoteObjectTable`, a `ScriptRegistry`, a `DebugHandler` subscription — none of it thread-safe.
- **Nothing a command returns may outlive the command.** A `CommandContext` is valid for the command that
  received it, and must not be captured.
- **Serialization happens on the engine thread too**, because that is where the `JsValue` is. That is why
  `ProtocolEvent` carries `ParametersJson` rather than an object: serializing later would mean reflecting
  over a type the session does not know.
- **`Engine` is not thread-safe and sharing a `JsValue` across engines is unsupported.** Both are stated in
  the repository-root [`AGENTS.md`](../AGENTS.md#gotchas); this package must never break them.

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
- **A browser context is not something an engine target has.** `getBrowserContexts` answers an empty list;
  `createBrowserContext` and `disposeBrowserContext` answer `-32000` with the reason, because minting an
  identifier that partitions nothing would tell a client its next target was isolated when it is not.

### RemoteObject lifetime

A client cannot hold a `JsValue`, so it holds an `objectId` and the server holds the value for it. That
promise — **the value will still be there when you come back** — is the whole of
`Domains/RemoteObjectTable.cs`, and everything about handles follows from it:

- **The table is on the `EngineTarget`, ownership is per attachment.** The values belong to the engine, so
  two sessions address the same value by the same identifier; each entry remembers which attachment
  registered it, so detaching releases that attachment's handles and nobody else's.
- **Strong, never weak, never deduplicated.** A fresh identifier per wrap, which is V8's behaviour and what
  stops a client's release of one handle invalidating another it still holds.
- **Four endings**: `releaseObject`, `releaseObjectGroup`, the attachment detaching, and the target being
  disposed. Nothing expires on its own, and a client that leaks handles leaks engine values — the cost of
  the promise rather than a defect in it.
- **A group is inherited on the way in.** `getProperties` and `callFunctionOn` bill the handles they mint to
  the group the receiver already belongs to, so `releaseObjectGroup` frees the tree a client walked rather
  than only its root. A group name is a client's own vocabulary, so a release is scoped to the attachment:
  two clients both use `"console"`.
- **Detaching runs on a transport thread, and may.** Dropping a reference and a binding subscription both
  run no engine code, so a detach is answered rather than queued behind whatever the engine is busy with.

**Describing a value runs none of that value's code.** Type, subtype, class name, description and preview
all come from `Jint.Diagnostics.ValueInspector`, the engine's own getter-free, trap-free describer: an
accessor is reported rather than called, a proxy named by its kind, a CLR value named rather than read.
`getProperties` keeps the same promise by reading descriptors — so a proxy answers *no* properties, because
`ownKeys` and `getOwnPropertyDescriptor` are script, and a CLR property arrives as an accessor descriptor.

**`returnByValue` is the deliberate exception, and it runs script.** It is `Jint.Native.Json.JsonSerializer`
— `JSON.stringify`'s own contract, `toJSON` hooks and getters both — because a client that asked for the
value itself asked for exactly that, and V8 does the same. A cycle, a `toJSON` that threw and a value with
no JSON form are all `-32000 "Object couldn't be returned by value"`. There is one other place: rendering a
*thrown* error reads a `stack` a script may have defined as an accessor, which is why that render is asked
for under the engine's own `ResultLimits`. Everything else on the path is getter-free.

**`Domains/RemoteObjectDescriber.cs` is the seam `Jint.Browser` fills in.** It is consulted first for every
non-primitive value and may answer a subtype, a class name and a description — `subtype: "node"`,
`description: "div#id.cls"` — and it is held to the same promise: a describer that reads a script-visible
accessor breaks the one invariant a client relies on while paused. It hangs off `EngineTargetOptions` and is
internal, because the type publishes the protocol's own vocabulary and no third-party describer yet
justifies making that a public commitment.

Two gaps are real rather than pending. `[[PromiseResult]]` is answered as a *description* with no handle,
because the engine publishes a settled promise's value to nothing outside its own assembly. And a host
object's members are listed without their values, for the reason above.

### What a client hears without asking

Three things reach a domain from *inside* the engine, on the engine thread, with no command to answer: a
`console` call, an exception that escaped the pump, and a promise rejected with nothing to handle it.
`ITargetObserver` is the one interface for all three and `EngineTarget` fans out to whoever is attached.
They go out through `DevToolsDomain.EmitDetached`, which queues rather than writes, so nothing blocks the
engine and no transport failure erupts out of the host's own pump.

**The console arrives through the sink, and the sink wraps rather than replaces.** `UseDevTools` installs a
`DevToolsConsoleSink` around whatever `Options.WebApi.Console.Sink` the host had and forwards both overloads
first, so a host keeps every line it was getting. **One sink speaks for one engine**: the engine reads its
sink out of `Options` on every emit and `ConsoleRecord` carries no engine, so a sink shared by two could not
tell which was talking. It binds to the first target over an engine and refuses a second engine's. An engine
without `WebApiFeatures.Console` has no `console` to log through and reports nothing.

**`ConsoleJournal` is the last hundred calls**, replayed on `Runtime.enable` and `Console.enable` as V8
replays its own store. The bound is a memory bound first: an entry holds its arguments strongly, exactly as
a handle does, so evicting one releases every handle any attachment minted for it. Arguments are billed to
the group `"console"`, which a client's "clear console" releases; `Runtime.discardConsoleEntries` and
`Console.clearMessages` empty the journal, which is the target's because the history is the engine's.

**`Console` is the flat half, `Log` the failure half.** `Console.messageAdded` carries the finished line the
engine's printer produced and no handles, so a call that printed nothing — `groupEnd`, a `time` that started
a timer — is absent there and present in `Runtime.consoleAPICalled`, which a front end draws a group from.
`Log.entryAdded` carries what failed, and **`Log.enable` replays nothing**: the journal is the console's.

**A rejection is reported earlier than V8 reports it.** `Tasks.PromiseRejectionTracker` raises `Reject` the
moment a promise is rejected unhandled, where V8 waits for the end of the microtask checkpoint — so a
rejection handled on the next line produces `exceptionThrown` then `exceptionRevoked` where Chrome produces
neither, which is the pair the revocation exists for. The identifier is remembered per attachment (the last
64) rather than the event delayed, which would mean this package deciding when a checkpoint ended.

**`EngineTarget.ReportUncaughtException` is the host's own door**, called by the `LibraryOwned` loop for
script that escaped `ProcessTasks` and by a `HostOwned` host from its own `catch`. Reporting is not
handling: it writes to whoever is attached and returns.

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

### The protocol pin, and regenerating

`tools/devtools-protocol/` holds `js_protocol.json` and `browser_protocol.json` verbatim from
[ChromeDevTools/devtools-protocol](https://github.com/ChromeDevTools/devtools-protocol) at the commit
`pin.json` names, under the 3-Clause BSD licence beside them. `tools/devtools-protocol/README.md` is the
file to read before touching any of it; two rules from it matter enough to repeat:

- **A bump is a code change, not a pin change.** Upstream renames methods, moves them between domains and
  turns optional parameters into required ones. Fetch, regenerate, *read the diff of
  `Jint.DevTools/Protocol/Generated/`* — that diff is the upstream change in the vocabulary this repository
  compiles — and fix what it broke, in the same pull request.
- **The generated output is checked in and diffed by a test.** `GeneratedProtocolIsCurrentTests` runs the
  emitter in memory and compares byte for byte, so editing the manifest without regenerating fails the
  build, and so does hand-editing a `.g.cs`.

```bash
dotnet run --project tools/devtools-protocol/Jint.DevTools.ProtocolGenerator -c Release -- \
    --protocol tools/devtools-protocol \
    --manifest tools/devtools-protocol/manifest.json \
    --output Jint.DevTools/Protocol/Generated
```

A Roslyn source generator was rejected: the `System.Text.Json` context has to be generated *over* the data
transfer objects and generators do not chain, `Jint.SourceGenerators` is `netstandard2.0` without
`System.Text.Json`, and a protocol surface is the kind of thing whose diff a reviewer wants to read.

Four things about the emitted code are decisions rather than accidents:

- **A CDP `enum` becomes `string` plus a `<Type>Values` class of `const string`s**, never a C# enum.
  `JsonStringEnumMemberName` is .NET 9+ and `net8.0` has to compile; string constants are also what a
  protocol that gains a value in a bump degrades into gracefully.
- **Everything is `global::`-qualified.** The data transfer objects live in `Jint.DevTools.Protocol.Runtime`,
  `.Console`, `.Debugger` and so on, where an unqualified `Console` or `Runtime` binds to the namespace.
- **Every serializable type is declared in `ProtocolJsonContext` with a domain-qualified
  `TypeInfoPropertyName`**, arrays included. Without the arrays, the serialization generator names a
  transitively discovered type after the element's *short* name — `CallFrameArray` for both
  `Runtime.CallFrame[]` and `Debugger.CallFrame[]` — and refuses to generate the second one (`SYSLIB1031`).
- **A `$ref` into a domain the manifest does not generate must resolve to a primitive alias**, or the
  generator fails rather than emit something that will not compile. Today `Network.RequestId` and
  `Page.FrameId` are both `string`.

### The manifest

`tools/devtools-protocol/manifest.json` is the boundary between what the package *describes* and what it
*answers*, and it is load-bearing at run time rather than documentation:

- `generatedDomains` — the domains that get data transfer objects, a `<Domain>DomainBase` and a
  `<Domain>Events` factory. Every command gets a `protected virtual`, so the surface is discoverable.
- `implementedMethods` — the commands that get a `case` in the generated dispatch. **Everything else is
  `-32601` and never a silent success**, and — the part that is easy to get wrong — it is `-32601` *before
  the parameters are looked at*: an unimplemented command is not in Chrome's dispatch table at all, so
  answering `-32602` would tell a client it called a command wrongly that does not exist here.
- `implementedEvents` — the events the package emits, checked against the protocol the same way.
- `reportedDomains` — what `Schema.getDomains` answers. **The generator refuses an entry with no
  implemented command**, so a client feature-detecting through it is never told about a domain that answers
  nothing.

`ProtocolManifestTests` holds the manifest and the code to each other in both directions: every implemented
method is overridden on a registered domain, and nothing else is. The workflow for a new command is exactly
three steps — add the manifest entry, regenerate, override the virtual — and skipping any of them fails.

Registration lives in one place, `Domains/BuiltInDomains`, which is what those tests read. Two lists,
because there are two kinds of session — a browser conversation answers about the server, an attachment
about one engine — and a domain registered on one and checked against the other verifies nothing.

### The envelope and the error codes

`Protocol/ProtocolMessage.cs` reads `{ id, method, params?, sessionId? }` and writes the three outgoing
shapes. Two properties are contracts:

- **Exactly one reply per message, whatever went wrong.** A client waiting on an `id` that never comes back
  is a hang rather than an error, so every path out of `DevToolsSession.HandleMessageAsync` writes
  something — including the last-resort `catch`, which answers `-32000` with the exception's message rather
  than letting a domain's bug erupt into the host.
- **A failure with no readable `id` is an error *notification*, not a response.** That is what Chrome does,
  and a response carrying an `id` the client never sent is worse than no response at all.

The codes and their wording come from Chromium's `crdtp/dispatch.cc`, which every client was written
against: `-32700` parse error, `-32600` invalid request, `-32601` method not found, `-32602` invalid
parameters, `-32603` internal error, `-32000` server error. One message is load-bearing —
`'<Domain.method>' wasn't found` — because clients feature-detect a domain by sending one of its commands;
it is pinned verbatim by test and must not be reworded.

`Throw.*` raises all of them, in the shape `Jint/Runtime/Throw.cs` established, and `-32602` is decided in
one place: `ProtocolPayload.Read`, the only point where a client's JSON becomes a CLR object.

### Citing the protocol

Cite the section a member implements, the way the rest of the repository cites TC39 and WHATWG:

```
https://chromedevtools.github.io/devtools-protocol/tot/<Domain>/                the domain itself
https://chromedevtools.github.io/devtools-protocol/tot/<Domain>/#method-<name>  also #event- and #type-
```

`ProtocolCitationTests` resolves every one against the **vendored** JSON — offline, no register, no network,
because the authoritative document is already in the tree. Deliberately *not*
`Jint.Tests/SpecCitationTests.cs`, which covers `tc39.es` anchors through a register refreshed from the
living documents; do not extend that one to reach the network for these.

The protocol's own `description` becomes the `<summary>` of everything emitted: first sentence, whitespace
collapsed, XML-escaped, and replaced by a generic sentence past the 25-word cap in
[`docs/xml-doc-style.md`](../docs/xml-doc-style.md). Generated summaries keep the protocol's wording rather
than being rewritten to start with `Gets`; that rule governs the *public* surface, and none of this is.

### Visibility, and what is public

**The public surface is the host's door and nothing else**: `DevToolsServer`, `DevToolsServerOptions`,
`EngineTarget`, `EngineTargetOptions`, `ThreadMode`, and the `UseDevTools` extension with its
`DevToolsEngineOptions`. Everything else — every protocol type, every dispatch base, every session class —
is `internal`, and the baseline in `Jint.Tests.DevTools/Verify/` is the diff somebody reads when that
changes; see
[`Jint.Tests.PublicInterface/AGENTS.md`](../Jint.Tests.PublicInterface/AGENTS.md#the-public-api-baselines)
for how one is accepted.

**None of that surface carries `[Experimental]`, and that is a decision.** `JINTDT001` says *the shape of
this member follows a living upstream document rather than this repository's compatibility contract*, which
is true of a generated data transfer object and false of a server, a target and an options bag: those keep
Jint's own contract, with a `docs/v5-migration.md` row when they change. The first member that really does
publish a protocol shape is the one that carries the attribute; putting it on the entry point would make
every host write a `#pragma` to use the package at all. It is separate from Jint's `JINT0001` because the
two say different things.

**There is deliberately no `InternalsVisibleTo` grant from `Jint` to this package.** It consumes the
published engine API and nothing else, which is the point: a protocol server that could only be written
from inside the engine assembly is one no third party could have written. Every seam it turns out to need
is a seam the engine should expose — as public API with a row in
[`docs/v5-migration.md`](../docs/v5-migration.md), on the terms
[`Jint/AGENTS.md`](../Jint/AGENTS.md#what-counts-as-a-public-contract) sets. `Jint.DevTools` does grant
`InternalsVisibleTo` to `Jint.Tests.DevTools` and `Jint.Browser`.

### What is deliberately absent

Not oversights, and not to be added without a decision:

- **Async call stacks and blackboxing.** `setAsyncCallStackDepth`, `setBlackboxPatterns` and
  `setBlackboxedRanges` are answered as no-ops — every recorded client sends one while connecting, so
  refusing would fail an ordinary connection. The engine retains no stack across a promise reaction, and a
  filter that silently skipped frames would make a step mean something other than what `DebugHandler` did.
- **Pausing on exceptions.** `setPauseOnExceptions` stores the state a client set and stops on nothing: the
  engine reports every throw and says nothing about whether a `catch` is waiting, so `uncaught` cannot be
  honoured and `all` would stop inside every library that throws internally.
- **Source maps.** The protocol carries `sourceMapURL` and the front end resolves it; nothing here reads
  one.
- **`HeapProfiler`.** There is no object graph to walk that would mean anything to a .NET host; memory is
  `engine.Diagnostics` and the constraints, not this protocol.
- **Per-session debugging.** Breakpoints and the step mode live on the engine's `DebugHandler`, so a second
  session's `Debugger.enable` is refused with `-32000` rather than silently sharing the first one's. Making
  them per-session means a filter on every pause.
- **`Runtime.globalLexicalScopeNames`.** The realm's global declarative record publishes a binding *count*
  and no names, so answering means either an engine seam nobody has asked for or an empty list that tells a
  client there are no `let`s when there are.
- **`Runtime.queryObjects`.** It enumerates a heap by prototype; the heap is the CLR's.
- **`Runtime.getExceptionDetails`.** Nothing retains an exception's details past the command that reported
  them, and an identifier resolving to nothing is worse than no command.
- **`Runtime.terminateExecution`.** Execution is bounded by `Options.Constraints`, the host's decision; a
  client that could stop a host's script at will is a different security posture.
- **`throwOnSideEffect`.** The console's eager evaluation asks for "throw rather than run anything
  observable", which needs a side-effect analysis of the interpreter. It is refused with `-32000` rather
  than answered by running the very code the client asked not to be run; a front end shows no preview.
- **The evaluation parameters this package does not act on.** `timeout`, `silent`, `userGesture`,
  `disableBreaks`, `replMode`, `includeCommandLineAPI`, `allowUnsafeEvalBlockedByCSP`, `uniqueContextId`
  and `serializationOptions` are accepted and ignored rather than refused: each is a client asking for
  *more*, and a `-32601` would fail an ordinary evaluation on a target that can perfectly well answer it.
  Bounding one evaluation is `Options.Constraints`, which is the host's decision rather than a client's.
  `Debugger.continueToLocation`'s `targetCallFrames`, `stepInto`'s `breakOnAsyncCall` and `skipList`, and
  `getPossibleBreakpoints`' `restrictToFunction` are ignored on the same terms.
- **The full protocol description at `/json/protocol`.** It answers what this server implements, derived
  from the manifest: the pinned document is two megabytes of mostly `-32601`, and a client reading it would
  be told it can call all of them.
- **HTTP beyond the `/json` documents.** `Transport/HttpRequestHead.cs` is not an HTTP server and must not
  become one: no bodies, no chunked encoding, no keep-alive.

### Test strategy

`Jint.Tests.DevTools` is NUnit plus AwesomeAssertions on `net8.0;net10.0`, and it is signed because
everything it tests is `internal`.

- **Protocol tests assert text, not objects.** A client library matches on `id`, `error.code` and — for a
  method it is feature-detecting — on `error.message`, so those are what a test asserts; one that called a
  domain method directly would pass with the envelope broken. `Session/ProtocolSession.cs` is the helper.
- **The generator has two tests of its own**: the currency diff, and that two runs produce the same bytes —
  which makes the diff mean "the inputs changed" rather than "the emitter is nondeterministic".
- **An extension point nothing ships yet is exercised by a domain the suite declares itself.**
  `DomainLifecycleTests` derives from a generated base as a real domain would; an untested extension point
  is a design nobody has tried.
- **The socket tests are not a duplicate of the in-process ones.** The envelope is the same code either
  way; the upgrade handshake, the frame handling, the single writer, the close ordering and *whether the
  read loop keeps reading while a command is outstanding* are exercised by nothing without a socket, and
  that is where a protocol server goes wrong. `Transport/DevToolsClient.cs` is the one helper, and **every
  wait in it is bounded** — a protocol test that can hang is a CI leg that can hang.
- **`Clients/PuppeteerSharpTests.cs` is the only test that can claim client compatibility.** Everything
  else asserts what this server answers; that one asserts a library nobody here wrote is satisfied by it.
  It launches no browser — `ConnectAsync` speaks to an endpoint that already exists.
- **`Protocol/HandshakeReplayTests.cs` replays the recordings** in
  `tools/devtools-protocol/handshakes/`: every method a real client was seen sending is sent here, a
  manifest method must be answered, and every other must be *exactly* `-32601`. Its `Absent` table names
  each unimplemented method and why, so the tolerance stays a decision — a re-recording or a client release
  arrives as a diff somebody reads rather than as a silently widened test.
- **What is not here yet**: the manual checklist for attaching
  `devtools://devtools/bundled/js_app.html?v8only=true&ws=`, which is how the front end's own behaviour is
  claimed and which nothing automated replaces.
