To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.
* YantraJS.Core 1.2.404 has a severe regression on `dromaeo-object-regexp` (~36 s/op, ~32 GB allocated); earlier 1.2.344 ran it at ~1 s/op.
* Measured against `main` with the engine-comparison performance work merged (#2502 empty-FDI fast path, #2503 eval/`new Function` compilation cache, #2504 prepared-script fix, #2506 zero-copy string slice views, #2507 global-binding inline cache). Most visibly `dromaeo-object-string` dropped from ~170 ms / 1.3 GB to ~61 ms / 139 MB (now ahead of NiL.JS), and `stopwatch` from ~205 ms to ~181 ms.

Last updated 2026-06-08 (engine-comparison perf work merged to main)

* Jint main (development build)
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.404

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8457/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.300
  [Host]     : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.8 (10.0.8, 10.0.826.23019), X64 RyuJIT x86-64-v3


```
| Method            | FileName             | Mean              | StdDev          | Rank | Allocated      |
|------------------ |--------------------- |------------------:|----------------:|-----:|---------------:|
| Jint_ParsedScript | array-stress         |      3,289.764 μs |       9.7923 μs |    1 |     1281.47 KB |
| Jint              | array-stress         |      3,462.574 μs |      38.5094 μs |    2 |     1310.26 KB |
| YantraJS          | array-stress         |      3,686.796 μs |       8.1957 μs |    3 |    27043.38 KB |
| NilJS             | array-stress         |      5,079.797 μs |      21.1851 μs |    4 |     4521.19 KB |
| Jurassic          | array-stress         |      8,907.976 μs |      27.1426 μs |    5 |    11644.88 KB |
|                   |                      |                   |                 |      |                |
| YantraJS          | dromaeo-3d-cube      |      2,499.876 μs |       9.8127 μs |    1 |      7591.5 KB |
| NilJS             | dromaeo-3d-cube      |      6,113.129 μs |      26.9704 μs |    2 |     4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |     12,778.674 μs |      40.0405 μs |    3 |     5623.09 KB |
| Jint              | dromaeo-3d-cube      |     13,140.556 μs |      35.4067 μs |    4 |      5926.3 KB |
| Jurassic          | dromaeo-3d-cube      |     54,537.536 μs |     144.6034 μs |    5 |    10651.37 KB |
|                   |                      |                   |                 |      |                |
| Jurassic          | droma(...)odern [22] |                NA |              NA |    ? |             NA |
| YantraJS          | droma(...)odern [22] |      2,464.684 μs |      16.5669 μs |    1 |     7509.73 KB |
| NilJS             | droma(...)odern [22] |      6,964.944 μs |      18.3871 μs |    2 |     5977.95 KB |
| Jint_ParsedScript | droma(...)odern [22] |     12,842.909 μs |      33.6295 μs |    3 |     5622.58 KB |
| Jint              | droma(...)odern [22] |     13,304.243 μs |      32.7982 μs |    4 |     5925.61 KB |
|                   |                      |                   |                 |      |                |
| NilJS             | dromaeo-core-eval    |      1,209.630 μs |       5.0592 μs |    1 |      1577.1 KB |
| Jint              | dromaeo-core-eval    |      2,431.086 μs |       7.4164 μs |    2 |       352.7 KB |
| Jint_ParsedScript | dromaeo-core-eval    |      2,432.104 μs |       6.3468 μs |    2 |       332.3 KB |
| YantraJS          | dromaeo-core-eval    |      4,795.084 μs |      62.3807 μs |    3 |    35784.73 KB |
| Jurassic          | dromaeo-core-eval    |     16,608.832 μs |      14.6017 μs |    4 |     2876.04 KB |
|                   |                      |                   |                 |      |                |
| Jurassic          | droma(...)odern [24] |                NA |              NA |    ? |             NA |
| NilJS             | droma(...)odern [24] |      1,476.924 μs |       7.0056 μs |    1 |     1575.94 KB |
| Jint              | droma(...)odern [24] |      2,401.823 μs |       8.4657 μs |    2 |      351.66 KB |
| Jint_ParsedScript | droma(...)odern [24] |      2,488.798 μs |       8.7534 μs |    3 |         332 KB |
| YantraJS          | droma(...)odern [24] |      4,729.213 μs |      30.2361 μs |    4 |    35784.84 KB |
|                   |                      |                   |                 |      |                |
| Jint_ParsedScript | dromaeo-object-array |     17,445.714 μs |     348.4832 μs |    1 |     9986.38 KB |
| Jint              | dromaeo-object-array |     17,534.436 μs |      53.5845 μs |    1 |    10034.74 KB |
| Jurassic          | dromaeo-object-array |     35,647.915 μs |     113.0425 μs |    2 |    25809.34 KB |
| NilJS             | dromaeo-object-array |     50,983.858 μs |     105.9934 μs |    3 |    17862.17 KB |
| YantraJS          | dromaeo-object-array |    219,552.491 μs |   6,089.5792 μs |    4 |   379901.99 KB |
|                   |                      |                   |                 |      |                |
| Jurassic          | droma(...)odern [27] |                NA |              NA |    ? |             NA |
| Jint_ParsedScript | droma(...)odern [27] |     18,423.893 μs |      61.0915 μs |    1 |     9988.12 KB |
| Jint              | droma(...)odern [27] |     18,585.744 μs |     106.8190 μs |    1 |    10035.45 KB |
| NilJS             | droma(...)odern [27] |     51,049.806 μs |     145.7115 μs |    2 |    17863.19 KB |
| YantraJS          | droma(...)odern [27] |    278,448.777 μs |   7,917.7268 μs |    3 |   383700.82 KB |
|                   |                      |                   |                 |      |                |
| Jint_ParsedScript | droma(...)egexp [21] |     98,717.183 μs |   4,322.2402 μs |    1 |   159281.22 KB |
| Jint              | droma(...)egexp [21] |    118,923.323 μs |   6,958.4710 μs |    2 |   161015.96 KB |
| NilJS             | droma(...)egexp [21] |    531,246.913 μs |   7,641.2865 μs |    3 |   767275.66 KB |
| Jurassic          | droma(...)egexp [21] |    670,691.806 μs |  20,632.7546 μs |    4 |    825651.6 KB |
| YantraJS          | droma(...)egexp [21] | 36,688,689.587 μs | 699,427.1780 μs |    5 | 32369644.33 KB |
|                   |                      |                   |                 |      |                |
| Jurassic          | droma(...)odern [28] |                NA |              NA |    ? |             NA |
| Jint_ParsedScript | droma(...)odern [28] |    101,866.507 μs |   3,629.9180 μs |    1 |   159829.28 KB |
| Jint              | droma(...)odern [28] |    123,313.658 μs |   3,104.9569 μs |    2 |   162294.24 KB |
| NilJS             | droma(...)odern [28] |    541,069.193 μs |   8,769.0953 μs |    3 |   767228.98 KB |
| YantraJS          | droma(...)odern [28] | 35,847,155.571 μs | 172,561.2634 μs |    4 |  32385611.1 KB |
|                   |                      |                   |                 |      |                |
| Jint_ParsedScript | droma(...)tring [21] |     58,408.696 μs |   1,643.6074 μs |    1 |    139082.1 KB |
| Jint              | droma(...)tring [21] |     61,469.163 μs |   1,580.7853 μs |    1 |    139200.5 KB |
| NilJS             | droma(...)tring [21] |    127,915.632 μs |   1,765.2459 μs |    2 |  1355063.54 KB |
| Jurassic          | droma(...)tring [21] |    206,279.473 μs |   3,791.7606 μs |    3 |  1430466.78 KB |
| YantraJS          | droma(...)tring [21] |    376,804.226 μs |  14,449.9228 μs |    4 |  3192674.39 KB |
|                   |                      |                   |                 |      |                |
| Jurassic          | droma(...)odern [28] |                NA |              NA |    ? |             NA |
| Jint_ParsedScript | droma(...)odern [28] |     60,310.751 μs |   1,289.2113 μs |    1 |    139076.8 KB |
| Jint              | droma(...)odern [28] |     62,534.443 μs |   1,277.5920 μs |    1 |   139258.06 KB |
| NilJS             | droma(...)odern [28] |    128,891.479 μs |   2,456.4796 μs |    2 |  1355159.36 KB |
| YantraJS          | droma(...)odern [28] |    381,371.804 μs |  15,538.1737 μs |    3 |  3207881.17 KB |
|                   |                      |                   |                 |      |                |
| NilJS             | droma(...)ase64 [21] |     25,901.806 μs |     290.1419 μs |    1 |    19588.63 KB |
| Jint              | droma(...)ase64 [21] |     26,999.002 μs |      25.7679 μs |    2 |     2413.52 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |     27,770.209 μs |      43.7242 μs |    3 |     2313.63 KB |
| YantraJS          | droma(...)ase64 [21] |     33,326.301 μs |     213.9267 μs |    4 |   767611.48 KB |
| Jurassic          | droma(...)ase64 [21] |     46,316.023 μs |     234.4451 μs |    5 |    73290.49 KB |
|                   |                      |                   |                 |      |                |
| Jurassic          | droma(...)odern [28] |                NA |              NA |    ? |             NA |
| NilJS             | droma(...)odern [28] |     31,607.764 μs |     105.3508 μs |    1 |    31360.22 KB |
| Jint_ParsedScript | droma(...)odern [28] |     31,771.080 μs |      46.9043 μs |    1 |      2313.8 KB |
| Jint              | droma(...)odern [28] |     32,345.964 μs |      63.9810 μs |    1 |     2414.11 KB |
| YantraJS          | droma(...)odern [28] |     32,495.503 μs |     105.6619 μs |    1 |   768828.09 KB |
|                   |                      |                   |                 |      |                |
| Jint_ParsedScript | evaluation           |          4.810 μs |       0.0214 μs |    1 |       23.76 KB |
| Jint              | evaluation           |         15.044 μs |       0.0466 μs |    2 |        34.4 KB |
| NilJS             | evaluation           |         25.168 μs |       0.0568 μs |    3 |       22.36 KB |
| YantraJS          | evaluation           |        130.185 μs |       0.9772 μs |    4 |      703.42 KB |
| Jurassic          | evaluation           |      2,089.943 μs |       9.1276 μs |    5 |      418.92 KB |
|                   |                      |                   |                 |      |                |
| Jurassic          | evaluation-modern    |                NA |              NA |    ? |             NA |
| Jint_ParsedScript | evaluation-modern    |          4.891 μs |       0.0177 μs |    1 |       23.23 KB |
| Jint              | evaluation-modern    |         14.963 μs |       0.0371 μs |    2 |       34.35 KB |
| NilJS             | evaluation-modern    |         26.147 μs |       0.0343 μs |    3 |       22.35 KB |
| YantraJS          | evaluation-modern    |        137.680 μs |       4.1797 μs |    4 |       703.4 KB |
|                   |                      |                   |                 |      |                |
| Jint_ParsedScript | linq-js              |         57.697 μs |       0.3426 μs |    1 |      192.87 KB |
| YantraJS          | linq-js              |        324.000 μs |       1.5472 μs |    2 |     1049.75 KB |
| Jint              | linq-js              |      1,212.177 μs |       8.4957 μs |    3 |     1292.06 KB |
| NilJS             | linq-js              |      4,009.505 μs |      16.5929 μs |    4 |     2739.46 KB |
| Jurassic          | linq-js              |     36,816.587 μs |     893.4156 μs |    5 |      9102.3 KB |
|                   |                      |                   |                 |      |                |
| Jint_ParsedScript | minimal              |          1.652 μs |       0.0073 μs |    1 |       15.38 KB |
| Jint              | minimal              |          2.710 μs |       0.0139 μs |    2 |       17.29 KB |
| NilJS             | minimal              |          2.790 μs |       0.0333 μs |    2 |        4.51 KB |
| YantraJS          | minimal              |        123.757 μs |       1.0633 μs |    3 |      697.62 KB |
| Jurassic          | minimal              |      2,305.389 μs |      15.8770 μs |    4 |      385.19 KB |
|                   |                      |                   |                 |      |                |
| YantraJS          | stopwatch            |     56,486.809 μs |     387.7893 μs |    1 |   215655.06 KB |
| Jurassic          | stopwatch            |    139,528.669 μs |     927.1843 μs |    2 |   156932.05 KB |
| NilJS             | stopwatch            |    140,363.862 μs |     869.1135 μs |    2 |    94876.49 KB |
| Jint_ParsedScript | stopwatch            |    174,078.086 μs |   1,209.6797 μs |    3 |    27136.64 KB |
| Jint              | stopwatch            |    180,884.300 μs |     489.5493 μs |    4 |    27168.41 KB |
|                   |                      |                   |                 |      |                |
| YantraJS          | stopwatch-modern     |     59,129.252 μs |   1,479.3084 μs |    1 |   234033.07 KB |
| Jurassic          | stopwatch-modern     |    144,917.571 μs |   1,040.8442 μs |    2 |   288625.25 KB |
| NilJS             | stopwatch-modern     |    230,308.762 μs |   3,194.4124 μs |    3 |   324502.66 KB |
| Jint              | stopwatch-modern     |    238,979.205 μs |   2,366.7945 μs |    3 |    27392.67 KB |
| Jint_ParsedScript | stopwatch-modern     |    241,053.095 μs |   2,261.3403 μs |    3 |    27360.31 KB |

Benchmarks with issues:
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [22]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [24]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [27]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=evaluation-modern]
