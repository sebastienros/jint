To run tests comparing engines, use:

```
dotnet run -c Release --allCategories EngineComparison
```

## Engine comparison results 

* tests are run in global engine strict mode, as YantraJS always uses strict mode which improves performance
* `Jint` and `Jint_ParsedScript` shows the difference between always parsing the script source file and reusing parsed `Script` instance.

Last updated 2022-10-14

* Jint main
* Jurassic 3.2.6
* NiL.JS 2.5.1600
* YantraJS.Core 1.2.51


``` ini

BenchmarkDotNet=v0.13.2, OS=Windows 11 (10.0.22621.675)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK=7.0.100-rc.2.22477.23
  [Host]     : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2


```
|            Method |             FileName |             Mean |         StdDev | Rank |     Allocated |
|------------------ |--------------------- |-----------------:|---------------:|-----:|--------------:|
|              **Jint** |         **array-stress** |    **10,215.696 μs** |     **14.2525 μs** |    **4** |     **7577235 B** |
| Jint_ParsedScript |         array-stress |     9,888.694 μs |     20.5964 μs |    3 |     7550403 B |
|          Jurassic |         array-stress |    11,085.546 μs |     18.8502 μs |    5 |    11926461 B |
|             NilJS |         array-stress |     5,636.598 μs |      5.7896 μs |    2 |    14746566 B |
|          YantraJS |         array-stress |     5,076.093 μs |     29.8884 μs |    1 |     6314988 B |
|                   |                      |                  |                |      |               |
|              **Jint** |      **dromaeo-3d-cube** |    **24,287.978 μs** |     **43.1781 μs** |    **4** |     **6726806 B** |
| Jint_ParsedScript |      dromaeo-3d-cube |    23,464.073 μs |     28.5274 μs |    3 |     6419862 B |
|          Jurassic |      dromaeo-3d-cube |    39,704.937 μs |     68.4706 μs |    5 |    10926049 B |
|             NilJS |      dromaeo-3d-cube |     9,205.576 μs |     14.4517 μs |    2 |     4240315 B |
|          YantraJS |      dromaeo-3d-cube |     4,528.786 μs |     16.8094 μs |    1 |     8783571 B |
|                   |                      |                  |                |      |               |
|              **Jint** |    **dromaeo-core-eval** |     **5,432.432 μs** |     **10.3033 μs** |    **2** |      **355182 B** |
| Jint_ParsedScript |    dromaeo-core-eval |     5,458.993 μs |     10.4310 μs |    2 |      335335 B |
|          Jurassic |    dromaeo-core-eval |    13,607.238 μs |     43.2512 μs |    4 |     2971060 B |
|             NilJS |    dromaeo-core-eval |     2,104.603 μs |      4.1631 μs |    1 |     1636803 B |
|          YantraJS |    dromaeo-core-eval |     8,146.161 μs |     27.3940 μs |    3 |    37128558 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **dromaeo-object-array** |    **67,516.323 μs** |    **206.4093 μs** |    **5** |   **104398774 B** |
| Jint_ParsedScript | dromaeo-object-array |    64,823.739 μs |    140.2733 μs |    4 |   104351697 B |
|          Jurassic | dromaeo-object-array |    40,609.674 μs |     59.2756 μs |    1 |    26433897 B |
|             NilJS | dromaeo-object-array |    55,900.598 μs |    138.4180 μs |    3 |   184599926 B |
|          YantraJS | dromaeo-object-array |    46,439.103 μs |     75.7535 μs |    2 |    24731921 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)egexp [21]** |   **242,184.582 μs** |  **2,540.4568 μs** |    **2** |   **177719160 B** |
| Jint_ParsedScript | droma(...)egexp [21] |   224,182.131 μs |  3,085.4110 μs |    1 |   177744853 B |
|          Jurassic | droma(...)egexp [21] |   668,193.653 μs | 13,262.9937 μs |    3 |   843971400 B |
|             NilJS | droma(...)egexp [21] |   667,795.640 μs | 12,173.5871 μs |    3 |   799041960 B |
|          YantraJS | droma(...)egexp [21] | 1,017,836.340 μs | 14,200.0239 μs |    4 |   962355680 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)tring [21]** |   **523,799.294 μs** | **17,239.4929 μs** |    **2** |  **1358370968 B** |
| Jint_ParsedScript | droma(...)tring [21] |   587,293.097 μs | 59,293.9537 μs |    4 |  1358240112 B |
|          Jurassic | droma(...)tring [21] |   555,974.511 μs | 25,269.6502 μs |    3 |  1492965432 B |
|             NilJS | droma(...)tring [21] |   388,563.930 μs | 10,811.7627 μs |    1 |  1418390840 B |
|          YantraJS | droma(...)tring [21] | 2,981,204.866 μs | 92,355.9688 μs |    5 | 16097451064 B |
|                   |                      |                  |                |      |               |
|              **Jint** | **droma(...)ase64 [21]** |    **68,781.989 μs** |    **155.3771 μs** |    **4** |     **7914940 B** |
| Jint_ParsedScript | droma(...)ase64 [21] |    66,471.309 μs |    241.4502 μs |    3 |     7817337 B |
|          Jurassic | droma(...)ase64 [21] |    67,141.458 μs |    187.7398 μs |    3 |    76103581 B |
|             NilJS | droma(...)ase64 [21] |    41,305.925 μs |     94.0063 μs |    2 |    23963321 B |
|          YantraJS | droma(...)ase64 [21] |    38,200.686 μs |    122.9918 μs |    1 |   778587651 B |
|                   |                      |                  |                |      |               |
|              **Jint** |           **evaluation** |        **27.771 μs** |      **0.0516 μs** |    **2** |       **34880 B** |
| Jint_ParsedScript |           evaluation |        11.306 μs |      0.0192 μs |    1 |       25152 B |
|          Jurassic |           evaluation |     1,288.449 μs |      1.1880 μs |    5 |      430506 B |
|             NilJS |           evaluation |        44.453 μs |      0.0368 μs |    3 |       23792 B |
|          YantraJS |           evaluation |       122.653 μs |      0.2922 μs |    4 |      175486 B |
|                   |                      |                  |                |      |               |
|              **Jint** |              **linq-js** |     **1,699.223 μs** |      **1.3486 μs** |    **2** |     **1295937 B** |
| Jint_ParsedScript |              linq-js |        82.746 μs |      0.1714 μs |    1 |      206064 B |
|          Jurassic |              linq-js |    39,257.886 μs |    618.5241 μs |    3 |     9540326 B |
|             NilJS |              linq-js |               NA |             NA |    ? |             - |
|          YantraJS |              linq-js |               NA |             NA |    ? |             - |
|                   |                      |                  |                |      |               |
|              **Jint** |              **minimal** |         **5.252 μs** |      **0.0302 μs** |    **3** |       **13992 B** |
| Jint_ParsedScript |              minimal |         3.597 μs |      0.0130 μs |    1 |       12496 B |
|          Jurassic |              minimal |       237.108 μs |      0.2916 μs |    5 |      395505 B |
|             NilJS |              minimal |         4.659 μs |      0.0061 μs |    2 |        4816 B |
|          YantraJS |              minimal |       118.594 μs |      0.1637 μs |    4 |      171638 B |
|                   |                      |                  |                |      |               |
|              **Jint** |            **stopwatch** |   **384,738.880 μs** |    **920.0042 μs** |    **4** |    **38907168 B** |
| Jint_ParsedScript |            stopwatch |   383,749.193 μs |    787.7331 μs |    4 |    38877848 B |
|          Jurassic |            stopwatch |   207,564.195 μs |    656.0519 μs |    2 |   160703632 B |
|             NilJS |            stopwatch |   240,530.110 μs |    713.6901 μs |    3 |    76378037 B |
|          YantraJS |            stopwatch |    74,697.115 μs |    247.4887 μs |    1 |   259044927 B |

Benchmarks with issues:
  EngineComparisonBenchmark.NilJS: DefaultJob [FileName=linq-js]
  EngineComparisonBenchmark.YantraJS: DefaultJob [FileName=linq-js]

