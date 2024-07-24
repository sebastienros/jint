To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2024-07-24

* Jint 4.0.0
* Jurassic 3.2.7
* NiL.JS 2.5.1684
* YantraJS.Core 1.2.209

```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.3880/23H2/2023Update/SunValley3)
12th Gen Intel Core i9-12900H, 1 CPU, 20 logical and 14 physical cores
.NET SDK 8.0.303
  [Host]     : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2


```
| Method            | FileName             | Mean             | StdDev         | Rank | Allocated      |
|------------------ |--------------------- |-----------------:|---------------:|-----:|---------------:|
| NilJS             | array-stress         |     5,058.353 μs |    114.0266 μs |    1 |     4533.76 KB |
| Jint_ParsedScript | array-stress         |     5,420.466 μs |    134.9755 μs |    2 |     6711.51 KB |
| Jint              | array-stress         |     5,563.246 μs |    138.3406 μs |    3 |     6745.77 KB |
| YantraJS          | array-stress         |     7,909.943 μs |     27.2683 μs |    4 |     8070.78 KB |
| Jurassic          | array-stress         |     8,655.221 μs |    121.3367 μs |    5 |    11647.78 KB |
|                   |                      |                  |                |      |                |
| YantraJS          | dromaeo-3d-cube      |     4,648.068 μs |    105.5598 μs |    1 |    11411.07 KB |
| NilJS             | dromaeo-3d-cube      |     6,260.663 μs |     74.0350 μs |    2 |     4693.22 KB |
| Jint              | dromaeo-3d-cube      |    11,904.695 μs |    211.4104 μs |    3 |     6215.87 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    11,943.593 μs |    108.3016 μs |    3 |     5866.83 KB |
| Jurassic          | dromaeo-3d-cube      |    42,077.764 μs |    457.8769 μs |    4 |    10669.72 KB |
|                   |                      |                  |                |      |                |
| NilJS             | dromaeo-core-eval    |     1,174.272 μs |      7.6441 μs |    1 |     1598.62 KB |
| Jint              | dromaeo-core-eval    |     2,144.905 μs |     28.9473 μs |    2 |       352.6 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     2,267.380 μs |     22.3290 μs |    3 |      329.44 KB |
| YantraJS          | dromaeo-core-eval    |     4,655.031 μs |     36.9158 μs |    4 |    36526.95 KB |
| Jurassic          | dromaeo-core-eval    |     7,720.964 μs |    142.1918 μs |    5 |     2884.11 KB |
|                   |                      |                  |                |      |                |
| Jint              | dromaeo-object-array |    32,741.598 μs |    495.4899 μs |    1 |    96290.31 KB |
| Jint_ParsedScript | dromaeo-object-array |    34,825.011 μs |    975.6224 μs |    2 |    96235.39 KB |
| Jurassic          | dromaeo-object-array |    36,264.011 μs |    601.6295 μs |    3 |     25813.2 KB |
| YantraJS          | dromaeo-object-array |    49,509.429 μs |  1,036.5551 μs |    4 |    29477.83 KB |
| NilJS             | dromaeo-object-array |    57,281.072 μs |    759.3914 μs |    5 |    17697.94 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | droma(...)egexp [21] |   117,033.368 μs |  7,497.3330 μs |    1 |   150094.35 KB |
| Jint              | droma(...)egexp [21] |   152,194.468 μs | 12,040.6197 μs |    2 |   148631.36 KB |
| NilJS             | droma(...)egexp [21] |   632,380.005 μs | 20,903.7357 μs |    3 |    768591.6 KB |
| Jurassic          | droma(...)egexp [21] |   738,929.771 μs | 19,097.1436 μs |    4 |   828685.81 KB |
| YantraJS          | droma(...)egexp [21] | 1,165,503.516 μs | 42,685.5570 μs |    5 |  1154595.89 KB |
|                   |                      |                  |                |      |                |
| Jint              | droma(...)tring [21] |   218,822.760 μs | 11,442.4202 μs |    1 |  1315974.43 KB |
| Jint_ParsedScript | droma(...)tring [21] |   226,004.225 μs |  6,621.1287 μs |    2 |  1315741.79 KB |
| Jurassic          | droma(...)tring [21] |   273,321.750 μs |  9,259.3369 μs |    3 |  1458042.21 KB |
| NilJS             | droma(...)tring [21] |   284,101.842 μs | 15,429.3717 μs |    4 |  1378087.55 KB |
| YantraJS          | droma(...)tring [21] | 1,194,631.607 μs | 17,181.5645 μs |    5 | 15729821.05 KB |
|                   |                      |                  |                |      |                |
| Jint              | droma(...)ase64 [21] |    24,972.089 μs |    251.3268 μs |    1 |     2418.45 KB |
| NilJS             | droma(...)ase64 [21] |    26,247.190 μs |    355.1010 μs |    2 |    19604.15 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    26,399.276 μs |    377.1769 μs |    2 |     2309.85 KB |
| YantraJS          | droma(...)ase64 [21] |    43,911.183 μs |  1,041.9280 μs |    3 |    760381.3 KB |
| Jurassic          | droma(...)ase64 [21] |    53,481.565 μs |    955.2119 μs |    4 |    73295.01 KB |
|                   |                      |                  |                |      |                |
| YantraJS          | evaluation           |               NA |             NA |    ? |             NA |
| Jint_ParsedScript | evaluation           |         4.579 μs |      0.0947 μs |    1 |       22.18 KB |
| Jint              | evaluation           |        12.561 μs |      0.2948 μs |    2 |       33.77 KB |
| NilJS             | evaluation           |        29.038 μs |      0.3273 μs |    3 |       23.47 KB |
| Jurassic          | evaluation           |     1,180.265 μs |     12.9881 μs |    4 |      420.35 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | linq-js              |        53.600 μs |      0.6773 μs |    1 |      175.92 KB |
| YantraJS          | linq-js              |       330.608 μs |      2.5773 μs |    2 |     1442.59 KB |
| Jint              | linq-js              |     1,165.992 μs |     24.5881 μs |    3 |     1292.41 KB |
| NilJS             | linq-js              |     5,972.618 μs |     94.8720 μs |    4 |      4121.1 KB |
| Jurassic          | linq-js              |    31,349.404 μs |    331.6671 μs |    5 |     9254.29 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | minimal              |         1.548 μs |      0.0246 μs |    1 |        14.3 KB |
| Jint              | minimal              |         2.545 μs |      0.0398 μs |    2 |       16.35 KB |
| NilJS             | minimal              |         3.033 μs |      0.0436 μs |    3 |        4.81 KB |
| YantraJS          | minimal              |       108.184 μs |      1.4088 μs |    4 |      916.78 KB |
| Jurassic          | minimal              |       190.723 μs |      2.1817 μs |    5 |      386.23 KB |
|                   |                      |                  |                |      |                |
| YantraJS          | stopwatch            |    65,630.452 μs |    644.4614 μs |    1 |   224268.21 KB |
| NilJS             | stopwatch            |   122,351.600 μs |  1,321.9403 μs |    2 |    97360.68 KB |
| Jurassic          | stopwatch            |   135,387.838 μs |  1,735.1390 μs |    3 |   156935.93 KB |
| Jint              | stopwatch            |   188,976.293 μs |  2,055.0970 μs |    4 |    53035.25 KB |
| Jint_ParsedScript | stopwatch            |   196,716.724 μs |  2,696.0127 μs |    5 |    52998.85 KB |

Benchmarks with issues:
  EngineComparisonBenchmark.YantraJS: DefaultJob [FileName=evaluation]
