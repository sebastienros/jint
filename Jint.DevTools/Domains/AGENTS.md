# Agent instructions: the protocol domains

> **Read this when:** You are implementing or changing a command, an event or a domain under
> `Jint.DevTools/Domains/`.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first, then [`Jint.DevTools/AGENTS.md`](../AGENTS.md) beside it —
> the thread rule, the mailbox, the pause loop, the manifest, the envelope and the error codes all live there,
> and nothing below repeats them.

A domain is where the protocol's vocabulary meets the engine's, and every rule here is one of two kinds: what
a client is promised about a value it can no longer see, and what the server is forbidden to run while
answering. The rest is the machinery in the file next door.

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

### The `Absent` table, and how a command leaves it

`Jint.Tests.DevTools/Protocol/HandshakeReplayTests.cs` replays the recordings in
`tools/devtools-protocol/handshakes/` — every method a real client was seen sending — and holds two properties:
a manifest method is answered, and every other answers *exactly* `-32601`. The second one needs a tolerance,
and `Absent` is it: a dictionary naming each recorded method this server does not implement and why.

Its three reasons are not interchangeable, and picking the wrong one is how a decision turns into a backlog
item nobody tracks:

- **`page`** — the method belongs to a target that has a document. It is `Jint.Browser`'s, which is AngleSharp
  plus Jint, and would be wrong to answer here whatever the implementation state: an engine target has no page
  to answer about.
- **`later`** — engine-level, and simply not written yet. This is the only reason that is debt, and the table
  is where that debt is visible.
- **`none`** — Chrome itself answered `-32601` in the very recording, so a client that sends it already handles
  not getting it.

**Implementing a command retires its entry, and the test says so in both directions.** A method that reaches
`implementedMethods` while still listed is a failure naming it stale; a recorded method that answers `-32601`
and is *not* listed is a failure too. So the workflow for a new command is the manifest entry, the
regeneration, the override — and then deleting the row that said it was missing.
