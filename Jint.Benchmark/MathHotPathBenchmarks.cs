using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Hot-path bench for the most-called Math methods. Gates [JsObject(Dispatch=PerMethod)] +
/// [JsFunction(Pure=true)] (Phase 2e/f of the source-gen plan): both target the cost of the
/// dispatcher's switch(_slot) on per-call paths. Math.abs/floor/sqrt/max are realistic targets —
/// they appear in every numeric-heavy benchmark (Dromaeo 3D cube, crypto, etc.) and have trivial
/// bodies where the dispatch overhead dominates.
///
/// Each tight loop runs 1000 calls inside one prepared script so we measure dispatch + call cost,
/// not script-eval/parse overhead. OperationsPerInvoke amortises BDN's per-iteration noise.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class MathHotPathBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private Engine _warm = null!;
    private Prepared<Script> _abs;
    private Prepared<Script> _floor;
    private Prepared<Script> _sqrt;
    private Prepared<Script> _max2;
    private Prepared<Script> _absInLoop;
    private Prepared<Script> _maxInLoop;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _abs   = Engine.PrepareScript("Math.abs(-1.5)");
        _floor = Engine.PrepareScript("Math.floor(3.7)");
        _sqrt  = Engine.PrepareScript("Math.sqrt(2.0)");
        _max2  = Engine.PrepareScript("Math.max(1, 2)");

        // Tight-loop variants: amortises script-eval cost, isolates dispatch + body.
        _absInLoop = Engine.PrepareScript("var s = 0; for (var i = 0; i < 1000; i++) s += Math.abs(i - 500); s");
        _maxInLoop = Engine.PrepareScript("var s = 0; for (var i = 0; i < 1000; i++) s += Math.max(i, 500); s");

        _warm = new Engine();
        _warm.Evaluate(_abs);
        _warm.Evaluate(_floor);
        _warm.Evaluate(_sqrt);
        _warm.Evaluate(_max2);
        _warm.Evaluate(_absInLoop);
        _warm.Evaluate(_maxInLoop);
    }

    [Benchmark] public JsValue Warm_Abs() => _warm.Evaluate(_abs);
    [Benchmark] public JsValue Warm_Floor() => _warm.Evaluate(_floor);
    [Benchmark] public JsValue Warm_Sqrt() => _warm.Evaluate(_sqrt);
    [Benchmark] public JsValue Warm_Max2() => _warm.Evaluate(_max2);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_AbsTightLoop() => _warm.Evaluate(_absInLoop);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_MaxTightLoop() => _warm.Evaluate(_maxInLoop);
}
