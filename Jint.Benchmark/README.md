# Engine comparison benchmarks

This project benchmarks Jint against the other JavaScript engines available to .NET applications:
the managed engines ([NiL.JS](https://github.com/nilproject/NiL.JS),
[Okojo](https://github.com/akeit0/okojo) and [YantraJS](https://github.com/yantrajs/yantra)) and
[ClearScript](https://github.com/ClearFoundry/ClearScript) — Microsoft-originated, now
ClearFoundry-maintained bindings to Google's native V8 engine (the JIT inside Chrome and Node.js) —
across a set of representative scripts.

## How each engine executes

The engines reach the result in different ways, which shapes the numbers below:

* **Jint** — tree-walking interpreter over a prepared AST.
* **NiL.JS** — interpreter (with an optimizing pass over its syntax tree).
* **Okojo** — interpreter that compiles the script to bytecode and runs it on a virtual machine.
* **YantraJS** — compiler: it emits .NET IL, which the CLR then JIT-compiles to native code.
* **ClearScript (V8)** — native V8 behind a managed ↔ native interop bridge: a multi-tier
  optimizing JIT running outside the CLR, with every host interaction crossing the native boundary.

The structural consequences shape the whole table. Only the two compilers (YantraJS and V8) run
ahead on long, tight numeric/call loops, where compiled code approaches native speed. The
interpreters own the other end of the trade: engine start-up and small scripts, where even a
context created from a pre-warmed V8 isolate costs ~375 µs for work Jint completes in about a
microsecond. And pure-JS compute is only half of an embedding story — the other half is the
script ↔ host boundary, measured separately in the
[interop section](#script--host-interop) below, where the price of V8's native boundary inverts
the picture.

## The scripts

One variant per workload, 12 scripts in total. Where the suite previously carried both a classic
ES5 script and an ES2015+ `-modern` rewrite of it, only the modern variant is kept — that is how
JavaScript is written today, and the duplicated rows ranked the engines nearly identically while
doubling the wall-clock time of a full run. (The classic scripts remain in `Scripts/` and are still
used by the Jint-only benchmarks.)

## Running the benchmarks

Run from **this** directory (the scripts are loaded relative to the working directory):

```
dotnet run -c Release -- --allCategories EngineComparison
```

Notes:

* The `--` separator is required so the arguments are forwarded to BenchmarkDotNet instead of being
  consumed by `dotnet run`.
* `--allCategories EngineComparison` runs both the script suite and the interop suite; use
  `--allCategories EngineComparisonInterop` for the interop suite alone, or
  `--filter "*EngineComparisonBenchmark*"` for the script suite alone.
* Results are written to `BenchmarkDotNet.Artifacts/results/` — the
  `*-report-github.md` file is the table reproduced below.
* To re-measure a single engine (e.g. after a package bump) without disturbing the rest of the
  table, filter to its lane, for example `--filter "*EngineComparisonBenchmark.Okojo*"`.
* The benchmark config widens the parameter column (`MaxParameterColumnWidth = 40`) so the full
  script names are printed instead of BenchmarkDotNet's default truncation (e.g.
  `dromaeo-object-string-modern` rather than `droma(...)odern [28]`).

## How to read the table

* All engines are run in **global strict mode** — YantraJS is strict-only, and Okojo and
  ClearScript have no engine-level strict switch, so their source carries a leading `"use strict"`
  directive. Strict mode improves performance across the board.
* Every operation uses a **fresh engine** — the embedding pattern where executions must not leak
  state into each other.
* Three engines have a **cached-artifact lane** next to the re-parse lane, and the pairs mean the
  same thing: `Jint_ParsedScript` reuses a `Prepared<Script>` produced once by
  `Engine.PrepareScript`; `Okojo_Prepared` reuses a parsed program (Okojo's realm-independent
  artifact) and re-compiles the bytecode against each run's fresh realm; `ClearScript_Compiled`
  reuses a `V8Script` compiled once by a shared `V8Runtime` and runs it in a fresh script engine (a
  fresh V8 context) created from that runtime. The gap to the re-parse lane is parsing/compilation
  cost — **in production you should cache the prepared artifact**, which is what these lanes
  represent.
* The plain `ClearScript` lane creates a full V8 isolate + context per operation — the honest cost
  when each request gets a fully isolated engine, and the reason its short-script rows start around
  a millisecond. `ClearScript_Compiled` shares one isolate (and its warmed JIT state) across
  operations while still using a fresh context per operation — ClearScript's recommended production
  path.
* **`Allocated` counts managed memory only.** ClearScript's working memory lives on V8's native
  heap, which the managed diagnoser cannot see — the few dozen KB in its rows are interop-bridge
  overhead, not a memory-use figure comparable with the managed engines. Memory claims in this
  document therefore compare the managed engines with each other.
* V8 runs background threads (tiered JIT compilation, garbage collection). `Mean` is the wall-clock
  time the executing thread observes; total CPU consumed is higher than for the single-threaded
  managed engines, which matters on saturated servers.
* `Mean` is time per operation (lower is better); `Rank` groups results that are statistically
  tied. Every lane in both tables below comes from a single benchmark session on one machine, so
  ranks are BenchmarkDotNet's own — no rows are merged from separate runs. Cross-session
  comparisons of absolute numbers (including the V8 lanes) are unreliable; compare within a table.
* The `dromaeo-object-regexp-modern` row is the highest-variance in the table (for Jint it is
  dominated by .NET `Regex`); treat small gaps there — including ClearScript's fresh-engine lane
  appearing ahead of its compiled lane — as run-to-run noise. Jint's 4.15.0 lead on that row is
  not one of those: ~19 ms clear of the nearest V8 lane, several times its own standard deviation.

## At a glance

Using each engine's recommended production path (for Jint a cached prepared script,
`Jint_ParsedScript`; for ClearScript a precompiled `V8Script` on a shared runtime,
`ClearScript_Compiled`):

* **Jint owns everything start-up-shaped and eval-shaped; native V8 owns long compute.** Jint is
  the fastest engine outright on `minimal` (**1.1 µs vs V8's 374 µs, ~350×**), `evaluation-modern`
  (**~80×**), `linq-js` (**~6.6×**), `dromaeo-core-eval-modern` (eval defeats V8's compile cache)
  and — new at 4.15.0 — `dromaeo-object-regexp-modern` (**1.26× ahead of V8's fresh-context lane
  and 1.48× ahead of its compiled lane**), and is rank-tied with V8's compiled lane for the lead
  on `array-stress`. That is five rows outright plus a shared sixth, out of twelve. No other
  managed engine takes a single row's top rank away from Jint or V8.
* **V8's wins are the tight-loop compute rows**: `dromaeo-string-base64-modern` (10.2×),
  `dromaeo-object-string-modern` (6.0×), `stopwatch-modern` (5.9×), `dromaeo-3d-cube-modern`
  (3.4×), `json-parse-modern` (2.1×) and a narrow lead on `dromaeo-object-array-modern` (1.13×) —
  the structural interpreter-vs-JIT gap, narrower on both string rows than in the previous
  session (11.7× and 7.6×).
* **Jint is the fastest managed engine on 10 of 12 scripts** (the IL-compiling YantraJS leads
  `dromaeo-3d-cube-modern` and `stopwatch-modern`) and **the fastest interpreter on all 12**.
* **Among the managed engines, Jint and Okojo allocate the least** — NiL.JS and YantraJS allocate
  one to two orders of magnitude more (up to ~470× more than Jint on
  `dromaeo-string-base64-modern`), which means far heavier GC pressure in real applications.
  (ClearScript's rows cannot be compared here — its memory lives on the V8 native heap, which the
  managed diagnoser does not see.)
* **Pure-JS compute is only half the story** — the [interop suite](#script--host-interop) below
  measures the script ↔ host boundary, where the picture inverts and Jint beats ClearScript on
  every row by 3.5×–11.6×.

### What changed since the 4.13.0 table

A profile-guided campaign (PRs [#2716](https://github.com/sebastienros/jint/pull/2716)–
[#2722](https://github.com/sebastienros/jint/pull/2722)) attacked the rows where V8 led, measured
with same-base A/B gates: string-receiver method calls are cached per call site
(`dromaeo-object-string-modern` −20%, `-base64-modern` −17%), JSON.parse interns property keys
(bestbuy-style payloads −22% and −32% allocation), chained `slice()` no longer materializes
intermediate views (−99.3% on that pattern), interop argument binding lost its per-call
reflection checks and boxing, ObjectWrapper members gained a per-callsite inline cache
(`interop-property-access` −25%), and CLR arrays gained identity caching plus an opt-in
`ArrayConversionMode.LiveView` mode (array traversal 2.5× faster, −75% allocation, without flags).

A second campaign round (PRs [#2725](https://github.com/sebastienros/jint/pull/2725),
[#2726](https://github.com/sebastienros/jint/pull/2726)) added bulk JSON string scanning plus an
exactly-rounded simple-number fast path (JsonBenchmark Parse −15–17%) and memoized closure-read
chain validation (`dromaeo-string-base64-modern` −6% at launchCount-5). A third candidate —
arity-specialized direct dispatch for built-in calls, eliminating the argument array — was built,
proven to engage on 4.3M of 4.3M eligible calls, and measured **flat**: Jint's builtin-call path
(pooled argument arrays + the cached-callee lane) is already at its cost floor, so the remaining
gap on the string rows is interpreter dispatch itself, not call ceremony. Measured-and-dropped is
recorded here so it isn't re-attempted.

The 4.14.0 release round shipped the two interop default flips — CLR arrays cross as live views
([#2728](https://github.com/sebastienros/jint/pull/2728)) and recently wrapped objects reuse their
wrappers ([#2734](https://github.com/sebastienros/jint/pull/2734)) — backed by cached array-like
wrapper factories with lazy length ([#2730](https://github.com/sebastienros/jint/pull/2730)),
boxing-free primitive element conversion ([#2731](https://github.com/sebastienros/jint/pull/2731),
extended to the `Array.prototype` iteration lane in
[#2735](https://github.com/sebastienros/jint/pull/2735)), a compiled-invoker fast lane for
single-candidate interop calls ([#2733](https://github.com/sebastienros/jint/pull/2733)) and
JSON.parse value interning plus span-based number parsing
([#2732](https://github.com/sebastienros/jint/pull/2732)). The visible movement in this table:
`interop-collection-traversal` **15,597 → 1,433 µs (10.9×, −99% allocation)** without any script
changes, every other interop row −8–16% with −17–76% allocation, `json-parse-modern` −6% time and
−23% allocation, and `stopwatch-modern` −6%. A pre-release review wave
([#2735](https://github.com/sebastienros/jint/pull/2735)–[#2740](https://github.com/sebastienros/jint/pull/2740))
hardened the new defaults (declared-type contracts, JS-array `in`/enumeration/out-of-range
semantics, constraint-gate balance) with no measurable cost; one further candidate — carrying the
interned JSON key hash into member adds — measured flat and was dropped.

Since 4.14.0, a further interop round cached the compiled method invoker process-wide instead of
per-engine ([#2743](https://github.com/sebastienros/jint/pull/2743)), compiled CLR property and
field access instead of reflecting on every hit ([#2744](https://github.com/sebastienros/jint/pull/2744),
which also more than halved `interop-property-access` allocation, 798 → 329 KB), trimmed the method
fast lane's per-call overhead ([#2745](https://github.com/sebastienros/jint/pull/2745)), and
memoized the converted value of a stable reference-typed property so a host array read in a loop
skips re-conversion ([#2756](https://github.com/sebastienros/jint/pull/2756)). The visible movement
in the interop table: `interop-collection-traversal` and `interop-string-passing` now rank first
(collection −8% from the memo alone, ahead of NiL.JS), and method calls closed to within ~4% of
NiL.JS while still allocating 7× less.

### What changed for 4.15.0

The 4.15.0 round shipped a string and regexp allocation wave — vectorized special-casing and
search pre-scans ([#2775](https://github.com/sebastienros/jint/pull/2775),
[#2776](https://github.com/sebastienros/jint/pull/2776)), `toWellFormed` no longer rebuilding
already-well-formed strings ([#2778](https://github.com/sebastienros/jint/pull/2778)), dense fast
paths for the callback-free array methods ([#2779](https://github.com/sebastienros/jint/pull/2779),
[#2780](https://github.com/sebastienros/jint/pull/2780)) and the string-concat plus RegExp fixes
aimed at this suite's regexp row ([#2781](https://github.com/sebastienros/jint/pull/2781), with
follow-up concat correctness in [#2811](https://github.com/sebastienros/jint/pull/2811) and
[#2812](https://github.com/sebastienros/jint/pull/2812)) — a fast-call lane for built-in calls
with declared per-argument leaf guards
([#2783](https://github.com/sebastienros/jint/pull/2783),
[#2828](https://github.com/sebastienros/jint/pull/2828),
[#2844](https://github.com/sebastienros/jint/pull/2844)), span- and UTF-8-based JSON parsing
([#2832](https://github.com/sebastienros/jint/pull/2832)), and an interop round that compiles CLR
member access for keyed writes, indexers and statics
([#2839](https://github.com/sebastienros/jint/pull/2839)), shares resolved members process-wide
across engines ([#2798](https://github.com/sebastienros/jint/pull/2798)) and stops re-reflecting
on every host delegate call ([#2799](https://github.com/sebastienros/jint/pull/2799)).

The visible movement in these tables: `dromaeo-object-regexp-modern`
**123,563 → 71,625 µs (−42%) with −47% allocation** (157,850 → 82,980 KB), which takes the row
from rank 3 to rank 1 — ahead of both V8 lanes, and the first time Jint leads it;
`dromaeo-object-string-modern` **−21%**; `dromaeo-string-base64-modern` **−13%** on the prepared
lane (the re-parse lane's −30% is partly the previous session's high reading — the two Jint lanes
now agree to within 1%); `array-stress` −5%, back to a rank-1 tie with V8's compiled lane;
`json-parse-modern` −1% time with −4% allocation. On the interop side `interop-property-access` is
−11% and `interop-method-calls` −4%, while `interop-collection-traversal` moved the other way
(+5%) and NiL.JS retook that row by ~4%.

## Script ↔ host interop

Embedding a JavaScript engine is rarely about pure computation — scripts exist to drive the host
application, and in interop-heavy systems the script ↔ host boundary dominates. This is also where
the engines differ structurally: the managed engines dispatch host calls in-process, while every
ClearScript host interaction crosses the managed ↔ native V8 boundary and marshals its arguments
across it.

`EngineComparisonInteropBenchmark` (run with `--allCategories EngineComparisonInterop`) drives
four byte-identical scripts against each engine: a host method-call loop, a host property
read/write loop, strings crossing the boundary, and traversal of a host `int[]`. Details that
keep the comparison fair:

* Host members are lowercase (`host.add`, `host.value`, …) because YantraJS camel-cases CLR
  member names while the other engines surface them verbatim — already-lowercase names are the
  fixed point of both conventions, so every engine runs the same source.
* Each script validates its final aggregate and throws on a mismatch, so an engine that silently
  mis-marshals (undefined, NaN) fails loudly instead of posting a fantasy time.
* `ClearScript` binds the host object with plain `AddHostObject` (reflection-based, like the
  managed engines); `ClearScript_FastProxy` uses ClearScript 7.5's FastProxy API — explicit
  member registration with zero-allocation marshaling for fundamental types — its recommended
  path for hot host objects.
* Okojo is absent: 0.1.2-preview.1 provides no public way to enable CLR access.
* As above, `Allocated` is meaningful for the managed engines but only counts bridge overhead
  for the two ClearScript lanes.

What the numbers show:

* **The managed engines win every interop row.** Crossing the native boundary costs plain
  ClearScript **8.9×–11.6×** against Jint on every row — the mirror image of the pure-compute
  table, and the half of the story that matters most in chatty embedding scenarios.
* **FastProxy narrows the gap but does not close it**: still **3.5×–7.0×** behind Jint, and it
  trades away convenience — every member is hand-registered instead of reflected. Its
  zero-allocation claim is real, though: 13 KB managed allocation on the method-call row where
  reflective ClearScript burns 12.8 MB.
* **Jint leads string passing outright and allocates the least on all four**: it is rank 1 on
  string passing and rank 2 on the other three — collection traversal (NiL.JS ahead by ~4%),
  method calls (NiL.JS by ~12%) and property access (YantraJS by ~6%) — while allocating the
  least of the managed engines on every row (roughly 4×–12× less than the row's nearest
  competitor).
* **Collection traversal — last of all engines at 4.13.0 — stays transformed at
  15,597 → 1,279 µs (12.2×, −99% allocation) with no script changes**: the 4.14.0
  `ArrayConversionMode.LiveView` default exposes the host array as a live view instead of
  re-copying it on every read, and a per-descriptor value memo
  ([#2756](https://github.com/sebastienros/jint/pull/2756)) skips re-converting the array on every
  read. Jint held rank 1 on this row in the previous session; here NiL.JS is ~4% ahead again
  (Jint +5%, NiL.JS −4% between the two sessions) while allocating 12× more. Either way the old
  hoist-into-a-local workaround is no longer needed.

| Method                | FileName                     | Mean        | StdDev   | Rank | Allocated   |
|---------------------- |----------------------------- |------------:|---------:|-----:|------------:|
| NilJS                 | interop-collection-traversal |  1,227.3 μs |  5.40 μs |    1 |  4088.14 KB |
| Jint                  | interop-collection-traversal |  1,279.3 μs |  2.99 μs |    2 |    330.8 KB |
| YantraJS              | interop-collection-traversal |  3,556.8 μs | 16.01 μs |    3 |  5395.96 KB |
| ClearScript_FastProxy | interop-collection-traversal |  8,983.1 μs | 33.71 μs |    4 |   1185.7 KB |
| ClearScript           | interop-collection-traversal | 11,371.2 μs | 28.14 μs |    5 |  5016.25 KB |
|                       |                              |             |          |      |             |
| NilJS                 | interop-method-calls         |  1,209.8 μs |  4.56 μs |    1 |  2437.94 KB |
| Jint                  | interop-method-calls         |  1,381.1 μs |  2.22 μs |    2 |   328.18 KB |
| YantraJS              | interop-method-calls         |  1,963.6 μs | 31.65 μs |    3 |  3355.13 KB |
| ClearScript_FastProxy | interop-method-calls         |  5,329.7 μs | 21.06 μs |    4 |    12.96 KB |
| ClearScript           | interop-method-calls         | 16,050.5 μs | 92.40 μs |    5 | 12752.53 KB |
|                       |                              |             |          |      |             |
| YantraJS              | interop-property-access      |  1,370.6 μs |  4.55 μs |    1 |  1870.68 KB |
| Jint                  | interop-property-access      |  1,451.0 μs |  7.34 μs |    2 |   329.13 KB |
| NilJS                 | interop-property-access      |  1,620.8 μs |  9.06 μs |    3 |  4391.52 KB |
| ClearScript_FastProxy | interop-property-access      |  5,127.2 μs | 16.00 μs |    4 |    12.59 KB |
| ClearScript           | interop-property-access      | 12,974.6 μs | 46.27 μs |    5 |  10561.8 KB |
|                       |                              |             |          |      |             |
| Jint                  | interop-string-passing       |    511.5 μs |  1.81 μs |    1 |   303.87 KB |
| YantraJS              | interop-string-passing       |    594.3 μs |  4.34 μs |    2 |  1714.38 KB |
| NilJS                 | interop-string-passing       |    758.8 μs |  2.45 μs |    3 |  1179.64 KB |
| ClearScript_FastProxy | interop-string-passing       |  2,936.4 μs | 15.33 μs |    4 |   247.15 KB |
| ClearScript           | interop-string-passing       |  5,276.8 μs | 27.10 μs |    5 |  2470.18 KB |

## Engine versions

* Jint 4.15.0
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.419
* Microsoft.ClearScript.V8 7.5.1

Both tables come from one benchmark session on the same machine and .NET runtime (all lanes,
including ClearScript, measured together): 116 benchmarks in 49 minutes, default job, otherwise
idle machine. ClearScript's V8 lanes land within ~1.5% of the previous (2026-07-24) session on the
identical package, so those rows are comparable across the two — but the ~30% swing those same
lanes showed between the 4.14.0 and 2026-07-24 sessions is the standing reminder that the
native-JIT rows carry more session-to-session variance than the managed engines, and that absolute
numbers should be compared within a table rather than across sessions. YantraJS moved 1.2.406 →
1.2.419 between the two sessions, which accounts for its own row-level movement. Last updated
2026-07-28 (4.15.0 release candidate `7e14ac5`).

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

```
| Method               | FileName                     | Mean             | StdDev         | Rank | Allocated     |
|--------------------- |----------------------------- |-----------------:|---------------:|-----:|--------------:|
| ClearScript_Compiled | array-stress                 |     2,055.304 μs |     11.9523 μs |    1 |       8.05 KB |
| Jint_ParsedScript    | array-stress                 |     2,114.002 μs |     10.7837 μs |    1 |    1062.84 KB |
| Jint                 | array-stress                 |     2,220.384 μs |     11.0118 μs |    2 |    1091.56 KB |
| YantraJS             | array-stress                 |     2,815.329 μs |     20.4272 μs |    3 |   17093.93 KB |
| ClearScript          | array-stress                 |     3,357.104 μs |     18.9154 μs |    4 |      16.08 KB |
| NilJS                | array-stress                 |     4,765.925 μs |     22.4772 μs |    5 |    4521.19 KB |
| Okojo                | array-stress                 |     5,478.518 μs |     11.0619 μs |    6 |    2697.58 KB |
| Okojo_Prepared       | array-stress                 |     5,577.971 μs |     32.7893 μs |    6 |    2681.43 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-3d-cube-modern       |     1,360.676 μs |      2.0043 μs |    1 |       9.25 KB |
| YantraJS             | dromaeo-3d-cube-modern       |     2,287.215 μs |     10.6203 μs |    2 |    7436.04 KB |
| ClearScript          | dromaeo-3d-cube-modern       |     3,176.822 μs |     23.8893 μs |    3 |      14.53 KB |
| Jint_ParsedScript    | dromaeo-3d-cube-modern       |     4,661.519 μs |     11.2339 μs |    4 |    1372.82 KB |
| Jint                 | dromaeo-3d-cube-modern       |     5,080.790 μs |     20.8574 μs |    5 |    1676.99 KB |
| Okojo_Prepared       | dromaeo-3d-cube-modern       |     6,860.292 μs |     14.8855 μs |    6 |    2308.77 KB |
| NilJS                | dromaeo-3d-cube-modern       |     7,001.324 μs |     13.8705 μs |    6 |    5977.95 KB |
| Okojo                | dromaeo-3d-cube-modern       |     7,427.878 μs |     65.7890 μs |    7 |    2496.09 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | dromaeo-core-eval-modern     |       881.897 μs |      2.1203 μs |    1 |     346.27 KB |
| Jint                 | dromaeo-core-eval-modern     |       900.845 μs |      1.3235 μs |    1 |     365.98 KB |
| ClearScript_Compiled | dromaeo-core-eval-modern     |       942.798 μs |      4.0005 μs |    2 |       8.03 KB |
| NilJS                | dromaeo-core-eval-modern     |     1,460.634 μs |      4.8927 μs |    3 |    1575.94 KB |
| ClearScript          | dromaeo-core-eval-modern     |     2,076.543 μs |     22.2236 μs |    4 |       12.9 KB |
| YantraJS             | dromaeo-core-eval-modern     |     4,869.179 μs |     46.5279 μs |    5 |   35784.84 KB |
| Okojo                | dromaeo-core-eval-modern     |     6,282.858 μs |    271.9095 μs |    6 |    4627.71 KB |
| Okojo_Prepared       | dromaeo-core-eval-modern     |     6,478.593 μs |    182.6656 μs |    6 |    4613.29 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-array-modern  |    13,650.042 μs |     65.8246 μs |    1 |      16.35 KB |
| Jint                 | dromaeo-object-array-modern  |    15,192.955 μs |    228.6974 μs |    2 |    9168.54 KB |
| Jint_ParsedScript    | dromaeo-object-array-modern  |    15,361.577 μs |    132.5288 μs |    2 |    9121.15 KB |
| ClearScript          | dromaeo-object-array-modern  |    15,496.640 μs |     86.4433 μs |    2 |     115.75 KB |
| YantraJS             | dromaeo-object-array-modern  |    24,804.335 μs |    284.2477 μs |    3 |  223683.36 KB |
| Okojo_Prepared       | dromaeo-object-array-modern  |    39,670.486 μs |    572.2854 μs |    4 |    6984.32 KB |
| Okojo                | dromaeo-object-array-modern  |    40,853.377 μs |    140.9573 μs |    4 |    7014.64 KB |
| NilJS                | dromaeo-object-array-modern  |    51,111.217 μs |    145.6237 μs |    5 |   17863.19 KB |
|                      |                              |                  |                |      |               |
| Jint                 | dromaeo-object-regexp-modern |    71,047.052 μs |  3,881.3493 μs |    1 |   82866.49 KB |
| Jint_ParsedScript    | dromaeo-object-regexp-modern |    71,624.961 μs |  3,445.7446 μs |    1 |   82980.43 KB |
| ClearScript          | dromaeo-object-regexp-modern |    90,050.486 μs |    301.7880 μs |    2 |      31.44 KB |
| ClearScript_Compiled | dromaeo-object-regexp-modern |   105,906.449 μs |  1,250.2385 μs |    3 |      15.81 KB |
| NilJS                | dromaeo-object-regexp-modern |   528,188.420 μs |  7,304.9796 μs |    4 |  768215.57 KB |
| YantraJS             | dromaeo-object-regexp-modern |   706,866.653 μs | 10,102.6059 μs |    5 |  822916.12 KB |
| Okojo_Prepared       | dromaeo-object-regexp-modern | 1,845,300.707 μs | 11,829.4954 μs |    6 | 1800069.52 KB |
| Okojo                | dromaeo-object-regexp-modern | 1,877,926.827 μs | 22,797.5619 μs |    6 | 1799050.88 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-string-modern |     5,839.399 μs |     22.6792 μs |    1 |      15.68 KB |
| ClearScript          | dromaeo-object-string-modern |     8,660.822 μs |     74.7272 μs |    2 |      25.15 KB |
| Jint_ParsedScript    | dromaeo-object-string-modern |    35,171.381 μs |  1,847.2748 μs |    3 |    21359.9 KB |
| Jint                 | dromaeo-object-string-modern |    36,140.180 μs |  1,365.2787 μs |    3 |   21511.14 KB |
| Okojo_Prepared       | dromaeo-object-string-modern |    55,965.561 μs |    803.2405 μs |    4 |   33408.13 KB |
| Okojo                | dromaeo-object-string-modern |    56,227.462 μs |  1,004.4060 μs |    4 |   33528.06 KB |
| NilJS                | dromaeo-object-string-modern |   136,008.260 μs |  2,764.5542 μs |    5 | 1354996.28 KB |
| YantraJS             | dromaeo-object-string-modern |   161,211.518 μs |  5,258.6889 μs |    6 |  1648912.1 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-string-base64-modern |     1,687.091 μs |      5.3769 μs |    1 |       8.85 KB |
| ClearScript          | dromaeo-string-base64-modern |     3,454.163 μs |     13.1761 μs |    2 |      15.39 KB |
| Jint                 | dromaeo-string-base64-modern |    17,055.918 μs |     29.8870 μs |    3 |    1727.65 KB |
| Jint_ParsedScript    | dromaeo-string-base64-modern |    17,199.535 μs |     50.8998 μs |    3 |    1627.25 KB |
| Okojo                | dromaeo-string-base64-modern |    30,987.824 μs |    421.9603 μs |    4 |   43824.04 KB |
| Okojo_Prepared       | dromaeo-string-base64-modern |    31,333.952 μs |     65.9674 μs |    4 |   43746.74 KB |
| NilJS                | dromaeo-string-base64-modern |    31,864.328 μs |    349.0626 μs |    4 |   31360.34 KB |
| YantraJS             | dromaeo-string-base64-modern |    32,343.567 μs |    342.3979 μs |    4 |  763918.33 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | evaluation-modern            |         4.684 μs |      0.0210 μs |    1 |      18.38 KB |
| Jint                 | evaluation-modern            |        14.192 μs |      0.1000 μs |    2 |      29.51 KB |
| NilJS                | evaluation-modern            |        26.400 μs |      0.1532 μs |    3 |      22.35 KB |
| YantraJS             | evaluation-modern            |       127.633 μs |      0.7249 μs |    4 |      703.4 KB |
| ClearScript_Compiled | evaluation-modern            |       376.941 μs |      1.9437 μs |    5 |        6.1 KB |
| ClearScript          | evaluation-modern            |     1,163.904 μs |      8.0066 μs |    6 |      10.97 KB |
| Okojo                | evaluation-modern            |     1,478.049 μs |     67.7751 μs |    7 |    1290.75 KB |
| Okojo_Prepared       | evaluation-modern            |     1,517.025 μs |     67.0758 μs |    7 |    1283.57 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | json-parse-modern            |     7,339.696 μs |     29.5113 μs |    1 |       9.85 KB |
| ClearScript          | json-parse-modern            |     9,291.399 μs |     50.0888 μs |    2 |      17.11 KB |
| Jint_ParsedScript    | json-parse-modern            |    15,730.175 μs |    265.4263 μs |    3 |   11396.96 KB |
| Jint                 | json-parse-modern            |    16,166.667 μs |    268.3455 μs |    3 |   11433.95 KB |
| YantraJS             | json-parse-modern            |    24,127.186 μs |    244.3200 μs |    4 |    42849.6 KB |
| Okojo_Prepared       | json-parse-modern            |    25,739.595 μs |    832.9876 μs |    5 |   27235.39 KB |
| Okojo                | json-parse-modern            |    26,075.714 μs |    525.1372 μs |    5 |   27272.22 KB |
| NilJS                | json-parse-modern            |   126,009.669 μs |    528.5084 μs |    6 |   67095.19 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | linq-js                      |        69.885 μs |      0.2259 μs |    1 |     214.16 KB |
| YantraJS             | linq-js                      |       322.654 μs |      2.4363 μs |    2 |    1049.75 KB |
| ClearScript_Compiled | linq-js                      |       459.778 μs |      2.2336 μs |    3 |       6.21 KB |
| Jint                 | linq-js                      |     1,184.084 μs |      4.7134 μs |    4 |    1313.39 KB |
| ClearScript          | linq-js                      |     2,051.331 μs |     11.6180 μs |    5 |      10.96 KB |
| NilJS                | linq-js                      |     3,885.547 μs |     13.4352 μs |    6 |    2739.46 KB |
| Okojo_Prepared       | linq-js                      |     6,230.265 μs |    116.2009 μs |    7 |       4132 KB |
| Okojo                | linq-js                      |     8,808.947 μs |    222.2924 μs |    8 |    4928.69 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | minimal                      |         1.066 μs |      0.0058 μs |    1 |       9.77 KB |
| Jint                 | minimal                      |         2.125 μs |      0.0111 μs |    2 |       11.7 KB |
| NilJS                | minimal                      |         2.733 μs |      0.0104 μs |    3 |       4.51 KB |
| YantraJS             | minimal                      |       126.250 μs |      0.7113 μs |    4 |     697.62 KB |
| ClearScript_Compiled | minimal                      |       374.155 μs |      2.3749 μs |    5 |       6.09 KB |
| ClearScript          | minimal                      |     1,106.276 μs |      9.2632 μs |    6 |      10.97 KB |
| Okojo_Prepared       | minimal                      |     1,153.538 μs |     84.3375 μs |    6 |    1247.38 KB |
| Okojo                | minimal                      |     1,179.491 μs |     77.9065 μs |    6 |    1249.22 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | stopwatch-modern             |    14,098.863 μs |     27.7595 μs |    1 |       9.12 KB |
| ClearScript          | stopwatch-modern             |    16,807.184 μs |     82.0086 μs |    2 |      22.58 KB |
| YantraJS             | stopwatch-modern             |    57,314.845 μs |    182.4605 μs |    3 |  233993.23 KB |
| Jint                 | stopwatch-modern             |    82,749.616 μs |    516.7966 μs |    4 |    12122.7 KB |
| Jint_ParsedScript    | stopwatch-modern             |    83,439.559 μs |    320.8908 μs |    4 |   12090.31 KB |
| Okojo                | stopwatch-modern             |   149,655.655 μs |  2,782.2848 μs |    5 |   21468.64 KB |
| Okojo_Prepared       | stopwatch-modern             |   154,810.518 μs |  4,495.9343 μs |    5 |   21445.78 KB |
| NilJS                | stopwatch-modern             |   227,604.409 μs |  1,715.6427 μs |    6 |  324502.66 KB |
