using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class EvaluationBenchmark : SingleScriptBenchmark
{
    [Params(false, true)]
    public bool Modern { get; set; }

    protected override string FileName => Modern ? "evaluation-modern.js" : "evaluation.js";
}
