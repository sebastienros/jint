using BenchmarkDotNet.Attributes;
using Esprima;
using Esprima.Ast;

namespace Jint.Benchmark;

[RankColumn]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory("EngineComparison")]
public abstract class SingleScriptBenchmark
{
    private Script _parsedScript;

    protected abstract string Script { get; }

    public virtual int N => 10;

    [GlobalSetup]
    public void Setup()
    {
        _parsedScript = new JavaScriptParser().ParseScript(Script);
    }

    [Benchmark]
    public bool Jint()
    {
        var engine = new Engine();
        engine.Execute(Script);
        return engine.GetValue("done").AsBoolean();
    }

    [Benchmark]
    public bool Jint_ParsedScript()
    {
        var engine = new Engine();
        engine.Execute(_parsedScript);
        return engine.GetValue("done").AsBoolean();
    }

    [Benchmark]
    public bool Jurassic()
    {
        var engine = new Jurassic.ScriptEngine();
        engine.Execute(Script);
        return engine.GetGlobalValue<bool>("done");
    }

    [Benchmark]
    public bool NilJS()
    {
        var engine = new NiL.JS.Core.Context();
        engine.Eval(Script);
        return (bool) engine.GetVariable("done");
    }

    [Benchmark]
    public bool YantraJS()
    {
        var engine = new YantraJS.Core.JSContext();
        engine.Eval(Script);
        return engine["done"].BooleanValue;
    }
}
