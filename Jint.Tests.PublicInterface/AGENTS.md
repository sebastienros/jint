# Agent instructions: integrator-facing tests

> **Read this when:** You are writing a test that has to prove a third party can actually reach an API,
> **or a build here has failed on `PublicApiTest` or `PublicApiDocumentationTest`, or a run here ended
> without reporting a failing test at all.**
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### When the run dies instead of failing

A test host that stops talking is reported by the platform, not by the test framework: the run ends with
`The active test run was aborted`, no test named and no stack trace. That is not an exception anything
threw and not a failure the adapter observed — it is VSTest noticing that the pipe went quiet. Two
consequences, both of which cost time on sebastienros/jint#3308 before they were understood:

- **The sentence saying what happened is easily filtered out.** CI runs
  `--logger "console;verbosity=quiet"`, which drops informational messages. Re-run the one assembly at
  `verbosity=normal` to read them; do not raise the verbosity of the whole-solution run, which prints a
  line per passing test.
- **A summary is still printed for the subset that reported.** A partial run reports `Failed: 0` and only
  the process exit code disagrees. Never conclude a leg is green from the summary line alone.

`JINT_TEST_TRACE=1` turns on `TestProcessTrace.cs`, which writes one stderr line per test start and
finish; the highest ordinal with no matching finish names the test that was in flight when the process
went, and `dotnet test -- NUnit.NumberOfTestWorkers=0` makes that exactly one test. Exit codes are worth
reading too: `134` is SIGABRT, which is what a managed stack overflow becomes, `139` SIGSEGV, `137` the
OOM killer, and macOS leaves a `.ips` report in `~/Library/Logs/DiagnosticReports` besides.

The one time this has happened it was a stack overflow: linking a thousand-module import chain recursed
once per module and ran out of stack on macOS under `net8.0` and nowhere else. The general lesson is the
one in `Jint/Runtime/Modules/AGENTS.md` — a test whose passing depends on how much stack the runner gave
the thread is asserting a property of the runner.

### Where integrator-facing tests belong

`Jint.Tests.PublicInterface` is the only test project **without** `InternalsVisibleTo` (the grant list is `Jint.Tests`, `Jint.Tests.Test262`, `Jint.Benchmark`, `Jint.Repl`), so a test there actually proves the surface is reachable by a third party. Put new integrator-facing tests there, in **generically named files** describing the capability rather than any particular integrator — the `Host*Tests.cs` family (`HostObjectSemanticsTests`, `HostObjectProbeCountTests`, `HostObjectEnumerationTests`, `HostDelegateTests`, …) is the established precedent. Remember that a `protected internal` member is seen as `protected` from outside the assembly, so an override is spelled `protected override`.

### The public API baselines

`Verify/PublicApiTest_<tfm>.verified.txt` **is** Jint's public surface, written down — one file per target
framework it ships, produced by `PublicApiTest.cs`. It is the only guard this repository has against an
unintended public API change: there is no ApiCompat run and no shipped/unshipped API files.

**A failure is a diff to review, not a bug report to act on blindly.** Read it first.

- **Intended?** Accept the new baseline and carry the same diff into the v5 migration guide. Verify writes a
  `*.received.txt` beside the `*.verified.txt` on failure; accepting is replacing the one with the other (a
  diff tool, `DiffEngineTray`, or a plain copy — Verify's own docs cover the tooling).
- **Not intended?** The diff *is* the bug report. Nothing else in the repository would have caught it.
- **Never hand-edit a baseline.** A hand-written line is a claim about the assembly that nothing checked, and
  it survives forever because the next run compares against it rather than against the compiler.

These files are also what the migration guide gets written from: `git diff <rev>:<path> <rev>:<path>` over a
baseline is the exhaustive, always-current API delta between two revisions, which no hand-maintained table
stays.

Three things about how it is wired are worth knowing before touching it:

- **Five baselines, because the surface genuinely differs.** Everything under `Jint/WebApi/` is behind
  `#if NET8_0_OR_GREATER`, and `SUPPORTS_HALF` and its siblings add members downlevel targets lack, so the
  diff for a new member tells you *which consumers reach it*. As it stands the five collapse into two distinct
  surfaces — `net472` = `netstandard2.0` = `netstandard2.1`, and `net8.0` = `net10.0` — and keeping them as
  five separate files is what makes a future divergence inside either group show up as a diff instead of as
  nothing.
- **They are generated from `artifacts/bin/Jint/`, not from the loaded assembly.** A test project can only
  ever *load* two of the five, so `netstandard2.0`, `netstandard2.1` and `net8.0` are read out of the build
  output through a `MetadataLoadContext` — which composes with `PublicApiGenerator` because it reads the file
  with Mono.Cecil from `Assembly.Location` and never reflects over it. The `BuildJintForEveryShippedTargetFramework`
  target in the `.csproj` builds the outer, cross-targeting Jint project so all five are present and current
  whenever this suite builds; if one is missing or the tree is left over from a different build, the test says
  so and names `dotnet build -c Release Jint/Jint.csproj` rather than passing on stale output.
- **An attribute whose type is `internal` in that assembly is not rendered.** `PublicApiGenerator` skips it,
  so `[Experimental("JINT0001")]` — Jint's marker on its declared non-contracts — shows up only in the
  `net8.0` and `net10.0` baselines. Downlevel it is PolySharp's polyfill, which this repository deliberately
  generates `internal`. The diagnostic itself still fires for a downlevel consumer (the `net472` leg of this
  very project proves it, against Jint's `net472` asset); only the snapshot is silent. Do not "fix" that by
  making the polyfills public — that would put a type whose members vary by target framework into the surface
  these files exist to pin.
- **The rows come from `Jint.csproj`'s own `<TargetFrameworks>`.** Adding a target framework adds a failing row
  that wants a baseline, rather than silently shipping an unsnapshotted surface. The test itself runs on
  **exactly one** leg, selected by the `RunsPublicApiBaselines` property in the `.csproj` — which also
  defines the `PUBLIC_API_BASELINES` symbol the file is wrapped in and gates the build target, so the three
  cannot drift apart. Two reasons, and only the first is obvious. `net472` is excluded because
  `PublicApiGenerator` formats through CodeDom and nothing promises .NET Framework's lays the same metadata
  out identically, so that leg would be verifying the host rather than Jint. Every *other* leg is excluded
  because the snapshot is of five files on disk: which runtime reads them changes nothing, so a second leg
  is redundancy — but it is a second process reading `artifacts/bin/Jint/` while the other leg's build is
  still writing it, and a second `Jint.dll` beside a test binary that the outer Jint build never refreshes.
  A stale copy trips `TheSnapshottedAssembliesAreTheOnesThisTestRunWasBuiltFrom`; a torn read faults inside
  `MetadataLoadContext`, where the runner cannot attribute it to a test, so the run dies part-way through
  and still reports what it managed. If a future change wants the
  baselines on a different runtime, move the property — do not add a second leg.

### The documentation gate

`PublicApiDocumentationTest.cs` holds every declaration of that same approved surface to carrying a
`<summary>`, and `UndocumentedPublicApi.txt` is the register of the ones that do not yet. **The house style a
new or rewritten doc comment is written to is [`docs/xml-doc-style.md`](../docs/xml-doc-style.md)** — read it
before writing one, not after a review says the summary is three sentences long.

Three things about it are worth knowing before a failure here sends you looking.

- **It does not parse the baseline.** The surface is enumerated out of the assembly's metadata as ISO
  documentation comment ids, because that is the key `Jint.xml` is already keyed by; deriving one from a
  C#-like baseline line would mean re-implementing overload and generic-arity rendering, which is the part
  that is hard to get right. The baseline is still load-bearing:
  `TheEnumeratedSurfaceIsTheApprovedBaseline` holds the two to the same declaration count, so an enumerator
  that quietly stopped seeing a whole category of member fails instead of reporting less debt.
- **The register may only shrink.** Documenting something that is in it fails the test until the file is
  regenerated, which is deliberate — an allowlist that silently absorbs its own progress stops being a count
  of what is left. `JINT_PUBLIC_API_DOCS=update` rewrites it; nothing else may edit it, and a line is never
  added by hand. A new public declaration ships documented.
- **It runs on one target framework and proves that is enough.** `NoTargetFrameworkIsDocumentedLessThanTheNewest`
  checks the other four are subsets *and* that nothing documented on the newest is undocumented on them, which
  is the failure a single-target gate would otherwise miss: a doc comment inside `#if NET8_0_OR_GREATER`, or on
  the only part of a `partial` type that is gated, is invisible to a `netstandard` consumer. It found two.

All four checks together cost about a second, so they are not gated on an environment variable the way
`JINT_WPT_CENSUS` is — every leg that runs this suite runs them.

### What counts as a public contract

**A change to any of it is a row in [`docs/v5-migration.md`](../docs/v5-migration.md), written in the same pull request.**
That includes a change that breaks nothing at compile time — a flipped default, a narrowed lane, a message that stops
being detailed. A compiler cannot find those, so the guide is the only place an embedder can.

| Surface | Location |
| --- | --- |
| `Options.AddLazyGlobal` — extension method on `OptionsExtensions` — and its per-engine counterpart `Engine.AddLazyGlobal`, whose `<TState>` overload takes the state so a `static` factory can serve it without a closure | `Jint/Options.Extensions.cs`, `Jint/Engine.Globals.cs` |
| `Engine.HostDefined` — the `[[HostDefined]]` field of the engine's **principal** realm (`Realm.HostDefined`), reachable from an `Engine`; an opaque `object?` the engine never reads, so host code handed nothing but an `Engine` can get back to per-request state. Principal, not current: it does not move inside a `ShadowRealm`, whose own slot the spec starts empty and `Host.InitializeShadowRealm` exists to fill | `Jint/Engine.Globals.cs`, `Jint/Runtime/Realm.cs` |
| `ReferencedGlobals` + `Prepared<T>.ReferencedGlobals` + `{Script,Module}PreparationOptions.CollectReferencedGlobals` | `Jint/ReferencedGlobals.cs`, `Jint/Prepared.cs`, `Jint/PreparationOptions.cs` |
| `{Script,Module}PreparationOptions.StaticAnalysis` — the opt-out from the prepare-time analysis pass, plus the promise that the parse-only tree it returns is *still* safe to share across engines | `Jint/PreparationOptions.cs` |
| `GlobalSnapshot` + `Engine.Advanced.CaptureGlobalSnapshot` / `RestoreGlobalSnapshot` / `WithRestoredGlobals` | `Jint/Engine.GlobalSnapshot.cs` |
| `ResultLimits` + `ResultLimit` + `ResultLimitExceededException` + `Engine.ConvertResult` + the `JsonSerializer` constructor that takes them / `JavaScriptException.GetJavaScriptErrorString` | `Jint/ResultLimits.cs`, `Jint/Runtime/ResultLimitExceededException.cs`, `Jint/Engine.cs`, `Jint/Native/Json/JsonSerializer.cs`, `Jint/Runtime/JavaScriptException.cs` |
| The `*Async` failure channel — which of the two ways out of an `*Async` entry a given failure takes | `Jint/Engine.Async.cs`, `Jint/Engine.Modules.cs`, `Jint/Engine.Pump.cs` |
| `Engine.Options` — the frozen configuration the engine actually runs under, which for a hardened profile is the engine's private clone rather than the instance the host handed in, and which `Engine.WebApi.Enable` replaces with a copy of its own | `Jint/Engine.cs`, `Jint.Tests.PublicInterface/HostEngineConfigurationTests.cs` |
| A `string` reaching an invocation entry — `Engine.Invoke`/`InvokeAsync` name **one property of the global object**, by that literal name, and nothing on `Engine` parses a name as source. `Call(string)`/`Construct(string)` did, and were deleted for it (#3289) | `Jint/Engine.cs`, `Jint.Tests.PublicInterface/HostCallableResolutionTests.cs` |
| `Engine.Tasks.Post` — the one public entry that takes **no** host-call scope, so a thread that does not own the engine may hand it work; giving it one "for consistency" would refuse the very callers it exists for. On a live engine it enqueues and nothing else: the action runs on the pumping thread, under the generation captured at post time, with no memory state (a host post belongs to no engine operation) | `Jint/Engine.Tasks.cs`, `Jint.Tests.PublicInterface/HostPostedWorkTests.cs` |
| `Engine.IsDisposed` / `Engine.Disposed` / `Dispose`'s idempotency — the seam a component holding somebody else's engine releases through, and the reason `Post` above is a barrier rather than an enqueue after `Dispose` ([#3684](https://github.com/sebastienros/jint/issues/3684)). The event is raised once, on the disposing thread, with `IsDisposed` already `true`, before anything is released and before `EnterHostCall`; a handler that throws still leaves the engine released | `Jint/Engine.cs`, `Jint/Engine.Tasks.cs`, `Jint.Tests.PublicInterface/HostEngineDisposalTests.cs` |

**Five areas keep their own rows beside the code they govern**, on exactly these terms — the same rule, the
same meaning of "breaking", the same migration-guide row. Look there as well before deciding a change is safe:
[`ObjectInstance`'s virtuals, the access lanes and the shape factories](../Jint/Native/Object/AGENTS.md#what-counts-as-a-public-contract);
[the property descriptor and its lazy hooks](../Jint/Runtime/Descriptors/AGENTS.md#what-counts-as-a-public-contract);
[the reference resolvers, the converters, `ProxyHandler` and the CLR exception behind an interop error](../Jint/Runtime/Interop/AGENTS.md#what-counts-as-a-public-contract);
[`Constraint` and the two host-armed budgets](../Jint/Constraints/AGENTS.md#what-counts-as-a-public-contract);
[the module loaders and a module's location](../Jint/Runtime/Modules/AGENTS.md#what-counts-as-a-public-contract).

Three things are deliberately **not** contracts, and since [#3304](https://github.com/sebastienros/jint/pull/3304) they say so to the compiler rather than only in prose: each carries `[Experimental(JintDiagnosticIds.NonContractDiagnostic)]`, i.e. **`JINT0001`**, so reaching for one is an error at the call site until a host acknowledges it. `Jint/JintDiagnosticIds.cs` is the single place that identifier is documented; `Jint.csproj` suppresses it for the assembly that owns the types, and the suites that exercise them acknowledge it per file. Add a new non-contract to that list rather than trusting a paragraph nobody reads — and give a genuinely new *area* its own identifier instead of folding it into this one.

**`JINT0002` is a second identifier with a different meaning, and mixing them up loses the distinction.** `JINT0001` says *the answer describes an internal representation*; `JINT0002` says *the capability is settled and its shape is not yet* — a preview. Today it marks `Jint.Diagnostics.ValueInspector` and the `ValueDescription` / `ValueEntry` / `ValueKind` / `ValueInspectorOptions` types it answers with, whose exact `Description` text may change in any release. A third area gets a third identifier rather than joining either.

The first: `ObjectRepresentation` and `Engine.DiagnosticOperations.GetObjectRepresentation` name an internal representation for diagnostics and tests. Which representation an object lands in may change in any release and the enum may gain members; neither counts as breaking. Do not let a host branch on it in production code, and do not freeze the engine's behaviour to preserve it. What *is* stable is the one question a host actually asks of it — is this object's own string-keyed storage a layout shared with its siblings — and that has its own predicate, `Engine.Advanced.HasSharedShape`, which is a contract ([`Native/Object/AGENTS.md`](../Jint/Native/Object/AGENTS.md#what-counts-as-a-public-contract)). A host may pin its answer for the documented success case of the factory it called (`JsObject.Create`, `JsObject.CreateFromEntries`, `JsObjectShape.Instantiate`); which *other* construction paths reach a shared layout can still improve release to release. Both read the same internal flags, and `HasSharedShape` is literally `GetObjectRepresentation` reduced to its shared-layout members, so they can never disagree — including that neither perturbs, so a lazily-initialized object answers for its settled representation only once something has touched it. A test asserting shaping belongs on the predicate; reach for the enum only to explain a `false`.

`Engine.Diagnostics.GetMemoryReport` and the `Jint.Diagnostics` report records it returns are the second deliberate non-contract, and for the same reason: they count internal collections, so which collections are counted and how may be refined in any release and the records may gain members. That is why every one of their constructors is `internal` — a host reads the properties it knows about and a new one is not a breaking change. Two properties of it *are* load-bearing and have tests: it adds **no field to `Engine`** (every figure is derived on demand from state that already exists, so an engine nobody asks pays nothing), and reading it **materializes nothing** — no getter invoked, no lazy property factory run, no built-in's function object created — which is what lets a host log it on every request without moving the very numbers it is watching. The walks therefore read descriptor storage directly and test `PropertyFlag`s rather than calling `GetOwnProperty`, `descriptor.Get` or `descriptor.Value`; any new figure added to the report has to keep both properties, and the census in particular must never reach for `ObjectInstance.Get`. What it deliberately does *not* offer is a per-call-site breakdown of the warmed handler trees: the interpreter's node classes carry no child enumeration, and a reflection walker over their private fields would break silently on the next field added, so the report surfaces the three cache sizes (`_functionDefinitions`, `_scriptStatementLists`, `_evaluatedScripts`) as the retention roots and says so in its own documentation.

`InteropConversionDiagnostics` and `Engine.DiagnosticOperations.GetInteropConversionDiagnostics` are the third, on the same terms: which internal events are counted may be refined in any release.

Two public members read as if they belonged on that list and deliberately do not. `Engine.Tasks.ProcessTasks` is the canonical host loop — the *only* way a timer callback, a settled `Atomics.waitAsync` or a worker message ever runs — so its old "this API may break and change behavior!" line was a defect, not a declaration, and was corrected; `TimeUntilNextScheduledWork`'s documentation is the one to keep it consistent with. `Engine.Tasks.RegisterPromise` is a real capability rather than a report about an internal representation, so its "EXPERIMENTAL! Subject to change." banner was removed rather than promoted: with `JINT0001` in the assembly the word has to mean exactly one thing.
