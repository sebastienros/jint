To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2024-01-07

* Jint main
* Jurassic 3.2.7
* NiL.JS 2.5.1677
* YantraJS.Core 1.2.206

```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.23612.1000)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method            | FileName             | Mean             | StdDev         | Median           | Rank | Allocated      |
|------------------ |--------------------- |-----------------:|---------------:|-----------------:|-----:|---------------:|
| Jint              | array-stress         |     5,701.482 μs |     51.0031 μs |     5,679.317 μs |    1 |     7013.19 KB |
| Jint_ParsedScript | array-stress         |     5,769.485 μs |     18.1258 μs |     5,773.903 μs |    2 |     6992.01 KB |
| NilJS             | array-stress         |     6,734.868 μs |     32.5135 μs |     6,736.069 μs |    3 |     4533.76 KB |
| YantraJS          | array-stress         |     7,306.284 μs |    262.7479 μs |     7,307.334 μs |    4 |     8073.61 KB |
| Jurassic          | array-stress         |    11,089.906 μs |     71.6310 μs |    11,067.920 μs |    5 |    11647.13 KB |
|                   |                      |                  |                |                  |      |                |
| YantraJS          | dromaeo-3d-cube      |     5,297.465 μs |     43.8914 μs |     5,316.661 μs |    1 |    11412.16 KB |
| NilJS             | dromaeo-3d-cube      |     7,853.636 μs |     19.0195 μs |     7,859.741 μs |    2 |     4693.22 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    19,773.251 μs |     43.0614 μs |    19,783.850 μs |    3 |     5951.47 KB |
| Jint              | dromaeo-3d-cube      |    20,295.461 μs |     20.8509 μs |    20,298.377 μs |    4 |     6208.78 KB |
| Jurassic          | dromaeo-3d-cube      |    54,596.781 μs |    286.3963 μs |    54,645.300 μs |    5 |    10668.95 KB |
|                   |                      |                  |                |                  |      |                |
| NilJS             | dromaeo-core-eval    |     1,610.734 μs |      9.8954 μs |     1,608.199 μs |    1 |     1598.62 KB |
| Jint              | dromaeo-core-eval    |     3,408.444 μs |      5.5157 μs |     3,408.544 μs |    2 |      340.16 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     3,447.974 μs |    110.0602 μs |     3,510.933 μs |    3 |      323.11 KB |
| YantraJS          | dromaeo-core-eval    |     5,710.298 μs |     30.4418 μs |     5,719.807 μs |    4 |    36528.17 KB |
| Jurassic          | dromaeo-core-eval    |    10,190.579 μs |     45.9655 μs |    10,182.594 μs |    5 |     2883.96 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript | dromaeo-object-array |    41,591.401 μs |    178.5376 μs |    41,647.465 μs |    1 |   100366.56 KB |
| Jint              | dromaeo-object-array |    41,745.082 μs |    604.3720 μs |    41,368.333 μs |    1 |   100406.57 KB |
| Jurassic          | dromaeo-object-array |    43,073.872 μs |    181.7241 μs |    43,032.475 μs |    2 |    25812.61 KB |
| YantraJS          | dromaeo-object-array |    59,837.230 μs |    375.7357 μs |    59,707.694 μs |    3 |     29477.8 KB |
| NilJS             | dromaeo-object-array |    68,668.962 μs |    145.9207 μs |    68,675.675 μs |    4 |    17697.94 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript | droma(...)egexp [21] |   160,631.005 μs |  3,835.9723 μs |   159,112.100 μs |    1 |   163386.28 KB |
| Jint              | droma(...)egexp [21] |   170,538.964 μs |  2,311.2516 μs |   169,839.850 μs |    2 |   161772.38 KB |
| NilJS             | droma(...)egexp [21] |   678,785.547 μs |  5,091.0200 μs |   680,536.600 μs |    3 |   767311.77 KB |
| Jurassic          | droma(...)egexp [21] |   750,543.741 μs | 20,457.1115 μs |   747,805.000 μs |    4 |   824711.31 KB |
| YantraJS          | droma(...)egexp [21] | 1,204,735.240 μs | 27,385.4498 μs | 1,211,328.950 μs |    5 |   1153992.6 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript | droma(...)tring [21] |   264,195.604 μs | 16,545.2879 μs |   260,601.000 μs |    1 |  1321631.38 KB |
| Jint              | droma(...)tring [21] |   272,963.545 μs | 15,656.3798 μs |   275,780.400 μs |    1 |  1321635.98 KB |
| NilJS             | droma(...)tring [21] |   277,067.087 μs |  7,920.0419 μs |   275,379.550 μs |    1 |  1378224.85 KB |
| Jurassic          | droma(...)tring [21] |   291,717.260 μs |  2,856.5265 μs |   292,119.900 μs |    2 |  1458185.09 KB |
| YantraJS          | droma(...)tring [21] |   963,754.713 μs | 13,326.8728 μs |   964,274.200 μs |    3 | 15730168.65 KB |
|                   |                      |                  |                |                  |      |                |
| NilJS             | droma(...)ase64 [21] |    33,150.312 μs |    470.2301 μs |    33,029.025 μs |    1 |    19604.02 KB |
| YantraJS          | droma(...)ase64 [21] |    46,806.801 μs |    620.2937 μs |    46,589.700 μs |    2 |   760382.51 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    50,864.849 μs |    150.8649 μs |    50,804.680 μs |    3 |      6032.5 KB |
| Jint              | droma(...)ase64 [21] |    51,077.330 μs |    108.1142 μs |    51,066.010 μs |    3 |     6116.48 KB |
| Jurassic          | droma(...)ase64 [21] |    77,242.467 μs |    380.2579 μs |    77,224.329 μs |    4 |    73294.74 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript | evaluation           |        10.778 μs |      0.0597 μs |        10.785 μs |    1 |       26.96 KB |
| Jint              | evaluation           |        24.090 μs |      0.0847 μs |        24.096 μs |    2 |       35.53 KB |
| NilJS             | evaluation           |        39.711 μs |      0.0998 μs |        39.727 μs |    3 |       23.47 KB |
| YantraJS          | evaluation           |       144.919 μs |      2.3529 μs |       146.034 μs |    4 |      923.46 KB |
| Jurassic          | evaluation           |     1,416.071 μs |      5.0698 μs |     1,417.150 μs |    5 |      420.34 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript | linq-js              |        89.984 μs |      0.3819 μs |        89.854 μs |    1 |      217.66 KB |
| YantraJS          | linq-js              |       420.317 μs |      2.8181 μs |       419.227 μs |    2 |     1443.82 KB |
| Jint              | linq-js              |     1,678.234 μs |      5.1131 μs |     1,679.083 μs |    3 |     1266.43 KB |
| NilJS             | linq-js              |     6,592.095 μs |     15.3867 μs |     6,589.037 μs |    4 |      4121.1 KB |
| Jurassic          | linq-js              |    34,391.184 μs |    131.4197 μs |    34,396.960 μs |    5 |     9252.67 KB |
|                   |                      |                  |                |                  |      |                |
| Jint_ParsedScript | minimal              |         2.672 μs |      0.0170 μs |         2.676 μs |    1 |       13.09 KB |
| NilJS             | minimal              |         4.038 μs |      0.0117 μs |         4.041 μs |    2 |        4.81 KB |
| Jint              | minimal              |         4.270 μs |      0.0241 μs |         4.279 μs |    3 |       14.48 KB |
| YantraJS          | minimal              |       138.742 μs |      2.5846 μs |       139.684 μs |    4 |      918.04 KB |
| Jurassic          | minimal              |       256.402 μs |      2.0078 μs |       255.556 μs |    5 |      386.21 KB |
|                   |                      |                  |                |                  |      |                |
| YantraJS          | stopwatch            |    87,634.606 μs |    543.9371 μs |    87,538.233 μs |    1 |   224269.43 KB |
| NilJS             | stopwatch            |   175,626.098 μs |    668.8330 μs |   175,822.317 μs |    2 |     97360.8 KB |
| Jurassic          | stopwatch            |   187,896.208 μs |    651.0930 μs |   188,090.833 μs |    3 |   156936.57 KB |
| Jint_ParsedScript | stopwatch            |   281,773.983 μs |    468.8012 μs |   281,660.500 μs |    4 |    53013.96 KB |
| Jint              | stopwatch            |   291,598.703 μs |    599.9161 μs |   291,632.900 μs |    5 |    53038.47 KB |
