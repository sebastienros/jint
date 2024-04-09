using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public abstract class SingleScriptBenchmark
{
    private string _script;
    private Prepared<Script> _parsedScript;

    protected abstract string FileName { get; }

    [GlobalSetup]
    public void Setup()
    {
        _script = File.ReadAllText($"Scripts/{FileName}");
        _parsedScript = Engine.PrepareScript(_script, strict: true);
    }

    [Benchmark]
    public void Execute()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_script);
    }

    [Benchmark]
    public void Execute_ParsedScript()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_parsedScript);
    }
}
