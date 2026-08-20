# Engine comparison benchmarks

This project benchmarks Jint against the other JavaScript engines available to .NET applications:
the managed engines ([NiL.JS](https://github.com/nilproject/NiL.JS),
[Okojo](https://github.com/akeit0/okojo) and [YantraJS](https://github.com/yantrajs/yantra)) and
[ClearScript](https://github.com/ClearFoundry/ClearScript) — Microsoft-originated, now
ClearFoundry-maintained bindings to Google's native V8 engine (the JIT inside Chrome and Node.js) —
across a set of representative scripts.

> Most of this document is about that cross-engine comparison. The rest of the project is Jint-only
> micro-benchmarks, which need no prose beyond their own XML doc comments — with one exception:
> the opt-in WHATWG web APIs have their own category, their own smoke mode and their own rules about
> what to run when. That is [Web API rows](#web-api-rows), at the end.

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
  appearing ahead of its compiled lane, and Jint's own two lanes sitting ~12% apart — as
  run-to-run noise. Jint's lead on that row is not one of those: the prepared lane is ~18 ms clear
  of the nearest V8 lane, more than five times its own standard deviation.

## At a glance

Using each engine's recommended production path (for Jint a cached prepared script,
`Jint_ParsedScript`; for ClearScript a precompiled `V8Script` on a shared runtime,
`ClearScript_Compiled`):

* **Jint owns everything start-up-shaped and eval-shaped; native V8 owns long compute.** Jint is
  the fastest engine outright on `minimal` (**1.1 µs vs V8's 380 µs, ~345×**), `evaluation-modern`
  (**~80×**), `linq-js` (**~6.7×**), `dromaeo-core-eval-modern` (**~5%** ahead of V8's compiled
  lane — eval defeats its compile cache) and `dromaeo-object-regexp-modern` (**1.25× ahead of V8's
  fresh-context lane and 1.46× ahead of its compiled lane**). That is five rows outright, out of
  twelve. No other managed engine takes a single row's top rank away from Jint or V8.
* **V8's wins are the tight-loop compute rows**: `dromaeo-string-base64-modern` (9.8×),
  `dromaeo-object-string-modern` (6.6×), `stopwatch-modern` (6.0×), `dromaeo-3d-cube-modern`
  (3.4×), `json-parse-modern` (2.2×) and narrow leads on `array-stress` (1.09×) and
  `dromaeo-object-array-modern` (1.08×) — the structural interpreter-vs-JIT gap. The two string
  rows carry the widest spread between Jint's own two lanes in this table (5% on base64, 10% on
  object-string, for lanes that differ only in whether the script was re-parsed), so read those
  two multipliers as approximate.
* **Jint is the fastest managed engine on 10 of 12 scripts** (the IL-compiling YantraJS leads
  `dromaeo-3d-cube-modern` and `stopwatch-modern`) and **the fastest interpreter on all 12**.
* **Jint allocates the least of the managed engines on 10 of 12 scripts** — Okojo is lower on
  `dromaeo-object-array-modern` (6,985 vs 9,122 KB) and NiL.JS on `minimal` (4.5 vs 9.9 KB).
  NiL.JS and YantraJS allocate one to two orders of magnitude more on the heavy rows, up to
  **~469×** more than Jint on `dromaeo-string-base64-modern` (763,918 vs 1,628 KB), which means
  far heavier GC pressure in real applications. (ClearScript's rows cannot be compared here — its
  memory lives on the V8 native heap, which the managed diagnoser does not see.)
* **Pure-JS compute is only half the story** — the [interop suite](#script--host-interop) below
  measures the script ↔ host boundary, where the picture inverts and Jint beats ClearScript on
  every row by 3.4×–11.2×.

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

### What changed for 4.16.0

The headline of the 4.16.0 round is **proper tail calls for strict functions**
([#2975](https://github.com/sebastienros/jint/pull/2975)): tail-position analysis plus a trampoline
that dispatches an interpreted tail call without growing the execution-context or call stacks,
with cleanup, constructor, callback, debugger and recursion-limit semantics preserved. In Jint's
own script benchmarks — not this comparison suite — the `controlflow-recursive` row measured
**−15.6% time and −40.4% allocation** against 4.15.3. Here it lands wherever a script recurses in
tail position rather than on any single row.

The fast-call lane — built-in calls that skip the pooled argument array and, where every route to
user code is closed off by a checkable guard, the call frame as well — grew again:
`parseInt`/`parseFloat` became one realm intrinsic each and took the frameless lane
([#2968](https://github.com/sebastienros/jint/pull/2968)); the cheapest `Number` type tests and six
`String.prototype` search methods followed under declared per-argument guards
([#2980](https://github.com/sebastienros/jint/pull/2980)); and `Map`/`Set` lookups became frameless
once each instance carried a type bit the guard could check
([#2984](https://github.com/sebastienros/jint/pull/2984)). On the interop side a wrapped dictionary
now answers existence questions — `in`, `hasOwnProperty`, `for..in`, `Object.keys`, spread,
`JSON.stringify` — from the target's compiled `ContainsKey` instead of converting the value and
building a descriptor only to discard both
([#2969](https://github.com/sebastienros/jint/pull/2969)); no script in the interop suite
enumerates a host dictionary, so that one does not surface in the table below.

One correctness fix went the other way, and its cost is disclosed rather than absorbed quietly:
`Array.prototype.join` (and `toString`, which is defined as a call to it) now re-reads a hole
through the array instead of answering `undefined` from its snapshot, because a `toString` side
effect on an earlier element can add an index property to the prototype that every later element
has to see ([#3003](https://github.com/sebastienros/jint/pull/3003)). The packed path is untouched;
a hole-heavy join costs **+3.4%**.

Against the 4.15.0 tables: the two sessions ran on different runtime and OS builds (.NET 10.0.10 →
10.0.11) with a YantraJS bump in between, so no row-for-row delta is claimed here — the comparison
below is directional only. The rank structure is the one 4.15.0 published, with one change of lead
in each table, and they point in opposite directions: `array-stress` slips out of its rank-1 tie,
leaving V8's compiled lane 1.09× ahead, while `interop-collection-traversal` puts Jint back into a
rank-1 tie with NiL.JS. Every other row is led by the engine that led it before, and
`dromaeo-object-regexp-modern` stays Jint's, ~18 ms clear of the nearest V8 lane.

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
  ClearScript **8.6×–11.2×** against Jint on every row — the mirror image of the pure-compute
  table, and the half of the story that matters most in chatty embedding scenarios.
* **FastProxy narrows the gap but does not close it**: still **3.4×–7.0×** behind Jint, and it
  trades away convenience — every member is hand-registered instead of reflected. Its
  zero-allocation claim is real, though: 13 KB managed allocation on the method-call row where
  reflective ClearScript burns 12,753 KB.
* **Jint leads string passing outright, is tied for the lead on collection traversal, and
  allocates the least of the managed engines on all four rows.** It is rank 1 on
  `interop-string-passing` (522.4 µs, ~10% clear of YantraJS) and rank 1 alongside NiL.JS on
  `interop-collection-traversal` (1,251.0 vs 1,242.7 µs — 0.7% apart, a statistical tie that
  BenchmarkDotNet ranks equal). It is rank 2 on `interop-method-calls` (NiL.JS ahead, 1,172.2 vs
  1,440.0 µs, Jint +23%) and on `interop-property-access` (YantraJS ahead, 1,451.9 vs 1,489.8 µs,
  Jint +2.6%). Allocation is Jint's on every row: 3.9×–12.4× less than the row's nearest managed
  competitor. (Three ClearScript_FastProxy rows show smaller KB figures, but those count
  interop-bridge overhead only — V8's working memory is on its native heap.)
* **Collection traversal — last of all engines at 4.13.0 — stays transformed at
  15,597 → 1,251 µs (12.5×, −99% allocation) with no script changes**: the 4.14.0
  `ArrayConversionMode.LiveView` default exposes the host array as a live view instead of
  re-copying it on every read, and a per-descriptor value memo
  ([#2756](https://github.com/sebastienros/jint/pull/2756)) skips re-converting the array on every
  read. NiL.JS took the row back by ~4% in the previous session; here the two are level (0.7%
  apart, both rank 1) while NiL.JS allocates 12× more. Either way the old hoist-into-a-local
  workaround is no longer needed.

| Method                | FileName                     | Mean        | StdDev   | Rank | Allocated   |
|---------------------- |----------------------------- |------------:|---------:|-----:|------------:|
| NilJS                 | interop-collection-traversal |  1,242.7 μs |  3.19 μs |    1 |  4088.14 KB |
| Jint                  | interop-collection-traversal |  1,251.0 μs |  1.78 μs |    1 |   330.97 KB |
| YantraJS              | interop-collection-traversal |  3,685.3 μs | 13.86 μs |    2 |  5395.96 KB |
| ClearScript_FastProxy | interop-collection-traversal |  8,779.5 μs | 18.91 μs |    3 |  1185.72 KB |
| ClearScript           | interop-collection-traversal | 11,691.8 μs | 27.62 μs |    4 |  5016.24 KB |
|                       |                              |             |          |      |             |
| NilJS                 | interop-method-calls         |  1,172.2 μs |  3.76 μs |    1 |  2437.94 KB |
| Jint                  | interop-method-calls         |  1,440.0 μs |  2.86 μs |    2 |   328.37 KB |
| YantraJS              | interop-method-calls         |  2,102.4 μs |  8.30 μs |    3 |  3355.13 KB |
| ClearScript_FastProxy | interop-method-calls         |  5,457.7 μs | 15.37 μs |    4 |    13.01 KB |
| ClearScript           | interop-method-calls         | 16,108.5 μs | 57.35 μs |    5 | 12752.52 KB |
|                       |                              |             |          |      |             |
| YantraJS              | interop-property-access      |  1,451.9 μs |  3.45 μs |    1 |  1870.68 KB |
| Jint                  | interop-property-access      |  1,489.8 μs |  3.12 μs |    2 |    329.3 KB |
| NilJS                 | interop-property-access      |  1,702.0 μs |  5.10 μs |    3 |  4391.52 KB |
| ClearScript_FastProxy | interop-property-access      |  5,111.8 μs |  9.73 μs |    4 |    12.66 KB |
| ClearScript           | interop-property-access      | 12,875.7 μs | 20.43 μs |    5 | 10561.78 KB |
|                       |                              |             |          |      |             |
| Jint                  | interop-string-passing       |    522.4 μs |  0.96 μs |    1 |   304.08 KB |
| YantraJS              | interop-string-passing       |    577.5 μs |  3.25 μs |    2 |  1714.38 KB |
| NilJS                 | interop-string-passing       |    769.8 μs |  1.45 μs |    3 |  1179.64 KB |
| ClearScript_FastProxy | interop-string-passing       |  2,961.1 μs |  9.59 μs |    4 |   247.15 KB |
| ClearScript           | interop-string-passing       |  5,263.2 μs | 19.95 μs |    5 |  2470.18 KB |

## Engine versions

* Jint 4.16.0
* NiL.JS 2.6.1722
* Okojo 0.1.2-preview.1
* YantraJS.Core 1.2.422
* Microsoft.ClearScript.V8 7.5.1

Both tables come from one benchmark session on the same machine and .NET runtime (all lanes,
including ClearScript, measured together): 116 benchmarks — 96 script rows (8 lanes × 12 scripts)
and 20 interop rows (5 lanes × 4 scripts) — default job, otherwise idle machine. ClearScript's V8
lanes land within ~3% of the previous (2026-07-28) session on the identical package, so those rows
are broadly comparable across the two — but the ~30% swing those same lanes showed between the
4.14.0 and 2026-07-24 sessions is the standing reminder that the native-JIT rows carry more
session-to-session variance than the managed engines, and that absolute numbers should be compared
within a table rather than across sessions. Two things moved under the whole table between the two
sessions besides Jint itself: YantraJS 1.2.419 → 1.2.422, and the host runtime .NET 10.0.10 →
10.0.11 (SDK 10.0.302 → 10.0.400) on a newer Windows build. Last updated 2026-08-13 (4.16.0
release candidate `7b56c8341`).

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

```
| Method               | FileName                     | Mean             | StdDev         | Rank | Allocated     |
|--------------------- |----------------------------- |-----------------:|---------------:|-----:|--------------:|
| ClearScript_Compiled | array-stress                 |     2,035.717 μs |      7.6419 μs |    1 |        8.1 KB |
| Jint                 | array-stress                 |     2,209.995 μs |      3.9495 μs |    2 |    1091.93 KB |
| Jint_ParsedScript    | array-stress                 |     2,212.058 μs |      5.5029 μs |    2 |     1063.2 KB |
| YantraJS             | array-stress                 |     2,815.214 μs |     26.4164 μs |    3 |   17093.93 KB |
| ClearScript          | array-stress                 |     3,295.998 μs |      9.5510 μs |    4 |      16.06 KB |
| NilJS                | array-stress                 |     4,845.159 μs |      6.6924 μs |    5 |    4521.19 KB |
| Okojo                | array-stress                 |     5,546.333 μs |     17.9347 μs |    6 |    2697.82 KB |
| Okojo_Prepared       | array-stress                 |     5,546.651 μs |     25.6847 μs |    6 |    2682.11 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-3d-cube-modern       |     1,361.995 μs |      4.2358 μs |    1 |       9.26 KB |
| YantraJS             | dromaeo-3d-cube-modern       |     2,299.281 μs |      5.9113 μs |    2 |    7436.04 KB |
| ClearScript          | dromaeo-3d-cube-modern       |     3,178.514 μs |     16.6576 μs |    3 |      14.49 KB |
| Jint_ParsedScript    | dromaeo-3d-cube-modern       |     4,607.760 μs |      8.5718 μs |    4 |    1374.41 KB |
| Jint                 | dromaeo-3d-cube-modern       |     4,988.145 μs |      6.7079 μs |    5 |    1679.78 KB |
| Okojo_Prepared       | dromaeo-3d-cube-modern       |     6,826.450 μs |    109.1199 μs |    6 |    2311.61 KB |
| NilJS                | dromaeo-3d-cube-modern       |     7,095.742 μs |      4.8978 μs |    7 |    5977.95 KB |
| Okojo                | dromaeo-3d-cube-modern       |     7,161.629 μs |     97.7917 μs |    7 |    2498.75 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | dromaeo-core-eval-modern     |       886.665 μs |      0.6373 μs |    1 |     346.77 KB |
| Jint                 | dromaeo-core-eval-modern     |       920.932 μs |      1.4350 μs |    2 |     366.23 KB |
| ClearScript_Compiled | dromaeo-core-eval-modern     |       933.105 μs |      3.4049 μs |    2 |       7.82 KB |
| NilJS                | dromaeo-core-eval-modern     |     1,382.077 μs |      1.4440 μs |    3 |    1575.94 KB |
| ClearScript          | dromaeo-core-eval-modern     |     2,052.775 μs |     12.2834 μs |    4 |       12.9 KB |
| YantraJS             | dromaeo-core-eval-modern     |     4,839.609 μs |     21.3171 μs |    5 |   35784.84 KB |
| Okojo                | dromaeo-core-eval-modern     |     5,766.205 μs |    282.7096 μs |    6 |    4628.16 KB |
| Okojo_Prepared       | dromaeo-core-eval-modern     |     6,731.548 μs |    166.2416 μs |    7 |    4613.12 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-array-modern  |    13,511.360 μs |     28.1602 μs |    1 |      16.29 KB |
| Jint                 | dromaeo-object-array-modern  |    14,560.874 μs |    161.6323 μs |    2 |    9168.77 KB |
| Jint_ParsedScript    | dromaeo-object-array-modern  |    14,574.726 μs |    148.7198 μs |    2 |    9121.77 KB |
| ClearScript          | dromaeo-object-array-modern  |    15,233.491 μs |     42.0857 μs |    3 |     111.74 KB |
| YantraJS             | dromaeo-object-array-modern  |    26,468.928 μs |    449.6599 μs |    4 |  223683.36 KB |
| Okojo_Prepared       | dromaeo-object-array-modern  |    40,069.776 μs |     82.4893 μs |    5 |    6984.74 KB |
| Okojo                | dromaeo-object-array-modern  |    40,370.712 μs |     73.8563 μs |    5 |    7014.22 KB |
| NilJS                | dromaeo-object-array-modern  |    52,137.758 μs |     63.4400 μs |    6 |   17863.19 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | dromaeo-object-regexp-modern |    71,958.764 μs |  3,427.6529 μs |    1 |   82555.71 KB |
| Jint                 | dromaeo-object-regexp-modern |    80,412.888 μs |  6,125.1272 μs |    2 |   83913.27 KB |
| ClearScript          | dromaeo-object-regexp-modern |    90,178.237 μs |    286.2653 μs |    3 |      32.86 KB |
| ClearScript_Compiled | dromaeo-object-regexp-modern |   105,206.432 μs |  1,176.4466 μs |    4 |      15.71 KB |
| NilJS                | dromaeo-object-regexp-modern |   538,262.653 μs |  6,740.4339 μs |    5 |  767391.28 KB |
| YantraJS             | dromaeo-object-regexp-modern |   727,779.264 μs |  7,367.8313 μs |    6 |  826323.88 KB |
| Okojo_Prepared       | dromaeo-object-regexp-modern | 1,873,094.293 μs |  8,600.8802 μs |    7 | 1799271.88 KB |
| Okojo                | dromaeo-object-regexp-modern | 1,886,667.414 μs | 20,848.1909 μs |    7 | 1799460.78 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-object-string-modern |     5,837.141 μs |     22.5034 μs |    1 |      15.64 KB |
| ClearScript          | dromaeo-object-string-modern |     8,561.271 μs |     27.2480 μs |    2 |      25.24 KB |
| Jint                 | dromaeo-object-string-modern |    35,062.101 μs |  1,535.6300 μs |    3 |   21529.67 KB |
| Jint_ParsedScript    | dromaeo-object-string-modern |    38,511.691 μs |  2,516.2942 μs |    4 |   21340.16 KB |
| Okojo_Prepared       | dromaeo-object-string-modern |    55,635.516 μs |  1,113.7534 μs |    5 |    33452.7 KB |
| Okojo                | dromaeo-object-string-modern |    55,910.038 μs |    646.7601 μs |    5 |    33511.2 KB |
| NilJS                | dromaeo-object-string-modern |   143,285.835 μs |  2,970.2749 μs |    6 | 1354957.51 KB |
| YantraJS             | dromaeo-object-string-modern |   176,114.896 μs |  6,047.6327 μs |    7 | 1648871.26 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | dromaeo-string-base64-modern |     1,697.677 μs |      7.8604 μs |    1 |       8.89 KB |
| ClearScript          | dromaeo-string-base64-modern |     3,404.818 μs |     12.1244 μs |    2 |      15.38 KB |
| Jint_ParsedScript    | dromaeo-string-base64-modern |    16,717.857 μs |     31.9543 μs |    3 |    1628.09 KB |
| Jint                 | dromaeo-string-base64-modern |    17,639.956 μs |     21.7736 μs |    4 |    1728.46 KB |
| Okojo_Prepared       | dromaeo-string-base64-modern |    30,042.911 μs |    426.3164 μs |    5 |   43745.71 KB |
| Okojo                | dromaeo-string-base64-modern |    31,428.279 μs |    285.0574 μs |    6 |   43825.36 KB |
| NilJS                | dromaeo-string-base64-modern |    32,092.329 μs |    607.6797 μs |    6 |   31360.34 KB |
| YantraJS             | dromaeo-string-base64-modern |    35,415.762 μs |    762.1968 μs |    7 |  763917.73 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | evaluation-modern            |         4.651 μs |      0.0116 μs |    1 |      18.61 KB |
| Jint                 | evaluation-modern            |        14.038 μs |      0.0454 μs |    2 |      29.74 KB |
| NilJS                | evaluation-modern            |        25.992 μs |      0.0602 μs |    3 |      22.35 KB |
| YantraJS             | evaluation-modern            |       129.352 μs |      1.0605 μs |    4 |      703.4 KB |
| ClearScript_Compiled | evaluation-modern            |       374.891 μs |      1.1516 μs |    5 |       6.09 KB |
| ClearScript          | evaluation-modern            |     1,139.884 μs |      4.8187 μs |    6 |      10.97 KB |
| Okojo_Prepared       | evaluation-modern            |     1,437.314 μs |     49.8982 μs |    7 |    1283.54 KB |
| Okojo                | evaluation-modern            |     1,564.927 μs |     61.0150 μs |    8 |    1290.72 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | json-parse-modern            |     7,465.707 μs |     18.3971 μs |    1 |       9.79 KB |
| ClearScript          | json-parse-modern            |     9,252.981 μs |     38.1479 μs |    2 |      17.14 KB |
| Jint_ParsedScript    | json-parse-modern            |    16,243.839 μs |    208.5431 μs |    3 |   11397.93 KB |
| Jint                 | json-parse-modern            |    16,680.470 μs |    224.1474 μs |    3 |   11434.71 KB |
| YantraJS             | json-parse-modern            |    24,030.128 μs |    202.9171 μs |    4 |   42849.84 KB |
| Okojo                | json-parse-modern            |    24,851.231 μs |     56.3649 μs |    5 |   27271.79 KB |
| Okojo_Prepared       | json-parse-modern            |    24,871.608 μs |     43.3566 μs |    5 |   27235.55 KB |
| NilJS                | json-parse-modern            |   126,595.546 μs |    275.3617 μs |    6 |   67095.19 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | linq-js                      |        68.403 μs |      0.2889 μs |    1 |     214.39 KB |
| YantraJS             | linq-js                      |       316.724 μs |      1.6240 μs |    2 |    1049.75 KB |
| ClearScript_Compiled | linq-js                      |       462.290 μs |      2.4241 μs |    3 |       6.21 KB |
| Jint                 | linq-js                      |     1,091.041 μs |      3.5935 μs |    4 |    1296.63 KB |
| ClearScript          | linq-js                      |     2,036.363 μs |      8.9904 μs |    5 |      10.96 KB |
| NilJS                | linq-js                      |     3,865.936 μs |      4.6159 μs |    6 |    2739.46 KB |
| Okojo_Prepared       | linq-js                      |     5,912.862 μs |    133.9733 μs |    7 |    4131.95 KB |
| Okojo                | linq-js                      |     8,574.856 μs |    337.6247 μs |    8 |    4928.88 KB |
|                      |                              |                  |                |      |               |
| Jint_ParsedScript    | minimal                      |         1.094 μs |      0.0051 μs |    1 |       9.92 KB |
| Jint                 | minimal                      |         2.095 μs |      0.0061 μs |    2 |      11.84 KB |
| NilJS                | minimal                      |         2.883 μs |      0.0080 μs |    3 |       4.51 KB |
| YantraJS             | minimal                      |       126.579 μs |      0.5990 μs |    4 |     697.62 KB |
| ClearScript_Compiled | minimal                      |       379.509 μs |      1.4249 μs |    5 |       6.12 KB |
| ClearScript          | minimal                      |     1,127.502 μs |      6.8833 μs |    6 |      10.97 KB |
| Okojo                | minimal                      |     1,136.618 μs |     79.5654 μs |    6 |    1249.24 KB |
| Okojo_Prepared       | minimal                      |     1,154.071 μs |     80.6357 μs |    6 |    1247.39 KB |
|                      |                              |                  |                |      |               |
| ClearScript_Compiled | stopwatch-modern             |    14,161.990 μs |     43.6106 μs |    1 |       9.01 KB |
| ClearScript          | stopwatch-modern             |    17,170.161 μs |     53.0071 μs |    2 |      22.67 KB |
| YantraJS             | stopwatch-modern             |    56,498.545 μs |    187.0381 μs |    3 |  233993.23 KB |
| Jint                 | stopwatch-modern             |    85,132.661 μs |    332.5876 μs |    4 |   12122.73 KB |
| Jint_ParsedScript    | stopwatch-modern             |    85,204.309 μs |    753.0055 μs |    4 |   12090.63 KB |
| Okojo                | stopwatch-modern             |   151,840.067 μs |    916.1697 μs |    5 |   21469.24 KB |
| Okojo_Prepared       | stopwatch-modern             |   155,504.922 μs |  2,858.1551 μs |    5 |   21445.98 KB |
| NilJS                | stopwatch-modern             |   209,122.986 μs |    815.7981 μs |    6 |  324502.66 KB |

## Web API rows

The opt-in WHATWG web APIs under `Jint/WebApi/` shipped with a strict no-cost-when-off discipline —
nothing is installed unless `Options.WebApi.Features` names it, and a default engine is byte-for-byte
the engine it was before they existed. That half was measured. The other half, what the surface costs
a host that *does* switch it on, had nothing tracking it release to release. These rows are that.

Run the whole category:

```
dotnet run -c Release --project Jint.Benchmark -- --allCategories WebApi
```

Unlike the comparison suite, these rows load no files, so it does not matter which directory you run
them from. As everywhere else here, set `JINT_BENCH_MODE=gate` before quoting any number in a PR.

### Check the rows before measuring them

```
dotnet run -c Release --project Jint.Benchmark -- --smoke-webapi
```

This runs every web-API class's `[GlobalSetup]` and then each of its rows three times, and reports
pass/fail per class in a few seconds. It measures nothing. Two failures it is there to catch, both of
which otherwise surface half an hour into a benchmark session:

* a row's script throwing — a built-in that moved, or a row asking for the wrong feature flag;
* a row that works once and **drifts afterwards**. Every deterministic row records what its own first
  run produced and compares every later run against it, because BenchmarkDotNet will happily average
  an operation that is doing more work each time it runs — state accumulating on the row's engine, a
  queue that is never emptied, a buffer that was detached last time and is not now. That check can
  only bite from the second operation onwards, which is why the smoke mode runs each row more than
  once.

### Which rows cover which feature

| Class | `WebApiFeatures` | Source | Rows |
| --- | --- | --- | --- |
| `WebApiUrlBenchmark` | `Url` | `Url/` | `ParseAbsolute`, `ParseRelative`, `ReadComponents`, `MutateAndSerialize`, `SearchParamsMutate`, `SearchParamsIterate` |
| `WebApiEncodingBenchmark` | `Encoding` | `Encoding/` | `RoundTripSmall`, `RoundTripLarge`, `DecodeStreaming`, `EncodeIntoSmall` |
| `WebApiStructuredCloneBenchmark` | `StructuredClone` | `StructuredClone/` | `CloneFlat`, `CloneNested`, `CloneTransfer` |
| `WebApiFetchObjectModelBenchmark` | `Fetch` (implies `Events`, `Url`, `Files`) | `Fetch/` | `HeadersAppendAndIterate`, `RequestConstruction`, `ResponseConstruction`, `RequestClone` |
| `WebApiStreamsBenchmark` | `Streams` | `Streams/` | `PumpWithReader`, `PumpWithAsyncIteration`, `PipeThroughTransform` |
| `WebApiTimerBenchmark` | `Timers` | `Timers/` | `ScheduleAndCancel`, `FanOutFiring`, `IntervalFiring` |
| `WebApiCryptoBenchmark` | `Crypto` | `Crypto/` | `RandomValuesSmall`, `RandomValuesLarge`, `RandomUuid` |

**No row covers `Console`, `Base64`, `Performance`, `Files`, `Navigator`, `Scheduler` or
`crypto.subtle` yet**, and `Events` is only reached incidentally, through the `AbortSignal` every
`Request` carries. A PR touching one of those has nothing here to move, and saying so is the point of
this paragraph — the alternative is a contributor running the category, seeing it flat, and reading
that as evidence.

Three properties of these rows are load-bearing and should survive any edit to them:

* **One engine per row, warmed with that row's own workload and nothing else** — see
  `IsolatedScript`'s doc comment for the measurement defect that rule exists to prevent. It matters
  more here than elsewhere, because these globals are installed lazily and one row's touch is what
  materializes them.
* **Each engine carries only its own feature**, never `WebApiFeatures.Default`, so a change to one
  feature cannot move another feature's rows through shared installation cost.
* **The timer rows drive a manual `TimeProvider`.** On `TimeProvider.System` every `setTimeout` would
  pay a real clock read and a row whose timers fire would be measuring how long the machine took to
  reach a wall-clock instant. Nothing in these rows sleeps, waits or opens a socket — including the
  fetch rows, which build `Request`/`Response`/`Headers` and never call `fetch`.

### The pre-release regression pass

Seven rows are the ones to run alongside SunSpider and Dromaeo before a release — one per feature
area, each chosen as the row a regression in its area reaches first:

```
dotnet run -c Release --project Jint.Benchmark -- --filter "*WebApiUrlBenchmark.ParseAbsolute" "*WebApiUrlBenchmark.SearchParamsMutate" "*WebApiEncodingBenchmark.RoundTripSmall" "*WebApiStructuredCloneBenchmark.CloneNested" "*WebApiFetchObjectModelBenchmark.RequestConstruction" "*WebApiStreamsBenchmark.PumpWithReader" "*WebApiTimerBenchmark.FanOutFiring"
```

* `WebApiUrlBenchmark.ParseAbsolute` — the URL parser is the largest hand-written state machine in
  the subtree, and `Request` construction runs it too.
* `WebApiUrlBenchmark.SearchParamsMutate` — the other half of `Url/`: the list, its serializer and
  the URL-object update the setters trigger.
* `WebApiEncodingBenchmark.RoundTripSmall` — dominated by per-call ceremony (WebIDL argument
  conversion, result-view allocation), which is the cost every one of these APIs shares, so it is the
  row a change to that shared layer moves.
* `WebApiStructuredCloneBenchmark.CloneNested` — the deepest recursive algorithm in the subtree, over
  a graph carrying a `Map`, a `Set`, a `Date`, a `RegExp`, a typed array and a cycle.
* `WebApiFetchObjectModelBenchmark.RequestConstruction` — one row crossing URL parsing, header-init
  conversion, body extraction and `AbortSignal` creation.
* `WebApiStreamsBenchmark.PumpWithReader` — promise and job-queue traffic, 256 chunks' worth.
* `WebApiTimerBenchmark.FanOutFiring` — the event loop's timer promotion, 500 timers per operation.

The last two are worth running for a change to the **engine's own** event loop as well, not only for
one under `Jint/WebApi/`: they are the densest promise-and-job workloads in the suite.

No baseline table is published here yet. The first `JINT_BENCH_MODE=gate` run of a release cycle
establishes one, and a number measured before that has nothing to be compared against.
