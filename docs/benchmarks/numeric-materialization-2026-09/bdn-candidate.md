```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8 (24H23) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  Job-GHXJKN : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

AnalyzeLaunchVariance=True  Concurrent=False  LaunchCount=3

```
| Method       | Mean      | Error     | StdDev    | Median    | MValue | Gen0     | Allocated |
|------------- |----------:|----------:|----------:|----------:|-------:|---------:|----------:|
| Arithmetic   |  1.991 ms | 0.0083 ms | 0.0158 ms |  1.993 ms |  2.000 |        - |     824 B |
| PropertyRead |  1.974 ms | 0.0100 ms | 0.0191 ms |  1.972 ms |  2.000 |        - |     864 B |
| Callback     | 10.364 ms | 0.0525 ms | 0.1000 ms | 10.343 ms |  2.000 | 718.7500 | 6065936 B |
