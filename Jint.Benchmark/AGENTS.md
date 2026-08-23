# Agent instructions: benchmarks

> **Read this when:** You are writing, running, or quoting a benchmark number.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

## Benchmarks

Benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) and live in `Jint.Benchmark/`.

```bash
# List, then run a class, a method, or a wildcard match
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --list flat
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --filter "Jint.Benchmark.EngineConstructionBenchmark"
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --filter "*EngineConstructionBenchmark.BuildEngine"
dotnet run -c Release --project Jint.Benchmark\Jint.Benchmark.csproj -- --filter "*Array*"
```

Running everything is slow — filter unless you need the full set.

> **Do not report or compare numbers from `--job short`.** It reduces warmup and iteration counts to ~3, so its run-to-run variance (~10%) exceeds most of the wins we measure. It is a smoke-test that a benchmark *runs*; use the default job for any figure that reaches a README, PR, or commit message.

The cross-engine comparison (`EngineComparisonBenchmark`) has its own notes and published results in [`Jint.Benchmark/README.md`](README.md). Run it from the `Jint.Benchmark` directory so the `Scripts/*.js` files resolve: `dotnet run -c Release -- --allCategories EngineComparison`.

### The measurement environment

Every benchmark runs under `JintBenchmarkConfig` (`Jint.Benchmark/JintBenchmarkConfig.cs`), applied globally from `Program.cs`. It exists because the suite previously ran on BenchmarkDotNet's bare `DefaultJob` — and almost everything needed here was already in BenchmarkDotNet and merely unused: `LaunchCount`, `WithAffinity`, `WithPowerPlan`, `WithGcConcurrent`, `WithEnvironmentVariable`, `AnalyzeLaunchVariance`, and the `MValue` column. BDN was already computing the multimodality statistic that marks a row as untrustworthy, and this project was throwing it away.

**Keep the two error terms apart.** *Within-run noise* is what `StdDev` describes; BDN resamples it up to 100 iterations and the mean converges. *Between-run offset* — code layout, heap placement, tiered-compilation outcome, starting core — is constant within a process and varies between them, so with `LaunchCount = 1` **none of it reaches the reported error**. That is the shape of an A/B pair that looks decisive and then will not reproduce. More iterations cannot fix an offset; only more launches can.

Three modes, selected by `JINT_BENCH_MODE`:

| Mode | What it adds | Use it for |
| --- | --- | --- |
| `stable` (default) | machine-idle check, affinity, fixed-clock plan, blocking GC | day-to-day development |
| `gate` | the above plus `LaunchCount=3` and launch-variance analysis | **any number quoted in a PR** |
| `legacy` | nothing — bare `DefaultJob` | measuring the old environment for comparison |

The machine-specific pieces all fail safe. The **affinity mask is derived, never hard-coded** (`BenchmarkTopology`): it picks the last-level-cache domain containing CPU 0 and drops physical core 0, falling back to no pinning on non-Windows, on multi-group machines and on anything too small to pin. On the gating 5950X that is CPU 2–15 — one 32 MB L3 domain, so a thread cannot migrate across the CCD boundary and lose its last-level working set.

**What each control actually costs**, measured one factor at a time against `legacy` on a machine verified idle (medians over six `GuardComparisonBenchmark` rows, three launches each):

| control | cost | variance benefit |
| --- | --- | --- |
| blocking GC | +1.1% | none that survives the noise |
| affinity | +3.2% (+1.6% excluding one bimodal row) | none that survives the noise |
| **fixed-clock power plan** | **+47%** | **none** |

So pinning and GC mode are kept because they are free and have a principled rationale, not because this experiment proved they help. The **fixed clock is opt-in** and needs *both* `JINT_BENCH_FIXED_CLOCK=1` and `JINT_BENCH_POWERPLAN`: the GUID tends to live in a user-level environment variable forever, and applying it implicitly would silently cost ~47% on every run thereafter. It does buy one thing the numbers support — it removes boost as a free variable (measured 3,375 MHz with peak equal to mean, against 4,744 MHz peak), so absolute figures become comparable across sessions. Create the plan once with `Jint.Benchmark/setup-benchmark-machine.ps1` (elevated).

> **`MachineStateValidator` refuses to start on a busy machine**, naming the offending processes. BDN has no such notion and will happily format a beautiful table from a contaminated run — the failure is otherwise completely silent. `CompatTelRunner.exe` (Microsoft Compatibility Telemetry) is a repeat offender here and starts on its own schedule; it was caught taking 65% of a core. Override with `JINT_BENCH_SKIP_IDLE_CHECK=1` only when the numbers do not need to be gate-quality.

> **After any interrupted run, `./setup-benchmark-machine.ps1 -Restore`.** BDN restores the power plan only on a clean exit. A killed run leaves the fixed-clock plan active *and* can leave an orphaned `Jint.Benchmark` process that re-applies it, so whatever runs next silently runs at nominal frequency.

**Tiered compilation and dynamic PGO stay at production defaults, and nothing works around them.** Turning tiering off does tighten the spread, but it measures code no embedder runs and forfeits PGO's devirtualization — most of the win for a tree-walking interpreter. Two mitigations were built and measured against a verified-idle baseline on `TypeofStringGuard` over ten launches, and **neither shipped**:

| config | StdDev | MValue |
| --- | --- | --- |
| production defaults | **0.783 ms** | 2.000 (unimodal) |
| run to JIT quiescence (`JitInfo.GetCompiledMethodCount`) | 1.224 ms | 2.000 |
| `DOTNET_TC_QuickJitForLoops=0` + `DOTNET_TC_CallCountingDelayMs=0` | 1.275 ms | 2.795 (and ~13% slower) |

The same row measured StdDev 1.946 ms and looked bimodal on a machine that was quietly busy. **The "bistable slow mode" that looked like a tiering artifact was mostly contention** — which is the strongest argument for the idle check above, and a warning against diagnosing the runtime from numbers taken on a shared machine.

**No machine control reduced the between-process offset**, which is the term that actually breaks reproducibility. Only sampling it helps, which is what `LaunchCount` and the paired comparison below are for.

**Comparing two builds:** BDN runs all of job A then all of job B, so anything that drifts between those two windows becomes a systematic between-arm bias (BDN issue #2004 is that symptom). `Jint.Benchmark/measure-paired.ps1` interleaves the two worktrees round by round, alternating order, and reports the per-round *difference* with a percentile bootstrap CI — the paired ("duet") design, reported in the literature as 2.3–12.5× more accurate than measuring the two separately. **A row is a regression only when its CI excludes zero.** It also **fails loudly**, because the failure mode that actually happens is silent: BenchmarkDotNet writes microseconds with a Greek mu and its CSV carries ~40 job-characteristic and ratio columns, either of which can make every row fail to parse or fail to pair while the script still prints a table and exits 0. So a round whose side parsed nothing aborts naming the artifacts directory, and the run exits non-zero when a row paired in fewer than half the rounds, was seen on only one side, or the table came out empty. `./measure-paired.ps1 -SelfTest` runs the parser and the pairing/statistics pipeline against inline CSV fixtures — no worktrees, no benchmarks — and is the check to run after touching the script.

Validated with an A/A run (the same worktree as both sides, six rounds), which correctly reported *no change* on both rows and calibrates the detection floor:

| row | median Δ | 95% CI |
| --- | --- | --- |
| `NullCheckBenchmark.LooseEqualNull` | −0.49% | [−2.37, +0.59] |
| `NullCheckBenchmark.LooseNotEqualNull` | −0.23% | [−1.03, +0.71] |

So six rounds resolves roughly a 1–2.5% effect on these rows; add rounds rather than reading the median on its own when the interval is too wide to decide. **This is also why the old flat "1% blocks" rule cannot work as stated** — on many rows 1% is below what the measurement can resolve, so it manufactures re-runs rather than catching regressions.

### Adding a new benchmark

1. Create a class in `Jint.Benchmark/` with `[MemoryDiagnoser]`.
2. For script-file benchmarks, extend `SingleScriptBenchmark` and override `FileName` to point at a file in `Scripts/`. The base class handles loading, parsing and the `Execute` / `Execute_ParsedScript` methods.
3. For standalone benchmarks, add `[Benchmark]` methods directly, using `Engine.PrepareScript()` to separate parsing from execution.
4. Put required JS files in `Jint.Benchmark/Scripts/`; they are copied to the output directory automatically.

### Never warm one engine with more than one row's workload

**`[GlobalSetup]` may only touch the engine a row is measured on with that row's own work.** A class that builds one `Engine` and warms it by evaluating *every* `[Benchmark]` row's script hands each row an engine carrying its siblings' state: their globals on the shared global object (nearly every micro-script here declares `var i`, `var s` or `function f`, so they collide outright), their entries in the engine-owned handler-tree caches (`Engine._functionDefinitions`, `_scriptStatementLists`), their per-call-site monomorphic caches, and the environment-reuse and constructor-shape state behind them. A row's number then depends on which sibling rows exist and on what a change did to *them*. This is not theoretical: a call-path change that a single-workload reproduction showed to be 2.5–3.0% **faster** was reported by `MethodCallBenchmark` as +5.6…+9.2% **slower** on three rows, reproducibly and in both A/B orderings.

The default fix is **one engine per row**, built in `[GlobalSetup]` and warmed with that row's script and nothing else — `IsolatedScript` (`Jint.Benchmark/IsolatedScript.cs`) is the helper, `MethodCallBenchmark` and `ClosureCallBenchmarks` the worked examples, and `InteropMethodDispatchBenchmark` the pre-existing precedent for doing it with plain `Engine` fields when a row's workload is not a `Prepared<Script>`. It keeps each row's *warm* dispatch character — several of these classes exist specifically to measure warm dispatch, and making them cold would silently change what the suite is for — and keeps engine construction and warm-up out of the measurement.

Two things this rule does **not** forbid. A shared *fixture* every row needs (`engine.Execute("var testArray = [...]")`) is fine, as long as each row still gets its own engine. And re-evaluating one row's own script many times on one engine is fine where that is the point. Where a benchmark genuinely wants cold-start behaviour, build the engine **inside the benchmark method** so BenchmarkDotNet can auto-scale `InvocationCount` — see `DromaeoBenchmark`, `SunSpiderBenchmark` and `SingleScriptBenchmark`. **Never reach for `[IterationSetup]`**: it forces `InvocationCount=1`, which leaks tiered-JIT warmup into the measured iterations and made identical code report 2.489 ms and 9.811 ms in different runs (the full account is in `DromaeoBenchmark`'s comment).

Document the choice in the class's XML doc comment, including whether engine construction now enters the measurement and roughly what it costs relative to the op.

### Benchmarking host-object shapes

`Jint.Benchmark` **does** have `InternalsVisibleTo`, so a "host object" written there can accidentally use members no real embedder could reach. Restrict such types to the public surface deliberately and say so in the type's doc comment — the existing host types in that project do, and record where the restriction bites. Measurement must be serial on an otherwise idle machine; treat a small delta on an untouched control row as the cross-process floor rather than as a result.
