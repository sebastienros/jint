using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Compares cold/warm Math intrinsic costs for the source-generator branch versus upstream main.
/// Run alone (no parallel workloads) per project memory; intended to be diff'd between branches.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class MathBenchmarks
{
    private Engine _warm = null!;
    private Prepared<Script> _abs;
    private Prepared<Script> _pi;
    private Prepared<Script> _max;
    private Prepared<Script> _tenMethods;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _abs = Engine.PrepareScript("Math.abs(-5)");
        _pi = Engine.PrepareScript("Math.PI");
        _max = Engine.PrepareScript("Math.max(1, 2, 3, 4, 5)");
        _tenMethods = Engine.PrepareScript(
            "Math.abs(-1) + Math.cos(0) + Math.sin(0) + Math.exp(1) + Math.log(1) + " +
            "Math.sqrt(4) + Math.tan(0) + Math.atan(0) + Math.ceil(1.5) + Math.floor(1.5)");

        _warm = new Engine();
        _warm.Evaluate(_abs);
        _warm.Evaluate(_pi);
        _warm.Evaluate(_max);
        _warm.Evaluate(_tenMethods);
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
    public JsValue Warm_MathPi() => _warm.Evaluate(_pi);

    [Benchmark]
    public JsValue Warm_MathAbs() => _warm.Evaluate(_abs);

    [Benchmark]
    public JsValue Warm_MathMaxVarargs() => _warm.Evaluate(_max);

    [Benchmark]
    public JsValue Warm_TenMethodsCombined() => _warm.Evaluate(_tenMethods);
}
