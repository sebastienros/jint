using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class StopwatchBenchmark : SingleScriptBenchmark
{
    protected override string FileName => "stopwatch.js";
}
