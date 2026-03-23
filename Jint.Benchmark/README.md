To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2026-03-01

* Jint 4.6.1
* Jurassic 3.2.9
* NiL.JS 2.6.1712
* YantraJS.Core 1.2.301

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


```
| Method            | FileName             | Mean             | StdDev         | Median           | Rank | Allocated     |
|------------------ |--------------------- |-----------------:|---------------:|-----------------:|-----:|--------------:|
| Jint              | array-stress         |     4,691.616 μs |     17.0325 μs |     4,696.770 μs |    1 |    6748.79 KB |
| Jint_ParsedScript | array-stress         |     4,708.329 μs |     27.4563 μs |     4,714.834 μs |    1 |    6712.54 KB |
| NilJS             | array-stress         |     5,050.918 μs |     24.1263 μs |     5,039.567 μs |    2 |    4521.19 KB |
| Jurassic          | array-stress         |     9,206.247 μs |     50.4266 μs |     9,213.697 μs |    3 |   11644.86 KB |
| YantraJS          | array-stress         |    15,396.920 μs |    172.8415 μs |    15,478.598 μs |    4 |   86548.68 KB |
|                   |                      |                  |                |                  |      |               |
| YantraJS          | dromaeo-3d-cube      |     3,261.457 μs |     48.4388 μs |     3,261.962 μs |    1 |   10695.01 KB |
| NilJS             | dromaeo-3d-cube      |     6,509.515 μs |     45.1234 μs |     6,518.614 μs |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    13,228.323 μs |     29.6305 μs |    13,220.320 μs |    3 |    5421.53 KB |
| Jint              | dromaeo-3d-cube      |    13,259.427 μs |     64.6836 μs |    13,268.498 μs |    3 |    5784.63 KB |
| Jurassic          | dromaeo-3d-cube      |    53,666.467 μs |    486.3579 μs |    53,650.550 μs |    4 |   10654.72 KB |
|                   |                      |                  |                |                  |      |               |
| Jurassic          | droma(...)odern [22] |               NA |             NA |               NA |    ? |            NA |
| YantraJS          | droma(...)odern [22] |     3,024.308 μs |     35.1755 μs |     3,024.986 μs |    1 |   10452.92 KB |
| NilJS             | droma(...)odern [22] |     7,203.517 μs |     40.8363 μs |     7,195.374 μs |    2 |    5977.95 KB |
| Jint              | droma(...)odern [22] |    13,170.754 μs |     66.6874 μs |    13,152.802 μs |    3 |    5291.46 KB |
| Jint_ParsedScript | droma(...)odern [22] |    13,714.986 μs |     96.4996 μs |    13,702.873 μs |    4 |    4930.14 KB |
|                   |                      |                  |                |                  |      |               |
| NilJS             | dromaeo-core-eval    |     1,272.317 μs |      9.3278 μs |     1,268.690 μs |    1 |     1577.1 KB |
| Jint              | dromaeo-core-eval    |     2,525.904 μs |     23.4180 μs |     2,511.754 μs |    2 |     360.92 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     2,542.429 μs |     14.0752 μs |     2,545.205 μs |    2 |      337.3 KB |
| YantraJS          | dromaeo-core-eval    |     4,387.624 μs |     80.1260 μs |     4,356.207 μs |    3 |   36529.26 KB |
| Jurassic          | dromaeo-core-eval    |    16,196.879 μs |    137.7718 μs |    16,103.456 μs |    4 |    2876.11 KB |
|                   |                      |                  |                |                  |      |               |
| Jurassic          | droma(...)odern [24] |               NA |             NA |               NA |    ? |            NA |
| NilJS             | droma(...)odern [24] |     1,479.726 μs |     13.4973 μs |     1,482.924 μs |    1 |    1575.94 KB |
| Jint_ParsedScript | droma(...)odern [24] |     2,504.267 μs |     13.8348 μs |     2,507.193 μs |    2 |     336.95 KB |
| Jint              | droma(...)odern [24] |     2,580.271 μs |      7.6896 μs |     2,578.716 μs |    3 |     359.67 KB |
| YantraJS          | droma(...)odern [24] |     4,459.993 μs |     82.0219 μs |     4,458.036 μs |    4 |   36529.38 KB |
|                   |                      |                  |                |                  |      |               |
| Jint              | dromaeo-object-array |    32,117.117 μs |    320.3459 μs |    32,225.750 μs |    1 |   96290.54 KB |
| Jint_ParsedScript | dromaeo-object-array |    33,036.621 μs |    354.2772 μs |    33,104.734 μs |    1 |   96233.61 KB |
| Jurassic          | dromaeo-object-array |    37,392.516 μs |    457.9002 μs |    37,559.164 μs |    2 |   25808.58 KB |
| NilJS             | dromaeo-object-array |    53,241.602 μs |    733.3681 μs |    52,963.585 μs |    3 |   17862.17 KB |
| YantraJS          | dromaeo-object-array |    67,266.373 μs |  1,809.8205 μs |    67,874.471 μs |    4 | 1265136.13 KB |
|                   |                      |                  |                |                  |      |               |
| Jurassic          | droma(...)odern [27] |               NA |             NA |               NA |    ? |            NA |
| Jint              | droma(...)odern [27] |    32,734.302 μs |    508.0678 μs |    32,967.300 μs |    1 |   96289.81 KB |
| Jint_ParsedScript | droma(...)odern [27] |    34,250.823 μs |    252.1166 μs |    34,190.947 μs |    2 |   96235.32 KB |
| NilJS             | droma(...)odern [27] |    53,863.733 μs |    269.7683 μs |    53,850.830 μs |    3 |   17863.19 KB |
| YantraJS          | droma(...)odern [27] |    76,244.086 μs |  1,861.5264 μs |    77,095.669 μs |    4 | 1268927.74 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | droma(...)egexp [21] |    95,485.275 μs |  3,980.1824 μs |    95,663.620 μs |    1 |  145346.22 KB |
| Jint              | droma(...)egexp [21] |   151,314.551 μs |  9,138.8180 μs |   151,138.533 μs |    2 |  151305.22 KB |
| NilJS             | droma(...)egexp [21] |   531,372.379 μs |  7,276.1515 μs |   530,397.550 μs |    3 |  767094.38 KB |
| Jurassic          | droma(...)egexp [21] |   708,152.418 μs | 16,521.4057 μs |   711,786.100 μs |    4 |  826669.16 KB |
| YantraJS          | droma(...)egexp [21] | 1,061,324.245 μs | 23,494.1166 μs | 1,063,029.400 μs |    5 | 1160463.81 KB |
|                   |                      |                  |                |                  |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |               NA |    ? |            NA |
| Jint_ParsedScript | droma(...)odern [28] |    97,881.258 μs |  4,590.8004 μs |    97,112.333 μs |    1 |  151615.38 KB |
| Jint              | droma(...)odern [28] |   151,527.870 μs | 15,229.3341 μs |   150,456.900 μs |    2 |  148812.27 KB |
| NilJS             | droma(...)odern [28] |   533,492.806 μs | 10,604.1301 μs |   531,319.500 μs |    3 |  766253.38 KB |
| YantraJS          | droma(...)odern [28] | 1,054,218.960 μs | 31,368.6551 μs | 1,059,431.000 μs |    4 | 1154824.82 KB |
|                   |                      |                  |                |                  |      |               |
| NilJS             | droma(...)tring [21] |   144,388.575 μs |  3,693.8766 μs |   144,360.367 μs |    1 | 1354866.88 KB |
| Jint_ParsedScript | droma(...)tring [21] |   174,392.785 μs |  7,429.0888 μs |   173,322.133 μs |    2 | 1302862.09 KB |
| YantraJS          | droma(...)tring [21] |   194,913.575 μs |  6,740.5396 μs |   195,749.233 μs |    3 | 1673875.06 KB |
| Jurassic          | droma(...)tring [21] |   225,891.690 μs |  5,343.8812 μs |   225,844.433 μs |    4 | 1430414.53 KB |
| Jint              | droma(...)tring [21] |   229,886.706 μs | 71,180.7863 μs |   180,669.167 μs |    5 | 1303014.04 KB |
|                   |                      |                  |                |                  |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |               NA |    ? |            NA |
| NilJS             | droma(...)odern [28] |   147,794.780 μs |  4,632.3069 μs |   148,534.633 μs |    1 | 1354920.03 KB |
| Jint_ParsedScript | droma(...)odern [28] |   177,155.292 μs |  7,261.1855 μs |   176,953.550 μs |    2 | 1302772.05 KB |
| YantraJS          | droma(...)odern [28] |   192,820.336 μs |  8,497.2666 μs |   191,434.567 μs |    3 | 1677377.26 KB |
| Jint              | droma(...)odern [28] |   239,253.797 μs | 76,484.0724 μs |   182,877.650 μs |    3 | 1303074.31 KB |
|                   |                      |                  |                |                  |      |               |
| NilJS             | droma(...)ase64 [21] |    26,340.913 μs |    198.9573 μs |    26,430.794 μs |    1 |   19588.58 KB |
| Jint              | droma(...)ase64 [21] |    28,422.226 μs |     65.0186 μs |    28,432.869 μs |    2 |    2421.23 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    28,847.812 μs |    168.9030 μs |    28,810.209 μs |    2 |    2309.92 KB |
| YantraJS          | droma(...)ase64 [21] |    38,987.504 μs |    922.2634 μs |    38,943.408 μs |    3 |  760372.05 KB |
| Jurassic          | droma(...)ase64 [21] |    47,416.892 μs |    286.3888 μs |    47,453.645 μs |    4 |   73290.47 KB |
|                   |                      |                  |                |                  |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |               NA |    ? |            NA |
| Jint              | droma(...)odern [28] |    33,036.355 μs |     71.0817 μs |    33,004.000 μs |    1 |    2421.24 KB |
| NilJS             | droma(...)odern [28] |    33,116.482 μs |    231.6549 μs |    33,158.093 μs |    1 |   31360.18 KB |
| Jint_ParsedScript | droma(...)odern [28] |    36,905.425 μs |    222.7873 μs |    36,802.261 μs |    2 |    2309.98 KB |
| YantraJS          | droma(...)odern [28] |    39,365.056 μs |    705.5726 μs |    39,291.075 μs |    3 |  761588.59 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | evaluation           |         5.197 μs |      0.0644 μs |         5.173 μs |    1 |       22.6 KB |
| Jint              | evaluation           |        15.932 μs |      0.0864 μs |        15.922 μs |    2 |      34.52 KB |
| NilJS             | evaluation           |        26.889 μs |      0.0903 μs |        26.905 μs |    3 |      22.36 KB |
| YantraJS          | evaluation           |       122.493 μs |      2.0514 μs |       123.048 μs |    4 |     924.33 KB |
| Jurassic          | evaluation           |     2,015.653 μs |      8.9151 μs |     2,014.522 μs |    5 |     418.92 KB |
|                   |                      |                  |                |                  |      |               |
| Jurassic          | evaluation-modern    |               NA |             NA |               NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern    |         5.275 μs |      0.0769 μs |         5.270 μs |    1 |      22.34 KB |
| Jint              | evaluation-modern    |        15.328 μs |      0.1349 μs |        15.305 μs |    2 |      34.82 KB |
| NilJS             | evaluation-modern    |        27.372 μs |      0.2383 μs |        27.330 μs |    3 |      22.35 KB |
| YantraJS          | evaluation-modern    |       121.993 μs |      2.5549 μs |       122.139 μs |    4 |      924.3 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | linq-js              |        59.702 μs |      0.4075 μs |        59.762 μs |    1 |     180.67 KB |
| YantraJS          | linq-js              |       351.259 μs |      3.4444 μs |       350.863 μs |    2 |    1467.33 KB |
| Jint              | linq-js              |     1,255.229 μs |      8.6941 μs |     1,257.645 μs |    3 |    1299.95 KB |
| NilJS             | linq-js              |     4,087.173 μs |     33.0994 μs |     4,088.310 μs |    4 |    2739.46 KB |
| Jurassic          | linq-js              |    36,843.230 μs |    484.8170 μs |    36,891.627 μs |    5 |    9103.59 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | minimal              |         1.698 μs |      0.0257 μs |         1.703 μs |    1 |      14.91 KB |
| Jint              | minimal              |         2.812 μs |      0.0173 μs |         2.811 μs |    2 |      16.91 KB |
| NilJS             | minimal              |         2.826 μs |      0.0106 μs |         2.825 μs |    2 |       4.51 KB |
| YantraJS          | minimal              |       129.871 μs |      8.0617 μs |       129.180 μs |    3 |     919.22 KB |
| Jurassic          | minimal              |     2,225.560 μs |      9.7999 μs |     2,224.859 μs |    4 |     385.19 KB |
|                   |                      |                  |                |                  |      |               |
| YantraJS          | stopwatch            |    73,079.255 μs |  3,263.4883 μs |    73,055.613 μs |    1 |  219944.24 KB |
| NilJS             | stopwatch            |   152,174.728 μs |  4,460.1733 μs |   152,703.100 μs |    2 |   94876.49 KB |
| Jurassic          | stopwatch            |   161,037.916 μs |  5,878.3002 μs |   159,930.150 μs |    2 |  156932.58 KB |
| Jint              | stopwatch            |   230,005.112 μs |  5,216.7066 μs |   228,664.567 μs |    3 |   60456.74 KB |
| Jint_ParsedScript | stopwatch            |   247,600.049 μs |  7,371.2674 μs |   247,241.867 μs |    4 |   60419.34 KB |
|                   |                      |                  |                |                  |      |               |
| YantraJS          | stopwatch-modern     |    66,467.444 μs |    845.1552 μs |    66,285.750 μs |    1 |  238322.26 KB |
| Jurassic          | stopwatch-modern     |   152,886.837 μs |  1,082.4752 μs |   152,660.825 μs |    2 |  288625.79 KB |
| NilJS             | stopwatch-modern     |   252,079.271 μs |  1,817.7111 μs |   252,044.050 μs |    3 |  324502.66 KB |
| Jint              | stopwatch-modern     |   277,991.452 μs | 10,096.6550 μs |   272,907.800 μs |    4 |   60672.24 KB |
| Jint_ParsedScript | stopwatch-modern     |   285,139.187 μs |  2,595.5357 μs |   283,688.850 μs |    4 |   60635.06 KB |

Benchmarks with issues:
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [22]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [24]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [27]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=evaluation-modern]
