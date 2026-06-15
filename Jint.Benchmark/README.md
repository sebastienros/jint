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

* **Tiny-script latency.** `minimal` 1.7 µs (1.7× faster than the field), `evaluation` 4.9 µs and
  `evaluation-modern` 5.0 µs (~5× faster). Jint has almost no per-run engine overhead.
* **Prepared scripts are dramatically faster than re-parsing.** `linq-js` drops from 1,227 µs
  (parse every run) to **60 µs** when reused — 20× — and that is 5.6× faster than the next engine
  (YantraJS 337 µs). Cache your `Prepared<Script>`.
* **Object & string workloads.** Fastest on `dromaeo-object-array` (17.2 ms, 1.6× ahead of
  YantraJS), `dromaeo-object-string` (48 ms, ~3× ahead of NiL.JS) and `dromaeo-object-regexp`
  (101 ms, ~5.4× ahead of NiL.JS).
* **Allocations are 1–2 orders of magnitude lower than the field**, which means far less GC
  pressure in real applications:

  | Benchmark | Jint | Best competitor | Worst competitor |
  |--- |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | NiL.JS 1,355 MB (64×) | Jurassic 1,431 MB (68×) |
  | `dromaeo-string-base64` | **2.4 MB** | NiL.JS 19 MB (8×) | YantraJS 764 MB (316×) |
  | `dromaeo-object-array` | **9.8 MB** | NiL.JS 17 MB (1.8×) | YantraJS 220 MB (22×) |
  | `stopwatch` | **12 MB** | NiL.JS 95 MB (8×) | YantraJS 216 MB (18×) |

**Where Jint trails**

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 8.6 ms vs YantraJS 2.7 ms (3.2×) and
  NiL.JS 6.7 ms (1.3×). YantraJS compiles to IL, so tight numeric loops favour it — this is the
  structural interpreter-vs-compiler gap.
* **Call- and closure-dispatch-heavy loops with frequent `new Date()`** (`stopwatch` /
  `stopwatch-modern`): slowest in raw time (~2.6–3.3× behind YantraJS), though it does the same work
  while allocating ~18× less memory.
* **`dromaeo-core-eval` / `dromaeo-string-base64`**: NiL.JS edges ahead on wall-clock (~2× and
  ~1.1×), while Jint allocates 4.5×/8× less.

## Latest results

Compared with the previous refresh (measured the same way on the same host), the
per-call closure-allocation fix that the old notes flagged as "pending" is now merged
([#2534](https://github.com/sebastienros/jint/pull/2534)), and two further allocation cuts landed on
`main`: [#2535](https://github.com/sebastienros/jint/pull/2535) shrinks `JsDate` by 8 bytes and
[#2536](https://github.com/sebastienros/jint/pull/2536) relocates `_privateElements` off every
object. Relative to the previous table (which already included #2534), `#2535`/`#2536` cut
`stopwatch` allocation a further ~18% (14.8 → 12.1 MB/op) with execution time flat, as that workload
is call-dispatch bound.

`YantraJS.Core` was also updated **1.2.404 → 1.2.405**, which **fixes the severe
`dromaeo-object-regexp` regression** present in 1.2.404 (previously ~35 s/op and ~32 GB allocated;
now ~0.7 s/op and ~0.8 GB) — bringing it back in line with the rest of the field.

Engine versions:

* Jint 4.10.0
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.405

Last updated 2026-06-15.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean           | StdDev         | Rank | Allocated     |
|------------------ |----------------------------- |---------------:|---------------:|-----:|--------------:|
| YantraJS          | array-stress                 |   2,886.738 μs |     33.7137 μs |    1 |   17123.82 KB |
| Jint_ParsedScript | array-stress                 |   2,921.552 μs |      7.2265 μs |    1 |    1227.26 KB |
| Jint              | array-stress                 |   2,924.027 μs |     10.5189 μs |    1 |    1256.05 KB |
| NilJS             | array-stress                 |   5,232.713 μs |     36.9404 μs |    2 |    4521.19 KB |
| Jurassic          | array-stress                 |   9,156.071 μs |     29.0176 μs |    3 |   11644.86 KB |
|                   |                              |                |                |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,710.769 μs |     12.1604 μs |    1 |    7596.01 KB |
| NilJS             | dromaeo-3d-cube              |   6,691.628 μs |     15.9072 μs |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   8,640.233 μs |     50.5719 μs |    3 |    2604.27 KB |
| Jint              | dromaeo-3d-cube              |   8,984.666 μs |     71.6001 μs |    4 |    2908.28 KB |
| Jurassic          | dromaeo-3d-cube              |  60,293.012 μs |    406.4090 μs |    5 |   10654.77 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |             NA |             NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,551.108 μs |     22.9985 μs |    1 |    7514.24 KB |
| NilJS             | dromaeo-3d-cube-modern       |   7,261.587 μs |     46.4219 μs |    2 |    5977.95 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   9,171.085 μs |     34.6663 μs |    3 |    2604.13 KB |
| Jint              | dromaeo-3d-cube-modern       |   9,630.650 μs |     56.2902 μs |    4 |    2907.95 KB |
|                   |                              |                |                |      |               |
| NilJS             | dromaeo-core-eval            |   1,261.363 μs |     10.7548 μs |    1 |     1577.1 KB |
| Jint              | dromaeo-core-eval            |   2,519.387 μs |     10.1976 μs |    2 |     352.29 KB |
| Jint_ParsedScript | dromaeo-core-eval            |   2,533.756 μs |     24.5240 μs |    2 |     331.89 KB |
| YantraJS          | dromaeo-core-eval            |   4,945.732 μs |     99.8282 μs |    3 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  17,456.272 μs |    121.3015 μs |    4 |    2876.11 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-core-eval-modern     |             NA |             NA |    ? |            NA |
| NilJS             | dromaeo-core-eval-modern     |   1,639.096 μs |     36.4909 μs |    1 |    1575.94 KB |
| Jint              | dromaeo-core-eval-modern     |   2,515.644 μs |     22.0924 μs |    2 |     351.53 KB |
| Jint_ParsedScript | dromaeo-core-eval-modern     |   2,561.776 μs |     16.7591 μs |    2 |     331.87 KB |
| YantraJS          | dromaeo-core-eval-modern     |   4,990.948 μs |     85.2302 μs |    3 |   35784.84 KB |
|                   |                              |                |                |      |               |
| Jint              | dromaeo-object-array         |  17,176.910 μs |    450.4919 μs |    1 |    9816.85 KB |
| Jint_ParsedScript | dromaeo-object-array         |  17,207.788 μs |    300.7036 μs |    1 |    9768.54 KB |
| YantraJS          | dromaeo-object-array         |  27,560.389 μs |    305.6235 μs |    2 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  37,742.300 μs |    728.4667 μs |    3 |   25808.63 KB |
| NilJS             | dromaeo-object-array         |  63,102.549 μs |    491.4785 μs |    4 |   17862.17 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-object-array-modern  |             NA |             NA |    ? |            NA |
| Jint              | dromaeo-object-array-modern  |  17,126.472 μs |    161.8500 μs |    1 |    9818.27 KB |
| Jint_ParsedScript | dromaeo-object-array-modern  |  18,983.831 μs |    211.8840 μs |    2 |    9770.93 KB |
| YantraJS          | dromaeo-object-array-modern  |  26,215.511 μs |    814.4717 μs |    3 |  223803.56 KB |
| NilJS             | dromaeo-object-array-modern  |  67,465.594 μs |    268.1233 μs |    4 |   17863.19 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        | 100,970.797 μs |  3,332.7229 μs |    1 |  157689.29 KB |
| Jint              | dromaeo-object-regexp        | 134,917.285 μs | 15,759.2708 μs |    2 |   161334.8 KB |
| NilJS             | dromaeo-object-regexp        | 549,693.946 μs | 26,815.7816 μs |    3 |  767381.57 KB |
| Jurassic          | dromaeo-object-regexp        | 685,661.021 μs | 19,471.5700 μs |    4 |  821477.01 KB |
| YantraJS          | dromaeo-object-regexp        | 716,822.771 μs |  2,177.4196 μs |    4 |  822909.02 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-object-regexp-modern |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 104,313.526 μs |  4,566.9229 μs |    1 |   156264.3 KB |
| Jint              | dromaeo-object-regexp-modern | 135,130.490 μs |  6,437.4111 μs |    2 |  158966.18 KB |
| NilJS             | dromaeo-object-regexp-modern | 533,347.213 μs |  8,622.6346 μs |    3 |  768499.52 KB |
| YantraJS          | dromaeo-object-regexp-modern | 714,024.260 μs |  4,766.2649 μs |    4 |  827834.27 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  48,357.118 μs |    878.0192 μs |    1 |   21541.45 KB |
| Jint              | dromaeo-object-string        |  58,390.439 μs |    916.7484 μs |    2 |   21666.15 KB |
| NilJS             | dromaeo-object-string        | 144,193.438 μs |  5,043.3192 μs |    3 | 1355058.42 KB |
| YantraJS          | dromaeo-object-string        | 172,557.731 μs |  6,345.2702 μs |    4 | 1652907.47 KB |
| Jurassic          | dromaeo-object-string        | 228,301.700 μs |  4,450.4256 μs |    5 | 1430513.48 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-object-string-modern |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-string-modern |  53,735.533 μs |  1,181.7368 μs |    1 |   21532.99 KB |
| Jint              | dromaeo-object-string-modern |  58,952.738 μs |    218.9964 μs |    2 |    21677.7 KB |
| NilJS             | dromaeo-object-string-modern | 146,026.583 μs |  3,498.6808 μs |    3 | 1355058.12 KB |
| YantraJS          | dromaeo-object-string-modern | 174,199.643 μs |  4,971.1987 μs |    4 | 1656305.25 KB |
|                   |                              |                |                |      |               |
| NilJS             | dromaeo-string-base64        |  26,533.186 μs |    493.2203 μs |    1 |   19588.63 KB |
| Jint              | dromaeo-string-base64        |  29,822.242 μs |     67.7097 μs |    2 |    2412.16 KB |
| Jint_ParsedScript | dromaeo-string-base64        |  31,082.196 μs |    147.1622 μs |    3 |    2312.26 KB |
| YantraJS          | dromaeo-string-base64        |  41,879.891 μs |  1,183.0879 μs |    4 |  763555.06 KB |
| Jurassic          | dromaeo-string-base64        |  49,262.553 μs |    351.4665 μs |    5 |   73292.98 KB |
|                   |                              |                |                |      |               |
| Jurassic          | dromaeo-string-base64-modern |             NA |             NA |    ? |            NA |
| Jint              | dromaeo-string-base64-modern |  32,303.111 μs |     91.7578 μs |    1 |    2413.12 KB |
| YantraJS          | dromaeo-string-base64-modern |  34,507.650 μs |  1,212.7514 μs |    2 |  764771.12 KB |
| Jint_ParsedScript | dromaeo-string-base64-modern |  34,585.740 μs |    102.1190 μs |    2 |    2312.81 KB |
| NilJS             | dromaeo-string-base64-modern |  35,819.449 μs |    240.1383 μs |    3 |   31360.18 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | evaluation                   |       4.910 μs |      0.0252 μs |    1 |      23.43 KB |
| Jint              | evaluation                   |      15.424 μs |      0.0922 μs |    2 |      34.07 KB |
| NilJS             | evaluation                   |      26.059 μs |      0.0975 μs |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     134.997 μs |      1.2712 μs |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,148.957 μs |      7.1065 μs |    5 |     418.81 KB |
|                   |                              |                |                |      |               |
| Jurassic          | evaluation-modern            |             NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       4.961 μs |      0.0185 μs |    1 |      22.99 KB |
| Jint              | evaluation-modern            |      14.907 μs |      0.1435 μs |    2 |      34.12 KB |
| NilJS             | evaluation-modern            |      26.989 μs |      0.1745 μs |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     135.454 μs |      2.0917 μs |    4 |      703.4 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | linq-js                      |      60.406 μs |      0.2348 μs |    1 |     176.56 KB |
| YantraJS          | linq-js                      |     337.363 μs |      2.0916 μs |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,227.433 μs |      5.6279 μs |    3 |    1275.76 KB |
| NilJS             | linq-js                      |   4,076.928 μs |     15.8097 μs |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  36,881.014 μs |    695.6649 μs |    5 |    9102.12 KB |
|                   |                              |                |                |      |               |
| Jint_ParsedScript | minimal                      |       1.667 μs |      0.0237 μs |    1 |      15.36 KB |
| Jint              | minimal                      |       2.795 μs |      0.0172 μs |    2 |      17.27 KB |
| NilJS             | minimal                      |       2.829 μs |      0.0171 μs |    2 |       4.51 KB |
| YantraJS          | minimal                      |     137.926 μs |      1.8444 μs |    3 |     697.62 KB |
| Jurassic          | minimal                      |   2,401.562 μs |     38.8046 μs |    4 |     385.19 KB |
|                   |                              |                |                |      |               |
| YantraJS          | stopwatch                    |  60,624.543 μs |    713.7583 μs |    1 |  215655.06 KB |
| NilJS             | stopwatch                    | 146,661.663 μs |    250.9987 μs |    2 |   94876.49 KB |
| Jurassic          | stopwatch                    | 147,112.862 μs |  1,636.4861 μs |    2 |  156932.58 KB |
| Jint_ParsedScript | stopwatch                    | 157,030.855 μs |  1,361.4125 μs |    3 |   12089.95 KB |
| Jint              | stopwatch                    | 158,362.040 μs |    609.8658 μs |    3 |   12121.72 KB |
|                   |                              |                |                |      |               |
| YantraJS          | stopwatch-modern             |  63,096.171 μs |    261.2323 μs |    1 |  234033.08 KB |
| Jurassic          | stopwatch-modern             | 154,911.290 μs |    866.0485 μs |    2 |  288624.72 KB |
| Jint_ParsedScript | stopwatch-modern             | 209,245.090 μs |  1,303.4226 μs |    3 |    12090.5 KB |
| Jint              | stopwatch-modern             | 211,952.844 μs |  1,233.1462 μs |    3 |   12122.86 KB |
| NilJS             | stopwatch-modern             | 244,387.231 μs |    759.2897 μs |    4 |  324502.66 KB |
```
