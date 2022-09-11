To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2022-09-11

* Jint main
* Jurassic 3.2.6
* NiL.JS 2.5.1600
* YantraJS.Core 1.2.50


``` ini

BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.457)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=6.0.400
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT AVX2


```
|            Method |             FileName |             Mean |         StdDev | Rank |     Allocated |
|------------------ |--------------------- |-----------------:|---------------:|-----:|--------------:|
|              **Jint** |         **array-stress** |    **11,379.224 μs** |     **15.0971 μs** |    **4** |     **8535395 B** |
| Jint_ParsedScript |         array-stress |    11,074.970 μs |     18.4598 μs |    3 |     8508563 B |
|          Jurassic |         array-stress |    10,979.843 μs |      8.6910 μs |    3 |    11926434 B |
|             NilJS |         array-stress |     5,642.704 μs |      4.1736 μs |    2 |    14746566 B |
|          YantraJS |         array-stress |     5,496.992 μs |     23.4065 μs |    1 |     6318056 B |
|                   |                      |                  |                |      |               |
|              **Jint** |      **dromaeo-3d-cube** |    **24,146.511 μs** |     **26.1510 μs** |    **4** |     **7259082 B** |
| Jint_ParsedScript |      dromaeo-3d-cube |    23,483.111 μs |     32.3476 μs |    3 |     6952138 B |
|          Jurassic |      dromaeo-3d-cube |    39,587.090 μs |     61.9786 μs |    5 |    10926049 B |
|             NilJS |      dromaeo-3d-cube |     9,424.855 μs |     14.3491 μs |    2 |     4240315 B |
|          YantraJS |      dromaeo-3d-cube |     4,912.535 μs |     21.6984 μs |    1 |     8786605 B |
|                   |                      |                  |                |      |               |
|              **Jint** |    **dromaeo-core-eval** |     **5,728.484 μs** |      **9.5477 μs** |    **2** |      **355438 B** |
| Jint_ParsedScript |    dromaeo-core-eval |     5,855.588 μs |     11.6084 μs |    3 |      335590 B |
|          Jurassic |    dromaeo-core-eval |    13,536.638 μs |      9.6263 μs |    5 |     2971094 B |
|             NilJS |    dromaeo-core-eval |     2,227.991 μs |      6.6451 μs |    1 |     1636803 B |
|          YantraJS |    dromaeo-core-eval |     8,280.202 μs |     47.5025 μs |    4 |    37131592 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **dromaeo-object-array** |    **77,560.010 μs** |    **232.4946 μs** |    **4** |   **108722570 B** |
| Jint_ParsedScript | dromaeo-object-array |    77,566.594 μs |    301.2466 μs |    4 |   108677466 B |
|          Jurassic | dromaeo-object-array |    41,083.167 μs |     50.7771 μs |    1 |    26433962 B |
|             NilJS | dromaeo-object-array |    57,373.831 μs |    858.8375 μs |    3 |   184600105 B |
|          YantraJS | dromaeo-object-array |    50,393.076 μs |    533.5444 μs |    2 |    24735072 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)egexp [21]** |   **298,896.106 μs** |  **8,135.1484 μs** |    **1** |   **242978848 B** |
| Jint_ParsedScript | droma(...)egexp [21] |   302,652.271 μs |  7,025.5037 μs |    1 |   243414424 B |
|          Jurassic | droma(...)egexp [21] |   654,490.231 μs | 10,258.3833 μs |    2 |   846502528 B |
|             NilJS | droma(...)egexp [21] |   674,164.900 μs |  6,095.0307 μs |    3 |   799695424 B |
|          YantraJS | droma(...)egexp [21] | 1,006,796.520 μs | 10,505.9810 μs |    4 |   966405696 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)tring [21]** |   **525,297.081 μs** | **21,438.2488 μs** |    **2** |  **1363376760 B** |
| Jint_ParsedScript | droma(...)tring [21] |   526,864.316 μs | 17,744.2228 μs |    2 |  1363452752 B |
|          Jurassic | droma(...)tring [21] |   547,799.136 μs | 17,846.3415 μs |    3 |  1492885824 B |
|             NilJS | droma(...)tring [21] |   394,707.218 μs | 14,726.4115 μs |    1 |  1418391032 B |
|          YantraJS | droma(...)tring [21] | 2,310,121.450 μs | 68,560.6400 μs |    4 | 16092741168 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)ase64 [21]** |    **67,005.288 μs** |     **64.0066 μs** |    **4** |     **8051766 B** |
| Jint_ParsedScript | droma(...)ase64 [21] |    65,846.243 μs |    120.6587 μs |    3 |     7953457 B |
|          Jurassic | droma(...)ase64 [21] |    70,798.359 μs |    238.4890 μs |    5 |    76105352 B |
|             NilJS | droma(...)ase64 [21] |    40,621.460 μs |     82.0179 μs |    1 |    23963164 B |
|          YantraJS | droma(...)ase64 [21] |    42,127.730 μs |    301.0910 μs |    2 |   778590774 B |
|                   |                      |                  |                |      |               |
|              **Jint** |           **evaluation** |        **28.213 μs** |      **0.1590 μs** |    **2** |       **35232 B** |
| Jint_ParsedScript |           evaluation |        11.793 μs |      0.0203 μs |    1 |       25504 B |
|          Jurassic |           evaluation |     1,277.875 μs |      3.9611 μs |    5 |      430506 B |
|             NilJS |           evaluation |        48.449 μs |      0.1407 μs |    3 |       23792 B |
|          YantraJS |           evaluation |       179.522 μs |      0.6104 μs |    4 |      178518 B |
|                   |                      |                  |                |      |               |
|              **Jint** |              **linq-js** |     **1,806.243 μs** |      **1.2092 μs** |    **3** |     **1300129 B** |
| Jint_ParsedScript |              linq-js |        86.629 μs |      0.0921 μs |    1 |      210256 B |
|          Jurassic |              linq-js |    37,427.618 μs |    606.2405 μs |    4 |     9538437 B |
|             NilJS |              linq-js |               NA |             NA |    ? |             - |
|          YantraJS |              linq-js |       393.218 μs |      1.1641 μs |    2 |      490377 B |
|                   |                      |                  |                |      |               |
|              **Jint** |              **minimal** |         **5.423 μs** |      **0.0044 μs** |    **3** |       **14248 B** |
| Jint_ParsedScript |              minimal |         3.650 μs |      0.0062 μs |    1 |       12752 B |
|          Jurassic |              minimal |       237.757 μs |      0.3432 μs |    5 |      395506 B |
|             NilJS |              minimal |         5.119 μs |      0.0149 μs |    2 |        4816 B |
|          YantraJS |              minimal |       174.715 μs |      0.4206 μs |    4 |      174671 B |
|                   |                      |                  |                |      |               |
|              **Jint** |            **stopwatch** |   **384,627.940 μs** |  **1,009.2949 μs** |    **4** |    **38904816 B** |
| Jint_ParsedScript |            stopwatch |   386,851.529 μs |  1,015.5684 μs |    4 |    38875776 B |
|          Jurassic |            stopwatch |   206,273.171 μs |    562.2965 μs |    2 |   160703632 B |
|             NilJS |            stopwatch |   243,991.748 μs |  2,371.4106 μs |    3 |    76377896 B |
|          YantraJS |            stopwatch |    81,158.826 μs |    214.1868 μs |    1 |   259048157 B |

Benchmarks with issues:
  EngineComparisonBenchmark.NilJS: DefaultJob [FileName=linq-js]
