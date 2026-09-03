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

- **The table is on the `TargetRuntime`, ownership is per attachment.** The values belong to the engine, so
  two sessions address the same value by the same identifier; each entry remembers which attachment
  registered it, so detaching releases that attachment's handles and nobody else's. It is on the runtime and
  not the target because a navigation replaces the engine and every value in it: a client that comes back
  with a handle from the document before is told the object cannot be found, which is what Chrome tells it.
- **Strong, never weak, never deduplicated.** A fresh identifier per wrap, which is V8's behaviour and what
  stops a client's release of one handle invalidating another it still holds.
- **Five endings**: `releaseObject`, `releaseObjectGroup`, the attachment detaching, the target being
  closed, and the engine being replaced under it. Nothing expires on its own, and a client that leaks handles
  leaks engine values — the cost of the promise rather than a defect in it.
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

**A function's `description` is its source text, and the label belongs to nothing.** The front end reads that
field as `Function.prototype.toString` output and parses the name back out of it, so `ƒ computeTotal()` made
every function in a Scope pane render as **`ƒ undefined()`**. It comes from
`ValueInspectorOptions.FunctionSourceText`, which is the engine computing what `toString` would answer
*without* running any of it — no host `FunctionToStringHandler`, no coercion of a `name` a script replaced
with an accessor. A `PropertyPreview` of a function carries `value: ""` and not the label either: that is
what Chrome sends, recorded rather than assumed, and the front end draws the `ƒ` from the type. There is no
place left that sends `ƒ name()`; the console's own printer is `ConsoleFormatter`, and it is not this.

**`returnByValue` is the deliberate exception, and it runs script.** It is `Jint.Native.Json.JsonSerializer`
— `JSON.stringify`'s own contract, `toJSON` hooks and getters both — because a client that asked for the
value itself asked for exactly that, and V8 does the same. A cycle, a `toJSON` that threw and a value with
no JSON form are all `-32000 "Object couldn't be returned by value"`. There is one other place: rendering a
*thrown* error reads a `stack` a script may have defined as an accessor, which is why that render is asked
for under the engine's own `ResultLimits`. Everything else on the path is getter-free.

**A node's `RemoteObject` is what that seam was opened for, and `Jint.Browser` fills it.**
`DomRemoteObjectDescriber` answers `subtype: "node"`, the DOM interface as the class name
(`HTMLDivElement`, `Text`, `HTMLDocument` — the same string `Object.prototype.toString` reports) and Chrome's
one-line description: `div#id.one.two` for an element, and the node name for everything else (`#text`,
`#comment`, `#document`). It reads the node's own name and its `id` and `class` content attributes off
AngleSharp's tree and nothing else, so it keeps the promise below. **The subtype is not decoration**: a
client library builds an element handle out of it and a plain object handle without it, so `$('button')`
returns something that cannot be clicked long before the click reaches `Input.dispatchMouseEvent`.

**`Domains/RemoteObjectDescriber.cs` is the seam `Jint.Browser` fills in.** It is consulted first for every
non-primitive value and may answer a subtype, a class name and a description — `subtype: "node"`,
`description: "div#id.cls"` — and it is held to the same promise: a describer that reads a script-visible
accessor breaks the one invariant a client relies on while paused. It hangs off `EngineTargetOptions` and is
internal, because the type publishes the protocol's own vocabulary and no third-party describer yet
justifies making that a public commitment.

**An internal property is a slot, and every one sent is one the engine already holds.** `[[Prototype]]` and
a promise's `[[PromiseState]]`/`[[PromiseResult]]`, plus — for a function — `[[FunctionLocation]]`, and for a
bound one `[[TargetFunction]]`, `[[BoundThis]]` and `[[BoundArgs]]`. `[[FunctionLocation]]` is the one that
makes a function *clickable*: without it a front end names the function and opens nothing. It is the
declaration node the function already carries — `Function.FunctionDeclaration` — resolved to a script through
`Function.Program` and `ScriptRegistry.For`, by identity, so two parses of one text are told apart. A
function the engine has no declaration for carries no location at all, rather than one against the sentinel
identifier `0`; a function whose *program* the registry does not know — `eval`, the `Function` constructor, a
script evicted past `MaxScripts` — carries one against that sentinel, which is what Chrome sends for a
location it cannot attribute.
`[[BoundArgs]]` is a **copy** of the arguments array, because the engine's own is what every call through the
bound function reads from. `[[Scopes]]` is absent for the same reason `[[Handler]]` and `[[Target]]` of a
proxy are: the engine publishes neither the environment a closure captured nor a proxy's slots outside its own
assembly, and a *paused* frame's scope chain reaches a client through `Debugger.paused` instead.

Two gaps are real rather than pending. `[[PromiseResult]]` is answered as a *description* with no handle,
because the engine publishes a settled promise's value to nothing outside its own assembly. And a host
object's members are listed without their values, for the reason above.

### What a client hears without asking

Five things reach a domain from *inside* the engine, on the engine thread, with no command to answer: a
`console` call, an exception that escaped the pump, a promise rejected with nothing to handle it, the engine
being replaced under the target, and an isolated world being minted over it. `ITargetObserver` is the one
interface for all five and `DevToolsTarget` fans out to whoever is attached. They go out through
`DevToolsDomain.EmitDetached`, which queues rather than writes, so nothing blocks the engine and no transport
failure erupts out of the host's own pump.

**`RuntimeReplaced` is the seam a navigation reaches every domain through**, and each of the five answers it
differently. `Runtime` emits `executionContextsCleared` and then `executionContextCreated` for the new
default context — Chrome's order, and the handles are already gone with the table. `Debugger` moves its
`Break`/`Step` subscriptions onto the new engine, forgets where every request was *placed* (those named
positions of an engine that no longer exists) and keeps the requests themselves, so a breakpoint set by URL
is resolved again as the next document's scripts are parsed; the client's pause-on-exceptions state, its
skip-all-pauses flag and its asynchronous stack depth carry over. `Profiler` ends whatever was recording and
throws it away, because a profile over two engines answers a question nobody asked. `Console` and `Log` do
nothing: the journal is the runtime's and went with it.

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

**Every console call carries its call site, which is why the sink asks for one.** `ConsoleRecord.StackTrace`
used to be `console.trace`'s alone, and a message with no frames is a message a front end anchors nowhere —
V8 prints `app.js:44` on the right of every line. `DevToolsConsoleSink.WantsStackTrace` is `true`, which is
the engine seam that turns the capture on; the frames are only readable while the call is still on the stack,
so it is asked *before* the sink is reached or not at all, and every other host still pays nothing. Each
frame is matched back to a registered script through `ScriptRegistry.At`, so the anchor is clickable rather
than a URL the front end cannot open; a location no script claims reports the identifier `0`.

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

`Domains/ScriptRegistry.cs` is every program one engine has parsed — the *current* engine, since the
registry is the `TargetRuntime`'s and a navigation starts a fresh one — and three of its properties are load
bearing rather than incidental:

- **Keyed on the `Program` by reference**, so a cached `Prepared<Script>` run a thousand times is one script
  and one `scriptParsed`. It fills from `DebugHandler.BeforeEvaluate`, which the *target* subscribes when it
  is made rather than when a client enables the domain, so an attachment is replayed what the engine already
  ran.
- **Bounded at `MaxScripts`**, oldest first, because a registry that never forgets holds every abstract
  syntax tree a host ever evaluated. A dropped script's identifier stops resolving and its source stops being
  fetchable; run again, it is announced under a new one.
- **A running position is matched back to a script by identity, never by name.** `CallFrame.Program`, a
  profile frame's `Program` and `CoverageSource.Program` are the engine seam that carries it
  ([#3632](https://github.com/sebastienros/jint/issues/3632)), and `ScriptRegistry.For` is a lookup in the
  same by-reference map `scriptParsed` was minted from. Matching by name is what this replaced, and it must
  not come back where a program is in hand: every `engine.Execute(code)` with no source argument is
  `<anonymous>`, so a name answers for every sourceless script at once. A program the engine names none for —
  `eval`, the `Function` constructor, and a script evicted past `MaxScripts` — is reported against the
  identifier `0`, Chrome's own sentinel for a location it cannot attribute.
- **`ScriptRegistry.At` is the fallback, and only a *rendered* stack frame may use it.** A frame of
  `Error.stack` and a `ConsoleStackFrame` are text — a source name, a line and a column, and no program —
  so `Runtime.consoleAPICalled`'s `stackTrace` and the frames parsed out of a thrown error's `stack` match by
  name and range and cannot tell two sourceless scripts apart. They are the only two callers left:
  `[[FunctionLocation]]` was the third until `Function.Program`
  ([#3666](https://github.com/sebastienros/jint/issues/3666)) extended the seam #3632 opened for a frame to a
  function value, and it resolves through `For` now. Do not reach for `At` from anywhere that has a program.

**A source name becomes a URL here, and nowhere else.** The engine's source names stay exactly what the host
passed — a stack trace prints them, and `Options.Interop.BuildCallStackHandler` is handed them — so
`Domains/ScriptUrl.cs` is the protocol's own vocabulary over the top: a name that *is* an absolute filesystem
path (a drive letter, a UNC share, a leading slash) is published as a `file://` URL, and every other name is
unchanged. Without it Chrome's navigator files a script under "(no domain)" with its whole path for a name,
because a bare path has no origin. Two consequences: `ScriptRegistry.At` maps before it matches, since a
location carries the source name and a script is registered under the URL; and `setBreakpointByUrl` accepts
either form through `ScriptUrl.Same`, because a client sends back what it read off `scriptParsed` while a
host driving the protocol has only ever seen the name it passed. `EngineTargetOptions.Url` goes through the
same mapping, and so does the `sourceURL` a client hands `Runtime.compileScript`, so `/json/list`,
`scriptParsed` and a compile failure all name one location rather than three spellings of it. The shapes are recognized
without asking the operating system: a source name reaches an engine from wherever the host got it.

**`Runtime.getExceptionDetails` reconstructs, it does not remember.** The front end sends it once per error
object it renders, which is how it draws the expandable stack under a console message — so nothing retaining
an exception past the report that carried it is not the obstacle it looked like. `text` is the error's
`name: message` read as descriptors; the frames come from parsing its own `stack`, which is the one thing on
this path besides `returnByValue` and a thrown error's render that may run script, and is asked for under the
same `ResultLimits`. A `stack` in any other shape — a host's `BuildCallStackHandler`, a script's own string —
produces *no* `stackTrace` rather than a guessed one, and the rendered text is in `exception.description`
either way. A handle that is not an error is `-32000 "errorObjectId is not a JS error object"`, Chrome's
wording.

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

- **Each of the protocol's four states is one engine mode**, `caught` included since
  [#3631](https://github.com/sebastienros/jint/issues/3631), so this domain filters nothing: a throw that
  reaches the `Break` handler is one the client asked to stop on. **Deciding it here instead is what must not
  come back.** Declining a pause means returning a `StepMode`, and every one of those but
  `StepMode.Unchanged` *sets* the mode — so a filter here cancels a step the client had in flight. Where a
  future filter genuinely has no engine mode behind it, `StepMode.Unchanged` is the answer.
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

**A profile comes through `IProfileSource`, and both of the engine's profilers fit through it.**
`SampledProfileSource` notes what the stack looks like at the engine's own check points
([#3608](https://github.com/sebastienros/jint/pull/3608), readable since
[#3630](https://github.com/sebastienros/jint/issues/3630)); `EventedProfileSource` records every call at the
call boundary. The seam speaks a function table and a balanced stream of enters and leaves, deliberately not
either profiler's own type: a seam that names one instrument is a seam only that instrument fits through. A
sampler converts into that shape losslessly, because two consecutive samples differ by a suffix of the stack
and that difference is a run of leaves and enters.

**The sampler is what a recording uses, and the choice is made when it starts.** A CDP `Profile` is what
V8's sampling profiler produces and what `setSamplingInterval` sets the rate of, so it is the instrument a
front end is asking for; and unlike the exact one it costs the run nothing per call. There is one sampling
session per engine, though, and it may be the host's own — a client that arrives then is given the exact
profiler rather than a refusal. What the sampler cannot show is a call it never sampled: a function entered
and left between two check points is not in the profile at all.

`ProfileBuilder` turns that stream into the protocol's document, and three things about it are decisions:

- **One sample per interval, not per tick.** The stream says when each activation happened, so the top of the
  stack is known for every instant between two of them, and one weighted sample of that node is what the
  panel wants. The deltas therefore *add up to* the recording rather than approximating it, whichever
  instrument filled the stream in — the only loss is each interval's truncation to whole microseconds.
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

### What a request log promises

The `Network` and `Fetch` domains are `Jint.Browser`'s rather than this package's, but the promise they keep
is the same shape as the remote-object one above and is worth stating here, because the next domain that
reports something a client cannot see again will want it.

**A `requestId` is a promise that the request is still findable when the client comes back.** A client reads
one off `requestWillBeSent` and hands it to `getResponseBody` some time later, so the log has to hold what
it reported — and it holds it *bounded*, by count and by bytes, which is the same trade the console journal
makes. The three consequences are the ones an implementation gets wrong:

- **The bound is the host's, not the client's.** `Network.enable`'s `maxTotalBufferSize` is accepted and not
  honoured; `BrowserOptions.MaxCapturedResponseBytes` is what decides, because a client must not be able to
  ask a host's process for memory.
- **Nothing is copied until a client asks to be told.** The capture is armed by `Network.enable` and emptied
  by `disable`, so a page nobody is driving pays nothing — the same rule that keeps `Debugger`'s `Skip`
  subscription off an engine no session has enabled it on.
- **A body that is gone is `-32000 "No data found for resource with given identifier"`**, Chrome's own
  wording, and never an empty string: a client cannot tell an empty response from a forgotten one.

**A paused request is a promise about a thread, and it is the opposite of the pause loop's.** A debugger
pause holds the *engine* thread and services the client inline; a `Fetch` pause holds the one transport
thread its request is being sent on and the engine goes on running, which is what lets the very command that
releases it be answered at all. A design that moved the pause onto the engine thread would deadlock the case
the page cannot pump through, and `Jint.Browser/DevTools/FetchDomain.cs` names it.

**Reporting is not observing twice.** Both domains read one seam the page already has —
`Jint.Browser`'s request log, which is the engine's `FetchObserver` — so `Page.Requests` and the protocol
say the same thing about the same request and the two domains share identifiers. A second observer on the
transport would be a second truth.

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
