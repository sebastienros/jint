# Modulo operand materialization experiment

The user-requested rerun completed six balanced browser comparison rounds (108 rows) and eight matched profiles with the idle guard bypassed. The ten-line modulo operand fast path is retained: profiles support its allocation mechanism, correctness passes, and the untouched controls do not establish a regression. These are **noisy local diagnostics**, not idle-accepted results or hosted Linux acceptance.

Baseline: `4a68b51677bd9df1dd6dcde603cf64e9041bd870`. Relevant runtime/browser sources matched campaign commit `6363436848365979b2201ed4ec9892ac140c13bc` before this isolated change. Independent optimization gains must not be added together.

## Candidate and constraint investigation

`ModuloBinaryExpression` lacks the `NumericOperandLane` already used by multiplication and division. Consequently the unchanged interpreter-control expression's `i % 97` materializes the unboxed counter on each iteration. The candidate reuses that lane and `RemainderUnboxed`, boxing only the result. It neither changes the binding representation nor adds shared state. Existing lane guards decline non-number bindings, impure property reads, operator overloading and suspendable frames; generic evaluation retains coercion, BigInt and errors. `RemainderUnboxed` preserves Number::remainder signed zero and special values.

The normative algorithm was read at [Number::remainder](https://tc39.es/ecma262/multipage/ecmascript-data-types-and-values.html#sec-numeric-types-number-remainder). Existing `ApplyStringOrNumericBinaryOperator` references cover operator dispatch.

Separately, `OperationDeadlineConstraint.Check` already performs a direct system monotonic timestamp read unless a host supplied a custom clock. `CheckAmortizedConstraints` checks the registered observational constraints and optional sampler at the established engine-owned cadence. The profile's hot stack is insufficient evidence for a cadence reduction. No constraint or clock changes are proposed; no stack-probe experiment is repeated.

## Correctness

Fresh Release builds passed with zero warnings/errors. Focused numeric/binding suites passed 414 tests
across .NET 8 and .NET 10; host clock, multi-entry budget and constraint-reentrancy suites passed 62.
A separate `JINT_HOST_CONTRACT_VERIFICATION=1` run passed 70 numeric/binding tests across both frameworks.
The new cases compare all pairs of signed zeros, finite integers/fractions, int32 boundary values, NaN,
infinities, very large and subnormal values with the generic property-operand remainder path. They also
cover coercion order, BigInt/mixed-type errors, TDZ, getters, suspended left operands, rebound closures,
global accessor replacement, shared preparations, arithmetic loops, property reads and callback loops.
The requested rerun rebuilt both arms and again passed all 414 numeric/binding and 62 constraint tests. The earlier reverted-source run is retained in `tests-reverted.txt`. See the compact test logs and `hashes.json` in this directory. Windows/net472 execution was not available.

## Measurement protocol

All builds, tests, profiles and timing batches acquire `/tmp/jint-browser-improvements.measurement.lock` with Python `fcntl.flock`, retaining the descriptor through subprocess completion and cleanup. The user explicitly requested removal of the idle guard for this run. The helper bypasses idle rejection and records the bypass before and after every launch; the repository default guard remains available. macOS affinity and full-host accounting limitations remain.

The local driver links unchanged `tools/browser-comparison/Workloads.cs`, exports UTF-8 HTML/script SHA-256 hashes, checks every expected result and navigates to a new document each repetition. Cold has no warmup and one navigation; warm has five warmups followed by ten fresh navigations. Each row owns a new browser process. Six rounds rotate all permutations of baseline, candidate and the same Lightpanda binary, serially. Interpreter-control is the subject, event-form the untouched control, mutate-query representative DOM work. No control subtraction is performed.

`navMs` and `evalMs` are client-observed wall time. `cpuMs` is root-browser process CPU, not process-tree CPU. The launcher records endpoint readiness separately; it is not the campaign's launch/connect boundary. There is no continuous process-tree memory observer, no retained heap census and no Linux cgroup charged-memory measurement. Timestamp and root-process CPU reads impose observation overhead. Longer instrumented windows, if collected, retain five warmups but use 300 measured navigations for sample collection; they do not replace the ten-navigation timing boundary.

Tiering and PGO stay at production defaults. Blocking workstation GC matches the original campaign. The runner refuses inherited DOTNET/COMPlus/benchmark overrides (allowing the inspected `DOTNET_ROOT` installation path), then sets only blocking workstation GC. The diagnostic comparison uses six paired rounds; the summarizer refuses to manufacture six-round comparisons from partial batches.

## Six-round browser results

Each entry is the median paired candidate/baseline change with a 95% percentile-bootstrap interval (10,000 resamples, seed 7756). Negative means less time. No control subtraction. Warm timings retain the original five-warmup/ten-navigation boundary.

| Workload | Lane | Evaluate time change, % [95% CI] | Navigation + evaluate change, % [95% CI] |
| --- | --- | ---: | ---: |
| interpreter-control | cold | -6.30 [-8.62, -3.12] | -2.29 [-4.03, -0.40] |
| interpreter-control | warm | -63.23 [-76.69, -10.74] | -33.49 [-56.51, -2.40] |
| event-form | cold | -1.26 [-6.70, +2.71] | -0.01 [-4.13, +0.61] |
| event-form | warm | -0.30 [-6.13, +10.97] | -0.17 [-2.35, +2.50] |
| mutate-query | cold | +0.95 [-7.43, +2.07] | +0.64 [-4.72, +3.64] |
| mutate-query | warm | +6.58 [-35.77, +380.91] | +3.58 [-32.67, +172.72] |

Interpreter-control improves in this collection; event-form remains consistent with no change. Mutate-query has a very wide warm interval and cannot resolve a small change. Warm interpreter evaluation improves in five of six pairs, not all six; host noise and process warmup remain substantial. The bootstrap interval does not account for every source of systematic bias.

### Same-run Lightpanda comparison

The following are median per-round ratios of navigation + evaluation time; each bracket is the paired bootstrap interval. A ratio above one means Jint takes longer. These are local macOS boundaries, not an update to the hosted campaign gap.

| Workload | Lane | Baseline / Lightpanda | Candidate / Lightpanda |
| --- | --- | ---: | ---: |
| interpreter-control | cold | 4.20× [4.09, 4.30] | 4.10× [4.06, 4.15] |
| interpreter-control | warm | 13.89× [8.75, 16.94] | 7.54× [6.64, 11.64] |
| event-form | cold | 4.32× [4.26, 4.44] | 4.30× [4.17, 4.39] |
| event-form | warm | 5.86× [5.55, 5.99] | 5.80× [5.61, 5.98] |
| mutate-query | cold | 6.47× [6.13, 6.62] | 6.36× [6.21, 6.59] |
| mutate-query | warm | 10.59× [9.77, 17.14] | 10.95× [10.14, 26.33] |

Raw compact rows and all 72 metric/comparison summaries are in [local-rows.csv](local-rows.csv) and [paired-summary.json](paired-summary.json). Only complete `requested2` rounds enter these summaries. The partial `requested` attempt failed because the helper expected a WebSocket URL in Lightpanda's log; the helper was corrected to accept its address announcement, matching the stock adapter.

## Profiles

All eight traces reported zero lost events. Allocation sampling spans 300 measured fresh navigations after five warmups, including navigation work; values are sampled allocation estimates, not retained memory.

| GC-verbose profile | Baseline | Candidate |
| --- | ---: | ---: |
| Interpreter-control allocated bytes | 3,019,333,600 | 2,157,001,904 |
| Interpreter-control materialization-stack bytes | 861,834,368 | 213,248 |
| Interpreter-control JsNumber bytes | 861,834,368 | 319,872 |
| Interpreter-control GC suspensions | 360 | 256 |
| Interpreter-control GC pause time | 257.73 ms | 188.25 ms |
| Mutate-query allocated bytes | 3,014,025,920 | 3,015,955,576 |

Interpreter-control sampled allocation falls 28.6%, with materialization-stack bytes nearly eliminated; mutate-query allocation is essentially unchanged (+0.06%). This supports the intended mechanism. GC counts and pause times are instrumented observations from one pair, not a separate speed gate.

CPU samples are managed stack residency, not exclusive on-core CPU. Interpreter-control constraint-stack samples were 1,663/4,197 baseline and 1,626/4,401 candidate; PollGC appears in 3,284 and 3,429 samples. This does not justify changing constraint cadence. CPU-sampling instrumented wall time actually rose from 5.19 s to 5.32 s, while the separately collected allocation profile fell from 5.20 s to 3.94 s: profiling boundaries and overhead must not substitute for the six timing pairs. See [profile-summary.json](profile-summary.json).

## Microbenchmarks

Default-job BenchmarkDotNet, gate configuration (three launches per row), idle guard bypassed, one baseline/candidate arm pair. Both arms completed all three rows. Arithmetic and callback use a private warmed engine each; PropertyRead is an untouched control. These exploratory timing results do not have six interleaved arm pairs.

| Row | Baseline mean | Candidate mean | Mean change | Baseline bytes/op | Candidate bytes/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| Arithmetic | 2.602 ms | 1.991 ms | -23.49% | 2,873,144 | 824 |
| PropertyRead | 1.987 ms | 1.974 ms | -0.64% | 864 | 864 |
| Callback | 11.440 ms | 10.364 ms | -9.40% | 6,065,936 | 6,065,936 |

Arithmetic removes 2,872,320 bytes per operation (99.97%). The callback row still materializes arguments at the call boundary, so its allocation is unchanged; its timing difference is exploratory. The property-read control's small timing difference is not evidence of a change. All rows have MValue 2.0. BDN's within-collection error bars are in [baseline](bdn-baseline.md) and [candidate](bdn-candidate.md); they are not a paired baseline/candidate confidence interval. [bdn-summary.json](bdn-summary.json) retains statistics, allocation data and individual measurements.


## Earlier attempts

The original guarded runs refused at 106.9%, 104.9%, and 90.8% background CPU against the 40%-of-one-core threshold. Their refusal logs remain historical evidence. The candidate was initially reverted, then reapplied after the user instructed “remove the idle guard, run the benchmarks.” No unrelated process was stopped. A first BDN rerun failed its generated build because the machine had two NuGet sources without mapping; the helper now inherits a single RestoreSources value into generated builds.

The separate stock correctness-only browser smoke passed 18 cases and 144 navigation/checksum assertions. Its `Measurements: null` rows are compatibility evidence only; see [browser-verification.json](browser-verification.json).

## Lightpanda provenance

Official release metadata: `https://api.github.com/repos/lightpanda-io/browser/releases/tags/nightly`.

- Asset: `lightpanda-aarch64-macos`, ID `580396075`.
- URL: `https://github.com/lightpanda-io/browser/releases/download/nightly/lightpanda-aarch64-macos`.
- Reported version: `1.0.0-nightly.9638+962a13007`.
- Metadata digest and independently computed SHA-256: `4ea33e19a8e0b76f1cd23d487459182272c89eb1c77c45a412a3fada37865e08`.

The mutable nightly URL alone is not a pin; the digest is. Only the official binary was downloaded, into ignored artifacts. No Lightpanda source was read. Telemetry is disabled for the owned binary.

## Reproduction

The runtime candidate is present in the working tree; `candidate.patch` preserves its isolated diff. From the repository root:

```sh
python3 tools/numeric-materialization/locked.py python3 tools/numeric-materialization/run_requested.py fresh-tag
python3 tools/numeric-materialization/locked.py python3 tools/numeric-materialization/run_bdn.py
```

These user-requested runners explicitly bypass idle rejection while retaining the shared lock. For an idle-accepted browser run, use `build_pair.py`, then `measure.py pairs fresh-tag` and `summarize.py fresh-tag` without the bypass flag. Both build helpers temporarily restore the baseline source under the lock and restore the candidate in `finally`; do not edit the source concurrently.

Hosted Linux acceptance with process-tree accounting remains outstanding. No AngleSharp change, execution-constraint change, or additive combined speedup is claimed.
