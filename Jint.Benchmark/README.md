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

## At a glance (4.12.0)

Using Jint's recommended production path (a cached prepared script) and comparing against the
**closest competitor** on each script:

* **Fastest engine on 17 of the 21 scripts** — leading the object, string, base64, regex and JSON
  scripts by **1.05×–5.4×** over the next-fastest engine, starting tiny scripts up **3×–6×** faster
  (`minimal` executes in **under a microsecond**), and leading `dromaeo-core-eval`. The
  `json-parse` row is a Jint win outright: **the fastest of any engine**, ahead of the closest
  competitor while allocating the least in the field.
* **Fastest interpreter on every script.** On the remaining four rows only the engine that compiles
  JavaScript to IL is ahead: `dromaeo-3d-cube` runs **1.21×** ahead of the next interpreter and
  `stopwatch` / `stopwatch-modern` — long the closest-fought interpreter rows — lead the nearest
  interpreter by **~1.4× / ~1.8×**.
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

* **Tiny-script latency.** `minimal` runs in **0.95 µs** and `evaluation` / `evaluation-modern` in
  ~4.2–4.4 µs — **3× and 6× faster than the closest competitor**.
* **Cached scripts avoid re-parsing.** `linq-js` drops from 1,193 µs (re-parsed every run) to
  **58 µs** when the prepared script is reused — ~20× — which is also **5.5× faster than the
  next-fastest engine**. Cache your `Prepared<Script>` in production.
* **eval-heavy code.** `dromaeo-core-eval` runs in **0.97 ms — 1.26× faster than the closest
  competitor** (the modern variant is 1.46× faster); eval bodies execute on the same per-iteration
  fast paths as regular function bodies.
* **Object, string, base64 and regex workloads.** Jint is the fastest engine on
  `dromaeo-object-array` (**2.1×**), `dromaeo-object-string` (**1.3×**, now over Okojo — the closest
  competitor here), `dromaeo-object-regexp` (**5.4×**) and `dromaeo-string-base64` (**1.05×**,
  allocating **12× less** than the next engine).
* **JSON parsing** (`json-parse` / `json-parse-modern`): **20.1 ms / 17.8 ms — the fastest of any
  engine**, ahead of the closest competitor while allocating the least in the field (21 MB vs
  27–64 MB). Parsed objects build a shared hidden class, so an array of like-shaped records is one
  allocation per record instead of a property dictionary each.
* **Call- and closure-heavy loops** (`stopwatch` / `stopwatch-modern`): **~95 ms and 90 ms — the
  fastest interpreter, ~1.4× / ~1.8× ahead of the nearest interpreter** (previously the
  closest-fought rows in the table).
* **Heavy floating-point math among interpreters.** `dromaeo-3d-cube` runs in **5.11 ms — 1.21×
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

* **Heavy floating-point / matrix math** (`dromaeo-3d-cube`): 5.11 ms vs the IL compiler's 2.56 ms
  (2.0×). Every interpreter is behind Jint on this script.
* **Call- and closure-heavy loops with frequent `new Date()`** (`stopwatch` / `stopwatch-modern`):
  ~95 ms / 90 ms vs the IL compiler's 56 ms / 59 ms (1.69× / 1.51×) — while allocating 18×–19× less
  than it. Either way Jint is the fastest interpreter on both rows.

## What's new in 4.12.0

Measured against the 4.11.0 release. **The comparison field also changed this refresh:** Jurassic —
unmaintained and ES5-only — was dropped and replaced by [Okojo](https://github.com/akeit0/okojo), a
new low-allocation, AOT-friendly bytecode engine for .NET 10. Okojo lands mid-pack: it is the
fastest non-Jint engine on the `dromaeo-object-string` rows and a genuinely frugal allocator (it
edges Jint on `object-array`), but it does not top any row and trails the field on regex-heavy work.

The Jint-side improvements in 4.12.0:

* **New `json-parse` / `json-parse-modern` rows, and Jint leads them.** `JSON.parse` now builds
  parsed objects as shared hidden classes
  ([#2634](https://github.com/sebastienros/jint/pull/2634)) — an array of like-shaped records costs
  one allocation per record instead of a property dictionary plus per-property descriptors, so the
  win shows even on a freshly constructed engine. Jint parses the array-of-records workload in
  **20.1 ms / 21 MB — the fastest of any engine**, ahead of the closest competitor and allocating
  the least in the field.
* **Tighter call and loop dispatch.** A dispatch campaign
  ([#2622](https://github.com/sebastienros/jint/pull/2622)–[#2628](https://github.com/sebastienros/jint/pull/2628))
  trimmed per-call ceremony, added unboxed operand lanes for comparisons and bitwise operators, and
  cached zero-argument constructors, which is most visible on the call- and closure-heavy
  `stopwatch` / `stopwatch-modern` rows and keeps Jint the fastest interpreter there.
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

## Engine versions

* Jint 4.12.0
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.406

Jint, NiL.JS and YantraJS numbers are from the 4.12.0 release run; the Okojo rows were measured
separately on the same machine and .NET runtime and merged in. Last updated 2026-07-12.

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName                     | Mean             | StdDev           | Median           | Rank | Allocated     |
|------------------ |----------------------------- |-----------------:|-----------------:|-----------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress                 |   2,377,261.6 ns |     22,074.11 ns |   2,375,025.2 ns |    1 |     1059.9 KB |
| Jint              | array-stress                 |   2,432,410.2 ns |     11,736.49 ns |   2,437,587.5 ns |    1 |    1088.63 KB |
| YantraJS          | array-stress                 |   2,962,473.3 ns |     24,026.83 ns |   2,964,086.7 ns |    2 |   17123.82 KB |
| NilJS             | array-stress                 |   4,935,863.9 ns |     26,287.35 ns |   4,928,714.1 ns |    3 |    4521.19 KB |
| Okojo_Prepared    | array-stress                 |   6,201,000.0 ns |      42,300.0 ns |   6,205,000.0 ns |    4 |    2682.88 KB |
| Okojo             | array-stress                 |   6,325,000.0 ns |     111,200.0 ns |   6,304,000.0 ns |    4 |    2693.12 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | dromaeo-3d-cube              |   2,561,766.0 ns |     27,070.22 ns |   2,553,780.9 ns |    1 |    7596.01 KB |
| Jint_ParsedScript | dromaeo-3d-cube              |   5,107,575.2 ns |     27,901.62 ns |   5,104,432.4 ns |    2 |    1418.61 KB |
| Jint              | dromaeo-3d-cube              |   5,604,767.0 ns |     28,874.45 ns |   5,596,511.7 ns |    3 |    1722.59 KB |
| NilJS             | dromaeo-3d-cube              |   6,160,956.3 ns |     26,192.56 ns |   6,150,566.4 ns |    4 |    4903.32 KB |
| Okojo_Prepared    | dromaeo-3d-cube              |   7,472,000.0 ns |      79,100.0 ns |   7,467,000.0 ns |    5 |    2293.76 KB |
| Okojo             | dromaeo-3d-cube              |   8,271,000.0 ns |      71,900.0 ns |   8,257,000.0 ns |    6 |    2467.84 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | dromaeo-3d-cube-modern       |   2,547,685.1 ns |     17,772.11 ns |   2,556,654.3 ns |    1 |    7514.24 KB |
| Jint_ParsedScript | dromaeo-3d-cube-modern       |   5,147,509.7 ns |     16,493.69 ns |   5,145,196.9 ns |    2 |    1344.26 KB |
| Jint              | dromaeo-3d-cube-modern       |   5,542,344.7 ns |     28,215.38 ns |   5,547,254.7 ns |    3 |    1648.05 KB |
| NilJS             | dromaeo-3d-cube-modern       |   7,085,778.3 ns |     31,631.74 ns |   7,085,266.8 ns |    4 |    5977.95 KB |
| Okojo_Prepared    | dromaeo-3d-cube-modern       |   7,590,000.0 ns |      59,600.0 ns |   7,587,000.0 ns |    5 |    2314.24 KB |
| Okojo             | dromaeo-3d-cube-modern       |   9,386,000.0 ns |      51,400.0 ns |   9,368,000.0 ns |    6 |    2498.56 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-core-eval            |     970,521.1 ns |      4,197.76 ns |     970,979.5 ns |    1 |     340.65 KB |
| Jint              | dromaeo-core-eval            |     978,891.5 ns |      7,385.88 ns |     977,063.5 ns |    1 |      361.1 KB |
| NilJS             | dromaeo-core-eval            |   1,222,849.4 ns |      8,278.98 ns |   1,220,361.3 ns |    2 |     1577.1 KB |
| YantraJS          | dromaeo-core-eval            |   5,085,778.9 ns |     42,106.17 ns |   5,081,568.4 ns |    3 |   35784.73 KB |
| Okojo             | dromaeo-core-eval            |   5,522,000.0 ns |     142,600.0 ns |   5,539,000.0 ns |    4 |    4618.24 KB |
| Okojo_Prepared    | dromaeo-core-eval            |   5,573,000.0 ns |     189,000.0 ns |   5,569,000.0 ns |    4 |       4608 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-core-eval-modern     |     955,586.4 ns |      6,260.46 ns |     954,861.7 ns |    1 |     340.45 KB |
| Jint              | dromaeo-core-eval-modern     |     996,669.7 ns |      4,605.74 ns |     996,358.3 ns |    2 |     360.17 KB |
| NilJS             | dromaeo-core-eval-modern     |   1,397,301.0 ns |      5,253.50 ns |   1,397,674.2 ns |    3 |    1575.94 KB |
| YantraJS          | dromaeo-core-eval-modern     |   4,782,230.0 ns |     52,091.30 ns |   4,778,414.1 ns |    4 |   35784.84 KB |
| Okojo             | dromaeo-core-eval-modern     |   5,444,000.0 ns |     197,400.0 ns |   5,465,000.0 ns |    5 |    4628.48 KB |
| Okojo_Prepared    | dromaeo-core-eval-modern     |   5,614,000.0 ns |     133,300.0 ns |   5,571,000.0 ns |    5 |    4618.24 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | dromaeo-object-array         |  11,734,070.8 ns |     24,769.73 ns |  11,732,057.8 ns |    1 |    9164.62 KB |
| Jint_ParsedScript | dromaeo-object-array         |  11,848,101.5 ns |    148,691.21 ns |  11,769,998.4 ns |    1 |    9116.21 KB |
| YantraJS          | dromaeo-object-array         |  24,885,871.9 ns |    122,500.69 ns |  24,850,543.8 ns |    2 |  220011.02 KB |
| Okojo_Prepared    | dromaeo-object-array         |  41,367,000.0 ns |     122,200.0 ns |  41,380,000.0 ns |    3 |    6973.44 KB |
| Okojo             | dromaeo-object-array         |  42,056,000.0 ns |     326,500.0 ns |  42,007,000.0 ns |    3 |    7004.16 KB |
| NilJS             | dromaeo-object-array         |  51,968,596.4 ns |    278,679.88 ns |  51,948,890.0 ns |    4 |   17862.17 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-array-modern  |  14,636,747.8 ns |    107,715.09 ns |  14,675,878.1 ns |    1 |    9117.77 KB |
| Jint              | dromaeo-object-array-modern  |  15,032,690.4 ns |    121,041.45 ns |  15,042,318.8 ns |    1 |    9165.16 KB |
| YantraJS          | dromaeo-object-array-modern  |  24,666,946.7 ns |    553,079.34 ns |  24,919,767.2 ns |    2 |  223803.33 KB |
| Okojo_Prepared    | dromaeo-object-array-modern  |  43,777,000.0 ns |     137,200.0 ns |  43,737,000.0 ns |    3 |    6983.68 KB |
| Okojo             | dromaeo-object-array-modern  |  43,847,000.0 ns |     163,000.0 ns |  43,809,000.0 ns |    3 |     7014.4 KB |
| NilJS             | dromaeo-object-array-modern  |  51,456,789.3 ns |    221,398.90 ns |  51,420,225.0 ns |    4 |   17863.19 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-regexp        |  99,397,424.1 ns |  5,187,878.46 ns |  99,105,900.0 ns |    1 |  159484.19 KB |
| Jint              | dromaeo-object-regexp        | 121,920,600.0 ns |  4,517,573.70 ns | 122,353,133.3 ns |    2 |  154780.48 KB |
| NilJS             | dromaeo-object-regexp        | 533,917,041.2 ns | 10,914,303.67 ns | 532,245,400.0 ns |    3 |   767448.2 KB |
| YantraJS          | dromaeo-object-regexp        | 719,093,020.0 ns |  4,256,641.94 ns | 719,911,400.0 ns |    4 |   825423.4 KB |
| Okojo             | dromaeo-object-regexp        | 1,888,188,000.0 ns |  42,715,900.0 ns | 1,880,354,000.0 ns |    5 |  1799219.2 KB |
| Okojo_Prepared    | dromaeo-object-regexp        | 2,018,209,000.0 ns |  35,162,200.0 ns | 2,009,512,000.0 ns |    6 | 1796997.12 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-regexp-modern |  95,252,668.4 ns |  3,322,069.84 ns |  94,795,100.0 ns |    1 |   153586.8 KB |
| Jint              | dromaeo-object-regexp-modern | 115,301,910.2 ns |  5,080,328.67 ns | 113,304,400.0 ns |    2 |  154275.57 KB |
| NilJS             | dromaeo-object-regexp-modern | 530,882,869.2 ns |  5,331,822.71 ns | 531,923,900.0 ns |    3 |  765558.98 KB |
| YantraJS          | dromaeo-object-regexp-modern | 714,553,964.3 ns |  7,419,506.80 ns | 717,243,050.0 ns |    4 |  828657.69 KB |
| Okojo             | dromaeo-object-regexp-modern | 2,077,881,000.0 ns |  12,349,500.0 ns | 2,077,540,000.0 ns |    5 |  1800550.4 KB |
| Okojo_Prepared    | dromaeo-object-regexp-modern | 2,094,326,000.0 ns |  73,343,500.0 ns | 2,075,714,000.0 ns |    5 | 1797570.56 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-object-string        |  44,850,624.5 ns |    632,186.43 ns |  45,144,718.2 ns |    1 |   21585.85 KB |
| Jint              | dromaeo-object-string        |  46,304,197.0 ns |    821,304.67 ns |  46,624,054.5 ns |    1 |   21680.99 KB |
| Okojo             | dromaeo-object-string        |  58,592,000.0 ns |   2,011,400.0 ns |  58,198,000.0 ns |    2 |   33495.04 KB |
| Okojo_Prepared    | dromaeo-object-string        |  59,310,000.0 ns |   1,722,100.0 ns |  59,236,000.0 ns |    2 |   33413.12 KB |
| NilJS             | dromaeo-object-string        | 149,830,460.0 ns |  3,878,608.64 ns | 148,667,000.0 ns |    3 | 1354908.97 KB |
| YantraJS          | dromaeo-object-string        | 179,837,241.7 ns |  4,487,951.27 ns | 180,884,100.0 ns |    4 | 1653065.13 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | dromaeo-object-string-modern |  55,397,017.6 ns |  1,478,006.03 ns |  55,736,600.0 ns |    1 |   21477.14 KB |
| Jint_ParsedScript | dromaeo-object-string-modern |  58,143,847.9 ns |  1,658,899.03 ns |  58,846,455.0 ns |    1 |   21297.14 KB |
| Okojo             | dromaeo-object-string-modern |  60,484,000.0 ns |   2,077,100.0 ns |  60,525,000.0 ns |    2 |   33515.52 KB |
| Okojo_Prepared    | dromaeo-object-string-modern |  60,555,000.0 ns |   1,842,200.0 ns |  61,181,000.0 ns |    2 |   33423.36 KB |
| NilJS             | dromaeo-object-string-modern | 152,155,665.0 ns |  2,843,333.52 ns | 152,427,600.0 ns |    3 | 1354903.94 KB |
| YantraJS          | dromaeo-object-string-modern | 184,368,004.9 ns |  5,041,705.48 ns | 183,499,666.7 ns |    4 | 1656426.61 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-string-base64        |  24,336,383.9 ns |    118,294.09 ns |  24,312,368.8 ns |    1 |    1620.09 KB |
| Jint              | dromaeo-string-base64        |  24,402,786.8 ns |    112,777.82 ns |  24,392,987.5 ns |    1 |    1720.08 KB |
| NilJS             | dromaeo-string-base64        |  25,443,252.1 ns |     75,762.89 ns |  25,430,228.1 ns |    2 |   19588.61 KB |
| Okojo             | dromaeo-string-base64        |  32,311,000.0 ns |     577,100.0 ns |  32,212,000.0 ns |    3 |   43806.72 KB |
| Okojo_Prepared    | dromaeo-string-base64        |  33,655,000.0 ns |     497,300.0 ns |  33,580,000.0 ns |    3 |   43735.04 KB |
| YantraJS          | dromaeo-string-base64        |  35,414,163.2 ns |    249,392.04 ns |  35,371,529.2 ns |    4 |  763555.06 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | dromaeo-string-base64-modern |  27,042,369.7 ns |    231,135.59 ns |  26,984,065.6 ns |    1 |     1620.3 KB |
| Jint              | dromaeo-string-base64-modern |  28,171,282.9 ns |     96,713.59 ns |  28,163,131.2 ns |    2 |     1720.7 KB |
| NilJS             | dromaeo-string-base64-modern |  33,052,923.6 ns |    111,553.42 ns |  33,027,487.5 ns |    3 |   31360.39 KB |
| Okojo_Prepared    | dromaeo-string-base64-modern |  33,268,000.0 ns |     889,200.0 ns |  33,321,000.0 ns |    4 |   43745.28 KB |
| Okojo             | dromaeo-string-base64-modern |  33,807,000.0 ns |     391,100.0 ns |  33,738,000.0 ns |    4 |    43827.2 KB |
| YantraJS          | dromaeo-string-base64-modern |  35,126,301.8 ns |    605,473.20 ns |  34,857,245.8 ns |    5 |  764771.12 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | evaluation                   |       4,363.4 ns |         20.86 ns |       4,365.6 ns |    1 |      17.28 KB |
| Jint              | evaluation                   |      14,221.4 ns |         51.98 ns |      14,235.8 ns |    2 |      27.92 KB |
| NilJS             | evaluation                   |      26,042.9 ns |        153.24 ns |      25,968.8 ns |    3 |      22.36 KB |
| YantraJS          | evaluation                   |     130,565.4 ns |        930.31 ns |     130,860.2 ns |    4 |     703.42 KB |
| Okojo_Prepared    | evaluation                   |   1,552,000.0 ns |     234,800.0 ns |   1,634,000.0 ns |    5 |       1280 KB |
| Okojo             | evaluation                   |   1,581,000.0 ns |     132,700.0 ns |   1,627,000.0 ns |    5 |    1290.24 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | evaluation-modern            |       4,209.2 ns |         18.28 ns |       4,207.3 ns |    1 |      16.78 KB |
| Jint              | evaluation-modern            |      13,841.3 ns |         70.60 ns |      13,823.1 ns |    2 |      27.91 KB |
| NilJS             | evaluation-modern            |      26,088.4 ns |        174.06 ns |      26,069.8 ns |    3 |      22.35 KB |
| YantraJS          | evaluation-modern            |     131,669.8 ns |      1,030.95 ns |     131,872.5 ns |    4 |      703.4 KB |
| Okojo_Prepared    | evaluation-modern            |   1,338,000.0 ns |     276,600.0 ns |   1,467,000.0 ns |    5 |       1280 KB |
| Okojo             | evaluation-modern            |   1,592,000.0 ns |     146,700.0 ns |   1,620,000.0 ns |    6 |    1290.24 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | json-parse                   |  20,067,604.2 ns |     29,619.62 ns |  20,073,329.7 ns |    1 |   21368.15 KB |
| Jint              | json-parse                   |  20,300,920.4 ns |    286,195.57 ns |  20,166,353.1 ns |    1 |   21404.04 KB |
| YantraJS          | json-parse                   |  26,910,567.5 ns |    421,865.53 ns |  27,174,953.8 ns |    2 |   44331.53 KB |
| Okojo_Prepared    | json-parse                   |  28,751,000.0 ns |   1,114,600.0 ns |  28,712,000.0 ns |    3 |   27228.16 KB |
| Okojo             | json-parse                   |  29,262,000.0 ns |     667,500.0 ns |  29,137,000.0 ns |    3 |   27258.88 KB |
| NilJS             | json-parse                   | 128,909,611.5 ns |  1,379,237.09 ns | 128,379,600.0 ns |    4 |   66013.91 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint              | json-parse-modern            |  17,830,508.8 ns |    349,857.75 ns |  17,628,834.4 ns |    1 |    22915.4 KB |
| Jint_ParsedScript | json-parse-modern            |  17,879,467.2 ns |    362,263.33 ns |  17,687,507.8 ns |    1 |   22879.95 KB |
| YantraJS          | json-parse-modern            |  24,786,600.6 ns |    174,790.72 ns |  24,807,180.8 ns |    2 |   43167.16 KB |
| Okojo             | json-parse-modern            |  26,222,000.0 ns |     504,200.0 ns |  26,181,000.0 ns |    3 |   27269.12 KB |
| Okojo_Prepared    | json-parse-modern            |  27,180,000.0 ns |     696,300.0 ns |  27,001,000.0 ns |    3 |    27238.4 KB |
| NilJS             | json-parse-modern            | 125,787,153.6 ns |  1,621,613.30 ns | 124,912,075.0 ns |    4 |   67094.88 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | linq-js                      |      58,235.1 ns |        392.77 ns |      58,093.7 ns |    1 |      211.9 KB |
| YantraJS          | linq-js                      |     322,222.6 ns |      3,192.77 ns |     322,240.8 ns |    2 |    1049.75 KB |
| Jint              | linq-js                      |   1,192,750.2 ns |      9,414.22 ns |   1,189,361.2 ns |    3 |    1311.13 KB |
| NilJS             | linq-js                      |   3,886,025.4 ns |     41,647.19 ns |   3,881,030.5 ns |    4 |    2739.46 KB |
| Okojo_Prepared    | linq-js                      |   7,802,000.0 ns |     292,200.0 ns |   7,879,000.0 ns |    5 |    4126.72 KB |
| Okojo             | linq-js                      |   8,249,000.0 ns |     199,400.0 ns |   8,198,000.0 ns |    6 |    4925.44 KB |
|                   |                              |                  |                  |                  |      |               |
| Jint_ParsedScript | minimal                      |         950.8 ns |          7.82 ns |         952.1 ns |    1 |       9.12 KB |
| Jint              | minimal                      |       1,964.0 ns |         23.06 ns |       1,957.1 ns |    2 |      11.03 KB |
| NilJS             | minimal                      |       2,790.0 ns |         22.29 ns |       2,787.9 ns |    3 |       4.51 KB |
| YantraJS          | minimal                      |     124,250.3 ns |      1,590.06 ns |     124,430.0 ns |    4 |     697.62 KB |
| Okojo_Prepared    | minimal                      |   1,380,000.0 ns |     209,500.0 ns |   1,482,000.0 ns |    5 |    1249.28 KB |
| Okojo             | minimal                      |   1,434,000.0 ns |     157,900.0 ns |   1,486,000.0 ns |    5 |    1249.28 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch                    |  56,170,394.0 ns |    237,927.52 ns |  56,210,455.6 ns |    1 |  215655.06 KB |
| Jint_ParsedScript | stopwatch                    |  95,080,321.4 ns |    852,902.45 ns |  95,083,116.7 ns |    2 |   12087.23 KB |
| Jint              | stopwatch                    |  96,506,734.6 ns |    743,831.93 ns |  96,532,500.0 ns |    2 |   12119.02 KB |
| NilJS             | stopwatch                    | 133,192,350.0 ns |  1,554,538.11 ns | 132,605,487.5 ns |    3 |   94876.49 KB |
| Okojo_Prepared    | stopwatch                    | 151,527,000.0 ns |   3,479,400.0 ns | 149,922,000.0 ns |    4 |   21442.56 KB |
| Okojo             | stopwatch                    | 156,070,000.0 ns |   3,932,200.0 ns | 154,480,000.0 ns |    4 |   21463.04 KB |
|                   |                              |                  |                  |                  |      |               |
| YantraJS          | stopwatch-modern             |  59,463,966.7 ns |    956,652.74 ns |  59,240,988.9 ns |    1 |  234033.07 KB |
| Jint_ParsedScript | stopwatch-modern             |  89,865,310.3 ns |    380,947.14 ns |  89,836,400.0 ns |    2 |   12088.24 KB |
| Jint              | stopwatch-modern             |  91,184,776.4 ns |    379,078.85 ns |  91,091,916.7 ns |    2 |   12120.63 KB |
| Okojo_Prepared    | stopwatch-modern             | 161,884,000.0 ns |   2,257,800.0 ns | 161,535,000.0 ns |    3 |   21442.56 KB |
| Okojo             | stopwatch-modern             | 163,926,000.0 ns |   1,077,400.0 ns | 163,945,000.0 ns |    3 |   21473.28 KB |
| NilJS             | stopwatch-modern             | 209,825,102.6 ns |  2,549,034.66 ns | 208,754,100.0 ns |    4 |  324502.66 KB |
```
