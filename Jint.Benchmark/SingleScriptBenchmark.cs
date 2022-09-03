using BenchmarkDotNet.Attributes;
using Esprima;
using Esprima.Ast;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public abstract class SingleScriptBenchmark
{
    private string _script;
    private Script _parsedScript;

    protected abstract string FileName { get; }

    [GlobalSetup]
    public void Setup()
    {
        _script = File.ReadAllText($"Scripts/{FileName}");
        _parsedScript = new JavaScriptParser().ParseScript(_script);
    }

    [Benchmark]
    public void Execute()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(FileName);
    }

    [Benchmark]
    public void Execute_ParsedScript()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_parsedScript);
    }
}
