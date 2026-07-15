# Engine comparison benchmarks

This project benchmarks Jint against the other managed JavaScript engines for .NET
([NiL.JS](https://github.com/nilproject/NiL.JS), [Okojo](https://github.com/akeit0/okojo) and
[YantraJS](https://github.com/yantrajs/yantra)) across a set of representative scripts.

## How each engine executes

The engines reach the result in different ways, which shapes the numbers below:

* **Jint** — tree-walking interpreter over a prepared AST.
* **NiL.JS** — interpreter (with an optimizing pass over its syntax tree).
* **Okojo** — interpreter that compiles the script to bytecode and runs it on a virtual machine.
* **YantraJS** — compiler: it emits .NET IL, which the CLR then JIT-compiles to native code.

Only the engine that compiles to IL runs ahead on tight numeric/call loops — the structural
interpreter-vs-compiler gap. On every script in this table Jint is the fastest **interpreter**.

## At a glance (4.13.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on 17 of the 21 scripts** — leading the object, string, base64, regex and JSON
  scripts by **1.1×–4.2×** over the next-fastest engine, starting tiny scripts up **~3×–6×** faster
  (`minimal` executes in **~1 microsecond**), and leading `dromaeo-core-eval`. The
  `json-parse` row is a Jint win outright: **the fastest of any engine**, ahead of the closest
  competitor while allocating the least in the field.
* **Fastest interpreter on every script.** On the remaining four rows only the engine that compiles
  JavaScript to IL is ahead: `dromaeo-3d-cube` runs **1.30×** ahead of the next interpreter and
  `stopwatch` / `stopwatch-modern` — long the closest-fought interpreter rows — lead the nearest
  interpreter by **~1.4× / ~1.7×**.
* **Among the lowest memory use in the field.** Jint and Okojo are the two low-allocation engines:
  Jint allocates the least on almost every script (Okojo edges it on the two `object-array`
  workloads), while NiL.JS and YantraJS allocate one to two orders of magnitude more — up to
  **471×** more than Jint on `dromaeo-string-base64` — which means much lighter GC pressure in real
  applications.

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
* To re-measure a single engine (e.g. after a package bump) without disturbing the rest of the
  table, filter to its lane, for example `--filter "*EngineComparisonBenchmark.Okojo*"`.
* The benchmark config widens the parameter column (`MaxParameterColumnWidth = 40`) so the full
  script names are printed instead of BenchmarkDotNet's default truncation (e.g.
  `dromaeo-object-string-modern` rather than `droma(...)odern [28]`).

## How to read the table

* All engines are run in **global strict mode** — one competitor (YantraJS) is strict-only, and one
  (Okojo) has no engine-level strict switch, so its source carries a leading `"use strict"`
  directive. Strict mode improves performance across the board.
* `Jint` re-parses the script source on every operation; `Jint_ParsedScript` reuses a cached
  `Prepared<Script>` produced once by `Engine.PrepareScript`. The gap between the two is pure
  parsing cost — **in production you should cache the prepared script**, which is what
  `Jint_ParsedScript` represents.
* `Okojo` likewise re-parses and re-compiles on every operation; `Okojo_Prepared` reuses a parsed
  program (Okojo's realm-independent artifact) and re-compiles the bytecode against each run's own
  fresh realm — so its gap to `Okojo` is parsing cost, mirroring the two Jint lanes.
* The `*-modern` scripts are ES2015+ rewrites (`const`, arrow functions, …) of the classic ES5
  scripts. Every engine in the field parses ES2015+, so all of them report a result on the `-modern`
  rows.
* `Mean` is time per operation (lower is better); `Allocated` is managed memory per operation
  (lower is better). `Rank` groups results that are statistically tied.

## Where Jint leads

Numbers use Jint's recommended production path (`Jint_ParsedScript`, a cached prepared script) and
compare against the closest competitor on each script.

* **Tiny-script latency.** `minimal` runs in **1.0 µs** and `evaluation` / `evaluation-modern` in
  ~4.3–4.5 µs — **~3× and ~6× faster than the closest competitor**.
* **Cached scripts avoid re-parsing.** `linq-js` drops from 1,173 µs (re-parsed every run) to
  **56 µs** when the prepared script is reused — ~21× — which is also **5.7× faster than the
  next-fastest engine**. Cache your `Prepared<Script>` in production.
* **eval-heavy code.** `dromaeo-core-eval` runs in **0.92 ms — 1.32× faster than the closest
  competitor** (the modern variant is 1.58× faster); eval bodies execute on the same per-iteration
  fast paths as regular function bodies.
* **Object, string, base64 and regex workloads.** Jint is the fastest engine on
  `dromaeo-object-array` (**2.1×**), `dromaeo-object-string` (**1.14×**, over Okojo — the closest
  competitor here), `dromaeo-object-regexp` (**4.0×**) and `dromaeo-string-base64` (**1.21×**,
  allocating **12× less** than the next engine).
* **JSON parsing** (`json-parse` / `json-parse-modern`): **19.0 ms / 17.5 ms — the fastest of any
  engine**, ahead of the closest competitor while allocating the least in the field (21 MB vs
  27–66 MB). Parsed objects build a shared hidden class, so an array of like-shaped records is one
  allocation per record instead of a property dictionary each.
* **Call- and closure-heavy loops** (`stopwatch` / `stopwatch-modern`): **~94 ms and 90 ms — the
  fastest interpreter, ~1.4× / ~1.7× ahead of the nearest interpreter** (previously the
  closest-fought rows in the table).
* **Heavy floating-point math among interpreters.** `dromaeo-3d-cube` runs in **4.66 ms — 1.30×
  ahead of every other interpreter** — while allocating the least of any engine on that script
  (1.4 MB vs 2.2–7.4 MB).
* **Jint and Okojo are the two frugal engines; NiL.JS and YantraJS allocate far more,** which means
  far less GC pressure in real applications:

  | Benchmark | Jint | Okojo | NiL.JS | YantraJS |
  |--- |---: |---: |---: |---: |
  | `dromaeo-object-string` | **21 MB** | 33 MB | 1,323 MB | 1,614 MB |
  | `dromaeo-string-base64` | **1.6 MB** | 43 MB | 19 MB | 746 MB |
  | `dromaeo-object-array` | 8.9 MB | **6.8 MB** | 17 MB | 215 MB |
  | `stopwatch` | **12 MB** | 21 MB | 93 MB | 211 MB |
  | `json-parse` | **21 MB** | 27 MB | 64 MB | 43 MB |

## Where Jint trails

Only the engine that compiles JavaScript to IL remains ahead, and only on tight numeric/call loops
where compiled code runs close to native speed — the structural interpreter-vs-compiler gap:

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 4.66 ms vs the IL compiler's 2.46 ms
  (1.9×). Every interpreter is behind Jint on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  ~94 ms / 90 ms vs the IL compiler's 56 ms / 60 ms (1.68× / 1.50×) — while allocating 18×–19× less
  than it. Either way Jint is the fastest interpreter on both rows.

## What's new in 4.13.0

Measured against the 4.12.0 release; the comparison field is unchanged (same engines and versions).
The 4.13.0 improvements visible in this fresh-engine table:

* **Faster arithmetic-heavy math.** `dromaeo-3d-cube` drops from 5.11 ms to **4.66 ms** (−9%) and
  `dromaeo-string-base64` from 24.3 ms to **21.8 ms** (−10%). A hot-path arithmetic campaign added
  unboxed operand lanes for the binary operators
  ([#2664](https://github.com/sebastienros/jint/pull/2664)), an int32 fast lane for remainder
  ([#2671](https://github.com/sebastienros/jint/pull/2671)), flag-proven `Unsafe.As` casts
  ([#2673](https://github.com/sebastienros/jint/pull/2673)), and removed a native `fmod` from
  integral detection ([#2662](https://github.com/sebastienros/jint/pull/2662)).
* **Tighter reads and branches.** Member-expression identifier reads now route through the
  identifier caches ([#2660](https://github.com/sebastienros/jint/pull/2660)), strict-equality
  guards against `undefined` / `null` / `typeof` are fused
  ([#2658](https://github.com/sebastienros/jint/pull/2658)), and the identifier slot cache was
  restructured hop-0-first ([#2689](https://github.com/sebastienros/jint/pull/2689)) — trimming
  `dromaeo-core-eval` (−6%), `dromaeo-object-array` (−6%) and `json-parse` (−5%).
* **Cheaper loops and calls (mostly steady-state).** Several wins target long-lived engines and so
  show less on this fresh-engine table: `for..of` over an array no longer allocates an
  iterator-result object per element (**~260× less allocation** on the hot path,
  [#2700](https://github.com/sebastienros/jint/pull/2700)); the tight-loop fast lane now covers
  `while` / `do-while` ([#2688](https://github.com/sebastienros/jint/pull/2688)); per-call nested
  function instantiation is allocation-free ([#2684](https://github.com/sebastienros/jint/pull/2684));
  and `&&` / `||` ([#2701](https://github.com/sebastienros/jint/pull/2701)), `x == null`
  ([#2702](https://github.com/sebastienros/jint/pull/2702)) and value-producing `i++`
  ([#2703](https://github.com/sebastienros/jint/pull/2703)) execute on unboxed / slot fast paths.
* **A Proxy overhaul and correctness fixes.** Proxy trap dispatch was rebuilt to forward with
  effectively zero allocation and gained a public CLR trap API
  (`Engine.Advanced.CreateProxy` + `ProxyHandler`), alongside a batch of Proxy, RegExp and async
  correctness fixes from the pre-release review.

The `dromaeo-object-regexp` rows are dominated by .NET `Regex` and are the highest-variance in the
table; the regex execution path is unchanged from 4.12.0, so treat that row as roughly flat rather
than a movement.

## Engine versions

* Jint 4.13.0
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.406

Jint, NiL.JS and YantraJS numbers are from the 4.13.0 release run; the Okojo rows were measured
separately on the same machine and .NET runtime and merged in. Last updated 2026-07-15.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean             | StdDev         | Rank | Allocated     |
|------------------ |----------------------------- |-----------------:|---------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress                 |     2,256.761 us |      7.1240 us |    1 |    1060.39 KB |
| Jint              | array-stress                 |     2,315.740 us |     14.6480 us |    1 |    1089.12 KB |
| YantraJS          | array-stress                 |     2,792.018 us |     27.8405 us |    2 |   17123.82 KB |
| NilJS             | array-stress                 |     5,133.058 us |     18.8123 us |    3 |    4521.19 KB |
| Okojo_Prepared    | array-stress                 |     5,434.204 us |     40.9309 us |    4 |    2681.62 KB |
| Okojo             | array-stress                 |     5,494.069 us |     43.4340 us |    4 |    2697.39 KB |
|                   |                              |                  |                |      |               |
| YantraJS          | dromaeo-3d-cube              |     2,461.143 us |      8.2646 us |    1 |    7596.01 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |     4,661.673 us |      7.1767 us |    2 |    1416.99 KB |
| Jint              | dromaeo-3d-cube              |     5,145.748 us |     11.2630 us |    3 |    1721.05 KB |
| NilJS             | dromaeo-3d-cube              |     6,067.962 us |     29.0884 us |    4 |    4903.32 KB |
| Okojo             | dromaeo-3d-cube              |     7,128.906 us |     24.4769 us |    5 |    2463.17 KB |
| Okojo_Prepared    | dromaeo-3d-cube              |     7,232.647 us |     15.2943 us |    5 |    2295.53 KB |
|                   |                              |                  |                |      |               |
| YantraJS          | dromaeo-3d-cube-modern       |     2,455.419 us |     10.9833 us |    1 |    7514.24 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |     4,766.692 us |     20.9342 us |    2 |    1340.62 KB |
| Jint              | dromaeo-3d-cube-modern       |     5,081.631 us |     21.2716 us |    3 |    1644.48 KB |
| NilJS             | dromaeo-3d-cube-modern       |     7,095.752 us |     35.0040 us |    4 |    5977.95 KB |
| Okojo_Prepared    | dromaeo-3d-cube-modern       |     7,321.080 us |     18.7803 us |    5 |    2311.29 KB |
| Okojo             | dromaeo-3d-cube-modern       |     7,321.862 us |     28.8572 us |    5 |    2496.04 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-core-eval            |       916.407 us |      3.0409 us |    1 |     340.27 KB |
| Jint              | dromaeo-core-eval            |       927.436 us |      5.9208 us |    1 |     360.72 KB |
| NilJS             | dromaeo-core-eval            |     1,209.535 us |      7.7293 us |    2 |     1577.1 KB |
| YantraJS          | dromaeo-core-eval            |     4,783.342 us |     36.2068 us |    3 |   35784.73 KB |
| Okojo_Prepared    | dromaeo-core-eval            |     5,194.773 us |     88.9601 us |    4 |    4609.03 KB |
| Okojo             | dromaeo-core-eval            |     5,257.288 us |     61.6742 us |    4 |    4621.81 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-core-eval-modern     |       909.369 us |      5.7235 us |    1 |     340.21 KB |
| Jint              | dromaeo-core-eval-modern     |       926.399 us |      2.4254 us |    1 |     359.93 KB |
| NilJS             | dromaeo-core-eval-modern     |     1,435.926 us |      5.1354 us |    2 |    1575.94 KB |
| YantraJS          | dromaeo-core-eval-modern     |     4,866.540 us |     51.7995 us |    3 |   35784.84 KB |
| Okojo             | dromaeo-core-eval-modern     |     5,144.141 us |     62.8990 us |    4 |    4627.67 KB |
| Okojo_Prepared    | dromaeo-core-eval-modern     |     5,225.851 us |     78.2768 us |    4 |    4613.46 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-object-array         |    11,131.395 us |    207.2574 us |    1 |    9115.86 KB |
| Jint              | dromaeo-object-array         |    11,318.284 us |    192.7535 us |    1 |    9164.27 KB |
| YantraJS          | dromaeo-object-array         |    23,278.391 us |    171.9981 us |    2 |  220010.57 KB |
| Okojo_Prepared    | dromaeo-object-array         |    38,466.730 us |    123.7053 us |    3 |    6973.53 KB |
| Okojo             | dromaeo-object-array         |    38,739.367 us |    411.2380 us |    3 |    6999.78 KB |
| NilJS             | dromaeo-object-array         |    50,973.780 us |     92.4295 us |    4 |   17862.17 KB |
|                   |                              |                  |                |      |               |
| Jint              | dromaeo-object-array-modern  |    14,258.293 us |    154.7903 us |    1 |    9165.37 KB |
| Jint_ParsedScript | dromaeo-object-array-modern  |    14,570.784 us |    167.2733 us |    1 |    9117.79 KB |
| YantraJS          | dromaeo-object-array-modern  |    24,713.087 us |    480.4059 us |    2 |  223803.18 KB |
| Okojo_Prepared    | dromaeo-object-array-modern  |    39,672.749 us |     82.5583 us |    3 |    6984.54 KB |
| Okojo             | dromaeo-object-array-modern  |    40,965.505 us |     63.8418 us |    4 |    7013.55 KB |
| NilJS             | dromaeo-object-array-modern  |    51,511.299 us |    140.7231 us |    5 |   17863.19 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        |   131,607.741 us |  6,710.3205 us |    1 |  157427.53 KB |
| Jint              | dromaeo-object-regexp        |   137,174.876 us |  5,429.6440 us |    1 |  158919.15 KB |
| NilJS             | dromaeo-object-regexp        |   529,601.127 us |  8,878.2362 us |    2 |  766996.89 KB |
| YantraJS          | dromaeo-object-regexp        |   695,104.586 us |  7,907.4654 us |    3 |  825611.63 KB |
| Okojo             | dromaeo-object-regexp        | 1,790,480.007 us | 11,704.0554 us |    4 | 1798210.36 KB |
| Okojo_Prepared    | dromaeo-object-regexp        | 1,808,627.392 us |  6,421.8392 us |    4 | 1799342.77 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-object-regexp-modern |   125,900.462 us |  5,143.9732 us |    1 |  157138.22 KB |
| Jint              | dromaeo-object-regexp-modern |   127,541.094 us |  6,915.9485 us |    1 |  156436.69 KB |
| NilJS             | dromaeo-object-regexp-modern |   527,152.460 us |  6,296.1280 us |    2 |  767231.57 KB |
| YantraJS          | dromaeo-object-regexp-modern |   707,109.067 us |  8,559.8541 us |    3 |  831286.49 KB |
| Okojo             | dromaeo-object-regexp-modern | 1,825,988.240 us | 12,449.7110 us |    4 | 1801468.92 KB |
| Okojo_Prepared    | dromaeo-object-regexp-modern | 1,853,339.280 us | 15,718.2952 us |    4 | 1799047.36 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-object-string        |    44,236.228 us |    894.4357 us |    1 |   21517.32 KB |
| Jint              | dromaeo-object-string        |    44,765.976 us |    358.2312 us |    1 |   21690.78 KB |
| Okojo             | dromaeo-object-string        |    50,282.033 us |    809.8340 us |    2 |   33471.35 KB |
| Okojo_Prepared    | dromaeo-object-string        |    50,346.630 us |  1,032.0417 us |    2 |    33457.2 KB |
| NilJS             | dromaeo-object-string        |   140,032.067 us |  3,391.8404 us |    3 |  1355029.2 KB |
| YantraJS          | dromaeo-object-string        |   168,246.208 us |  4,540.7816 us |    4 | 1653121.45 KB |
|                   |                              |                  |                |      |               |
| Jint              | dromaeo-object-string-modern |    52,281.562 us |  1,265.5325 us |    1 |   21495.39 KB |
| Okojo_Prepared    | dromaeo-object-string-modern |    54,182.270 us |    876.1328 us |    1 |   33451.08 KB |
| Okojo             | dromaeo-object-string-modern |    54,754.242 us |  1,330.5147 us |    1 |   33538.01 KB |
| Jint_ParsedScript | dromaeo-object-string-modern |    56,936.391 us |  1,731.9417 us |    1 |   21317.66 KB |
| NilJS             | dromaeo-object-string-modern |   142,747.049 us |  3,997.6165 us |    2 | 1355011.41 KB |
| YantraJS          | dromaeo-object-string-modern |   169,179.149 us |  3,759.4630 us |    3 | 1656324.17 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-string-base64        |    21,833.256 us |     61.4609 us |    1 |    1620.51 KB |
| Jint              | dromaeo-string-base64        |    22,527.694 us |     51.7302 us |    2 |     1720.5 KB |
| NilJS             | dromaeo-string-base64        |    26,319.325 us |    168.7616 us |    3 |   19588.66 KB |
| Okojo_Prepared    | dromaeo-string-base64        |    28,397.267 us |     68.0999 us |    4 |    43734.5 KB |
| Okojo             | dromaeo-string-base64        |    28,438.659 us |     97.5886 us |    4 |   43806.57 KB |
| YantraJS          | dromaeo-string-base64        |    33,295.734 us |    367.4019 us |    5 |  763555.06 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | dromaeo-string-base64-modern |    25,894.140 us |     54.7045 us |    1 |    1620.97 KB |
| Jint              | dromaeo-string-base64-modern |    27,010.827 us |     60.4811 us |    2 |    1721.37 KB |
| Okojo_Prepared    | dromaeo-string-base64-modern |    28,505.601 us |    197.3657 us |    3 |   43745.96 KB |
| Okojo             | dromaeo-string-base64-modern |    31,410.661 us |    150.0738 us |    4 |   43824.53 KB |
| YantraJS          | dromaeo-string-base64-modern |    32,057.062 us |    484.4823 us |    4 |  764771.13 KB |
| NilJS             | dromaeo-string-base64-modern |    32,844.012 us |    344.3122 us |    4 |   31360.29 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | evaluation                   |         4.505 us |      0.0172 us |    1 |      17.55 KB |
| Jint              | evaluation                   |        14.413 us |      0.0294 us |    2 |      28.19 KB |
| NilJS             | evaluation                   |        25.128 us |      0.0647 us |    3 |      22.36 KB |
| YantraJS          | evaluation                   |       132.453 us |      1.1654 us |    4 |     703.42 KB |
| Okojo_Prepared    | evaluation                   |     1,574.309 us |     24.5322 us |    5 |    1280.28 KB |
| Okojo             | evaluation                   |     1,609.272 us |     36.2644 us |    5 |    1286.73 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | evaluation-modern            |         4.314 us |      0.0139 us |    1 |      17.16 KB |
| Jint              | evaluation-modern            |        13.762 us |      0.0431 us |    2 |      28.28 KB |
| NilJS             | evaluation-modern            |        25.719 us |      0.1213 us |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |       127.944 us |      0.8540 us |    4 |      703.4 KB |
| Okojo_Prepared    | evaluation-modern            |     1,552.680 us |     35.7149 us |    5 |    1283.36 KB |
| Okojo             | evaluation-modern            |     1,637.282 us |     28.8504 us |    5 |     1290.5 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | json-parse                   |    19,015.252 us |     42.5062 us |    1 |   21368.26 KB |
| Jint              | json-parse                   |    20,265.080 us |    227.1496 us |    2 |   21404.21 KB |
| YantraJS          | json-parse                   |    25,410.563 us |    195.4429 us |    3 |   44331.31 KB |
| Okojo_Prepared    | json-parse                   |    26,848.594 us |     52.9249 us |    4 |    27231.9 KB |
| Okojo             | json-parse                   |    26,850.924 us |     73.0589 us |    4 |   27262.65 KB |
| NilJS             | json-parse                   |   128,678.536 us |    826.0189 us |    5 |    66014.1 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | json-parse-modern            |    17,514.534 us |     56.3865 us |    1 |   21377.29 KB |
| Jint              | json-parse-modern            |    17,530.900 us |     60.9269 us |    1 |   21412.83 KB |
| YantraJS          | json-parse-modern            |    24,620.339 us |    281.6592 us |    2 |   43167.22 KB |
| Okojo             | json-parse-modern            |    26,597.647 us |    284.9263 us |    3 |   27271.68 KB |
| Okojo_Prepared    | json-parse-modern            |    26,846.415 us |     56.2260 us |    3 |   27235.28 KB |
| NilJS             | json-parse-modern            |   125,067.457 us |    472.8780 us |    4 |   67095.19 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | linq-js                      |        56.124 us |      0.1793 us |    1 |     199.59 KB |
| YantraJS          | linq-js                      |       322.556 us |      1.9429 us |    2 |    1049.75 KB |
| Jint              | linq-js                      |     1,172.644 us |      4.8812 us |    3 |    1298.82 KB |
| NilJS             | linq-js                      |     3,825.634 us |     12.0575 us |    4 |    2739.46 KB |
| Okojo_Prepared    | linq-js                      |     7,080.933 us |    327.1464 us |    5 |    4131.48 KB |
| Okojo             | linq-js                      |     8,180.639 us |     92.8429 us |    6 |    4928.45 KB |
|                   |                              |                  |                |      |               |
| Jint_ParsedScript | minimal                      |         1.023 us |      0.0079 us |    1 |       9.33 KB |
| Jint              | minimal                      |         2.156 us |      0.0666 us |    2 |      11.26 KB |
| NilJS             | minimal                      |         2.910 us |      0.0167 us |    3 |       4.51 KB |
| YantraJS          | minimal                      |       127.611 us |      1.1214 us |    4 |     697.62 KB |
| Okojo             | minimal                      |     1,432.968 us |     40.8994 us |    5 |     1249.1 KB |
| Okojo_Prepared    | minimal                      |     1,448.889 us |     38.0131 us |    5 |    1247.19 KB |
|                   |                              |                  |                |      |               |
| YantraJS          | stopwatch                    |    56,173.667 us |    249.9468 us |    1 |  215655.06 KB |
| Jint_ParsedScript | stopwatch                    |    94,493.023 us |    670.4730 us |    2 |   12086.91 KB |
| Jint              | stopwatch                    |    95,565.990 us |    777.4872 us |    2 |    12118.7 KB |
| NilJS             | stopwatch                    |   133,199.477 us |    538.3957 us |    3 |   94876.49 KB |
| Okojo             | stopwatch                    |   147,027.552 us |    962.2557 us |    4 |   21467.24 KB |
| Okojo_Prepared    | stopwatch                    |   147,292.810 us |  2,545.5226 us |    4 |   21444.76 KB |
|                   |                              |                  |                |      |               |
| YantraJS          | stopwatch-modern             |    59,827.731 us |    223.9642 us |    1 |  234033.07 KB |
| Jint_ParsedScript | stopwatch-modern             |    89,643.204 us |    378.7984 us |    2 |   12087.93 KB |
| Jint              | stopwatch-modern             |    90,244.348 us |    679.2107 us |    2 |   12120.31 KB |
| Okojo             | stopwatch-modern             |   154,027.404 us |    605.5049 us |    3 |   21468.89 KB |
| Okojo_Prepared    | stopwatch-modern             |   155,334.860 us |  1,594.1182 us |    3 |   21444.11 KB |
| NilJS             | stopwatch-modern             |   222,745.085 us |  5,723.3724 us |    4 |  324502.66 KB |
