using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class EngineResetBenchmark
{
    private Engine _reusableEngine = null!;
    private Engine _reusableEngineWithOptions = null!;
    private Prepared<Script> _setupScript;
    private Prepared<Script> _workScript;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _setupScript = Engine.PrepareScript(@"
            function processInput(input) {
                return JSON.parse(input);
            }
            function formatOutput(obj) {
                return JSON.stringify(obj, null, 2);
            }
            var CONFIG = { maxRetries: 3, timeout: 5000 };
        ");

        _workScript = Engine.PrepareScript(@"
            var result = processInput('{""key"":1}');
            formatOutput(result);
        ");

        _reusableEngine = new Engine();
        _reusableEngine.Execute(_setupScript);
        _reusableEngine.Evaluate(_workScript);

        _reusableEngineWithOptions = new Engine(options =>
        {
            options.Strict = true;
        });
        _reusableEngineWithOptions.Execute(_setupScript);
        _reusableEngineWithOptions.Evaluate(_workScript);
    }

    [Benchmark(Baseline = true)]
    public JsValue NewEngine_WithSetup()
    {
        var engine = new Engine();
        engine.Execute(_setupScript);
        return engine.Evaluate(_workScript);
    }

    [Benchmark]
    public JsValue ResetState_WithSetup()
    {
        _reusableEngine.ResetState();
        _reusableEngine.Execute(_setupScript);
        return _reusableEngine.Evaluate(_workScript);
    }

    [Benchmark]
    public JsValue NewEngine_WithOptions_WithSetup()
    {
        var engine = new Engine(options => options.Strict = true);
        engine.Execute(_setupScript);
        return engine.Evaluate(_workScript);
    }

    [Benchmark]
    public JsValue ResetState_WithOptions_WithSetup()
    {
        _reusableEngineWithOptions.ResetState();
        _reusableEngineWithOptions.Execute(_setupScript);
        return _reusableEngineWithOptions.Evaluate(_workScript);
    }
}
