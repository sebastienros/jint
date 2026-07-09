# Engine comparison benchmarks

This project benchmarks Jint against the other managed JavaScript engines for .NET
([Jurassic](https://github.com/paiden/Jurassic), [NiL.JS](https://github.com/nilproject/NiL.JS) and
[YantraJS](https://github.com/yantrajs/yantra)) across a set of representative scripts.

## At a glance (current main, post-4.11.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on almost every workload.** It leads the object, string, base64 and regex scripts
  by **1.04×–4.9×** over the next-fastest engine, starts tiny scripts up **3×–6×** faster
  (`minimal` executes in **under a microsecond**), and now also leads `dromaeo-core-eval` — the
  last row another interpreter used to win.
* **Fastest interpreter on heavy floating-point math.** `dromaeo-3d-cube` moved ahead of NiL.JS in
  this refresh; only the IL-compiling engine remains structurally faster on tight numeric loops.
* **By far the lowest memory use.** Typically **2×–63×** less allocation than the closest competitor
  — and up to **330×** less than the highest in the field — which means much lighter GC pressure in
  real applications.

## Running the benchmarks

Run from **this** directory (the scripts are loaded relative to the working directory):

```
dotnet run -c Release -- --allCategories EngineComparison
```

Notes:

* The `--` separator is required so the arguments are forwarded to BenchmarkDotNet instead of being
  consumed by `dotnet run`.
* Results are written to `BenchmarkDotNet.Artifacts/results/` — the
  `*-report-github.md` file is the table reproduced below.
* The benchmark config widens the parameter column (`MaxParameterColumnWidth = 40`) so the full
  script names are printed instead of BenchmarkDotNet's default truncation (e.g.
  `dromaeo-object-string-modern` rather than `droma(...)odern [28]`).

## How to read the table

* All engines are run in **global strict mode** — YantraJS is strict-only, and strict mode improves
  performance across the board.
* `Jint` re-parses the script source on every operation; `Jint_ParsedScript` reuses a cached
  `Prepared<Script>` produced once by `Engine.PrepareScript`. The gap between the two is pure
  parsing cost — **in production you should cache the prepared script**, which is what
  `Jint_ParsedScript` represents.
* The `*-modern` scripts are ES2015+ rewrites (`const`, arrow functions, …) of the classic ES5
  scripts. **Jurassic reports `NA` on every `-modern` row** because it predates ES2015 and cannot
  parse that syntax.
* `Mean` is time per operation (lower is better); `Allocated` is managed memory per operation
  (lower is better). `Rank` groups results that are statistically tied.

## Where Jint leads

Numbers use Jint's recommended production path (`Jint_ParsedScript`, a cached prepared script) and
compare against the closest competitor on each script.

* **Tiny-script latency.** `minimal` runs in **0.91 µs** and `evaluation` / `evaluation-modern` in
  ~4.2–4.4 µs — **3× and 6× faster than the closest competitor**.
* **Cached scripts avoid re-parsing.** `linq-js` drops from 1,188 µs (re-parsed every run) to
  **57 µs** when the prepared script is reused — ~20× — which is also **5.7× faster than the
  next-fastest engine**. Cache your `Prepared<Script>` in production.
* **eval-heavy code.** `dromaeo-core-eval` now runs in **0.92 ms — 1.3× faster than the closest
  competitor** (the modern variant is 1.65× faster); eval bodies execute on the same per-iteration
  fast paths as regular function bodies.
* **Object, string, base64 and regex workloads.** Jint is the fastest engine on
  `dromaeo-object-array` (**2.1×**), `dromaeo-object-string` (**3.0×**), `dromaeo-object-regexp`
  (**4.9×**) and `dromaeo-string-base64` (statistically tied for first, allocating **8.5× less**
  than the tied competitor).
* **Heavy floating-point math among interpreters.** `dromaeo-3d-cube` runs in **5.99 ms — ahead of
  every other interpreter** — while allocating the least of any engine on that script (1.3 MB vs
  4.9–10.7 MB).
* **Allocations are one to two orders of magnitude lower than the field,** which means far less GC
  pressure in real applications:

  | Benchmark | Jint | Closest competitor | Highest in field |
  |--- |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | 1,355 MB (63×) | 1,653 MB (77×) |
  | `dromaeo-string-base64` | **2.2 MB** | 19 MB (8.5×) | 746 MB (332×) |
  | `dromaeo-object-array` | **8.9 MB** | 17 MB (2.0×) | 215 MB (24×) |
  | `stopwatch` | **12 MB** | 93 MB (7.9×) | 211 MB (18×) |

## Where Jint trails

Only the engine that compiles JavaScript to IL remains ahead, and only on tight numeric/call loops
where compiled code runs close to native speed — the structural interpreter-vs-compiler gap:

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 5.99 ms vs the IL compiler's 2.46 ms
  (2.4×). Every interpreter is now behind Jint on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  statistically tied with the best interpreter and with Jurassic (classic 138 ms, modern 146 ms)
  while allocating 8–24× less than any competitor; the IL-compiling engine remains ~2.4× faster in
  raw time.

## What's new since the previous refresh

Measured against the 2026-07-07 refresh — competitor versions are unchanged, and their rows were
used as a thermal canary to confirm the window is comparable (all within a few percent):

* **`dromaeo-core-eval` −40% (1.52 → 0.92 ms), taking first place from NiL.JS.** Equality operators
  and fused modulo tests joined the unboxed comparison lanes
  ([#2602](https://github.com/sebastienros/jint/pull/2602),
  [#2603](https://github.com/sebastienros/jint/pull/2603),
  [#2604](https://github.com/sebastienros/jint/pull/2604)), expression-only loop bodies run a tight
  per-iteration cycle that drops the function-local loop floor from ~30 to ~10 ns/iteration
  ([#2605](https://github.com/sebastienros/jint/pull/2605)), and dead completion values let
  script/eval top-level code use the same path
  ([#2606](https://github.com/sebastienros/jint/pull/2606)).
* **`dromaeo-3d-cube` −19% time / −35% allocation (7.38 → 5.99 ms), moving ahead of NiL.JS.**
  Sum-of-products arithmetic evaluates on raw doubles and boxes once
  ([#2611](https://github.com/sebastienros/jint/pull/2611)) and dense-array appends take a fast
  path ([#2608](https://github.com/sebastienros/jint/pull/2608)).
* **`stopwatch` −7% (148.7 → 138.3 ms, now tied with the best interpreter and ahead of Jurassic)
  and `stopwatch-modern` −13% (167.9 → 145.8 ms, now tied with Jurassic).** The `z % k == 0`
  if-chain runs as one fused unboxed test, `const` operands were admitted to all comparison lanes
  ([#2604](https://github.com/sebastienros/jint/pull/2604)), loop-body `const` bindings flatten
  into the pooled loop environment ([#2610](https://github.com/sebastienros/jint/pull/2610)), and
  `new Date()` constructs straight from clock ticks
  ([#2609](https://github.com/sebastienros/jint/pull/2609)).
* **`dromaeo-object-array` −10%, `dromaeo-object-regexp` −13%, `dromaeo-object-string` −5%** from
  the same lane and loop work.
* **Behavior fixes shipped alongside**: sticky+global `[Symbol.match]` results
  ([#2600](https://github.com/sebastienros/jint/pull/2600)) and raw `SetProperty` before lazy
  initialization ([#2601](https://github.com/sebastienros/jint/pull/2601)).

## Engine versions

* Jint main (post-4.11.0)
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.406

Last updated 2026-07-09.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean             | StdDev           | Rank | Allocated     |
|------------------ |----------------------------- |-----------------:|-----------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress                 |   2,347,983.2 ns |      3,095.01 ns |    1 |    1058.77 KB |
| Jint              | array-stress                 |   2,421,260.3 ns |      6,430.30 ns |    2 |    1087.44 KB |
| YantraJS          | array-stress                 |   2,839,675.8 ns |     29,555.87 ns |    3 |   17123.82 KB |
| NilJS             | array-stress                 |   4,812,505.5 ns |     12,062.82 ns |    4 |    4521.19 KB |
| Jurassic          | array-stress                 |   8,900,266.0 ns |     43,120.85 ns |    5 |   11644.88 KB |
|                   |                              |                  |                  |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,461,378.3 ns |      8,395.27 ns |    1 |    7596.01 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   5,989,584.2 ns |     13,790.23 ns |    2 |    1343.72 KB |
| NilJS             | dromaeo-3d-cube              |   6,162,996.8 ns |     11,294.08 ns |    3 |    4903.32 KB |
| Jint              | dromaeo-3d-cube              |   6,436,566.8 ns |     11,715.52 ns |    4 |    1647.23 KB |
| Jurassic          | dromaeo-3d-cube              |  54,764,593.3 ns |    127,212.06 ns |    5 |   10654.77 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |               NA |               NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,450,911.9 ns |     17,063.82 ns |    1 |    7514.24 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   6,074,783.9 ns |      9,045.77 ns |    2 |    1341.94 KB |
| Jint              | dromaeo-3d-cube-modern       |   6,488,198.8 ns |     11,832.94 ns |    3 |    1645.26 KB |
| NilJS             | dromaeo-3d-cube-modern       |   7,091,308.6 ns |     14,844.75 ns |    4 |    5977.95 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-core-eval            |     917,232.8 ns |        884.79 ns |    1 |     337.82 KB |
| Jint              | dromaeo-core-eval            |     941,944.4 ns |      1,319.05 ns |    2 |     358.22 KB |
| NilJS             | dromaeo-core-eval            |   1,217,156.2 ns |      1,696.60 ns |    3 |     1577.1 KB |
| YantraJS          | dromaeo-core-eval            |   4,806,999.8 ns |     42,678.89 ns |    4 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  16,421,155.0 ns |     36,999.97 ns |    5 |    2876.11 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-core-eval-modern     |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-core-eval-modern     |     918,705.4 ns |      1,790.49 ns |    1 |     337.63 KB |
| Jint              | dromaeo-core-eval-modern     |     945,761.3 ns |      1,995.50 ns |    2 |     357.29 KB |
| NilJS             | dromaeo-core-eval-modern     |   1,518,953.2 ns |      4,768.80 ns |    3 |    1575.94 KB |
| YantraJS          | dromaeo-core-eval-modern     |   4,845,797.6 ns |     50,625.16 ns |    4 |   35784.84 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-array         |  11,947,145.1 ns |    140,682.17 ns |    1 |    9114.09 KB |
| Jint              | dromaeo-object-array         |  12,687,882.9 ns |    111,889.40 ns |    2 |    9162.42 KB |
| YantraJS          | dromaeo-object-array         |  25,511,017.6 ns |    547,848.38 ns |    3 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  35,911,912.6 ns |    131,042.32 ns |    4 |   25808.82 KB |
| NilJS             | dromaeo-object-array         |  52,241,517.3 ns |    112,816.25 ns |    5 |   17862.17 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-object-array-modern  |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-array-modern  |  15,217,523.3 ns |    119,064.18 ns |    1 |    9116.69 KB |
| Jint              | dromaeo-object-array-modern  |  15,466,509.4 ns |    230,086.13 ns |    1 |    9163.99 KB |
| YantraJS          | dromaeo-object-array-modern  |  24,667,062.5 ns |    235,330.46 ns |    2 |  223803.56 KB |
| NilJS             | dromaeo-object-array-modern  |  52,543,274.3 ns |     99,161.26 ns |    3 |   17863.19 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        | 109,284,797.5 ns |  2,946,284.24 ns |    1 |  159106.47 KB |
| Jint              | dromaeo-object-regexp        | 135,437,983.5 ns |  7,671,733.38 ns |    2 |  159008.67 KB |
| NilJS             | dromaeo-object-regexp        | 533,166,972.2 ns | 11,223,539.45 ns |    3 |  767524.98 KB |
| Jurassic          | dromaeo-object-regexp        | 680,119,108.6 ns | 22,160,081.52 ns |    4 |  818542.42 KB |
| YantraJS          | dromaeo-object-regexp        | 722,897,980.0 ns |  5,302,941.79 ns |    5 |     825999 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-object-regexp-modern |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 104,815,409.0 ns |  4,093,233.32 ns |    1 |  157574.86 KB |
| Jint              | dromaeo-object-regexp-modern | 126,225,314.4 ns |  4,674,719.00 ns |    2 |   158483.3 KB |
| NilJS             | dromaeo-object-regexp-modern | 537,054,440.0 ns |  7,834,141.76 ns |    3 |  767812.77 KB |
| YantraJS          | dromaeo-object-regexp-modern | 718,067,293.3 ns |  9,358,159.43 ns |    4 |  829367.57 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  44,025,293.0 ns |    942,687.22 ns |    1 |   21486.47 KB |
| Jint              | dromaeo-object-string        |  45,296,243.4 ns |  1,006,422.03 ns |    1 |   21596.54 KB |
| NilJS             | dromaeo-object-string        | 130,168,283.3 ns |  2,082,954.93 ns |    2 | 1355037.27 KB |
| YantraJS          | dromaeo-object-string        | 158,274,325.3 ns |  4,513,485.55 ns |    3 | 1653126.43 KB |
| Jurassic          | dromaeo-object-string        | 209,648,577.8 ns |  3,592,105.81 ns |    4 | 1430556.28 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-object-string-modern |               NA |               NA |    ? |            NA |
| Jint              | dromaeo-object-string-modern |  52,596,431.5 ns |  1,158,003.29 ns |    1 |   21407.44 KB |
| Jint_ParsedScript | dromaeo-object-string-modern |  58,863,977.8 ns |    828,485.25 ns |    2 |   21364.05 KB |
| NilJS             | dromaeo-object-string-modern | 129,476,040.4 ns |  2,735,066.22 ns |    3 | 1355034.98 KB |
| YantraJS          | dromaeo-object-string-modern | 159,863,818.7 ns |  4,113,821.16 ns |    4 | 1656442.03 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-string-base64        |  25,144,557.7 ns |     77,849.72 ns |    1 |    2300.33 KB |
| Jint              | dromaeo-string-base64        |  25,387,077.5 ns |     58,610.63 ns |    1 |     2400.2 KB |
| NilJS             | dromaeo-string-base64        |  26,087,507.7 ns |    227,246.72 ns |    1 |   19588.63 KB |
| YantraJS          | dromaeo-string-base64        |  36,550,341.5 ns |    338,128.81 ns |    2 |  763555.54 KB |
| Jurassic          | dromaeo-string-base64        |  47,588,786.7 ns |    145,545.03 ns |    3 |   73289.91 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-string-base64-modern |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-string-base64-modern |  27,765,481.5 ns |     48,691.63 ns |    1 |    2300.42 KB |
| Jint              | dromaeo-string-base64-modern |  28,484,697.8 ns |     45,386.24 ns |    2 |     2400.7 KB |
| NilJS             | dromaeo-string-base64-modern |  33,462,627.5 ns |     79,823.90 ns |    3 |   31360.22 KB |
| YantraJS          | dromaeo-string-base64-modern |  34,969,227.0 ns |  1,138,024.13 ns |    3 |  764771.51 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | evaluation                   |       4,397.1 ns |         16.36 ns |    1 |      16.58 KB |
| Jint              | evaluation                   |      14,220.2 ns |         35.98 ns |    2 |       27.2 KB |
| NilJS             | evaluation                   |      25,371.9 ns |         46.87 ns |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     132,578.2 ns |      1,109.84 ns |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,088,797.7 ns |      4,499.28 ns |    5 |     418.81 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | evaluation-modern            |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       4,219.5 ns |         14.92 ns |    1 |      16.07 KB |
| Jint              | evaluation-modern            |      13,308.2 ns |         42.56 ns |    2 |      27.18 KB |
| NilJS             | evaluation-modern            |      26,770.6 ns |         69.72 ns |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     130,785.4 ns |        752.72 ns |    4 |      703.4 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | linq-js                      |      57,071.0 ns |        111.50 ns |    1 |     206.37 KB |
| YantraJS          | linq-js                      |     324,554.9 ns |      2,101.26 ns |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,188,372.7 ns |      3,989.54 ns |    3 |    1305.56 KB |
| NilJS             | linq-js                      |   3,931,699.0 ns |      7,777.65 ns |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  35,588,142.4 ns |    492,136.25 ns |    5 |    9102.27 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | minimal                      |         914.7 ns |          5.33 ns |    1 |       8.55 KB |
| Jint              | minimal                      |       1,997.0 ns |         14.61 ns |    2 |      10.46 KB |
| NilJS             | minimal                      |       2,725.8 ns |          9.82 ns |    3 |       4.51 KB |
| YantraJS          | minimal                      |     125,804.1 ns |      1,034.79 ns |    4 |     697.62 KB |
| Jurassic          | minimal                      |   2,302,617.8 ns |      2,863.55 ns |    5 |     385.19 KB |
|                   |                              |                  |                  |      |               |
| YantraJS          | stopwatch                    |  56,830,699.3 ns |    361,187.50 ns |    1 |  215655.06 KB |
| NilJS             | stopwatch                    | 135,716,181.7 ns |    717,069.81 ns |    2 |   94876.49 KB |
| Jint              | stopwatch                    | 136,772,095.0 ns |    918,255.41 ns |    2 |   12118.23 KB |
| Jint_ParsedScript | stopwatch                    | 138,266,123.3 ns |    997,029.75 ns |    2 |    12086.5 KB |
| Jurassic          | stopwatch                    | 140,261,986.5 ns |    789,271.96 ns |    2 |  156932.58 KB |
|                   |                              |                  |                  |      |               |
| YantraJS          | stopwatch-modern             |  58,441,248.1 ns |    314,416.07 ns |    1 |  234033.07 KB |
| Jurassic          | stopwatch-modern             | 144,262,953.6 ns |    386,267.43 ns |    2 |  288625.25 KB |
| Jint_ParsedScript | stopwatch-modern             | 145,757,117.3 ns |    343,340.71 ns |    2 |   12087.19 KB |
| Jint              | stopwatch-modern             | 146,825,546.7 ns |  1,082,532.30 ns |    2 |   12119.52 KB |
| NilJS             | stopwatch-modern             | 214,322,853.3 ns |    981,069.14 ns |    3 |  324502.66 KB |
