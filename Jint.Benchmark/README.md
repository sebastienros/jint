To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.
* YantraJS.Core 1.2.404 has a severe regression on `dromaeo-object-regexp` (~35 s/op, ~32 GB allocated); earlier 1.2.344 ran it at ~1 s/op.

Last updated 2026-06-07

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
| Method            | FileName             | Mean              | StdDev          | Rank | Allocated     |
|------------------ |--------------------- |------------------:|----------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress         |      3,884.565 μs |      10.6826 μs |    1 |    1279.92 KB |
| Jint              | array-stress         |      3,986.551 μs |      71.0641 μs |    1 |    1308.71 KB |
| YantraJS          | array-stress         |      4,216.783 μs |      33.3669 μs |    2 |   27043.37 KB |
| NilJS             | array-stress         |      4,986.759 μs |       9.0188 μs |    3 |    4521.19 KB |
| Jurassic          | array-stress         |      9,443.106 μs |      71.1875 μs |    4 |   11644.87 KB |
|                   |                      |                   |                 |      |               |
| YantraJS          | dromaeo-3d-cube      |      2,747.828 μs |      22.3558 μs |    1 |     7591.5 KB |
| NilJS             | dromaeo-3d-cube      |      6,274.587 μs |      32.2313 μs |    2 |    4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |     13,487.736 μs |     264.0475 μs |    3 |    5609.45 KB |
| Jint              | dromaeo-3d-cube      |     13,560.241 μs |      64.3115 μs |    3 |    5912.66 KB |
| Jurassic          | dromaeo-3d-cube      |     56,833.242 μs |     459.0482 μs |    4 |   10654.76 KB |
|                   |                      |                   |                 |      |               |
| Jurassic          | droma(...)odern [22] |                NA |              NA |    ? |            NA |
| YantraJS          | droma(...)odern [22] |      2,723.184 μs |      39.9377 μs |    1 |    7509.73 KB |
| NilJS             | droma(...)odern [22] |      7,418.213 μs |      32.1755 μs |    2 |    5977.95 KB |
| Jint              | droma(...)odern [22] |     13,695.182 μs |      50.8233 μs |    3 |    5911.97 KB |
| Jint_ParsedScript | droma(...)odern [22] |     13,877.620 μs |      67.2606 μs |    3 |    5608.94 KB |
|                   |                      |                   |                 |      |               |
| NilJS             | dromaeo-core-eval    |      1,290.748 μs |       5.9023 μs |    1 |     1577.1 KB |
| Jint_ParsedScript | dromaeo-core-eval    |      2,514.161 μs |       8.7448 μs |    2 |     326.28 KB |
| Jint              | dromaeo-core-eval    |      2,541.992 μs |      14.6324 μs |    2 |     346.68 KB |
| YantraJS          | dromaeo-core-eval    |      5,374.960 μs |      78.3009 μs |    3 |   35784.73 KB |
| Jurassic          | dromaeo-core-eval    |     17,589.345 μs |     121.0171 μs |    4 |    2876.11 KB |
|                   |                      |                   |                 |      |               |
| Jurassic          | droma(...)odern [24] |                NA |              NA |    ? |            NA |
| NilJS             | droma(...)odern [24] |      1,556.632 μs |       8.2707 μs |    1 |    1575.94 KB |
| Jint_ParsedScript | droma(...)odern [24] |      2,533.579 μs |       9.8256 μs |    2 |     325.98 KB |
| Jint              | droma(...)odern [24] |      2,549.765 μs |      15.4544 μs |    2 |     345.64 KB |
| YantraJS          | droma(...)odern [24] |      5,352.394 μs |      66.1533 μs |    3 |   35784.84 KB |
|                   |                      |                   |                 |      |               |
| Jint_ParsedScript | dromaeo-object-array |     17,825.325 μs |     387.7687 μs |    1 |    9984.48 KB |
| Jint              | dromaeo-object-array |     17,978.911 μs |     251.0557 μs |    1 |   10032.84 KB |
| Jurassic          | dromaeo-object-array |     36,210.725 μs |     404.2246 μs |    2 |   25809.34 KB |
| NilJS             | dromaeo-object-array |     53,545.809 μs |     500.6367 μs |    3 |   17862.17 KB |
| YantraJS          | dromaeo-object-array |    216,796.043 μs |  11,120.7582 μs |    4 |  379906.64 KB |
|                   |                      |                   |                 |      |               |
| Jurassic          | droma(...)odern [27] |                NA |              NA |    ? |            NA |
| Jint              | droma(...)odern [27] |     18,639.340 μs |     165.7005 μs |    1 |   10033.55 KB |
| Jint_ParsedScript | droma(...)odern [27] |     20,424.429 μs |     253.1990 μs |    2 |    9986.22 KB |
| NilJS             | droma(...)odern [27] |     64,017.370 μs |     238.9562 μs |    3 |   17863.19 KB |
| YantraJS          | droma(...)odern [27] |    283,120.053 μs |  12,313.6681 μs |    4 |  383693.85 KB |
|                   |                      |                   |                 |      |               |
| Jint_ParsedScript | droma(...)egexp [21] |    104,866.200 μs |   3,980.1898 μs |    1 |  161367.87 KB |
| Jint              | droma(...)egexp [21] |    147,594.515 μs |   9,129.6071 μs |    2 |   163904.6 KB |
| NilJS             | droma(...)egexp [21] |    551,121.686 μs |   8,640.5860 μs |    3 |  766159.61 KB |
| Jurassic          | droma(...)egexp [21] |    708,597.942 μs |  15,483.9177 μs |    4 |  827428.66 KB |
| YantraJS          | droma(...)egexp [21] | 35,978,791.292 μs | 978,611.1967 μs |    5 | 32377831.3 KB |
|                   |                      |                   |                 |      |               |
| Jurassic          | droma(...)odern [28] |                NA |              NA |    ? |            NA |
| Jint_ParsedScript | droma(...)odern [28] |    107,799.821 μs |   5,178.8734 μs |    1 |  160858.64 KB |
| Jint              | droma(...)odern [28] |    157,611.898 μs |   6,246.9206 μs |    2 |  162076.97 KB |
| NilJS             | droma(...)odern [28] |    549,603.747 μs |   6,800.0527 μs |    3 |  767001.06 KB |
| YantraJS          | droma(...)odern [28] | 36,376,792.713 μs | 494,569.8550 μs |    4 | 32380887.8 KB |
|                   |                      |                   |                 |      |               |
| NilJS             | droma(...)tring [21] |    137,230.885 μs |   2,923.8759 μs |    1 | 1355055.44 KB |
| Jint_ParsedScript | droma(...)tring [21] |    164,274.957 μs |   3,762.6438 μs |    2 | 1302019.91 KB |
| Jint              | droma(...)tring [21] |    170,227.214 μs |   7,808.5779 μs |    2 |  1302405.1 KB |
| Jurassic          | droma(...)tring [21] |    219,459.004 μs |   3,982.5453 μs |    3 |  1430525.1 KB |
| YantraJS          | droma(...)tring [21] |    389,559.040 μs |  16,800.3358 μs |    4 | 3186121.89 KB |
|                   |                      |                   |                 |      |               |
| Jurassic          | droma(...)odern [28] |                NA |              NA |    ? |            NA |
| NilJS             | droma(...)odern [28] |    136,300.400 μs |   2,761.7467 μs |    1 | 1354933.21 KB |
| Jint_ParsedScript | droma(...)odern [28] |    163,456.076 μs |   5,852.0264 μs |    2 |  1302207.8 KB |
| Jint              | droma(...)odern [28] |    170,694.620 μs |   7,631.1554 μs |    2 | 1302174.35 KB |
| YantraJS          | droma(...)odern [28] |    401,293.472 μs |  17,838.9865 μs |    3 | 3240349.95 KB |
|                   |                      |                   |                 |      |               |
| NilJS             | droma(...)ase64 [21] |     27,299.719 μs |     153.3658 μs |    1 |   19588.63 KB |
| Jint              | droma(...)ase64 [21] |     28,206.942 μs |      25.8015 μs |    2 |    2410.55 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |     29,256.310 μs |      66.9853 μs |    3 |    2310.65 KB |
| YantraJS          | droma(...)ase64 [21] |     35,838.831 μs |     470.3548 μs |    4 |  767611.48 KB |
| Jurassic          | droma(...)ase64 [21] |     48,403.600 μs |     217.9071 μs |    5 |   73290.34 KB |
|                   |                      |                   |                 |      |               |
| Jurassic          | droma(...)odern [28] |                NA |              NA |    ? |            NA |
| Jint              | droma(...)odern [28] |     32,961.213 μs |      61.5842 μs |    1 |    2411.11 KB |
| Jint_ParsedScript | droma(...)odern [28] |     34,037.507 μs |      60.9581 μs |    2 |     2310.8 KB |
| YantraJS          | droma(...)odern [28] |     35,257.440 μs |     280.0280 μs |    3 |  768827.59 KB |
| NilJS             | droma(...)odern [28] |     35,585.568 μs |     780.2112 μs |    3 |    31360.5 KB |
|                   |                      |                   |                 |      |               |
| Jint_ParsedScript | evaluation           |          5.290 μs |       0.0580 μs |    1 |      23.31 KB |
| Jint              | evaluation           |         15.850 μs |       0.0352 μs |    2 |      33.95 KB |
| NilJS             | evaluation           |         26.316 μs |       0.0905 μs |    3 |      22.36 KB |
| YantraJS          | evaluation           |        141.290 μs |       1.6104 μs |    4 |     703.42 KB |
| Jurassic          | evaluation           |      2,163.937 μs |      18.0548 μs |    5 |     418.92 KB |
|                   |                      |                   |                 |      |               |
| Jurassic          | evaluation-modern    |                NA |              NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern    |          5.393 μs |       0.0599 μs |    1 |      22.76 KB |
| Jint              | evaluation-modern    |         15.484 μs |       0.0620 μs |    2 |      33.88 KB |
| NilJS             | evaluation-modern    |         26.697 μs |       0.0891 μs |    3 |      22.35 KB |
| YantraJS          | evaluation-modern    |        138.235 μs |       1.2082 μs |    4 |      703.4 KB |
|                   |                      |                   |                 |      |               |
| Jint_ParsedScript | linq-js              |         59.927 μs |       0.1356 μs |    1 |     188.51 KB |
| YantraJS          | linq-js              |        347.204 μs |       2.3057 μs |    2 |    1049.75 KB |
| Jint              | linq-js              |      1,239.217 μs |       2.2829 μs |    3 |     1287.7 KB |
| NilJS             | linq-js              |      4,222.760 μs |      20.8878 μs |    4 |    2739.46 KB |
| Jurassic          | linq-js              |     36,770.770 μs |     467.2782 μs |    5 |    9102.27 KB |
|                   |                      |                   |                 |      |               |
| Jint_ParsedScript | minimal              |          1.759 μs |       0.0127 μs |    1 |      15.32 KB |
| NilJS             | minimal              |          2.953 μs |       0.0101 μs |    2 |       4.51 KB |
| Jint              | minimal              |          2.958 μs |       0.0191 μs |    2 |      17.23 KB |
| YantraJS          | minimal              |        135.976 μs |       0.8930 μs |    3 |     697.62 KB |
| Jurassic          | minimal              |      2,328.425 μs |       3.2468 μs |    4 |     385.19 KB |
|                   |                      |                   |                 |      |               |
| YantraJS          | stopwatch            |     58,938.518 μs |     476.0077 μs |    1 |  215655.06 KB |
| NilJS             | stopwatch            |    141,258.753 μs |     502.1725 μs |    2 |   94876.49 KB |
| Jurassic          | stopwatch            |    143,498.668 μs |     464.8944 μs |    2 |  156932.58 KB |
| Jint              | stopwatch            |    205,396.024 μs |     526.3967 μs |    3 |   27167.16 KB |
| Jint_ParsedScript | stopwatch            |    215,217.887 μs |   1,341.9642 μs |    4 |    27135.4 KB |
|                   |                      |                   |                 |      |               |
| YantraJS          | stopwatch-modern     |     61,571.408 μs |     417.5061 μs |    1 |  234033.07 KB |
| Jurassic          | stopwatch-modern     |    151,925.363 μs |     549.7888 μs |    2 |  288624.72 KB |
| NilJS             | stopwatch-modern     |    221,678.182 μs |   1,068.5886 μs |    3 |  324502.66 KB |
| Jint              | stopwatch-modern     |    261,272.775 μs |   1,126.7463 μs |    4 |   27391.55 KB |
| Jint_ParsedScript | stopwatch-modern     |    266,677.773 μs |   1,039.2424 μs |    4 |   27359.13 KB |

Benchmarks with issues:
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [22]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [24]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [27]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=droma(...)odern [28]]
  EngineComparisonBenchmark.Jurassic: DefaultJob [FileName=evaluation-modern]
