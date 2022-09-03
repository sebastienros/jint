using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class EvaluationBenchmark : SingleScriptBenchmark
{
    protected override string FileName => "evaluation.js";
}
