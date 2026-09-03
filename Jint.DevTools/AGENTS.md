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

`Domains/` carries its own instruction file. [`Domains/AGENTS.md`](Domains/AGENTS.md) is what a domain
*answers* — remote-object handles and the promise they keep, the console journal, the script registry and
breakpoints, profiles and coverage, and the `Absent` table a newly implemented command leaves. Read it before
adding or changing any command; this file is the machinery underneath it and does not repeat it.

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

### Sessions, targets and the pause loop have a file of their own

How a command crosses from the transport to the engine (the mailbox), what a target is and how a session
attaches to one, why a target outlives its engine and what a swap tells each domain, and the message loop
that runs while the engine is paused are [`Session/AGENTS.md`](Session/AGENTS.md). The one rule to carry
across without opening it is the thread rule above: a `JsValue` never leaves the engine thread, and a
transport thread only ever moves strings.

### The protocol pin, and regenerating

`tools/devtools-protocol/` holds `js_protocol.json` and `browser_protocol.json` verbatim from
[ChromeDevTools/devtools-protocol](https://github.com/ChromeDevTools/devtools-protocol) at the commit
`pin.json` names, under the 3-Clause BSD licence beside them. `tools/devtools-protocol/README.md` is the
file to read before touching any of it; two rules from it matter enough to repeat:

- **A bump is a code change, not a pin change.** Upstream renames methods, moves them between domains and
  turns optional parameters into required ones. Fetch, regenerate, *read the diff of
  `Jint.DevTools/Protocol/Generated/`* — that diff is the upstream change in the vocabulary this repository
  compiles — and fix what it broke, in the same pull request.
- **A third file beside the two vendored ones is ours.** `jint_protocol.json` describes the `Jint` domain
  in the protocol's own format, is read alike, and is cited against itself rather than against Chrome.
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
- **A named type declared as an object with no properties is a map**, not an empty record:
  `Network.Headers` resolves to `Dictionary<string, string>` and no type is emitted for it. An *inline*
  `"type": "object"` member is a different thing and stays a `JsonElement` — see the map-type section of
  [`tools/devtools-protocol/README.md`](../tools/devtools-protocol/README.md) for why the two differ.

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
- **Editing a paused program.** `setScriptSource` and `restartFrame` both re-enter a frame the engine is
  standing in; the interpreter runs an abstract syntax tree it has already walked into, and there is no way
  to swap one out under a live call stack. `setReturnValue` is refused for the same shape of reason and a
  smaller one: `CallFrame.ReturnValue` is read-only, so there is nothing to write through.
- **`getStackTrace`.** It resolves a `StackTraceId`, which only exists where asynchronous stacks do.
- **`setBreakpointOnFunctionCall`.** A breakpoint here is a position, matched at an execution point the
  engine reaches; a function object is not one, and the engine has no per-call hook to hang it off.
- **Source maps.** The protocol carries `sourceMapURL` and the front end resolves it; nothing here reads
  one.
- **`console.profile`, and the two `Profiler` events it drives.** The engine implements neither
  `console.profile` nor `console.profileEnd` — `ConsoleMethod` has no member for either — so
  `consoleProfileStarted` and `consoleProfileFinished` would be events nothing could ever raise. Adding them
  starts in `Jint/WebApi/Console/`, not here.
- **`HeapProfiler`.** There is no object graph to walk that would mean anything to a .NET host; memory is
  `engine.Diagnostics` and the constraints, not this protocol.
- **Per-session debugging.** Breakpoints and the step mode live on the engine's `DebugHandler`, so a second
  session's `Debugger.enable` is refused with `-32000` rather than silently sharing the first one's. Making
  them per-session means a filter on every pause.
- **`Runtime.queryObjects`.** It enumerates a heap by prototype; the heap is the CLR's.
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
  `tools/devtools-protocol/handshakes/`: every method a real client was seen sending is sent here, a manifest
  method must be answered, and every other must be *exactly* `-32601`. Its `Absent` table is what keeps that
  tolerance a decision, and
  [`Domains/AGENTS.md`](Domains/AGENTS.md#the-absent-table-and-how-a-command-leaves-it) says what an entry
  means and how one is retired.
- **The front end is claimed by driving it, not by asserting against it.**
  [`docs/manual-checklist.md`](docs/manual-checklist.md) is the walk — `chrome://inspect` → Configure → the
  port, then what each panel should show and what is expected to be empty — and
  [`tools/devtools-frontend-smoke/`](../tools/devtools-frontend-smoke/README.md) automates most of it against a real
  Chrome and a real Jint process. That tool downloads a browser and reaches the network, so it is **not in
  CI**; run it when the protocol surface changes, and run the checklist by hand for the steps it reports as
  not driven.
- **The published binary is a claim too.** `Jint.AotExample` references this package without rooting it and
  probes it over a real socket — attach, evaluate, break, resume — so the `aot` leg's own run is what says
  Native AOT works here. That leg additionally fails if any `IL2xxx`/`IL3xxx` diagnostic is attributed to a
  file in this package: everything it serializes goes through a source-generated `System.Text.Json` context,
  and the first reflective one would show up there rather than in a test.
