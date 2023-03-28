To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2023-03-26

* Jint main
* Jurassic 3.2.6
* NiL.JS 2.5.1650
* YantraJS.Core 1.2.117


``` ini

BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1483/22H2/2022Update/SunValley2)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=7.0.202
  [Host]     : .NET 6.0.15 (6.0.1523.11507), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.15 (6.0.1523.11507), X64 RyuJIT AVX2


```
|            Method |             FileName |             Mean |         StdDev | Rank |     Allocated |
|------------------ |--------------------- |-----------------:|---------------:|-----:|--------------:|
|              **Jint** |         **array-stress** |    **10,799.249 μs** |     **24.4948 μs** |    **4** |     **7473326 B** |
| Jint_ParsedScript |         array-stress |    10,568.342 μs |     44.6133 μs |    3 |     7446494 B |
|          Jurassic |         array-stress |    11,280.104 μs |     27.9486 μs |    5 |    11926463 B |
|             NilJS |         array-stress |     5,600.761 μs |     74.2339 μs |    2 |     4241527 B |
|          YantraJS |         array-stress |     4,891.973 μs |     18.8130 μs |    1 |     6518316 B |
|                   |                      |                  |                |      |               |
|              **Jint** |      **dromaeo-3d-cube** |    **25,234.322 μs** |     **28.3103 μs** |    **4** |     **6307004 B** |
| Jint_ParsedScript |      dromaeo-3d-cube |    24,227.022 μs |     30.4230 μs |    3 |     6000208 B |
|          Jurassic |      dromaeo-3d-cube |    40,155.874 μs |     83.9446 μs |    5 |    10925739 B |
|             NilJS |      dromaeo-3d-cube |     9,203.608 μs |     18.2137 μs |    2 |     4671638 B |
|          YantraJS |      dromaeo-3d-cube |     4,478.599 μs |     35.6857 μs |    1 |     8885293 B |
|                   |                      |                  |                |      |               |
|              **Jint** |    **dromaeo-core-eval** |     **5,496.472 μs** |     **20.8861 μs** |    **2** |      **359439 B** |
| Jint_ParsedScript |    dromaeo-core-eval |     5,555.814 μs |     29.8913 μs |    2 |      339647 B |
|          Jurassic |    dromaeo-core-eval |    13,739.640 μs |     52.5257 μs |    4 |     2971062 B |
|             NilJS |    dromaeo-core-eval |     2,099.576 μs |     13.3939 μs |    1 |     1637011 B |
|          YantraJS |    dromaeo-core-eval |     8,068.664 μs |     41.8267 μs |    3 |    37131162 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **dromaeo-object-array** |    **68,157.350 μs** |    **106.5991 μs** |    **4** |   **103974805 B** |
| Jint_ParsedScript | dromaeo-object-array |    70,379.819 μs |    412.9971 μs |    5 |   103928047 B |
|          Jurassic | dromaeo-object-array |    41,917.036 μs |    147.6689 μs |    1 |    26433545 B |
|             NilJS | dromaeo-object-array |    50,941.149 μs |    116.1891 μs |    3 |    16518497 B |
|          YantraJS | dromaeo-object-array |    43,952.908 μs |     52.7966 μs |    2 |    25538081 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)egexp [21]** |   **202,513.639 μs** |  **6,280.4683 μs** |    **2** |   **169538072 B** |
| Jint_ParsedScript | droma(...)egexp [21] |   194,203.576 μs |  2,648.5440 μs |    1 |   176401621 B |
|          Jurassic | droma(...)egexp [21] |   673,099.103 μs | 19,449.6381 μs |    4 |   845159056 B |
|             NilJS | droma(...)egexp [21] |   498,404.131 μs |  9,389.0173 μs |    3 |   785342384 B |
|          YantraJS | droma(...)egexp [21] | 1,021,021.213 μs | 10,171.5386 μs |    5 |   965363992 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)tring [21]** |   **513,139.116 μs** | **21,598.0545 μs** |    **2** |  **1353627632 B** |
| Jint_ParsedScript | droma(...)tring [21] |   598,346.054 μs | 62,241.8961 μs |    4 |  1353603760 B |
|          Jurassic | droma(...)tring [21] |   547,882.282 μs | 15,131.8012 μs |    3 |  1492956848 B |
|             NilJS | droma(...)tring [21] |   380,880.402 μs | 17,953.6040 μs |    1 |  1410785672 B |
|          YantraJS | droma(...)tring [21] | 3,146,801.586 μs | 75,534.4740 μs |    5 | 16097426568 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)ase64 [21]** |    **54,972.605 μs** |    **123.8465 μs** |    **3** |     **6764151 B** |
| Jint_ParsedScript | droma(...)ase64 [21] |    53,534.261 μs |    272.9451 μs |    2 |     6665682 B |
|          Jurassic | droma(...)ase64 [21] |    69,111.283 μs |    166.1184 μs |    4 |    76105181 B |
|             NilJS | droma(...)ase64 [21] |    40,131.392 μs |    385.5791 μs |    1 |    20074818 B |
|          YantraJS | droma(...)ase64 [21] |   108,860.979 μs |    697.7800 μs |    5 |   778591469 B |
|                   |                      |                  |                |      |               |
|              **Jint** |           **evaluation** |        **28.773 μs** |      **0.0494 μs** |    **2** |       **36792 B** |
| Jint_ParsedScript |           evaluation |        11.296 μs |      0.0295 μs |    1 |       27072 B |
|          Jurassic |           evaluation |     1,291.843 μs |      3.3295 μs |    5 |      430510 B |
|             NilJS |           evaluation |        45.473 μs |      0.1385 μs |    3 |       24032 B |
|          YantraJS |           evaluation |       120.995 μs |      0.3959 μs |    4 |      177876 B |
|                   |                      |                  |                |      |               |
|              **Jint** |              **linq-js** |     **1,774.118 μs** |      **8.6162 μs** |    **2** |     **1303058 B** |
| Jint_ParsedScript |              linq-js |        90.567 μs |      0.2234 μs |    1 |      213218 B |
|          Jurassic |              linq-js |    39,199.309 μs |    614.5447 μs |    4 |     9525761 B |
|             NilJS |              linq-js |     7,630.382 μs |     11.3679 μs |    3 |     4226480 B |
|          YantraJS |              linq-js |               NA |             NA |    ? |             - |
|                   |                      |                  |                |      |               |
|              **Jint** |              **minimal** |         **4.937 μs** |      **0.0103 μs** |    **3** |       **14664 B** |
| Jint_ParsedScript |              minimal |         3.124 μs |      0.0123 μs |    1 |       13168 B |
|          Jurassic |              minimal |       238.384 μs |      0.6775 μs |    5 |      395506 B |
|             NilJS |              minimal |         4.751 μs |      0.0103 μs |    2 |        4928 B |
|          YantraJS |              minimal |       117.579 μs |      0.5920 μs |    4 |      173770 B |
|                   |                      |                  |                |      |               |
|              **Jint** |            **stopwatch** |   **368,697.936 μs** |  **1,043.7863 μs** |    **4** |    **52946080 B** |
| Jint_ParsedScript |            stopwatch |   408,149.533 μs |  1,052.2644 μs |    5 |    52912288 B |
|          Jurassic |            stopwatch |   211,731.362 μs |    544.2606 μs |    2 |   160704435 B |
|             NilJS |            stopwatch |   238,188.624 μs |    480.6546 μs |    3 |    76378157 B |
|          YantraJS |            stopwatch |    78,043.391 μs |    228.5660 μs |    1 |   264535415 B |

Benchmarks with issues:
EngineComparisonBenchmark.YantraJS: DefaultJob [FileName=linq-js]