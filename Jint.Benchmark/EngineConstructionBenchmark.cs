using BenchmarkDotNet.Attributes;
using Esprima;
using Esprima.Ast;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class EngineConstructionBenchmark
{
    private readonly Script _program;

    public EngineConstructionBenchmark()
    {
        var parser = new JavaScriptParser();
        _program = parser.ParseScript("return [].length + ''.length");
    }

    [Benchmark]
    public double BuildEngine()
    {
        var engine = new Engine();
        return engine.Evaluate(_program).AsNumber();
    }
}