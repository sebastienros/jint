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

The `FormData(form)` compatibility failure originally found by the unchanged `event-form` workload
([#3931](https://github.com/sebastienros/jint/issues/3931)) is resolved. The collector and correctness smoke
landed in [#3941](https://github.com/sebastienros/jint/pull/3941). The remaining X4 acceptance work is an
empirical Jint A/A calibration and the complete three-browser comparison on an idle-accepted Linux host
with delegated cgroup v2, including the scoped hosted-VM route below, followed by independent
reproduction, retained evidence and a scoped conclusion in
[#3930](https://github.com/sebastienros/jint/issues/3930).

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

Complete installation/runtime manifests and workload export happen before the idle checks. A single batch
hashes the union of every configured adapter and harness installation plus system library roots once.
Each adapter retains its own executable, arguments and version label, with the same conservative superset
of dependency hashes. Coverage metadata explicitly identifies the batch union; analysis requires identical
union contents for every adapter and the harness. An independent fresh batch after collection hashes the
whole union again; there is no cache across phases, and changed dependencies invalidate the entire run.
The batch setup deadline is 1,800 seconds, bounded separately from measurement: hosted diagnostics showed
steady progress through 130,796 files / 9.47 GB at 870 seconds before the former 900-second identity limit.
No measurement-window, idle-check or accounting deadline changes.
The Linux manifests include complete system library trees to cover native loaders, dynamic libraries,
child helpers and runtime snapshots, plus configured `dependencyRoots` for additional installations.
The one declared exclusion is canonical `/etc/ssl/private` and its descendants: this private-key
store is reached by Ubuntu's `/usr/lib/ssl/private` directory link, but is not a library installation.
Its contents are never enumerated or opened. The manifest records the exact boundary and reason,
and analysis rejects absent or broadened exclusion metadata. Explicit executables, file arguments or
configured dependency roots within that boundary are refused. Native helpers and external library
symlinks remain covered, and every other missing/unreadable file still fails visibly.
Symlinks are resolved before checking the boundary; dynamic-loader overrides are rejected.
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
is idle. A dedicated idle physical host gives stronger isolation; hosted-VM campaign evidence must retain
that limitation, pass the same guards and accounting checks, and independently reproduce.

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
the evidence alongside the recorded physical-host or hosted-VM boundary.

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
dotnet test --project tools/browser-comparison.Tests/BrowserComparison.Tests.csproj -c Release
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
analysis. Calibration must run on the same machine (the same VM for a hosted run) before trusting small deltas; this
repository does not claim that synthetic zero-difference fixtures constitute that calibration.

## Manual hosted-runner feasibility

`.github/workflows/browser-comparison-hosted.yml` offers `verify`, `calibrate`, and `compare` dispatches
on `ubuntu-24.04`, with a 360-minute job ceiling and no parallel measurement jobs. This is a hosted-VM
feasibility experiment, not evidence that its physical host is dedicated or idle. An accepted guest idle
check cannot observe noisy physical neighbors. Keep this boundary alongside any conclusions. Campaign
acceptance also requires an independent repeat under the agreed criteria; a single successful hosted run
does not establish reproducibility or guarantee that a later runner will qualify.

`verify` installs the reviewed `hosted-pins.json` assets and runs correctness only. `calibrate` runs Jint
A/A through the existing six-round, three-launch collector. `compare` first performs that calibration,
then the full three-browser collection serially on the **same VM**. Inspect the calibration intervals and
untouched controls before interpreting differences; a completed calibration is not an automatic noise-floor
acceptance verdict. A refusal or timeout is feasibility evidence, never a timing result. No gate is weakened,
no service unrelated to this run is disabled, and no physical-host control is claimed.

The optional `browser_pins` JSON dispatch input replaces the reviewed pins in full. Full Chrome for Testing
must have a versioned official linux64 ZIP URL, exact version and independently reviewed archive SHA256.
Lightpanda must have an official release asset API URL, matching asset ID and SHA256 of the executable.
The checked-in pins were resolved before measurement; a mutable nightly alias is never sufficient.
The default Lightpanda asset is the Linux x86_64 binary from
[stable release 0.4.1](https://github.com/lightpanda-io/browser/releases/tag/0.4.1).
The earlier pinned nightly asset was removed upstream before calibration; changing the pin requires
fresh correctness verification, and its results must not be mixed with the earlier binary's evidence.
`setup_hosted.py` verifies downloaded bytes **before execution**, preserves download/executable hashes,
records actual version output, and creates the collector configuration outside the clean checkout.
No Lightpanda source is accessed. A removed asset or digest mismatch fails without resolving a replacement.

Ubuntu's user-namespace restriction needs a scoped allowance for downloaded Chrome. The workflow uses
[Chromium's documented AppArmor option 2](https://chromium.googlesource.com/chromium/src/+/main/docs/security/apparmor-userns-restrictions.md),
with an exact canonical executable path instead of a glob. A run/attempt-specific root-owned profile
allows `userns` for that executable; the path alphabet rejects AppArmor patterns and variables.
Chrome's sandbox stays enabled, no global sysctl is changed, and no unrelated profile is reloaded.
The generated policy, its hash, and load/removal logs remain in the artifact. Cleanup removes only the
profile installed by this run, after stopping its measurement service. No AppArmor setup runs locally.

Only the measurement invocation runs in a transient systemd unit. Administrative creation delegates CPU and
memory controllers to that unit; `DelegateSubgroup=controller` keeps the unprivileged collector out of the
empty parent. `run_hosted.sh` enables controllers only in its own delegation and creates the empty measurement
root there. The unchanged collector still creates a fresh child scope per owned browser launch and runs
its real kernel validation. Unit termination cleans only that invocation's descendants. No collector code
acquires elevated privileges or changes a parent cgroup policy.

Always-uploaded artifacts include dispatch pins, runner image and source identity, browser archives/binaries,
setup manifest, build and driver logs, full calibration/comparison directories, accepted analysis or partial
failure evidence. Retention is 30 days; retain the artifact independently before using it for campaign closure.
The setup helper's archive, URL and digest rejection tests run without network or browser execution:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/browser-comparison -p test_setup_hosted.py
```

The hosted image `20260907.300.1` contained three dangling LLDB18 Python library links, all owned by
`python3-lldb-18`; strict system-library hashing correctly refused verification. The setup preflight inventories
all dangling links and installed package versions. It permits only those exact paths, targets and ownership,
then simulates removal of the unused `python3-lldb-18` package without autoremove. The removal set must be a
subset of `python3-lldb-18`, `lldb-18`, and `lldb`, contain the defective package, and install/configure nothing.
The same package-manager removal is then applied, actual package changes must match the simulation, and a
fresh inventory must contain no dangling links. Unknown defects fail before mutation. This is recorded setup
before binary manifests or measurements; it neither skips library hashes nor changes the collector.
`library-preflight/` retains link inventories, package versions, apt logs and the terminal repair verdict.
The workflow's `inspect` phase (or the helper's `--inspect`) records evidence without any package mutation.
