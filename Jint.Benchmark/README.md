# Engine comparison benchmarks

This project benchmarks Jint against the other managed JavaScript engines for .NET
([Jurassic](https://github.com/paiden/Jurassic), [NiL.JS](https://github.com/nilproject/NiL.JS) and
[YantraJS](https://github.com/yantrajs/yantra)) across a set of representative scripts.

## At a glance (current main, post-4.11.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on almost every workload.** It leads the object, string, base64 and regex scripts
  by **1.04×–4.6×** over the next-fastest engine, starts tiny scripts up **3×–6×** faster
  (`minimal` executes in **under a microsecond**), and leads `dromaeo-core-eval` — the last row
  another interpreter used to win.
* **Fastest interpreter on heavy floating-point math.** `dromaeo-3d-cube` runs ahead of every other
  interpreter; only the IL-compiling engine remains structurally faster on tight numeric loops.
* **By far the lowest memory use.** Typically **2×–63×** less allocation than the closest competitor
  — and up to **470×** less than the highest in the field — which means much lighter GC pressure in
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

* **Tiny-script latency.** `minimal` runs in **0.92 µs** and `evaluation` / `evaluation-modern` in
  ~4.2–4.5 µs — **3× and 6× faster than the closest competitor**.
* **Cached scripts avoid re-parsing.** `linq-js` drops from 1,188 µs (re-parsed every run) to
  **57 µs** when the prepared script is reused — ~20× — which is also **5.7× faster than the
  next-fastest engine**. Cache your `Prepared<Script>` in production.
* **eval-heavy code.** `dromaeo-core-eval` runs in **0.93 ms — 1.3× faster than the closest
  competitor** (the modern variant is 1.5× faster); eval bodies execute on the same per-iteration
  fast paths as regular function bodies.
* **Object, string, base64 and regex workloads.** Jint is the fastest engine on
  `dromaeo-object-array` (**2.1×**), `dromaeo-object-string` (**3.0×**), `dromaeo-object-regexp`
  (**4.6×**) and `dromaeo-string-base64` (**1.04×**, allocating **12× less** than the next
  engine).
* **Heavy floating-point math among interpreters.** `dromaeo-3d-cube` runs in **6.03 ms — ahead of
  every other interpreter** — while allocating the least of any engine on that script (1.3 MB vs
  4.9–10.7 MB).
* **Allocations are one to two orders of magnitude lower than the field,** which means far less GC
  pressure in real applications:

  | Benchmark | Jint | Closest competitor | Highest in field |
  |--- |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | 1,323 MB (63×) | 1,614 MB (77×) |
  | `dromaeo-string-base64` | **1.6 MB** | 19 MB (12×) | 746 MB (472×) |
  | `dromaeo-object-array` | **8.9 MB** | 17 MB (2.0×) | 215 MB (24×) |
  | `stopwatch` | **12 MB** | 93 MB (7.9×) | 211 MB (18×) |

## Where Jint trails

Only the engine that compiles JavaScript to IL remains ahead, and only on tight numeric/call loops
where compiled code runs close to native speed — the structural interpreter-vs-compiler gap:

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 6.03 ms vs the IL compiler's 2.50 ms
  (2.4×). Every interpreter is now behind Jint on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  classic runs 136 ms — within ~3% of the best interpreter and ahead of Jurassic — and modern
  145 ms, statistically tied with Jurassic, while allocating 8–24× less than any competitor; the
  IL-compiling engine remains ~2.4× faster in raw time.

## What's new since the previous refresh

Measured against the 2026-07-09 refresh — competitor versions are unchanged, and their rows were
used as a thermal canary to confirm the window is comparable (times within ±3%, allocations
byte-identical):

* **`dromaeo-string-base64` allocation −30% (2.30 → 1.62 MB per op) with `-modern` −4.3% time; the
  classic row is now ranked first outright** (previously statistically tied with NiL.JS). Loop
  tests of the form `i < s.length` / `i < arr.length` read the length unboxed through the
  comparison lane instead of materializing a number per iteration
  ([#2617](https://github.com/sebastienros/jint/pull/2617)).
* **Engine-reuse embeddings got much faster — not visible in this fresh-engine-per-op table.**
  Re-evaluating a prepared script on a long-lived engine now reuses hoisted-function and
  class-member interpreter definitions ([#2613](https://github.com/sebastienros/jint/pull/2613),
  [#2615](https://github.com/sebastienros/jint/pull/2615) — class re-evaluation −23% time / −34%
  allocation), and a slot-cache fix keeps every unboxed lane alive from the second re-evaluation
  onward ([#2616](https://github.com/sebastienros/jint/pull/2616) — a re-evaluated 100k-iteration
  loop runs −72% time with allocations dropping from megabytes to kilobytes).
* **Behavior fix shipped alongside**: integer multiplication preserves `-0` for zero products of
  opposite signs ([#2620](https://github.com/sebastienros/jint/pull/2620)).
* Everything else moved within this table's documented noise bands (`dromaeo-core-eval` ±2%
  bimodality, the regexp/string families ±3–7%); ranks are unchanged except the base64 promotion
  above and `stopwatch`, where NiL.JS's own row drifted −3% and is now grouped one rank ahead
  (Jint remains within ~3% and ahead of Jurassic).

## Engine versions

* Jint main (post-4.11.0)
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.406

Last updated 2026-07-10.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean             | StdDev           | Rank | Allocated     |
|------------------ |----------------------------- |-----------------:|-----------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress                 |   2,341,833.3 ns |      9,325.45 ns |    1 |       1059 KB |
| Jint              | array-stress                 |   2,347,370.8 ns |      7,015.44 ns |    1 |    1087.66 KB |
| YantraJS          | array-stress                 |   2,858,665.2 ns |     33,861.73 ns |    2 |   17123.82 KB |
| NilJS             | array-stress                 |   4,794,724.3 ns |     18,489.67 ns |    3 |    4521.19 KB |
| Jurassic          | array-stress                 |   8,998,049.8 ns |     42,546.11 ns |    4 |   11644.87 KB |
|                   |                              |                  |                  |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,499,533.9 ns |     23,636.56 ns |    1 |    7596.01 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   6,029,287.1 ns |     57,750.86 ns |    2 |    1342.62 KB |
| NilJS             | dromaeo-3d-cube              |   6,292,545.5 ns |     28,887.06 ns |    3 |    4903.32 KB |
| Jint              | dromaeo-3d-cube              |   6,424,847.1 ns |     29,722.43 ns |    3 |    1646.13 KB |
| Jurassic          | dromaeo-3d-cube              |  55,758,988.0 ns |  1,468,674.92 ns |    4 |   10654.72 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |               NA |               NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,488,926.5 ns |     24,484.99 ns |    1 |    7514.24 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   6,029,388.2 ns |     24,412.85 ns |    2 |    1340.84 KB |
| Jint              | dromaeo-3d-cube-modern       |   6,447,100.6 ns |     31,152.64 ns |    3 |    1644.16 KB |
| NilJS             | dromaeo-3d-cube-modern       |   7,126,191.0 ns |     29,030.75 ns |    4 |    5977.95 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-core-eval            |     933,692.0 ns |      5,832.66 ns |    1 |     338.24 KB |
| Jint              | dromaeo-core-eval            |     958,266.0 ns |      6,288.71 ns |    1 |     358.64 KB |
| NilJS             | dromaeo-core-eval            |   1,239,804.8 ns |     37,081.70 ns |    2 |     1577.1 KB |
| YantraJS          | dromaeo-core-eval            |   4,831,932.3 ns |     53,782.77 ns |    3 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  16,565,076.7 ns |     81,277.40 ns |    4 |    2876.04 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-core-eval-modern     |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-core-eval-modern     |     941,396.5 ns |      2,515.63 ns |    1 |     338.05 KB |
| Jint              | dromaeo-core-eval-modern     |     961,102.3 ns |      6,680.04 ns |    1 |     357.71 KB |
| NilJS             | dromaeo-core-eval-modern     |   1,450,060.1 ns |      7,316.13 ns |    2 |    1575.94 KB |
| YantraJS          | dromaeo-core-eval-modern     |   4,835,046.7 ns |    154,542.00 ns |    3 |   35784.84 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-array         |  11,962,862.5 ns |    111,815.86 ns |    1 |    9114.32 KB |
| Jint              | dromaeo-object-array         |  12,342,950.9 ns |    189,477.26 ns |    1 |    9162.64 KB |
| YantraJS          | dromaeo-object-array         |  24,724,580.1 ns |    235,885.99 ns |    2 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  35,992,711.4 ns |    437,440.16 ns |    3 |   25808.92 KB |
| NilJS             | dromaeo-object-array         |  52,676,222.0 ns |    203,579.07 ns |    4 |   17862.17 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-object-array-modern  |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-array-modern  |  14,832,692.8 ns |    128,511.50 ns |    1 |    9116.93 KB |
| Jint              | dromaeo-object-array-modern  |  15,616,271.1 ns |    138,802.48 ns |    2 |    9164.23 KB |
| YantraJS          | dromaeo-object-array-modern  |  26,843,990.2 ns |    616,884.43 ns |    3 |  223803.56 KB |
| NilJS             | dromaeo-object-array-modern  |  52,742,506.4 ns |    276,922.55 ns |    4 |   17863.19 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        | 116,584,742.2 ns | 10,540,624.92 ns |    1 |  156743.23 KB |
| Jint              | dromaeo-object-regexp        | 143,491,240.5 ns | 12,561,587.35 ns |    2 |     156712 KB |
| NilJS             | dromaeo-object-regexp        | 534,022,966.7 ns |  9,450,443.01 ns |    3 |  764900.62 KB |
| Jurassic          | dromaeo-object-regexp        | 695,473,574.1 ns | 19,126,633.37 ns |    4 |  825543.63 KB |
| YantraJS          | dromaeo-object-regexp        | 717,553,561.5 ns |  5,202,443.95 ns |    4 |  823338.93 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-object-regexp-modern |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 103,192,028.9 ns |  4,373,843.52 ns |    1 |  157420.48 KB |
| Jint              | dromaeo-object-regexp-modern | 126,254,226.6 ns |  6,049,902.13 ns |    2 |  158307.88 KB |
| NilJS             | dromaeo-object-regexp-modern | 536,633,261.3 ns | 16,201,468.66 ns |    3 |  766178.33 KB |
| YantraJS          | dromaeo-object-regexp-modern | 716,206,706.7 ns | 11,469,286.96 ns |    4 |  829278.34 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  45,421,314.4 ns |    905,127.69 ns |    1 |   21531.34 KB |
| Jint              | dromaeo-object-string        |  46,368,719.0 ns |    211,991.33 ns |    1 |   21636.44 KB |
| NilJS             | dromaeo-object-string        | 135,993,993.9 ns |  4,273,765.38 ns |    2 | 1355193.95 KB |
| YantraJS          | dromaeo-object-string        | 158,698,441.9 ns |  5,350,764.29 ns |    3 | 1652995.34 KB |
| Jurassic          | dromaeo-object-string        | 216,397,075.0 ns |  3,123,165.51 ns |    4 | 1430455.09 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-object-string-modern |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-string-modern |  57,080,251.3 ns |  1,684,324.85 ns |    1 |   21321.19 KB |
| Jint              | dromaeo-object-string-modern |  57,307,018.5 ns |  1,039,663.37 ns |    1 |   21454.24 KB |
| NilJS             | dromaeo-object-string-modern | 128,823,198.1 ns |  1,528,964.52 ns |    2 | 1355011.26 KB |
| YantraJS          | dromaeo-object-string-modern | 158,777,550.0 ns |  3,225,082.59 ns |    3 | 1656375.28 KB |
|                   |                              |                  |                  |      |               |
| Jint              | dromaeo-string-base64        |  24,845,840.4 ns |    171,894.52 ns |    1 |    1717.72 KB |
| Jint_ParsedScript | dromaeo-string-base64        |  24,924,156.2 ns |     65,208.29 ns |    1 |    1617.85 KB |
| NilJS             | dromaeo-string-base64        |  25,898,258.9 ns |    143,090.58 ns |    2 |   19588.63 KB |
| YantraJS          | dromaeo-string-base64        |  34,045,624.6 ns |  1,093,963.65 ns |    3 |  763555.07 KB |
| Jurassic          | dromaeo-string-base64        |  46,121,022.1 ns |    351,918.33 ns |    4 |   73290.53 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | dromaeo-string-base64-modern |               NA |               NA |    ? |            NA |
| Jint              | dromaeo-string-base64-modern |  25,757,712.7 ns |     72,674.84 ns |    1 |    1718.22 KB |
| Jint_ParsedScript | dromaeo-string-base64-modern |  26,567,300.6 ns |     76,391.00 ns |    2 |    1617.95 KB |
| NilJS             | dromaeo-string-base64-modern |  33,057,098.0 ns |    727,054.70 ns |    3 |   31360.22 KB |
| YantraJS          | dromaeo-string-base64-modern |  33,480,242.5 ns |    576,223.58 ns |    3 |  764771.49 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | evaluation                   |       4,483.5 ns |         25.93 ns |    1 |      16.88 KB |
| Jint              | evaluation                   |      14,489.6 ns |         38.15 ns |    2 |       27.5 KB |
| NilJS             | evaluation                   |      25,331.1 ns |        107.13 ns |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     130,951.9 ns |      1,529.60 ns |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,102,834.2 ns |      9,827.00 ns |    5 |     418.92 KB |
|                   |                              |                  |                  |      |               |
| Jurassic          | evaluation-modern            |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       4,187.9 ns |         22.61 ns |    1 |      16.26 KB |
| Jint              | evaluation-modern            |      13,471.2 ns |         84.80 ns |    2 |      27.37 KB |
| NilJS             | evaluation-modern            |      26,468.0 ns |         91.15 ns |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     132,655.3 ns |      3,521.95 ns |    4 |      703.4 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | linq-js                      |      57,069.5 ns |        389.93 ns |    1 |     206.55 KB |
| YantraJS          | linq-js                      |     324,443.8 ns |      2,361.84 ns |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,196,892.2 ns |      5,987.80 ns |    3 |    1305.75 KB |
| NilJS             | linq-js                      |   3,979,968.5 ns |     96,338.27 ns |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  36,602,683.1 ns |    399,177.34 ns |    5 |    9103.62 KB |
|                   |                              |                  |                  |      |               |
| Jint_ParsedScript | minimal                      |         918.3 ns |          9.53 ns |    1 |       8.72 KB |
| Jint              | minimal                      |       1,979.4 ns |         16.15 ns |    2 |      10.63 KB |
| NilJS             | minimal                      |       2,927.6 ns |          6.30 ns |    3 |       4.51 KB |
| YantraJS          | minimal                      |     125,950.4 ns |        885.66 ns |    4 |     697.62 KB |
| Jurassic          | minimal                      |   2,294,017.7 ns |      9,042.62 ns |    5 |     385.19 KB |
|                   |                              |                  |                  |      |               |
| YantraJS          | stopwatch                    |  55,583,551.9 ns |    491,519.84 ns |    1 |  215655.06 KB |
| NilJS             | stopwatch                    | 131,567,076.7 ns |    501,447.47 ns |    2 |   94876.49 KB |
| Jint_ParsedScript | stopwatch                    | 136,014,776.8 ns |    775,141.19 ns |    3 |   12086.82 KB |
| Jint              | stopwatch                    | 138,337,031.7 ns |    681,250.87 ns |    3 |   12118.55 KB |
| Jurassic          | stopwatch                    | 139,104,256.7 ns |  1,026,645.00 ns |    3 |  156933.11 KB |
|                   |                              |                  |                  |      |               |
| YantraJS          | stopwatch-modern             |  58,472,560.0 ns |    560,806.40 ns |    1 |  234033.07 KB |
| Jurassic          | stopwatch-modern             | 142,959,041.7 ns |    774,636.42 ns |    2 |  288625.25 KB |
| Jint_ParsedScript | stopwatch-modern             | 144,775,610.7 ns |    395,516.10 ns |    2 |   12087.51 KB |
| Jint              | stopwatch-modern             | 149,099,908.3 ns |  1,082,118.48 ns |    3 |   12119.84 KB |
| NilJS             | stopwatch-modern             | 212,847,947.6 ns |  1,295,046.44 ns |    4 |  324502.66 KB |
