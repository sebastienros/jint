# HTTP cache measurement for #4236

This records the socket-free image workload for [#4236](https://github.com/sebastienros/jint/issues/4236).
It measures the cache's costs and network avoidance separately. It does not establish a production
speedup or resolve the image allocation investigation in #4013.

## Workload and comparison

The starting `origin/main` revision is `42e39e684`. Both sides use the same fixture: a document with 300 images,
each a deterministic 64 KiB PNG representation. Parameters select repeated or distinct image URLs
and disabled, memory or disk configuration. The baseline fixture keeps the same operation bodies,
including disk-row context reopening, but omits cache configuration and clearing because those APIs
do not exist on the starting revision. Its enum has the same names and values as the new public enum.
[The baseline fixture](baseline-workload.cs.txt) and [candidate source hashes](candidate-source-sha256.txt)
make that adaptation reviewable.

The measurements precede the subsequent CLI/MCP configuration wiring and the context-configuration
callback added for protocol-created contexts. The hashes identify the cache implementation measured here;
these data do not measure that later command-line integration. No small-effect performance claim is made
for the follow-up.

`ColdMiss` clears before navigating. `WarmHit` navigates again; disk rows first close and reopen the
context to exercise persistence. `StaleRevalidation` advances an injected clock by two hours and
navigates, with image validators answered by 304. The document itself always returns 200 with
`max-age=0`, ensuring the same parsing and image-loading work each time. Each row owns its fixture
and warms its own work. Control has a separate setup and parses sixteen source-less 300-image
documents without entering the transport, to put construction and parsing on a similar cost scale.
Engine/document setup and per-consumer body copies enter the measured operation. Sockets, actual
network latency and pixel decoding do not.

The final run uses Release net10.0 default BenchmarkDotNet jobs, three separate alternating pairs,
blocking workstation GC and production tiering/PGO. The machine is Apple M4 Pro, macOS 15.8.1,
.NET SDK 10.0.400 / runtime 10.0.11, BenchmarkDotNet 0.15.8. Both checkouts use the same SDK and runtime.
Affinity and process priority could not be applied on this host. Per the issue's human no-idle-wait
policy, `JINT_BENCH_SKIP_IDLE_CHECK=1` bypasses idle waiting. These observations are **not gate-quality**.
The earlier exploratory runs overlapped validation or preceded final fixes and are excluded.

The paired report uses the repository's fixed-seed, 5,000-resample percentile bootstrap of the median
per-pair percentage difference. Three pairs can reveal large effects; their interval is effectively
bounded by the observed range. Small changes require six or more pairs and an unaffected control
before any regression or improvement claim. Neither a small median nor an isolated interval excluding
zero establishes a small effect here.

```sh
# Ensure the same dotnet executable is first on PATH for both checkouts.
JINT_BENCH_SKIP_IDLE_CHECK=1 pwsh -NoProfile -File Jint.Benchmark/measure-paired.ps1 \
  -Baseline /path/to/starting-revision -Candidate /path/to/candidate \
  -Filter 'Jint.Benchmark.BrowserHttpCacheBenchmark.*' -Rounds 3 -OutputRoot /tmp/http-cache-paired
```

## Timing and allocation results

All six runs completed all 24 rows. The [full paired table](paired-table.md) includes every operation and
control, with median per-arm times, paired deltas, intervals and allocation. The per-arm columns are
medians of the three reported values; the delta is the median of per-pair percentage differences, so it
need not equal the ratio of the displayed per-arm times. Allocation units retain BenchmarkDotNet's MB label.
The [runner output](paired-analysis.txt) records its fixed-seed bootstrap calculation. Its mechanical
FASTER/SLOWER labels alone do not establish small effects with three pairs.

Raw CSV exports remain local and are excluded from this pull request. The paired table, analysis and
workload observations are included for review; the command above reproduces the exports.
[Timing-quality notes](timing-quality.md) identify six rows with MValue above 2.8. In particular, one
warm distinct-memory row and two cold distinct-disk rows have multiple timing modes. These shared-machine
observations are descriptive and are not a production performance gate.

This socket-free workload shows cache overhead rather than a speedup. For distinct URLs, warm memory
has a median 8.074 ms operation and a paired +16.31% difference [10.43%, 23.74%]; warm disk, including
context reopening and entry restoration, has a median 32.565 ms operation and +387.70% [378.33%, 406.67%].
Cold distinct-memory capture costs +26.47% [26.18%, 28.45%]. Distinct-disk cold storage and validation
are much more expensive because they create, checksum and publish 300 entry files. This fixture has no
network latency or bandwidth cost to offset that work. It cannot predict end-to-end savings on a real site.

Distinct-memory allocation rises from 60.20 MB to 113.51 MB for cold capture and remains 60.49 MB on a
warm hit, against 60.20 MB uncached. Warm disk allocates 82.02 MB, including restored bodies and metadata.
Avoiding transferred image bodies does not remove per-image consumer allocation. No improvement or
regression claim is made for the small disabled-cache differences: controls move in both directions,
and six or more pairs would be needed to investigate those small effects.

## Counts, bytes and retained heap

The correctness driver runs outside BenchmarkDotNet timing:

```sh
dotnet run --project Jint.Benchmark/Jint.Benchmark.csproj -c Release -f net10.0 -- --validate-http-cache
```

[Raw observations](workload-observations.txt) report request counts, transferred body bytes, validating
requests, whole-process allocated bytes and retained-heap observations separately. The driver asserts
that all 300 images complete with their expected width, that warm cached images cause no transport
requests, and that the control makes no requests. The document's bytes are included in body totals.
The server/handler counters count bodies actually handed to the transport; they do not estimate
HTTP header, framing or socket bytes.

| Operation | Disabled requests | Cached, repeated URLs | Cached, distinct URLs |
| --- | ---: | ---: | ---: |
| Cold | 301 | 2 | 301 |
| Warm | 301 | 1 | 1 |
| Stale | 301 | 2 (one image 304) | 301 (300 image 304s) |

Warm and validated image responses transfer no image body bytes. The unchanged document still
transfers its body. Disk and memory have the same request/body-count outcomes. These are deterministic
counter assertions, independent of the timing comparison.

The retained-heap probe forces collections before and after clearing a warmed cache, with the page
still open, and also records process heap differences around each operation. These are whole-process
GC observations, **not measurements of cache-owned bytes**: consumer buffers, engine generations,
asynchronous handoffs and collector timing contribute. Their large variation must not be presented as
a precise cache footprint. Configured byte budgets cover stored bodies, estimated metadata and
pending capture reservations; the workload's 300 distinct stored image bodies alone total 19,660,800
bytes. Per-consumer copies remain in the page workload after a hit.

## Correctness checks

The final source builds the complete solution in Release with zero warnings and errors. With host-contract
verification enabled, the cache and Request fixtures pass 210 tests across .NET 8 and .NET 10; browser cache,
Network and Fetch domain fixtures pass 130 tests across those frameworks. Broader Web API, browser
navigation/parsing/worker, public-interface and DevTools runs also passed during implementation.
