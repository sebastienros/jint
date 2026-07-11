# Engine comparison benchmarks

This project benchmarks Jint against the other managed JavaScript engines for .NET
([Jurassic](https://github.com/paiden/Jurassic), [NiL.JS](https://github.com/nilproject/NiL.JS) and
[YantraJS](https://github.com/yantrajs/yantra)) across a set of representative scripts.

## At a glance (current main, post-4.11.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on 15 of the 19 scripts** — leading the object, string, base64 and regex scripts
  by **1.07×–4.9×** over the next-fastest engine, starting tiny scripts up **3×–6×** faster
  (`minimal` executes in **under a microsecond**), and leading `dromaeo-core-eval`.
* **Fastest interpreter on every script.** On the remaining four rows only the IL-compiling engine
  is ahead: `dromaeo-3d-cube` runs **1.18×** ahead of the next interpreter and `stopwatch` /
  `stopwatch-modern` — long the closest-fought interpreter rows — now lead the nearest interpreter
  by **1.41× / 1.40×**.
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

* **Tiny-script latency.** `minimal` runs in **0.94 µs** and `evaluation` / `evaluation-modern` in
  ~4.2–4.3 µs — **3× and 6× faster than the closest competitor**.
* **Cached scripts avoid re-parsing.** `linq-js` drops from 1,180 µs (re-parsed every run) to
  **59 µs** when the prepared script is reused — ~20× — which is also **5.6× faster than the
  next-fastest engine**. Cache your `Prepared<Script>` in production.
* **eval-heavy code.** `dromaeo-core-eval` runs in **0.97 ms — 1.26× faster than the closest
  competitor** (the modern variant is 1.56× faster); eval bodies execute on the same per-iteration
  fast paths as regular function bodies.
* **Object, string, base64 and regex workloads.** Jint is the fastest engine on
  `dromaeo-object-array` (**2.1×**), `dromaeo-object-string` (**2.8×**), `dromaeo-object-regexp`
  (**4.9×**) and `dromaeo-string-base64` (**1.07×**, allocating **12× less** than the next
  engine).
* **Call- and closure-heavy loops** (`stopwatch` / `stopwatch-modern`): **96 ms and 105 ms —
  1.41× / 1.40× ahead of the nearest interpreter** (previously the closest-fought rows in the
  table), while allocating **7.9×–26×** less than any competitor.
* **Heavy floating-point math among interpreters.** `dromaeo-3d-cube` runs in **5.08 ms — 1.18×
  ahead of every other interpreter** — while allocating the least of any engine on that script
  (1.4 MB vs 4.9–10.7 MB).
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

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 5.08 ms vs the IL compiler's 2.47 ms
  (2.1×). Every interpreter is behind Jint on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  96 ms / 105 ms vs the IL compiler's 56 ms / 60 ms (1.70× / 1.75×) — down from 2.45× at the
  previous refresh — while allocating 18–19× less than it.

## What's new since the previous refresh

Measured against the 2026-07-10 refresh — competitor versions are unchanged, and their rows were
used as a thermal canary to confirm the window is comparable (times within ±3%, allocations
byte-identical):

* **`stopwatch` −29% (136.0 → 96.0 ms) and `stopwatch-modern` −27% (144.8 → 105.2 ms).** The rows
  went from rank 3 / tied-rank 2 to **clear rank 2 behind only the IL compiler**, ahead of the
  nearest interpreter by 1.41× / 1.40×. A dispatch-cost campaign
  ([#2622](https://github.com/sebastienros/jint/pull/2622)–[#2627](https://github.com/sebastienros/jint/pull/2627))
  removed the ceremony these call-heavy loops paid per operation: tiny closures with nothing to
  instantiate and no `this`/`arguments` route now run **env-less against their captured
  environment** (#2627) with the this-binding skipped (#2626), the for-loop tight-body lane admits
  the real body shape — if/else chains and variable declarations — instead of only expression
  statements (#2623), `new Date()` constructs through a call-site constructor cache that skips the
  call-stack frame for the leaf built-in (#2624), nested-scope global reads validate through a
  pinned chain memo instead of re-walking with shadow probes per read (#2625), and every call
  stopped re-resolving its definition state three times (#2622).
* **`dromaeo-3d-cube` −16% (6.03 → 5.08 ms)** — the same call-path work applied to its rotation and
  draw helpers; `dromaeo-object-regexp` −8%, `evaluation` −3%, `dromaeo-string-base64` −2.7%.
* Small residuals in the other direction, disclosed: `dromaeo-core-eval` +3.9% inside its
  documented bimodal band (rank 1 held on both variants), `linq-js` +3.1% and `minimal` +2.8%
  (the new per-handler lane/cache fields cost a few hundred bytes per fresh-engine evaluation —
  visible only on these smallest rows; long-lived engines amortize them away).
* A follow-up lane for `identifier ^ identifier` bitwise shapes
  ([#2628](https://github.com/sebastienros/jint/pull/2628), −7% on `stopwatch-modern` with
  allocation halved on out-of-cache loop shapes) was still in review when this table was measured.

## Engine versions

* Jint main (post-4.11.0)
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.406

Last updated 2026-07-11.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean             | StdDev           | Median           | Rank | Allocated     |
|------------------ |----------------------------- |-----------------:|-----------------:|-----------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress                 |   2,323,858.8 ns |     12,850.61 ns |   2,325,760.4 ns |    1 |    1059.52 KB |
| Jint              | array-stress                 |   2,382,753.8 ns |      1,908.06 ns |   2,382,593.4 ns |    1 |    1088.18 KB |
| YantraJS          | array-stress                 |   2,867,995.7 ns |     18,948.48 ns |   2,877,378.5 ns |    2 |   17123.82 KB |
| NilJS             | array-stress                 |   4,932,822.2 ns |     17,147.49 ns |   4,925,607.0 ns |    3 |    4521.19 KB |
| Jurassic          | array-stress                 |   8,996,451.1 ns |     75,228.84 ns |   8,969,945.3 ns |    4 |   11644.86 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,465,682.8 ns |     16,027.07 ns |   2,462,924.8 ns |    1 |    7596.01 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   5,081,546.1 ns |     23,702.16 ns |   5,080,966.4 ns |    2 |    1420.38 KB |
| Jint              | dromaeo-3d-cube              |   5,555,738.9 ns |     20,160.31 ns |   5,561,004.7 ns |    3 |    1724.03 KB |
| NilJS             | dromaeo-3d-cube              |   5,971,941.9 ns |     35,153.09 ns |   5,967,571.1 ns |    4 |    4903.32 KB |
| Jurassic          | dromaeo-3d-cube              |  54,855,836.7 ns |    389,598.37 ns |  54,869,860.0 ns |    5 |   10654.72 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |               NA |               NA |               NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,460,119.0 ns |     19,937.32 ns |   2,462,223.8 ns |    1 |    7514.24 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   5,077,279.0 ns |     18,689.76 ns |   5,074,689.5 ns |    2 |    1346.03 KB |
| Jint              | dromaeo-3d-cube-modern       |   5,543,627.8 ns |     34,329.26 ns |   5,545,122.7 ns |    3 |    1649.49 KB |
| NilJS             | dromaeo-3d-cube-modern       |   6,902,565.8 ns |     32,008.37 ns |   6,896,696.9 ns |    4 |    5977.95 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-core-eval            |     970,437.0 ns |      4,713.69 ns |     970,941.1 ns |    1 |     340.02 KB |
| Jint              | dromaeo-core-eval            |     986,358.8 ns |      5,382.61 ns |     984,601.8 ns |    1 |     360.47 KB |
| NilJS             | dromaeo-core-eval            |   1,224,682.7 ns |     10,223.61 ns |   1,221,560.7 ns |    2 |     1577.1 KB |
| YantraJS          | dromaeo-core-eval            |   4,835,153.9 ns |     52,280.07 ns |   4,824,348.4 ns |    3 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  16,605,947.7 ns |     99,285.02 ns |  16,569,325.0 ns |    4 |    2876.11 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-core-eval-modern     |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-core-eval-modern     |     956,761.2 ns |      3,587.46 ns |     957,130.2 ns |    1 |     339.82 KB |
| Jint              | dromaeo-core-eval-modern     |     989,560.8 ns |      7,401.19 ns |     986,171.9 ns |    2 |     359.54 KB |
| NilJS             | dromaeo-core-eval-modern     |   1,490,229.4 ns |      8,109.13 ns |   1,489,026.2 ns |    3 |    1575.94 KB |
| YantraJS          | dromaeo-core-eval-modern     |   4,796,660.0 ns |     51,024.10 ns |   4,794,025.8 ns |    4 |   35784.84 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | dromaeo-object-array         |  11,761,219.4 ns |    103,217.83 ns |  11,791,384.4 ns |    1 |    9164.25 KB |
| Jint_ParsedScript | dromaeo-object-array         |  12,020,547.0 ns |     63,173.19 ns |  12,034,606.2 ns |    1 |    9115.82 KB |
| YantraJS          | dromaeo-object-array         |  24,751,053.5 ns |    233,843.58 ns |  24,851,600.0 ns |    2 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  35,705,364.0 ns |    460,753.10 ns |  35,512,786.7 ns |    3 |   25809.38 KB |
| NilJS             | dromaeo-object-array         |  52,100,695.8 ns |    124,917.66 ns |  52,148,920.0 ns |    4 |   17862.17 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-array-modern  |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-array-modern  |  14,874,974.4 ns |    117,955.13 ns |  14,830,925.0 ns |    1 |    9117.39 KB |
| Jint              | dromaeo-object-array-modern  |  15,304,482.8 ns |    177,768.16 ns |  15,277,500.0 ns |    1 |    9164.78 KB |
| YantraJS          | dromaeo-object-array-modern  |  24,577,191.0 ns |    349,839.97 ns |  24,465,100.0 ns |    2 |  223803.56 KB |
| NilJS             | dromaeo-object-array-modern  |  51,278,022.3 ns |    225,588.97 ns |  51,289,200.0 ns |    3 |   17863.19 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        | 107,415,813.9 ns |  5,339,693.58 ns | 107,034,187.5 ns |    1 |  153227.34 KB |
| Jint              | dromaeo-object-regexp        | 129,315,007.1 ns |  5,812,638.95 ns | 130,227,833.3 ns |    2 |  154538.76 KB |
| NilJS             | dromaeo-object-regexp        | 529,208,040.0 ns |  9,106,174.39 ns | 527,290,900.0 ns |    3 |  767543.45 KB |
| Jurassic          | dromaeo-object-regexp        | 666,878,425.0 ns |  9,581,028.31 ns | 666,022,500.0 ns |    4 |  821538.87 KB |
| YantraJS          | dromaeo-object-regexp        | 712,722,826.7 ns |  5,576,613.76 ns | 713,281,100.0 ns |    5 |  826944.92 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-regexp-modern |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 102,663,740.1 ns |  3,134,526.75 ns | 102,910,841.7 ns |    1 |  156089.02 KB |
| Jint              | dromaeo-object-regexp-modern | 111,255,319.6 ns |  4,335,769.93 ns | 109,346,100.0 ns |    2 |  160648.86 KB |
| NilJS             | dromaeo-object-regexp-modern | 528,427,406.2 ns |  9,960,611.80 ns | 528,483,500.0 ns |    3 |  765861.22 KB |
| YantraJS          | dromaeo-object-regexp-modern | 710,318,620.0 ns | 11,916,899.72 ns | 711,602,100.0 ns |    4 |  830041.32 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | dromaeo-object-string        |  44,379,196.2 ns |    979,102.47 ns |  44,040,887.5 ns |    1 |   21703.88 KB |
| Jint_ParsedScript | dromaeo-object-string        |  45,760,521.2 ns |    722,513.46 ns |  45,382,527.3 ns |    1 |   21555.58 KB |
| NilJS             | dromaeo-object-string        | 128,846,141.7 ns |  1,248,763.90 ns | 128,951,600.0 ns |    2 | 1355056.96 KB |
| YantraJS          | dromaeo-object-string        | 159,400,191.2 ns |  3,564,435.83 ns | 160,220,975.0 ns |    3 | 1653056.97 KB |
| Jurassic          | dromaeo-object-string        | 210,836,863.0 ns |  5,835,382.78 ns | 212,104,400.0 ns |    4 | 1430480.84 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-string-modern |               NA |               NA |               NA |    ? |            NA |
| Jint              | dromaeo-object-string-modern |  57,811,397.0 ns |    928,928.01 ns |  58,223,000.0 ns |    1 |   21496.84 KB |
| Jint_ParsedScript | dromaeo-object-string-modern |  57,833,781.1 ns |  1,329,810.25 ns |  58,392,433.3 ns |    1 |   21333.18 KB |
| NilJS             | dromaeo-object-string-modern | 128,116,435.3 ns |  2,548,287.50 ns | 128,007,375.0 ns |    2 | 1355126.32 KB |
| YantraJS          | dromaeo-object-string-modern | 157,647,145.2 ns |  4,383,260.53 ns | 157,806,583.3 ns |    3 | 1656278.58 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | dromaeo-string-base64        |  23,840,966.7 ns |     92,140.68 ns |  23,824,434.4 ns |    1 |    1718.61 KB |
| Jint_ParsedScript | dromaeo-string-base64        |  24,249,608.9 ns |     91,298.36 ns |  24,239,225.0 ns |    1 |    1618.67 KB |
| NilJS             | dromaeo-string-base64        |  25,908,972.7 ns |    273,980.63 ns |  25,974,287.5 ns |    2 |   19588.63 KB |
| YantraJS          | dromaeo-string-base64        |  33,219,612.1 ns |    450,908.64 ns |  33,321,884.4 ns |    3 |  763555.52 KB |
| Jurassic          | dromaeo-string-base64        |  46,024,384.8 ns |    436,477.63 ns |  45,855,118.2 ns |    4 |   73290.27 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-string-base64-modern |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-string-base64-modern |  26,804,761.1 ns |     80,170.75 ns |  26,785,506.2 ns |    1 |    1618.88 KB |
| Jint              | dromaeo-string-base64-modern |  26,814,454.0 ns |     71,870.96 ns |  26,814,621.9 ns |    1 |    1719.23 KB |
| NilJS             | dromaeo-string-base64-modern |  31,265,206.2 ns |    133,736.89 ns |  31,237,325.0 ns |    2 |   31360.22 KB |
| YantraJS          | dromaeo-string-base64-modern |  33,304,687.9 ns |    449,982.72 ns |  33,133,150.0 ns |    3 |  764771.49 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | evaluation                   |       4,344.2 ns |         34.48 ns |       4,349.7 ns |    1 |      17.01 KB |
| Jint              | evaluation                   |      14,354.5 ns |         53.29 ns |      14,353.5 ns |    2 |      27.64 KB |
| NilJS             | evaluation                   |      25,644.3 ns |        112.66 ns |      25,634.1 ns |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     131,329.3 ns |      1,248.59 ns |     131,463.2 ns |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,080,231.1 ns |     12,776.98 ns |   2,080,205.9 ns |    5 |     418.81 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | evaluation-modern            |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       4,209.1 ns |         31.19 ns |       4,207.3 ns |    1 |       16.4 KB |
| Jint              | evaluation-modern            |      13,897.5 ns |         48.81 ns |      13,890.6 ns |    2 |      27.52 KB |
| NilJS             | evaluation-modern            |      26,215.1 ns |         96.69 ns |      26,212.8 ns |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     132,729.1 ns |      1,223.48 ns |     132,488.2 ns |    4 |      703.4 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | linq-js                      |      58,861.9 ns |        193.96 ns |      58,888.5 ns |    1 |     208.09 KB |
| YantraJS          | linq-js                      |     331,028.2 ns |      3,254.26 ns |     331,066.5 ns |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,179,516.8 ns |      5,925.38 ns |   1,179,393.4 ns |    3 |    1307.31 KB |
| NilJS             | linq-js                      |   3,959,257.6 ns |     10,861.30 ns |   3,955,770.3 ns |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  35,500,340.5 ns |    527,448.01 ns |  35,526,478.6 ns |    5 |    9102.27 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | minimal                      |         943.9 ns |          7.12 ns |         941.5 ns |    1 |       8.73 KB |
| Jint              | minimal                      |       1,950.6 ns |         10.24 ns |       1,950.6 ns |    2 |      10.65 KB |
| NilJS             | minimal                      |       2,718.0 ns |         12.12 ns |       2,717.3 ns |    3 |       4.51 KB |
| YantraJS          | minimal                      |     125,871.2 ns |        924.24 ns |     126,047.5 ns |    4 |     697.62 KB |
| Jurassic          | minimal                      |   2,287,829.5 ns |     12,117.87 ns |   2,286,275.8 ns |    5 |     385.19 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch                    |  56,418,502.2 ns |    392,385.82 ns |  56,277,266.7 ns |    1 |  215655.06 KB |
| Jint_ParsedScript | stopwatch                    |  96,000,731.1 ns |    680,212.94 ns |  95,835,433.3 ns |    2 |    12086.9 KB |
| Jint              | stopwatch                    | 100,083,807.1 ns |    961,531.45 ns |  99,944,700.0 ns |    3 |   12118.67 KB |
| NilJS             | stopwatch                    | 135,015,010.7 ns |    444,214.96 ns | 134,986,775.0 ns |    4 |   94876.49 KB |
| Jurassic          | stopwatch                    | 139,933,130.4 ns |    577,007.22 ns | 139,980,687.5 ns |    5 |  156932.58 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch-modern             |  60,092,910.4 ns |    564,556.36 ns |  60,141,377.8 ns |    1 |  234033.07 KB |
| Jint_ParsedScript | stopwatch-modern             | 105,244,860.0 ns |    524,530.96 ns | 105,230,440.0 ns |    2 |   12087.91 KB |
| Jint              | stopwatch-modern             | 110,516,428.0 ns |    543,040.44 ns | 110,384,820.0 ns |    3 |   12120.28 KB |
| Jurassic          | stopwatch-modern             | 147,016,044.6 ns |    764,416.97 ns | 146,863,100.0 ns |    4 |  288625.25 KB |
| NilJS             | stopwatch-modern             | 209,203,209.5 ns |    862,546.08 ns | 209,275,966.7 ns |    5 |  324502.66 KB |
