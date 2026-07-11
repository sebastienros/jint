# Engine comparison benchmarks

This project benchmarks Jint against the other managed JavaScript engines for .NET
([Jurassic](https://github.com/paiden/Jurassic), [NiL.JS](https://github.com/nilproject/NiL.JS) and
[YantraJS](https://github.com/yantrajs/yantra)) across a set of representative scripts.

## At a glance (current main, post-4.11.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on 17 of the 21 scripts** — leading the object, string, base64, regex and JSON
  scripts by **1.05×–4.9×** over the next-fastest engine, starting tiny scripts up **3×–6×** faster
  (`minimal` executes in **under a microsecond**), and leading `dromaeo-core-eval`. The new
  `json-parse` row is a Jint win outright: **2.3× faster than Jurassic and 1.4× faster than the
  YantraJS JIT** while allocating the least of any engine.
* **Fastest interpreter on every script.** On the remaining four rows only the IL-compiling engine
  is ahead: `dromaeo-3d-cube` runs **1.18×** ahead of the next interpreter and `stopwatch` /
  `stopwatch-modern` — long the closest-fought interpreter rows — lead the nearest interpreter by
  **~1.33× / ~1.6×**.
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
  `dromaeo-object-array` (**2.1×**), `dromaeo-object-string` (**3.0×**), `dromaeo-object-regexp`
  (**4.9×**) and `dromaeo-string-base64` (**1.05×**, allocating **12× less** than the next
  engine).
* **JSON parsing** (`json-parse` / `json-parse-modern`): **20.3 ms / 18.2 ms — the fastest of any
  engine**, 2.3× ahead of Jurassic and **1.4× ahead of the YantraJS JIT**, while allocating the
  least in the field (21 MB vs 44–76 MB, 2.1×–3.6× less). Parsed objects build a shared hidden
  class, so an array of like-shaped records is one allocation per record instead of a property
  dictionary each.
* **Call- and closure-heavy loops** (`stopwatch` / `stopwatch-modern`): **~100 ms and 91 ms — the
  fastest interpreter, ~1.33× / ~1.6× ahead of the nearest interpreter** (previously the
  closest-fought rows in the table), while allocating **7.9×–26×** less than any competitor.
* **Heavy floating-point math among interpreters.** `dromaeo-3d-cube` runs in **5.08 ms — 1.18×
  ahead of every other interpreter** — while allocating the least of any engine on that script
  (1.4 MB vs 4.9–10.7 MB).
* **Allocations are one to two orders of magnitude lower than the field,** which means far less GC
  pressure in real applications:

  | Benchmark | Jint | Closest competitor | Highest in field |
  |--- |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | 1,355 MB (63×) | 1,653 MB (77×) |
  | `dromaeo-string-base64` | **1.6 MB** | 20 MB (12×) | 764 MB (472×) |
  | `dromaeo-object-array` | **8.9 MB** | 18 MB (2.0×) | 215 MB (24×) |
  | `stopwatch` | **12 MB** | 95 MB (7.9×) | 216 MB (18×) |
  | `json-parse` | **21 MB** | 44 MB (2.1×) | 76 MB (3.6×) |

## Where Jint trails

Only the engine that compiles JavaScript to IL remains ahead, and only on tight numeric/call loops
where compiled code runs close to native speed — the structural interpreter-vs-compiler gap:

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 5.08 ms vs the IL compiler's 2.47 ms
  (2.1×). Every interpreter is behind Jint on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  ~100 ms / 91 ms vs the IL compiler's 57 ms / 60 ms (1.77× / 1.53×) — while allocating 18× less
  than it. These rows are bimodal on this machine (the re-parsing `Jint` row reads 95.6 ms,
  slightly ahead of the cached `Jint_ParsedScript` row); either way Jint is the fastest interpreter.

## What's new since the previous refresh

Measured against the previous refresh ([#2629](https://github.com/sebastienros/jint/pull/2629)) —
competitor versions are unchanged, and their rows were used as a thermal canary to confirm the
window is comparable (times within ±3%).

* **New `json-parse` / `json-parse-modern` rows, and Jint leads them.** `JSON.parse` now builds
  parsed objects as shared hidden classes
  ([#2634](https://github.com/sebastienros/jint/pull/2634)) — an array of like-shaped records costs
  one allocation per record instead of a property dictionary plus per-property descriptors, so the
  win shows even on a freshly constructed engine. Jint parses the array-of-records workload in
  **20.3 ms / 21 MB — 2.3× faster than Jurassic and 1.4× faster than the YantraJS JIT**, allocating
  2.1×–3.6× less than either.
* **A coverage campaign added benchmarks for common patterns the table did not exercise**
  ([#2630](https://github.com/sebastienros/jint/pull/2630)) — `reduce`/`forEach`, object
  spread/`Object.assign`, optional chaining, async/await + microtasks, `try`/`catch` + `Error`,
  Map/Set lookups, `for-in`, `parseInt`/`toFixed`, tagged templates — then closed the allocation
  hotspots they surfaced. Most of these wins are **steady-state and do not appear in this table**,
  which reconstructs a fresh engine per operation; they show in long-lived (reuse-engine) workloads:
  resolved `await` chains allocate **86% less** per await and run 39% faster
  ([#2639](https://github.com/sebastienros/jint/pull/2639)), `for-in` enumeration allocates **~99%
  less** ([#2640](https://github.com/sebastienros/jint/pull/2640)), `throw`/`catch` **73% less**
  ([#2641](https://github.com/sebastienros/jint/pull/2641)), `toFixed`-style number-method calls
  **41% less** (no wrapper object, [#2642](https://github.com/sebastienros/jint/pull/2642)), and
  object spread / rest / `Object.assign` build shaped targets
  ([#2635](https://github.com/sebastienros/jint/pull/2635)).
* **Constructors shape their instances sooner.** A provably-simple constructor body now builds a
  hidden class from its third instance instead of after the sixteenth
  ([#2636](https://github.com/sebastienros/jint/pull/2636)), helping short-lived / per-request
  engines that never reached the old promote threshold.
* The remaining table rows are within noise of the previous refresh, as expected — the object,
  string, base64, regex, eval, `stopwatch` and `3d-cube` rows were already at the levels the
  preceding dispatch campaign ([#2622](https://github.com/sebastienros/jint/pull/2622)–[#2629](https://github.com/sebastienros/jint/pull/2629))
  established, and this campaign targeted patterns that engine-reuse — not fresh construction —
  makes visible.

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
| Jint_ParsedScript | array-stress                 |   2,367,685.0 ns |     10,328.31 ns |   2,372,176.2 ns |    1 |    1059.78 KB |
| Jint              | array-stress                 |   2,422,946.1 ns |     98,947.68 ns |   2,367,493.8 ns |    1 |    1088.51 KB |
| YantraJS          | array-stress                 |   2,927,969.9 ns |     18,930.99 ns |   2,928,348.8 ns |    2 |   17123.82 KB |
| NilJS             | array-stress                 |   4,906,359.4 ns |     30,002.30 ns |   4,899,641.8 ns |    3 |    4521.19 KB |
| Jurassic          | array-stress                 |   8,969,059.3 ns |     65,019.25 ns |   8,953,382.8 ns |    4 |   11644.88 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,553,403.9 ns |     17,766.99 ns |   2,551,056.6 ns |    1 |    7596.01 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   5,094,186.2 ns |     27,322.71 ns |   5,093,593.0 ns |    2 |     1418.6 KB |
| Jint              | dromaeo-3d-cube              |   5,530,627.0 ns |     33,060.97 ns |   5,534,997.3 ns |    3 |    1722.58 KB |
| NilJS             | dromaeo-3d-cube              |   6,283,147.2 ns |     32,810.05 ns |   6,276,694.5 ns |    4 |    4903.32 KB |
| Jurassic          | dromaeo-3d-cube              |  54,991,977.9 ns |    163,238.29 ns |  54,943,145.0 ns |    5 |   10654.72 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-3d-cube-modern       |               NA |               NA |               NA |    ? |            NA |
| YantraJS          | dromaeo-3d-cube-modern       |   2,521,332.4 ns |     17,063.91 ns |   2,520,745.3 ns |    1 |    7514.24 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   5,069,655.6 ns |     23,720.98 ns |   5,067,628.9 ns |    2 |    1344.25 KB |
| Jint              | dromaeo-3d-cube-modern       |   5,478,349.9 ns |     11,192.32 ns |   5,481,219.1 ns |    3 |    1648.04 KB |
| NilJS             | dromaeo-3d-cube-modern       |   7,139,304.2 ns |     41,613.63 ns |   7,120,471.1 ns |    4 |    5977.95 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-core-eval            |     965,808.8 ns |      4,187.16 ns |     965,885.3 ns |    1 |     340.53 KB |
| Jint              | dromaeo-core-eval            |   1,001,650.2 ns |      7,337.67 ns |   1,000,871.3 ns |    2 |     360.98 KB |
| NilJS             | dromaeo-core-eval            |   1,235,228.2 ns |     10,886.31 ns |   1,232,450.8 ns |    3 |     1577.1 KB |
| YantraJS          | dromaeo-core-eval            |   4,917,499.9 ns |     74,359.00 ns |   4,920,242.2 ns |    4 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval            |  16,815,663.3 ns |    104,998.16 ns |  16,800,131.2 ns |    5 |    2876.04 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-core-eval-modern     |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-core-eval-modern     |     950,410.8 ns |      4,717.88 ns |     948,553.9 ns |    1 |     340.34 KB |
| Jint              | dromaeo-core-eval-modern     |     992,673.4 ns |      4,046.04 ns |     993,194.8 ns |    2 |     360.05 KB |
| NilJS             | dromaeo-core-eval-modern     |   1,417,470.1 ns |      8,424.90 ns |   1,418,806.2 ns |    3 |    1575.94 KB |
| YantraJS          | dromaeo-core-eval-modern     |   4,948,015.0 ns |     43,124.99 ns |   4,939,085.5 ns |    4 |   35784.84 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | dromaeo-object-array         |  11,766,127.6 ns |     79,271.25 ns |  11,794,828.1 ns |    1 |     9164.5 KB |
| Jint_ParsedScript | dromaeo-object-array         |  12,021,449.7 ns |     85,975.11 ns |  12,037,174.2 ns |    1 |    9116.09 KB |
| YantraJS          | dromaeo-object-array         |  24,923,264.1 ns |     57,647.19 ns |  24,916,221.9 ns |    2 |  220011.02 KB |
| Jurassic          | dromaeo-object-array         |  36,133,644.6 ns |  1,114,946.95 ns |  35,901,553.6 ns |    3 |   25809.29 KB |
| NilJS             | dromaeo-object-array         |  52,301,016.7 ns |    142,264.68 ns |  52,285,350.0 ns |    4 |   17862.17 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-array-modern  |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-array-modern  |  15,272,670.0 ns |     55,547.33 ns |  15,277,971.1 ns |    1 |    9117.65 KB |
| Jint              | dromaeo-object-array-modern  |  15,556,543.5 ns |     91,077.89 ns |  15,521,724.2 ns |    1 |    9165.04 KB |
| YantraJS          | dromaeo-object-array-modern  |  25,039,326.6 ns |    369,632.66 ns |  25,078,678.1 ns |    2 |  223803.33 KB |
| NilJS             | dromaeo-object-array-modern  |  53,326,535.3 ns |    238,138.22 ns |  53,280,880.0 ns |    3 |   17863.19 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        | 107,283,743.1 ns |  4,074,046.75 ns | 107,433,520.0 ns |    1 |  156672.11 KB |
| Jint              | dromaeo-object-regexp        | 119,247,442.2 ns |  6,490,391.49 ns | 116,886,250.0 ns |    2 |  155188.01 KB |
| NilJS             | dromaeo-object-regexp        | 525,172,194.4 ns | 10,541,455.24 ns | 523,316,450.0 ns |    3 |  765635.07 KB |
| Jurassic          | dromaeo-object-regexp        | 675,056,796.2 ns | 17,789,127.00 ns | 677,059,600.0 ns |    4 |  825213.33 KB |
| YantraJS          | dromaeo-object-regexp        | 712,107,600.0 ns |  6,597,436.50 ns | 715,644,100.0 ns |    5 |  823988.74 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-regexp-modern |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-object-regexp-modern | 103,746,144.5 ns |  3,157,827.93 ns | 103,207,460.0 ns |    1 |  155630.77 KB |
| Jint              | dromaeo-object-regexp-modern | 122,124,285.9 ns |  4,638,530.53 ns | 123,167,766.7 ns |    2 |  157474.95 KB |
| NilJS             | dromaeo-object-regexp-modern | 533,831,813.3 ns |  8,360,126.65 ns | 531,678,700.0 ns |    3 |  767177.59 KB |
| YantraJS          | dromaeo-object-regexp-modern | 709,789,385.7 ns | 10,539,573.08 ns | 714,021,200.0 ns |    4 |  825336.03 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  44,249,779.8 ns |    548,172.07 ns |  44,042,541.7 ns |    1 |   21542.13 KB |
| Jint              | dromaeo-object-string        |  44,282,187.2 ns |  1,108,168.55 ns |  43,971,000.0 ns |    1 |   21649.53 KB |
| NilJS             | dromaeo-object-string        | 132,881,186.4 ns |  3,142,258.10 ns | 132,345,550.0 ns |    2 | 1355040.14 KB |
| YantraJS          | dromaeo-object-string        | 156,668,485.5 ns |  3,936,479.85 ns | 156,774,966.7 ns |    3 | 1653002.42 KB |
| Jurassic          | dromaeo-object-string        | 208,366,033.3 ns |  6,489,728.48 ns | 209,774,400.0 ns |    4 | 1430487.87 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-object-string-modern |               NA |               NA |               NA |    ? |            NA |
| Jint              | dromaeo-object-string-modern |  56,202,203.0 ns |  1,540,643.38 ns |  56,062,980.0 ns |    1 |    21503.6 KB |
| Jint_ParsedScript | dromaeo-object-string-modern |  57,745,485.2 ns |    980,495.59 ns |  57,288,188.9 ns |    1 |   21305.94 KB |
| NilJS             | dromaeo-object-string-modern | 129,356,355.4 ns |  1,830,319.80 ns | 128,529,200.0 ns |    2 |  1355046.4 KB |
| YantraJS          | dromaeo-object-string-modern | 159,363,848.7 ns |  2,179,571.95 ns | 160,101,133.3 ns |    3 | 1656320.21 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-string-base64        |  23,826,306.5 ns |    104,274.56 ns |  23,840,259.4 ns |    1 |    1620.08 KB |
| NilJS             | dromaeo-string-base64        |  24,911,115.2 ns |    371,340.54 ns |  24,934,218.8 ns |    2 |   19588.61 KB |
| Jint              | dromaeo-string-base64        |  25,052,072.3 ns |     71,227.32 ns |  25,062,984.4 ns |    2 |    1720.07 KB |
| YantraJS          | dromaeo-string-base64        |  33,619,117.4 ns |    521,794.29 ns |  33,643,540.6 ns |    3 |  763555.52 KB |
| Jurassic          | dromaeo-string-base64        |  45,788,779.4 ns |    476,283.01 ns |  45,711,090.9 ns |    4 |   73293.16 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | dromaeo-string-base64-modern |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | dromaeo-string-base64-modern |  26,734,823.2 ns |     69,601.24 ns |  26,721,785.9 ns |    1 |    1620.29 KB |
| Jint              | dromaeo-string-base64-modern |  27,230,475.4 ns |    214,764.46 ns |  27,167,734.4 ns |    1 |    1720.69 KB |
| NilJS             | dromaeo-string-base64-modern |  31,791,701.9 ns |    264,300.58 ns |  31,903,859.4 ns |    2 |   31360.22 KB |
| YantraJS          | dromaeo-string-base64-modern |  34,399,149.0 ns |    423,966.49 ns |  34,326,646.7 ns |    3 |  764771.51 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | evaluation                   |       4,359.2 ns |         36.76 ns |       4,350.4 ns |    1 |      17.27 KB |
| Jint              | evaluation                   |      14,686.1 ns |         80.99 ns |      14,682.4 ns |    2 |      27.91 KB |
| NilJS             | evaluation                   |      25,921.7 ns |        106.91 ns |      25,932.9 ns |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     133,080.4 ns |        918.96 ns |     133,206.0 ns |    4 |     703.42 KB |
| Jurassic          | evaluation                   |   2,105,716.3 ns |     11,134.24 ns |   2,101,877.3 ns |    5 |     418.92 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | evaluation-modern            |               NA |               NA |               NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern            |       4,315.6 ns |         24.11 ns |       4,320.8 ns |    1 |      16.66 KB |
| Jint              | evaluation-modern            |      13,764.3 ns |         88.92 ns |      13,781.4 ns |    2 |      27.79 KB |
| NilJS             | evaluation-modern            |      25,920.1 ns |         72.90 ns |      25,904.4 ns |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     132,014.9 ns |      1,293.47 ns |     131,951.2 ns |    4 |      703.4 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | json-parse                   |  20,121,635.3 ns |     57,124.22 ns |  20,125,218.8 ns |    1 |   21403.84 KB |
| Jint_ParsedScript | json-parse                   |  20,299,693.3 ns |    209,584.30 ns |  20,170,759.4 ns |    1 |   21367.85 KB |
| YantraJS          | json-parse                   |  27,310,032.3 ns |    374,273.37 ns |  27,244,450.0 ns |    2 |   44331.62 KB |
| Jurassic          | json-parse                   |  46,436,431.5 ns |    249,934.28 ns |  46,405,700.0 ns |    3 |    76117.7 KB |
| NilJS             | json-parse                   | 132,461,216.7 ns |    756,571.10 ns | 132,478,425.0 ns |    4 |   66013.91 KB |
|                   |                              |                  |                  |                  |      |               |
| Jurassic          | json-parse-modern            |               NA |               NA |               NA |    ? |            NA |
| Jint              | json-parse-modern            |  17,698,042.5 ns |    160,410.71 ns |  17,693,768.8 ns |    1 |   22915.28 KB |
| Jint_ParsedScript | json-parse-modern            |  18,193,412.5 ns |    446,627.42 ns |  18,169,551.6 ns |    1 |   22879.74 KB |
| YantraJS          | json-parse-modern            |  25,577,906.0 ns |    314,955.52 ns |  25,514,371.9 ns |    2 |   43167.16 KB |
| NilJS             | json-parse-modern            | 128,287,223.1 ns |    551,164.43 ns | 128,316,900.0 ns |    3 |   67094.88 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | linq-js                      |      59,896.0 ns |        429.75 ns |      59,838.1 ns |    1 |     211.78 KB |
| YantraJS          | linq-js                      |     327,105.2 ns |      2,660.27 ns |     326,599.5 ns |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,186,893.7 ns |      4,136.66 ns |   1,187,021.8 ns |    3 |    1311.01 KB |
| NilJS             | linq-js                      |   3,971,705.8 ns |     12,712.32 ns |   3,969,751.6 ns |    4 |    2739.46 KB |
| Jurassic          | linq-js                      |  35,622,140.0 ns |    613,532.81 ns |  35,704,478.6 ns |    5 |    9102.27 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | minimal                      |         945.4 ns |         12.33 ns |         943.6 ns |    1 |          9 KB |
| Jint              | minimal                      |       2,038.6 ns |         25.58 ns |       2,024.4 ns |    2 |      10.91 KB |
| NilJS             | minimal                      |       2,733.4 ns |         15.14 ns |       2,731.3 ns |    3 |       4.51 KB |
| YantraJS          | minimal                      |     123,636.9 ns |        810.36 ns |     123,729.1 ns |    4 |     697.62 KB |
| Jurassic          | minimal                      |   2,286,128.6 ns |      5,795.15 ns |   2,286,150.6 ns |    5 |     385.19 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch                    |  56,751,977.0 ns |    380,809.30 ns |  56,737,366.7 ns |    1 |  215655.06 KB |
| Jint              | stopwatch                    |  95,600,360.0 ns |    647,495.32 ns |  95,324,566.7 ns |    2 |   12119.01 KB |
| Jint_ParsedScript | stopwatch                    | 100,538,990.0 ns |    444,573.73 ns | 100,561,250.0 ns |    3 |   12087.22 KB |
| NilJS             | stopwatch                    | 133,270,078.3 ns |  1,478,716.49 ns | 133,233,625.0 ns |    4 |   94876.49 KB |
| Jurassic          | stopwatch                    | 140,171,082.1 ns |    603,177.09 ns | 140,151,625.0 ns |    5 |  156933.11 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch-modern             |  59,655,363.0 ns |    616,608.36 ns |  59,440,400.0 ns |    1 |  234033.07 KB |
| Jint_ParsedScript | stopwatch-modern             |  91,267,376.7 ns |    398,030.43 ns |  91,124,600.0 ns |    2 |   12088.23 KB |
| Jint              | stopwatch-modern             |  92,301,712.2 ns |    440,814.61 ns |  92,478,233.3 ns |    2 |   12120.62 KB |
| Jurassic          | stopwatch-modern             | 149,765,953.3 ns |  1,485,281.00 ns | 149,175,425.0 ns |    3 |  288625.25 KB |
| NilJS             | stopwatch-modern             | 228,358,292.9 ns |  1,424,659.13 ns | 228,212,566.7 ns |    4 |  324502.66 KB |
