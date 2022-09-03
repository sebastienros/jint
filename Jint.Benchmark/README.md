To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2022-09-03

* Jint main
* Jurassic 3.2.6
* NiL.JS.NetCore 2.5.1419
* YantraJS.Core 1.2.47


``` ini

BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.457)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=6.0.400
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT AVX2


```
|            Method |             FileName |             Mean |         StdDev | Rank |     Allocated |
|------------------ |--------------------- |-----------------:|---------------:|-----:|--------------:|
|              **Jint** |         **array-stress** |    **13,507.095 μs** |     **23.0399 μs** |    **5** |     **8745627 B** |
| Jint_ParsedScript |         array-stress |    13,102.050 μs |     15.5290 μs |    4 |     8723795 B |
|          Jurassic |         array-stress |    11,213.554 μs |     14.3872 μs |    3 |    11926461 B |
|             NilJS |         array-stress |     5,334.318 μs |     23.5378 μs |    1 |     4553815 B |
|          YantraJS |         array-stress |     7,286.025 μs |     32.1957 μs |    2 |     6318022 B |
|                   |                      |                  |                |      |               |
|              **Jint** |      **dromaeo-3d-cube** |    **23,587.542 μs** |     **23.6725 μs** |    **4** |     **7247466 B** |
| Jint_ParsedScript |      dromaeo-3d-cube |    21,772.708 μs |     40.7036 μs |    3 |     7005434 B |
|          Jurassic |      dromaeo-3d-cube |    39,741.721 μs |     64.2544 μs |    5 |    10925914 B |
|             NilJS |      dromaeo-3d-cube |     8,708.896 μs |     12.4054 μs |    2 |     4125451 B |
|          YantraJS |      dromaeo-3d-cube |     5,017.989 μs |     19.0233 μs |    1 |     8786605 B |
|                   |                      |                  |                |      |               |
|              **Jint** |    **dromaeo-core-eval** |     **5,705.326 μs** |     **21.1657 μs** |    **2** |      **350102 B** |
| Jint_ParsedScript |    dromaeo-core-eval |     5,668.950 μs |     12.4161 μs |    2 |      336990 B |
|          Jurassic |    dromaeo-core-eval |    13,447.357 μs |     30.6629 μs |    4 |     2971062 B |
|             NilJS |    dromaeo-core-eval |     2,173.203 μs |     46.5428 μs |    1 |     1595219 B |
|          YantraJS |    dromaeo-core-eval |     8,265.873 μs |     56.7684 μs |    3 |    37131603 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **dromaeo-object-array** |   **108,627.168 μs** |    **511.8916 μs** |    **4** |   **103655627 B** |
| Jint_ParsedScript | dromaeo-object-array |   108,528.990 μs |    288.8372 μs |    4 |   103625558 B |
|          Jurassic | dromaeo-object-array |    41,209.118 μs |     44.6309 μs |    1 |    26433962 B |
|             NilJS | dromaeo-object-array |    53,391.875 μs |    732.5365 μs |    2 |    18027054 B |
|          YantraJS | dromaeo-object-array |    74,332.930 μs |    528.6855 μs |    3 |    24735409 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)egexp [21]** |   **299,361.023 μs** |  **7,904.1811 μs** |    **1** |   **241632484 B** |
| Jint_ParsedScript | droma(...)egexp [21] |   295,042.982 μs |  8,424.5753 μs |    1 |   247151352 B |
|          Jurassic | droma(...)egexp [21] |   683,250.476 μs | 13,213.8098 μs |    2 |   842918784 B |
|             NilJS | droma(...)egexp [21] |   801,835.247 μs | 13,416.6210 μs |    3 |   901970880 B |
|          YantraJS | droma(...)egexp [21] | 1,017,060.643 μs |  7,046.5834 μs |    4 |   964707752 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)tring [21]** |   **628,645.310 μs** | **21,985.7843 μs** |    **4** |  **1363791136 B** |
| Jint_ParsedScript | droma(...)tring [21] |   524,500.457 μs | 22,516.2241 μs |    2 |  1363541192 B |
|          Jurassic | droma(...)tring [21] |   557,218.173 μs | 23,822.7984 μs |    3 |  1493044472 B |
|             NilJS | droma(...)tring [21] |   400,573.819 μs | 12,223.2081 μs |    1 |  1446930864 B |
|          YantraJS | droma(...)tring [21] | 2,258,766.944 μs | 60,256.3876 μs |    5 | 16092618624 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)ase64 [21]** |    **66,060.024 μs** |     **79.4200 μs** |    **4** |     **8049804 B** |
| Jint_ParsedScript | droma(...)ase64 [21] |    64,215.565 μs |     62.6330 μs |    3 |     7968098 B |
|          Jurassic | droma(...)ase64 [21] |    69,690.458 μs |    199.4819 μs |    5 |    76103531 B |
|             NilJS | droma(...)ase64 [21] |    45,237.687 μs |    534.6955 μs |    2 |    51047211 B |
|          YantraJS | droma(...)ase64 [21] |    42,177.560 μs |    246.7996 μs |    1 |   778591540 B |
|                   |                      |                  |                |      |               |
|              **Jint** |           **evaluation** |        **28.520 μs** |      **0.0348 μs** |    **2** |       **34784 B** |
| Jint_ParsedScript |           evaluation |        12.906 μs |      0.0298 μs |    1 |       26752 B |
|          Jurassic |           evaluation |     1,286.204 μs |      3.0882 μs |    5 |      430506 B |
|             NilJS |           evaluation |        43.866 μs |      1.2033 μs |    3 |       22456 B |
|          YantraJS |           evaluation |       178.979 μs |      0.6314 μs |    4 |      178517 B |
|                   |                      |                  |                |      |               |
|              **Jint** |              **linq-js** |     **1,876.845 μs** |      **3.2033 μs** |    **3** |     **1301929 B** |
| Jint_ParsedScript |              linq-js |       123.396 μs |      0.1138 μs |    1 |      230841 B |
|          Jurassic |              linq-js |    36,984.974 μs |    395.7409 μs |    4 |     9526082 B |
|             NilJS |              linq-js |               NA |             NA |    ? |             - |
|          YantraJS |              linq-js |       391.523 μs |      1.8922 μs |    2 |      490377 B |
|                   |                      |                  |                |      |               |
|              **Jint** |              **minimal** |         **5.334 μs** |      **0.0065 μs** |    **3** |       **14120 B** |
| Jint_ParsedScript |              minimal |         3.535 μs |      0.0143 μs |    1 |       12680 B |
|          Jurassic |              minimal |       234.184 μs |      0.6536 μs |    5 |      395505 B |
|             NilJS |              minimal |         4.516 μs |      0.0933 μs |    2 |        4272 B |
|          YantraJS |              minimal |       173.909 μs |      0.1640 μs |    4 |      174668 B |
|                   |                      |                  |                |      |               |
|              **Jint** |            **stopwatch** |   **357,288.654 μs** |  **1,123.5726 μs** |    **4** |    **38906408 B** |
| Jint_ParsedScript |            stopwatch |   362,800.054 μs |    962.7038 μs |    5 |    38890808 B |
|          Jurassic |            stopwatch |   209,929.047 μs |    346.3075 μs |    2 |   160703632 B |
|             NilJS |            stopwatch |   247,228.571 μs |    527.9922 μs |    3 |    85866368 B |
|          YantraJS |            stopwatch |    76,725.368 μs |    194.3363 μs |    1 |   259048377 B |

Benchmarks with issues:
  EngineComparisonBenchmark.NilJS: DefaultJob [FileName=linq-js]
