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
context created from a pre-warmed V8 isolate costs ~380 µs for work Jint completes in about a
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
  appearing ahead of its compiled lane — as run-to-run noise.

## At a glance

Using each engine's recommended production path (for Jint a cached prepared script,
`Jint_ParsedScript`; for ClearScript a precompiled `V8Script` on a shared runtime,
`ClearScript_Compiled`):

* **Jint owns everything start-up-shaped and eval-shaped; native V8 owns long compute.** Jint is
  the fastest engine outright on `minimal` (**1.0 µs vs V8's 375 µs, ~355×**), `evaluation-modern`
  (**~80×**), `linq-js` (**~6.5×**) and `dromaeo-core-eval-modern` (eval defeats V8's compile
  cache), sits a close second to V8's compiled lane on `array-stress` and
  `dromaeo-object-array-modern` (both within ~1.1×), and is rank-tied on
  `dromaeo-object-regexp-modern`. No other managed engine takes a single row's top rank away from
  Jint or V8.
* **V8's wins are the tight-loop compute rows**: `dromaeo-string-base64-modern` (~12×),
  `dromaeo-object-string-modern` (~7.6×), `stopwatch-modern` (~6×), `dromaeo-3d-cube-modern`
  (~3.4×) and `json-parse-modern` (~2.1×) — the structural interpreter-vs-JIT gap. V8's compiled
  lane also edges `array-stress` and `dromaeo-object-array-modern` this session, both rows where
  4.14.0 showed Jint tied for the lead; those two carry more V8 session-variance than the managed
  engines (see the note under the second table).
* **Jint is the fastest managed engine on 10 of 12 scripts** (the IL-compiling YantraJS leads
  `dromaeo-3d-cube-modern` and `stopwatch-modern`) and **the fastest interpreter on all 12**.
* **Among the managed engines, Jint and Okojo allocate the least** — NiL.JS and YantraJS allocate
  one to two orders of magnitude more (up to ~470× more than Jint on
  `dromaeo-string-base64-modern`), which means far heavier GC pressure in real applications.
  (ClearScript's rows cannot be compared here — its memory lives on the V8 native heap, which the
  managed diagnoser does not see.)
* **Pure-JS compute is only half the story** — the [interop suite](#script--host-interop) below
  measures the script ↔ host boundary, where the picture inverts and Jint beats ClearScript on
  every row by 3.1×–11×.

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
  ClearScript **7.8×–11×** against Jint on every row — the mirror image of the pure-compute
  table, and the half of the story that matters most in chatty embedding scenarios.
* **FastProxy narrows the gap but does not close it**: still **3.1×–7.1×** behind Jint, and it
  trades away convenience — every member is hand-registered instead of reflected. Its
  zero-allocation claim is real, though: 13 KB managed allocation on the method-call row where
  reflective ClearScript burns 12.8 MB.
* **Jint leads two rows outright and allocates the least on all four**: it is rank 1 on collection
  traversal and string passing, second on method calls (NiL.JS ahead by ~4%) and property access
  (YantraJS ahead) — and allocates the least of the managed engines on every row (roughly 4×–12×
  less than the row's nearest competitor).
* **Collection traversal — the one row Jint lost at 4.13.0 — is now first, having moved
  15,597 → 1,213 µs (12.9×, −99% allocation) with no script changes**: the 4.14.0
  `ArrayConversionMode.LiveView` default exposes the host array as a live view instead of
  re-copying it on every read, and a per-descriptor value memo
  ([#2756](https://github.com/sebastienros/jint/pull/2756)) skips re-converting the array on every
  read. Jint now edges NiL.JS while allocating 12× less, and the old hoist-into-a-local workaround
  is no longer needed.

| Method                | FileName                     | Mean        | StdDev   | Rank | Allocated   |
|---------------------- |----------------------------- |------------:|---------:|-----:|------------:|
| Jint                  | interop-collection-traversal |  1,212.5 μs |  2.48 μs |    1 |    331.7 KB |
| NilJS                 | interop-collection-traversal |  1,280.2 μs |  6.65 μs |    2 |  4088.14 KB |
| YantraJS              | interop-collection-traversal |  3,672.0 μs | 12.98 μs |    3 |  5399.83 KB |
| ClearScript_FastProxy | interop-collection-traversal |  8,619.8 μs | 21.18 μs |    4 |  1185.69 KB |
| ClearScript           | interop-collection-traversal | 11,832.5 μs | 40.53 μs |    5 |  5016.24 KB |
|                       |                              |             |          |      |             |
| NilJS                 | interop-method-calls         |  1,374.0 μs |  8.85 μs |    1 |  2437.94 KB |
| Jint                  | interop-method-calls         |  1,431.1 μs |  3.33 μs |    2 |    329.3 KB |
| YantraJS              | interop-method-calls         |  2,063.4 μs | 14.22 μs |    3 |  3355.13 KB |
| ClearScript_FastProxy | interop-method-calls         |  5,373.7 μs | 14.48 μs |    4 |    12.96 KB |
| ClearScript           | interop-method-calls         | 15,924.3 μs | 54.53 μs |    5 | 12752.53 KB |
|                       |                              |             |          |      |             |
| YantraJS              | interop-property-access      |  1,393.0 μs |  6.49 μs |    1 |  1870.68 KB |
| Jint                  | interop-property-access      |  1,638.3 μs |  4.13 μs |    2 |   329.39 KB |
| NilJS                 | interop-property-access      |  1,708.2 μs |  5.75 μs |    3 |  4391.52 KB |
| ClearScript_FastProxy | interop-property-access      |  5,142.7 μs | 14.18 μs |    4 |    12.63 KB |
| ClearScript           | interop-property-access      | 12,857.9 μs | 58.71 μs |    5 | 10561.78 KB |
|                       |                              |             |          |      |             |
| Jint                  | interop-string-passing       |    514.8 μs |  1.08 μs |    1 |   304.95 KB |
| YantraJS              | interop-string-passing       |    618.3 μs |  2.47 μs |    2 |  1823.71 KB |
| NilJS                 | interop-string-passing       |    864.6 μs |  4.35 μs |    3 |  1179.64 KB |
| ClearScript_FastProxy | interop-string-passing       |  2,903.7 μs | 18.96 μs |    4 |   247.15 KB |
| ClearScript           | interop-string-passing       |  5,441.7 μs | 34.01 μs |    5 |  2470.18 KB |

## Engine versions

* Jint — `main`, post-4.14.0 (includes the interop work through #2756)
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.406
* Microsoft.ClearScript.V8 7.5.1

Both tables come from one benchmark session on the same machine and .NET runtime (all lanes,
including ClearScript, measured together). ClearScript's V8 lanes run measurably faster here than
in the 4.14.0 session on the identical package (7.5.1) — a reminder that the native-JIT rows carry
more session-to-session variance than the managed engines: `array-stress` and
`dromaeo-object-array-modern` now rank the V8 compiled lane first where the 4.14.0 table showed
Jint tied for the lead, while Jint's own times on those rows are unchanged. Last updated
2026-07-24 (`main`, post-4.14.0 interop work through #2756).

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

```
| Method               | FileName                     | Mean             | StdDev         | Rank | Allocated     |
|--------------------- |----------------------------- |-----------------:|---------------:|-----:|--------------:|
| ClearScript_Compiled | array-stress                 |     2,070.717 μs |     18.8536 μs |    1 |       8.03 KB |
| Jint_ParsedScript    | array-stress                 |     2,224.312 μs |     10.7156 μs |    2 |     1062.1 KB |
| Jint                 | array-stress                 |     2,251.671 μs |     12.8188 μs |    2 |    1090.83 KB |
| YantraJS             | array-stress                 |     2,866.395 μs |     38.4095 μs |    3 |   17123.82 KB |
| ClearScript          | array-stress                 |     3,338.085 μs |     21.1109 μs |    4 |       16.1 KB |
| NilJS                | array-stress                 |     4,874.909 μs |     28.8639 μs |    5 |    4521.19 KB |
| Okojo                | array-stress                 |     5,582.364 μs |     26.4414 μs |    6 |    2697.71 KB |
| Okojo_Prepared       | array-stress                 |     5,669.218 μs |     14.0773 μs |    6 |    2682.08 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-3d-cube-modern       |     1,373.079 μs |      8.0547 μs |    1 |       9.27 KB |
| YantraJS             | dromaeo-3d-cube-modern       |     2,481.972 μs |     13.9852 μs |    2 |    7514.24 KB |
| ClearScript          | dromaeo-3d-cube-modern       |     3,192.467 μs |     15.7704 μs |    3 |      14.52 KB |
| Jint_ParsedScript    | dromaeo-3d-cube-modern       |     4,674.579 μs |     15.0968 μs |    4 |     1367.5 KB |
| Jint                 | dromaeo-3d-cube-modern       |     5,122.418 μs |     18.8065 μs |    5 |    1671.38 KB |
| NilJS                | dromaeo-3d-cube-modern       |     6,872.215 μs |     31.0621 μs |    6 |    5977.95 KB |
| Okojo_Prepared       | dromaeo-3d-cube-modern       |     7,169.112 μs |    102.8560 μs |    7 |    2311.46 KB |
| Okojo                | dromaeo-3d-cube-modern       |     7,464.154 μs |    103.8379 μs |    7 |    2498.68 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | dromaeo-core-eval-modern     |       890.107 μs |      4.9618 μs |    1 |     345.53 KB |
| Jint                 | dromaeo-core-eval-modern     |       925.648 μs |      2.8018 μs |    2 |     365.25 KB |
| ClearScript_Compiled | dromaeo-core-eval-modern     |       942.198 μs |      3.8575 μs |    2 |       8.02 KB |
| NilJS                | dromaeo-core-eval-modern     |     1,476.435 μs |      7.5360 μs |    3 |    1575.94 KB |
| ClearScript          | dromaeo-core-eval-modern     |     2,054.288 μs |      9.8899 μs |    4 |       12.9 KB |
| YantraJS             | dromaeo-core-eval-modern     |     4,919.636 μs |     53.8798 μs |    5 |   35784.84 KB |
| Okojo                | dromaeo-core-eval-modern     |     6,505.542 μs |    164.3748 μs |    6 |    4627.74 KB |
| Okojo_Prepared       | dromaeo-core-eval-modern     |     6,570.109 μs |    164.5604 μs |    6 |    4613.45 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-array-modern  |    13,772.605 μs |     59.6378 μs |    1 |      16.12 KB |
| ClearScript          | dromaeo-object-array-modern  |    15,416.686 μs |     99.2888 μs |    2 |     114.05 KB |
| Jint                 | dromaeo-object-array-modern  |    15,604.874 μs |     73.4542 μs |    2 |    9167.38 KB |
| Jint_ParsedScript    | dromaeo-object-array-modern  |    15,611.712 μs |     53.2607 μs |    2 |    9119.99 KB |
| YantraJS             | dromaeo-object-array-modern  |    24,449.422 μs |    319.8778 μs |    3 |   223803.5 KB |
| Okojo                | dromaeo-object-array-modern  |    40,179.459 μs |    179.2848 μs |    4 |    7014.16 KB |
| Okojo_Prepared       | dromaeo-object-array-modern  |    41,189.978 μs |    129.2995 μs |    4 |    6984.35 KB |
| NilJS                | dromaeo-object-array-modern  |    52,567.030 μs |    153.4730 μs |    5 |   17863.19 KB |
|                      |                              |                  |                |      |               |
| ClearScript          | dromaeo-object-regexp-modern |    90,695.067 μs |    287.1551 μs |    1 |      41.43 KB |
| ClearScript_Compiled | dromaeo-object-regexp-modern |   105,977.967 μs |  1,093.7108 μs |    2 |      15.66 KB |
| Jint                 | dromaeo-object-regexp-modern |   111,918.157 μs |  9,427.2541 μs |    2 |  153356.36 KB |
| Jint_ParsedScript    | dromaeo-object-regexp-modern |   123,562.907 μs |  8,075.9673 μs |    3 |  157849.62 KB |
| NilJS                | dromaeo-object-regexp-modern |   529,892.929 μs | 10,846.6914 μs |    4 |  766815.88 KB |
| YantraJS             | dromaeo-object-regexp-modern |   709,595.600 μs |  4,699.6622 μs |    5 |  826331.01 KB |
| Okojo_Prepared       | dromaeo-object-regexp-modern | 1,862,967.473 μs | 11,284.3376 μs |    6 | 1801036.77 KB |
| Okojo                | dromaeo-object-regexp-modern | 1,869,167.413 μs | 13,788.7379 μs |    6 | 1798696.52 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-string-modern |     5,848.974 μs |     39.4787 μs |    1 |      15.69 KB |
| ClearScript          | dromaeo-object-string-modern |     8,632.229 μs |     42.5448 μs |    2 |      25.26 KB |
| Jint_ParsedScript    | dromaeo-object-string-modern |    44,403.764 μs |    771.9922 μs |    3 |   21389.36 KB |
| Jint                 | dromaeo-object-string-modern |    44,928.790 μs |    938.8042 μs |    3 |   21486.57 KB |
| Okojo_Prepared       | dromaeo-object-string-modern |    53,770.123 μs |  1,202.7834 μs |    4 |   33410.09 KB |
| Okojo                | dromaeo-object-string-modern |    55,051.622 μs |  1,131.2740 μs |    4 |   33526.08 KB |
| NilJS                | dromaeo-object-string-modern |   137,293.988 μs |  3,469.0529 μs |    5 | 1354903.56 KB |
| YantraJS             | dromaeo-object-string-modern |   164,813.590 μs |  5,628.5749 μs |    6 | 1656450.69 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-string-base64-modern |     1,691.525 μs |      5.5891 μs |    1 |       8.85 KB |
| ClearScript          | dromaeo-string-base64-modern |     3,414.285 μs |     21.6836 μs |    2 |      15.39 KB |
| Jint_ParsedScript    | dromaeo-string-base64-modern |    19,742.280 μs |     43.1686 μs |    3 |    1625.73 KB |
| Jint                 | dromaeo-string-base64-modern |    24,546.672 μs |     61.8519 μs |    4 |    1726.13 KB |
| NilJS                | dromaeo-string-base64-modern |    31,173.319 μs |    568.5709 μs |    5 |   31360.34 KB |
| Okojo_Prepared       | dromaeo-string-base64-modern |    31,565.555 μs |    458.1345 μs |    5 |   43747.38 KB |
| Okojo                | dromaeo-string-base64-modern |    32,036.239 μs |    145.9724 μs |    5 |   43821.93 KB |
| YantraJS             | dromaeo-string-base64-modern |    35,053.551 μs |    366.7451 μs |    6 |  764771.55 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | evaluation-modern            |         4.737 μs |      0.0233 μs |    1 |      17.88 KB |
| Jint                 | evaluation-modern            |        14.151 μs |      0.0912 μs |    2 |      29.01 KB |
| NilJS                | evaluation-modern            |        26.860 μs |      0.1301 μs |    3 |      22.35 KB |
| YantraJS             | evaluation-modern            |       132.166 μs |      1.1629 μs |    4 |      703.4 KB |
| ClearScript_Compiled | evaluation-modern            |       377.080 μs |      1.2704 μs |    5 |        6.1 KB |
| ClearScript          | evaluation-modern            |     1,144.307 μs |      5.6028 μs |    6 |      10.97 KB |
| Okojo                | evaluation-modern            |     1,576.968 μs |     56.5058 μs |    7 |    1290.76 KB |
| Okojo_Prepared       | evaluation-modern            |     1,629.981 μs |     45.8518 μs |    7 |    1283.45 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | json-parse-modern            |     7,433.759 μs |     21.2209 μs |    1 |       9.93 KB |
| ClearScript          | json-parse-modern            |     9,278.783 μs |     39.6587 μs |    2 |      16.98 KB |
| Jint_ParsedScript    | json-parse-modern            |    15,930.472 μs |    234.2620 μs |    3 |   11892.29 KB |
| Jint                 | json-parse-modern            |    16,696.534 μs |    229.7754 μs |    4 |   11927.81 KB |
| YantraJS             | json-parse-modern            |    25,754.020 μs |    223.2252 μs |    5 |   43167.35 KB |
| Okojo_Prepared       | json-parse-modern            |    26,753.731 μs |  1,038.8543 μs |    5 |   27235.55 KB |
| Okojo                | json-parse-modern            |    26,991.257 μs |  1,364.0664 μs |    5 |   27271.83 KB |
| NilJS                | json-parse-modern            |   127,561.817 μs |    530.0344 μs |    6 |   67095.19 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | linq-js                      |        71.250 μs |      0.2669 μs |    1 |     213.59 KB |
| YantraJS             | linq-js                      |       330.127 μs |      1.9148 μs |    2 |    1049.75 KB |
| ClearScript_Compiled | linq-js                      |       462.526 μs |      3.0524 μs |    3 |       6.22 KB |
| Jint                 | linq-js                      |     1,204.323 μs |      4.8938 μs |    4 |    1312.81 KB |
| ClearScript          | linq-js                      |     2,043.326 μs |      6.5984 μs |    5 |      10.96 KB |
| NilJS                | linq-js                      |     4,070.344 μs |     10.8037 μs |    6 |    2739.46 KB |
| Okojo_Prepared       | linq-js                      |     6,363.422 μs |     13.5666 μs |    7 |    4131.84 KB |
| Okojo                | linq-js                      |     8,884.573 μs |    251.6005 μs |    8 |    4955.02 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | minimal                      |         1.058 μs |      0.0174 μs |    1 |       9.38 KB |
| Jint                 | minimal                      |         2.149 μs |      0.0158 μs |    2 |      11.31 KB |
| NilJS                | minimal                      |         2.816 μs |      0.0083 μs |    3 |       4.51 KB |
| YantraJS             | minimal                      |       127.858 μs |      1.0156 μs |    4 |     697.62 KB |
| ClearScript_Compiled | minimal                      |       375.419 μs |      2.6112 μs |    5 |       6.09 KB |
| ClearScript          | minimal                      |     1,113.591 μs |      5.4184 μs |    6 |      10.97 KB |
| Okojo_Prepared       | minimal                      |     1,312.666 μs |     80.2226 μs |    7 |    1247.39 KB |
| Okojo                | minimal                      |     1,328.746 μs |     72.6554 μs |    7 |     1249.2 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | stopwatch-modern             |    14,118.650 μs |     19.8369 μs |    1 |       9.16 KB |
| ClearScript          | stopwatch-modern             |    16,941.619 μs |     73.4862 μs |    2 |      22.71 KB |
| YantraJS             | stopwatch-modern             |    60,972.002 μs |    325.6506 μs |    3 |  234033.07 KB |
| Jint_ParsedScript    | stopwatch-modern             |    86,615.751 μs |    394.9141 μs |    4 |   12089.66 KB |
| Jint                 | stopwatch-modern             |    88,606.332 μs |    701.3330 μs |    4 |   12122.04 KB |
| Okojo                | stopwatch-modern             |   149,499.554 μs |    661.2721 μs |    5 |   21469.59 KB |
| Okojo_Prepared       | stopwatch-modern             |   156,826.875 μs |    844.2773 μs |    6 |   21444.16 KB |
| NilJS                | stopwatch-modern             |   212,344.536 μs |  1,242.0594 μs |    7 |  324502.66 KB |
