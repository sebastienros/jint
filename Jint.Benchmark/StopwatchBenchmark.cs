using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class StopwatchBenchmark : SingleScriptBenchmark
{
    [Params(false, true)]
    public bool Modern { get; set; }

    protected override string FileName => Modern ? "stopwatch-modern.js" : "stopwatch.js";
}
