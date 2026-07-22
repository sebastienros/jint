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
  the fastest engine outright on `minimal` (**1.0 µs vs V8's 406 µs, ~400×**), `evaluation-modern`
  (**~86×**), `linq-js` (**7×**) and `dromaeo-core-eval-modern` (eval defeats V8's compile cache),
  leads `array-stress` and `dromaeo-object-array-modern` outright (statistically rank-tied with
  V8's compiled lane — rows V8 narrowly led at 4.13.0), and is rank-tied on
  `dromaeo-object-regexp-modern`. No other managed engine wins a single row against either of them.
* **V8's wins are the tight-loop compute rows**: `dromaeo-string-base64-modern` (8.1×),
  `stopwatch-modern` (6.0×), `dromaeo-object-string-modern` (5.0×), `dromaeo-3d-cube-modern`
  (2.6×) and `json-parse-modern` (1.4×) — the structural interpreter-vs-JIT gap, narrower on
  every one of these rows than at 4.13.0.
* **Jint is the fastest managed engine on 10 of 12 scripts** (the IL-compiling YantraJS leads
  `dromaeo-3d-cube-modern` and `stopwatch-modern`) and **the fastest interpreter on all 12**.
* **Among the managed engines, Jint and Okojo allocate the least** — NiL.JS and YantraJS allocate
  one to two orders of magnitude more (up to ~470× more than Jint on
  `dromaeo-string-base64-modern`), which means far heavier GC pressure in real applications.
  (ClearScript's rows cannot be compared here — its memory lives on the V8 native heap, which the
  managed diagnoser does not see.)
* **Pure-JS compute is only half the story** — the [interop suite](#script--host-interop) below
  measures the script ↔ host boundary, where the picture inverts and Jint beats ClearScript on
  every row by 2.8×–9.1×.

### What changed since the 4.13.0 table

A profile-guided campaign (PRs [#2716](https://github.com/sebastienros/jint/pull/2716)–
[#2722](https://github.com/sebastienros/jint/pull/2722)) attacked the rows where V8 led, measured
with same-base A/B gates: string-receiver method calls are cached per call site
(`dromaeo-object-string-modern` −20%, `-base64-modern` −17%), JSON.parse interns property keys
(bestbuy-style payloads −22% and −32% allocation), chained `slice()` no longer materializes
intermediate views (−99.3% on that pattern), interop argument binding lost its per-call
reflection checks and boxing, ObjectWrapper members gained a per-callsite inline cache
(`interop-property-access` −25%), and CLR arrays gained identity caching plus an opt-in
`ArrayConversionMode.LiveView` mode (array traversal 2.5× faster, −75% allocation, without flags).

A second campaign round (PRs [#2725](https://github.com/sebastienros/jint/pull/2725),
[#2726](https://github.com/sebastienros/jint/pull/2726)) added bulk JSON string scanning plus an
exactly-rounded simple-number fast path (JsonBenchmark Parse −15–17%) and memoized closure-read
chain validation (`dromaeo-string-base64-modern` −6% at launchCount-5). A third candidate —
arity-specialized direct dispatch for built-in calls, eliminating the argument array — was built,
proven to engage on 4.3M of 4.3M eligible calls, and measured **flat**: Jint's builtin-call path
(pooled argument arrays + the cached-callee lane) is already at its cost floor, so the remaining
gap on the string rows is interpreter dispatch itself, not call ceremony. Measured-and-dropped is
recorded here so it isn't re-attempted.

The 4.14.0 release round shipped the two interop default flips — CLR arrays cross as live views
([#2728](https://github.com/sebastienros/jint/pull/2728)) and recently wrapped objects reuse their
wrappers ([#2734](https://github.com/sebastienros/jint/pull/2734)) — backed by cached array-like
wrapper factories with lazy length ([#2730](https://github.com/sebastienros/jint/pull/2730)),
boxing-free primitive element conversion ([#2731](https://github.com/sebastienros/jint/pull/2731),
extended to the `Array.prototype` iteration lane in
[#2735](https://github.com/sebastienros/jint/pull/2735)), a compiled-invoker fast lane for
single-candidate interop calls ([#2733](https://github.com/sebastienros/jint/pull/2733)) and
JSON.parse value interning plus span-based number parsing
([#2732](https://github.com/sebastienros/jint/pull/2732)). The visible movement in this table:
`interop-collection-traversal` **15,597 → 1,433 µs (10.9×, −99% allocation)** without any script
changes, every other interop row −8–16% with −17–76% allocation, `json-parse-modern` −6% time and
−23% allocation, and `stopwatch-modern` −6%. A pre-release review wave
([#2735](https://github.com/sebastienros/jint/pull/2735)–[#2740](https://github.com/sebastienros/jint/pull/2740))
hardened the new defaults (declared-type contracts, JS-array `in`/enumeration/out-of-range
semantics, constraint-gate balance) with no measurable cost; one further candidate — carrying the
interned JSON key hash into member adds — measured flat and was dropped.

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
  ClearScript **7.1×–9.1×** against Jint on every row — the mirror image of the pure-compute
  table, and the half of the story that matters most in chatty embedding scenarios.
* **FastProxy narrows the gap but does not close it**: still **2.8×–6.7×** behind Jint, and it
  trades away convenience — every member is hand-registered instead of reflected. Its
  zero-allocation claim is real, though: 13 KB managed allocation on the method-call row where
  reflective ClearScript burns 12.8 MB.
* **The managed field is tight at the top**: Jint is rank-tied with YantraJS on method calls and
  with NiL.JS on both property access and string passing — and allocates the least of the managed
  engines on every row (2.3×–12× less than the row's nearest competitor).
* **Collection traversal — the one row Jint lost at 4.13.0 — moved 15,597 → 1,433 µs (10.9×,
  −99% allocation) with no script changes**: the 4.14.0 `ArrayConversionMode.LiveView` default
  exposes the host array as a live view instead of re-copying it on every read, and element reads
  convert without boxing. The row is now second (NiL.JS leads by ~13% while allocating 12× more),
  and the old hoist-into-a-local workaround is no longer needed.

| Method                | FileName                     | Mean        | StdDev    | Median      | Rank | Allocated   |
|---------------------- |----------------------------- |------------:|----------:|------------:|-----:|------------:|
| NilJS                 | interop-collection-traversal |  1,269.4 μs |  11.48 μs |  1,266.3 μs |    1 |  4088.14 KB |
| Jint                  | interop-collection-traversal |  1,432.7 μs |   7.31 μs |  1,432.6 μs |    2 |   331.59 KB |
| YantraJS              | interop-collection-traversal |  3,689.5 μs |  70.53 μs |  3,672.8 μs |    3 |  5399.83 KB |
| ClearScript_FastProxy | interop-collection-traversal |  9,593.5 μs |  58.02 μs |  9,594.8 μs |    4 |  1185.69 KB |
| ClearScript           | interop-collection-traversal | 13,012.7 μs | 355.84 μs | 12,873.7 μs |    5 |  5016.24 KB |
|                       |                              |             |           |             |      |             |
| NilJS                 | interop-method-calls         |  1,186.8 μs |   6.90 μs |  1,184.9 μs |    1 |  2437.94 KB |
| YantraJS              | interop-method-calls         |  1,973.7 μs |  21.91 μs |  1,967.4 μs |    2 |  3355.13 KB |
| Jint                  | interop-method-calls         |  2,032.3 μs |  44.82 μs |  2,015.0 μs |    2 |   347.25 KB |
| ClearScript_FastProxy | interop-method-calls         |  5,594.0 μs | 150.45 μs |  5,546.6 μs |    3 |    12.94 KB |
| ClearScript           | interop-method-calls         | 15,940.1 μs | 130.46 μs | 15,954.8 μs |    4 | 12752.52 KB |
|                       |                              |             |           |             |      |             |
| YantraJS              | interop-property-access      |  1,435.3 μs |   6.21 μs |  1,434.3 μs |    1 |  1870.68 KB |
| Jint                  | interop-property-access      |  1,720.7 μs |   7.10 μs |  1,719.5 μs |    2 |   798.08 KB |
| NilJS                 | interop-property-access      |  1,772.9 μs |  14.26 μs |  1,772.7 μs |    2 |  4391.52 KB |
| ClearScript_FastProxy | interop-property-access      |  5,116.0 μs |  23.94 μs |  5,115.3 μs |    3 |    12.56 KB |
| ClearScript           | interop-property-access      | 13,086.0 μs | 138.18 μs | 13,039.6 μs |    4 | 10561.78 KB |
|                       |                              |             |           |             |      |             |
| YantraJS              | interop-string-passing       |    596.9 μs |   3.21 μs |    597.3 μs |    1 |  1823.71 KB |
| NilJS                 | interop-string-passing       |    812.8 μs |  11.63 μs |    809.3 μs |    2 |  1179.64 KB |
| Jint                  | interop-string-passing       |    816.4 μs |  35.04 μs |    802.3 μs |    2 |   317.12 KB |
| ClearScript_FastProxy | interop-string-passing       |  3,714.8 μs |  25.76 μs |  3,705.1 μs |    3 |   247.15 KB |
| ClearScript           | interop-string-passing       |  5,830.8 μs | 126.25 μs |  5,786.5 μs |    4 |  2470.18 KB |

## Engine versions

* Jint 4.14.0
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.406
* Microsoft.ClearScript.V8 7.5.1

Both tables come from one benchmark session on the same machine and .NET runtime (all lanes,
including ClearScript, measured together). Last updated 2026-07-22 (4.14.0 release).

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

```
| Method               | FileName                     | Mean             | StdDev         | Median           | Rank | Allocated     |
|--------------------- |----------------------------- |-----------------:|---------------:|-----------------:|-----:|--------------:|
| Jint                 | array-stress                 |     2,462.168 μs |     34.6302 μs |     2,450.466 μs |    1 |    1090.81 KB |
| ClearScript_Compiled | array-stress                 |     2,524.296 μs |     31.3424 μs |     2,519.719 μs |    1 |       8.11 KB |
| Jint_ParsedScript    | array-stress                 |     2,526.140 μs |     45.0127 μs |     2,506.173 μs |    1 |    1062.09 KB |
| YantraJS             | array-stress                 |     3,000.115 μs |     42.2450 μs |     2,992.866 μs |    2 |   17123.79 KB |
| ClearScript          | array-stress                 |     4,199.773 μs |     73.5949 μs |     4,185.855 μs |    3 |      16.05 KB |
| NilJS                | array-stress                 |     5,218.499 μs |     24.6692 μs |     5,214.760 μs |    4 |    4521.19 KB |
| Okojo                | array-stress                 |     6,390.222 μs |    100.3574 μs |     6,371.577 μs |    5 |    2697.18 KB |
| Okojo_Prepared       | array-stress                 |     6,398.316 μs |     71.5210 μs |     6,399.807 μs |    5 |    2682.34 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | dromaeo-3d-cube-modern       |     1,967.526 μs |     25.2104 μs |     1,963.547 μs |    1 |       9.28 KB |
| YantraJS             | dromaeo-3d-cube-modern       |     2,656.411 μs |     38.3528 μs |     2,644.599 μs |    2 |    7514.24 KB |
| ClearScript          | dromaeo-3d-cube-modern       |     4,079.435 μs |     42.1260 μs |     4,085.191 μs |    3 |      14.46 KB |
| Jint_ParsedScript    | dromaeo-3d-cube-modern       |     5,127.719 μs |     40.2426 μs |     5,123.502 μs |    4 |    1366.52 KB |
| Jint                 | dromaeo-3d-cube-modern       |     5,614.141 μs |     54.2492 μs |     5,603.165 μs |    5 |     1670.4 KB |
| NilJS                | dromaeo-3d-cube-modern       |     7,379.043 μs |     77.7110 μs |     7,378.651 μs |    6 |    5977.95 KB |
| Okojo_Prepared       | dromaeo-3d-cube-modern       |     7,953.434 μs |     88.6869 μs |     7,948.787 μs |    7 |    2308.94 KB |
| Okojo                | dromaeo-3d-cube-modern       |     8,107.590 μs |    107.3014 μs |     8,102.859 μs |    7 |    2496.09 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | dromaeo-core-eval-modern     |       975.274 μs |     12.9620 μs |       974.708 μs |    1 |     345.42 KB |
| Jint                 | dromaeo-core-eval-modern     |     1,018.717 μs |      8.7201 μs |     1,018.553 μs |    1 |     365.14 KB |
| ClearScript_Compiled | dromaeo-core-eval-modern     |     1,303.570 μs |     17.2976 μs |     1,300.120 μs |    2 |        8.3 KB |
| NilJS                | dromaeo-core-eval-modern     |     1,517.357 μs |     21.6223 μs |     1,512.009 μs |    3 |    1575.94 KB |
| ClearScript          | dromaeo-core-eval-modern     |     2,672.310 μs |     58.6493 μs |     2,662.691 μs |    4 |      12.91 KB |
| Okojo_Prepared       | dromaeo-core-eval-modern     |     4,973.192 μs |    201.9519 μs |     4,952.862 μs |    5 |    4613.19 KB |
| Okojo                | dromaeo-core-eval-modern     |     5,152.215 μs |    273.6571 μs |     5,085.370 μs |    5 |    4627.76 KB |
| YantraJS             | dromaeo-core-eval-modern     |     5,258.782 μs |    165.8174 μs |     5,212.151 μs |    5 |   35784.84 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | dromaeo-object-array-modern  |    16,298.966 μs |    291.4237 μs |    16,304.941 μs |    1 |    9119.94 KB |
| ClearScript_Compiled | dromaeo-object-array-modern  |    16,425.989 μs |    344.9071 μs |    16,422.804 μs |    1 |      16.66 KB |
| Jint                 | dromaeo-object-array-modern  |    17,542.693 μs |    421.1509 μs |    17,573.491 μs |    2 |    9167.33 KB |
| ClearScript          | dromaeo-object-array-modern  |    22,630.676 μs |    348.8724 μs |    22,673.803 μs |    3 |     143.03 KB |
| YantraJS             | dromaeo-object-array-modern  |    27,246.122 μs |    121.8817 μs |    27,177.066 μs |    4 |   223803.5 KB |
| Okojo                | dromaeo-object-array-modern  |    43,238.741 μs |    473.8963 μs |    42,992.178 μs |    5 |    7015.53 KB |
| Okojo_Prepared       | dromaeo-object-array-modern  |    44,021.335 μs |    328.5045 μs |    43,906.579 μs |    5 |    6984.72 KB |
| NilJS                | dromaeo-object-array-modern  |    64,433.133 μs |    151.4493 μs |    64,406.062 μs |    6 |   17863.19 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | dromaeo-object-regexp-modern |    92,662.267 μs |  5,554.5635 μs |    91,570.320 μs |    1 |  158965.94 KB |
| ClearScript          | dromaeo-object-regexp-modern |    99,650.242 μs |  1,359.4224 μs |    99,285.350 μs |    1 |      30.92 KB |
| ClearScript_Compiled | dromaeo-object-regexp-modern |   112,746.416 μs |  1,402.3480 μs |   112,546.920 μs |    2 |      17.57 KB |
| Jint                 | dromaeo-object-regexp-modern |   112,903.090 μs | 14,730.5513 μs |   111,210.250 μs |    2 |  157478.02 KB |
| NilJS                | dromaeo-object-regexp-modern |   539,184.393 μs |  9,242.9886 μs |   536,586.400 μs |    3 |  767583.77 KB |
| YantraJS             | dromaeo-object-regexp-modern |   725,602.560 μs | 12,330.6937 μs |   722,690.500 μs |    4 |  827473.91 KB |
| Okojo_Prepared       | dromaeo-object-regexp-modern | 1,909,429.971 μs | 18,522.4561 μs | 1,902,020.050 μs |    5 | 1800719.13 KB |
| Okojo                | dromaeo-object-regexp-modern | 2,055,150.425 μs | 58,882.4957 μs | 2,026,682.200 μs |    6 | 1797741.38 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | dromaeo-object-string-modern |     9,306.998 μs |     37.1795 μs |     9,302.820 μs |    1 |      16.02 KB |
| ClearScript          | dromaeo-object-string-modern |    13,331.351 μs |     78.8480 μs |    13,321.055 μs |    2 |      24.95 KB |
| Jint                 | dromaeo-object-string-modern |    46,144.158 μs |    626.9467 μs |    46,419.155 μs |    3 |   21502.78 KB |
| Jint_ParsedScript    | dromaeo-object-string-modern |    46,356.892 μs |    402.2200 μs |    46,419.482 μs |    3 |   21367.77 KB |
| Okojo_Prepared       | dromaeo-object-string-modern |    53,784.723 μs |    872.0968 μs |    54,273.031 μs |    4 |   33477.18 KB |
| Okojo                | dromaeo-object-string-modern |    54,484.202 μs |  1,488.0814 μs |    54,402.579 μs |    4 |   33563.08 KB |
| NilJS                | dromaeo-object-string-modern |   165,092.637 μs |  3,857.1772 μs |   165,471.300 μs |    5 | 1354920.47 KB |
| YantraJS             | dromaeo-object-string-modern |   200,552.157 μs |  6,157.8424 μs |   200,882.400 μs |    6 |  1656357.3 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | dromaeo-string-base64-modern |     2,458.410 μs |     12.5989 μs |     2,454.482 μs |    1 |       8.87 KB |
| ClearScript          | dromaeo-string-base64-modern |     4,539.616 μs |     30.1221 μs |     4,538.975 μs |    2 |       15.4 KB |
| Jint                 | dromaeo-string-base64-modern |    19,832.853 μs |     85.6510 μs |    19,824.650 μs |    3 |    1726.02 KB |
| Jint_ParsedScript    | dromaeo-string-base64-modern |    20,297.459 μs |     81.8251 μs |    20,301.366 μs |    3 |    1625.63 KB |
| Okojo                | dromaeo-string-base64-modern |    30,987.298 μs |    151.7031 μs |    31,018.621 μs |    4 |   43824.24 KB |
| Okojo_Prepared       | dromaeo-string-base64-modern |    31,027.693 μs |    216.0771 μs |    30,979.608 μs |    4 |    43746.3 KB |
| NilJS                | dromaeo-string-base64-modern |    31,171.499 μs |    556.0282 μs |    31,284.734 μs |    4 |   31360.34 KB |
| YantraJS             | dromaeo-string-base64-modern |    34,354.223 μs |    140.8657 μs |    34,296.350 μs |    5 |  764771.12 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | evaluation-modern            |         4.807 μs |      0.0289 μs |         4.803 μs |    1 |      17.77 KB |
| Jint                 | evaluation-modern            |        14.400 μs |      0.1428 μs |        14.369 μs |    2 |       28.9 KB |
| NilJS                | evaluation-modern            |        27.311 μs |      0.1634 μs |        27.295 μs |    3 |      22.35 KB |
| YantraJS             | evaluation-modern            |       132.653 μs |      1.8977 μs |       131.845 μs |    4 |      703.4 KB |
| ClearScript_Compiled | evaluation-modern            |       412.519 μs |      2.8495 μs |       411.989 μs |    5 |       6.11 KB |
| ClearScript          | evaluation-modern            |     1,201.114 μs |      9.6170 μs |     1,199.829 μs |    6 |      10.97 KB |
| Okojo                | evaluation-modern            |     1,423.357 μs |     83.2387 μs |     1,433.136 μs |    7 |    1290.67 KB |
| Okojo_Prepared       | evaluation-modern            |     1,548.435 μs |    110.1872 μs |     1,569.070 μs |    8 |    1283.51 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | json-parse-modern            |    11,417.815 μs |    134.0698 μs |    11,431.615 μs |    1 |      10.18 KB |
| ClearScript          | json-parse-modern            |    12,861.491 μs |    113.0581 μs |    12,881.366 μs |    2 |      17.29 KB |
| Jint_ParsedScript    | json-parse-modern            |    16,548.387 μs |    319.2286 μs |    16,586.675 μs |    3 |   11892.46 KB |
| Jint                 | json-parse-modern            |    16,915.605 μs |    396.5301 μs |    16,960.416 μs |    3 |   11927.58 KB |
| YantraJS             | json-parse-modern            |    25,693.139 μs |    433.3713 μs |    25,805.281 μs |    4 |   43167.35 KB |
| Okojo                | json-parse-modern            |    27,379.574 μs |    964.8804 μs |    27,393.323 μs |    4 |   27272.07 KB |
| Okojo_Prepared       | json-parse-modern            |    28,657.410 μs |    426.2874 μs |    28,802.882 μs |    4 |   27236.39 KB |
| NilJS                | json-parse-modern            |   131,330.459 μs |  2,495.3952 μs |   130,253.300 μs |    5 |   67095.19 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | linq-js                      |        71.275 μs |      1.4979 μs |        70.870 μs |    1 |     213.54 KB |
| YantraJS             | linq-js                      |       336.472 μs |      2.8947 μs |       335.788 μs |    2 |    1049.75 KB |
| ClearScript_Compiled | linq-js                      |       501.836 μs |     10.6001 μs |       498.771 μs |    3 |       6.24 KB |
| Jint                 | linq-js                      |     1,244.147 μs |      4.4263 μs |     1,244.484 μs |    4 |    1312.77 KB |
| ClearScript          | linq-js                      |     2,130.219 μs |     18.4304 μs |     2,129.845 μs |    5 |      10.96 KB |
| NilJS                | linq-js                      |     3,989.127 μs |     63.7262 μs |     3,964.191 μs |    6 |    2739.46 KB |
| Okojo_Prepared       | linq-js                      |     5,980.499 μs |    269.2085 μs |     5,920.364 μs |    7 |    4131.99 KB |
| Okojo                | linq-js                      |     9,522.473 μs |     72.7819 μs |     9,547.427 μs |    8 |    4928.77 KB |
|                      |                              |                  |                |                  |      |               |
| Jint_ParsedScript    | minimal                      |         1.009 μs |      0.0185 μs |         1.001 μs |    1 |       9.37 KB |
| Jint                 | minimal                      |         2.092 μs |      0.0143 μs |         2.086 μs |    2 |       11.3 KB |
| NilJS                | minimal                      |         2.857 μs |      0.0570 μs |         2.838 μs |    3 |       4.51 KB |
| YantraJS             | minimal                      |       127.545 μs |      3.5529 μs |       126.823 μs |    4 |     697.62 KB |
| ClearScript_Compiled | minimal                      |       406.098 μs |      2.2378 μs |       405.789 μs |    5 |        6.1 KB |
| Okojo_Prepared       | minimal                      |     1,130.931 μs |     85.6225 μs |     1,130.006 μs |    6 |    1247.45 KB |
| Okojo                | minimal                      |     1,142.914 μs |     92.6550 μs |     1,155.443 μs |    6 |    1249.26 KB |
| ClearScript          | minimal                      |     1,166.631 μs |      7.7466 μs |     1,164.342 μs |    6 |      10.97 KB |
|                      |                              |                  |                |                  |      |               |
| ClearScript_Compiled | stopwatch-modern             |    14,664.285 μs |    152.3281 μs |    14,626.067 μs |    1 |       8.87 KB |
| ClearScript          | stopwatch-modern             |    19,222.272 μs |    434.5008 μs |    19,094.945 μs |    2 |      22.93 KB |
| YantraJS             | stopwatch-modern             |    59,069.089 μs |    344.9357 μs |    59,033.983 μs |    3 |  234033.07 KB |
| Jint                 | stopwatch-modern             |    87,624.167 μs |    610.6516 μs |    87,476.242 μs |    4 |   12121.99 KB |
| Jint_ParsedScript    | stopwatch-modern             |    87,624.758 μs |    652.2827 μs |    87,637.367 μs |    4 |   12089.61 KB |
| Okojo_Prepared       | stopwatch-modern             |   153,561.591 μs |  2,274.0002 μs |   153,442.388 μs |    5 |   21444.55 KB |
| Okojo                | stopwatch-modern             |   157,869.987 μs |  4,518.4418 μs |   155,728.388 μs |    5 |   21468.58 KB |
| NilJS                | stopwatch-modern             |   216,589.444 μs |  6,494.8789 μs |   213,188.233 μs |    6 |  324502.66 KB |
