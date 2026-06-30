# Engine comparison benchmarks

This project benchmarks Jint against other managed JavaScript engines for .NET
([Jurassic](https://github.com/paiden/Jurassic), [NiL.JS](https://github.com/nilproject/NiL.JS) and
[YantraJS](https://github.com/yantrajs/yantra)) using a set of representative scripts.

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
  scripts. **Jurassic shows `NA` on every `-modern` row** because it predates ES2015 and cannot
  parse that syntax.
* `Mean` is time per operation (lower is better); `Allocated` is managed memory per operation
  (lower is better). `Rank` groups results that are statistically tied.

## Highlights — strengths & weaknesses

Numbers below use Jint's recommended production path (`Jint_ParsedScript`, i.e. a cached prepared
script) unless noted; "field" means the best result from the other three engines.

**Where Jint leads**

* **Tiny-script latency.** `minimal` 1.7 µs (1.6× faster than the field), `evaluation` 5.0 µs and
  `evaluation-modern` 4.9 µs (~5× faster). Jint has almost no per-run engine overhead.
* **Prepared scripts are dramatically faster than re-parsing.** `linq-js` drops from 1,185 µs
  (parse every run) to **61 µs** when reused — ~19× — and that is 5.3× faster than the next engine
  (YantraJS 325 µs). Cache your `Prepared<Script>`.
* **Object & string workloads.** Fastest on `dromaeo-object-array` (15.1 ms, 1.6× ahead of
  YantraJS), `dromaeo-object-string` (46 ms, ~2.8× ahead of NiL.JS) and `dromaeo-object-regexp`
  (96 ms, ~5.6× ahead of NiL.JS).
* **Allocations are 1–2 orders of magnitude lower than the field**, which means far less GC
  pressure in real applications:

  | Benchmark | Jint | Best competitor | Worst competitor |
  |--- |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | NiL.JS 1,355 MB (63×) | Jurassic 1,431 MB (66×) |
  | `dromaeo-string-base64` | **2.3 MB** | NiL.JS 19 MB (8×) | YantraJS 764 MB (330×) |
  | `dromaeo-object-array` | **9.1 MB** | NiL.JS 17 MB (2.0×) | YantraJS 220 MB (24×) |
  | `stopwatch` | **12 MB** | NiL.JS 95 MB (8×) | YantraJS 216 MB (18×) |

**Where Jint trails**

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 8.5 ms vs YantraJS 2.5 ms (3.4×) and
  NiL.JS 6.1 ms (1.4×). YantraJS compiles to IL, so tight numeric loops favour it — this is the
  structural interpreter-vs-compiler gap.
* **Call- and closure-dispatch-heavy loops with frequent `new Date()`** (`stopwatch` /
  `stopwatch-modern`): slowest in raw time (~2.8–3.6× behind YantraJS), though it does the same work
  while allocating ~18× less memory.
* **`dromaeo-core-eval` / `dromaeo-string-base64`**: NiL.JS edges ahead on wall-clock (~2× and
  ~1.1×), while Jint allocates 4.7×/8.5× less.

## Latest results

This refresh follows a substantial round of allocation and dispatch tuning landed on `main` since the
`v4.10.1` release (14 commits). The largest pieces are hidden-class **shapes** for object literals
([#2552](https://github.com/sebastienros/jint/pull/2552)) and hot constructors
([#2553](https://github.com/sebastienros/jint/pull/2553)); **in-object properties** that let a small
object store its shape and first few slots in a single allocation
([#2559](https://github.com/sebastienros/jint/pull/2559)); source-generated **built-in shapes** for the
intrinsics ([#2554](https://github.com/sebastienros/jint/pull/2554)–[#2557](https://github.com/sebastienros/jint/pull/2557));
a **write-side inline cache** for `obj.prop = value` ([#2546](https://github.com/sebastienros/jint/pull/2546))
and a **prototype-method inline cache** for `obj.method()` ([#2558](https://github.com/sebastienros/jint/pull/2558));
environment pooling for direct recursion ([#2549](https://github.com/sebastienros/jint/pull/2549)); and
zero-copy `SlicedString` search ([#2547](https://github.com/sebastienros/jint/pull/2547)).

Measured against `v4.10.1` on the same host (Jint-only A/B in one thermal window), the net effect is:

* **Faster on the array/object-dispatch-heavy scripts**, driven by the prototype-method inline cache:
  `dromaeo-object-array` **−12.7%** (15.1 ms, from 17.0 ms), `dromaeo-object-array-modern` −3%, and
  `array-stress` **−6.5%** on the prepared path; the re-parse path shows the same direction (−7%, −12%,
  −12%). These are the only time deltas that move consistently in both execution paths.
* **Allocation is flat** (within ±2%) on every script except `linq-js`, where it rises ~28 KB/op
  (175 → 203 KB on the prepared path) — the one workload that builds many distinct object shapes per
  run, so the new shape metadata is rebuilt each operation. It is still 5× below YantraJS and 13× below
  NiL.JS, and the absolute increase is ~28 KB.
* **No alarming regressions.** The remaining time deltas (`stopwatch`, `dromaeo-object-string`,
  `dromaeo-string-base64`) sit inside these scripts' run-to-run variance: within a single run the
  re-parse (`Jint`) and prepared (`Jint_ParsedScript`) timings for the same script disagree by more than
  the deltas and flip sign between runs, so they are measurement noise rather than a real change.

Engine versions:

* Jint `main` @ `9c0266e6c` (post-4.10.1)
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.406

Last updated 2026-06-30.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean           | StdDev         | Rank | Allocated     |
|------------------ |----------------------------- |---------------:|---------------:|-----:|--------------:|
| Jint              | array-stress                 |   2,518.953 μs |      7.8094 μs |    1 |    1095.84 KB |
| Jint_ParsedScript | array-stress                 |   2,528.321 μs |     10.3647 μs |    1 |    1067.05 KB |
| YantraJS          | array-stress                 |   2,960.967 μs |     38.5909 μs |    2 |   17123.82 KB |
| NilJS             | array-stress                 |   4,784.033 μs |     26.4139 μs |    3 |    4521.19 KB |
| Jurassic          | array-stress                 |   8,984.306 μs |     28.2604 μs |    4 |   11644.47 KB |
|                   |                              |                |                |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,527.770 μs |     15.7270 μs |    1 |    7596.01 KB |
| NilJS             | dromaeo-3d-cube              |   6,062.183 μs |     20.0349 μs |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   8,514.003 μs |     23.3835 μs |    3 |    2488.21 KB |
| Jint              | dromaeo-3d-cube              |   8,889.444 μs |     31.5711 μs |    4 |     2792.8 KB |
| Jurassic          | dromaeo-3d-cube              |  54,767.277 μs |    252.4594 μs |    5 |   10654.72 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |             NA |             NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,443.757 μs |     15.2978 μs |    1 |    7514.24 KB |
| NilJS             | dromaeo-3d-cube-modern       |   6,995.874 μs |     27.6265 μs |    2 |    5977.95 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   8,556.056 μs |     23.8652 μs |    3 |    2488.06 KB |
| Jint              | dromaeo-3d-cube-modern       |   9,012.658 μs |     32.9011 μs |    4 |    2792.32 KB |
|                   |                              |                |                |      |               |
| NilJS             | dromaeo-core-eval            |   1,211.770 μs |      5.8758 μs |    1 |     1577.1 KB |
| Jint_ParsedScript | dromaeo-core-eval            |   2,426.559 μs |      9.2249 μs |    2 |     337.77 KB |
| Jint              | dromaeo-core-eval            |   2,454.576 μs |      7.3505 μs |    2 |     358.27 KB |
| YantraJS          | dromaeo-core-eval            |   4,752.636 μs |     37.7421 μs |    3 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  16,517.154 μs |     74.2371 μs |    4 |    2876.04 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-core-eval-modern     |             NA |             NA |    ? |            NA |
| NilJS             | dromaeo-core-eval-modern     |   1,390.878 μs |      6.3405 μs |    1 |    1575.94 KB |
| Jint_ParsedScript | dromaeo-core-eval-modern     |   2,447.346 μs |      7.5377 μs |    2 |     337.74 KB |
| Jint              | dromaeo-core-eval-modern     |   2,449.645 μs |     12.5204 μs |    2 |     357.52 KB |
| YantraJS          | dromaeo-core-eval-modern     |   4,801.661 μs |     32.4183 μs |    3 |   35784.84 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-array         |  15,077.730 μs |    112.4341 μs |    1 |    9122.72 KB |
| Jint              | dromaeo-object-array         |  15,650.133 μs |     29.1578 μs |    2 |    9171.24 KB |
| YantraJS          | dromaeo-object-array         |  24,844.211 μs |     52.2043 μs |    3 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  34,864.571 μs |    499.3787 μs |    4 |   25809.24 KB |
| NilJS             | dromaeo-object-array         |  53,792.302 μs |  1,053.6066 μs |    5 |   17862.17 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-object-array-modern  |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-array-modern  |  15,226.230 μs |    150.3676 μs |    1 |    9125.16 KB |
| Jint              | dromaeo-object-array-modern  |  15,394.691 μs |     95.8232 μs |    1 |    9172.67 KB |
| YantraJS          | dromaeo-object-array-modern  |  24,542.134 μs |    359.5132 μs |    2 |  223803.56 KB |
| NilJS             | dromaeo-object-array-modern  |  53,197.595 μs |    305.4380 μs |    3 |   17863.19 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        |  96,336.133 μs |  4,009.1237 μs |    1 |  157931.56 KB |
| Jint              | dromaeo-object-regexp        | 132,240.268 μs | 11,814.1213 μs |    2 |  165683.45 KB |
| NilJS             | dromaeo-object-regexp        | 535,069.240 μs |  6,560.7694 μs |    3 |  767236.66 KB |
| Jurassic          | dromaeo-object-regexp        | 679,564.467 μs | 18,599.8578 μs |    4 |  820519.94 KB |
| YantraJS          | dromaeo-object-regexp        | 712,866.573 μs |  8,147.7807 μs |    4 |  824975.95 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-object-regexp-modern |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 101,233.603 μs |  3,310.6203 μs |    1 |  160552.93 KB |
| Jint              | dromaeo-object-regexp-modern | 125,806.193 μs |  4,531.8286 μs |    2 |  159230.47 KB |
| NilJS             | dromaeo-object-regexp-modern | 535,955.080 μs |  7,291.0197 μs |    3 |  766333.41 KB |
| YantraJS          | dromaeo-object-regexp-modern | 712,426.913 μs |  9,861.5331 μs |    4 |  828629.54 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  46,182.027 μs |    815.5121 μs |    1 |   21512.98 KB |
| Jint              | dromaeo-object-string        |  49,440.511 μs |    819.0026 μs |    2 |   21693.88 KB |
| NilJS             | dromaeo-object-string        | 129,801.794 μs |  2,487.9976 μs |    3 | 1354984.23 KB |
| YantraJS          | dromaeo-object-string        | 153,269.826 μs |  3,809.8240 μs |    4 | 1653052.58 KB |
| Jurassic          | dromaeo-object-string        | 208,326.476 μs |  6,558.2119 μs |    5 | 1430514.92 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-object-string-modern |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-string-modern |  54,990.045 μs |  1,175.9424 μs |    1 |   21541.65 KB |
| Jint              | dromaeo-object-string-modern |  61,505.786 μs |  1,300.4658 μs |    2 |   21655.88 KB |
| NilJS             | dromaeo-object-string-modern | 128,688.137 μs |  2,184.2749 μs |    3 | 1354978.92 KB |
| YantraJS          | dromaeo-object-string-modern | 157,023.438 μs |  5,933.2701 μs |    4 | 1656486.75 KB |
|                   |                              |                |                |      |               |
| NilJS             | dromaeo-string-base64        |  25,682.022 μs |    160.8848 μs |    1 |   19588.64 KB |
| Jint_ParsedScript | dromaeo-string-base64        |  27,735.817 μs |     76.7722 μs |    2 |    2312.48 KB |
| Jint              | dromaeo-string-base64        |  28,108.714 μs |     59.8655 μs |    2 |     2412.6 KB |
| YantraJS          | dromaeo-string-base64        |  31,934.862 μs |    392.0349 μs |    3 |  763555.52 KB |
| Jurassic          | dromaeo-string-base64        |  46,556.841 μs |    321.5111 μs |    4 |   73291.91 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-string-base64-modern |             NA |             NA |    ? |            NA |
| NilJS             | dromaeo-string-base64-modern |  31,570.518 μs |    412.6986 μs |    1 |   31360.22 KB |
| YantraJS          | dromaeo-string-base64-modern |  32,388.468 μs |    194.5287 μs |    1 |  764771.49 KB |
| Jint              | dromaeo-string-base64-modern |  33,505.326 μs |    121.6692 μs |    2 |    2413.58 KB |
| Jint_ParsedScript | dromaeo-string-base64-modern |  33,541.127 μs |    123.4492 μs |    2 |    2313.05 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | evaluation                   |       4.988 μs |      0.0303 μs |    1 |      23.79 KB |
| Jint              | evaluation                   |      14.674 μs |      0.0805 μs |    2 |      34.59 KB |
| NilJS             | evaluation                   |      25.171 μs |      0.1151 μs |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     127.706 μs |      1.2575 μs |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,069.014 μs |      7.4161 μs |    5 |     418.92 KB |
|                   |                              |                |                |      |               |
| Jurassic          | evaluation-modern            |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       4.924 μs |      0.0401 μs |    1 |      23.68 KB |
| Jint              | evaluation-modern            |      14.521 μs |      0.0527 μs |    2 |      34.82 KB |
| NilJS             | evaluation-modern            |      26.655 μs |      0.1339 μs |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     129.847 μs |      0.9274 μs |    4 |      703.4 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | linq-js                      |      61.424 μs |      1.5401 μs |    1 |     202.95 KB |
| YantraJS          | linq-js                      |     325.472 μs |      1.9735 μs |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,185.429 μs |      5.4544 μs |    3 |     1302.2 KB |
| NilJS             | linq-js                      |   3,990.001 μs |     30.0132 μs |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  37,258.040 μs |    845.9397 μs |    5 |    9102.43 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | minimal                      |       1.719 μs |      0.0191 μs |    1 |      15.38 KB |
| Jint              | minimal                      |       2.772 μs |      0.0256 μs |    2 |       17.3 KB |
| NilJS             | minimal                      |       2.832 μs |      0.0167 μs |    2 |       4.51 KB |
| YantraJS          | minimal                      |     126.271 μs |      1.0986 μs |    3 |     697.62 KB |
| Jurassic          | minimal                      |   2,344.863 μs |     30.9041 μs |    4 |     385.19 KB |
|                   |                              |                |                |      |               |
| YantraJS          | stopwatch                    |  57,557.737 μs |    713.0743 μs |    1 |  215655.06 KB |
| NilJS             | stopwatch                    | 137,200.496 μs |    792.0322 μs |    2 |   94876.49 KB |
| Jurassic          | stopwatch                    | 141,117.788 μs |  1,659.7159 μs |    2 |  156932.58 KB |
| Jint              | stopwatch                    | 154,632.017 μs |    832.6555 μs |    3 |   12124.02 KB |
| Jint_ParsedScript | stopwatch                    | 163,590.646 μs |  1,434.6458 μs |    4 |   12092.18 KB |
|                   |                              |                |                |      |               |
| YantraJS          | stopwatch-modern             |  58,858.768 μs |    365.5048 μs |    1 |  234033.07 KB |
| Jurassic          | stopwatch-modern             | 149,185.618 μs |  2,207.8551 μs |    2 |  288625.25 KB |
| Jint_ParsedScript | stopwatch-modern             | 216,826.869 μs |  1,085.9807 μs |    3 |   12092.73 KB |
| Jint              | stopwatch-modern             | 220,518.983 μs |  2,102.3338 μs |    3 |   12125.16 KB |
| NilJS             | stopwatch-modern             | 237,614.307 μs |  2,865.5620 μs |    4 |  324502.66 KB |
```
