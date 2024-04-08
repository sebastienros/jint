using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
[BenchmarkCategory("ShadowRealm")]
public class ShadowRealmBenchmark
{
    private const string sourceCode = @"
(function (){return 'some string'})();
";

    private Engine engine;
    private Prepared<Script> parsedScript;

    [GlobalSetup]
    public void Setup()
    {
        engine = new Engine();
        parsedScript = Engine.PrepareScript(sourceCode);
    }

    [Benchmark]
    public void ReusingEngine()
    {
        engine.Evaluate(sourceCode);
    }

    [Benchmark]
    public void NewEngineInstance()
    {
        new Engine().Evaluate(sourceCode);
    }

    [Benchmark]
    public void ShadowRealm()
    {
        var shadowRealm = engine.Realm.Intrinsics.ShadowRealm.Construct();
        shadowRealm.Evaluate(sourceCode);
    }

    [Benchmark]
    public void ReusingEngine_ParsedScript()
    {
        engine.Evaluate(parsedScript);
    }

    [Benchmark]
    public void NewEngineInstance_ParsedScript()
    {
        new Engine().Evaluate(parsedScript);
    }

    [Benchmark]
    public void ShadowRealm_ParsedScript()
    {
        var shadowRealm = engine.Realm.Intrinsics.ShadowRealm.Construct();
        shadowRealm.Evaluate(parsedScript);
    }
}
