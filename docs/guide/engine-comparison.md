# JavaScript Engine Comparison

There is no universally fastest JavaScript engine. The right choice depends on
whether an application runs short embedded rules, long compute-heavy programs,
or scripts that frequently call into .NET.

The results below compare JavaScript engines available to .NET applications.
They are a starting point, not a substitute for measuring your own scripts and
host objects.

::: info Benchmark snapshot
The current published comparison measures Jint 4.16.0. It does not yet measure
the Jint 5 preview.
:::

## Compared engines

| Engine | Description |
| --- | --- |
| [Jint](https://github.com/sebastienros/jint) | A fully managed tree-walking interpreter that executes prepared JavaScript syntax trees in-process. |
| [NiL.JS](https://github.com/nilproject/NiL.JS) | A managed interpreter with an optimizing pass over its JavaScript syntax tree. |
| [Okojo](https://github.com/akeit0/okojo) | A managed engine that compiles JavaScript to bytecode and executes it on a virtual machine. |
| [YantraJS](https://github.com/yantrajs/yantra) | A managed compiler that emits .NET IL, which the CLR then compiles to native code. |
| [ClearScript with V8](https://github.com/ClearFoundry/ClearScript) | A .NET binding to V8, Google's native multi-tier optimizing JavaScript JIT. |

Interpreters generally start quickly because they execute parsed program
structures directly. Bytecode virtual machines add a compact intermediate
instruction set and can amortize compilation over repeated execution. IL and
native JIT compilers spend more time preparing code so hot loops can eventually
run as optimized machine code.

Those categories describe tendencies, not guarantees. Standards support,
diagnostics, security controls, deployment constraints, memory behavior, and
the host API can be more important than a benchmark result.

## Current results at a glance

Using each engine's recommended cached execution path:

- Jint is the fastest managed engine on 10 of 12 script workloads and the
  fastest interpreter on all 12.
- Jint is fastest overall on five startup-, evaluation-, or
  regular-expression-shaped workloads.
- V8 leads most long, compute-heavy loops.
- YantraJS leads the managed engines on the two compute workloads where its
  compiled IL repays the startup cost.
- Jint has the lowest managed allocation on 10 of 12 script workloads.
- In the host-interop suite, Jint is 3.4x to 11.2x faster than the measured
  ClearScript lanes, depending on the workload and interop mode.

## Script execution results

The table uses the cached lane where one exists: `Prepared<Script>` for Jint, a
reused parsed program for Okojo, and a precompiled `V8Script` on a shared V8
runtime for ClearScript. Each operation still receives a fresh engine or
context, so script state does not leak between runs.

Mean time is shown in microseconds. Lower is better. Each row uses the same
five-shade rank scale; the fastest lane has the strongest background and bold
text. Close measurements may be statistically tied, in which case they share a
shade.

<div class="benchmark-rank-legend" aria-label="Benchmark rank color scale">
  <span><span class="benchmark-rank-swatch benchmark-rank-1"></span>Fastest</span>
  <span><span class="benchmark-rank-swatch benchmark-rank-2"></span>Second</span>
  <span><span class="benchmark-rank-swatch benchmark-rank-3"></span>Third</span>
  <span><span class="benchmark-rank-swatch benchmark-rank-4"></span>Fourth</span>
  <span><span class="benchmark-rank-swatch benchmark-rank-5"></span>Slowest</span>
</div>

| Workload | Jint prepared | NiL.JS | Okojo prepared | YantraJS | V8 compiled |
| --- | ---: | ---: | ---: | ---: | ---: |
| Minimal script | <span class="benchmark-result benchmark-rank-1">1.094</span> | <span class="benchmark-result benchmark-rank-2">2.883</span> | <span class="benchmark-result benchmark-rank-5">1,154.071</span> | <span class="benchmark-result benchmark-rank-3">126.579</span> | <span class="benchmark-result benchmark-rank-4">379.509</span> |
| Expression evaluation | <span class="benchmark-result benchmark-rank-1">4.651</span> | <span class="benchmark-result benchmark-rank-2">25.992</span> | <span class="benchmark-result benchmark-rank-5">1,437.314</span> | <span class="benchmark-result benchmark-rank-3">129.352</span> | <span class="benchmark-result benchmark-rank-4">374.891</span> |
| LINQ-style JavaScript | <span class="benchmark-result benchmark-rank-1">68.403</span> | <span class="benchmark-result benchmark-rank-4">3,865.936</span> | <span class="benchmark-result benchmark-rank-5">5,912.862</span> | <span class="benchmark-result benchmark-rank-2">316.724</span> | <span class="benchmark-result benchmark-rank-3">462.290</span> |
| Dromaeo core evaluation | <span class="benchmark-result benchmark-rank-1">886.665</span> | <span class="benchmark-result benchmark-rank-3">1,382.077</span> | <span class="benchmark-result benchmark-rank-5">6,731.548</span> | <span class="benchmark-result benchmark-rank-4">4,839.609</span> | <span class="benchmark-result benchmark-rank-2">933.105</span> |
| Array stress | <span class="benchmark-result benchmark-rank-2">2,212.058</span> | <span class="benchmark-result benchmark-rank-4">4,845.159</span> | <span class="benchmark-result benchmark-rank-5">5,546.651</span> | <span class="benchmark-result benchmark-rank-3">2,815.214</span> | <span class="benchmark-result benchmark-rank-1">2,035.717</span> |
| Dromaeo 3D cube | <span class="benchmark-result benchmark-rank-3">4,607.760</span> | <span class="benchmark-result benchmark-rank-5">7,095.742</span> | <span class="benchmark-result benchmark-rank-4">6,826.450</span> | <span class="benchmark-result benchmark-rank-2">2,299.281</span> | <span class="benchmark-result benchmark-rank-1">1,361.995</span> |
| JSON parsing | <span class="benchmark-result benchmark-rank-2">16,243.839</span> | <span class="benchmark-result benchmark-rank-5">126,595.546</span> | <span class="benchmark-result benchmark-rank-4">24,871.608</span> | <span class="benchmark-result benchmark-rank-3">24,030.128</span> | <span class="benchmark-result benchmark-rank-1">7,465.707</span> |
| Dromaeo object arrays | <span class="benchmark-result benchmark-rank-2">14,574.726</span> | <span class="benchmark-result benchmark-rank-5">52,137.758</span> | <span class="benchmark-result benchmark-rank-4">40,069.776</span> | <span class="benchmark-result benchmark-rank-3">26,468.928</span> | <span class="benchmark-result benchmark-rank-1">13,511.360</span> |
| Dromaeo regular expressions | <span class="benchmark-result benchmark-rank-1">71,958.764</span> | <span class="benchmark-result benchmark-rank-3">538,262.653</span> | <span class="benchmark-result benchmark-rank-5">1,873,094.293</span> | <span class="benchmark-result benchmark-rank-4">727,779.264</span> | <span class="benchmark-result benchmark-rank-2">105,206.432</span> |
| Dromaeo object strings | <span class="benchmark-result benchmark-rank-2">38,511.691</span> | <span class="benchmark-result benchmark-rank-4">143,285.835</span> | <span class="benchmark-result benchmark-rank-3">55,635.516</span> | <span class="benchmark-result benchmark-rank-5">176,114.896</span> | <span class="benchmark-result benchmark-rank-1">5,837.141</span> |
| Dromaeo Base64 strings | <span class="benchmark-result benchmark-rank-2">16,717.857</span> | <span class="benchmark-result benchmark-rank-4">32,092.329</span> | <span class="benchmark-result benchmark-rank-3">30,042.911</span> | <span class="benchmark-result benchmark-rank-5">35,415.762</span> | <span class="benchmark-result benchmark-rank-1">1,697.677</span> |
| Stopwatch | <span class="benchmark-result benchmark-rank-3">85,204.309</span> | <span class="benchmark-result benchmark-rank-5">209,122.986</span> | <span class="benchmark-result benchmark-rank-4">155,504.922</span> | <span class="benchmark-result benchmark-rank-2">56,498.545</span> | <span class="benchmark-result benchmark-rank-1">14,161.990</span> |

The gap between the two ends is intentional. A prepared interpreter avoids the
compilation and context setup that dominate very small programs. An optimizing
JIT pays those costs to accelerate sufficiently hot loops.

## Script-to-host interop results

Embedded scripts often spend more time exchanging data with the host than
performing pure JavaScript computation. The interop suite runs identical
scripts that call methods, access properties, pass strings, and traverse a host
`int[]`.

ClearScript appears twice: its ordinary reflection-based host object and its
lower-overhead FastProxy API, where every exposed member is explicitly
registered. Okojo is absent because the benchmarked version has no public way
to enable CLR access.

Mean time is shown in microseconds. Lower is better. The same rank scale is
used here; Jint and NiL.JS share the fastest shade for collection traversal
because BenchmarkDotNet reports them as statistically tied.

| Workload | Jint | NiL.JS | YantraJS | V8 FastProxy | V8 reflection |
| --- | ---: | ---: | ---: | ---: | ---: |
| Collection traversal | <span class="benchmark-result benchmark-rank-1">1,251.0</span> | <span class="benchmark-result benchmark-rank-1">1,242.7</span> | <span class="benchmark-result benchmark-rank-3">3,685.3</span> | <span class="benchmark-result benchmark-rank-4">8,779.5</span> | <span class="benchmark-result benchmark-rank-5">11,691.8</span> |
| Method calls | <span class="benchmark-result benchmark-rank-2">1,440.0</span> | <span class="benchmark-result benchmark-rank-1">1,172.2</span> | <span class="benchmark-result benchmark-rank-3">2,102.4</span> | <span class="benchmark-result benchmark-rank-4">5,457.7</span> | <span class="benchmark-result benchmark-rank-5">16,108.5</span> |
| Property access | <span class="benchmark-result benchmark-rank-2">1,489.8</span> | <span class="benchmark-result benchmark-rank-3">1,702.0</span> | <span class="benchmark-result benchmark-rank-1">1,451.9</span> | <span class="benchmark-result benchmark-rank-4">5,111.8</span> | <span class="benchmark-result benchmark-rank-5">12,875.7</span> |
| String passing | <span class="benchmark-result benchmark-rank-1">522.4</span> | <span class="benchmark-result benchmark-rank-3">769.8</span> | <span class="benchmark-result benchmark-rank-2">577.5</span> | <span class="benchmark-result benchmark-rank-4">2,961.1</span> | <span class="benchmark-result benchmark-rank-5">5,263.2</span> |

Jint and NiL.JS run host calls inside the managed process. ClearScript must
marshal each interaction across the managed/native boundary. FastProxy reduces
that cost substantially, but requires an explicit host-object projection.

## Methodology and limits

These results use Jint 4.16.0, NiL.JS 2.6.1722, Okojo
0.1.2-preview.1, YantraJS.Core 1.2.422, and Microsoft.ClearScript.V8
7.5.1. They were collected in one BenchmarkDotNet session on .NET 10.0.11 and
an AMD Ryzen 9 5950X. The report was last updated on August 13, 2026.

All script lanes:

- Run in global strict mode.
- Create a fresh engine or context for every operation.
- Use the same script source for a workload.
- Run together on the same otherwise-idle machine.

Managed allocation figures cannot include V8's native heap, and V8 performs
background compilation and garbage collection. Compare times within a table;
absolute numbers from different sessions or machines are not directly
comparable.

The repository report contains standard deviations, ranks, managed allocation,
engine setup details, historical results, and reproduction commands. Read the
[complete benchmark report](https://github.com/sebastienros/jint/blob/main/Jint.Benchmark/README.md)
before making an engine decision, then benchmark the production workload.

For optimizing Jint itself, continue with [Performance](./performance.md).
