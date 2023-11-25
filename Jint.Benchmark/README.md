To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2023-11-24

* Jint main
* Jurassic 3.2.7
* NiL.JS 2.5.1674
* YantraJS.Core 1.2.206

```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.23590.1000)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method            | FileName             | Mean             | StdDev         | Median           | Rank | Allocated     |
|------------------ |--------------------- |-----------------:|---------------:|-----------------:|-----:|--------------:|
| Jint_ParsedScript | array-stress         |     5,743.534 μs |      7.3172 μs |     5,745.281 μs |    1 |    6994.68 KB |
| Jint              | array-stress         |     6,057.026 μs |     20.8293 μs |     6,053.305 μs |    2 |    7015.86 KB |
| NilJS             | array-stress         |     6,439.544 μs |     51.8156 μs |     6,411.337 μs |    3 |    4533.76 KB |
| YantraJS          | array-stress         |     7,788.618 μs |     90.7911 μs |     7,762.502 μs |    4 |     8073.6 KB |
| Jurassic          | array-stress         |    11,360.976 μs |     35.3027 μs |    11,350.166 μs |    5 |   11647.14 KB |
|                   |                      |                  |                |                  |      |               |
| YantraJS          | dromaeo-3d-cube      |     5,193.282 μs |     14.8710 μs |     5,194.805 μs |    1 |   11412.17 KB |
| NilJS             | dromaeo-3d-cube      |     7,770.177 μs |      6.6077 μs |     7,769.502 μs |    2 |    4693.22 KB |
| Jint              | dromaeo-3d-cube      |    21,327.081 μs |     64.2045 μs |    21,320.428 μs |    3 |    6221.67 KB |
| Jint_ParsedScript | dromaeo-3d-cube      |    22,275.834 μs |     48.3428 μs |    22,297.088 μs |    4 |    5964.35 KB |
| Jurassic          | dromaeo-3d-cube      |    53,870.773 μs |     75.4553 μs |    53,884.650 μs |    5 |   10671.29 KB |
|                   |                      |                  |                |                  |      |               |
| NilJS             | dromaeo-core-eval    |     1,576.585 μs |      6.4653 μs |     1,573.083 μs |    1 |    1598.62 KB |
| Jint_ParsedScript | dromaeo-core-eval    |     3,678.113 μs |     10.0864 μs |     3,675.353 μs |    2 |     332.16 KB |
| Jint              | dromaeo-core-eval    |     3,681.029 μs |     14.6248 μs |     3,679.859 μs |    2 |     349.21 KB |
| YantraJS          | dromaeo-core-eval    |     6,366.414 μs |     57.4589 μs |     6,366.488 μs |    3 |   36528.17 KB |
| Jurassic          | dromaeo-core-eval    |    10,197.244 μs |     41.9562 μs |    10,196.675 μs |    4 |    2883.96 KB |
|                   |                      |                  |                |                  |      |               |
| Jint              | dromaeo-object-array |    41,313.413 μs |     55.8197 μs |    41,316.171 μs |    1 |  100409.09 KB |
| Jint_ParsedScript | dromaeo-object-array |    43,203.196 μs |    411.2464 μs |    43,182.583 μs |    2 |   100368.9 KB |
| Jurassic          | dromaeo-object-array |    43,495.683 μs |    252.0098 μs |    43,432.125 μs |    2 |   25812.61 KB |
| YantraJS          | dromaeo-object-array |    61,354.141 μs |    482.3533 μs |    61,399.233 μs |    3 |    29478.4 KB |
| NilJS             | dromaeo-object-array |    69,506.941 μs |    353.6867 μs |    69,678.650 μs |    4 |   17697.94 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | droma(...)egexp [21] |   161,815.086 μs |  2,069.4489 μs |   161,312.500 μs |    1 |  170563.44 KB |
| Jint              | droma(...)egexp [21] |   178,719.583 μs |  3,296.4427 μs |   178,220.700 μs |    2 |  166374.76 KB |
| NilJS             | droma(...)egexp [21] |   682,594.680 μs | 12,026.0148 μs |   680,779.900 μs |    3 |  767253.88 KB |
| Jurassic          | droma(...)egexp [21] |   769,271.481 μs | 13,898.9774 μs |   768,696.800 μs |    4 |  822217.41 KB |
| YantraJS          | droma(...)egexp [21] | 1,222,190.085 μs | 14,714.3112 μs | 1,226,884.600 μs |    5 | 1156481.56 KB |
|                   |                      |                  |                |                  |      |               |
| Jint              | droma(...)tring [21] |   281,911.294 μs | 11,500.7817 μs |   275,857.700 μs |    1 | 1322546.26 KB |
| Jint_ParsedScript | droma(...)tring [21] |   283,360.189 μs | 13,196.6995 μs |   289,888.500 μs |    1 | 1322259.13 KB |
| NilJS             | droma(...)tring [21] |   293,451.562 μs |  9,299.1813 μs |   292,627.800 μs |    2 | 1378002.01 KB |
| Jurassic          | droma(...)tring [21] |   294,483.175 μs |  5,379.5117 μs |   292,301.200 μs |    2 | 1458171.62 KB |
| YantraJS          | droma(...)tring [21] |   998,437.664 μs | 11,452.5169 μs |   996,547.500 μs |    3 | 15730070.8 KB |
|                   |                      |                  |                |                  |      |               |
| NilJS             | droma(...)ase64 [21] |    33,371.183 μs |    600.4889 μs |    33,236.238 μs |    1 |   19604.34 KB |
| YantraJS          | droma(...)ase64 [21] |    46,639.004 μs |    560.7072 μs |    46,511.677 μs |    2 |  760382.48 KB |
| Jint_ParsedScript | droma(...)ase64 [21] |    49,046.433 μs |    159.2847 μs |    49,076.973 μs |    3 |    6720.18 KB |
| Jint              | droma(...)ase64 [21] |    53,332.563 μs |     86.6296 μs |    53,336.920 μs |    4 |    6804.01 KB |
| Jurassic          | droma(...)ase64 [21] |    72,535.076 μs |    149.4006 μs |    72,556.157 μs |    5 |    73295.9 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | evaluation           |        10.408 μs |      0.0391 μs |        10.407 μs |    1 |      27.39 KB |
| Jint              | evaluation           |        24.182 μs |      0.1142 μs |        24.183 μs |    2 |      35.96 KB |
| NilJS             | evaluation           |        37.611 μs |      0.0868 μs |        37.595 μs |    3 |      23.47 KB |
| YantraJS          | evaluation           |       148.389 μs |      2.2671 μs |       148.973 μs |    4 |     923.46 KB |
| Jurassic          | evaluation           |     1,419.913 μs |      5.1742 μs |     1,416.831 μs |    5 |     420.34 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | linq-js              |        86.047 μs |      0.1106 μs |        86.063 μs |    1 |     225.04 KB |
| YantraJS          | linq-js              |       447.064 μs |      2.5265 μs |       447.306 μs |    2 |    1443.82 KB |
| Jint              | linq-js              |     1,657.159 μs |      5.9514 μs |     1,655.432 μs |    3 |    1273.81 KB |
| NilJS             | linq-js              |     6,464.907 μs |     24.8496 μs |     6,454.286 μs |    4 |     4121.1 KB |
| Jurassic          | linq-js              |    34,559.331 μs |    107.6734 μs |    34,576.087 μs |    5 |    9252.69 KB |
|                   |                      |                  |                |                  |      |               |
| Jint_ParsedScript | minimal              |         2.823 μs |      0.0219 μs |         2.825 μs |    1 |      12.95 KB |
| NilJS             | minimal              |         3.865 μs |      0.0120 μs |         3.866 μs |    2 |       4.81 KB |
| Jint              | minimal              |         4.026 μs |      0.0270 μs |         4.021 μs |    3 |      14.34 KB |
| YantraJS          | minimal              |       141.911 μs |      2.0400 μs |       142.566 μs |    4 |     918.04 KB |
| Jurassic          | minimal              |       257.933 μs |      0.9659 μs |       257.808 μs |    5 |     386.21 KB |
|                   |                      |                  |                |                  |      |               |
| YantraJS          | stopwatch            |    86,849.358 μs |    572.5135 μs |    86,760.983 μs |    1 |  224269.43 KB |
| NilJS             | stopwatch            |   178,338.578 μs |  1,337.6320 μs |   178,821.267 μs |    2 |    97360.8 KB |
| Jurassic          | stopwatch            |   189,956.560 μs |    315.0817 μs |   189,865.650 μs |    3 |  156935.97 KB |
| Jint              | stopwatch            |   279,388.597 μs |  1,913.7346 μs |   279,178.250 μs |    4 |   53039.87 KB |
| Jint_ParsedScript | stopwatch            |   288,838.154 μs |    851.9774 μs |   288,601.800 μs |    5 |   53015.34 KB |
