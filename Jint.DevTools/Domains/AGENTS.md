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

### Scripts, breakpoints, and why one pause happened

`Domains/ScriptRegistry.cs` is every program one engine has parsed, and three of its properties are load
bearing rather than incidental:

- **Keyed on the `Program` by reference**, so a cached `Prepared<Script>` run a thousand times is one script
  and one `scriptParsed`. It fills from `DebugHandler.BeforeEvaluate`, which the *target* subscribes when it
  is made rather than when a client enables the domain, so an attachment is replayed what the engine already
  ran.
- **Bounded at `MaxScripts`**, oldest first, because a registry that never forgets holds every abstract
  syntax tree a host ever evaluated. A dropped script's identifier stops resolving and its source stops being
  fetchable; run again, it is announced under a new one.
- **A location is matched back to a script by *name*, not by identity.** A call frame carries a
  `SourceLocation` and not the program it came from, so `ScriptRegistry.At` takes the scripts under that
  source name and picks the one whose range contains the position, newest first. Several programs parsed
  under one name — every `engine.Execute(code)` with no source argument is `<anonymous>` — therefore share
  one answer, and a location nothing claims is reported against the identifier `0`, which is Chrome's own
  sentinel for a location it cannot attribute. This is the one place a frame can name the wrong script, and
  closing it needs an engine seam that puts the program on the frame.

A breakpoint is a `DevToolsBreakPoint : BreakPoint` — the engine's class is unsealed exactly so that
`DebugInformation.BreakPoint` hands the instance straight back and `hitBreakpoints` can name it. Two
consequences: the engine keeps **one breakpoint per position**, so two protocol breakpoints that resolve to
the same line collapse onto the last one set; and a `continueToLocation` breakpoint is one-shot, taken away
inside the pause it caused so that a client stepping on does not meet it again.

**`Debugger.paused` reports a reason, and there are two.** A breakpoint, a `debugger` statement and a step
are all `other` — the protocol's enum has no member for a `debugger` statement, V8 answers `other` for one
too, and `hitBreakpoints` is what tells a breakpoint apart. The other is `exception`.

### Pausing on exceptions

`DebugHandler.PauseOnExceptions` stops the engine **at the throw**, before anything unwinds, and raises it
through `Break` with `PauseType.Exception`. The domain reads `DebugInformation.ThrownValue` and
`IsUncaught` there. Four things about the mapping are decisions:

- **`caught` is not an engine mode.** `ExceptionPauseMode` is `None`, `Uncaught` or `All`, so the client's
  `caught` asks the engine for `All` and the pause decision drops the uncaught half. Inverting it — asking
  for `Uncaught` and keeping what it did not raise — cannot work, because the engine does not raise a pause
  at all for a throw it was not asked about.
- **Filtering one out cancels a step in flight**, because the delegate's return value *is* the next step mode
  and there is no way to say "unchanged". It is tolerable in this one case and nowhere else: the throw being
  filtered is an uncaught one, and the frames a step was walking are about to be unwound by it anyway.
- **`data` is the thrown value's `RemoteObject` with `uncaught` written onto it**, which is V8's shape
  exactly. The front end reads both halves — the object to render, and the flag to choose its wording — and
  the handle is billed to the backtrace group, so it dies with the frames. `hitBreakpoints` is an empty
  array rather than absent: a client reading it unconditionally is reading the truth, which is that a throw
  stopped the engine and no breakpoint did.
- **The mode is the attachment's.** It reaches the engine on `enable` and on the command, and goes back to
  `None` on disable or detach — otherwise a client that walked away leaves a host's engine stopping on every
  throw with nobody to answer the pause.

Two divergences from Chrome come from the engine and are not this package's to fix:

- **A throw crossing an async function's body boundary is *uncaught*, whatever the caller wrapped it in.**
  The throw becomes a rejection of that function's promise, which is a different thing from an exception, so
  the count of executing `catch` clauses resets there. A user asking to stop on uncaught exceptions means
  this; a client comparing against Chrome will see it as a divergence.
- **`Promise.reject(value)` never stops the engine**, because nothing was thrown. `Runtime.exceptionThrown`
  still reports it, from the rejection tracker, and is unaffected by any of this.

### Evaluating in a frame

`Debugger.evaluateOnCallFrame` runs in **any** frame of the current pause, through
`DebugHandler.Evaluate(text, frame)`: the frame's own scope chain is what the expression resolves against, so
a binding the innermost frame shadows is read — and written — as that frame sees it.

A `callFrameId` is `"<pauseSerial>.<frameIndex>"`, minted while the `paused` event is built and parsed back
against the serial of the pause that is running. **That check is the whole of the identifier's meaning**: the
engine stamps its own generation on a frame and refuses one from an execution point it has left, so a client
acting on a `paused` event it has already resumed from is told `Invalid call frame id` — Chrome's wording —
rather than answered about a different frame that happens to sit at the same index.

`Debugger.setVariableValue` writes a binding of any frame too, and is the only way to change a paused
engine's state other than an assignment in an evaluation: the scope objects a client expands are read-only
snapshots, so a value changed after one was handed out is not reflected in it.

### Profiles and coverage

The `Profiler` domain answers two questions with two instruments, and neither is the other's fallback.

**A profile comes through `IProfileSource`, and the shipped source is the *exact* profiler.** The engine has
two: `Engine.Diagnostics.StartProfiling`, which records every call at the call boundary, and the sampling
profiler of [#3608](https://github.com/sebastienros/jint/pull/3608), which notes what the stack looks like at
the engine's own check points. Only the first is usable from here, and the reason is a visibility one rather
than a design one: `SampledProfile` keeps its sample, frame, stack and function tables as `internal` fields
and publishes a Firefox Profiler document as its only output, so consuming it would mean serializing to JSON
and parsing it back. **Making those tables public is an engine change**, and when it lands the sampler becomes
a second `IProfileSource` rather than an edit to the domain — which is why the seam exists before there is a
second implementation of it. The seam speaks a function table and a balanced stream of enters and leaves,
deliberately not the engine's own `ScriptProfileFrame`: a seam that names one profiler's type is a seam only
that profiler fits through.

`ProfileBuilder` turns that stream into the protocol's document, and three things about it are decisions:

- **One sample per interval, not per tick.** The stream says exactly when each call happened, so the top of
  the stack is known for every instant between two activations, and one weighted sample of that node is what
  the panel wants. The deltas therefore *add up to* the recording rather than approximating it — the only
  loss is each interval's truncation to whole microseconds.
- **A node is a call position, not a function.** One function reached from two places is two nodes, which is
  what lets the panel say where the time went rather than only in what.
- **`(root)` and `(program)`, and nothing else synthetic.** `(program)` takes the time no script function was
  on the stack. `(idle)` is never emitted, because an engine target has no idle state to report: a host that
  is not running script is not idle, it is doing something this package cannot see. `(garbage collector)`
  never, because the heap is the CLR's.

**There is no console-driven half.** `console.profile` and `console.profileEnd` are not implemented by the
engine at all — `ConsoleMethod` has no member for either, and the properties are not installed on `console`
— so `consoleProfileStarted` and `consoleProfileFinished` are events nothing could raise. Adding them starts
with the engine.

**Coverage inverts a set.** `Engine.Diagnostics.GetCoverage` reports what *ran*: a construct with no entry
never executed, so a report built from it alone would say every function it mentions was used and say nothing
about the rest — which is the opposite of what a Coverage panel is for. The gap is closed from the abstract
syntax tree the script registry already holds: every function the program declares gets a range, and the ones
with no entry get `count: 0`. Two things stay approximate and are the domain's to state rather than to fix: a
statement inside a function that ran but did not itself run is covered by its function's count, so unused code
is reported at function granularity whatever `detailed` asks for; and a function's name is the syntax's, with
the same inference the language makes for `Function.prototype.name` when there is none.

**Taking coverage resets it, and that costs the host.** The protocol says `startPreciseCoverage` and
`takePreciseCoverage` both reset execution counters — which is what makes successive takes incremental — and
the counters are the engine's one set, so a host reading `GetCoverage` for its own purposes loses its numbers
to an attached client. That is the protocol's contract rather than a defect here, and it is the reason
coverage is off unless `UseDevTools` is asked for it. `getBestEffortCoverage` is the read that takes nothing
away.

**Both halves refuse an engine that was not built for them, by name.** Profiling needs
`Options.Profiling.Enabled`, which `UseDevTools` always sets; coverage needs `Options.Coverage.Enabled`,
which it sets only when asked. Both are construction-time, so the refusal names the option rather than
answering an empty report that reads as a script which never ran.

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
