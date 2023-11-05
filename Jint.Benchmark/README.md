To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2023-11-05

* Jint main
* Jurassic 3.2.7
* NiL.JS 2.5.1674
* YantraJS.Core 1.2.203

```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.23580.1000)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 8.0.100-rc.2.23502.2
  [Host]     : .NET 6.0.24 (6.0.2423.51814), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.24 (6.0.2423.51814), X64 RyuJIT AVX2


```
| Method            | FileName             | Mean             | StdDev          | Rank | Allocated      |
|------------------ |--------------------- |-----------------:|----------------:|-----:|---------------:|
| NilJS             | array-stress         |     7,618.410 μs |       9.6261 μs |    1 |     4533.76 KB |
| YantraJS          | array-stress         |     7,634.348 μs |     154.1204 μs |    1 |     8080.23 KB |
| Jint              | array-stress         |    10,189.119 μs |     100.0517 μs |    2 |     7112.02 KB |
| Jint_ParsedScript | array-stress         |    10,829.737 μs |      29.7168 μs |    3 |     7090.84 KB |
| Jurassic          | array-stress         |    13,478.434 μs |     134.6721 μs |    4 |    11646.94 KB |
|                   |                      |                  |                 |      |                |
| YantraJS          | dromaeo-3d-cube      |     6,903.873 μs |      29.0353 μs |    1 |    11426.85 KB |
| NilJS             | dromaeo-3d-cube      |    11,520.118 μs |      12.9132 μs |    2 |     4694.63 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    25,403.787 μs |      76.2460 μs |    3 |     5934.53 KB |
| Jint              | dromaeo-3d-cube      |    26,214.383 μs |      19.9661 μs |    4 |     6191.84 KB |
| Jurassic          | dromaeo-3d-cube      |    49,227.408 μs |     112.1937 μs |    5 |    10670.73 KB |
|                   |                      |                  |                 |      |                |
| NilJS             | dromaeo-core-eval    |     2,688.083 μs |       7.5997 μs |    1 |     1598.78 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     5,404.308 μs |      11.9911 μs |    2 |      333.82 KB |
| Jint              | dromaeo-core-eval    |     5,745.384 μs |      10.9482 μs |    3 |      350.86 KB |
| YantraJS          | dromaeo-core-eval    |    10,070.410 μs |      26.0609 μs |    4 |     70662.5 KB |
| Jurassic          | dromaeo-core-eval    |    12,145.734 μs |      44.3013 μs |    5 |     2884.85 KB |
|                   |                      |                  |                 |      |                |
| Jurassic          | dromaeo-object-array |    50,713.787 μs |     105.2239 μs |    1 |    25814.89 KB |
| YantraJS          | dromaeo-object-array |    63,932.521 μs |     830.2901 μs |    2 |    29485.54 KB |
| Jint              | dromaeo-object-array |    65,413.838 μs |     508.2830 μs |    3 |   100793.32 KB |
| Jint_ParsedScript | dromaeo-object-array |    66,591.951 μs |     140.1417 μs |    4 |   100754.12 KB |
| NilJS             | dromaeo-object-array |    75,914.511 μs |     126.7804 μs |    5 |    17698.17 KB |
|                   |                      |                  |                 |      |                |
| Jint              | droma(...)egexp [21] |   285,685.732 μs |   7,488.3067 μs |    1 |   170207.95 KB |
| Jint_ParsedScript | droma(...)egexp [21] |   289,626.408 μs |   1,896.8981 μs |    1 |   167049.52 KB |
| NilJS             | droma(...)egexp [21] |   614,280.120 μs |   7,612.4833 μs |    2 |   766193.08 KB |
| Jurassic          | droma(...)egexp [21] |   807,573.300 μs |  14,825.6620 μs |    3 |   825805.82 KB |
| YantraJS          | droma(...)egexp [21] | 1,229,769.600 μs |  16,340.3960 μs |    4 |  1254750.68 KB |
|                   |                      |                  |                 |      |                |
| NilJS             | droma(...)tring [21] |   426,125.000 μs |   7,684.4889 μs |    1 |  1377812.93 KB |
| Jint_ParsedScript | droma(...)tring [21] |   608,204.631 μs |  58,027.2350 μs |    2 |  1322143.68 KB |
| Jint              | droma(...)tring [21] |   617,734.935 μs |  39,573.9870 μs |    2 |  1322326.65 KB |
| Jurassic          | droma(...)tring [21] |   619,546.013 μs |   9,041.0940 μs |    2 |   1458038.6 KB |
| YantraJS          | droma(...)tring [21] | 4,197,768.766 μs | 343,801.2590 μs |    3 | 29822200.57 KB |
|                   |                      |                  |                 |      |                |
| NilJS             | droma(...)ase64 [21] |    49,243.377 μs |     108.4628 μs |    1 |     19604.8 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    59,677.164 μs |     177.0683 μs |    2 |     6722.79 KB |
| Jint              | droma(...)ase64 [21] |    63,301.808 μs |     318.1747 μs |    3 |     6806.58 KB |
| YantraJS          | droma(...)ase64 [21] |    78,283.727 μs |     991.5219 μs |    4 |   1492785.6 KB |
| Jurassic          | droma(...)ase64 [21] |    83,485.030 μs |     198.9519 μs |    5 |    74319.52 KB |
|                   |                      |                  |                 |      |                |
| Jint_ParsedScript | evaluation           |        13.500 μs |       0.0247 μs |    1 |       27.48 KB |
| Jint              | evaluation           |        33.419 μs |       0.0412 μs |    2 |       36.05 KB |
| NilJS             | evaluation           |        59.750 μs |       0.1541 μs |    3 |       23.47 KB |
| YantraJS          | evaluation           |       159.699 μs |       0.6218 μs |    4 |         931 KB |
| Jurassic          | evaluation           |     1,582.755 μs |       2.5897 μs |    5 |      420.41 KB |
|                   |                      |                  |                 |      |                |
| Jint_ParsedScript | linq-js              |       111.636 μs |       0.2424 μs |    1 |      226.52 KB |
| YantraJS          | linq-js              |       461.542 μs |       0.4869 μs |    2 |      1453.9 KB |
| Jint              | linq-js              |     2,208.746 μs |      12.8004 μs |    3 |      1276.1 KB |
| NilJS             | linq-js              |    10,450.782 μs |      73.1973 μs |    4 |     4127.79 KB |
| Jurassic          | linq-js              |    44,781.543 μs |     394.7200 μs |    5 |     9302.34 KB |
|                   |                      |                  |                 |      |                |
| Jint_ParsedScript | minimal              |         3.382 μs |       0.0078 μs |    1 |       12.99 KB |
| Jint              | minimal              |         5.334 μs |       0.0174 μs |    2 |       14.38 KB |
| NilJS             | minimal              |         5.891 μs |       0.0096 μs |    3 |        4.81 KB |
| YantraJS          | minimal              |       153.890 μs |       0.8023 μs |    4 |      925.48 KB |
| Jurassic          | minimal              |       294.178 μs |       0.3525 μs |    5 |      386.24 KB |
|                   |                      |                  |                 |      |                |
| YantraJS          | stopwatch            |   112,865.841 μs |     223.3235 μs |    1 |   224277.02 KB |
| Jurassic          | stopwatch            |   254,529.440 μs |     839.9853 μs |    2 |   156937.17 KB |
| NilJS             | stopwatch            |   297,948.830 μs |   1,345.9144 μs |    3 |     97363.1 KB |
| Jint_ParsedScript | stopwatch            |   394,136.840 μs |   3,882.0490 μs |    4 |    53015.98 KB |
| Jint              | stopwatch            |   457,053.953 μs |   1,630.9870 μs |    5 |    53045.23 KB |
