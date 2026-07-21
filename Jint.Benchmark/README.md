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
context created from a pre-warmed V8 isolate costs ~380 µs for work Jint completes in about a
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
  tied. Every lane in both tables below comes from a single benchmark session on one machine, so
  ranks are BenchmarkDotNet's own — no rows are merged from separate runs. Cross-session
  comparisons of absolute numbers (including the V8 lanes) are unreliable; compare within a table.
* The `dromaeo-object-regexp-modern` row is the highest-variance in the table (for Jint it is
  dominated by .NET `Regex`); treat small gaps there — including ClearScript's fresh-engine lane
  appearing ahead of its compiled lane — as run-to-run noise.

## At a glance

Using each engine's recommended production path (for Jint a cached prepared script,
`Jint_ParsedScript`; for ClearScript a precompiled `V8Script` on a shared runtime,
`ClearScript_Compiled`):

* **Jint owns everything start-up-shaped and eval-shaped; native V8 owns long compute.** Jint is
  the fastest engine outright on `minimal` (**1.1 µs vs V8's 377 µs, ~350×**), `evaluation-modern`
  (**~80×**), `linq-js` (**6.5×**) and `dromaeo-core-eval-modern` (eval defeats V8's compile
  cache), and is statistically rank-tied with V8's fresh-context lane on
  `dromaeo-object-array-modern` and `dromaeo-object-regexp-modern`. No other managed engine wins
  a single row against either of them.
* **V8's wins are the tight-loop compute rows**: `dromaeo-string-base64-modern` (12.8×),
  `dromaeo-object-string-modern` (7.8×), `stopwatch-modern` (6.4×), `dromaeo-3d-cube-modern`
  (3.5×), `json-parse-modern` (2.3×), and narrow leads on `array-stress` (1.08×) and
  `dromaeo-object-array-modern` (1.10×) — the structural interpreter-vs-JIT gap.
* **Jint is the fastest managed engine on 10 of 12 scripts** (the IL-compiling YantraJS leads
  `dromaeo-3d-cube-modern` and `stopwatch-modern`) and **the fastest interpreter on all 12**.
* **Among the managed engines, Jint and Okojo allocate the least** — NiL.JS and YantraJS allocate
  one to two orders of magnitude more (up to 471× more than Jint on
  `dromaeo-string-base64-modern`), which means far heavier GC pressure in real applications.
  (ClearScript's rows cannot be compared here — its memory lives on the V8 native heap, which the
  managed diagnoser does not see.)
* **Pure-JS compute is only half the story** — the [interop suite](#script--host-interop) below
  measures the script ↔ host boundary, where the picture inverts and Jint beats ClearScript on
  every row by 2.7×–7.7×.

### What changed since the 4.13.0 table

A profile-guided campaign (PRs [#2716](https://github.com/sebastienros/jint/pull/2716)–
[#2722](https://github.com/sebastienros/jint/pull/2722)) attacked the rows where V8 led, measured
with same-base A/B gates: string-receiver method calls are cached per call site
(`dromaeo-object-string-modern` −20%, `-base64-modern` −17%), JSON.parse interns property keys
(bestbuy-style payloads −22% and −32% allocation), chained `slice()` no longer materializes
intermediate views (−99.3% on that pattern), interop argument binding lost its per-call
reflection checks and boxing, ObjectWrapper members gained a per-callsite inline cache
(`interop-property-access` −25%), and CLR arrays gained identity caching plus an opt-in
`ClrArrayConversion.LiveView` mode (array traversal 2.5× faster, −75% allocation, without flags).

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
  ClearScript **5.8×–7.7×** against Jint on method calls, property access and string passing —
  the mirror image of the pure-compute table, and the half of the story that matters most in
  chatty embedding scenarios.
* **FastProxy narrows the gap but does not close it**: still **2.7×–3.2×** behind Jint on those
  rows, and it trades away convenience — every member is hand-registered instead of reflected.
  Its zero-allocation claim is real, though: 13 KB managed allocation on the method-call row
  where reflective ClearScript burns 12.8 MB.
* **The managed field is tight at the top**: Jint is rank-tied with YantraJS on method calls and
  with NiL.JS on property access, third on string passing — after the campaign's interop PRs
  moved all three rows 10–25%.
* **Collection traversal is the one row Jint still loses**, because the shared script deliberately
  re-reads `host.numbers` inside the loop and the default `Copy` conversion re-copies the array
  per read. Two remedies ship today: hoist the collection into a local
  (`var numbers = host.numbers;` — drops Jint to ~1 ms, fastest in the field), or opt into
  `Options.Interop.ClrArrayConversion = LiveView` for live, fixed-size array views (2.5× faster
  and −75% allocation on this pattern with no other changes).

| Method                | FileName                     | Mean        | StdDev   | Rank | Allocated   |
|---------------------- |----------------------------- |------------:|---------:|-----:|------------:|
| NilJS                 | interop-collection-traversal |  1,442.8 μs |  9.45 μs |    1 |  4088.14 KB |
| YantraJS              | interop-collection-traversal |  3,620.3 μs | 14.69 μs |    2 |  5399.83 KB |
| ClearScript_FastProxy | interop-collection-traversal |  8,619.8 μs | 40.96 μs |    3 |   1185.7 KB |
| ClearScript           | interop-collection-traversal | 12,187.9 μs | 55.02 μs |    4 |  5016.25 KB |
| Jint                  | interop-collection-traversal | 15,009.3 μs | 57.23 μs |    5 | 32909.06 KB |
|                       |                              |             |          |      |             |
| NilJS                 | interop-method-calls         |  1,345.6 μs |  4.93 μs |    1 |  2437.94 KB |
| YantraJS              | interop-method-calls         |  2,015.6 μs |  8.20 μs |    2 |  3355.13 KB |
| Jint                  | interop-method-calls         |  2,028.2 μs |  6.33 μs |    2 |  1424.29 KB |
| ClearScript_FastProxy | interop-method-calls         |  5,437.2 μs | 17.85 μs |    3 |    12.95 KB |
| ClearScript           | interop-method-calls         | 15,715.1 μs | 62.49 μs |    4 | 12752.53 KB |
|                       |                              |             |          |      |             |
| YantraJS              | interop-property-access      |  1,469.8 μs |  5.57 μs |    1 |  1870.68 KB |
| NilJS                 | interop-property-access      |  1,668.1 μs |  4.90 μs |    2 |  4391.52 KB |
| Jint                  | interop-property-access      |  1,674.9 μs |  4.53 μs |    2 |   797.63 KB |
| ClearScript_FastProxy | interop-property-access      |  5,221.5 μs |  8.65 μs |    3 |     12.6 KB |
| ClearScript           | interop-property-access      | 12,829.2 μs | 34.74 μs |    4 | 10561.78 KB |
|                       |                              |             |          |      |             |
| YantraJS              | interop-string-passing       |    608.8 μs |  3.78 μs |    1 |  1823.71 KB |
| NilJS                 | interop-string-passing       |    847.8 μs |  4.22 μs |    2 |  1179.64 KB |
| Jint                  | interop-string-passing       |    917.1 μs |  4.48 μs |    3 |   384.07 KB |
| ClearScript_FastProxy | interop-string-passing       |  2,945.3 μs | 14.73 μs |    4 |   247.15 KB |
| ClearScript           | interop-string-passing       |  5,358.8 μs | 14.46 μs |    5 |  2470.18 KB |

## Engine versions

* Jint main @ ab4367673 (post-4.13.0, campaign PRs #2716-#2722)
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.406
* Microsoft.ClearScript.V8 7.5.1

Both tables come from one benchmark session on the same machine and .NET runtime (all lanes,
including ClearScript, measured together). Last updated 2026-07-21.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

```
| Method               | FileName                     | Mean             | StdDev         | Rank | Allocated     |
|--------------------- |----------------------------- |-----------------:|---------------:|-----:|--------------:|
| ClearScript_Compiled | array-stress                 |     2,122.865 μs |     14.2022 μs |    1 |       8.06 KB |
| Jint                 | array-stress                 |     2,270.240 μs |      6.0504 μs |    2 |    1089.51 KB |
| Jint_ParsedScript    | array-stress                 |     2,296.738 μs |      6.1801 μs |    2 |    1060.78 KB |
| YantraJS             | array-stress                 |     3,066.961 μs |     33.1016 μs |    3 |   17123.82 KB |
| ClearScript          | array-stress                 |     3,433.412 μs |     26.4434 μs |    4 |       16.1 KB |
| NilJS                | array-stress                 |     4,870.763 μs |     13.5074 μs |    5 |    4521.19 KB |
| Okojo_Prepared       | array-stress                 |     5,416.597 μs |     15.3633 μs |    6 |    2681.88 KB |
| Okojo                | array-stress                 |     5,532.279 μs |     27.4719 μs |    6 |    2697.74 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-3d-cube-modern       |     1,380.904 μs |      4.2986 μs |    1 |       9.28 KB |
| YantraJS             | dromaeo-3d-cube-modern       |     2,519.855 μs |     31.1457 μs |    2 |    7514.24 KB |
| ClearScript          | dromaeo-3d-cube-modern       |     3,284.284 μs |     17.8778 μs |    3 |      14.53 KB |
| Jint_ParsedScript    | dromaeo-3d-cube-modern       |     4,816.912 μs |     16.1991 μs |    4 |    1353.12 KB |
| Jint                 | dromaeo-3d-cube-modern       |     5,177.126 μs |     14.1273 μs |    5 |    1656.98 KB |
| NilJS                | dromaeo-3d-cube-modern       |     7,147.427 μs |     40.8166 μs |    6 |    5977.95 KB |
| Okojo_Prepared       | dromaeo-3d-cube-modern       |     7,147.897 μs |     55.0017 μs |    6 |    2311.61 KB |
| Okojo                | dromaeo-3d-cube-modern       |     7,540.586 μs |     72.2140 μs |    7 |    2498.88 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | dromaeo-core-eval-modern     |       910.444 μs |      2.1860 μs |    1 |     340.21 KB |
| Jint                 | dromaeo-core-eval-modern     |       937.973 μs |      3.9338 μs |    2 |     359.93 KB |
| ClearScript_Compiled | dromaeo-core-eval-modern     |       956.865 μs |      4.0812 μs |    2 |       8.01 KB |
| NilJS                | dromaeo-core-eval-modern     |     1,421.783 μs |      6.2106 μs |    3 |    1575.94 KB |
| ClearScript          | dromaeo-core-eval-modern     |     2,108.919 μs |     27.2368 μs |    4 |       12.9 KB |
| YantraJS             | dromaeo-core-eval-modern     |     5,088.348 μs |     41.5503 μs |    5 |   35784.84 KB |
| Okojo                | dromaeo-core-eval-modern     |     6,776.192 μs |    107.3668 μs |    6 |    4627.59 KB |
| Okojo_Prepared       | dromaeo-core-eval-modern     |     6,828.625 μs |     73.8879 μs |    6 |    4613.49 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-array-modern  |    14,581.176 μs |     41.5273 μs |    1 |      16.51 KB |
| ClearScript          | dromaeo-object-array-modern  |    15,686.799 μs |     42.8549 μs |    2 |     112.59 KB |
| Jint_ParsedScript    | dromaeo-object-array-modern  |    16,013.891 μs |    258.2276 μs |    2 |    9118.26 KB |
| Jint                 | dromaeo-object-array-modern  |    16,305.830 μs |    413.4515 μs |    2 |    9165.65 KB |
| YantraJS             | dromaeo-object-array-modern  |    27,411.026 μs |     65.6559 μs |    3 |   223803.5 KB |
| Okojo_Prepared       | dromaeo-object-array-modern  |    40,617.810 μs |    100.7091 μs |    4 |    6984.75 KB |
| Okojo                | dromaeo-object-array-modern  |    40,705.147 μs |    146.9328 μs |    4 |    7014.78 KB |
| NilJS                | dromaeo-object-array-modern  |    52,003.161 μs |    166.9631 μs |    5 |   17863.19 KB |
|                      |                              |                  |                |      |               |
| ClearScript          | dromaeo-object-regexp-modern |    91,649.288 μs |    477.2249 μs |    1 |      32.05 KB |
| ClearScript_Compiled | dromaeo-object-regexp-modern |   107,093.116 μs |    981.8532 μs |    2 |      17.62 KB |
| Jint_ParsedScript    | dromaeo-object-regexp-modern |   113,531.481 μs |  8,391.2874 μs |    2 |  154391.04 KB |
| Jint                 | dromaeo-object-regexp-modern |   129,701.995 μs | 10,987.7392 μs |    3 |   155060.9 KB |
| NilJS                | dromaeo-object-regexp-modern |   538,781.973 μs |  8,200.5012 μs |    4 |  766366.22 KB |
| YantraJS             | dromaeo-object-regexp-modern |   720,444.300 μs |  5,484.2869 μs |    5 |  824049.21 KB |
| Okojo                | dromaeo-object-regexp-modern | 1,893,982.873 μs | 12,847.3321 μs |    6 | 1798830.04 KB |
| Okojo_Prepared       | dromaeo-object-regexp-modern | 1,925,628.079 μs | 14,708.5274 μs |    6 | 1797748.76 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-string-modern |     5,943.633 μs |     27.2020 μs |    1 |      15.77 KB |
| ClearScript          | dromaeo-object-string-modern |     8,805.940 μs |     85.3538 μs |    2 |      25.28 KB |
| Jint                 | dromaeo-object-string-modern |    45,776.389 μs |    896.0354 μs |    3 |   21445.61 KB |
| Jint_ParsedScript    | dromaeo-object-string-modern |    46,164.896 μs |    606.3545 μs |    3 |   21305.18 KB |
| Okojo                | dromaeo-object-string-modern |    56,543.228 μs |  1,280.9675 μs |    4 |   33544.61 KB |
| Okojo_Prepared       | dromaeo-object-string-modern |    57,089.815 μs |    498.9570 μs |    4 |   33432.15 KB |
| NilJS                | dromaeo-object-string-modern |   141,919.297 μs |  4,548.8732 μs |    5 | 1354999.22 KB |
| YantraJS             | dromaeo-object-string-modern |   171,329.781 μs |  3,302.0661 μs |    6 | 1656398.73 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-string-base64-modern |     1,722.602 μs |      8.5076 μs |    1 |       8.84 KB |
| ClearScript          | dromaeo-string-base64-modern |     3,495.787 μs |     20.8322 μs |    2 |       15.4 KB |
| Jint_ParsedScript    | dromaeo-string-base64-modern |    22,111.368 μs |     48.2077 μs |    3 |    1622.18 KB |
| Jint                 | dromaeo-string-base64-modern |    22,811.553 μs |     49.5415 μs |    4 |    1722.58 KB |
| Okojo_Prepared       | dromaeo-string-base64-modern |    31,568.224 μs |    474.3633 μs |    5 |    43747.5 KB |
| Okojo                | dromaeo-string-base64-modern |    32,112.818 μs |    413.9916 μs |    5 |   43823.85 KB |
| NilJS                | dromaeo-string-base64-modern |    33,236.587 μs |     89.6632 μs |    6 |   31360.34 KB |
| YantraJS             | dromaeo-string-base64-modern |    35,898.369 μs |    435.3298 μs |    7 |  764771.55 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | evaluation-modern            |         4.865 μs |      0.0424 μs |    1 |      17.39 KB |
| Jint                 | evaluation-modern            |        14.526 μs |      0.0486 μs |    2 |      28.52 KB |
| NilJS                | evaluation-modern            |        26.335 μs |      0.0686 μs |    3 |      22.35 KB |
| YantraJS             | evaluation-modern            |       139.788 μs |      1.2111 μs |    4 |      703.4 KB |
| ClearScript_Compiled | evaluation-modern            |       390.483 μs |      1.3316 μs |    5 |        6.1 KB |
| ClearScript          | evaluation-modern            |     1,172.685 μs |      6.6711 μs |    6 |      10.97 KB |
| Okojo_Prepared       | evaluation-modern            |     1,739.310 μs |     59.0979 μs |    7 |    1283.45 KB |
| Okojo                | evaluation-modern            |     1,759.698 μs |     55.1924 μs |    7 |    1290.68 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | json-parse-modern            |     7,513.570 μs |     24.3087 μs |    1 |       9.74 KB |
| ClearScript          | json-parse-modern            |     9,388.594 μs |     44.6263 μs |    2 |      17.24 KB |
| Jint                 | json-parse-modern            |    17,608.149 μs |     80.0495 μs |    3 |   15493.71 KB |
| Jint_ParsedScript    | json-parse-modern            |    17,633.629 μs |     60.8675 μs |    3 |    15458.2 KB |
| YantraJS             | json-parse-modern            |    26,187.819 μs |    207.8082 μs |    4 |   43167.35 KB |
| Okojo_Prepared       | json-parse-modern            |    26,429.088 μs |  1,248.0657 μs |    4 |   27235.55 KB |
| Okojo                | json-parse-modern            |    30,372.699 μs |    223.5037 μs |    5 |   27271.64 KB |
| NilJS                | json-parse-modern            |   130,210.857 μs |    798.0902 μs |    6 |   67095.19 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | linq-js                      |        73.110 μs |      0.2567 μs |    1 |     209.52 KB |
| YantraJS             | linq-js                      |       338.094 μs |      2.6530 μs |    2 |    1049.75 KB |
| ClearScript_Compiled | linq-js                      |       471.832 μs |      1.3941 μs |    3 |       6.21 KB |
| Jint                 | linq-js                      |     1,230.820 μs |      4.8982 μs |    4 |    1308.74 KB |
| ClearScript          | linq-js                      |     2,076.960 μs |     20.3454 μs |    5 |      10.96 KB |
| NilJS                | linq-js                      |     4,050.048 μs |     21.0730 μs |    6 |    2739.46 KB |
| Okojo_Prepared       | linq-js                      |     6,528.129 μs |    110.7950 μs |    7 |     4131.9 KB |
| Okojo                | linq-js                      |     9,495.612 μs |     97.3517 μs |    8 |    4928.98 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | minimal                      |         1.088 μs |      0.0086 μs |    1 |       9.33 KB |
| Jint                 | minimal                      |         2.199 μs |      0.0169 μs |    2 |      11.26 KB |
| NilJS                | minimal                      |         2.830 μs |      0.0149 μs |    3 |       4.51 KB |
| YantraJS             | minimal                      |       125.370 μs |      1.2628 μs |    4 |     697.62 KB |
| ClearScript_Compiled | minimal                      |       377.213 μs |      1.7142 μs |    5 |       6.08 KB |
| ClearScript          | minimal                      |     1,119.006 μs |      5.7360 μs |    6 |      10.97 KB |
| Okojo_Prepared       | minimal                      |     1,251.448 μs |     64.4237 μs |    7 |    1247.32 KB |
| Okojo                | minimal                      |     1,457.027 μs |     77.3155 μs |    8 |    1249.25 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | stopwatch-modern             |    14,209.535 μs |     30.9915 μs |    1 |       9.26 KB |
| ClearScript          | stopwatch-modern             |    17,295.575 μs |     45.1967 μs |    2 |      22.71 KB |
| YantraJS             | stopwatch-modern             |    59,355.825 μs |    423.4778 μs |    3 |  234033.07 KB |
| Jint                 | stopwatch-modern             |    88,705.563 μs |    353.8117 μs |    4 |    12120.9 KB |
| Jint_ParsedScript    | stopwatch-modern             |    90,621.520 μs |    350.9179 μs |    4 |   12088.52 KB |
| Okojo                | stopwatch-modern             |   149,131.710 μs |  2,655.2821 μs |    5 |   21469.08 KB |
| Okojo_Prepared       | stopwatch-modern             |   151,910.737 μs |  1,547.5919 μs |    5 |   21445.53 KB |
| NilJS                | stopwatch-modern             |   242,150.569 μs |  1,912.2259 μs |    6 |  324502.66 KB |
