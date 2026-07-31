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
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one shared <c>_warm</c> engine warmed with all
/// six scripts, so each row was measured on an engine carrying its siblings' globals (the two tight-loop
/// scripts both declare <c>s</c> and <c>i</c>) plus their handler-tree and call-site state. The rows still
/// measure warm dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class MathHotPathBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private IsolatedScript _abs;
    private IsolatedScript _floor;
    private IsolatedScript _sqrt;
    private IsolatedScript _max2;
    private IsolatedScript _absInLoop;
    private IsolatedScript _maxInLoop;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _abs   = IsolatedScript.Warm("Math.abs(-1.5)");
        _floor = IsolatedScript.Warm("Math.floor(3.7)");
        _sqrt  = IsolatedScript.Warm("Math.sqrt(2.0)");
        _max2  = IsolatedScript.Warm("Math.max(1, 2)");

        // Tight-loop variants: amortises script-eval cost, isolates dispatch + body.
        _absInLoop = IsolatedScript.Warm("var s = 0; for (var i = 0; i < 1000; i++) s += Math.abs(i - 500); s");
        _maxInLoop = IsolatedScript.Warm("var s = 0; for (var i = 0; i < 1000; i++) s += Math.max(i, 500); s");
    }

    [Benchmark] public JsValue Warm_Abs() => _abs.Run();
    [Benchmark] public JsValue Warm_Floor() => _floor.Run();
    [Benchmark] public JsValue Warm_Sqrt() => _sqrt.Run();
    [Benchmark] public JsValue Warm_Max2() => _max2.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_AbsTightLoop() => _absInLoop.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public JsValue Warm_MaxTightLoop() => _maxInLoop.Run();
}
