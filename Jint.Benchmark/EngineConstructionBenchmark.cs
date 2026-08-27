using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Measures cold construction, including the complete construction cost for the hardened-profile row.
/// Engine construction is the operation under test; no engine is warmed or reused between invocations.
/// </summary>
[MemoryDiagnoser]
public class EngineConstructionBenchmark
{
    private Prepared<Script> _program;
    private Prepared<Script> _simple;
    private Options _untrustedOptions;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _program = Engine.PrepareScript("([].length + ''.length)");
        _simple = Engine.PrepareScript("1");
        new Engine().Evaluate(_program);
        _untrustedOptions = new Options().ForUntrustedCode(new UntrustedCodeLimits
        {
            TimeoutInterval = TimeSpan.FromSeconds(1),
            MaxStatements = 100_000,
            MemoryLimit = 16_000_000,
            MaxRecursionDepth = 64,
            MaxArraySize = 10_000,
            RegexTimeout = TimeSpan.FromMilliseconds(250),
            PromiseTimeout = TimeSpan.FromMilliseconds(500),
            MaxOperationDuration = TimeSpan.FromSeconds(2),
        });
    }

    [Benchmark]
    public Engine BuildUntrustedEngine() => new(_untrustedOptions);

    [Benchmark]
    public Engine BuildEngine()
    {
        var engine = new Engine();
        return engine;
    }

    [Benchmark]
    public JsValue EvaluateSimple()
    {
        var engine = new Engine();
        return engine.Evaluate(_simple);
    }
}
