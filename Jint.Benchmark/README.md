# Engine comparison benchmarks

This project benchmarks Jint against the other JavaScript engines available to .NET applications:
the managed engines ([NiL.JS](https://github.com/nilproject/NiL.JS),
[Okojo](https://github.com/akeit0/okojo) and [YantraJS](https://github.com/yantrajs/yantra)) and
[ClearScript](https://github.com/ClearFoundry/ClearScript) — Microsoft-originated, now
ClearFoundry-maintained bindings to Google's native V8 engine (the JIT inside Chrome and Node.js) —
across a set of representative scripts.

## How each engine executes

The engines reach the result in different ways, which shapes the numbers below:

* **Jint** — tree-walking interpreter over a prepared AST.
* **NiL.JS** — interpreter (with an optimizing pass over its syntax tree).
* **Okojo** — interpreter that compiles the script to bytecode and runs it on a virtual machine.
* **YantraJS** — compiler: it emits .NET IL, which the CLR then JIT-compiles to native code.
* **ClearScript (V8)** — native V8 behind a managed ↔ native interop bridge: a multi-tier
  optimizing JIT running outside the CLR, with every host interaction crossing the native boundary.

The structural consequences shape the whole table. Only the two compilers (YantraJS and V8) run
ahead on long, tight numeric/call loops, where compiled code approaches native speed. The
interpreters own the other end of the trade: engine start-up and small scripts, where even a
context created from a pre-warmed V8 isolate costs ~460 µs for work Jint completes in about a
microsecond. And pure-JS compute is only half of an embedding story — the other half is the
script ↔ host boundary, measured separately in the
[interop section](#script--host-interop) below, where the price of V8's native boundary inverts
the picture.

## The scripts

One variant per workload, 12 scripts in total. Where the suite previously carried both a classic
ES5 script and an ES2015+ `-modern` rewrite of it, only the modern variant is kept — that is how
JavaScript is written today, and the duplicated rows ranked the engines nearly identically while
doubling the wall-clock time of a full run. (The classic scripts remain in `Scripts/` and are still
used by the Jint-only benchmarks.)

## Running the benchmarks

Run from **this** directory (the scripts are loaded relative to the working directory):

```
dotnet run -c Release -- --allCategories EngineComparison
```

Notes:

* The `--` separator is required so the arguments are forwarded to BenchmarkDotNet instead of being
  consumed by `dotnet run`.
* `--allCategories EngineComparison` runs both the script suite and the interop suite; use
  `--allCategories EngineComparisonInterop` for the interop suite alone, or
  `--filter "*EngineComparisonBenchmark*"` for the script suite alone.
* Results are written to `BenchmarkDotNet.Artifacts/results/` — the
  `*-report-github.md` file is the table reproduced below.
* To re-measure a single engine (e.g. after a package bump) without disturbing the rest of the
  table, filter to its lane, for example `--filter "*EngineComparisonBenchmark.Okojo*"`.
* The benchmark config widens the parameter column (`MaxParameterColumnWidth = 40`) so the full
  script names are printed instead of BenchmarkDotNet's default truncation (e.g.
  `dromaeo-object-string-modern` rather than `droma(...)odern [28]`).

## How to read the table

* All engines are run in **global strict mode** — YantraJS is strict-only, and Okojo and
  ClearScript have no engine-level strict switch, so their source carries a leading `"use strict"`
  directive. Strict mode improves performance across the board.
* Every operation uses a **fresh engine** — the embedding pattern where executions must not leak
  state into each other.
* Three engines have a **cached-artifact lane** next to the re-parse lane, and the pairs mean the
  same thing: `Jint_ParsedScript` reuses a `Prepared<Script>` produced once by
  `Engine.PrepareScript`; `Okojo_Prepared` reuses a parsed program (Okojo's realm-independent
  artifact) and re-compiles the bytecode against each run's fresh realm; `ClearScript_Compiled`
  reuses a `V8Script` compiled once by a shared `V8Runtime` and runs it in a fresh script engine (a
  fresh V8 context) created from that runtime. The gap to the re-parse lane is parsing/compilation
  cost — **in production you should cache the prepared artifact**, which is what these lanes
  represent.
* The plain `ClearScript` lane creates a full V8 isolate + context per operation — the honest cost
  when each request gets a fully isolated engine, and the reason its short-script rows start around
  a millisecond. `ClearScript_Compiled` shares one isolate (and its warmed JIT state) across
  operations while still using a fresh context per operation — ClearScript's recommended production
  path.
* **`Allocated` counts managed memory only.** ClearScript's working memory lives on V8's native
  heap, which the managed diagnoser cannot see — the few dozen KB in its rows are interop-bridge
  overhead, not a memory-use figure comparable with the managed engines. Memory claims in this
  document therefore compare the managed engines with each other.
* V8 runs background threads (tiered JIT compilation, garbage collection). `Mean` is the wall-clock
  time the executing thread observes; total CPU consumed is higher than for the single-threaded
  managed engines, which matters on saturated servers.
* `Mean` is time per operation (lower is better); `Rank` groups results that are statistically
  tied. The separately measured ClearScript rows are merged into each script's block by mean.
* The `dromaeo-object-regexp-modern` row is the highest-variance in the table (for Jint it is
  dominated by .NET `Regex`); treat small gaps there — including ClearScript's fresh-engine lane
  appearing ahead of its compiled lane — as run-to-run noise.

## At a glance (Jint 4.13.0)

Using each engine's recommended production path (for Jint a cached prepared script,
`Jint_ParsedScript`; for ClearScript a precompiled `V8Script` on a shared runtime,
`ClearScript_Compiled`):

* **The table splits cleanly down the middle between Jint and native V8: each is the fastest
  engine on 6 of the 12 scripts.** A tree-walking managed interpreter beating or matching the JIT
  inside Chrome on half the field is the headline; no other managed engine wins a single row.
* **Jint wins everything start-up-shaped and everything eval-shaped.** `minimal` runs in
  **1.0 µs vs V8's 461 µs (~450×)** — and ~1,200× against a cold V8 isolate; `evaluation-modern`
  is **~107×** ahead; loading the LINQ.js library (`linq-js`) is **~10×** ahead. On
  `dromaeo-core-eval-modern` — code built around `eval`, which no compile cache can help —
  Jint is **1.35×** ahead of V8, and it also leads `dromaeo-object-array-modern` (**1.07×**) and
  `array-stress` (statistically tied).
* **V8 wins the long compute rows**: `dromaeo-3d-cube-modern` (2.5×), `json-parse-modern` (1.7×),
  `stopwatch-modern` (5.6×) and the two string-crunching rows, `dromaeo-object-string-modern`
  (8.8×) and `dromaeo-string-base64-modern` (9.7×). The `dromaeo-object-regexp-modern` gap
  (1.06×) is within that row's variance.
* **Jint is the fastest managed engine on 10 of 12 scripts** (the IL-compiling YantraJS leads
  `dromaeo-3d-cube-modern` and `stopwatch-modern`) and **the fastest interpreter on all 12**.
* **Among the managed engines, Jint and Okojo allocate the least** — NiL.JS and YantraJS allocate
  one to two orders of magnitude more (up to 471× more than Jint on
  `dromaeo-string-base64-modern`), which means far heavier GC pressure in real applications.
  (ClearScript's rows cannot be compared here — its memory lives on the V8 native heap, which the
  managed diagnoser does not see.)
* **Pure-JS compute is where V8 is strongest — and it is still only half the story.** The
  [interop suite](#script--host-interop) below measures the other half, the script ↔ host
  boundary, where the picture inverts.

## Script ↔ host interop

Embedding a JavaScript engine is rarely about pure computation — scripts exist to drive the host
application, and in interop-heavy systems the script ↔ host boundary dominates. This is also where
the engines differ structurally: the managed engines dispatch host calls in-process, while every
ClearScript host interaction crosses the managed ↔ native V8 boundary and marshals its arguments
across it.

`EngineComparisonInteropBenchmark` (run with `--allCategories EngineComparisonInterop`) drives
four byte-identical scripts against each engine: a host method-call loop, a host property
read/write loop, strings crossing the boundary, and traversal of a host `int[]`. Details that
keep the comparison fair:

* Host members are lowercase (`host.add`, `host.value`, …) because YantraJS camel-cases CLR
  member names while the other engines surface them verbatim — already-lowercase names are the
  fixed point of both conventions, so every engine runs the same source.
* Each script validates its final aggregate and throws on a mismatch, so an engine that silently
  mis-marshals (undefined, NaN) fails loudly instead of posting a fantasy time.
* `ClearScript` binds the host object with plain `AddHostObject` (reflection-based, like the
  managed engines); `ClearScript_FastProxy` uses ClearScript 7.5's FastProxy API — explicit
  member registration with zero-allocation marshaling for fundamental types — its recommended
  path for hot host objects.
* Okojo is absent: 0.1.2-preview.1 provides no public way to enable CLR access.
* As above, `Allocated` is meaningful for the managed engines but only counts bridge overhead
  for the two ClearScript lanes.

What the numbers show:

* **The managed engines win every interop row.** Crossing the native boundary costs plain
  ClearScript **5.7×–7.0×** against Jint on method calls, property access and string passing —
  the mirror image of the pure-compute table, and the half of the story that matters most in
  chatty embedding scenarios.
* **FastProxy narrows the gap but does not close it**: still **2.4×–3.6×** behind Jint on those
  rows, and it trades away convenience — every member is hand-registered instead of reflected.
  Its zero-allocation claim is real, though: 13 KB managed allocation on the method-call row
  where reflective ClearScript burns 12.8 MB.
* **Among the managed engines the interop crown is shared**: NiL.JS leads method calls and
  collection traversal, YantraJS property access and string passing, with Jint close behind on
  all three of the non-traversal rows.
* **The collection-traversal row is Jint's weak spot, and the fix is one line of script.** The
  shared script deliberately re-reads `host.numbers` inside the loop, and Jint re-wraps the host
  array on every read (32.9 MB allocated). Hoisting the collection into a local
  (`var numbers = host.numbers;`) drops Jint from 19 ms to **~1.1 ms / 0.3 MB** — faster than
  every other engine on this row. Caching repeated wraps of the same host object is a Jint
  improvement opportunity this suite now tracks.

| Method                | FileName                     | Mean        | StdDev      | Rank | Allocated   |
|---------------------- |----------------------------- |------------:|------------:|-----:|------------:|
| NilJS                 | interop-collection-traversal |  1,356.3 us |    23.93 us |    1 |  4088.14 KB |
| YantraJS              | interop-collection-traversal |  4,001.4 us |    32.28 us |    2 |  5399.83 KB |
| ClearScript_FastProxy | interop-collection-traversal | 10,156.4 us |   231.71 us |    3 |  1185.69 KB |
| ClearScript           | interop-collection-traversal | 13,118.8 us |    42.26 us |    4 |  5016.25 KB |
| Jint                  | interop-collection-traversal | 19,023.3 us | 2,959.99 us |    5 | 32909.04 KB |
|                       |                              |             |             |      |             |
| NilJS                 | interop-method-calls         |  1,340.9 us |    13.28 us |    1 |  2437.94 KB |
| YantraJS              | interop-method-calls         |  2,189.6 us |    18.99 us |    2 |  3355.13 KB |
| Jint                  | interop-method-calls         |  2,522.6 us |    37.61 us |    3 |  1892.97 KB |
| ClearScript_FastProxy | interop-method-calls         |  6,138.3 us |    30.53 us |    4 |    12.95 KB |
| ClearScript           | interop-method-calls         | 17,593.5 us |   135.49 us |    5 | 12752.52 KB |
|                       |                              |             |             |      |             |
| YantraJS              | interop-property-access      |  1,540.4 us |    11.38 us |    1 |  1870.68 KB |
| NilJS                 | interop-property-access      |  1,873.0 us |    10.42 us |    2 |  4391.52 KB |
| Jint                  | interop-property-access      |  2,239.4 us |    23.47 us |    3 |   797.57 KB |
| ClearScript_FastProxy | interop-property-access      |  5,841.7 us |    55.72 us |    4 |    12.58 KB |
| ClearScript           | interop-property-access      | 14,606.0 us |   331.24 us |    5 | 10561.79 KB |
|                       |                              |             |             |      |             |
| YantraJS              | interop-string-passing       |    661.0 us |     4.60 us |    1 |  1823.71 KB |
| NilJS                 | interop-string-passing       |    844.2 us |     4.52 us |    2 |  1179.64 KB |
| Jint                  | interop-string-passing       |  1,098.1 us |     7.00 us |    3 |   383.95 KB |
| ClearScript_FastProxy | interop-string-passing       |  3,986.1 us |    36.28 us |    4 |   247.16 KB |
| ClearScript           | interop-string-passing       |  6,234.2 us |    42.55 us |    5 |  2470.18 KB |

## Engine versions

* Jint 4.13.0
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.406
* Microsoft.ClearScript.V8 7.5.1

Jint, NiL.JS and YantraJS numbers are from the 4.13.0 release run; the Okojo rows were measured
separately on the same machine and .NET runtime and merged in. The ClearScript rows and the interop
table were measured 2026-07-20 on the same machine and .NET runtime. Last updated 2026-07-20.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method               | FileName                     | Mean             | StdDev         | Rank | Allocated     |
|--------------------- |----------------------------- |-----------------:|---------------:|-----:|--------------:|
| Jint_ParsedScript    | array-stress                 |     2,256.761 us |      7.1240 us |    1 |    1060.39 KB |
| Jint                 | array-stress                 |     2,315.740 us |     14.6480 us |    1 |    1089.12 KB |
| ClearScript_Compiled | array-stress                 |     2,385.5 us   |    123.68 us   |    1 |       8.14 KB |
| YantraJS             | array-stress                 |     2,792.018 us |     27.8405 us |    2 |   17123.82 KB |
| ClearScript          | array-stress                 |     3,975.7 us   |    117.77 us   |    3 |      15.88 KB |
| NilJS                | array-stress                 |     5,133.058 us |     18.8123 us |    4 |    4521.19 KB |
| Okojo_Prepared       | array-stress                 |     5,434.204 us |     40.9309 us |    5 |    2681.62 KB |
| Okojo                | array-stress                 |     5,494.069 us |     43.4340 us |    5 |    2697.39 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-3d-cube-modern       |     1,894.5 us   |     33.83 us   |    1 |       9.32 KB |
| YantraJS             | dromaeo-3d-cube-modern       |     2,455.419 us |     10.9833 us |    2 |    7514.24 KB |
| ClearScript          | dromaeo-3d-cube-modern       |     3,986.9 us   |     52.25 us   |    3 |      14.53 KB |
| Jint_ParsedScript    | dromaeo-3d-cube-modern       |     4,766.692 us |     20.9342 us |    4 |    1340.62 KB |
| Jint                 | dromaeo-3d-cube-modern       |     5,081.631 us |     21.2716 us |    5 |    1644.48 KB |
| NilJS                | dromaeo-3d-cube-modern       |     7,095.752 us |     35.0040 us |    6 |    5977.95 KB |
| Okojo_Prepared       | dromaeo-3d-cube-modern       |     7,321.080 us |     18.7803 us |    7 |    2311.29 KB |
| Okojo                | dromaeo-3d-cube-modern       |     7,321.862 us |     28.8572 us |    7 |    2496.04 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | dromaeo-core-eval-modern     |       909.369 us |      5.7235 us |    1 |     340.21 KB |
| Jint                 | dromaeo-core-eval-modern     |       926.399 us |      2.4254 us |    1 |     359.93 KB |
| ClearScript_Compiled | dromaeo-core-eval-modern     |     1,228.0 us   |      8.54 us   |    2 |       8.17 KB |
| NilJS                | dromaeo-core-eval-modern     |     1,435.926 us |      5.1354 us |    3 |    1575.94 KB |
| ClearScript          | dromaeo-core-eval-modern     |     2,536.1 us   |     21.49 us   |    4 |      12.91 KB |
| YantraJS             | dromaeo-core-eval-modern     |     4,866.540 us |     51.7995 us |    5 |   35784.84 KB |
| Okojo                | dromaeo-core-eval-modern     |     5,144.141 us |     62.8990 us |    6 |    4627.67 KB |
| Okojo_Prepared       | dromaeo-core-eval-modern     |     5,225.851 us |     78.2768 us |    6 |    4613.46 KB |
|                      |                              |                  |                |      |               |
| Jint                 | dromaeo-object-array-modern  |    14,258.293 us |    154.7903 us |    1 |    9165.37 KB |
| Jint_ParsedScript    | dromaeo-object-array-modern  |    14,570.784 us |    167.2733 us |    1 |    9117.79 KB |
| ClearScript_Compiled | dromaeo-object-array-modern  |    15,663.4 us   |     95.13 us   |    2 |      16.66 KB |
| ClearScript          | dromaeo-object-array-modern  |    20,471.0 us   |    213.00 us   |    3 |     110.06 KB |
| YantraJS             | dromaeo-object-array-modern  |    24,713.087 us |    480.4059 us |    4 |  223803.18 KB |
| Okojo_Prepared       | dromaeo-object-array-modern  |    39,672.749 us |     82.5583 us |    5 |    6984.54 KB |
| Okojo                | dromaeo-object-array-modern  |    40,965.505 us |     63.8418 us |    6 |    7013.55 KB |
| NilJS                | dromaeo-object-array-modern  |    51,511.299 us |    140.7231 us |    7 |   17863.19 KB |
|                      |                              |                  |                |      |               |
| ClearScript          | dromaeo-object-regexp-modern |   103,294.6 us   |    910.06 us   |    1 |      32.74 KB |
| ClearScript_Compiled | dromaeo-object-regexp-modern |   119,046.5 us   |  5,092.80 us   |    2 |      15.58 KB |
| Jint_ParsedScript    | dromaeo-object-regexp-modern |   125,900.462 us |  5,143.9732 us |    2 |  157138.22 KB |
| Jint                 | dromaeo-object-regexp-modern |   127,541.094 us |  6,915.9485 us |    2 |  156436.69 KB |
| NilJS                | dromaeo-object-regexp-modern |   527,152.460 us |  6,296.1280 us |    3 |  767231.57 KB |
| YantraJS             | dromaeo-object-regexp-modern |   707,109.067 us |  8,559.8541 us |    4 |  831286.49 KB |
| Okojo                | dromaeo-object-regexp-modern | 1,825,988.240 us | 12,449.7110 us |    5 | 1801468.92 KB |
| Okojo_Prepared       | dromaeo-object-regexp-modern | 1,853,339.280 us | 15,718.2952 us |    5 | 1799047.36 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-string-modern |     5,940.5 us   |    115.62 us   |    1 |      15.81 KB |
| ClearScript          | dromaeo-object-string-modern |     8,775.3 us   |    101.99 us   |    2 |       25.3 KB |
| Jint                 | dromaeo-object-string-modern |    52,281.562 us |  1,265.5325 us |    3 |   21495.39 KB |
| Okojo_Prepared       | dromaeo-object-string-modern |    54,182.270 us |    876.1328 us |    3 |   33451.08 KB |
| Okojo                | dromaeo-object-string-modern |    54,754.242 us |  1,330.5147 us |    3 |   33538.01 KB |
| Jint_ParsedScript    | dromaeo-object-string-modern |    56,936.391 us |  1,731.9417 us |    3 |   21317.66 KB |
| NilJS                | dromaeo-object-string-modern |   142,747.049 us |  3,997.6165 us |    4 | 1355011.41 KB |
| YantraJS             | dromaeo-object-string-modern |   169,179.149 us |  3,759.4630 us |    5 | 1656324.17 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-string-base64-modern |     2,656.1 us   |     42.79 us   |    1 |       8.84 KB |
| ClearScript          | dromaeo-string-base64-modern |     4,658.0 us   |     88.66 us   |    2 |      15.31 KB |
| Jint_ParsedScript    | dromaeo-string-base64-modern |    25,894.140 us |     54.7045 us |    3 |    1620.97 KB |
| Jint                 | dromaeo-string-base64-modern |    27,010.827 us |     60.4811 us |    4 |    1721.37 KB |
| Okojo_Prepared       | dromaeo-string-base64-modern |    28,505.601 us |    197.3657 us |    5 |   43745.96 KB |
| Okojo                | dromaeo-string-base64-modern |    31,410.661 us |    150.0738 us |    6 |   43824.53 KB |
| YantraJS             | dromaeo-string-base64-modern |    32,057.062 us |    484.4823 us |    6 |  764771.13 KB |
| NilJS                | dromaeo-string-base64-modern |    32,844.012 us |    344.3122 us |    6 |   31360.29 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | evaluation-modern            |         4.314 us |      0.0139 us |    1 |      17.16 KB |
| Jint                 | evaluation-modern            |        13.762 us |      0.0431 us |    2 |      28.28 KB |
| NilJS                | evaluation-modern            |        25.719 us |      0.1213 us |    3 |      22.35 KB |
| YantraJS             | evaluation-modern            |       127.944 us |      0.8540 us |    4 |      703.4 KB |
| ClearScript_Compiled | evaluation-modern            |       461.1 us   |      2.25 us   |    5 |        6.1 KB |
| ClearScript          | evaluation-modern            |     1,252.3 us   |     13.64 us   |    6 |      10.97 KB |
| Okojo_Prepared       | evaluation-modern            |     1,552.680 us |     35.7149 us |    7 |    1283.36 KB |
| Okojo                | evaluation-modern            |     1,637.282 us |     28.8504 us |    7 |     1290.5 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | json-parse-modern            |    10,429.5 us   |    124.76 us   |    1 |      10.15 KB |
| ClearScript          | json-parse-modern            |    12,448.8 us   |    241.20 us   |    2 |      17.69 KB |
| Jint_ParsedScript    | json-parse-modern            |    17,514.534 us |     56.3865 us |    3 |   21377.29 KB |
| Jint                 | json-parse-modern            |    17,530.900 us |     60.9269 us |    3 |   21412.83 KB |
| YantraJS             | json-parse-modern            |    24,620.339 us |    281.6592 us |    4 |   43167.22 KB |
| Okojo                | json-parse-modern            |    26,597.647 us |    284.9263 us |    5 |   27271.68 KB |
| Okojo_Prepared       | json-parse-modern            |    26,846.415 us |     56.2260 us |    5 |   27235.28 KB |
| NilJS                | json-parse-modern            |   125,067.457 us |    472.8780 us |    6 |   67095.19 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | linq-js                      |        56.124 us |      0.1793 us |    1 |     199.59 KB |
| YantraJS             | linq-js                      |       322.556 us |      1.9429 us |    2 |    1049.75 KB |
| ClearScript_Compiled | linq-js                      |       555.1 us   |     10.31 us   |    3 |       6.21 KB |
| Jint                 | linq-js                      |     1,172.644 us |      4.8812 us |    4 |    1298.82 KB |
| ClearScript          | linq-js                      |     2,257.4 us   |     21.01 us   |    5 |      10.97 KB |
| NilJS                | linq-js                      |     3,825.634 us |     12.0575 us |    6 |    2739.46 KB |
| Okojo_Prepared       | linq-js                      |     7,080.933 us |    327.1464 us |    7 |    4131.48 KB |
| Okojo                | linq-js                      |     8,180.639 us |     92.8429 us |    8 |    4928.45 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | minimal                      |         1.023 us |      0.0079 us |    1 |       9.33 KB |
| Jint                 | minimal                      |         2.156 us |      0.0666 us |    2 |      11.26 KB |
| NilJS                | minimal                      |         2.910 us |      0.0167 us |    3 |       4.51 KB |
| YantraJS             | minimal                      |       127.611 us |      1.1214 us |    4 |     697.62 KB |
| ClearScript_Compiled | minimal                      |       460.6 us   |      4.61 us   |    5 |        6.1 KB |
| ClearScript          | minimal                      |     1,235.0 us   |     14.77 us   |    6 |      10.97 KB |
| Okojo                | minimal                      |     1,432.968 us |     40.8994 us |    7 |     1249.1 KB |
| Okojo_Prepared       | minimal                      |     1,448.889 us |     38.0131 us |    7 |    1247.19 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | stopwatch-modern             |    15,999.1 us   |    238.49 us   |    1 |       8.82 KB |
| ClearScript          | stopwatch-modern             |    20,285.1 us   |    291.74 us   |    2 |      22.64 KB |
| YantraJS             | stopwatch-modern             |    59,827.731 us |    223.9642 us |    3 |  234033.07 KB |
| Jint_ParsedScript    | stopwatch-modern             |    89,643.204 us |    378.7984 us |    4 |   12087.93 KB |
| Jint                 | stopwatch-modern             |    90,244.348 us |    679.2107 us |    4 |   12120.31 KB |
| Okojo                | stopwatch-modern             |   154,027.404 us |    605.5049 us |    5 |   21468.89 KB |
| Okojo_Prepared       | stopwatch-modern             |   155,334.860 us |  1,594.1182 us |    5 |   21444.11 KB |
| NilJS                | stopwatch-modern             |   222,745.085 us |  5,723.3724 us |    6 |  324502.66 KB |
