using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class MinimalScriptBenchmark : SingleScriptBenchmark
{
    protected override string Script => "var done = 1 + 1 === 2;";
}
