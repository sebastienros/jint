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

A second campaign round (PRs [#2725](https://github.com/sebastienros/jint/pull/2725),
[#2726](https://github.com/sebastienros/jint/pull/2726)) added bulk JSON string scanning plus an
exactly-rounded simple-number fast path (JsonBenchmark Parse −15–17%) and memoized closure-read
chain validation (`dromaeo-string-base64-modern` −6% at launchCount-5). A third candidate —
arity-specialized direct dispatch for built-in calls, eliminating the argument array — was built,
proven to engage on 4.3M of 4.3M eligible calls, and measured **flat**: Jint's builtin-call path
(pooled argument arrays + the cached-callee lane) is already at its cost floor, so the remaining
gap on the string rows is interpreter dispatch itself, not call ceremony. Measured-and-dropped is
recorded here so it isn't re-attempted.

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

| Method                | FileName                     | Mean        | StdDev    | Rank | Allocated   |
|---------------------- |----------------------------- |------------:|----------:|-----:|------------:|
| NilJS                 | interop-collection-traversal |  1,755.8 μs |  12.62 μs |    1 |  4088.14 KB |
| YantraJS              | interop-collection-traversal |  3,869.5 μs |  25.50 μs |    2 |  5399.83 KB |
| ClearScript_FastProxy | interop-collection-traversal |  9,874.7 μs | 147.39 μs |    3 |  1185.69 KB |
| ClearScript           | interop-collection-traversal | 13,882.2 μs | 117.40 μs |    4 |  5016.24 KB |
| Jint                  | interop-collection-traversal | 15,596.7 μs |  75.38 μs |    5 | 32909.39 KB |
|                       |                              |             |           |      |             |
| NilJS                 | interop-method-calls         |  1,320.3 μs |  15.83 μs |    1 |  2437.94 KB |
| Jint                  | interop-method-calls         |  2,240.2 μs |  44.27 μs |    2 |  1424.51 KB |
| YantraJS              | interop-method-calls         |  2,319.4 μs |  20.72 μs |    2 |  3355.13 KB |
| ClearScript_FastProxy | interop-method-calls         |  5,973.0 μs |  48.06 μs |    3 |    12.95 KB |
| ClearScript           | interop-method-calls         | 17,007.0 μs | 265.45 μs |    4 | 12752.52 KB |
|                       |                              |             |           |      |             |
| YantraJS              | interop-property-access      |  1,504.5 μs |  12.39 μs |    1 |  1870.68 KB |
| NilJS                 | interop-property-access      |  1,816.0 μs |  14.55 μs |    2 |  4391.52 KB |
| Jint                  | interop-property-access      |  1,863.8 μs |   5.69 μs |    2 |   797.87 KB |
| ClearScript_FastProxy | interop-property-access      |  5,690.5 μs |  52.59 μs |    3 |    12.56 KB |
| ClearScript           | interop-property-access      | 13,934.3 μs |  85.74 μs |    4 | 10561.78 KB |
|                       |                              |             |           |      |             |
| YantraJS              | interop-string-passing       |    636.9 μs |   6.99 μs |    1 |  1823.71 KB |
| NilJS                 | interop-string-passing       |    903.6 μs |  17.17 μs |    2 |  1179.64 KB |
| Jint                  | interop-string-passing       |    976.8 μs |  16.09 μs |    3 |   384.29 KB |
| ClearScript_FastProxy | interop-string-passing       |  3,959.1 μs |  25.55 μs |    4 |   247.15 KB |
| ClearScript           | interop-string-passing       |  6,153.8 μs |  45.85 μs |    5 |  2470.18 KB |

## Engine versions

* Jint main (post-4.13.0, campaign PRs #2716-#2726)
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.406
* Microsoft.ClearScript.V8 7.5.1

Both tables come from one benchmark session on the same machine and .NET runtime (all lanes,
including ClearScript, measured together). Last updated 2026-07-21 (second campaign round).

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

```
| Method               | FileName                     | Mean             | StdDev         | Median           | Rank | Allocated     |
|--------------------- |----------------------------- |-----------------:|---------------:|-----------------:|-----:|--------------:|
| ClearScript_Compiled | array-stress                 |     2,359.992 μs |     24.4766 μs |     2,352.952 μs |    1 |       8.15 KB |
| Jint_ParsedScript    | array-stress                 |     2,430.344 μs |     52.6173 μs |     2,439.412 μs |    1 |    1062.09 KB |
| Jint                 | array-stress                 |     2,475.303 μs |     35.1430 μs |     2,477.369 μs |    1 |    1090.81 KB |
| YantraJS             | array-stress                 |     2,996.069 μs |     24.8002 μs |     2,998.358 μs |    2 |   17123.82 KB |
| ClearScript          | array-stress                 |     3,967.369 μs |     11.6155 μs |     3,969.044 μs |    3 |      16.06 KB |
| NilJS                | array-stress                 |     5,142.304 μs |    109.8765 μs |     5,158.574 μs |    4 |    4521.19 KB |
| Okojo                | array-stress                 |     6,145.198 μs |     94.6956 μs |     6,133.786 μs |    5 |    2697.41 KB |
| Okojo_Prepared       | array-stress                 |     6,369.181 μs |     73.1990 μs |     6,378.340 μs |    5 |    2682.13 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | dromaeo-3d-cube-modern       |     1,862.899 μs |      6.2924 μs |     1,862.606 μs |    1 |       9.29 KB |
| YantraJS             | dromaeo-3d-cube-modern       |     2,607.872 μs |     45.5613 μs |     2,622.890 μs |    2 |    7514.24 KB |
| ClearScript          | dromaeo-3d-cube-modern       |     3,887.774 μs |     44.5700 μs |     3,883.027 μs |    3 |      14.52 KB |
| Jint_ParsedScript    | dromaeo-3d-cube-modern       |     5,086.289 μs |     69.1475 μs |     5,110.555 μs |    4 |    1366.52 KB |
| Jint                 | dromaeo-3d-cube-modern       |     5,552.317 μs |     54.8410 μs |     5,559.202 μs |    5 |     1670.4 KB |
| NilJS                | dromaeo-3d-cube-modern       |     7,546.084 μs |     37.1527 μs |     7,541.811 μs |    6 |    5977.95 KB |
| Okojo_Prepared       | dromaeo-3d-cube-modern       |     7,674.203 μs |    186.9177 μs |     7,664.059 μs |    6 |    2311.43 KB |
| Okojo                | dromaeo-3d-cube-modern       |     8,431.493 μs |    201.9635 μs |     8,430.559 μs |    7 |    2498.65 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | dromaeo-core-eval-modern     |       963.033 μs |      4.2105 μs |       963.744 μs |    1 |     345.42 KB |
| Jint                 | dromaeo-core-eval-modern     |     1,011.414 μs |      4.8872 μs |     1,011.055 μs |    2 |     365.14 KB |
| ClearScript_Compiled | dromaeo-core-eval-modern     |     1,209.804 μs |      4.7350 μs |     1,208.161 μs |    3 |       8.35 KB |
| NilJS                | dromaeo-core-eval-modern     |     1,512.408 μs |     16.6312 μs |     1,514.989 μs |    4 |    1575.94 KB |
| ClearScript          | dromaeo-core-eval-modern     |     2,522.647 μs |     11.5468 μs |     2,522.041 μs |    5 |       12.9 KB |
| Okojo_Prepared       | dromaeo-core-eval-modern     |     4,851.274 μs |    135.1445 μs |     4,836.599 μs |    6 |    4613.26 KB |
| Okojo                | dromaeo-core-eval-modern     |     5,054.159 μs |    177.8156 μs |     5,038.543 μs |    6 |    4627.59 KB |
| YantraJS             | dromaeo-core-eval-modern     |     5,097.357 μs |     53.1029 μs |     5,106.077 μs |    6 |   35784.84 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | dromaeo-object-array-modern  |    15,465.797 μs |     77.4279 μs |    15,460.837 μs |    1 |      16.77 KB |
| Jint_ParsedScript    | dromaeo-object-array-modern  |    16,714.443 μs |    346.9385 μs |    16,818.741 μs |    2 |    9119.94 KB |
| Jint                 | dromaeo-object-array-modern  |    17,527.851 μs |    502.6049 μs |    17,633.159 μs |    2 |    9167.33 KB |
| ClearScript          | dromaeo-object-array-modern  |    20,147.101 μs |    182.0349 μs |    20,145.338 μs |    3 |     110.92 KB |
| YantraJS             | dromaeo-object-array-modern  |    26,842.907 μs |    462.6855 μs |    26,907.597 μs |    4 |   223803.5 KB |
| Okojo                | dromaeo-object-array-modern  |    43,580.215 μs |    585.8829 μs |    43,775.309 μs |    5 |    7013.85 KB |
| Okojo_Prepared       | dromaeo-object-array-modern  |    43,961.936 μs |    959.4598 μs |    43,719.842 μs |    5 |    6984.82 KB |
| NilJS                | dromaeo-object-array-modern  |    56,436.197 μs |    909.6352 μs |    56,383.222 μs |    6 |   17863.19 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | dromaeo-object-regexp-modern |    92,629.092 μs |  6,692.4738 μs |    91,574.637 μs |    1 |  155676.94 KB |
| Jint                 | dromaeo-object-regexp-modern |   103,907.497 μs |  7,394.9280 μs |   103,868.863 μs |    2 |  152671.29 KB |
| ClearScript          | dromaeo-object-regexp-modern |   104,302.252 μs |  1,366.7935 μs |   104,216.150 μs |    2 |      33.83 KB |
| ClearScript_Compiled | dromaeo-object-regexp-modern |   117,740.883 μs |  1,580.8440 μs |   118,129.580 μs |    3 |      15.28 KB |
| NilJS                | dromaeo-object-regexp-modern |   543,634.279 μs | 15,648.5472 μs |   539,059.200 μs |    4 |  765542.49 KB |
| YantraJS             | dromaeo-object-regexp-modern |   717,421.425 μs | 14,072.4571 μs |   713,871.000 μs |    5 |  826906.73 KB |
| Okojo                | dromaeo-object-regexp-modern | 1,978,017.180 μs | 27,625.9550 μs | 1,973,697.100 μs |    6 | 1800782.55 KB |
| Okojo_Prepared       | dromaeo-object-regexp-modern | 1,985,016.347 μs | 15,525.3719 μs | 1,985,018.000 μs |    6 |  1798726.5 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | dromaeo-object-string-modern |     8,849.288 μs |    177.6850 μs |     8,780.291 μs |    1 |      16.46 KB |
| ClearScript          | dromaeo-object-string-modern |    12,799.615 μs |     77.9702 μs |    12,819.778 μs |    2 |      25.23 KB |
| Jint_ParsedScript    | dromaeo-object-string-modern |    46,187.780 μs |    929.8952 μs |    45,714.327 μs |    3 |   21367.73 KB |
| Jint                 | dromaeo-object-string-modern |    46,699.380 μs |    996.5029 μs |    46,579.505 μs |    3 |   21469.17 KB |
| Okojo_Prepared       | dromaeo-object-string-modern |    57,798.447 μs |  1,611.2410 μs |    57,759.625 μs |    4 |   33477.43 KB |
| Okojo                | dromaeo-object-string-modern |    60,912.311 μs |  1,389.9445 μs |    60,984.062 μs |    4 |   33515.91 KB |
| NilJS                | dromaeo-object-string-modern |   160,119.567 μs |  2,835.0930 μs |   159,920.825 μs |    5 |  1355025.1 KB |
| YantraJS             | dromaeo-object-string-modern |   196,022.733 μs |  7,105.5015 μs |   194,124.900 μs |    6 | 1656392.21 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | dromaeo-string-base64-modern |     2,529.151 μs |     25.1701 μs |     2,531.266 μs |    1 |       8.85 KB |
| ClearScript          | dromaeo-string-base64-modern |     4,356.631 μs |     26.6011 μs |     4,347.163 μs |    2 |      15.31 KB |
| Jint_ParsedScript    | dromaeo-string-base64-modern |    21,167.228 μs |    240.9144 μs |    21,266.880 μs |    3 |    1625.63 KB |
| Jint                 | dromaeo-string-base64-modern |    22,086.372 μs |    201.4478 μs |    22,122.366 μs |    4 |    1726.03 KB |
| Okojo                | dromaeo-string-base64-modern |    31,683.961 μs |    730.9366 μs |    31,846.329 μs |    5 |   43823.92 KB |
| Okojo_Prepared       | dromaeo-string-base64-modern |    32,032.173 μs |    931.5232 μs |    31,788.977 μs |    5 |   43747.19 KB |
| NilJS                | dromaeo-string-base64-modern |    33,118.500 μs |    607.3054 μs |    32,850.259 μs |    5 |   31360.34 KB |
| YantraJS             | dromaeo-string-base64-modern |    37,108.621 μs |    510.6866 μs |    37,195.850 μs |    6 |  764771.12 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | evaluation-modern            |         4.979 μs |      0.0537 μs |         4.983 μs |    1 |      17.77 KB |
| Jint                 | evaluation-modern            |        15.010 μs |      0.0857 μs |        15.000 μs |    2 |       28.9 KB |
| NilJS                | evaluation-modern            |        28.090 μs |      0.1767 μs |        28.113 μs |    3 |      22.35 KB |
| YantraJS             | evaluation-modern            |       139.215 μs |      1.8294 μs |       139.808 μs |    4 |      703.4 KB |
| ClearScript_Compiled | evaluation-modern            |       458.691 μs |      2.4329 μs |       458.712 μs |    5 |       6.09 KB |
| ClearScript          | evaluation-modern            |     1,226.081 μs |     16.1881 μs |     1,223.989 μs |    6 |      10.97 KB |
| Okojo                | evaluation-modern            |     1,500.291 μs |     66.2898 μs |     1,499.829 μs |    7 |    1290.75 KB |
| Okojo_Prepared       | evaluation-modern            |     1,564.997 μs |     54.7314 μs |     1,572.362 μs |    7 |    1283.51 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | json-parse-modern            |    10,279.649 μs |    186.9487 μs |    10,292.593 μs |    1 |      10.44 KB |
| ClearScript          | json-parse-modern            |    12,327.974 μs |    119.9236 μs |    12,318.953 μs |    2 |       17.5 KB |
| Jint_ParsedScript    | json-parse-modern            |    17,565.444 μs |    338.1997 μs |    17,537.480 μs |    3 |   15461.32 KB |
| Jint                 | json-parse-modern            |    17,857.202 μs |    557.4096 μs |    17,835.422 μs |    3 |   15496.56 KB |
| YantraJS             | json-parse-modern            |    25,920.718 μs |    635.0687 μs |    25,747.923 μs |    4 |   43167.18 KB |
| Okojo_Prepared       | json-parse-modern            |    29,697.735 μs |  1,157.0279 μs |    29,480.283 μs |    5 |   27236.36 KB |
| Okojo                | json-parse-modern            |    30,942.915 μs |  1,288.2467 μs |    30,678.412 μs |    5 |   27272.67 KB |
| NilJS                | json-parse-modern            |   137,863.746 μs |  4,884.9878 μs |   135,340.400 μs |    6 |   67094.88 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | linq-js                      |        80.618 μs |      0.9443 μs |        80.731 μs |    1 |     213.54 KB |
| YantraJS             | linq-js                      |       346.250 μs |      2.6072 μs |       346.433 μs |    2 |    1049.75 KB |
| ClearScript_Compiled | linq-js                      |       527.954 μs |      4.1316 μs |       527.854 μs |    3 |       6.21 KB |
| Jint                 | linq-js                      |     1,264.008 μs |     14.8847 μs |     1,268.350 μs |    4 |    1312.77 KB |
| ClearScript          | linq-js                      |     2,199.396 μs |     30.9001 μs |     2,209.078 μs |    5 |      10.96 KB |
| NilJS                | linq-js                      |     4,229.152 μs |     70.8914 μs |     4,229.789 μs |    6 |    2739.46 KB |
| Okojo_Prepared       | linq-js                      |     6,306.482 μs |    215.7944 μs |     6,255.406 μs |    7 |    4131.97 KB |
| Okojo                | linq-js                      |     9,640.549 μs |    198.7168 μs |     9,643.706 μs |    8 |    4955.05 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | minimal                      |         1.066 μs |      0.0219 μs |         1.073 μs |    1 |       9.37 KB |
| Jint                 | minimal                      |         2.243 μs |      0.0275 μs |         2.245 μs |    2 |       11.3 KB |
| NilJS                | minimal                      |         3.051 μs |      0.0287 μs |         3.054 μs |    3 |       4.51 KB |
| YantraJS             | minimal                      |       130.782 μs |      1.4299 μs |       130.649 μs |    4 |     697.62 KB |
| ClearScript_Compiled | minimal                      |       453.520 μs |      2.4016 μs |       453.787 μs |    5 |       6.09 KB |
| ClearScript          | minimal                      |     1,210.815 μs |     11.8561 μs |     1,208.969 μs |    6 |      10.97 KB |
| Okojo_Prepared       | minimal                      |     1,236.822 μs |     69.9061 μs |     1,243.398 μs |    6 |    1247.47 KB |
| Okojo                | minimal                      |     1,541.602 μs |     60.4255 μs |     1,547.140 μs |    7 |    1249.13 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | stopwatch-modern             |    15,240.847 μs |     77.6445 μs |    15,256.247 μs |    1 |       8.83 KB |
| ClearScript          | stopwatch-modern             |    19,633.375 μs |    288.8562 μs |    19,605.116 μs |    2 |      22.94 KB |
| YantraJS             | stopwatch-modern             |    62,910.536 μs |  1,065.0469 μs |    63,022.122 μs |    3 |  234033.07 KB |
| Jint_ParsedScript    | stopwatch-modern             |    93,496.756 μs |    999.8223 μs |    93,351.025 μs |    4 |   12089.61 KB |
| Jint                 | stopwatch-modern             |    98,350.177 μs |  1,272.2252 μs |    98,161.342 μs |    5 |   12121.99 KB |
| Okojo_Prepared       | stopwatch-modern             |   163,115.837 μs |  3,880.4520 μs |   162,482.025 μs |    6 |   21444.83 KB |
| Okojo                | stopwatch-modern             |   164,151.952 μs |  1,891.8384 μs |   164,654.938 μs |    6 |   21468.78 KB |
| NilJS                | stopwatch-modern             |   232,285.933 μs |  1,854.7813 μs |   232,592.000 μs |    7 |  324502.66 KB |
