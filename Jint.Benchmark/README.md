# Engine comparison benchmarks

This project benchmarks Jint against the other managed JavaScript engines for .NET
([Jurassic](https://github.com/paiden/Jurassic), [NiL.JS](https://github.com/nilproject/NiL.JS) and
[YantraJS](https://github.com/yantrajs/yantra)) across a set of representative scripts.

## At a glance (current main, post-4.11.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on most workloads.** It leads the object, string, base64 and regex scripts by
  **1.1×–4×** over the next-fastest engine, and starts tiny scripts up **3×–6×** faster —
  `minimal` now executes in **under a microsecond**.
* **By far the lowest memory use.** Typically **2×–63×** less allocation than the closest competitor
  — and up to **330×** less than the highest in the field — which means much lighter GC pressure in
  real applications.
* **The remaining gaps narrowed again.** `stopwatch-modern` moved ahead of NiL.JS and closed on
  Jurassic (1.13× behind, from 1.44×); heavy floating-point math (`dromaeo-3d-cube`) is 1.2× behind
  the closest interpreter, with only the IL-compiling engine structurally faster on tight numeric
  loops.

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

* **Tiny-script latency.** `minimal` runs in **0.93 µs** and `evaluation` / `evaluation-modern` in
  ~4.3 µs — **3× and 6× faster than the closest competitor**. Jint has almost no per-run engine
  overhead, and a fresh engine now allocates less than half of what it did in 4.11.0.
* **Cached scripts avoid re-parsing.** `linq-js` drops from 1,191 µs (re-parsed every run) to
  **58 µs** when the prepared script is reused — ~20× — which is also **5.7× faster than the
  next-fastest engine**. Cache your `Prepared<Script>` in production.
* **Object, string, base64 and regex workloads.** Jint is the fastest engine on
  `dromaeo-object-array` (**2.0×**), `dromaeo-object-string` (**3.0×**), `dromaeo-object-regexp`
  (**4.3×**) — and now also `dromaeo-string-base64`, where it took the lead from NiL.JS in this
  refresh.
* **Allocations are one to two orders of magnitude lower than the field,** which means far less GC
  pressure in real applications:

  | Benchmark | Jint | Closest competitor | Highest in field |
  |--- |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | 1,323 MB (63×) | 1,614 MB (77×) |
  | `dromaeo-string-base64` | **2.2 MB** | 19 MB (8.5×) | 746 MB (330×) |
  | `dromaeo-object-array` | **8.9 MB** | 17 MB (2.0×) | 215 MB (24×) |
  | `stopwatch` | **12 MB** | 93 MB (7.9×) | 211 MB (18×) |

## Where Jint trails

The remaining gaps favour an engine that compiles JavaScript to IL, where tight numeric loops run
closer to native speed. All of them narrowed again in this refresh:

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 7.4 ms, now **1.2× behind the closest
  interpreter** (down from 1.3×) — the IL-compiling engine is ~3× faster; this is the structural
  interpreter-vs-compiler gap. Jint still allocates the least of any engine on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  `stopwatch-modern` improved 22% in this refresh, moving **ahead of NiL.JS** and to within
  **1.13× of Jurassic** (from 1.44×); the classic variant is within 1.11× of the closest
  interpreter. Both allocate 8–24× less than any competitor; the IL-compiling engine remains
  ~2.5–2.8× faster in raw time.
* **`dromaeo-core-eval`**: **1.25× behind the closest competitor** while allocating ~4.7× less;
  the modern variant is at **1.04× — effectively parity**.

## What's new since 4.11.0

Measured against the 4.11.0 refresh — competitor versions are unchanged, and their rows were used
as a thermal canary to confirm the window is comparable:

* **`minimal` −47% time / −45% allocation** (1.76 → 0.93 µs, 15.4 → 8.45 KB prepared) and
  **`evaluation` / `evaluation-modern` −13–18% time / −33% allocation**. The global object and the
  remaining built-in namespaces now use lazily-materialized shared shapes instead of building
  per-realm property dictionaries
  ([#2580](https://github.com/sebastienros/jint/pull/2580),
  [#2581](https://github.com/sebastienros/jint/pull/2581),
  [#2582](https://github.com/sebastienros/jint/pull/2582),
  [#2588](https://github.com/sebastienros/jint/pull/2588),
  [#2590](https://github.com/sebastienros/jint/pull/2590),
  [#2595](https://github.com/sebastienros/jint/pull/2595),
  [#2597](https://github.com/sebastienros/jint/pull/2597)); engine construction alone is −60% time
  / −52% allocation.
* **`stopwatch-modern` −22%** (215 → 168 ms). Per-iteration block ceremony was trimmed and lexical
  declarations take a slot-based initialization lane
  ([#2583](https://github.com/sebastienros/jint/pull/2583)), and global bindings read from nested
  scopes hit a version-validated descriptor cache instead of walking the environment chain
  ([#2584](https://github.com/sebastienros/jint/pull/2584)).
* **`dromaeo-object-string` −14%, `dromaeo-string-base64` −12–13%, `dromaeo-object-array` −11%,
  `dromaeo-3d-cube` −4.6%** from the same binding work plus allocation-free own-property probes
  ([#2585](https://github.com/sebastienros/jint/pull/2585)), for-of iteration-environment reuse
  ([#2586](https://github.com/sebastienros/jint/pull/2586)) and allocation-free for-in key
  enumeration ([#2592](https://github.com/sebastienros/jint/pull/2592)).
* **Beyond this table**: `arguments`-heavy calls allocate −72%
  ([#2589](https://github.com/sebastienros/jint/pull/2589)), object literals built inside
  generators allocate −56% ([#2593](https://github.com/sebastienros/jint/pull/2593),
  [#2596](https://github.com/sebastienros/jint/pull/2596)).

Every Jint row is flat or faster than the previous refresh except `dromaeo-object-array-modern`
(+10%), a row family with a documented ±7–11% run-to-run swing whose classic sibling improved 11%;
the reading reproduces identically on builds with and without the most recent changes, so it is
being tracked as measurement noise pending the next refresh.

## Engine versions

* Jint main (post-4.11.0)
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.406

Last updated 2026-07-07.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean             | StdDev           | Median           | Rank | Allocated     |
|------------------ |----------------------------- |-----------------:|-----------------:|-----------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress                 |   2,491,470.4 ns |      6,561.25 ns |   2,493,616.6 ns |    1 |    1057.76 KB |
| Jint              | array-stress                 |   2,536,322.4 ns |      6,450.90 ns |   2,538,262.5 ns |    1 |    1086.42 KB |
| YantraJS          | array-stress                 |   2,814,722.5 ns |     20,849.84 ns |   2,814,398.0 ns |    2 |   17123.82 KB |
| NilJS             | array-stress                 |   4,709,726.3 ns |      7,455.66 ns |   4,709,054.7 ns |    3 |    4521.19 KB |
| Jurassic          | array-stress                 |   8,840,886.7 ns |     27,349.65 ns |   8,838,535.9 ns |    4 |   11644.87 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,464,433.9 ns |     15,647.46 ns |   2,463,939.5 ns |    1 |    7596.01 KB |
| NilJS             | dromaeo-3d-cube              |   6,281,472.3 ns |     23,999.34 ns |   6,276,086.3 ns |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   7,384,892.3 ns |     18,437.74 ns |   7,381,844.9 ns |    3 |    2073.16 KB |
| Jint              | dromaeo-3d-cube              |   7,908,764.5 ns |     22,831.14 ns |   7,906,231.2 ns |    4 |    2376.66 KB |
| Jurassic          | dromaeo-3d-cube              |  54,264,290.8 ns |    229,143.61 ns |  54,286,990.0 ns |    5 |   10654.72 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |               NA |               NA |               NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,712,981.5 ns |     30,235.78 ns |   2,703,813.3 ns |    1 |    7514.24 KB |
| NilJS             | dromaeo-3d-cube-modern       |   7,027,386.0 ns |     17,925.30 ns |   7,025,530.5 ns |    2 |    5977.95 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   7,556,955.6 ns |     34,966.31 ns |   7,557,454.7 ns |    3 |    2071.57 KB |
| Jint              | dromaeo-3d-cube-modern       |   7,943,530.3 ns |     35,615.28 ns |   7,951,181.2 ns |    4 |    2374.89 KB |
|                   |                              |                  |                  |                  |      |               |
| NilJS             | dromaeo-core-eval            |   1,213,813.9 ns |      4,285.02 ns |   1,213,908.6 ns |    1 |     1577.1 KB |
| Jint              | dromaeo-core-eval            |   1,511,293.0 ns |      6,590.43 ns |   1,514,099.6 ns |    2 |     353.98 KB |
| Jint_ParsedScript | dromaeo-core-eval            |   1,516,036.5 ns |      1,856.38 ns |   1,516,126.6 ns |    2 |     333.58 KB |
| YantraJS          | dromaeo-core-eval            |   5,108,833.1 ns |     67,098.13 ns |   5,132,896.9 ns |    3 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  16,662,663.0 ns |     38,603.22 ns |  16,657,634.4 ns |    4 |    2876.11 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-core-eval-modern     |               NA |               NA |               NA |    ? |            NA |
| NilJS             | dromaeo-core-eval-modern     |   1,409,928.4 ns |      5,222.07 ns |   1,410,134.7 ns |    1 |    1575.94 KB |
| Jint_ParsedScript | dromaeo-core-eval-modern     |   1,464,299.3 ns |      1,344.52 ns |   1,463,710.0 ns |    2 |      333.4 KB |
| Jint              | dromaeo-core-eval-modern     |   1,500,487.9 ns |      4,693.89 ns |   1,500,807.0 ns |    3 |     353.06 KB |
| YantraJS          | dromaeo-core-eval-modern     |   5,147,381.8 ns |     72,073.86 ns |   5,151,728.9 ns |    4 |   35784.84 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-array         |  13,212,378.3 ns |    155,892.20 ns |  13,234,054.7 ns |    1 |    9112.96 KB |
| Jint              | dromaeo-object-array         |  14,061,302.7 ns |    117,902.64 ns |  13,996,889.1 ns |    2 |    9161.27 KB |
| YantraJS          | dromaeo-object-array         |  25,771,422.9 ns |    845,566.34 ns |  25,448,646.9 ns |    3 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  35,307,674.9 ns |    171,330.03 ns |  35,339,026.7 ns |    4 |   25809.38 KB |
| NilJS             | dromaeo-object-array         |  51,939,617.3 ns |    233,302.74 ns |  51,894,510.0 ns |    5 |   17862.17 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-array-modern  |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-array-modern  |  16,105,896.2 ns |     67,352.58 ns |  16,101,400.0 ns |    1 |    9115.54 KB |
| Jint              | dromaeo-object-array-modern  |  16,555,748.8 ns |    148,166.85 ns |  16,613,425.0 ns |    1 |    9162.84 KB |
| YantraJS          | dromaeo-object-array-modern  |  27,296,201.8 ns |    426,389.16 ns |  27,290,987.5 ns |    2 |  223803.56 KB |
| NilJS             | dromaeo-object-array-modern  |  52,850,717.3 ns |    204,259.61 ns |  52,855,050.0 ns |    3 |   17863.19 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        | 126,196,234.4 ns |  6,392,193.09 ns | 127,530,950.0 ns |    1 |  156257.63 KB |
| Jint              | dromaeo-object-regexp        | 163,977,110.0 ns | 10,772,250.60 ns | 163,183,225.0 ns |    2 |  159950.82 KB |
| NilJS             | dromaeo-object-regexp        | 541,441,868.2 ns | 13,047,203.10 ns | 540,832,950.0 ns |    3 |  767452.66 KB |
| Jurassic          | dromaeo-object-regexp        | 703,167,756.7 ns | 20,809,665.45 ns | 703,128,700.0 ns |    4 |  825164.08 KB |
| YantraJS          | dromaeo-object-regexp        | 715,687,861.5 ns |  6,407,379.70 ns | 718,000,100.0 ns |    4 |  825559.55 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-regexp-modern |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 105,592,792.9 ns |  2,038,377.96 ns | 105,841,600.0 ns |    1 |   157415.3 KB |
| Jint              | dromaeo-object-regexp-modern | 135,512,176.8 ns |  6,383,954.71 ns | 134,672,466.7 ns |    2 |  159034.59 KB |
| NilJS             | dromaeo-object-regexp-modern | 536,240,920.0 ns |  7,267,126.77 ns | 533,834,500.0 ns |    3 |  768262.64 KB |
| YantraJS          | dromaeo-object-regexp-modern | 716,850,380.0 ns |  9,984,999.63 ns | 718,298,300.0 ns |    4 |  824546.12 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  46,544,920.6 ns |    547,344.52 ns |  46,751,490.9 ns |    1 |   21528.64 KB |
| Jint              | dromaeo-object-string        |  48,158,820.8 ns |    130,640.42 ns |  48,146,600.0 ns |    2 |   21679.94 KB |
| NilJS             | dromaeo-object-string        | 141,899,940.0 ns |  2,645,163.21 ns | 140,697,200.0 ns |    3 | 1354949.19 KB |
| YantraJS          | dromaeo-object-string        | 167,586,030.4 ns |  4,164,444.22 ns | 166,395,900.0 ns |    4 | 1653024.01 KB |
| Jurassic          | dromaeo-object-string        | 218,704,635.0 ns |  4,786,092.64 ns | 216,530,100.0 ns |    5 | 1430548.95 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-string-modern |               NA |               NA |               NA |    ? |            NA |
| Jint              | dromaeo-object-string-modern |  58,146,645.2 ns |  1,074,429.16 ns |  58,720,722.2 ns |    1 |   21433.43 KB |
| Jint_ParsedScript | dromaeo-object-string-modern |  58,366,215.7 ns |  1,176,203.43 ns |  58,788,144.4 ns |    1 |   21276.25 KB |
| NilJS             | dromaeo-object-string-modern | 143,312,796.1 ns |  2,905,532.93 ns | 144,871,600.0 ns |    2 | 1355001.17 KB |
| YantraJS          | dromaeo-object-string-modern | 168,518,110.1 ns |  4,189,654.02 ns | 168,600,333.3 ns |    3 | 1656377.75 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-string-base64        |  24,658,295.5 ns |     59,690.98 ns |  24,636,039.1 ns |    1 |    2299.05 KB |
| Jint              | dromaeo-string-base64        |  25,319,019.0 ns |     60,498.54 ns |  25,322,796.9 ns |    2 |    2398.91 KB |
| NilJS             | dromaeo-string-base64        |  26,099,779.7 ns |     56,563.69 ns |  26,113,464.1 ns |    3 |   19588.64 KB |
| YantraJS          | dromaeo-string-base64        |  34,386,093.8 ns |    895,233.21 ns |  34,005,037.5 ns |    4 |  763555.52 KB |
| Jurassic          | dromaeo-string-base64        |  46,923,833.1 ns |    170,833.45 ns |  46,924,454.5 ns |    5 |   73292.89 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-string-base64-modern |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-string-base64-modern |  27,929,216.1 ns |     36,571.68 ns |  27,925,329.7 ns |    1 |    2299.09 KB |
| Jint              | dromaeo-string-base64-modern |  29,754,939.1 ns |     39,285.65 ns |  29,761,678.1 ns |    2 |    2399.36 KB |
| NilJS             | dromaeo-string-base64-modern |  32,676,596.0 ns |    525,038.60 ns |  32,401,580.0 ns |    3 |   31360.23 KB |
| YantraJS          | dromaeo-string-base64-modern |  37,386,251.7 ns |  1,398,074.50 ns |  37,986,785.7 ns |    4 |  764771.13 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | evaluation                   |       4,280.9 ns |         30.74 ns |       4,283.9 ns |    1 |      16.11 KB |
| Jint              | evaluation                   |      14,021.2 ns |         66.82 ns |      14,002.1 ns |    2 |      26.73 KB |
| NilJS             | evaluation                   |      25,612.1 ns |         54.83 ns |      25,593.6 ns |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     135,253.5 ns |      1,003.43 ns |     135,568.6 ns |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,102,476.6 ns |     12,851.76 ns |   2,102,383.2 ns |    5 |     418.81 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | evaluation-modern            |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       4,127.3 ns |         33.72 ns |       4,129.6 ns |    1 |       15.6 KB |
| Jint              | evaluation-modern            |      13,631.7 ns |         43.13 ns |      13,630.3 ns |    2 |      26.71 KB |
| NilJS             | evaluation-modern            |      27,183.7 ns |         54.02 ns |      27,183.9 ns |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     134,871.4 ns |        833.99 ns |     135,085.4 ns |    4 |      703.4 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | linq-js                      |      58,165.9 ns |        239.33 ns |      58,180.9 ns |    1 |     195.13 KB |
| YantraJS          | linq-js                      |     331,166.4 ns |        865.17 ns |     331,376.9 ns |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,191,077.5 ns |      2,486.12 ns |   1,191,137.3 ns |    3 |    1294.33 KB |
| NilJS             | linq-js                      |   3,879,129.8 ns |      4,838.90 ns |   3,880,288.3 ns |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  35,798,176.0 ns |    589,280.01 ns |  35,723,253.6 ns |    5 |    9102.26 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | minimal                      |         929.7 ns |          5.10 ns |         930.2 ns |    1 |       8.45 KB |
| Jint              | minimal                      |       2,041.5 ns |         12.03 ns |       2,040.6 ns |    2 |      10.36 KB |
| NilJS             | minimal                      |       2,755.2 ns |         12.12 ns |       2,756.5 ns |    3 |       4.51 KB |
| YantraJS          | minimal                      |     131,824.8 ns |      1,343.93 ns |     132,053.6 ns |    4 |     697.62 KB |
| Jurassic          | minimal                      |   2,277,078.6 ns |      3,521.31 ns |   2,276,507.4 ns |    5 |     385.19 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch                    |  58,810,920.7 ns |    451,245.23 ns |  58,625,955.6 ns |    1 |  215655.06 KB |
| NilJS             | stopwatch                    | 133,501,419.6 ns |    426,328.56 ns | 133,520,450.0 ns |    2 |   94876.49 KB |
| Jurassic          | stopwatch                    | 140,804,658.9 ns |    577,536.54 ns | 140,758,412.5 ns |    3 |  156932.58 KB |
| Jint_ParsedScript | stopwatch                    | 148,748,183.9 ns |    476,074.63 ns | 148,701,000.0 ns |    4 |   12084.84 KB |
| Jint              | stopwatch                    | 157,502,048.3 ns |    973,739.18 ns | 157,557,975.0 ns |    5 |   12116.58 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch-modern             |  60,333,094.1 ns |    305,719.44 ns |  60,342,355.6 ns |    1 |  234033.07 KB |
| Jurassic          | stopwatch-modern             | 148,165,000.0 ns |    508,814.08 ns | 148,244,075.0 ns |    2 |  288625.25 KB |
| Jint_ParsedScript | stopwatch-modern             | 167,891,476.2 ns |    396,910.66 ns | 167,865,883.3 ns |    3 |   12085.44 KB |
| Jint              | stopwatch-modern             | 171,994,411.1 ns |    487,141.17 ns | 171,952,033.3 ns |    3 |   12117.77 KB |
| NilJS             | stopwatch-modern             | 218,241,531.1 ns |  1,206,977.88 ns | 218,150,433.3 ns |    4 |  324502.66 KB |
```
