using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class LinqJsBenchmark : SingleScriptBenchmark
{
    protected override string FileName => "linq-js.js";
}
