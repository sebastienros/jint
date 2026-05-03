To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2026-05-03

* Jint 4.8.0
* Jurassic 3.2.9
* NiL.JS 2.6.1721
* YantraJS.Core 1.2.334

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3


```
| Method            | FileName             | Mean             | StdDev          | Median           | Rank | Allocated     |
|------------------ |--------------------- |-----------------:|----------------:|-----------------:|-----:|--------------:|
| Jint              | array-stress         |     3,533.001 μs |       7.6367 μs |     3,535.903 μs |    1 |    1940.45 KB |
| Jint_ParsedScript | array-stress         |     3,651.231 μs |       5.8406 μs |     3,650.981 μs |    2 |    1911.66 KB |
| NilJS             | array-stress         |     4,707.643 μs |      14.4053 μs |     4,708.506 μs |    3 |    4521.19 KB |
| Jurassic          | array-stress         |     8,844.746 μs |      42.1863 μs |     8,836.739 μs |    4 |   11644.87 KB |
| YantraJS          | array-stress         |    15,037.398 μs |     478.6350 μs |    15,211.663 μs |    5 |   86559.47 KB |
|                   |                      |                  |                 |                  |      |               |
| YantraJS          | dromaeo-3d-cube      |     2,975.874 μs |      44.1024 μs |     2,966.238 μs |    1 |   10692.56 KB |
| NilJS             | dromaeo-3d-cube      |     5,951.302 μs |      14.9877 μs |     5,954.822 μs |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    11,886.489 μs |      64.7087 μs |    11,870.198 μs |    3 |    6072.38 KB |
| Jint              | dromaeo-3d-cube      |    12,438.565 μs |     103.4839 μs |    12,478.516 μs |    4 |    6375.01 KB |
| Jurassic          | dromaeo-3d-cube      |    53,881.945 μs |     210.5017 μs |    53,799.850 μs |    5 |   10654.72 KB |
|                   |                      |                  |                 |                  |      |               |
| Jurassic          | droma(...)odern [22] |               NA |              NA |               NA |    ? |            NA |
| YantraJS          | droma(...)odern [22] |     3,019.459 μs |      49.0287 μs |     3,007.408 μs |    1 |   10450.47 KB |
| NilJS             | droma(...)odern [22] |     7,563.750 μs |     147.1253 μs |     7,560.542 μs |    2 |    5977.95 KB |
| Jint_ParsedScript | droma(...)odern [22] |    12,688.439 μs |     243.1855 μs |    12,566.492 μs |    3 |    6071.52 KB |
| Jint              | droma(...)odern [22] |    12,886.968 μs |     205.4522 μs |    12,948.674 μs |    3 |    6373.95 KB |
|                   |                      |                  |                 |                  |      |               |
| NilJS             | dromaeo-core-eval    |     1,273.334 μs |      14.8007 μs |     1,273.958 μs |    1 |     1577.1 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     2,547.151 μs |      50.6741 μs |     2,530.190 μs |    2 |    1324.59 KB |
| Jint              | dromaeo-core-eval    |     2,653.478 μs |      82.1488 μs |     2,626.988 μs |    2 |    1344.95 KB |
| YantraJS          | dromaeo-core-eval    |     4,295.002 μs |      32.3982 μs |     4,296.717 μs |    3 |   36525.82 KB |
| Jurassic          | dromaeo-core-eval    |    18,121.533 μs |     321.6612 μs |    18,135.747 μs |    4 |    2876.04 KB |
|                   |                      |                  |                 |                  |      |               |
| Jurassic          | droma(...)odern [24] |               NA |              NA |               NA |    ? |            NA |
| NilJS             | droma(...)odern [24] |     1,561.127 μs |      18.4452 μs |     1,554.537 μs |    1 |    1575.94 KB |
| Jint              | droma(...)odern [24] |     2,544.973 μs |      33.3845 μs |     2,535.798 μs |    2 |     1343.9 KB |
| Jint_ParsedScript | droma(...)odern [24] |     2,703.006 μs |      40.5775 μs |     2,699.204 μs |    3 |    1324.26 KB |
| YantraJS          | droma(...)odern [24] |     4,425.681 μs |     122.5312 μs |     4,370.231 μs |    4 |   36525.94 KB |
|                   |                      |                  |                 |                  |      |               |
| Jint              | dromaeo-object-array |    17,432.945 μs |     330.5420 μs |    17,404.367 μs |    1 |   10512.39 KB |
| Jint_ParsedScript | dromaeo-object-array |    18,992.221 μs |     409.9450 μs |    18,921.875 μs |    2 |   10464.18 KB |
| Jurassic          | dromaeo-object-array |    37,884.989 μs |     887.8029 μs |    37,759.204 μs |    3 |   25808.85 KB |
| NilJS             | dromaeo-object-array |    54,790.363 μs |     312.6086 μs |    54,749.000 μs |    4 |   17862.17 KB |
| YantraJS          | dromaeo-object-array |    76,740.618 μs |   1,430.5283 μs |    76,985.762 μs |    5 | 1265132.98 KB |
|                   |                      |                  |                 |                  |      |               |
| Jurassic          | droma(...)odern [27] |               NA |              NA |               NA |    ? |            NA |
| Jint              | droma(...)odern [27] |    18,324.411 μs |     384.6255 μs |    18,143.150 μs |    1 |   10513.08 KB |
| Jint_ParsedScript | droma(...)odern [27] |    20,083.403 μs |     542.0078 μs |    19,947.159 μs |    2 |   10465.93 KB |
| NilJS             | droma(...)odern [27] |    54,684.835 μs |     215.5602 μs |    54,674.022 μs |    3 |   17863.19 KB |
| YantraJS          | droma(...)odern [27] |    79,095.895 μs |   2,274.2224 μs |    77,912.262 μs |    4 | 1268924.67 KB |
|                   |                      |                  |                 |                  |      |               |
| Jint_ParsedScript | droma(...)egexp [21] |   104,677.338 μs |   4,439.3757 μs |   104,395.275 μs |    1 |  162504.67 KB |
| Jint              | droma(...)egexp [21] |   139,649.992 μs |  10,465.3316 μs |   137,521.467 μs |    2 |  163274.49 KB |
| NilJS             | droma(...)egexp [21] |   548,893.146 μs |  12,991.1462 μs |   545,651.200 μs |    3 |  766689.91 KB |
| Jurassic          | droma(...)egexp [21] |   719,344.843 μs |  11,014.1862 μs |   720,642.650 μs |    4 |  821609.84 KB |
| YantraJS          | droma(...)egexp [21] | 1,445,028.508 μs | 108,673.0499 μs | 1,460,676.600 μs |    5 | 1152391.32 KB |
|                   |                      |                  |                 |                  |      |               |
| Jurassic          | droma(...)odern [28] |               NA |              NA |               NA |    ? |            NA |
| Jint_ParsedScript | droma(...)odern [28] |   160,398.950 μs |   6,859.6553 μs |   162,703.433 μs |    1 |  162863.27 KB |
| Jint              | droma(...)odern [28] |   207,784.571 μs |   7,233.1945 μs |   208,624.433 μs |    2 |  162948.93 KB |
| NilJS             | droma(...)odern [28] |   757,689.644 μs |  88,638.7260 μs |   778,966.950 μs |    3 |  767264.39 KB |
| YantraJS          | droma(...)odern [28] | 1,077,192.027 μs |  11,344.5171 μs | 1,075,658.600 μs |    4 | 1162835.16 KB |
|                   |                      |                  |                 |                  |      |               |
| Jint              | droma(...)tring [21] |   173,837.020 μs |   5,371.5216 μs |   172,753.567 μs |    1 | 1304324.96 KB |
| NilJS             | droma(...)tring [21] |   180,356.518 μs |   7,242.7912 μs |   179,883.083 μs |    1 | 1354915.83 KB |
| Jint_ParsedScript | droma(...)tring [21] |   185,724.091 μs |   7,386.9326 μs |   184,825.633 μs |    1 | 1304219.63 KB |
| YantraJS          | droma(...)tring [21] |   236,224.612 μs |  10,942.2860 μs |   236,532.833 μs |    2 | 1673943.14 KB |
| Jurassic          | droma(...)tring [21] |   289,323.391 μs |  36,855.7302 μs |   293,969.900 μs |    3 | 1430447.37 KB |
|                   |                      |                  |                 |                  |      |               |
| Jurassic          | droma(...)odern [28] |               NA |              NA |               NA |    ? |            NA |
| NilJS             | droma(...)odern [28] |   182,199.635 μs |   5,634.5102 μs |   182,610.375 μs |    1 | 1354978.47 KB |
| Jint              | droma(...)odern [28] |   217,426.803 μs |  17,630.3511 μs |   219,195.500 μs |    2 |  1304331.2 KB |
| YantraJS          | droma(...)odern [28] |   222,445.947 μs |  14,514.7690 μs |   219,882.967 μs |    2 | 1677310.09 KB |
| Jint_ParsedScript | droma(...)odern [28] |   241,302.877 μs |  16,109.5879 μs |   244,123.100 μs |    3 |  1304001.8 KB |
|                   |                      |                  |                 |                  |      |               |
| Jint              | droma(...)ase64 [21] |    30,518.458 μs |     148.9001 μs |    30,467.134 μs |    1 |     3773.4 KB |
| NilJS             | droma(...)ase64 [21] |    33,186.506 μs |   8,493.5127 μs |    28,461.479 μs |    1 |   19588.61 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    42,119.534 μs |  13,340.0448 μs |    32,260.403 μs |    2 |    3675.61 KB |
| Jurassic          | droma(...)ase64 [21] |    48,709.844 μs |     872.4823 μs |    48,333.127 μs |    3 |   73289.53 KB |
| YantraJS          | droma(...)ase64 [21] |    59,573.814 μs |   2,258.3502 μs |    60,295.982 μs |    4 |  760369.11 KB |
|                   |                      |                  |                 |                  |      |               |
| Jurassic          | droma(...)odern [28] |               NA |              NA |               NA |    ? |            NA |
| Jint_ParsedScript | droma(...)odern [28] |    32,129.737 μs |     102.7067 μs |    32,136.906 μs |    1 |    3675.69 KB |
| Jint              | droma(...)odern [28] |    33,149.610 μs |     184.1730 μs |    33,148.646 μs |    2 |    3773.89 KB |
| NilJS             | droma(...)odern [28] |    33,890.752 μs |     219.7029 μs |    33,929.593 μs |    2 |   31360.28 KB |
| YantraJS          | droma(...)odern [28] |    37,921.333 μs |   1,735.8423 μs |    37,402.909 μs |    3 |  761585.45 KB |
|                   |                      |                  |                 |                  |      |               |
| Jint_ParsedScript | evaluation           |         4.802 μs |       0.0327 μs |         4.809 μs |    1 |       23.3 KB |
| Jint              | evaluation           |        14.858 μs |       0.0468 μs |        14.867 μs |    2 |      33.94 KB |
| NilJS             | evaluation           |        25.341 μs |       0.1225 μs |        25.323 μs |    3 |      22.36 KB |
| YantraJS          | evaluation           |       137.833 μs |       6.1581 μs |       135.114 μs |    4 |     935.05 KB |
| Jurassic          | evaluation           |     2,083.457 μs |      12.0802 μs |     2,080.864 μs |    5 |     418.81 KB |
|                   |                      |                  |                 |                  |      |               |
| Jurassic          | evaluation-modern    |               NA |              NA |               NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern    |         4.842 μs |       0.0975 μs |         4.809 μs |    1 |      22.75 KB |
| Jint              | evaluation-modern    |        14.484 μs |       0.0716 μs |        14.470 μs |    2 |      33.87 KB |
| NilJS             | evaluation-modern    |        26.138 μs |       0.1034 μs |        26.133 μs |    3 |      22.35 KB |
| YantraJS          | evaluation-modern    |       150.818 μs |       9.7280 μs |       151.446 μs |    4 |     935.02 KB |
|                   |                      |                  |                 |                  |      |               |
| Jint_ParsedScript | linq-js              |        57.898 μs |       0.3332 μs |        57.973 μs |    1 |     186.83 KB |
| YantraJS          | linq-js              |       399.431 μs |       5.8884 μs |       401.252 μs |    2 |    1473.29 KB |
| Jint              | linq-js              |     1,248.876 μs |      10.4707 μs |     1,246.267 μs |    3 |    1286.02 KB |
| NilJS             | linq-js              |     3,986.681 μs |      15.6964 μs |     3,979.943 μs |    4 |    2739.46 KB |
| Jurassic          | linq-js              |    36,746.000 μs |   1,201.2314 μs |    36,538.457 μs |    5 |    9102.24 KB |
|                   |                      |                  |                 |                  |      |               |
| Jint_ParsedScript | minimal              |         1.611 μs |       0.0419 μs |         1.590 μs |    1 |      15.27 KB |
| NilJS             | minimal              |         2.620 μs |       0.0104 μs |         2.620 μs |    2 |       4.51 KB |
| Jint              | minimal              |         2.919 μs |       0.0261 μs |         2.913 μs |    3 |      17.19 KB |
| YantraJS          | minimal              |       130.423 μs |       1.5840 μs |       130.605 μs |    4 |     929.87 KB |
| Jurassic          | minimal              |     2,263.180 μs |       7.1142 μs |     2,263.679 μs |    5 |     385.19 KB |
|                   |                      |                  |                 |                  |      |               |
| YantraJS          | stopwatch            |    68,140.457 μs |   2,703.8970 μs |    67,666.837 μs |    1 |  219954.96 KB |
| NilJS             | stopwatch            |   134,223.171 μs |   1,019.1089 μs |   134,178.750 μs |    2 |   94876.49 KB |
| Jurassic          | stopwatch            |   139,452.179 μs |   1,061.5619 μs |   139,450.212 μs |    2 |  156932.58 KB |
| Jint              | stopwatch            |   199,938.744 μs |     476.8276 μs |   200,125.333 μs |    3 |   39418.89 KB |
| Jint_ParsedScript | stopwatch            |   203,129.997 μs |   1,015.4587 μs |   203,037.367 μs |    3 |   39387.13 KB |
|                   |                      |                  |                 |                  |      |               |
| YantraJS          | stopwatch-modern     |    65,962.307 μs |     341.4732 μs |    65,915.113 μs |    1 |  238332.97 KB |
| Jurassic          | stopwatch-modern     |   153,177.090 μs |   1,453.3715 μs |   152,457.575 μs |    2 |  288625.25 KB |
| NilJS             | stopwatch-modern     |   226,962.231 μs |   2,431.0559 μs |   226,595.833 μs |    3 |  324502.66 KB |
| Jint              | stopwatch-modern     |   255,795.133 μs |  13,625.7925 μs |   252,197.567 μs |    4 |   39635.13 KB |
| Jint_ParsedScript | stopwatch-modern     |   270,806.054 μs |   2,846.7509 μs |   269,562.350 μs |    4 |   39602.85 KB |

Benchmarks with issues:
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [22]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [24]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [27]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=evaluation-modern]
