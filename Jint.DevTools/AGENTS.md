# Agent instructions: the Chrome DevTools Protocol server

> **Read this when:** You are touching anything under `Jint.DevTools/`, the vendored protocol under
> `tools/devtools-protocol/`, or an engine seam this package consumes.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

`Jint.DevTools` speaks the Chrome DevTools Protocol to any embedder's engine. It is the *engine-level*
half — sessions, `Runtime`, `Debugger`, `Profiler`, `Console`, `Log`, `Schema`, `Target`, `Browser` — and
it works for a host that has no pages at all: an Orchard or Elsa host can attach Chrome DevTools to its
scripts. The page-level domains belong to `Jint.Browser`, which is AngleSharp plus Jint and is never
described as a DOM stack of its own.

The package is `net8.0;net10.0`, the same floor `Jint/WebApi/` sits on and for the same reason: it is host
surface built on the modern BCL, not engine surface a `netstandard` consumer resolves. Unlike the web APIs
there is **no `#if` anywhere in it**, and a test says so — `PublicApiTest.NoSourceFileIsGatedByTargetFramework`
— because that is what lets one public-API baseline cover both assets.

### The thread rule

**A `JsValue` never leaves the engine thread. A transport thread only ever moves strings.**

That is the whole reason `IDevToolsConnection` is as narrow as it is: `SendAsync(string)`, a
`Func<string, CancellationToken, ValueTask>` for received messages, and a closed callback. Whatever thread a
WebSocket receive loop happens to be on, the only thing it may hand a `DevToolsSession` is text.

Everything downstream of that follows:

- **Every domain method runs on the engine thread.** A `DevToolsDomain` may hold engine state — a
  `RemoteObjectTable`, a `ScriptRegistry`, a `DebugHandler` subscription — and none of it is thread-safe.
- **Nothing a command returns may outlive the command.** A `CommandContext` is valid for the command that
  received it and is not to be captured.
- **Serialization happens on the engine thread too**, because that is where the `JsValue` is. What crosses
  to the transport is the finished JSON string, which is why `ProtocolEvent` carries
  `ParametersJson` rather than an object: the type is still known at the point the domain builds it, and
  serializing later would mean reflecting over a type the session does not know.
- **`Engine` is not thread-safe and sharing a `JsValue` across engines is unsupported.** Both are stated in
  the repository-root [`AGENTS.md`](../AGENTS.md#gotchas); this package must never be the thing that breaks
  them.

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
  attaches reactions and completes from the job that runs them, which is V8's shape too.
- **The drain must never throw.** It is a job on the host's own pump; an exception there erupts out of
  `ProcessTasks` into the host. Every item catches everything and answers with it.
- **A command that times out is answered, not cancelled.** `CommandTimeout` bounds the *client's* wait; the
  item stays queued and still runs when the engine is next pumped. The two timeout messages are told apart
  deliberately — an item nothing dequeued says `Engine is not being pumped`, one that started and did not
  finish says `Command timed out` — because a host debugging the wrong one wastes an afternoon.
- **Host work and protocol commands share the queue**, so a host's `target.Post` runs in order with the
  commands around it. The single exception is `WaitForDebuggerOnStart`: host work is held and protocol
  commands are not, because otherwise the command that ends the wait could never be answered.

The two thread modes are `EngineTargetOptions.ThreadMode`. `HostOwned` (the default) is the host's own loop;
`EngineTarget.Pump` is a convenience over `engine.Tasks.ProcessTasks()`, not a second mechanism.
`LibraryOwned` starts one thread running drain → `ProcessTasks` → `WaitForScheduledWork`, and the host
submits work with `Post`/`PostAsync`. A host-owned target that has to wait for a debugger waits by
*pumping* (`WaitForDebugger`), because the command that releases it is answered on that very thread.

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
- **`setAutoAttach` on an attached session is a success that attaches nothing.** Clients walk down the
  target tree by sending it on every session they are handed; an engine target has no children, and a
  refusal there reads to a client as a broken target.
- **A browser context is not something an engine target has.** `getBrowserContexts` answers an empty list;
  `createBrowserContext` and `disposeBrowserContext` answer `-32000` with the reason. Minting an identifier
  that partitions nothing would tell a client its next target was isolated when it is not. The page package
  is where contexts start meaning something.

### RemoteObject lifetime

A client cannot hold a `JsValue`, so it holds an `objectId` and the server holds the value for it. That
promise — **the value will still be there when you come back** — is the whole of `Domains/RemoteObjectTable.cs`,
and everything else about handles follows from it:

- **The table is on the `EngineTarget`, ownership is per attachment.** The values in it belong to the engine,
  so two sessions attached to one engine address the same value by the same identifier; but each entry
  remembers which attachment registered it, so detaching releases that attachment's handles and nobody else's.
- **Strong, never weak, and never deduplicated.** A fresh identifier per wrap, which is V8's behaviour and
  what stops a client's release of one handle from invalidating another it still holds.
- **Four endings**: `releaseObject`, `releaseObjectGroup`, the attachment detaching, and the target being
  disposed. Nothing expires on its own, and a client that leaks handles leaks engine values — which is the
  cost of the promise rather than a defect in it.
- **A group is inherited on the way in.** `getProperties` and `callFunctionOn` bill the handles they mint to
  the group the receiver already belongs to, so `releaseObjectGroup` frees the tree a client walked rather
  than only the root it started from. A group name is a client's own vocabulary, so the release is scoped to
  the attachment: two clients both use `"console"`.
- **Detaching runs on a transport thread, and may.** Dropping a reference and dropping a binding
  subscription both run no engine code, so a detach is answered rather than queued behind whatever the
  engine is busy with — a target still waiting for a debugger included.

**Describing a value runs none of that value's code.** Type, subtype, class name, one-line description and
preview all come from `Jint.Diagnostics.ValueInspector`, the engine's own getter-free, trap-free describer:
an accessor is reported rather than called, a proxy is named by its kind, a CLR value is named rather than
read. `getProperties` keeps the same promise by reading descriptors — so a proxy answers *no* properties,
because `ownKeys` and `getOwnPropertyDescriptor` are script, and a CLR property arrives as an accessor
descriptor and is listed rather than read.

**`returnByValue` is the deliberate exception, and it runs script.** It is `Jint.Native.Json.JsonSerializer`
— `JSON.stringify`'s own contract, `toJSON` hooks and getters both — because a client that asked for the
value itself asked for exactly that, and V8 does the same. A cycle, a `toJSON` that threw, and a value with
no JSON form are all `-32000 "Object couldn't be returned by value"` with the engine's own message as the
data. Everything else on the path is getter-free; this one call is not.

**`Domains/RemoteObjectDescriber.cs` is the seam `Jint.Browser` fills in.** It is consulted first for every
non-primitive value and may answer a subtype, a class name and a description — `subtype: "node"`,
`description: "div#id.cls"` — and it is held to the same promise: a describer that reads a script-visible
accessor breaks the one invariant a client relies on while paused. It hangs off `EngineTargetOptions` and is
internal, because the type publishes the protocol's own vocabulary and there is no third-party describer yet
to justify making that a public commitment.

Two gaps are real rather than pending. `[[PromiseResult]]` is answered as a *description* with no handle,
because the engine publishes a settled promise's value to nothing outside its own assembly —
`Runtime.awaitPromise` is what hands the value over, and it does so by attaching reactions. And a host
object's members are listed without their values, for the reason above.

### The pause loop

When the debugger pauses, the engine thread is *inside* `DebugHandler`'s synchronous `Break`/`Step`
handler. It cannot return — returning is what resumes the script — and it is the only thread allowed to
touch the engine. So protocol traffic during a pause is serviced **from inside that handler**: a nested
message loop that drains the dispatcher's mailbox, answers `Debugger.evaluateOnCallFrame`,
`Runtime.getProperties`, `Debugger.resume` and the rest, and returns a step decision when the client asks
for one. That is V8's `runMessageLoopOnPause`/`quitMessageLoopOnPause` shape, and it is the one piece of
this package that cannot be written as ordinary asynchronous code.

Three consequences, all of which arrive with the pause loop (campaign P4) and none of which may be designed
away before then:

- A client that **disconnects mid-pause** must resume, or the host thread is wedged forever.
- Commands that do not touch the paused engine still have to be answered while paused, or a client that
  serializes on one of them deadlocks.
- Nothing inside the pause loop may `await` back onto a different thread and then touch the engine.

### The protocol pin, and regenerating

`tools/devtools-protocol/` holds `js_protocol.json` and `browser_protocol.json` verbatim from
[ChromeDevTools/devtools-protocol](https://github.com/ChromeDevTools/devtools-protocol) at the commit
`pin.json` names, under the 3-Clause BSD licence beside them. `tools/devtools-protocol/README.md` is the
file to read before touching any of it; two rules from it matter enough to repeat:

- **A bump is a code change, not a pin change.** Upstream renames methods, moves them between domains and
  turns optional parameters into required ones. Fetch, regenerate, *read the diff of
  `Jint.DevTools/Protocol/Generated/`* — that diff is the upstream change in the vocabulary this repository
  compiles — and fix what it broke, in the same pull request. The same discipline
  [`Jint.Tests.Test262/AGENTS.md`](../Jint.Tests.Test262/AGENTS.md) applies to `SuiteGitSha`.
- **The generated output is checked in and diffed by a test.** `GeneratedProtocolIsCurrentTests` runs the
  emitter in memory and compares byte for byte, so editing the manifest without regenerating fails the
  build, and so does hand-editing a `.g.cs`.

```bash
dotnet run --project tools/devtools-protocol/Jint.DevTools.ProtocolGenerator -c Release -- \
    --protocol tools/devtools-protocol \
    --manifest tools/devtools-protocol/manifest.json \
    --output Jint.DevTools/Protocol/Generated
```

A Roslyn source generator was considered and rejected: the `System.Text.Json` context has to be generated
*over* the data transfer objects and generators do not chain, `Jint.SourceGenerators` is `netstandard2.0`
without `System.Text.Json`, and a protocol surface is exactly the kind of thing whose diff a reviewer wants
to read.

Four things about the emitted code are decisions rather than accidents:

- **A CDP `enum` becomes `string` plus a `<Type>Values` class of `const string`s**, never a C# enum.
  `JsonStringEnumMemberName` is .NET 9+ and `net8.0` has to compile; string constants are also what a
  protocol that gains a value in a bump degrades into gracefully.
- **Everything is `global::`-qualified.** The data transfer objects live in `Jint.DevTools.Protocol.Runtime`,
  `.Console`, `.Debugger` and so on, and an unqualified `Console` or `Runtime` inside one of those would
  bind to the namespace.
- **Every serializable type is declared in `ProtocolJsonContext` with a domain-qualified
  `TypeInfoPropertyName`**, arrays included. Without the arrays, the serialization generator names a
  transitively discovered type after the element's *short* name — `CallFrameArray` for both
  `Runtime.CallFrame[]` and `Debugger.CallFrame[]` — and refuses to generate the second one (`SYSLIB1031`).
- **A `$ref` into a domain the manifest does not generate must resolve to a primitive alias**, or the
  generator fails rather than emitting something that will not compile. Today `Network.RequestId` and
  `Page.FrameId` are both `string`, so nothing outside the generated set is needed.

### The manifest

`tools/devtools-protocol/manifest.json` is the boundary between what the package *describes* and what it
*answers*, and it is load-bearing at run time rather than documentation:

- `generatedDomains` — the domains that get data transfer objects, a `<Domain>DomainBase` and a
  `<Domain>Events` factory. Every command of a generated domain gets a `protected virtual`, so the whole
  surface is discoverable in the IDE.
- `implementedMethods` — the commands that get a `case` in the generated dispatch. **Everything else is
  `-32601` and never a silent success**, and — this is the part that is easy to get wrong — it is `-32601`
  *before the parameters are looked at*. A command the server does not implement is not in Chrome's
  dispatch table at all, so its payload is never read; answering `-32602` there tells a client "you called
  it wrongly" about a command that does not exist here.
- `implementedEvents` — the events the package emits, checked against the protocol the same way.
- `reportedDomains` — what `Schema.getDomains` answers. **The generator refuses an entry with no
  implemented command**, so a client feature-detecting through it is never told about a domain that answers
  nothing.

`ProtocolManifestTests` holds the manifest and the code to each other in both directions: every implemented
method is overridden on a registered domain, and nothing else is. So the workflow for a new command is
exactly three steps — add the manifest entry, regenerate, override the virtual — and skipping any of them
fails.

Registration lives in one place, `Domains/BuiltInDomains`, which is what those tests read. There are two
lists there rather than one because there are two kinds of session — a browser conversation answers about
the server, an attachment answers about one engine — and `ProtocolManifestTests` reads both. A domain
registered on only one of them and checked against only the other is a manifest entry nothing verifies.

### The envelope and the error codes

`Protocol/ProtocolMessage.cs` reads `{ id, method, params?, sessionId? }` and writes the three outgoing
shapes. Two properties are contracts:

- **Exactly one reply per message, whatever went wrong.** A client waiting on an `id` that never comes back
  is a hang rather than an error, so every path out of `DevToolsSession.HandleMessageAsync` writes
  something — including the last-resort `catch`, which answers `-32000` with the exception's message rather
  than letting a domain's bug erupt into the host.
- **A failure with no readable `id` is an error *notification*, not a response.** That is what Chrome does,
  and a response carrying an `id` the client never sent is worse than no response at all.

The codes and their wording come from Chromium's `crdtp/dispatch.cc`, which is what every client was
written against: `-32700` parse error, `-32600` invalid request, `-32601` method not found, `-32602`
invalid parameters, `-32603` internal error, `-32000` server error. Only one message is load-bearing —
`'<Domain.method>' wasn't found` — because clients feature-detect a domain by sending one of its commands
and reading what comes back; it is pinned verbatim by test and must not be reworded.

`Throw.*` raises all of them, in the shape `Jint/Runtime/Throw.cs` established, and `-32602` is decided in
exactly one place: `ProtocolPayload.Read`, which is the only point where a client's JSON becomes a CLR
object.

### Citing the protocol

Cite the section a member implements, the way the rest of the repository cites TC39 and WHATWG:

```
https://chromedevtools.github.io/devtools-protocol/tot/<Domain>/            the domain
https://chromedevtools.github.io/devtools-protocol/tot/<Domain>/#method-<name>
https://chromedevtools.github.io/devtools-protocol/tot/<Domain>/#event-<name>
https://chromedevtools.github.io/devtools-protocol/tot/<Domain>/#type-<Name>
```

`ProtocolCitationTests` resolves every one of them against the **vendored** JSON — offline, no register, no
network, because the authoritative document is already in the tree. This is deliberately *not*
`Jint.Tests/SpecCitationTests.cs`, which covers `tc39.es` anchors through a checked-in register refreshed
by fetching the living documents; do not extend that one to reach the network for these.

The protocol's own `description` becomes the `<summary>` of everything emitted: first sentence, whitespace
collapsed, XML-escaped, and replaced by a generic one sentence when it runs past the 25-word cap in
[`docs/xml-doc-style.md`](../docs/xml-doc-style.md). Generated member summaries keep the protocol's wording
rather than being rewritten to start with `Gets`; that rule governs the *public* API surface, and none of
this is public.

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
is true of a generated data transfer object and false of a server, a target and an options bag: those are
Jint's own shapes and keep Jint's own contract, with a `docs/v5-migration.md` row when they change. The
first member that really does publish a protocol shape is the one that carries the attribute — putting it
on the entry point instead would make every host write a `#pragma` to use the package at all, and would
leave the identifier meaning nothing in particular. It is a separate identifier from Jint's `JINT0001`
because those two also say different things, and a host suppressing one has decided nothing about the other.

**There is deliberately no `InternalsVisibleTo` grant from `Jint` to this package.** It consumes the
published engine API and nothing else, which is the whole point: a protocol server that could only be
written from inside the engine assembly is one no third party could have written. Every seam this package
turns out to need is a seam the engine should expose — as a public API with a row in
[`docs/v5-migration.md`](../docs/v5-migration.md), on the terms
[`Jint/AGENTS.md`](../Jint/AGENTS.md#what-counts-as-a-public-contract) sets. `Jint.DevTools` does grant
`InternalsVisibleTo` to `Jint.Tests.DevTools` and `Jint.Browser`.

### What is deliberately absent

Not oversights, and not to be added without a decision:

- **Async call stacks.** `Debugger.setAsyncCallStackDepth` is answered as a no-op. The engine does not
  retain a stack across a promise reaction, and synthesizing one that is wrong is worse than none.
- **Source maps.** The protocol carries `sourceMapURL` and the front end resolves it; nothing here fetches
  or parses one.
- **Blackboxing.** `setBlackboxPatterns` is a no-op. Stepping is the engine's, and a filter that silently
  skips frames would make a step mean something different from what `DebugHandler` did.
- **`HeapProfiler`.** There is no object graph to walk that would mean anything to a .NET host; memory
  questions are `engine.Diagnostics` and the constraints, not this protocol.
- **Per-session breakpoint state.** Breakpoints live on the engine's `DebugHandler`, so two sessions
  attached to one engine share them. Making them per-session means a filter on every pause.
- **`Runtime.globalLexicalScopeNames`.** The realm's global declarative record publishes a binding *count*
  and no names, so answering would mean either an engine seam this package has not asked for or an empty
  list that tells a client there are no `let`s when there are.
- **`Runtime.queryObjects`.** It enumerates a heap by prototype, and the heap is the CLR's.
- **`Runtime.getExceptionDetails`.** Nothing retains an exception's details past the command that reported
  them, and an identifier that resolves to nothing is worse than no command.
- **`Runtime.terminateExecution`.** Execution is bounded by `Options.Constraints`, which is the host's
  decision; a client that could stop a host's script at will is a different security posture and would be a
  deliberate one.
- **`throwOnSideEffect`.** The console's eager evaluation asks for "throw rather than run anything
  observable", which needs a side-effect analysis of the interpreter. It is refused with `-32000` rather
  than answered by running the very code the client asked not to be run; no recorded client sends it, and a
  front end that gets the refusal simply shows no preview.
- **The evaluation parameters this package does not act on.** `timeout`, `silent`, `userGesture`,
  `disableBreaks`, `replMode`, `includeCommandLineAPI`, `allowUnsafeEvalBlockedByCSP`, `uniqueContextId`
  and `serializationOptions` are accepted and ignored rather than refused: each of them is a client asking
  for *more*, and a `-32601` would make an ordinary evaluation fail on a target that can perfectly well
  answer it. Bounding one evaluation is `Options.Constraints`, which is the host's decision rather than a
  client's.
- **The full protocol description at `/json/protocol`.** It answers what this server implements, derived
  from the manifest. The pinned document is two megabytes of mostly commands answered here with `-32601`,
  and a client reading it would be told it can call them.
- **HTTP beyond the handful of `/json` documents.** `Transport/HttpRequestHead.cs` is not an HTTP server and
  must not become one: no bodies, no chunked encoding, no keep-alive.

### Test strategy

`Jint.Tests.DevTools` is NUnit plus AwesomeAssertions on `net8.0;net10.0`, and it is signed because
everything it tests is `internal`.

- **Protocol tests assert text, not objects.** A client library matches on `id`, `error.code` and — for a
  method it is feature-detecting — on `error.message`, so those are what a test asserts. A test that called
  a domain method directly would pass with the envelope broken. `Session/ProtocolSession.cs` is the one
  helper: send a message, read the single reply.
- **The generator has two tests of its own**: the currency diff, and that two runs produce the same bytes —
  which is what makes the diff mean "the inputs changed" rather than "the emitter is nondeterministic".
- **An extension point nothing ships yet is exercised by a domain the suite declares itself.**
  `DomainLifecycleTests` derives from a generated base exactly as a real domain would; an untested
  extension point is a design nobody has tried.
- **The socket tests are not a duplicate of the in-process ones.** The envelope and the state machine are
  the same code either way; the upgrade handshake, the frame handling, the single writer and the close
  ordering are exercised by nothing without a socket, and that is where a protocol server goes wrong.
  `Transport/DevToolsClient.cs` is the one helper, and **every wait in it is bounded** — a protocol test
  that can hang is a CI leg that can hang, and the thing most likely to be wrong here is the thing that
  makes a reply never arrive.
- **`Clients/PuppeteerSharpTests.cs` is the only test that can claim client compatibility.** Everything
  else asserts what this server answers; that one asserts that a library nobody here wrote is satisfied by
  it. It launches no browser — `ConnectAsync` speaks to an endpoint that already exists — and it is pinned
  to the version the recorded handshake came from.
- **`Protocol/HandshakeReplayTests.cs` replays the recordings** in
  `tools/devtools-protocol/handshakes/`: every method a real client was seen sending is sent here, a
  manifest method must be answered, and every other must be *exactly* `-32601`. Its `Absent` table names
  each unimplemented method and why, so the tolerance stays a decision — a re-recording or a client release
  then arrives as a diff somebody reads rather than as a silently widened test.
- **What is not here yet**: the manual checklist for attaching
  `devtools://devtools/bundled/js_app.html?v8only=true&ws=`, which is how the front end's own behaviour is
  claimed and which no automated test replaces.
