```text

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8 (24H23) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  Job-GHXJKN : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

AnalyzeLaunchVariance=True  Concurrent=False  LaunchCount=3

```

| Method       | Mean      | Error     | StdDev    | Median    | MValue | Gen0     | Allocated |
|------------- |----------:|----------:|----------:|----------:|-------:|---------:|----------:|
| Arithmetic   |  2.602 ms | 0.0146 ms | 0.0278 ms |  2.601 ms |  2.000 | 339.8438 | 2873144 B |
| PropertyRead |  1.987 ms | 0.0094 ms | 0.0179 ms |  1.983 ms |  2.000 |        - |     864 B |
| Callback     | 11.440 ms | 0.0768 ms | 0.1462 ms | 11.441 ms |  2.000 | 718.7500 | 6065936 B |
