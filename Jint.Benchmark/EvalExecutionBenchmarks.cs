using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the *execution* side of the dromaeo-core-eval shape (16 copies of a 1000-iteration
/// string-append loop, ~1 KB source), complementing <see cref="EvalCacheBenchmarks"/> which
/// isolates the compile pipeline. Hot rows reuse one engine with the eval compile cache warmed
/// (pure body execution); Fresh rows create an engine per op like EngineComparisonBenchmark.
/// PlainFunctionLoopReference runs the identical body as a regular function — the structural
/// floor the eval rows should converge to. ParseOnlyTmp bounds the per-op parse share.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class EvalExecutionBenchmarks
{
    private const string Cmd = """var str="";for(var i=0;i<1000;i++){str += "a";}ret = str;""";

    private string _tmp16 = null!;

    private Engine _evalEngine = null!;
    private Engine _newFunctionEngine = null!;
    private Engine _plainFunctionEngine = null!;

    private Prepared<Script> _evalCall;
    private Prepared<Script> _newFunctionCall;
    private Prepared<Script> _plainFunctionCall;
    private Prepared<Script> _freshEvalScript;
    private Prepared<Script> _freshNewFunctionScript;

    [GlobalSetup]
    public void Setup()
    {
        _tmp16 = string.Concat(Enumerable.Repeat(Cmd, 16));

        // Fresh-engine variants mirror the dromaeo script: build tmp by doubling, then run once.
        const string BuildTmp = $"var ret; var cmd = '{Cmd}'; var tmp = cmd; for (var n = 0; n < 4; n++) tmp += tmp;";
        _freshEvalScript = Engine.PrepareScript(BuildTmp + " eval(tmp);", strict: true);
        _freshNewFunctionScript = Engine.PrepareScript(BuildTmp + " (new Function(tmp))();", strict: true);

        _evalCall = Engine.PrepareScript("eval(tmp);", strict: true);
        _newFunctionCall = Engine.PrepareScript("(new Function(tmp))();", strict: true);
        _plainFunctionCall = Engine.PrepareScript("__f();", strict: true);

        _evalEngine = CreateEngineWithTmp();
        _newFunctionEngine = CreateEngineWithTmp();

        _plainFunctionEngine = new Engine(static options => options.Strict());
        _plainFunctionEngine.Execute("var ret; function __f() { " + _tmp16 + " }");
        _plainFunctionEngine.Evaluate(_plainFunctionCall);

        // Two evaluations move the source past the eval/new Function caches' two-touch
        // probation, so every benchmark op measures the cache-hit (pure execution) path.
        for (var i = 0; i < 2; i++)
        {
            _evalEngine.Evaluate(_evalCall);
            _newFunctionEngine.Evaluate(_newFunctionCall);
        }
    }

    private Engine CreateEngineWithTmp()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute("var ret;");
        engine.SetValue("tmp", _tmp16);
        return engine;
    }

    [Benchmark]
    public JsValue EvalHotReusedEngine() => _evalEngine.Evaluate(_evalCall);

    [Benchmark]
    public JsValue NewFunctionHotReusedEngine() => _newFunctionEngine.Evaluate(_newFunctionCall);

    [Benchmark]
    public JsValue PlainFunctionLoopReference() => _plainFunctionEngine.Evaluate(_plainFunctionCall);

    [Benchmark]
    public void EvalFreshEngine()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_freshEvalScript);
    }

    [Benchmark]
    public void NewFunctionFreshEngine()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_freshNewFunctionScript);
    }

    [Benchmark]
    public Prepared<Script> ParseOnlyTmp() => Engine.PrepareScript(_tmp16, strict: true);
}
