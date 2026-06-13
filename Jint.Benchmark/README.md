To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.
* YantraJS.Core 1.2.404 has a severe regression on `dromaeo-object-regexp` (~35 s/op, ~32 GB allocated); earlier 1.2.344 ran it at ~1 s/op.
* Measured against `main` plus the pending per-call closure-allocation fix ("Avoid per-call closure allocation in EvaluateBody"), which removes a C#-compiler display class that was allocated on every JS function call. The headline mover is `stopwatch`/`stopwatch-modern`: allocations drop ~45% (27.2 → 14.8 MB/op) and time ~6–10% (`stopwatch` ~170→160 ms, `stopwatch-modern` ~228→212 ms), since their allocation was ~45% that per-call closure and is now almost entirely the mandatory `new Date()` result objects. Remaining row-to-row differences versus the previous table are within ~5–7% run-to-run variance (the fix only touches per-call allocation, and `stopwatch` is the call-dominated benchmark). Host runtime is unchanged at .NET 10.0.9.

Last updated 2026-06-14

* Jint main (development build)
* Jurassic 3.2.9
* NiL.JS 2.6.1722
* YantraJS.Core 1.2.404

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method            | FileName             | Mean              | StdDev          | Median            | Rank | Allocated      |
|------------------ |--------------------- |------------------:|----------------:|------------------:|-----:|---------------:|
| Jint_ParsedScript | array-stress         |      2,993.109 μs |      25.2279 μs |      2,991.567 μs |    1 |     1281.42 KB |
| Jint              | array-stress         |      3,143.497 μs |      41.7848 μs |      3,143.370 μs |    2 |     1310.21 KB |
| YantraJS          | array-stress         |      3,867.941 μs |      56.3724 μs |      3,847.888 μs |    3 |    27043.38 KB |
| NilJS             | array-stress         |      5,063.172 μs |      42.5054 μs |      5,060.857 μs |    4 |     4521.19 KB |
| Jurassic          | array-stress         |      9,402.396 μs |     114.2166 μs |      9,387.136 μs |    5 |    11644.86 KB |
|                   |                      |                   |                 |                   |      |                |
| YantraJS          | dromaeo-3d-cube      |      2,568.094 μs |      11.0935 μs |      2,571.802 μs |    1 |      7591.5 KB |
| NilJS             | dromaeo-3d-cube      |      6,487.514 μs |      31.4042 μs |      6,496.339 μs |    2 |     4903.32 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |      8,914.454 μs |     179.4153 μs |      8,856.951 μs |    3 |     2639.66 KB |
| Jint              | dromaeo-3d-cube      |      9,208.152 μs |      94.7846 μs |      9,168.230 μs |    3 |     2943.66 KB |
| Jurassic          | dromaeo-3d-cube      |     57,738.124 μs |     143.0289 μs |     57,751.489 μs |    4 |    10654.77 KB |
|                   |                      |                   |                 |                   |      |                |
| Jurassic          | droma(...)odern [22] |                NA |              NA |                NA |    ? |             NA |
| YantraJS          | droma(...)odern [22] |      2,530.923 μs |      12.7322 μs |      2,527.409 μs |    1 |     7509.73 KB |
| NilJS             | droma(...)odern [22] |      7,430.121 μs |     116.2245 μs |      7,384.412 μs |    2 |     5977.95 KB |
| Jint_ParsedScript | droma(...)odern [22] |      8,930.302 μs |      49.4731 μs |      8,912.070 μs |    3 |     2639.51 KB |
| Jint              | droma(...)odern [22] |      9,332.933 μs |     136.6094 μs |      9,283.054 μs |    4 |     2943.33 KB |
|                   |                      |                   |                 |                   |      |                |
| NilJS             | dromaeo-core-eval    |      1,195.842 μs |       5.2076 μs |      1,195.375 μs |    1 |      1577.1 KB |
| Jint              | dromaeo-core-eval    |      2,632.833 μs |      10.8894 μs |      2,631.187 μs |    2 |      352.39 KB |
| Jint_ParsedScript | dromaeo-core-eval    |      2,638.918 μs |       6.0880 μs |      2,640.220 μs |    2 |      331.99 KB |
| YantraJS          | dromaeo-core-eval    |      4,777.323 μs |      29.5579 μs |      4,781.716 μs |    3 |    35784.73 KB |
| Jurassic          | dromaeo-core-eval    |     16,559.516 μs |      59.4551 μs |     16,566.167 μs |    4 |     2876.04 KB |
|                   |                      |                   |                 |                   |      |                |
| Jurassic          | droma(...)odern [24] |                NA |              NA |                NA |    ? |             NA |
| NilJS             | droma(...)odern [24] |      1,559.343 μs |      13.7291 μs |      1,562.066 μs |    1 |     1575.94 KB |
| Jint_ParsedScript | droma(...)odern [24] |      2,457.570 μs |       6.8536 μs |      2,455.846 μs |    2 |      331.97 KB |
| Jint              | droma(...)odern [24] |      2,479.334 μs |      43.1280 μs |      2,458.892 μs |    2 |      351.63 KB |
| YantraJS          | droma(...)odern [24] |      4,929.588 μs |     143.9541 μs |      4,901.606 μs |    3 |    35784.84 KB |
|                   |                      |                   |                 |                   |      |                |
| Jint_ParsedScript | dromaeo-object-array |     16,329.167 μs |     374.2675 μs |     16,274.797 μs |    1 |     9984.76 KB |
| Jint              | dromaeo-object-array |     16,569.806 μs |     245.8515 μs |     16,490.789 μs |    1 |    10033.12 KB |
| Jurassic          | dromaeo-object-array |     37,072.922 μs |     208.0501 μs |     37,043.292 μs |    2 |    25808.85 KB |
| NilJS             | dromaeo-object-array |     53,952.598 μs |     256.8411 μs |     53,953.990 μs |    3 |    17862.17 KB |
| YantraJS          | dromaeo-object-array |    226,896.145 μs |   9,847.3403 μs |    227,124.800 μs |    4 |   379905.58 KB |
|                   |                      |                   |                 |                   |      |                |
| Jurassic          | droma(...)odern [27] |                NA |              NA |                NA |    ? |             NA |
| Jint              | droma(...)odern [27] |     17,152.192 μs |     165.6067 μs |     17,092.744 μs |    1 |    10034.53 KB |
| Jint_ParsedScript | droma(...)odern [27] |     17,981.349 μs |     241.3758 μs |     18,120.119 μs |    1 |      9987.2 KB |
| NilJS             | droma(...)odern [27] |     55,890.441 μs |   1,026.0937 μs |     55,574.444 μs |    2 |    17863.19 KB |
| YantraJS          | droma(...)odern [27] |    281,385.414 μs |   6,999.3720 μs |    281,754.100 μs |    3 |   383691.48 KB |
|                   |                      |                   |                 |                   |      |                |
| Jint_ParsedScript | droma(...)egexp [21] |     99,701.506 μs |   4,418.8549 μs |     99,219.137 μs |    1 |   156630.02 KB |
| Jint              | droma(...)egexp [21] |    127,216.916 μs |   6,818.2405 μs |    126,009.550 μs |    2 |   157705.99 KB |
| NilJS             | droma(...)egexp [21] |    537,558.317 μs |  11,241.1040 μs |    538,779.100 μs |    3 |   767720.89 KB |
| Jurassic          | droma(...)egexp [21] |    714,439.394 μs |  14,944.9450 μs |    714,756.950 μs |    4 |    824633.7 KB |
| YantraJS          | droma(...)egexp [21] | 35,308,784.407 μs | 491,721.1353 μs | 35,194,090.350 μs |    5 | 32378797.27 KB |
|                   |                      |                   |                 |                   |      |                |
| Jurassic          | droma(...)odern [28] |                NA |              NA |                NA |    ? |             NA |
| Jint_ParsedScript | droma(...)odern [28] |     98,298.807 μs |   3,272.9549 μs |     98,740.230 μs |    1 |    157581.6 KB |
| Jint              | droma(...)odern [28] |    118,436.994 μs |   6,355.8053 μs |    116,570.400 μs |    2 |   156216.14 KB |
| NilJS             | droma(...)odern [28] |    524,484.008 μs |   6,011.3592 μs |    524,514.600 μs |    3 |   768633.45 KB |
| YantraJS          | droma(...)odern [28] | 34,937,835.673 μs | 225,383.0014 μs | 34,890,311.000 μs |    4 | 32382205.38 KB |
|                   |                      |                   |                 |                   |      |                |
| Jint_ParsedScript | droma(...)tring [21] |     50,082.031 μs |     565.5997 μs |     49,990.340 μs |    1 |    21490.43 KB |
| Jint              | droma(...)tring [21] |     52,012.064 μs |   1,385.9785 μs |     52,582.050 μs |    1 |    21654.05 KB |
| NilJS             | droma(...)tring [21] |    127,219.660 μs |   2,120.1717 μs |    126,751.625 μs |    2 |  1355036.24 KB |
| Jurassic          | droma(...)tring [21] |    204,655.588 μs |   6,332.0368 μs |    205,154.200 μs |    3 |  1430585.15 KB |
| YantraJS          | droma(...)tring [21] |    367,377.658 μs |  19,208.3684 μs |    367,828.300 μs |    4 |  3184898.55 KB |
|                   |                      |                   |                 |                   |      |                |
| Jurassic          | droma(...)odern [28] |                NA |              NA |                NA |    ? |             NA |
| Jint_ParsedScript | droma(...)odern [28] |     49,937.337 μs |     248.3098 μs |     50,004.210 μs |    1 |     21480.7 KB |
| Jint              | droma(...)odern [28] |     58,757.297 μs |     244.3200 μs |     58,817.722 μs |    2 |    21677.15 KB |
| NilJS             | droma(...)odern [28] |    128,144.988 μs |     859.8993 μs |    128,087.850 μs |    3 |   1355078.8 KB |
| YantraJS          | droma(...)odern [28] |    376,377.518 μs |  15,223.3879 μs |    374,951.100 μs |    4 |  3172782.68 KB |
|                   |                      |                   |                 |                   |      |                |
| NilJS             | droma(...)ase64 [21] |     24,917.261 μs |     210.9999 μs |     25,023.702 μs |    1 |    19588.63 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |     27,508.836 μs |      71.0956 μs |     27,484.927 μs |    2 |     2312.48 KB |
| Jint              | droma(...)ase64 [21] |     27,610.750 μs |      55.0144 μs |     27,611.650 μs |    2 |     2412.38 KB |
| YantraJS          | droma(...)ase64 [21] |     33,116.372 μs |     359.2148 μs |     33,157.677 μs |    3 |   767611.48 KB |
| Jurassic          | droma(...)ase64 [21] |     48,697.885 μs |     574.1673 μs |     48,735.927 μs |    4 |    73289.43 KB |
|                   |                      |                   |                 |                   |      |                |
| Jurassic          | droma(...)odern [28] |                NA |              NA |                NA |    ? |             NA |
| NilJS             | droma(...)odern [28] |     31,911.753 μs |     178.0928 μs |     31,960.059 μs |    1 |    31360.33 KB |
| Jint              | droma(...)odern [28] |     31,999.853 μs |      50.9553 μs |     32,022.334 μs |    1 |     2413.34 KB |
| Jint_ParsedScript | droma(...)odern [28] |     32,932.052 μs |      87.0185 μs |     32,915.438 μs |    2 |     2313.03 KB |
| YantraJS          | droma(...)odern [28] |     34,390.843 μs |     395.9357 μs |     34,302.640 μs |    3 |   768828.11 KB |
|                   |                      |                   |                 |                   |      |                |
| Jint_ParsedScript | evaluation           |          4.707 μs |       0.0639 μs |          4.702 μs |    1 |       23.48 KB |
| Jint              | evaluation           |         15.197 μs |       0.0439 μs |         15.192 μs |    2 |       34.13 KB |
| NilJS             | evaluation           |         25.295 μs |       0.0460 μs |         25.306 μs |    3 |       22.36 KB |
| YantraJS          | evaluation           |        129.118 μs |       0.8336 μs |        129.078 μs |    4 |      703.42 KB |
| Jurassic          | evaluation           |      2,070.375 μs |       6.6568 μs |      2,072.145 μs |    5 |      418.81 KB |
|                   |                      |                   |                 |                   |      |                |
| Jurassic          | evaluation-modern    |                NA |              NA |                NA |    ? |             NA |
| Jint_ParsedScript | evaluation-modern    |          5.017 μs |       0.0172 μs |          5.020 μs |    1 |       23.05 KB |
| Jint              | evaluation-modern    |         14.625 μs |       0.0713 μs |         14.659 μs |    2 |       34.17 KB |
| NilJS             | evaluation-modern    |         26.091 μs |       0.0689 μs |         26.076 μs |    3 |       22.35 KB |
| YantraJS          | evaluation-modern    |        130.148 μs |       0.7496 μs |        130.237 μs |    4 |       703.4 KB |
|                   |                      |                   |                 |                   |      |                |
| Jint_ParsedScript | linq-js              |         55.093 μs |       0.4440 μs |         55.041 μs |    1 |       178.1 KB |
| YantraJS          | linq-js              |        329.977 μs |       1.6277 μs |        330.201 μs |    2 |     1049.75 KB |
| Jint              | linq-js              |      1,203.389 μs |       4.4981 μs |      1,202.915 μs |    3 |      1277.3 KB |
| NilJS             | linq-js              |      3,906.777 μs |      10.1862 μs |      3,905.809 μs |    4 |     2739.46 KB |
| Jurassic          | linq-js              |     36,771.948 μs |     686.2264 μs |     36,803.096 μs |    5 |     9102.12 KB |
|                   |                      |                   |                 |                   |      |                |
| Jint_ParsedScript | minimal              |          1.645 μs |       0.0140 μs |          1.650 μs |    1 |       15.39 KB |
| Jint              | minimal              |          2.686 μs |       0.0116 μs |          2.687 μs |    2 |        17.3 KB |
| NilJS             | minimal              |          2.760 μs |       0.0126 μs |          2.756 μs |    2 |        4.51 KB |
| YantraJS          | minimal              |        123.469 μs |       1.2847 μs |        123.663 μs |    3 |      697.62 KB |
| Jurassic          | minimal              |      2,271.842 μs |       5.0618 μs |      2,272.215 μs |    4 |      385.19 KB |
|                   |                      |                   |                 |                   |      |                |
| YantraJS          | stopwatch            |     54,594.339 μs |     305.7260 μs |     54,623.650 μs |    1 |   215655.05 KB |
| NilJS             | stopwatch            |    133,335.854 μs |     602.1139 μs |    133,133.975 μs |    2 |    94876.49 KB |
| Jurassic          | stopwatch            |    138,792.829 μs |     962.6176 μs |    138,860.013 μs |    3 |   156932.58 KB |
| Jint_ParsedScript | stopwatch            |    158,097.827 μs |     593.9291 μs |    158,040.825 μs |    4 |    14769.26 KB |
| Jint              | stopwatch            |    159,522.448 μs |     650.5848 μs |    159,425.500 μs |    4 |    14801.02 KB |
|                   |                      |                   |                 |                   |      |                |
| YantraJS          | stopwatch-modern     |     59,466.751 μs |     257.5186 μs |     59,538.789 μs |    1 |   234033.07 KB |
| Jurassic          | stopwatch-modern     |    143,815.895 μs |   1,588.2697 μs |    143,244.350 μs |    2 |   288625.25 KB |
| NilJS             | stopwatch-modern     |    205,668.147 μs |   1,732.5339 μs |    205,851.333 μs |    3 |   324502.66 KB |
| Jint_ParsedScript | stopwatch-modern     |    208,530.017 μs |   1,116.1597 μs |    208,101.717 μs |    3 |     14769.8 KB |
| Jint              | stopwatch-modern     |    212,248.296 μs |     645.7171 μs |    212,089.100 μs |    3 |    14802.16 KB |
```
