using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The callback-driven array pipeline: reduce/forEach/some/every and a map→filter→reduce chain
/// over 100,000 LCG-mixed small integers (values vary enough that the branch predictor cannot
/// memorize outcomes, yet stay inside the small-int cache so rows measure callback dispatch,
/// not number boxing). One built-in call per op — the element count is the loop.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ArrayCallbackBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _reduceSum;
    private Prepared<Script> _reduceToObject;
    private Prepared<Script> _forEachSum;
    private Prepared<Script> _someMiss;
    private Prepared<Script> _everyHit;
    private Prepared<Script> _mapFilterReduceChain;

    internal const string SetupSource = """
        var data = [];
        var data10k;
        (function () {
            var seed = 20260711;
            for (var i = 0; i < 100000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                data.push((seed >>> 4) & 1023);
            }
            data10k = data.slice(0, 10000);
        })();
        """;

    internal const string ReduceSumSource = "data.reduce(function (a, x) { return a + x; }, 0);";
    internal const string ForEachSumSource = "(function () { var s = 0; data.forEach(function (x) { s += x; }); return s; })();";
    internal const string MapFilterReduceChainSource = """
        data.map(function (x) { return x * 2; })
            .filter(function (x) { return (x % 3) === 0; })
            .reduce(function (a, x) { return a + x; }, 0);
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _reduceSum = Engine.PrepareScript(ReduceSumSource);

        // the dictionary-growth reduce shape: accumulator object gains keys as it goes
        _reduceToObject = Engine.PrepareScript("""
            data10k.reduce(function (a, x) { a['k' + (x & 63)] = x; return a; }, {});
            """);

        _forEachSum = Engine.PrepareScript(ForEachSumSource);

        // full-scan miss: predicate never satisfied
        _someMiss = Engine.PrepareScript("data.some(function (x) { return x < 0; });");

        // full-scan hit: predicate always satisfied
        _everyHit = Engine.PrepareScript("data.every(function (x) { return x >= 0; });");

        _mapFilterReduceChain = Engine.PrepareScript(MapFilterReduceChainSource);

        _engine.Evaluate(_reduceSum);
        _engine.Evaluate(_reduceToObject);
        _engine.Evaluate(_forEachSum);
        _engine.Evaluate(_someMiss);
        _engine.Evaluate(_everyHit);
        _engine.Evaluate(_mapFilterReduceChain);
    }

    [Benchmark]
    public JsValue ReduceSum() => _engine.Evaluate(_reduceSum);

    [Benchmark]
    public JsValue ReduceToObject() => _engine.Evaluate(_reduceToObject);

    [Benchmark]
    public JsValue ForEachSum() => _engine.Evaluate(_forEachSum);

    [Benchmark]
    public JsValue SomeMiss() => _engine.Evaluate(_someMiss);

    [Benchmark]
    public JsValue EveryHit() => _engine.Evaluate(_everyHit);

    [Benchmark]
    public JsValue MapFilterReduceChain() => _engine.Evaluate(_mapFilterReduceChain);
}
