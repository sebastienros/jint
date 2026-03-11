using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Tests prepared script reuse across multiple engine instances,
/// which is the pattern used by JavaScriptEngineSwitcher and similar hosts.
/// </summary>
[MemoryDiagnoser]
public class PreparedScriptBenchmark
{
    private string _script = null!;
    private Prepared<Script> _prepared;

    [GlobalSetup]
    public void Setup()
    {
        _script = File.ReadAllText("Scripts/dromaeo-3d-cube.js");
        _prepared = Engine.PrepareScript(_script);
    }

    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute("var startTest=function(){};var test=function(n,f){f()};var endTest=function(){};var prep=function(f){f()}");
        return engine;
    }

    [Benchmark(Baseline = true)]
    public void ExecuteStringOnMultipleEngines()
    {
        for (int i = 0; i < 4; i++)
        {
            var engine = CreateEngine();
            engine.Execute(_script);
        }
    }

    [Benchmark]
    public void ExecutePreparedOnMultipleEngines()
    {
        for (int i = 0; i < 4; i++)
        {
            var engine = CreateEngine();
            engine.Execute(_prepared);
        }
    }
}
