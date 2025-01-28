To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2025-01-28

* Jint 4.2.0
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
| Method            | FileName             | Mean             | StdDev         | Rank | Allocated     |
|------------------ |--------------------- |-----------------:|---------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress         |     4,049.548 μs |     52.5123 μs |    1 |    6711.54 KB |
| Jint              | array-stress         |     4,344.880 μs |     74.5139 μs |    2 |     6746.5 KB |
| NilJS             | array-stress         |     4,949.047 μs |     46.8181 μs |    3 |    4533.01 KB |
| Jurassic          | array-stress         |     8,012.405 μs |     98.4697 μs |    4 |   11645.98 KB |
| YantraJS          | array-stress         |    15,519.887 μs |     75.3653 μs |    5 |   86542.13 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | dromaeo-3d-cube      |     3,430.229 μs |     48.5585 μs |    1 |   10661.72 KB |
| NilJS             | dromaeo-3d-cube      |     5,695.423 μs |     46.1834 μs |    2 |    4690.96 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    11,320.559 μs |     74.6142 μs |    3 |    5779.53 KB |
| Jint              | dromaeo-3d-cube      |    11,501.850 μs |     65.2785 μs |    3 |    6130.62 KB |
| Jurassic          | dromaeo-3d-cube      |    43,668.843 μs |    524.2278 μs |    4 |   10660.96 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [22] |               NA |             NA |    ? |            NA |
| YantraJS          | droma(...)odern [22] |     3,657.895 μs |     79.7526 μs |    1 |   10419.61 KB |
| NilJS             | droma(...)odern [22] |     6,911.911 μs |     58.3349 μs |    2 |    6078.12 KB |
| Jint              | droma(...)odern [22] |    11,606.453 μs |     95.2853 μs |    3 |    5714.76 KB |
| Jint_ParsedScript | droma(...)odern [22] |    11,866.727 μs |    118.9020 μs |    3 |    5365.42 KB |
|                   |                      |                  |                |      |               |
| NilJS             | dromaeo-core-eval    |     1,139.493 μs |      6.6251 μs |    1 |    1595.42 KB |
| Jint              | dromaeo-core-eval    |     2,009.384 μs |     24.2555 μs |    2 |        355 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     2,045.270 μs |      9.7720 μs |    2 |     332.17 KB |
| YantraJS          | dromaeo-core-eval    |     4,498.056 μs |    120.6111 μs |    3 |   36506.75 KB |
| Jurassic          | dromaeo-core-eval    |     8,064.759 μs |     58.2595 μs |    4 |    2877.49 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [24] |               NA |             NA |    ? |            NA |
| NilJS             | droma(...)odern [24] |     1,336.953 μs |     13.3582 μs |    1 |    1592.37 KB |
| Jint_ParsedScript | droma(...)odern [24] |     2,021.403 μs |     22.5938 μs |    2 |     332.61 KB |
| Jint              | droma(...)odern [24] |     2,062.571 μs |     24.0229 μs |    2 |     354.62 KB |
| YantraJS          | droma(...)odern [24] |     4,461.169 μs |     50.2007 μs |    3 |   36506.87 KB |
|                   |                      |                  |                |      |               |
| Jint              | dromaeo-object-array |    29,424.250 μs |    548.5013 μs |    1 |   96288.85 KB |
| Jint_ParsedScript | dromaeo-object-array |    29,736.837 μs |    738.4800 μs |    1 |   96234.21 KB |
| Jurassic          | dromaeo-object-array |    33,145.188 μs |    497.1933 μs |    2 |   25810.81 KB |
| NilJS             | dromaeo-object-array |    54,053.116 μs |    802.5227 μs |    3 |   17696.81 KB |
| YantraJS          | dromaeo-object-array |    79,831.191 μs |    208.2295 μs |    4 | 1265113.49 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [27] |               NA |             NA |    ? |            NA |
| Jint              | droma(...)odern [27] |    33,170.077 μs |    294.3282 μs |    1 |  111456.89 KB |
| Jint_ParsedScript | droma(...)odern [27] |    34,667.482 μs |    490.7214 μs |    2 |  111404.24 KB |
| NilJS             | droma(...)odern [27] |    54,265.263 μs |    427.9003 μs |    3 |   17692.21 KB |
| YantraJS          | droma(...)odern [27] |    82,080.487 μs |  1,664.7608 μs |    4 | 1268905.02 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | droma(...)egexp [21] |   123,820.641 μs |  7,981.6248 μs |    1 |  148958.41 KB |
| Jint              | droma(...)egexp [21] |   157,893.529 μs | 10,616.7562 μs |    2 |   150038.6 KB |
| NilJS             | droma(...)egexp [21] |   607,327.253 μs | 13,298.9571 μs |    3 |  768118.45 KB |
| Jurassic          | droma(...)egexp [21] |   673,112.016 μs | 20,038.6003 μs |    4 |  824350.88 KB |
| YantraJS          | droma(...)egexp [21] | 1,106,377.237 μs | 24,084.8223 μs |    5 | 1154233.83 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | droma(...)odern [28] |   151,311.511 μs |  8,969.3169 μs |    1 |  165971.32 KB |
| Jint              | droma(...)odern [28] |   170,932.734 μs |  8,884.9477 μs |    2 |  164736.39 KB |
| NilJS             | droma(...)odern [28] |   610,273.007 μs | 17,261.6442 μs |    3 |   768322.3 KB |
| YantraJS          | droma(...)odern [28] | 1,057,986.133 μs | 24,853.8180 μs |    4 | 1156709.84 KB |
|                   |                      |                  |                |      |               |
| NilJS             | droma(...)tring [21] |   159,861.553 μs |  5,385.0060 μs |    1 |  1354918.4 KB |
| Jint_ParsedScript | droma(...)tring [21] |   189,329.236 μs |  6,173.3636 μs |    2 | 1303382.88 KB |
| Jint              | droma(...)tring [21] |   194,495.452 μs |  4,716.0067 μs |    2 | 1303569.54 KB |
| Jurassic          | droma(...)tring [21] |   249,463.843 μs |  8,385.8143 μs |    3 | 1430492.94 KB |
| YantraJS          | droma(...)tring [21] |   295,506.200 μs |  7,985.3094 μs |    4 |  1673914.9 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| NilJS             | droma(...)odern [28] |   159,338.102 μs |  3,218.6102 μs |    1 | 1354935.53 KB |
| Jint_ParsedScript | droma(...)odern [28] |   191,506.161 μs |  5,739.2590 μs |    2 | 1316807.36 KB |
| Jint              | droma(...)odern [28] |   196,197.432 μs |  7,029.1399 μs |    2 | 1316983.88 KB |
| YantraJS          | droma(...)odern [28] |   293,601.807 μs | 11,168.5511 μs |    3 | 1677338.36 KB |
|                   |                      |                  |                |      |               |
| Jint              | droma(...)ase64 [21] |    24,795.091 μs |    348.0758 μs |    1 |    2418.15 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    24,975.270 μs |    216.2499 μs |    1 |     2309.5 KB |
| NilJS             | droma(...)ase64 [21] |    25,169.648 μs |    364.3053 μs |    1 |   19603.62 KB |
| Jurassic          | droma(...)ase64 [21] |    46,916.905 μs |    635.0987 μs |    2 |   73292.37 KB |
| YantraJS          | droma(...)ase64 [21] |    53,904.094 μs |  1,912.0752 μs |    3 |  760342.21 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | droma(...)odern [28] |               NA |             NA |    ? |            NA |
| NilJS             | droma(...)odern [28] |    30,497.115 μs |    301.8924 μs |    1 |   30346.93 KB |
| Jint              | droma(...)odern [28] |    30,759.954 μs |    173.3515 μs |    1 |   12573.98 KB |
| Jint_ParsedScript | droma(...)odern [28] |    31,672.704 μs |    270.4769 μs |    1 |   12465.39 KB |
| YantraJS          | droma(...)odern [28] |    46,051.791 μs |    748.7986 μs |    2 |  761558.65 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | evaluation           |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation           |         4.753 μs |      0.0840 μs |    1 |      22.05 KB |
| Jint              | evaluation           |        12.248 μs |      0.0947 μs |    2 |      33.63 KB |
| NilJS             | evaluation           |        25.612 μs |      0.2340 μs |    3 |      23.47 KB |
| Jurassic          | evaluation           |     1,222.443 μs |     20.4536 μs |    4 |     419.12 KB |
|                   |                      |                  |                |      |               |
| Jurassic          | evaluation-modern    |               NA |             NA |    ? |            NA |
| YantraJS          | evaluation-modern    |               NA |             NA |    ? |            NA |
| Jint_ParsedScript | evaluation-modern    |         4.616 μs |      0.0887 μs |    1 |      21.79 KB |
| Jint              | evaluation-modern    |        11.664 μs |      0.1294 μs |    2 |      33.92 KB |
| NilJS             | evaluation-modern    |        25.737 μs |      0.1536 μs |    3 |      23.09 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | linq-js              |        53.399 μs |      0.3470 μs |    1 |     176.41 KB |
| YantraJS          | linq-js              |       348.332 μs |      7.1846 μs |    2 |    1461.39 KB |
| Jint              | linq-js              |     1,150.728 μs |     17.6557 μs |    3 |     1292.7 KB |
| NilJS             | linq-js              |     5,395.736 μs |     63.3636 μs |    4 |    4108.38 KB |
| Jurassic          | linq-js              |    32,084.349 μs |    520.9645 μs |    5 |    9191.95 KB |
|                   |                      |                  |                |      |               |
| Jint_ParsedScript | minimal              |         1.564 μs |      0.0191 μs |    1 |      14.39 KB |
| Jint              | minimal              |         2.471 μs |      0.0231 μs |    2 |      16.38 KB |
| NilJS             | minimal              |         2.673 μs |      0.0265 μs |    3 |       4.81 KB |
| YantraJS          | minimal              |       109.754 μs |      1.2390 μs |    4 |      911.1 KB |
| Jurassic          | minimal              |       205.359 μs |      3.1479 μs |    5 |     385.32 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | stopwatch            |    62,009.680 μs |    618.1669 μs |    1 |  217462.85 KB |
| NilJS             | stopwatch            |   124,139.737 μs |  1,141.8470 μs |    2 |   97360.49 KB |
| Jurassic          | stopwatch            |   134,130.907 μs |  1,068.4254 μs |    3 |  156933.96 KB |
| Jint              | stopwatch            |   187,135.200 μs |  2,677.8553 μs |    4 |   40668.65 KB |
| Jint_ParsedScript | stopwatch            |   194,420.464 μs |  1,555.2732 μs |    4 |   40632.31 KB |
|                   |                      |                  |                |      |               |
| YantraJS          | stopwatch-modern     |    63,054.678 μs |    829.2698 μs |    1 |  235840.87 KB |
| Jurassic          | stopwatch-modern     |   170,867.460 μs |  1,664.5481 μs |    2 |  288627.38 KB |
| NilJS             | stopwatch-modern     |   224,055.051 μs |  2,917.5827 μs |    3 |  369703.83 KB |
| Jint              | stopwatch-modern     |   275,296.163 μs |  2,591.5090 μs |    4 |  230654.54 KB |
| Jint_ParsedScript | stopwatch-modern     |   293,955.153 μs |  2,725.4396 μs |    5 |  230618.45 KB |

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
