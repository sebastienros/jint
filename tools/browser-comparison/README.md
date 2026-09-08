# Offline browser comparison harness

This is the harness portion of [#3575 X4](https://github.com/sebastienros/jint/issues/3575), tracked in
[#3902](https://github.com/sebastienros/jint/issues/3902). One PuppeteerSharp client runs the same offline
load/evaluate/assert workload against **AngleSharp + Jint**, Chromium, and optionally an existing local
Lightpanda endpoint. It downloads nothing and reads no Lightpanda source.

This tool produces smoke results or raw diagnostics, **not publication-quality benchmark comparisons**.
Read [the benchmark instructions](../../Jint.Benchmark/AGENTS.md) before collecting or quoting numbers.
The process counters deliberately cover one identified PID; Chromium renderer/utility children are absent,
so the memory and CPU records cannot support a whole-browser efficiency comparison.

## Run a smoke check

Build the Jint browser executable in Release, copy `example.json`, and replace its absolute paths. A
standalone published `jint-browser` works too: give its path as `executable` and omit `arguments`. The
harness appends `serve --port 0`; do not include those arguments yourself. Chromium must already be installed.
No browser-fetcher or package download is performed by the harness.

```sh
dotnet build Jint.Browser.Tool/Jint.Browser.Tool.csproj -c Release -f net10.0
dotnet run --project tools/browser-comparison/BrowserComparison.csproj -c Release -- \
  --config /absolute/path/to/local-config.json --smoke
```

Smoke runs each adapter once and exits nonzero for a launch, connection, navigation, evaluation, assertion,
or teardown failure. It writes JSON provenance and `measurements: null`, with no elapsed/CPU/memory figures.
A single-adapter configuration is useful when Chromium is unavailable. Missing required binaries are an
error, never an implicit skip. Browser and fixture server processes are disposed even if assertions fail.
Every operation has a bounded timeout. Jint's existing page/script budgets remain in force.

The fixture contains no external resources: a loopback server delivers one checked-in document. Its script
builds 100 elements and computes a checksum. The common workload navigates until `load`, reads DOM values,
mutates a heading, and asserts the exact result `100|4950|4950|Verified 100`. There is no browser-specific
workload substitution. This small DOM smoke fixture is a starting workload, not a representative web corpus.

For optional Lightpanda, add an adapter such as:

```json
{
  "name": "lightpanda",
  "kind": "lightpanda",
  "endpoint": "ws://127.0.0.1:9222/EXACT_BROWSER_ENDPOINT",
  "versionLabel": "EXACT_RELEASE_OR_COMMIT",
  "processId": 12345
}
```

Use the actual browser WebSocket endpoint. The endpoint must be local because it navigates to the same
loopback fixture server. `processId` is optional; without it, process counters are null. If supplied, it must
identify the browser's local root process; the harness cannot prove that a socket belongs to that PID.
The endpoint is borrowed: the harness closes only its own page, disconnects, and never terminates Lightpanda.
Its lifecycle is marked `external-process-not-restarted`; its startup and state are **not** comparable to the
fresh Jint/Chromium processes. Record the exact external build using `versionLabel`.

## Boundaries and provenance

Each row launches a new owned Jint/Chromium process. There is no warmup, pooling, reused page, or claim of
steady-state performance. Multiple diagnostic rounds alternate adapter order to expose order effects; they
do not replace BenchmarkDotNet's machine controls, launch analysis, or the paired statistical methodology.
The fixture server and PuppeteerSharp client live in the harness process, outside the browser PID.

| Record | Included |
| --- | --- |
| Launch/connect elapsed | Process launch (owned adapters), endpoint discovery, PuppeteerSharp connect; Lightpanda is connect-only |
| Page-work elapsed | New page, navigation to `load`, evaluation, result assertion, page disposal |
| Teardown elapsed | Chromium close; Jint disconnect and forced process-tree termination; Lightpanda disconnect only |
| Work CPU | `TotalProcessorTime` difference for the named PID across page work; excludes startup and children |
| OS peak working set | OS lifetime high-water mark before/after page work; includes prior startup/state, is not a stage delta or managed allocation count |

| Sampled working-set peak | Maximum observed every 10 ms during page work, plus boundary samples; can miss short-lived peaks, with sample count recorded |

Binary hashing and browser-version queries happen outside page work. The report contains OS, architecture,
.NET version, harness informational version, pinned PuppeteerSharp informational version, fixture SHA-256,
browser-reported version, browser user agent, command arguments, executable hash, and hashes for Jint's
neighboring managed DLLs. Browser-reported versions plus binary hashes identify what ran; `versionLabel`
can additionally record a checkout SHA, distribution label, flags, or external build. Keep configurations
free of secrets because arguments are recorded. The ephemeral debugging endpoint is not emitted.

Unsupported/inaccessible process counters retain a reason rather than becoming zero. In particular, an OS
lifetime-peak counter returning zero is unavailable; the independent sampled maximum still works when
current working-set readings are supported. Sampling runs in the harness and contributes observation overhead. A borrowed endpoint
without a local PID has null snapshots. Root-only counters do not include the harness, fixture server,
Chromium renderers, GPU process, utility processes, or the sum of a process tree's peaks.

## Collect raw diagnostics

```sh
dotnet run --project tools/browser-comparison/BrowserComparison.csproj -c Release -- \
  --config /absolute/path/to/local-config.json --collect > local-diagnostics.json
```

`rounds` must be between 1 and 100. `--collect` is explicit and labels its output
`raw-diagnostics-not-publication-quality`. Run it on a dedicated idle machine. This harness does not change
CPU affinity, power plans, GC settings, or tiered compilation, and does not enforce the benchmark project's
idle-machine guard. Do not quote these raw observations as comparative benchmark results. Full process-tree
accounting, lifecycle-matched external browser launches, representative workloads, machine controls, and
repeatable statistical analysis remain X4 follow-up work.
