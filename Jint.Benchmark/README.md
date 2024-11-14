To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2024-07-24

* Jint 4.1.0
* Jurassic 3.2.7
* NiL.JS 2.5.1684
* YantraJS.Core 1.2.238

```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26120.2213)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 9.0.100-rc.2.24474.11
  [Host]     : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2


```
| Method            | FileName             | Mean             | StdDev         | Rank | Allocated      |
|------------------ |--------------------- |-----------------:|---------------:|-----:|---------------:|
| Jint              | array-stress         |     6,258.416 μs |     69.0180 μs |    1 |     6745.91 KB |
| Jint_ParsedScript | array-stress         |     6,305.207 μs |     22.5779 μs |    1 |     6711.65 KB |
| NilJS             | array-stress         |     6,365.116 μs |     32.7286 μs |    1 |     4533.76 KB |
| Jurassic          | array-stress         |    11,289.945 μs |     66.9008 μs |    2 |    11647.13 KB |
| YantraJS          | array-stress         |    15,622.275 μs |    122.4703 μs |    3 |    86542.98 KB |
|                   |                      |                  |                |      |                |
| YantraJS          | dromaeo-3d-cube      |     5,036.446 μs |     16.7497 μs |    1 |    11533.11 KB |
| NilJS             | dromaeo-3d-cube      |     7,833.009 μs |     83.2227 μs |    2 |     4693.22 KB |
| Jint              | dromaeo-3d-cube      |    16,945.656 μs |     79.5081 μs |    3 |     6216.01 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    17,305.892 μs |     62.9644 μs |    3 |     5866.96 KB |
| Jurassic          | dromaeo-3d-cube      |    55,385.633 μs |    498.1236 μs |    4 |    10668.95 KB |
|                   |                      |                  |                |      |                |
| NilJS             | dromaeo-core-eval    |     1,592.238 μs |     10.1508 μs |    1 |     1598.62 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     3,155.035 μs |      9.8738 μs |    2 |      329.51 KB |
| Jint              | dromaeo-core-eval    |     3,618.745 μs |      9.4823 μs |    3 |      352.67 KB |
| YantraJS          | dromaeo-core-eval    |     5,607.719 μs |     44.7484 μs |    4 |    36508.96 KB |
| Jurassic          | dromaeo-core-eval    |    10,380.235 μs |     27.5212 μs |    5 |        2884 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | dromaeo-object-array |    39,975.017 μs |    103.9753 μs |    1 |    96235.78 KB |
| Jint              | dromaeo-object-array |    41,173.647 μs |    130.1963 μs |    2 |    96290.71 KB |
| Jurassic          | dromaeo-object-array |    45,654.101 μs |    772.3893 μs |    3 |    25812.63 KB |
| NilJS             | dromaeo-object-array |    69,645.418 μs |    369.3297 μs |    4 |    17697.94 KB |
| YantraJS          | dromaeo-object-array |    94,736.794 μs |  2,090.4250 μs |    5 |  1265117.24 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | droma(...)egexp [21] |   107,348.317 μs |  3,032.7934 μs |    1 |   148838.48 KB |
| Jint              | droma(...)egexp [21] |   153,156.794 μs |  4,482.3833 μs |    2 |   148213.07 KB |
| NilJS             | droma(...)egexp [21] |   636,202.230 μs | 21,246.0768 μs |    3 |   769871.85 KB |
| Jurassic          | droma(...)egexp [21] |   765,289.157 μs | 21,165.8327 μs |    4 |   827951.02 KB |
| YantraJS          | droma(...)egexp [21] | 1,169,763.931 μs | 15,485.1644 μs |    5 |  1161260.91 KB |
|                   |                      |                  |                |      |                |
| Jint              | droma(...)tring [21] |   205,775.712 μs |  4,203.8622 μs |    1 |  1315786.27 KB |
| Jint_ParsedScript | droma(...)tring [21] |   212,913.393 μs |  3,883.6616 μs |    1 |  1315771.52 KB |
| NilJS             | droma(...)tring [21] |   292,842.944 μs | 20,329.4550 μs |    2 |  1377739.88 KB |
| Jurassic          | droma(...)tring [21] |   303,757.266 μs | 13,069.2515 μs |    2 |  1457922.91 KB |
| YantraJS          | droma(...)tring [21] | 1,064,952.500 μs | 12,451.9675 μs |    3 | 15728823.04 KB |
|                   |                      |                  |                |      |                |
| NilJS             | droma(...)ase64 [21] |    35,653.327 μs |     83.7744 μs |    1 |    19604.29 KB |
| Jint              | droma(...)ase64 [21] |    35,769.940 μs |    128.2475 μs |    1 |     2418.64 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    40,076.245 μs |    204.1739 μs |    2 |     2310.04 KB |
| YantraJS          | droma(...)ase64 [21] |    46,497.362 μs |    629.8903 μs |    3 |   760344.56 KB |
| Jurassic          | droma(...)ase64 [21] |    80,680.161 μs |    413.3738 μs |    4 |    73294.62 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | evaluation           |         7.182 μs |      0.0453 μs |    1 |       22.25 KB |
| Jint              | evaluation           |        20.131 μs |      0.1440 μs |    2 |       33.84 KB |
| NilJS             | evaluation           |        38.174 μs |      0.2061 μs |    3 |       23.47 KB |
| YantraJS          | evaluation           |       138.705 μs |      1.0270 μs |    4 |      917.94 KB |
| Jurassic          | evaluation           |     1,456.245 μs |      9.0765 μs |    5 |      420.34 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | linq-js              |        75.372 μs |      0.3920 μs |    1 |      175.99 KB |
| YantraJS          | linq-js              |       417.279 μs |      3.0571 μs |    2 |     1463.23 KB |
| Jint              | linq-js              |     1,482.934 μs |      7.5980 μs |    3 |     1292.48 KB |
| NilJS             | linq-js              |     6,386.099 μs |     26.9427 μs |    4 |      4121.1 KB |
| Jurassic          | linq-js              |    35,486.294 μs |    503.4330 μs |    5 |     9252.17 KB |
|                   |                      |                  |                |      |                |
| Jint_ParsedScript | minimal              |         1.985 μs |      0.0103 μs |    1 |       14.38 KB |
| Jint              | minimal              |         3.542 μs |      0.0159 μs |    2 |       16.42 KB |
| NilJS             | minimal              |         4.030 μs |      0.0235 μs |    3 |        4.81 KB |
| YantraJS          | minimal              |       132.071 μs |      1.5841 μs |    4 |      912.46 KB |
| Jurassic          | minimal              |       258.836 μs |      1.0372 μs |    5 |      386.21 KB |
|                   |                      |                  |                |      |                |
| YantraJS          | stopwatch            |    82,956.492 μs |    272.1501 μs |    1 |   232304.05 KB |
| NilJS             | stopwatch            |   178,549.100 μs |    877.5415 μs |    2 |    97360.69 KB |
| Jurassic          | stopwatch            |   186,148.924 μs |  1,284.6511 μs |    3 |   156936.57 KB |
| Jint              | stopwatch            |   275,922.121 μs |    770.1970 μs |    4 |    53035.39 KB |
| Jint_ParsedScript | stopwatch            |   286,434.581 μs |  1,284.1468 μs |    5 |    52998.98 KB |

