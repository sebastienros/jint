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

The **dispatcher** that brings a transport thread's message to the engine thread arrives with the WebSocket
transport (campaign P2): a mailbox the engine drains, in the two thread modes a host can be in —
`HostOwned`, where the host already pumps the engine and the dispatcher only enqueues, and `LibraryOwned`,
where the package owns a loop. Until it lands, a session runs on whichever thread pumped it, and
`InProcessConnection` is the only transport. **State the rule in code you write now**; do not write
something a dispatcher will have to undo.

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

Registration lives in one place, `Domains/BuiltInDomains.RegisterOn`, which is what those tests read.

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

**Everything in this package is `internal` today, and the public API baseline is empty on purpose.** The
first member promoted out of that is a diff somebody reads — see
[`Jint.Tests.PublicInterface/AGENTS.md`](../Jint.Tests.PublicInterface/AGENTS.md#the-public-api-baselines)
for how a baseline is accepted. When something is promoted it carries
`[Experimental(DevToolsDiagnosticIds.ProtocolExtensionPoint)]`, i.e. `JINTDT001`, which says its shape
follows a living upstream document rather than this repository's compatibility contract. That is a separate
identifier from Jint's `JINT0001` because the two say different things, and a host suppressing one has
decided nothing about the other.

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
- **Sockets, in the skeleton.** The WebSocket transport and `/json/*` discovery arrive with the session
  core; `InProcessConnection` is the whole transport until then.

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
- **What is not here yet**: the PuppeteerSharp end-to-end suite over a real WebSocket, and the manual
  checklist for attaching `devtools://devtools/bundled/js_app.html?ws=`. Both arrive with the session core,
  and both are how a claim about *client compatibility* is made — no in-process test can make one.
