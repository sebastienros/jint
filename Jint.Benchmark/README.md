To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2023-08-29

* Jint main
* Jurassic 3.2.6
* NiL.JS 2.5.1665
* YantraJS.Core 1.2.179

```

BenchmarkDotNet v0.13.7, Windows 11 (10.0.23531.1001)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 8.0.100-preview.7.23376.3
  [Host]     : .NET 6.0.21 (6.0.2123.36311), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.21 (6.0.2123.36311), X64 RyuJIT AVX2


```
|            Method |             FileName |             Mean |         StdDev |           Median | Rank |      Allocated |
|------------------ |--------------------- |-----------------:|---------------:|-----------------:|-----:|---------------:|
|          YantraJS |         array-stress |     6,246.687 μs |     28.4141 μs |     6,240.110 μs |    1 |     6533.07 KB |
|             NilJS |         array-stress |     7,629.064 μs |     21.4722 μs |     7,626.207 μs |    2 |     4533.76 KB |
|              Jint |         array-stress |    11,304.127 μs |     24.2301 μs |    11,301.966 μs |    3 |      7111.4 KB |
| Jint_ParsedScript |         array-stress |    11,928.758 μs |     78.8122 μs |    11,974.534 μs |    4 |     7085.34 KB |
|          Jurassic |         array-stress |    13,568.895 μs |     49.1134 μs |    13,548.192 μs |    5 |    11646.96 KB |
|                   |                      |                  |                |                  |      |                |
|          YantraJS |      dromaeo-3d-cube |               NA |             NA |               NA |    ? |             NA |
|             NilJS |      dromaeo-3d-cube |    11,262.315 μs |     39.4986 μs |    11,253.403 μs |    1 |     4694.63 KB |
| Jint_ParsedScript |      dromaeo-3d-cube |    26,401.931 μs |     35.0936 μs |    26,404.000 μs |    2 |     5894.78 KB |
|              Jint |      dromaeo-3d-cube |    27,799.875 μs |     24.2606 μs |    27,802.781 μs |    3 |     6191.02 KB |
|          Jurassic |      dromaeo-3d-cube |    49,539.274 μs |    137.7463 μs |    49,615.655 μs |    4 |    10671.74 KB |
|                   |                      |                  |                |                  |      |                |
|             NilJS |    dromaeo-core-eval |     2,638.637 μs |      8.3816 μs |     2,642.709 μs |    1 |     1598.78 KB |
|              Jint |    dromaeo-core-eval |     5,986.834 μs |     23.0157 μs |     5,978.144 μs |    2 |      350.31 KB |
| Jint_ParsedScript |    dromaeo-core-eval |     6,049.918 μs |     47.3595 μs |     6,052.202 μs |    2 |      331.04 KB |
|          YantraJS |    dromaeo-core-eval |    15,121.048 μs |     33.6780 μs |    15,127.921 μs |    3 |    36547.42 KB |
|          Jurassic |    dromaeo-core-eval |    16,809.782 μs |      6.8618 μs |    16,811.078 μs |    4 |     2901.46 KB |
|                   |                      |                  |                |                  |      |                |
|          Jurassic | dromaeo-object-array |    50,652.934 μs |    193.8856 μs |    50,633.500 μs |    1 |     25814.2 KB |
|          YantraJS | dromaeo-object-array |    59,426.130 μs |    492.9887 μs |    59,443.689 μs |    2 |    24745.94 KB |
| Jint_ParsedScript | dromaeo-object-array |    66,402.409 μs |    254.3555 μs |    66,323.750 μs |    3 |   100747.98 KB |
|              Jint | dromaeo-object-array |    67,909.973 μs |    374.0128 μs |    67,856.262 μs |    4 |   100792.72 KB |
|             NilJS | dromaeo-object-array |    76,716.933 μs |    335.0924 μs |    76,717.700 μs |    5 |    17698.13 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript | droma(...)egexp [21] |   298,212.421 μs |  4,821.1841 μs |   297,469.850 μs |    1 |   165381.18 KB |
|              Jint | droma(...)egexp [21] |   308,929.771 μs |  7,106.6296 μs |   307,314.500 μs |    2 |   168713.91 KB |
|             NilJS | droma(...)egexp [21] |   603,665.040 μs |  4,970.4874 μs |   605,048.300 μs |    3 |   768274.13 KB |
|          Jurassic | droma(...)egexp [21] |   841,737.444 μs | 30,514.3096 μs |   839,741.400 μs |    4 |   825832.59 KB |
|          YantraJS | droma(...)egexp [21] | 1,202,044.984 μs | 25,781.0535 μs | 1,198,305.600 μs |    5 |   941580.41 KB |
|                   |                      |                  |                |                  |      |                |
|             NilJS | droma(...)tring [21] |   417,303.358 μs | 13,929.0807 μs |   418,108.100 μs |    1 |  1377743.66 KB |
| Jint_ParsedScript | droma(...)tring [21] |   563,559.551 μs | 33,924.5891 μs |   551,658.750 μs |    2 |  1322031.27 KB |
|              Jint | droma(...)tring [21] |   572,808.661 μs | 30,017.2335 μs |   570,151.650 μs |    2 |   1322176.1 KB |
|          Jurassic | droma(...)tring [21] |   692,137.075 μs | 28,047.3722 μs |   698,963.900 μs |    3 |  1457949.11 KB |
|          YantraJS | droma(...)tring [21] | 4,060,814.093 μs | 60,908.6909 μs | 4,079,384.300 μs |    4 | 15718148.67 KB |
|                   |                      |                  |                |                  |      |                |
|             NilJS | droma(...)ase64 [21] |    47,816.450 μs |    138.7136 μs |    47,770.455 μs |    1 |    19605.27 KB |
|              Jint | droma(...)ase64 [21] |    65,790.989 μs |    272.8843 μs |    65,817.512 μs |    2 |     6772.48 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    66,114.687 μs |    146.3463 μs |    66,118.562 μs |    2 |     6680.29 KB |
|          Jurassic | droma(...)ase64 [21] |    84,478.585 μs |    323.0873 μs |    84,454.725 μs |    3 |     74321.7 KB |
|          YantraJS | droma(...)ase64 [21] |   235,350.207 μs |    955.7123 μs |   235,605.333 μs |    4 |   760629.53 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript |           evaluation |        13.655 μs |      0.0306 μs |        13.663 μs |    1 |       26.46 KB |
|              Jint |           evaluation |        34.425 μs |      0.1069 μs |        34.444 μs |    2 |       35.89 KB |
|             NilJS |           evaluation |        56.276 μs |      0.1443 μs |        56.288 μs |    3 |       23.47 KB |
|          YantraJS |           evaluation |       129.051 μs |      0.5500 μs |       129.043 μs |    4 |      462.57 KB |
|          Jurassic |           evaluation |     1,611.643 μs |      2.3082 μs |     1,611.278 μs |    5 |      420.41 KB |
|                   |                      |                  |                |                  |      |                |
|          YantraJS |              linq-js |               NA |             NA |               NA |    ? |             NA |
| Jint_ParsedScript |              linq-js |       116.014 μs |      0.6197 μs |       115.770 μs |    1 |      215.13 KB |
|              Jint |              linq-js |     2,248.864 μs |      6.9181 μs |     2,247.281 μs |    2 |     1274.64 KB |
|             NilJS |              linq-js |     9,450.914 μs |     26.1239 μs |     9,452.424 μs |    3 |     4127.79 KB |
|          Jurassic |              linq-js |    46,341.837 μs |    310.1326 μs |    46,370.700 μs |    4 |     9305.52 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript |              minimal |         3.203 μs |      0.0178 μs |         3.196 μs |    1 |        12.9 KB |
|              Jint |              minimal |         5.230 μs |      0.0169 μs |         5.229 μs |    2 |       14.34 KB |
|             NilJS |              minimal |         6.055 μs |      0.0276 μs |         6.041 μs |    3 |        4.81 KB |
|          YantraJS |              minimal |       123.197 μs |      2.7249 μs |       122.828 μs |    4 |      458.56 KB |
|          Jurassic |              minimal |       297.292 μs |      0.6073 μs |       297.435 μs |    5 |      386.24 KB |
|                   |                      |                  |                |                  |      |                |
|          YantraJS |            stopwatch |   112,530.838 μs |    449.4615 μs |   112,460.320 μs |    1 |   258622.36 KB |
|          Jurassic |            stopwatch |   247,085.684 μs |  1,162.2628 μs |   246,727.867 μs |    2 |   156937.08 KB |
|             NilJS |            stopwatch |   295,878.240 μs |  2,000.4676 μs |   295,209.200 μs |    3 |    97361.17 KB |
| Jint_ParsedScript |            stopwatch |   471,369.071 μs |  1,578.5815 μs |   471,148.200 μs |    4 |    53023.28 KB |
|              Jint |            stopwatch |   472,028.947 μs |  3,209.3311 μs |   471,611.400 μs |    4 |    53044.52 KB |

Benchmarks with issues:
EngineComparisonBenchmark.YantraJS: DefaultJob [FileName=dromaeo-3d-cube]
EngineComparisonBenchmark.YantraJS: DefaultJob [FileName=linq-js]
