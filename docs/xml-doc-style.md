# XML documentation style

Every declaration of Jint's public API surface carries a `<summary>`. A test says which ones do not,
and this document says what a good one looks like.

- The gate is `Jint.Tests.PublicInterface/PublicApiDocumentationTest.cs`.
- The debt it has not been paid down to zero yet is
  `Jint.Tests.PublicInterface/UndocumentedPublicApi.txt`, which may only ever shrink.
- The surface it measures is the approved API baseline,
  `Jint.Tests.PublicInterface/Verify/PublicApiTest_<tfm>.verified.txt`. Not `CS1591`: that warning
  counts the public members of *internal* types too — 1,688 of them on `net10.0` — and no embedder
  reads those.

The audience is an embedder reading IntelliSense, not a maintainer reading the file. Everything
below follows from that one sentence.

## The rules

**`<summary>` is one sentence, at most 25 words, ending in a period.** It says what the member *is*
or *does*. Never why it exists, never what it used to be, never who asked for it.

**`<para>` never appears inside `<summary>`.** A summary is one sentence, so there is nothing to
paragraph. Something that needs two paragraphs is `<remarks>`.

**A property's summary starts `Gets`, `Gets or sets`, or — for a `bool` — `Whether`.** A method's
starts with a third-person verb: `Registers`, `Converts`, `Returns`, `Throws`.

**A default is one clause in the summary**, not a paragraph of its own:
`Defaults to <see langword="true"/>.`

**`<remarks>` holds only what a caller must do or avoid.** One rule per `<para>`, at most four lines
each, **at most four paragraphs**. Past that the material is not caller guidance any more — it is
design notes, and those belong in the co-located `AGENTS.md` for the area, with a `<see href>` back
to it, or in a `//` comment beside the code it explains.

**No history, no benchmark narrative, no rationale-about-the-author.** "The engine's older probe sat
in the call expression", "the benchmark gate measured 1.5–3% slower", "it is stated rather than left
to the implementation" — none of that helps somebody calling the method, and the numbers go stale
where nobody looks.

**`<param>` is a noun phrase.** "The global property name." Not "Pass the name of the global here."

**`<example>` only where the signature does not imply the call**, and at most 8 lines of `<code>`.
A loop shape, a two-call protocol, a factory whose lifetime is not obvious. Not `Evaluate(string)`.

**`<inheritdoc/>` counts as documented.** The declaration it inherits from is in the same surface and
is held to these rules in its own right, so writing the summary twice is worse than pointing at it.

**A doc comment on a `partial` type goes on a part every target framework compiles.** Jint ships five
assemblies; a comment inside `#if NET8_0_OR_GREATER` is invisible to the three that resolve the
`netstandard` and `net472` assets. `PublicApiDocumentationTest.NoTargetFrameworkIsDocumentedLessThanTheNewest`
checks this, and found two.

## Three rewrites

Real members, picked by measuring the longest doc comments in the tree. All three are in this
repository as the *after* column.

### 1. A method: `OptionsExtensions.AddLazyGlobal`

110 lines of XML doc for one method — the longest in `Jint/` — with six `<remarks>` paragraphs, one
of which opened a `<para>` and then nested three more inside it before closing. That nesting is
well-formed XML, so nothing in the build noticed; it is invalid in every renderer downstream,
because a paragraph cannot contain a paragraph.

**Before** (abridged; the omitted paragraphs are more of the same):

```xml
<summary>
Registers a global whose value is produced the first time script reads it, instead of when the
engine is created. Hosts that install a large fixed set of globals — of which a given script
typically touches a handful — pay only for the ones actually used.
</summary>
<remarks>
<para>
<b>Sharing an <see cref="Options"/> instance is supported, not required.</b> Because the factory
receives only the <see cref="Engine"/>, a host whose values depend on per-request or per-scope state
(a scoped <c>IServiceProvider</c>, a workflow context) cannot express that through a process-wide
<see cref="Options"/>. Constructing a fresh <see cref="Options"/> per scope or per evaluation and
letting the factories close over that scope is a supported and cheap pattern — an
<see cref="Options"/> object is a plain configuration record, and the caches that make repeated
engine construction cheap (resolved CLR members on the <see cref="TypeResolver"/>, delegate
metadata, compiled invokers) are keyed process-wide rather than on the <see cref="Options"/>
instance. Share one instance when the configuration is genuinely global; build one per scope when
it is not.
</para>
<para>
<b>A restore re-arms an unread global, and this is a contract.</b> ...
<para>                                            <!-- opened inside the paragraph above -->
<b>What the second run produces is the factory's business, not the restore's.</b> ...
</para>
<para>
The failure this rules out is silent, which is why it is stated rather than left to the
implementation: were the descriptor reinstated with its materialized value intact, a reused engine
would serve the next request a value closed over the previous one's state, with nothing to observe
but the wrong answer.
</para>
</para>                                           <!-- three paragraphs later -->
</remarks>
```

**After** — 22-word summary, four paragraphs of at most four lines, the example down to 8 lines:

```xml
<summary>
Registers a global whose value <paramref name="valueFactory"/> produces on the first read, rather
than when the engine is created.
</summary>
<remarks>
<para>
The property itself is installed eagerly, so <c>in</c>, <c>hasOwnProperty</c> and
<c>Object.keys(globalThis)</c> see it without materializing anything. Only the value waits, and
<c>typeof</c> counts as a read of it.
</para>
<para>
The factory runs once per engine, receiving the engine being built, so one <see cref="Options"/>
may be shared by any number of engines. It must not return <see langword="null"/>;
<see cref="JsValue.Undefined"/> is stored if it does.
</para>
<para>
Deleting the global before any read skips the factory. Overwriting or redefining it before any read
still runs the factory once and discards the result, because <c>[[DefineOwnProperty]]</c> reads the
current value before replacing it.
</para>
<para>
<see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> re-arms a global whose factory had not
run when the snapshot was taken, so it runs again — a host pooling engines depends on that. Whether
the second run produces a fresh value is the factory's business: one that hands back something it
holds gives the next cycle exactly what the previous one mutated.
</para>
</remarks>
```

What went: the "sharing an `Options` instance" essay (the surviving clause is the one a caller acts
on — one `Options`, any number of engines), the paragraph explaining why the restore contract is
stated at all, and the paragraph restating it from the other side.

### 2. A property: `Engine.AdvancedOperations.TimeUntilNextScheduledWork`

83 lines: five `<remarks>` paragraphs, a `<list>` inside one of them, and two `<code>` blocks.

**Before** (abridged):

```xml
<summary>
How long this engine may be left alone before <see cref="ProcessTasks"/> has something to run, or
<see langword="null"/> when it has nothing scheduled at all.
</summary>
<remarks>
<para>
<b>The canonical host loop is this property plus <see cref="ProcessTasks"/>, and there is
deliberately no third method that drains for a budget.</b> Jint never starts a thread to run script:
a <c>setTimeout</c> callback, a <c>scheduler.postTask</c> task, a settled <c>Atomics.waitAsync</c>
all run on whichever thread calls into the engine, and nowhere else. What a host driving its own
loop was missing is not another way to pump but the answer to <i>when</i> to pump, which is this.
</para>
<para>
The three answers:
<list type="bullet"> ... three <item><description> blocks ... </list>
</para>
... two more paragraphs, then a 15-line <example> and a second <code> block ...
</remarks>
```

**After** — `Gets`, four paragraphs, one 8-line example:

```xml
<summary>
Gets how long this engine may be left alone before <see cref="ProcessTasks"/> has work to run, or
<see langword="null"/> when nothing is scheduled.
</summary>
<remarks>
<para>
<see cref="TimeSpan.Zero"/> means there is work to run now — call <see cref="ProcessTasks"/>. A
positive value is how long until the earliest <em>timed</em> work comes due: a <c>setTimeout</c>,
an <c>AbortSignal.timeout()</c>, a delayed <c>scheduler.postTask</c>, an <c>Atomics.waitAsync</c>
deadline.
</para>
<para>
Do not treat <see langword="null"/> as "nothing will happen". A CLR task a script awaits through
interop, and an <c>Atomics.waitAsync</c> another agent notifies, both simply enqueue and have no
due time to report, so a host loop needs a cadence of its own.
</para>
<para>
The answer is a snapshot taken on the calling thread and may be stale the instant it is returned,
so do not sleep on it indefinitely. Use <see cref="WaitForScheduledWork(TimeSpan, CancellationToken)"/>
instead when there is no frame to keep — it also wakes on a job arriving from another thread.
</para>
<para>
Reading it is allocation-free, so a per-frame read costs nothing.
</para>
</remarks>
```

The `<list>` of "three answers" became the first paragraph, because a bullet per return value of a
`TimeSpan?` is three bullets saying zero, positive, and null. The second `<code>` block went: it
demonstrated a *different* member, and that member's own documentation is where it belongs.

### 3. A `bool` property: `Options.ConstraintOptions.StackOverflowGuard`

The summary was already the right shape — `Whether …`, `Defaults to <see langword="true"/>.` — and
only needed trimming to the 25-word cap. The `<remarks>` were the offender: six paragraphs, of which
one was a benchmark result and two were history.

**Before** (the two paragraphs that went, verbatim):

```xml
<para>
It measures the stack rather than counting calls, so it covers every route into a function body
rather than only a call expression: <c>new</c>, a getter or setter, a <c>valueOf</c>/<c>toString</c>
coercion, a Proxy trap, a callback a built-in invokes, and a host delegate that re-enters the
engine. Eighteen distinct routes reproduce the overflow, and the engine's older probe sat in the
call expression, so it saw one of them. Measuring also means the depth follows the stack of the
thread the engine runs on, instead of a frame count the host had to guess.
</para>
<para>
It is enabled by default because the alternative on an unbounded recursion is termination of the
host process. The probe is not free: the benchmark gate measured the recursion rows (<c>Fib</c>,
<c>DeepSum</c>, <c>Tak</c>) roughly 1.5–3% slower with it on, while hot shallow calls stayed within
run-to-run noise. A host whose scripts are all trusted and independently bounded can explicitly
set this property to <see langword="false"/> to recover that cost. A host sandboxing untrusted
input generally cannot, and a couple of percent on deep recursion is the price of staying alive.
</para>
```

**After** — the same two rules, without the archaeology or the number:

```xml
<summary>
Whether every entry into an interpreted function probes the remaining native stack and throws a
catchable <c>RangeError</c> when it runs low. Defaults to <see langword="true"/>.
</summary>
...
<para>
It measures the stack rather than counting calls, so it covers every route into a function body:
<c>new</c>, an accessor, a <c>valueOf</c>/<c>toString</c> coercion, a Proxy trap, a callback a
built-in invokes, and a host delegate that re-enters the engine.
</para>
...
<para>
It is a backstop, not a policy: where <see cref="MaxRecursionDepth"/> is also set that limit fires
first, and <see cref="MaxExecutionStackCount"/> takes precedence over this flag. Set it to
<see langword="false"/> to recover the probe's cost when every script is trusted and independently
bounded. Read once, while the engine is being constructed.
</para>
```

"Eighteen distinct routes reproduce the overflow" is a fact about a pull request, not about the
property. "1.5–3% slower" is a measurement of a machine that no longer exists; what the caller needs
is that the probe costs something on deep recursion and can be turned off for trusted script.

## What this is worth today

Measured on `net10.0` at the commit that added the gate.

| | |
| --- | ---: |
| Declarations in the approved public surface | 2,149 |
| …of which a person writes a `<summary>` for (the rest are overrides) | 1,919 |
| …of those, documented | 1,199 |
| **…of those, undocumented** | **720** |
| Summaries containing a `<para>` (public surface) | 35 |
| Summaries containing a `<para>` (whole assembly) | 452 |
| Documentation comments nesting a `<para>` | 0 (was 1) |

The 720, by namespace — this is the table the documentation wave works down:

| Namespace | Types | Methods | Properties | Fields | Total |
| --- | ---: | ---: | ---: | ---: | ---: |
| `Jint` | 24 | 92 | 25 | 68 | **209** |
| `Jint.Native` | 16 | 62 | 24 | 7 | **109** |
| `Jint.Runtime` | 13 | 22 | 40 | 16 | **91** |
| `Jint.Runtime.Modules` | 8 | 30 | 12 | 6 | **56** |
| `Jint.Runtime.Debugger` | 11 | 11 | 10 | 8 | **40** |
| `Jint.Runtime.Descriptors` | 3 | 8 | 10 | 17 | **38** |
| `Jint.Native.Intl` | | 30 | | | **30** |
| `Jint.Native.TypedArray` | 12 | 13 | | | **25** |
| `Jint.Runtime.Interop` | 2 | 13 | 5 | 1 | **21** |
| `Jint.Native.Object` | 2 | 12 | 3 | 1 | **18** |
| `Jint.Native.Symbol` | 1 | | | 15 | **16** |
| `Jint.Native.Array` | 2 | 9 | 3 | | **14** |
| `Jint.Native.Temporal` | | 1 | 12 | | **13** |
| `Jint.WebApi` | | 8 | | | **8** |
| `Jint.Native.Function` | 3 | | 2 | 2 | **7** |
| `Jint.Constraints` | 2 | 2 | | | **4** |
| `Jint.Native.Json` | 1 | 3 | | | **4** |
| `Jint.Native.ShadowRealm` | | 4 | | | **4** |
| `Jint.Native.Error` | 2 | 1 | | | **3** |
| `Jint.Native.RegExp` | 1 | 2 | | | **3** |
| `Jint.Native.Map` | 1 | 1 | | | **2** |
| `Jint.Native.Set` | 1 | 1 | | | **2** |
| `Jint.NodeCompat` | | 2 | | | **2** |
| `Jint.Native.Global` | 1 | | | | **1** |
| **total** | **106** | **327** | **146** | **141** | **720** |

Only the last row of the first table is enforced today. The `<para>`-in-`<summary>` counts are
measurements, not a gate: the wave removes them area by area, and a rule with 35 open violations
would need an allowlist of its own to be worth turning on.

## Working the allowlist down

Document an area, then regenerate and read the diff:

```bash
JINT_PUBLIC_API_DOCS=update dotnet test -c Release \
    Jint.Tests.PublicInterface/Jint.Tests.PublicInterface.csproj -f net10.0
```

The count on the first line of `UndocumentedPublicApi.txt` goes down by exactly as many lines as
were removed, which is the whole point of it being there. Three things fail the gate, and all three
are fixed the same way — review, then regenerate:

- a public declaration shipped with no summary that the file does not name;
- a declaration the file names that is now documented, so the file has to shrink;
- a declaration the file names that is no longer in the public surface at all.

Adding a line is the one edit the file does not accept. A new public declaration ships documented.

## What no summary is demanded of

Metadata holds 2,404 public and protected declarations. The approved baseline writes down 2,149 of
them — it already omits the compiler-synthesized and delegate members below — and of those 2,149 the
gate skips the 230 overrides, leaving 1,919. Each category is skipped because another declaration in
the same surface carries the documentation an IDE actually shows:

- **compiler-synthesized record members** — `Equals`, `GetHashCode`, `ToString`, `op_Equality`,
  `op_Inequality`, `PrintMembers`, `<Clone>$`, `EqualityContract`, the copy constructor, and a
  positional `Deconstruct`. 207 of them;
- **delegate members** — `Invoke`, `BeginInvoke`, `EndInvoke` and the constructor. The `delegate`
  declaration carries the summary and the `<param>` tags. 48 of them;
- **overrides** — 230 of them. The declaration being overridden is in the surface and is held to the
  rule; an IDE falls back to it;
- **explicit interface implementations** — 0 of them, because the compiler emits one as `private` and
  the surface is public and protected members only. The enumerator names the case anyway, so that a
  future change to the visibility filter does not silently start demanding a summary of a member
  nothing can call without a cast to the interface — whose declaration is in the surface already.

Everything else is in: types, constructors, fields, enum members, properties, indexers, events,
methods and user-written operators. An *implicitly* declared constructor is in too, and the only way
to document one is to declare it — which is a real improvement, not a workaround. See
`Jint/Constraints/OperationDeadlineConstraint.cs` for one done that way.
