To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2025-01-28

* Jint main
* Jurassic 3.2.8
* NiL.JS 2.5.1684
* YantraJS.Core 1.2.246

```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2894)
12th Gen Intel Core i9-12900H, 1 CPU, 20 logical and 14 physical cores
.NET SDK 9.0.102
  [Host]     : .NET 9.0.1 (9.0.124.61010), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.1 (9.0.124.61010), X64 RyuJIT AVX2


```
| Method            | FileName             | Mean             | StdDev          | Median           | Rank | Allocated      |
|------------------ |--------------------- |-----------------:|----------------:|-----------------:|-----:|---------------:|
| Jint              | array-stress         |     4,134.906 μs |      48.9872 μs |     4,154.371 μs |    1 |     6746.25 KB |
| Jint_ParsedScript | array-stress         |     4,267.848 μs |      45.9849 μs |     4,266.743 μs |    1 |     6712.04 KB |
| NilJS             | array-stress         |     4,976.963 μs |      68.2428 μs |     4,969.757 μs |    2 |     4533.01 KB |
| Jurassic          | array-stress         |     7,984.831 μs |      32.9704 μs |     7,980.435 μs |    3 |    11645.94 KB |
| YantraJS          | array-stress         |    15,410.528 μs |     109.2894 μs |    15,424.466 μs |    4 |    86542.18 KB |
|                   |                      |                  |                 |                  |      |                |
| YantraJS          | dromaeo-3d-cube      |     3,707.166 μs |      89.4799 μs |     3,731.775 μs |    1 |    10661.71 KB |
| NilJS             | dromaeo-3d-cube      |     5,810.101 μs |      40.6481 μs |     5,817.023 μs |    2 |     4690.96 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    11,410.794 μs |     138.7524 μs |    11,437.136 μs |    3 |      5781.4 KB |
| Jint              | dromaeo-3d-cube      |    11,513.462 μs |     115.1267 μs |    11,540.987 μs |    3 |     6129.69 KB |
| Jurassic          | dromaeo-3d-cube      |    44,072.561 μs |     613.1048 μs |    44,202.900 μs |    4 |    10660.96 KB |
|                   |                      |                  |                 |                  |      |                |
| Jurassic          | droma(...)odern [22] |               NA |              NA |               NA |    ? |             NA |
| YantraJS          | droma(...)odern [22] |     3,494.485 μs |      77.1171 μs |     3,498.384 μs |    1 |    10419.62 KB |
| NilJS             | droma(...)odern [22] |     6,840.550 μs |      68.7693 μs |     6,847.101 μs |    2 |     6078.12 KB |
| Jint              | droma(...)odern [22] |    11,639.226 μs |     101.3860 μs |    11,631.761 μs |    3 |     5713.83 KB |
| Jint_ParsedScript | droma(...)odern [22] |    12,044.325 μs |     141.0013 μs |    12,015.592 μs |    3 |     5367.29 KB |
|                   |                      |                  |                 |                  |      |                |
| NilJS             | dromaeo-core-eval    |     1,166.054 μs |      10.8880 μs |     1,164.985 μs |    1 |     1595.42 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     2,026.896 μs |      22.3793 μs |     2,030.850 μs |    2 |      331.17 KB |
| Jint              | dromaeo-core-eval    |     2,129.125 μs |      19.2930 μs |     2,136.228 μs |    3 |         354 KB |
| YantraJS          | dromaeo-core-eval    |     4,727.110 μs |      37.6915 μs |     4,731.884 μs |    4 |    36506.75 KB |
| Jurassic          | dromaeo-core-eval    |     8,171.871 μs |      96.0700 μs |     8,175.505 μs |    5 |     2877.49 KB |
|                   |                      |                  |                 |                  |      |                |
| Jurassic          | droma(...)odern [24] |               NA |              NA |               NA |    ? |             NA |
| NilJS             | droma(...)odern [24] |     1,333.308 μs |      12.4872 μs |     1,336.324 μs |    1 |     1592.37 KB |
| Jint_ParsedScript | droma(...)odern [24] |     2,072.305 μs |      23.3611 μs |     2,077.462 μs |    2 |      331.61 KB |
| Jint              | droma(...)odern [24] |     2,091.678 μs |      14.6069 μs |     2,092.029 μs |    2 |      353.62 KB |
| YantraJS          | droma(...)odern [24] |     4,584.036 μs |     130.1799 μs |     4,596.960 μs |    3 |    36506.87 KB |
|                   |                      |                  |                 |                  |      |                |
| Jint              | dromaeo-object-array |    28,970.878 μs |     587.9956 μs |    29,065.406 μs |    1 |    96288.81 KB |
| Jint_ParsedScript | dromaeo-object-array |    30,160.579 μs |     422.8974 μs |    30,334.766 μs |    1 |    96234.25 KB |
| Jurassic          | dromaeo-object-array |    32,046.411 μs |     410.0970 μs |    32,097.631 μs |    2 |    25810.41 KB |
| NilJS             | dromaeo-object-array |    55,021.017 μs |     464.6542 μs |    55,182.645 μs |    3 |    17696.81 KB |
| YantraJS          | dromaeo-object-array |    80,535.621 μs |   1,567.6182 μs |    79,770.017 μs |    4 |  1265113.61 KB |
|                   |                      |                  |                 |                  |      |                |
| Jurassic          | droma(...)odern [27] |               NA |              NA |               NA |    ? |             NA |
| Jint              | droma(...)odern [27] |    35,156.080 μs |     290.1205 μs |    35,232.660 μs |    1 |   111456.87 KB |
| Jint_ParsedScript | droma(...)odern [27] |    35,624.484 μs |     782.8793 μs |    35,693.727 μs |    1 |   111404.29 KB |
| NilJS             | droma(...)odern [27] |    55,465.717 μs |     615.9257 μs |    55,444.861 μs |    2 |    17692.22 KB |
| YantraJS          | droma(...)odern [27] |    82,229.209 μs |   1,784.0931 μs |    81,759.414 μs |    3 |  1268904.93 KB |
|                   |                      |                  |                 |                  |      |                |
| Jint_ParsedScript | droma(...)egexp [21] |   127,062.495 μs |   8,610.4880 μs |   127,053.262 μs |    1 |   148104.81 KB |
| Jint              | droma(...)egexp [21] |   149,213.216 μs |   8,877.4985 μs |   148,553.650 μs |    2 |   148425.54 KB |
| NilJS             | droma(...)egexp [21] |   595,261.858 μs |  12,967.6196 μs |   598,315.400 μs |    3 |   767379.75 KB |
| Jurassic          | droma(...)egexp [21] |   677,778.981 μs |  20,326.0688 μs |   681,648.700 μs |    4 |   825031.25 KB |
| YantraJS          | droma(...)egexp [21] | 1,113,766.961 μs |  30,960.7356 μs | 1,120,353.400 μs |    5 |  1155606.22 KB |
|                   |                      |                  |                 |                  |      |                |
| Jurassic          | droma(...)odern [28] |               NA |              NA |               NA |    ? |             NA |
| Jint_ParsedScript | droma(...)odern [28] |   150,944.914 μs |   9,976.3178 μs |   150,043.500 μs |    1 |   167293.22 KB |
| Jint              | droma(...)odern [28] |   174,638.096 μs |   9,981.9481 μs |   174,256.767 μs |    2 |    167309.4 KB |
| NilJS             | droma(...)odern [28] |   611,461.150 μs |  11,270.2271 μs |   613,201.800 μs |    3 |   768351.99 KB |
| YantraJS          | droma(...)odern [28] | 1,066,830.788 μs |  32,925.8243 μs | 1,072,661.050 μs |    4 |  1160301.32 KB |
|                   |                      |                  |                 |                  |      |                |
| Jint              | droma(...)tring [21] |   204,477.579 μs |  12,195.5349 μs |   205,806.200 μs |    1 |  1315956.85 KB |
| Jint_ParsedScript | droma(...)tring [21] |   208,097.175 μs |   6,646.8288 μs |   209,203.525 μs |    1 |  1315785.32 KB |
| Jurassic          | droma(...)tring [21] |   246,029.792 μs |   9,773.3870 μs |   245,497.950 μs |    2 |  1458064.51 KB |
| NilJS             | droma(...)tring [21] |   268,977.639 μs |   5,582.4217 μs |   270,391.650 μs |    3 |  1377799.54 KB |
| YantraJS          | droma(...)tring [21] | 1,248,007.107 μs |  13,536.8017 μs | 1,248,230.400 μs |    4 |  15728655.8 KB |
|                   |                      |                  |                 |                  |      |                |
| Jurassic          | droma(...)odern [28] |               NA |              NA |               NA |    ? |             NA |
| Jint_ParsedScript | droma(...)odern [28] | 2,435,338.867 μs |  41,434.1513 μs | 2,444,624.900 μs |    1 | 20820056.91 KB |
| Jint              | droma(...)odern [28] | 2,452,352.307 μs |  36,824.8006 μs | 2,465,557.200 μs |    1 | 20820515.62 KB |
| NilJS             | droma(...)odern [28] | 3,233,162.767 μs |  23,111.3094 μs | 3,237,140.500 μs |    2 | 21266800.21 KB |
| YantraJS          | droma(...)odern [28] | 4,627,904.805 μs | 101,878.7676 μs | 4,599,435.700 μs |    3 | 39260603.97 KB |
|                   |                      |                  |                 |                  |      |                |
| Jint              | droma(...)ase64 [21] |    24,365.155 μs |     162.1064 μs |    24,350.088 μs |    1 |        2418 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    25,213.864 μs |     275.3303 μs |    25,241.897 μs |    1 |     2309.81 KB |
| NilJS             | droma(...)ase64 [21] |    25,805.285 μs |     383.7300 μs |    25,835.589 μs |    1 |    19603.62 KB |
| YantraJS          | droma(...)ase64 [21] |    43,340.288 μs |     771.9420 μs |    43,268.725 μs |    2 |   760342.13 KB |
| Jurassic          | droma(...)ase64 [21] |    45,523.324 μs |     637.8335 μs |    45,469.500 μs |    2 |    73292.22 KB |
|                   |                      |                  |                 |                  |      |                |
| Jurassic          | droma(...)odern [28] |               NA |              NA |               NA |    ? |             NA |
| Jint              | droma(...)odern [28] |    31,471.179 μs |     237.3956 μs |    31,513.450 μs |    1 |    12573.82 KB |
| NilJS             | droma(...)odern [28] |    31,516.315 μs |     552.6211 μs |    31,633.894 μs |    1 |    30346.93 KB |
| Jint_ParsedScript | droma(...)odern [28] |    31,565.234 μs |     426.7583 μs |    31,646.056 μs |    1 |    12465.99 KB |
| YantraJS          | droma(...)odern [28] |    41,050.104 μs |   1,041.0559 μs |    41,080.488 μs |    2 |   761558.61 KB |
|                   |                      |                  |                 |                  |      |                |
| YantraJS          | evaluation           |               NA |              NA |               NA |    ? |             NA |
| Jint_ParsedScript | evaluation           |         4.477 μs |       0.0506 μs |         4.465 μs |    1 |       22.11 KB |
| Jint              | evaluation           |        11.708 μs |       0.1096 μs |        11.728 μs |    2 |        33.6 KB |
| NilJS             | evaluation           |        25.675 μs |       0.1635 μs |        25.685 μs |    3 |       23.47 KB |
| Jurassic          | evaluation           |     1,235.592 μs |      19.7662 μs |     1,237.863 μs |    4 |      419.12 KB |
|                   |                      |                  |                 |                  |      |                |
| Jurassic          | evaluation-modern    |               NA |              NA |               NA |    ? |             NA |
| YantraJS          | evaluation-modern    |               NA |              NA |               NA |    ? |             NA |
| Jint_ParsedScript | evaluation-modern    |         4.535 μs |       0.0598 μs |         4.519 μs |    1 |       21.85 KB |
| Jint              | evaluation-modern    |        11.761 μs |       0.1581 μs |        11.747 μs |    2 |       33.89 KB |
| NilJS             | evaluation-modern    |        25.713 μs |       0.1705 μs |        25.699 μs |    3 |       23.09 KB |
|                   |                      |                  |                 |                  |      |                |
| Jint_ParsedScript | linq-js              |        53.993 μs |       1.2147 μs |        54.130 μs |    1 |      176.41 KB |
| YantraJS          | linq-js              |       337.593 μs |       3.7425 μs |       337.514 μs |    2 |     1461.39 KB |
| Jint              | linq-js              |     1,337.860 μs |     320.5714 μs |     1,205.928 μs |    3 |      1292.7 KB |
| NilJS             | linq-js              |     5,417.306 μs |     107.9540 μs |     5,444.659 μs |    4 |     4108.38 KB |
| Jurassic          | linq-js              |    31,821.296 μs |     456.0910 μs |    31,902.875 μs |    5 |     9192.46 KB |
|                   |                      |                  |                 |                  |      |                |
| Jint_ParsedScript | minimal              |         1.570 μs |       0.0359 μs |         1.579 μs |    1 |       14.39 KB |
| Jint              | minimal              |         2.452 μs |       0.0282 μs |         2.448 μs |    2 |       16.38 KB |
| NilJS             | minimal              |         2.640 μs |       0.0363 μs |         2.646 μs |    3 |        4.81 KB |
| YantraJS          | minimal              |       104.900 μs |       1.3573 μs |       104.360 μs |    4 |       911.1 KB |
| Jurassic          | minimal              |       199.568 μs |       2.3285 μs |       199.644 μs |    5 |      385.32 KB |
|                   |                      |                  |                 |                  |      |                |
| YantraJS          | stopwatch            |    59,463.269 μs |     610.2905 μs |    59,571.289 μs |    1 |   217462.85 KB |
| NilJS             | stopwatch            |   128,642.995 μs |   1,331.2769 μs |   128,908.000 μs |    2 |    97360.52 KB |
| Jurassic          | stopwatch            |   132,700.140 μs |   1,534.5249 μs |   132,701.200 μs |    2 |   156934.49 KB |
| Jint              | stopwatch            |   186,821.867 μs |   1,645.9570 μs |   186,950.433 μs |    3 |    40668.59 KB |
| Jint_ParsedScript | stopwatch            |   198,675.067 μs |   2,545.0857 μs |   198,291.067 μs |    4 |    40632.43 KB |
|                   |                      |                  |                 |                  |      |                |
| YantraJS          | stopwatch-modern     |    64,356.615 μs |     796.3376 μs |    64,386.238 μs |    1 |   235840.88 KB |
| Jurassic          | stopwatch-modern     |   173,067.907 μs |   2,382.0187 μs |   172,899.500 μs |    2 |   288627.38 KB |
| NilJS             | stopwatch-modern     |   224,034.216 μs |   2,608.6675 μs |   224,207.867 μs |    3 |   369703.83 KB |
| Jint              | stopwatch-modern     |   272,945.230 μs |   3,351.3342 μs |   272,988.900 μs |    4 |   230654.34 KB |
| Jint_ParsedScript | stopwatch-modern     |   290,751.857 μs |   3,147.8824 μs |   292,035.750 μs |    5 |   230618.89 KB |

Benchmarks with issues:
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [22]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [24]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [27]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.YantraJS: DefaultJob [FileName=evaluation]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=evaluation-modern]
  EngineComparisonBenchmark.YantraJS: DefaultJob [FileName=evaluation-modern]
