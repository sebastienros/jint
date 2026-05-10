To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2026-05-10

* Jint 4.9.0
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.344

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method            | FileName             | Mean             | StdDev         | Rank | Allocated     |
|------------------ |--------------------- |-----------------:|---------------:|-----:|--------------:|
| Jint              | array-stress         |     3,600.142 μs |     10.2613 μs |    1 |    1940.45 KB |
| Jint_ParsedScript | array-stress         |     3,668.485 μs |      4.6862 μs |    1 |    1911.66 KB |
| NilJS             | array-stress         |     4,849.539 μs |      8.6038 μs |    2 |    4521.19 KB |
| Jurassic          | array-stress         |     9,150.877 μs |     37.3512 μs |    3 |   11644.85 KB |
| YantraJS          | array-stress         |    15,324.864 μs |    179.1992 μs |    4 |   87403.56 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | dromaeo-3d-cube      |     3,001.307 μs |     55.5474 μs |    1 |   12193.03 KB |
| NilJS             | dromaeo-3d-cube      |     6,191.135 μs |     23.1596 μs |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    12,085.109 μs |     44.5662 μs |    3 |    6072.38 KB |
| Jint              | dromaeo-3d-cube      |    12,391.892 μs |     56.5470 μs |    3 |    6375.01 KB |
| Jurassic          | dromaeo-3d-cube      |    55,098.604 μs |    316.3390 μs |    4 |   10654.72 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [22] |               NA |             NA |    ? |            NA |
| YantraJS          | droma(...)odern [22] |     2,909.481 μs |     21.1205 μs |    1 |   11937.58 KB |
| NilJS             | droma(...)odern [22] |     7,097.099 μs |     29.3895 μs |    2 |    5977.95 KB |
| Jint              | droma(...)odern [22] |    12,588.626 μs |     81.3668 μs |    3 |    6373.95 KB |
| Jint_ParsedScript | droma(...)odern [22] |    12,714.383 μs |     64.6725 μs |    3 |    6071.52 KB |
|                   |                      |                  |                |      |               |
| NilJS             | dromaeo-core-eval    |     1,228.232 μs |      4.2825 μs |    1 |     1577.1 KB |
| Jint              | dromaeo-core-eval    |     2,460.312 μs |      5.2240 μs |    2 |    1344.95 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     2,694.750 μs |      4.5889 μs |    3 |    1324.59 KB |
| YantraJS          | dromaeo-core-eval    |     4,543.888 μs |     56.7299 μs |    4 |   37524.12 KB |
| Jurassic          | dromaeo-core-eval    |    17,128.609 μs |     87.9014 μs |    5 |    2876.04 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [24] |               NA |             NA |    ? |            NA |
| NilJS             | droma(...)odern [24] |     1,516.158 μs |     15.0019 μs |    1 |    1575.94 KB |
| Jint_ParsedScript | droma(...)odern [24] |     2,472.925 μs |      4.7552 μs |    2 |    1324.26 KB |
| Jint              | droma(...)odern [24] |     2,475.032 μs |      4.6175 μs |    2 |     1343.9 KB |
| YantraJS          | droma(...)odern [24] |     4,905.856 μs |     71.2516 μs |    3 |   37524.23 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | dromaeo-object-array |    17,236.998 μs |    112.6718 μs |    1 |   10464.17 KB |
| Jint              | dromaeo-object-array |    18,464.348 μs |    227.6210 μs |    2 |   10512.56 KB |
| Jurassic          | dromaeo-object-array |    35,426.349 μs |    597.7225 μs |    3 |   25809.34 KB |
| NilJS             | dromaeo-object-array |    52,216.856 μs |    182.9681 μs |    4 |   17862.17 KB |
| YantraJS          | dromaeo-object-array |    65,702.997 μs |    164.6667 μs |    5 | 1267819.17 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [27] |               NA |             NA |    ? |            NA |
| Jint              | droma(...)odern [27] |    17,622.938 μs |     49.4907 μs |    1 |   10513.07 KB |
| Jint_ParsedScript | droma(...)odern [27] |    18,542.560 μs |     56.1619 μs |    2 |   10465.81 KB |
| NilJS             | droma(...)odern [27] |    52,170.723 μs |     93.7993 μs |    3 |   17863.19 KB |
| YantraJS          | droma(...)odern [27] |    64,772.950 μs |  1,022.4652 μs |    4 | 1271610.92 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | droma(...)egexp [21] |    96,606.269 μs |  4,319.6792 μs |    1 |  165386.35 KB |
| Jint              | droma(...)egexp [21] |   134,688.638 μs |  6,891.0792 μs |    2 |  165485.78 KB |
| NilJS             | droma(...)egexp [21] |   527,754.775 μs |  5,309.3810 μs |    3 |  767343.52 KB |
| Jurassic          | droma(...)egexp [21] |   678,034.615 μs | 18,454.5925 μs |    4 |  824223.65 KB |
| YantraJS          | droma(...)egexp [21] | 1,060,446.187 μs | 13,132.4548 μs |    5 | 1173206.74 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | droma(...)odern [28] |   103,489.002 μs |  4,444.6151 μs |    1 |  165159.68 KB |
| Jint              | droma(...)odern [28] |   131,367.144 μs |  5,159.7856 μs |    2 |   164500.1 KB |
| NilJS             | droma(...)odern [28] |   530,962.207 μs |  6,598.8183 μs |    3 |  766294.78 KB |
| YantraJS          | droma(...)odern [28] | 1,044,027.193 μs | 16,352.4393 μs |    4 | 1180047.69 KB |
|                   |                      |                  |                |      |               |
| NilJS             | droma(...)tring [21] |   127,806.654 μs |  2,138.5028 μs |    1 | 1355022.65 KB |
| Jint_ParsedScript | droma(...)tring [21] |   154,092.489 μs |  5,459.0404 μs |    2 | 1304213.01 KB |
| Jint              | droma(...)tring [21] |   155,115.708 μs |  5,401.7810 μs |    2 | 1304226.13 KB |
| YantraJS          | droma(...)tring [21] |   172,689.697 μs |  5,016.3322 μs |    3 | 1680455.58 KB |
| Jurassic          | droma(...)tring [21] |   204,885.390 μs |  5,289.4219 μs |    4 | 1430454.33 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| NilJS             | droma(...)odern [28] |   130,222.402 μs |  1,872.0072 μs |    1 | 1355026.97 KB |
| Jint              | droma(...)odern [28] |   154,706.676 μs |  5,950.7825 μs |    2 | 1304297.89 KB |
| Jint_ParsedScript | droma(...)odern [28] |   157,241.972 μs |  4,572.7657 μs |    2 | 1304120.01 KB |
| YantraJS          | droma(...)odern [28] |   170,594.524 μs |  6,710.2495 μs |    3 | 1683765.32 KB |
|                   |                      |                  |                |      |               |
| NilJS             | droma(...)ase64 [21] |    25,910.370 μs |    205.0569 μs |    1 |   19588.63 KB |
| Jint              | droma(...)ase64 [21] |    26,864.303 μs |     25.4021 μs |    2 |     3773.4 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    27,136.288 μs |     89.7848 μs |    2 |    3675.61 KB |
| YantraJS          | droma(...)ase64 [21] |    43,033.686 μs |  1,427.3137 μs |    3 |  766430.93 KB |
| Jurassic          | droma(...)ase64 [21] |    47,077.635 μs |    260.7801 μs |    4 |   73290.42 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| Jint              | droma(...)odern [28] |    32,706.717 μs |    114.1965 μs |    1 |    3773.89 KB |
| Jint_ParsedScript | droma(...)odern [28] |    32,816.943 μs |    137.9606 μs |    1 |    3675.69 KB |
| NilJS             | droma(...)odern [28] |    32,834.591 μs |    536.0987 μs |    1 |   31360.22 KB |
| YantraJS          | droma(...)odern [28] |    39,165.439 μs |    660.0182 μs |    2 |  767646.71 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | evaluation           |         4.748 μs |      0.0275 μs |    1 |       23.3 KB |
| Jint              | evaluation           |        15.097 μs |      0.1062 μs |    2 |      33.94 KB |
| NilJS             | evaluation           |        25.537 μs |      0.0802 μs |    3 |      22.36 KB |
| YantraJS          | evaluation           |       155.695 μs |      1.9187 μs |    4 |    1185.61 KB |
| Jurassic          | evaluation           |     2,109.650 μs |      7.3706 μs |    5 |     418.81 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | evaluation-modern    |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern    |         4.831 μs |      0.0088 μs |    1 |      22.75 KB |
| Jint              | evaluation-modern    |        14.867 μs |      0.0990 μs |    2 |      33.87 KB |
| NilJS             | evaluation-modern    |        26.888 μs |      0.1158 μs |    3 |      22.35 KB |
| YantraJS          | evaluation-modern    |       159.920 μs |      2.7576 μs |    4 |    1185.59 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | linq-js              |        55.871 μs |      0.1871 μs |    1 |     186.83 KB |
| YantraJS          | linq-js              |       344.355 μs |      5.0156 μs |    2 |    1728.82 KB |
| Jint              | linq-js              |     1,204.476 μs |     18.8003 μs |    3 |    1286.02 KB |
| NilJS             | linq-js              |     3,968.025 μs |     15.6721 μs |    4 |    2739.46 KB |
| Jurassic          | linq-js              |    36,246.917 μs |    629.9089 μs |    5 |    9102.27 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | minimal              |         1.648 μs |      0.0112 μs |    1 |      15.27 KB |
| Jint              | minimal              |         2.737 μs |      0.0206 μs |    2 |      17.19 KB |
| NilJS             | minimal              |         2.770 μs |      0.0112 μs |    2 |       4.51 KB |
| YantraJS          | minimal              |       153.309 μs |      2.1040 μs |    3 |    1177.01 KB |
| Jurassic          | minimal              |     2,304.920 μs |      9.9848 μs |    4 |     385.19 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | stopwatch            |    63,440.637 μs |    164.7934 μs |    1 |  244895.96 KB |
| NilJS             | stopwatch            |   132,099.170 μs |    496.5910 μs |    2 |   94876.49 KB |
| Jurassic          | stopwatch            |   142,392.662 μs |  1,537.6640 μs |    3 |  156932.58 KB |
| Jint              | stopwatch            |   194,933.185 μs |    555.8906 μs |    4 |   39418.89 KB |
| Jint_ParsedScript | stopwatch            |   210,552.696 μs |    381.3792 μs |    5 |   39387.13 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | stopwatch-modern     |    64,587.303 μs |  1,536.9192 μs |    1 |  263273.99 KB |
| Jurassic          | stopwatch-modern     |   148,260.623 μs |    837.0448 μs |    2 |  288625.25 KB |
| Jint              | stopwatch-modern     |   237,843.453 μs |    560.7782 μs |    3 |   39635.13 KB |
| NilJS             | stopwatch-modern     |   239,223.904 μs |  1,459.7702 μs |    3 |  324502.66 KB |
| Jint_ParsedScript | stopwatch-modern     |   250,468.370 μs |  1,134.9137 μs |    4 |   39602.85 KB |

Benchmarks with issues:
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [22]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [24]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [27]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=evaluation-modern]
