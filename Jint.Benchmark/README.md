# Engine comparison benchmarks

This project benchmarks Jint against the other managed JavaScript engines for .NET
([Jurassic](https://github.com/paiden/Jurassic), [NiL.JS](https://github.com/nilproject/NiL.JS) and
[YantraJS](https://github.com/yantrajs/yantra)) across a set of representative scripts.

## At a glance (Jint 4.11.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on most workloads.** It leads the object, string and regex scripts by **1.7×–5×**
  over the next-fastest engine, and starts tiny scripts up **~1.6×–5×** faster.
* **By far the lowest memory use.** Typically **2×–63×** less allocation than the closest competitor
  — and up to **330×** less than the highest in the field — which means much lighter GC pressure in
  real applications.
* **Two structural gaps remain,** both narrowed in 4.11.0: heavy floating-point/matrix math and
  call-plus-`new Date()`-heavy loops, where an engine that compiles JavaScript to IL pulls ahead on
  tight numeric loops.

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

* **Tiny-script latency.** `minimal` runs in 1.8 µs and `evaluation` / `evaluation-modern` in ~5 µs
  — **1.6× and ~5× faster than the closest competitor**. Jint has almost no per-run engine overhead.
* **Cached scripts avoid re-parsing.** `linq-js` drops from 1,146 µs (re-parsed every run) to
  **58 µs** when the prepared script is reused — ~20× — which is also **5.6× faster than the
  next-fastest engine**. Cache your `Prepared<Script>` in production.
* **Object, string and regex workloads.** Jint is the fastest engine on `dromaeo-object-array`,
  `dromaeo-object-string` and `dromaeo-object-regexp` — **1.7×**, **2.5×** and **~5×** faster than
  the closest competitor respectively.
* **Allocations are one to two orders of magnitude lower than the field,** which means far less GC
  pressure in real applications:

  | Benchmark | Jint | Closest competitor | Highest in field |
  |--- |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | 1,355 MB (63×) | 1,653 MB (77×) |
  | `dromaeo-string-base64` | **2.3 MB** | 19 MB (8.5×) | 764 MB (330×) |
  | `dromaeo-object-array` | **8.9 MB** | 17 MB (2.0×) | 220 MB (24×) |
  | `stopwatch` | **12 MB** | 95 MB (7.8×) | 216 MB (18×) |

## Where Jint trails

Two workloads still favour an engine that compiles JavaScript to IL, where tight numeric loops run
closer to native speed. Both gaps narrowed in 4.11.0:

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 7.7 ms, now **1.3× behind the closest
  competitor** (down from ~1.4×) — the structural interpreter-vs-compiler gap. Jint still allocates
  the least of any engine on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  within **~1.1× of the closest managed interpreter** while allocating ~8–18× less memory; the
  IL-compiling engine is ~2.6× faster in raw time.
* **`dromaeo-core-eval`**: **1.3× behind the closest competitor** (down from ~1.7× before the
  slot-backed eval environments landed), while allocating ~4.6× less. `dromaeo-string-base64` is
  effectively at parity (~1.05×).

## What's new in 4.11.0

Measured against the previous refresh — competitor versions are unchanged, and their rows were used
as a thermal canary to confirm the window is comparable:

* **`dromaeo-core-eval` −27%** (2.10 → 1.53 ms prepared; the modern variant −31%). Relational tests
  and plain assignments against slot-stored numbers now take unboxed fast lanes
  ([#2574](https://github.com/sebastienros/jint/pull/2574),
  [#2577](https://github.com/sebastienros/jint/pull/2577),
  [#2578](https://github.com/sebastienros/jint/pull/2578)), and `Function`-constructor instances
  reuse a definition-level environment ([#2579](https://github.com/sebastienros/jint/pull/2579)) —
  both of which the eval loop bodies exercise directly.
* **`dromaeo-3d-cube` −13–15%** (8.9 → 7.7 ms). The same unboxed loop lanes remove per-iteration
  boxing from the matrix loops, which also cut this script's allocation by ~16%.
* **`stopwatch` −10%** and **`dromaeo-string-base64` −5–12%** from the same loop and environment
  work.
* Prepared scripts no longer pin the engine that created them
  ([#2568](https://github.com/sebastienros/jint/pull/2568)), generator and async functions share
  compiled statement lists across invocations
  ([#2571](https://github.com/sebastienros/jint/pull/2571)), and `Function.prototype.toString`
  source-text retention became opt-in to reduce engine memory
  ([#2562](https://github.com/sebastienros/jint/pull/2562)).

No regressions: every Jint row is flat or faster than the previous refresh, with allocation flat or
lower.

## Engine versions

* Jint 4.11.0
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.406

Last updated 2026-07-05.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean           | StdDev         | Median         | Rank | Allocated     |
|------------------ |----------------------------- |---------------:|---------------:|---------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress                 |   2,507.980 μs |     12.2973 μs |   2,507.366 μs |    1 |    1067.55 KB |
| Jint              | array-stress                 |   2,672.680 μs |     14.1012 μs |   2,675.319 μs |    2 |    1096.22 KB |
| YantraJS          | array-stress                 |   2,746.542 μs |     14.0964 μs |   2,741.935 μs |    2 |   17123.82 KB |
| NilJS             | array-stress                 |   4,871.809 μs |    205.9993 μs |   4,757.379 μs |    3 |    4521.19 KB |
| Jurassic          | array-stress                 |   9,253.594 μs |    115.1944 μs |   9,263.347 μs |    4 |   11644.85 KB |
|                   |                              |                |                |                |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,459.015 μs |      7.5855 μs |   2,454.452 μs |    1 |    7596.01 KB |
| NilJS             | dromaeo-3d-cube              |   6,121.013 μs |     11.2652 μs |   6,118.626 μs |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   7,744.715 μs |     13.5961 μs |   7,741.244 μs |    3 |    2081.22 KB |
| Jint              | dromaeo-3d-cube              |   8,049.678 μs |     16.3419 μs |   8,050.702 μs |    4 |    2384.73 KB |
| Jurassic          | dromaeo-3d-cube              |  54,451.125 μs |    107.9230 μs |  54,473.500 μs |    5 |   10654.72 KB |
|                   |                              |                |                |                |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |             NA |             NA |             NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,400.455 μs |      7.7403 μs |   2,398.773 μs |    1 |    7514.24 KB |
| NilJS             | dromaeo-3d-cube-modern       |   6,806.229 μs |     12.5696 μs |   6,804.891 μs |    2 |    5977.95 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   7,817.215 μs |     18.2190 μs |   7,816.758 μs |    3 |    2080.89 KB |
| Jint              | dromaeo-3d-cube-modern       |   8,242.919 μs |     25.8936 μs |   8,246.635 μs |    4 |    2384.21 KB |
|                   |                              |                |                |                |      |               |
| NilJS             | dromaeo-core-eval            |   1,183.325 μs |      2.9017 μs |   1,183.702 μs |    1 |     1577.1 KB |
| Jint              | dromaeo-core-eval            |   1,524.158 μs |      3.4301 μs |   1,522.164 μs |    2 |     359.93 KB |
| Jint_ParsedScript | dromaeo-core-eval            |   1,530.149 μs |      3.9354 μs |   1,530.529 μs |    2 |     339.53 KB |
| YantraJS          | dromaeo-core-eval            |   4,700.705 μs |     32.8987 μs |   4,702.885 μs |    3 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  16,382.213 μs |     75.4400 μs |  16,353.050 μs |    4 |    2876.11 KB |
|                   |                              |                |                |                |      |               |
| Jurassic          | dromaeo-core-eval-modern     |             NA |             NA |             NA |    ? |            NA |
| NilJS             | dromaeo-core-eval-modern     |   1,403.333 μs |      5.8092 μs |   1,404.345 μs |    1 |    1575.94 KB |
| Jint              | dromaeo-core-eval-modern     |   1,514.636 μs |      5.5085 μs |   1,512.482 μs |    2 |     359.17 KB |
| Jint_ParsedScript | dromaeo-core-eval-modern     |   1,532.844 μs |      6.3219 μs |   1,535.039 μs |    2 |     339.51 KB |
| YantraJS          | dromaeo-core-eval-modern     |   5,052.918 μs |     50.5803 μs |   5,057.198 μs |    3 |   35784.84 KB |
|                   |                              |                |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-array         |  14,761.805 μs |    133.6431 μs |  14,705.813 μs |    1 |    9123.38 KB |
| Jint              | dromaeo-object-array         |  15,422.940 μs |     86.3368 μs |  15,453.362 μs |    2 |    9171.68 KB |
| YantraJS          | dromaeo-object-array         |  24,906.868 μs |     46.5200 μs |  24,904.231 μs |    3 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  35,304.577 μs |    335.8774 μs |  35,245.373 μs |    4 |   25809.33 KB |
| NilJS             | dromaeo-object-array         |  50,801.704 μs |    108.6013 μs |  50,779.850 μs |    5 |   17862.17 KB |
|                   |                              |                |                |                |      |               |
| Jurassic          | dromaeo-object-array-modern  |             NA |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-array-modern  |  14,700.769 μs |     33.5371 μs |  14,702.761 μs |    1 |    9125.81 KB |
| Jint              | dromaeo-object-array-modern  |  15,759.530 μs |    116.9909 μs |  15,692.602 μs |    2 |     9173.1 KB |
| YantraJS          | dromaeo-object-array-modern  |  24,299.814 μs |    121.1291 μs |  24,329.106 μs |    3 |  223803.56 KB |
| NilJS             | dromaeo-object-array-modern  |  50,781.086 μs |    154.9065 μs |  50,814.540 μs |    4 |   17863.19 KB |
|                   |                              |                |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        | 109,595.207 μs |  4,370.1477 μs | 110,119.717 μs |    1 |  157463.76 KB |
| Jint              | dromaeo-object-regexp        | 132,537.133 μs |  4,813.1950 μs | 131,899.100 μs |    2 |  161187.45 KB |
| NilJS             | dromaeo-object-regexp        | 530,201.946 μs |  6,051.9001 μs | 531,062.700 μs |    3 |  767110.49 KB |
| Jurassic          | dromaeo-object-regexp        | 676,647.048 μs | 15,647.5804 μs | 677,489.800 μs |    4 |  823155.92 KB |
| YantraJS          | dromaeo-object-regexp        | 714,261.167 μs |  6,292.5178 μs | 717,065.200 μs |    5 |  819040.14 KB |
|                   |                              |                |                |                |      |               |
| Jurassic          | dromaeo-object-regexp-modern |             NA |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 107,069.813 μs |  4,354.6856 μs | 105,663.083 μs |    1 |   157535.6 KB |
| Jint              | dromaeo-object-regexp-modern | 126,174.776 μs |  5,525.4517 μs | 126,010.383 μs |    2 |  156223.86 KB |
| NilJS             | dromaeo-object-regexp-modern | 538,730.380 μs |  7,468.7515 μs | 539,171.300 μs |    3 |  765846.56 KB |
| YantraJS          | dromaeo-object-regexp-modern | 718,185.533 μs |  8,535.5319 μs | 720,441.900 μs |    4 |   829299.1 KB |
|                   |                              |                |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  54,045.862 μs |  1,973.4703 μs |  54,097.160 μs |    1 |   21522.49 KB |
| Jint              | dromaeo-object-string        |  57,517.314 μs |  1,803.5601 μs |  57,720.510 μs |    2 |   21676.78 KB |
| NilJS             | dromaeo-object-string        | 136,105.693 μs |  3,867.7925 μs | 136,008.967 μs |    3 | 1355134.86 KB |
| YantraJS          | dromaeo-object-string        | 163,005.990 μs |  4,766.9870 μs | 164,164.733 μs |    4 | 1653049.32 KB |
| Jurassic          | dromaeo-object-string        | 221,511.155 μs |  5,100.3311 μs | 222,147.950 μs |    5 | 1430684.63 KB |
|                   |                              |                |                |                |      |               |
| Jurassic          | dromaeo-object-string-modern |             NA |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-string-modern |  58,599.782 μs |  1,780.4470 μs |  59,432.489 μs |    1 |   21343.15 KB |
| Jint              | dromaeo-object-string-modern |  60,366.847 μs |    823.8176 μs |  60,635.744 μs |    1 |    21448.1 KB |
| NilJS             | dromaeo-object-string-modern | 144,215.687 μs |  3,566.8382 μs | 144,874.000 μs |    2 | 1354964.23 KB |
| YantraJS          | dromaeo-object-string-modern | 170,115.801 μs |  5,060.9844 μs | 170,194.600 μs |    3 | 1656379.85 KB |
|                   |                              |                |                |                |      |               |
| NilJS             | dromaeo-string-base64        |  26,022.877 μs |     68.5868 μs |  26,011.705 μs |    1 |   19588.64 KB |
| Jint              | dromaeo-string-base64        |  27,302.883 μs |     64.3246 μs |  27,330.278 μs |    2 |    2412.72 KB |
| Jint_ParsedScript | dromaeo-string-base64        |  27,963.553 μs |     50.4349 μs |  27,969.716 μs |    3 |    2312.85 KB |
| YantraJS          | dromaeo-string-base64        |  39,251.315 μs |  1,018.4019 μs |  39,176.779 μs |    4 |  763555.07 KB |
| Jurassic          | dromaeo-string-base64        |  48,569.630 μs |    168.4504 μs |  48,534.773 μs |    5 |   73293.29 KB |
|                   |                              |                |                |                |      |               |
| Jurassic          | dromaeo-string-base64-modern |             NA |             NA |             NA |    ? |            NA |
| Jint              | dromaeo-string-base64-modern |  31,207.237 μs |    279.0938 μs |  31,134.037 μs |    1 |    2413.74 KB |
| NilJS             | dromaeo-string-base64-modern |  31,207.439 μs |    123.0813 μs |  31,225.719 μs |    1 |   31360.22 KB |
| Jint_ParsedScript | dromaeo-string-base64-modern |  32,003.537 μs |    136.7078 μs |  31,965.763 μs |    1 |    2313.47 KB |
| YantraJS          | dromaeo-string-base64-modern |  34,461.412 μs |    342.0384 μs |  34,526.700 μs |    2 |  764771.51 KB |
|                   |                              |                |                |                |      |               |
| Jint_ParsedScript | evaluation                   |       4.913 μs |      0.0200 μs |       4.913 μs |    1 |      24.05 KB |
| Jint              | evaluation                   |      14.982 μs |      0.0489 μs |      14.957 μs |    2 |      34.67 KB |
| NilJS             | evaluation                   |      25.096 μs |      0.0992 μs |      25.092 μs |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     128.469 μs |      1.1159 μs |     128.587 μs |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,067.093 μs |      6.6823 μs |   2,067.950 μs |    5 |     418.81 KB |
|                   |                              |                |                |                |      |               |
| Jurassic          | evaluation-modern            |             NA |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       5.043 μs |      0.0375 μs |       5.037 μs |    1 |      23.76 KB |
| Jint              | evaluation-modern            |      14.650 μs |      0.0924 μs |      14.641 μs |    2 |      34.87 KB |
| NilJS             | evaluation-modern            |      25.763 μs |      0.0644 μs |      25.771 μs |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     128.430 μs |      1.1246 μs |     128.412 μs |    4 |      703.4 KB |
|                   |                              |                |                |                |      |               |
| Jint_ParsedScript | linq-js                      |      57.857 μs |      0.2289 μs |      57.876 μs |    1 |     205.55 KB |
| YantraJS          | linq-js                      |     324.962 μs |      1.9464 μs |     325.345 μs |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,146.134 μs |      4.6014 μs |   1,147.805 μs |    3 |    1304.75 KB |
| NilJS             | linq-js                      |   3,907.824 μs |     12.4780 μs |   3,903.773 μs |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  36,108.272 μs |    416.4013 μs |  36,181.853 μs |    5 |    9103.62 KB |
|                   |                              |                |                |                |      |               |
| Jint_ParsedScript | minimal                      |       1.756 μs |      0.0226 μs |       1.763 μs |    1 |      15.38 KB |
| Jint              | minimal                      |       2.794 μs |      0.0187 μs |       2.790 μs |    2 |       17.3 KB |
| NilJS             | minimal                      |       2.804 μs |      0.0046 μs |       2.804 μs |    2 |       4.51 KB |
| YantraJS          | minimal                      |     131.215 μs |      1.4809 μs |     131.565 μs |    3 |     697.62 KB |
| Jurassic          | minimal                      |   2,336.103 μs |      7.2096 μs |   2,334.343 μs |    4 |     385.19 KB |
|                   |                              |                |                |                |      |               |
| YantraJS          | stopwatch                    |  57,162.139 μs |    240.6361 μs |  57,165.517 μs |    1 |  215655.06 KB |
| NilJS             | stopwatch                    | 137,431.840 μs |  1,335.8509 μs | 136,724.425 μs |    2 |   94876.49 KB |
| Jurassic          | stopwatch                    | 141,028.615 μs |    868.8798 μs | 141,257.200 μs |    2 |  156932.58 KB |
| Jint_ParsedScript | stopwatch                    | 150,330.587 μs |    445.0339 μs | 150,346.962 μs |    3 |   12092.41 KB |
| Jint              | stopwatch                    | 153,102.883 μs |    707.0369 μs | 152,985.150 μs |    3 |   12124.15 KB |
|                   |                              |                |                |                |      |               |
| YantraJS          | stopwatch-modern             |  60,500.106 μs |    242.8719 μs |  60,518.261 μs |    1 |  234033.07 KB |
| Jurassic          | stopwatch-modern             | 148,933.362 μs |    485.1544 μs | 148,875.250 μs |    2 |  288625.25 KB |
| Jint              | stopwatch-modern             | 207,374.887 μs |    414.9286 μs | 207,420.367 μs |    3 |   12125.29 KB |
| Jint_ParsedScript | stopwatch-modern             | 215,143.827 μs |    497.4181 μs | 215,266.900 μs |    4 |   12092.96 KB |
| NilJS             | stopwatch-modern             | 218,288.804 μs |  1,645.3758 μs | 217,556.433 μs |    4 |  324502.66 KB |
```
