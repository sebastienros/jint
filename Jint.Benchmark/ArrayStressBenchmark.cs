using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ArrayStressBenchmark : SingleScriptBenchmark
{
    protected override string FileName => "array-stress.js";
}
