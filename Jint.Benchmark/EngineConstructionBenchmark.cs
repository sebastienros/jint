using BenchmarkDotNet.Attributes;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class EngineConstructionBenchmark
{
    private Script _program;
    private Script _simple;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var parser = new JavaScriptParser();
        _program = parser.ParseScript("([].length + ''.length)");
        _simple = parser.ParseScript("1");
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
