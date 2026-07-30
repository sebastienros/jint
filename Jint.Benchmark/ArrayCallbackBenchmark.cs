using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The callback-driven array pipeline: reduce/forEach/some/every and a map→filter→reduce chain
/// over 100,000 LCG-mixed small integers (values vary enough that the branch predictor cannot
/// memorize outcomes, yet stay inside the small-int cache so rows measure callback dispatch,
/// not number boxing). One built-in call per op — the element count is the loop.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs <see cref="SetupSource"/> so each engine owns its own <c>data</c>/<c>data10k</c> — and warmed
/// with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one engine
/// warmed with all six row scripts, so each row was measured on an engine carrying the other five rows'
/// handler-tree and call-site caches and their callback shapes, which makes a row's number depend on
/// which siblings exist and on what a change did to <em>them</em>. The rows still measure warm callback
/// dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement.
/// <b>Numbers from this class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ArrayCallbackBenchmark
{
    private IsolatedScript _reduceSum;
    private IsolatedScript _reduceToObject;
    private IsolatedScript _forEachSum;
    private IsolatedScript _someMiss;
    private IsolatedScript _everyHit;
    private IsolatedScript _mapFilterReduceChain;

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

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _reduceSum = IsolatedScript.Warm(Engine.PrepareScript(ReduceSumSource), CreateEngine);

        // the dictionary-growth reduce shape: accumulator object gains keys as it goes
        _reduceToObject = IsolatedScript.Warm(Engine.PrepareScript("""
            data10k.reduce(function (a, x) { a['k' + (x & 63)] = x; return a; }, {});
            """), CreateEngine);

        _forEachSum = IsolatedScript.Warm(Engine.PrepareScript(ForEachSumSource), CreateEngine);

        // full-scan miss: predicate never satisfied
        _someMiss = IsolatedScript.Warm(Engine.PrepareScript("data.some(function (x) { return x < 0; });"), CreateEngine);

        // full-scan hit: predicate always satisfied
        _everyHit = IsolatedScript.Warm(Engine.PrepareScript("data.every(function (x) { return x >= 0; });"), CreateEngine);

        _mapFilterReduceChain = IsolatedScript.Warm(Engine.PrepareScript(MapFilterReduceChainSource), CreateEngine);
    }

    [Benchmark]
    public JsValue ReduceSum() => _reduceSum.Run();

    [Benchmark]
    public JsValue ReduceToObject() => _reduceToObject.Run();

    [Benchmark]
    public JsValue ForEachSum() => _forEachSum.Run();

    [Benchmark]
    public JsValue SomeMiss() => _someMiss.Run();

    [Benchmark]
    public JsValue EveryHit() => _everyHit.Run();

    [Benchmark]
    public JsValue MapFilterReduceChain() => _mapFilterReduceChain.Run();
}
