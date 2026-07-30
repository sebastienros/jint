using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Compares cold/warm Math intrinsic costs for the source-generator branch versus upstream main.
/// Run alone (no parallel workloads) per project memory; intended to be diff'd between branches.
///
/// <para><b>Engine isolation.</b> Each Warm_ row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one shared <c>_warm</c> engine warmed
/// with all four scripts, so every warm row was measured on an engine already carrying the other rows'
/// handler-tree and call-site state. The Cold_ rows are unchanged — they build their engine inside the
/// benchmark method, which is what they are for. <b>Numbers from the warm rows are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class MathBenchmarks
{
    // Shared by the Cold_ rows, which build their own engine per invocation.
    private Prepared<Script> _abs;
    private Prepared<Script> _pi;
    private Prepared<Script> _tenMethods;

    private IsolatedScript _warmAbs;
    private IsolatedScript _warmPi;
    private IsolatedScript _warmMax;
    private IsolatedScript _warmTenMethods;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _abs = Engine.PrepareScript("Math.abs(-5)");
        _pi = Engine.PrepareScript("Math.PI");
        var max = Engine.PrepareScript("Math.max(1, 2, 3, 4, 5)");
        _tenMethods = Engine.PrepareScript(
            "Math.abs(-1) + Math.cos(0) + Math.sin(0) + Math.exp(1) + Math.log(1) + " +
            "Math.sqrt(4) + Math.tan(0) + Math.atan(0) + Math.ceil(1.5) + Math.floor(1.5)");

        _warmAbs = IsolatedScript.Warm(_abs);
        _warmPi = IsolatedScript.Warm(_pi);
        _warmMax = IsolatedScript.Warm(max);
        _warmTenMethods = IsolatedScript.Warm(_tenMethods);
    }

    // ---------- Cold paths (include engine + Math intrinsic init) ----------

    [Benchmark]
    public Engine Cold_EngineOnly() => new Engine();

    [Benchmark]
    public JsValue Cold_EngineThenMathPi()
    {
        var e = new Engine();
        return e.Evaluate(_pi);
    }

    [Benchmark]
    public JsValue Cold_EngineThenMathAbs()
    {
        var e = new Engine();
        return e.Evaluate(_abs);
    }

    [Benchmark]
    public JsValue Cold_EngineThenTenMethods()
    {
        var e = new Engine();
        return e.Evaluate(_tenMethods);
    }

    // ---------- Warm paths (inline-cache hot) ----------

    [Benchmark]
    public JsValue Warm_MathPi() => _warmPi.Run();

    [Benchmark]
    public JsValue Warm_MathAbs() => _warmAbs.Run();

    [Benchmark]
    public JsValue Warm_MathMaxVarargs() => _warmMax.Run();

    [Benchmark]
    public JsValue Warm_TenMethodsCombined() => _warmTenMethods.Run();
}
