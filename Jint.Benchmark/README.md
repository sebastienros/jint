To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2025-03-09

* Jint 4.2.1
* Jurassic 3.2.8
* NiL.JS 2.5.1684
* YantraJS.Core 1.2.246

```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.3323)
12th Gen Intel Core i9-12900H, 1 CPU, 20 logical and 14 physical cores
.NET SDK 9.0.200
  [Host]     : .NET 9.0.2 (9.0.225.6610), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.2 (9.0.225.6610), X64 RyuJIT AVX2


```
| Method            | FileName             | Mean             | StdDev         | Rank | Allocated     |
|------------------ |--------------------- |-----------------:|---------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress         |     4,111.888 μs |     60.7202 μs |    1 |    6711.54 KB |
| Jint              | array-stress         |     4,186.182 μs |     45.7194 μs |    1 |     6746.5 KB |
| NilJS             | array-stress         |     4,877.415 μs |     42.8220 μs |    2 |    4533.01 KB |
| Jurassic          | array-stress         |     7,889.933 μs |     72.8781 μs |    3 |   11645.98 KB |
| YantraJS          | array-stress         |    15,710.545 μs |     22.3174 μs |    4 |   86542.21 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | dromaeo-3d-cube      |     3,542.463 μs |     63.4038 μs |    1 |   10679.69 KB |
| NilJS             | dromaeo-3d-cube      |     5,741.802 μs |     70.7057 μs |    2 |    4690.96 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    11,211.466 μs |     97.5139 μs |    3 |    5779.53 KB |
| Jint              | dromaeo-3d-cube      |    11,278.227 μs |     71.9873 μs |    3 |    6130.62 KB |
| Jurassic          | dromaeo-3d-cube      |    42,932.520 μs |    317.4465 μs |    4 |      10661 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [22] |               NA |             NA |    ? |            NA |
| YantraJS          | droma(...)odern [22] |     3,462.439 μs |     66.9179 μs |    1 |    10437.6 KB |
| NilJS             | droma(...)odern [22] |     6,808.862 μs |     77.3301 μs |    2 |    6078.12 KB |
| Jint              | droma(...)odern [22] |    11,590.522 μs |    104.9540 μs |    3 |    5714.76 KB |
| Jint_ParsedScript | droma(...)odern [22] |    11,780.682 μs |    112.6905 μs |    3 |    5365.42 KB |
|                   |                      |                  |                |      |               |
| NilJS             | dromaeo-core-eval    |     1,131.119 μs |     12.6519 μs |    1 |    1595.42 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     2,037.590 μs |     15.0844 μs |    2 |     332.17 KB |
| Jint              | dromaeo-core-eval    |     2,121.986 μs |     21.5909 μs |    3 |        355 KB |
| YantraJS          | dromaeo-core-eval    |     4,711.290 μs |     63.9502 μs |    4 |   36506.84 KB |
| Jurassic          | dromaeo-core-eval    |     8,072.938 μs |     73.7387 μs |    5 |    2877.49 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [24] |               NA |             NA |    ? |            NA |
| NilJS             | droma(...)odern [24] |     1,355.322 μs |      9.0446 μs |    1 |    1592.37 KB |
| Jint_ParsedScript | droma(...)odern [24] |     2,053.165 μs |     18.4399 μs |    2 |     332.61 KB |
| Jint              | droma(...)odern [24] |     2,151.733 μs |     15.0244 μs |    3 |     354.62 KB |
| YantraJS          | droma(...)odern [24] |     4,461.607 μs |     72.9639 μs |    4 |   36506.96 KB |
|                   |                      |                  |                |      |               |
| Jint              | dromaeo-object-array |    28,984.615 μs |    552.3175 μs |    1 |   96288.85 KB |
| Jint_ParsedScript | dromaeo-object-array |    30,984.329 μs |    903.6905 μs |    2 |   96234.19 KB |
| Jurassic          | dromaeo-object-array |    33,360.708 μs |    724.1208 μs |    3 |   25810.95 KB |
| NilJS             | dromaeo-object-array |    55,023.521 μs |    792.1230 μs |    4 |   17696.81 KB |
| YantraJS          | dromaeo-object-array |    81,210.753 μs |  1,090.0546 μs |    5 | 1265113.65 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [27] |               NA |             NA |    ? |            NA |
| Jint              | droma(...)odern [27] |    34,011.552 μs |    611.0439 μs |    1 |   111456.9 KB |
| Jint_ParsedScript | droma(...)odern [27] |    35,224.652 μs |    114.0642 μs |    1 |  111404.24 KB |
| NilJS             | droma(...)odern [27] |    54,346.758 μs |    414.8499 μs |    2 |   17692.21 KB |
| YantraJS          | droma(...)odern [27] |    82,645.126 μs |  1,741.6299 μs |    3 |  1268905.1 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | droma(...)egexp [21] |   130,556.008 μs |  9,794.0738 μs |    1 |  149220.57 KB |
| Jint              | droma(...)egexp [21] |   162,560.843 μs | 10,208.1510 μs |    2 |  149330.78 KB |
| NilJS             | droma(...)egexp [21] |   595,782.263 μs | 17,777.2763 μs |    3 |  768428.64 KB |
| Jurassic          | droma(...)egexp [21] |   680,373.550 μs | 22,451.2438 μs |    4 |  822291.96 KB |
| YantraJS          | droma(...)egexp [21] | 1,111,347.721 μs | 28,822.6819 μs |    5 | 1159103.63 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | droma(...)odern [28] |   153,353.770 μs | 10,897.8646 μs |    1 |   164460.9 KB |
| Jint              | droma(...)odern [28] |   177,474.408 μs | 10,182.4196 μs |    2 |  162789.61 KB |
| NilJS             | droma(...)odern [28] |   606,626.304 μs | 15,207.2842 μs |    3 |   766262.4 KB |
| YantraJS          | droma(...)odern [28] | 1,091,380.592 μs | 28,023.5991 μs |    4 | 1155437.84 KB |
|                   |                      |                  |                |      |               |
| NilJS             | droma(...)tring [21] |   160,545.738 μs |  4,042.2926 μs |    1 | 1355046.44 KB |
| Jint_ParsedScript | droma(...)tring [21] |   185,222.547 μs |  6,166.2279 μs |    2 | 1303371.99 KB |
| Jint              | droma(...)tring [21] |   188,223.856 μs |  6,364.8474 μs |    2 | 1303665.43 KB |
| Jurassic          | droma(...)tring [21] |   234,010.510 μs |  6,493.9783 μs |    3 | 1430292.75 KB |
| YantraJS          | droma(...)tring [21] |   295,496.646 μs |  9,611.2358 μs |    4 | 1674017.12 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| NilJS             | droma(...)odern [28] |   161,354.525 μs |  5,132.0526 μs |    1 |  1355094.8 KB |
| Jint              | droma(...)odern [28] |   194,074.952 μs |  8,059.7322 μs |    2 |  1316943.5 KB |
| Jint_ParsedScript | droma(...)odern [28] |   196,728.814 μs | 10,877.5638 μs |    2 | 1316853.62 KB |
| YantraJS          | droma(...)odern [28] |   294,020.693 μs | 10,884.1380 μs |    3 | 1677614.52 KB |
|                   |                      |                  |                |      |               |
| Jint              | droma(...)ase64 [21] |    24,782.737 μs |    305.0077 μs |    1 |    2418.16 KB |
| NilJS             | droma(...)ase64 [21] |    24,882.449 μs |    426.1119 μs |    1 |   19603.62 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    25,290.568 μs |    206.5123 μs |    1 |     2309.5 KB |
| YantraJS          | droma(...)ase64 [21] |    42,689.529 μs |    745.5571 μs |    2 |  760342.23 KB |
| Jurassic          | droma(...)ase64 [21] |    46,000.823 μs |    608.1506 μs |    3 |   73295.68 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| Jint              | droma(...)odern [28] |    30,810.878 μs |    243.6401 μs |    1 |   12573.99 KB |
| Jint_ParsedScript | droma(...)odern [28] |    31,836.720 μs |    349.9385 μs |    1 |    12465.4 KB |
| NilJS             | droma(...)odern [28] |    32,237.183 μs |  1,058.8045 μs |    1 |   30346.93 KB |
| YantraJS          | droma(...)odern [28] |    43,236.577 μs |  1,052.7235 μs |    2 |  761558.69 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | evaluation           |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation           |         4.365 μs |      0.0575 μs |    1 |      22.05 KB |
| Jint              | evaluation           |        11.724 μs |      0.1317 μs |    2 |      33.63 KB |
| NilJS             | evaluation           |        25.384 μs |      0.3108 μs |    3 |      23.47 KB |
| Jurassic          | evaluation           |     1,223.593 μs |     20.9102 μs |    4 |     419.12 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | evaluation-modern    |               NA |             NA |    ? |            NA |
| YantraJS          | evaluation-modern    |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern    |         4.296 μs |      0.0648 μs |    1 |      21.79 KB |
| Jint              | evaluation-modern    |        11.609 μs |      0.1393 μs |    2 |      33.92 KB |
| NilJS             | evaluation-modern    |        24.961 μs |      0.2283 μs |    3 |      23.09 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | linq-js              |        52.361 μs |      0.5213 μs |    1 |     176.41 KB |
| YantraJS          | linq-js              |       339.191 μs |      5.0327 μs |    2 |    1461.47 KB |
| Jint              | linq-js              |     1,132.948 μs |     13.5074 μs |    3 |     1292.7 KB |
| NilJS             | linq-js              |     5,383.998 μs |     59.1931 μs |    4 |    4108.38 KB |
| Jurassic          | linq-js              |    29,595.143 μs |    216.1425 μs |    5 |    9174.34 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | minimal              |         1.523 μs |      0.0231 μs |    1 |      14.39 KB |
| Jint              | minimal              |         2.391 μs |      0.0194 μs |    2 |      16.38 KB |
| NilJS             | minimal              |         2.653 μs |      0.0210 μs |    3 |       4.81 KB |
| YantraJS          | minimal              |       105.286 μs |      1.3830 μs |    4 |     911.12 KB |
| Jurassic          | minimal              |       199.561 μs |      4.4203 μs |    5 |     385.32 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | stopwatch            |    61,598.711 μs |    816.6275 μs |    1 |   219936.2 KB |
| NilJS             | stopwatch            |   127,369.070 μs |  1,981.0343 μs |    2 |   97360.45 KB |
| Jurassic          | stopwatch            |   134,398.101 μs |  2,843.7718 μs |    2 |  156933.96 KB |
| Jint              | stopwatch            |   185,650.753 μs |  1,270.7010 μs |    3 |   40668.65 KB |
| Jint_ParsedScript | stopwatch            |   190,491.231 μs |  1,674.1836 μs |    3 |   40632.31 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | stopwatch-modern     |    61,573.640 μs |    789.3805 μs |    1 |  238314.22 KB |
| Jurassic          | stopwatch-modern     |   171,200.289 μs |  1,630.0097 μs |    2 |  288626.84 KB |
| NilJS             | stopwatch-modern     |   228,566.224 μs |  3,180.7124 μs |    3 |  369703.72 KB |
| Jint_ParsedScript | stopwatch-modern     |   285,666.454 μs |  2,515.2747 μs |    4 |  230618.45 KB |
| Jint              | stopwatch-modern     |   287,034.243 μs |  2,953.8685 μs |    4 |  230654.54 KB |

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
