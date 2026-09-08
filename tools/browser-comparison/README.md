# Controlled offline browser comparison

This tool follows [#3575 X4](https://github.com/sebastienros/jint/issues/3575). The original
[#3907](https://github.com/sebastienros/jint/pull/3907) smoke/raw-diagnostic harness is extended under
[#3930](https://github.com/sebastienros/jint/issues/3930). One PuppeteerSharp client applies identical offline
workloads and independently expected checksums to **AngleSharp + Jint**, Chromium, and Lightpanda.
No Lightpanda source is read or copied.

A working harness is not a completed benchmark campaign. X4 remains open until the complete idle-accepted
paired run and its scoped conclusion are published. No timing result is included here.
Read [the benchmark instructions](../../Jint.Benchmark/AGENTS.md) before measuring or quoting numbers.

## Correctness first

Build the Jint executable in Release, copy `example.json`, and fill in the already installed browser paths.
The harness never installs or downloads a browser. Record the Lightpanda release asset identity and verify
its digest before running the binary. Official nightly assets currently include macOS aarch64 and x86_64;
[macOS 13 or later is supported](https://lightpanda.io/docs/run-locally/installation/system-requirements).
The [nightly URL is mutable](https://lightpanda.io/docs/run-locally/installation/nightly-builds), so a URL alone
is not a version pin. `LIGHTPANDA_DISABLE_TELEMETRY=true` is set for owned Lightpanda processes.

```sh
dotnet build Jint.Browser.Tool/Jint.Browser.Tool.csproj -c Release -f net10.0
python3 tools/browser-comparison/measure.py --config /absolute/path/config.json \
  --output /absolute/path/new-verification-directory
```

Verification freshly builds the Release harness and runs all five workloads in both lanes on every configured
adapter. It exports exact HTML, JavaScript, expected results, identity and provenance. Every result has
`Measurements: null`; no elapsed/CPU/memory fields are emitted as measurements. Missing binaries, unsupported
behavior, mismatched checksums and cleanup failures fail visibly. One adapter is sufficient for verification.

The underlying one-row CLI remains useful:

```sh
dotnet run --project tools/browser-comparison/BrowserComparison.csproj -c Release -- \
  --config /absolute/path/config.json --smoke
```

`workload`, `lane`, `warmupCount`, and `iterations` select a row. Cold requires one iteration on a new browser
and page; warm first executes five unmeasured repetitions of that row, then ten measured repetitions in the
collector. Each repetition navigates to a fresh copy of the fixture. Warm means a warmed browser/runtime,
not retention of a mutated DOM across iterations. No sibling workload warms another row's state.

| Workload | Contract |
| --- | --- |
| `parse-extract` | Parse 1,000 static articles and extract their count, numeric sum and final title |
| `mutate-query` | Create 1,000 nodes, remove alternate nodes, query and mutate the surviving inventory |
| `event-form` | Deliver 100 bubbling input events, serialize the form and inspect the resulting DOM |
| `fetch-update` | Fetch deterministic loopback JSON, update the DOM and cross a promise continuation |
| `interpreter-control` | Run a deterministic arithmetic loop without DOM operations |

Main `052b45aa9` fails the unchanged `event-form` workload because `FormData(form)` rejects a DOM
form ([#3931](https://github.com/sebastienros/jint/issues/3931)). Collection deliberately stops there;
the #3931 implementation is being verified against this unchanged workload before publication.

These cover the campaign's extraction, mutation, interaction and local asynchronous automation use cases.
They do not represent rendering, Internet latency, arbitrary production sites, or every framework.

## Ownership and measurement boundaries

Owned Jint, Chromium and Lightpanda adapters launch new processes for every row. Chromium receives a new
profile; Lightpanda uses its documented `serve --host 127.0.0.1 --port 0` endpoint announcement. Startup,
connection, page work, teardown and failure cleanup have bounded waits. Both diagnostic pipes are drained into the preserved per-launch stderr log.
The configured command and effective arguments/settings are recorded. Teardown attempts browser close and
records whether forced process-scope termination was needed; every owned descendant must exit.

A Lightpanda configuration with `endpoint` instead of `executable` borrows a local endpoint and may optionally
name its `processId`. Borrowed adapters disconnect without terminating that process. They remain available
for smoke/raw diagnostics and are rejected by the comparison gate.

| Boundary | Included |
| --- | --- |
| Launch/connect | Owned process launch, endpoint readiness, client connection |
| Preparation | Browser identity queries, page creation, and the row's warmup, if any |
| Page work | Navigation to `load`, workload evaluation and correctness assertion; repeated only in the warm lane |
| Teardown | Page close, browser close/disconnect, and confirmed owned process-scope exit |
| Total lifecycle | Continuous launch-through-teardown clock, including observer/boundary overhead |

Complete installation/runtime manifests and workload export happen before the idle checks. Each adapter
and the harness are hashed again after collection; changed dependencies invalidate the entire run.
The Linux manifests include complete system library trees to cover native loaders, dynamic libraries,
child helpers and runtime snapshots, plus configured `dependencyRoots` for additional installations.
Symlinks are resolved and missing/unreadable files fail visibly; dynamic-loader overrides are rejected.
On macOS, application bundles are hashed but the OS shared runtime remains diagnostic-only.
Binary hashing never runs between an accepted idle check and its browser launch. Stage sums can differ from the continuous total
because scope snapshots and observer shutdown are explicit overhead. Cold results are not a whole-application
startup measurement; warm rows are not a claim about persistent browser caches or JIT convergence.

## Whole process accounting and supported platforms

Gate mode requires an **already delegated Linux cgroup v2 root** with CPU and memory controllers enabled.
The host administrator supplies an empty delegation; the tool does not alter parent cgroup policy or elevate
itself. The kernel must provide `cpu.stat`, `memory.current`, `memory.peak`, `cgroup.kill`, and population state.
Each launch receives a new UUID child scope. The browser starts only after entering that scope, and all of its
descendants remain accounted even after reparenting or exit. The client, fixture server and observer remain
outside it. Shell startup and the membership write occur before scope entry and are excluded from the
cgroup counters; browser exec and the remaining shell work after entry are included. The launch wall clock
also includes that initial shell startup.

PID/start-time observations are fallible diagnostics. The collector rechecks start time and membership,
records observed identity changes, missing fields and read failures, and labels successful observations
non-atomic. Such misses do not invalidate the independent kernel CPU/memory counters or imply that a process exited.

CPU is the scope's cumulative microsecond counter, including exited children. Memory is **charged cgroup
usage**, which can include page cache and kernel charges; it is not portable RSS. The launch, preparation, page-work and teardown peaks are each the maximum observed simultaneous
scope usage sampled continuously every 10 ms, with explicit stage labels, timestamps and boundary samples.
The kernel's lifetime scope peak is recorded separately. Never sum individual PID peaks, subtract lifetime
peaks, or compare different backends as though they measured the same memory. Sampling can miss transients;
the observer itself adds overhead outside the browser's scope.

| Platform/backend | Verification/raw diagnostics | Gate comparisons |
| --- | --- | --- |
| Linux with delegated cgroup v2 and required counters | Supported | Supported after the kernel verification leg passes |
| Linux without delegation/counters | Supported | Explicit refusal |
| macOS / Windows | Supported when the requested browser distribution exists | Explicit refusal until a complete accounting backend is implemented |

The portable root-PID snapshots remain labeled diagnostics and are never substituted for missing tree
counters. Native macOS Lightpanda availability does not imply that complete accounting is available there.
Container/VM results must identify that host boundary; do not treat an idle guest as proof its physical host
is idle. Use a dedicated idle host for campaign evidence.

## Controlled collection and analysis

```sh
python3 tools/browser-comparison/measure.py --config /absolute/path/config.json \
  --output /absolute/path/new-gate-directory --measure --rounds 6 --launches 3 \
  --scope-root /sys/fs/cgroup/already-delegated-comparison-root
python3 tools/browser-comparison/analyze.py /absolute/path/new-gate-directory > analysis.json
```

The driver requires all three owned browsers, a clean committed checkout, and exact identities. It freshly
builds Release, exports workload bytes, requires a fresh successful real kernel accounting validation, then uses the **same blocking idle validator source as BenchmarkDotNet**
(40% of one core, up to 45 seconds settling). A second Linux aggregate CPU check closes process-enumeration
blind spots. Checks bracket every launch. An idle refusal invalidates the collection and is preserved;
`JINT_BENCH_SKIP_IDLE_CHECK` is rejected. Unsupported platform/counter conditions fail before measurement.
This does not continuously monitor interference inside an individual workload; that limitation remains in
the evidence alongside the requirement for a dedicated host.

The Linux topology behavior matches the repository's unpinned fallback. No power plan is changed; fixed-clock
overrides are rejected. Production tiering/PGO stays enabled. Jint uses blocking workstation GC; other engines
retain their own GC. Inherited runtime tuning overrides are rejected rather than silently changing a lane.

Six rounds cover all six orders of the three browsers, with three independent launches per row per round.
This balances middle positions as well as first/last order. Execution is serial. The analysis first takes each
browser's median launch value within a round, then forms paired round percentage differences. It reports their
median and a 95% percentile bootstrap interval over paired rounds (10,000 resamples, seed 3575), preserving
all pairs. These are pointwise exploratory intervals, without correction for multiple comparisons. Launches are not treated as independent rounds. An interval crossing zero has unresolved direction;
an untouched control establishes the experiment's noise floor. There is no control subtraction.

Analysis rejects missing/duplicate/reordered pairs, changed binary/dependency identities, mismatched workloads,
failed idle checks, digest mismatches, incomplete teardown, regressing/missing counters and invalid memory
samples. The artifact directory keeps the configuration, source/binary hashes, actual Lightpanda `version`
output and release identity, runtime/host details (including Linux CPU topology, memory and clock policy), schedule, idle logs, per-launch raw JSON and errors.
`manifest.json` records each planned row before it starts and is updated atomically as rows finish.
A per-launch lifecycle journal records every stage and a structured terminal failure/cleanup/accounting
verdict. The collector separately persists its own scope-cleanup attempt, even if the runner times out. A failed or partial
manifest is never accepted by analysis. Preserve the whole directory, not just a rendered table.

## Validation and calibration

```sh
dotnet test tools/browser-comparison.Tests/BrowserComparison.Tests.csproj -c Release
python3 -m unittest discover -s tools/browser-comparison -p 'test_*.py'
JINT_BROWSER_COMPARISON_CGROUP_ROOT=/sys/fs/cgroup/already-delegated-comparison-root \
  python3 -m unittest discover -s tools/browser-comparison -p test_linux_accounting.py
```

The kernel leg verifies a CPU/memory child whose parent has exited and retained counters. The Release
adapter tests exercise the actual owned-process cleanup for cancellation, timeout, checksum assertion
and early launch failures, including reparented children in the delegated Linux CI leg.
It reports a skip when delegation is absent; that is not a passing kernel validation. Synthetic tests cover
scheduling, counter failures, pairing and confidence intervals without supplying performance evidence.
An empirical A/A calibration is available by adding `--calibrate jint` (the configured adapter name) to
the gate collection command. It runs two identical owned configurations through the same collector and
analysis. Calibration must run on the same dedicated machine before trusting small deltas; this
repository does not claim that synthetic zero-difference fixtures constitute that calibration.
