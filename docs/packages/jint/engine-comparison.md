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

Mean time is shown in microseconds. Lower is better; the fastest lane for each
workload is **bold**.

| Workload | Jint prepared | NiL.JS | Okojo prepared | YantraJS | V8 compiled |
| --- | ---: | ---: | ---: | ---: | ---: |
| Minimal script | **1.094** | 2.883 | 1,154.071 | 126.579 | 379.509 |
| Expression evaluation | **4.651** | 25.992 | 1,437.314 | 129.352 | 374.891 |
| LINQ-style JavaScript | **68.403** | 3,865.936 | 5,912.862 | 316.724 | 462.290 |
| Dromaeo core evaluation | **886.665** | 1,382.077 | 6,731.548 | 4,839.609 | 933.105 |
| Array stress | 2,212.058 | 4,845.159 | 5,546.651 | 2,815.214 | **2,035.717** |
| Dromaeo 3D cube | 4,607.760 | 7,095.742 | 6,826.450 | 2,299.281 | **1,361.995** |
| JSON parsing | 16,243.839 | 126,595.546 | 24,871.608 | 24,030.128 | **7,465.707** |
| Dromaeo object arrays | 14,574.726 | 52,137.758 | 40,069.776 | 26,468.928 | **13,511.360** |
| Dromaeo regular expressions | **71,958.764** | 538,262.653 | 1,873,094.293 | 727,779.264 | 105,206.432 |
| Dromaeo object strings | 38,511.691 | 143,285.835 | 55,635.516 | 176,114.896 | **5,837.141** |
| Dromaeo Base64 strings | 16,717.857 | 32,092.329 | 30,042.911 | 35,415.762 | **1,697.677** |
| Stopwatch | 85,204.309 | 209,122.986 | 155,504.922 | 56,498.545 | **14,161.990** |

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

Mean time is shown in microseconds. Lower is better.

| Workload | Jint | NiL.JS | YantraJS | V8 FastProxy | V8 reflection |
| --- | ---: | ---: | ---: | ---: | ---: |
| Collection traversal | 1,251.0 | **1,242.7** | 3,685.3 | 8,779.5 | 11,691.8 |
| Method calls | 1,440.0 | **1,172.2** | 2,102.4 | 5,457.7 | 16,108.5 |
| Property access | 1,489.8 | 1,702.0 | **1,451.9** | 5,111.8 | 12,875.7 |
| String passing | **522.4** | 769.8 | 577.5 | 2,961.1 | 5,263.2 |

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
