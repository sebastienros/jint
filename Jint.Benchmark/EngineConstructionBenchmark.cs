using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class EngineConstructionBenchmark
{
    private Prepared<Script> _program;
    private Prepared<Script> _simple;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _program = Engine.PrepareScript("([].length + ''.length)");
        _simple = Engine.PrepareScript("1");
        new Engine().Evaluate(_program);
    }

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
