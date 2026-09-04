# Agent instructions: the main test suite

> **Read this when:** You are adding or changing a test in `Jint.Tests`, reaching for a wall-clock number in
> one, or looking at a failure from one of the tests that hold this repository to its own rules.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there. The web-platform-tests corpus that lives under `Wpt/` has its own file,
> [`Jint.Tests/Wpt/AGENTS.md`](Wpt/AGENTS.md).

## The NUnit contract is assembly-level, and it belongs to four assemblies

`TestFrameworkConventions.cs` holds two assembly attributes and nothing else, and both exist to make an
NUnit run mean what the xUnit run before it meant:

- **`[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]`.** NUnit's default is one instance per
  fixture, reused across its tests; xUnit constructed a new one per test. Nothing in either suite was
  written against a reused instance — `EngineTests`, `InteropTests`, `MethodAmbiguityTests`, `SamplesTests`
  and `UuidTests` all build an `Engine` in their constructor and expect it fresh — so the default would
  silently share one engine, its realm, its intrinsics and whatever a previous test wrote to the global
  object across every test of the class.
- **`[assembly: Parallelizable(ParallelScope.Fixtures)]`.** Fixtures run in parallel, the tests inside one
  run sequentially. It is deliberately not `ParallelScope.All`: several fixtures hold state across their own
  tests, and xUnit never ran those concurrently. The classes that must not run beside anything at all carry
  `[NonParallelizable]`, which NUnit runs in a single-worker shift — that shift is what gives them no
  parallel fixture *and* no other non-parallel fixture in flight, and it is what the garbage-collection
  fixtures depend on.

That file is `<Compile Include>`-linked into **`Jint.Tests.Browser`, `Jint.Tests.DevTools` and
`Jint.Tests.PublicInterface`** as well, so it is one contract for four assemblies and editing it edits all
four. Widening it is not a local decision, and the failure it buys is not local either: a fixture that
starts sharing an engine fails somewhere else, intermittently, in a test nobody touched.

## A wall-clock number is either the assertion or a wedge ceiling — never both

`TestBudgets.WedgeCeiling` (two minutes) is the budget for a wait released from *outside* the engine: a
module load settling on a loader's thread, a host `Task` completing, a callback dispatched from the thread
pool. A test using it asserts an outcome — a value, an exception type, a message — and never a duration, so
a healthy run spends none of it and widening it can hide nothing. What it removes is the thread pool from
the set of things deciding the outcome.

`TestBudgets.cs` is `<Compile Include>`-linked into `Jint.Tests.PublicInterface` too, so the constant is one
number for both suites — and every counter-example is over there. A test that asserts a budget **is**
respected states that budget itself and must never be routed through here:
`HostMemoryLimitTests.SynchronousImportDoesNotChargeAsyncLoaderWaitToExecutionTimeout` with its 200 ms
execution timeout, `HostModuleImportStateTests` with its 300 ms gate, and
`AsyncModuleLoaderTests.AWarmAnswerServesTheBlockingImportEvenWhereDrainingIsImpossible` with its
deliberately short 500 ms. **Ask of every timeout you write or widen: would this test still fail if the
behaviour it pins regressed?** If widening the number could mask the regression, the number is the
assertion, and reaching for the shared constant is how a real failure becomes a green run.

## Reach for the helper that already exists

The xUnit vocabulary was ported into named helpers rather than re-improvised per file, and a second spelling
is exactly how two suites compiled from the same conventions file drift apart anyway:

| You want | Use | Not |
| --- | --- | --- |
| whatever a delegate threw, or `null` | `Caught.Exception` / `Caught.ExceptionAsync` | `Assert.Throws`/`Assert.Catch`, which fail when nothing is thrown |
| typed, compiler-checked rows for `[TestCaseSource]` | `TestCases<T>` | a bare `object[]` enumerable |
| a test gated on how the run was started | `[IgnoreUnless(nameof(Condition), "reason")]` | a silent early `return`, which reports a pass |
| a test that needs a debugger or the network | `[RunnableInDebugOnly]` | a commented-out test |
| a bigger stack, a join timeout, or a thread that is not the pool's | `DedicatedThread.Run` / `RunAsync` | `new Thread(...)` per test |
| `engine.Evaluate("1 + 1").Should().Be(2)` | `JsValueAssertions` | unwrapping to CLR first |
| a top-level park on `WaitForScheduledWork` | `TopLevelPark` | a bare `Task.Run` and a sleep |

A gate that reports a **pass** for an unrun test is the failure mode all of these are shaped against: a
skipped test is reported skipped, and an unrunnable one is reported unrunnable.

## Four tests here hold the repository to its own rules

None of them is satisfied by editing the thing it checks.

- **`AgentInstructionFileTests`** — the byte budgets on every `AGENTS.md` and both halves of the routing map
  (the root index and `.claude/rules`). When it fails, relocate the material to the file that governs it and
  update both halves; the failure message prints every file's headroom precisely so there is an answer to
  "where does this go instead". Do not trim to fit.
- **`SpecCitationTests`** — every `tc39.es` anchor the tree cites, against the register in
  `Jint.Tests/SpecAnchors.txt`. A fragment that no longer exists does not 404, it silently lands the reader
  at the top of the document, which is why this is checked at all. An ordinary run only compares against the
  register; re-verifying against the living documents is `JINT_SPEC_ANCHORS=update`, which is a periodic
  chore rather than a gate. The `!` lines are **debt with a reason**, not configuration, and an update will
  not add one on its own.
- **`MigrationGuideTests`** — the numbering rule `docs/v5-migration.md` states for itself. Two pull requests
  that each pick "the next free subsection" append to different parts of one file, so **git merges them
  cleanly** and the duplicate exists only in the rendered document. Gaps are legitimate; duplication and
  disorder are not.
- **`TestFrameworkConventions`** — the two assembly attributes above, which is the section to read before
  touching it.

## Nullable is off for the project and on per file

`Jint.Tests.csproj` sets no `<Nullable>`, and roughly half the files open with `#nullable enable`. **A new
file adds that line**; retrofitting an old one is a change to argue on its own. `TreatWarningsAsErrors` is on
repository-wide, so the annotation you add has to be right rather than approximately right.

## Two things about the project shape

- **The target frameworks are `net8.0;net10.0`, plus `net472` on Windows.** A test written against a modern
  BCL API still has to compile on `net472`; the polyfill discipline that answers that is
  [`Jint/Extensions/AGENTS.md`](../Jint/Extensions/AGENTS.md), and it applies to test code too.
- **The dependency on `Jint.Tests.Browser` is one way.** This assembly grants it `InternalsVisibleTo` so the
  browser lane runs the web-platform-tests on *this* project's corpus — one corpus, one pin, one exclusion
  vocabulary. Nothing here may reference `Jint.Browser`.

Scripts a test loads are embedded resources under `Runtime/Scripts/` and `Parser/Scripts/`, declared in the
project file; adding a script is adding a file to one of those directories, not a new `EmbeddedResource`
entry.

## Host-contract verification is a separate leg, deliberately

`HostContractVerificationSwitch` turns Jint's host-contract verifiers on for a Release run, so the exact
configuration an embedder is told to use is one this harness exercises:

```bash
JINT_HOST_CONTRACT_VERIFICATION=1 dotnet test -c Release
```

The gate reads a `static readonly bool` at type initialization, so the switch has to be set before the first
use of *any* Jint type — a fixture, a constructor or an assembly-level lifetime hook all run far too late,
and a `[ModuleInitializer]` is early enough by construction. It is **not** the default, and that is load
bearing: the Release probe-count and no-descriptor pins in `Jint.Tests.PublicInterface` are the regression
net for the claim that the gate folds to zero cost when nobody asked for it, and they can only mean that in
a run where nobody did. See
[Host-contract verification](../Jint/Native/Object/AGENTS.md#host-contract-verification).
