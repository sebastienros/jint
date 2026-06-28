using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Function-local plain numeric assignment `lhs = a op b` (NOT compound `+=`, which already has an
/// unboxed slot path). The accumulator `s = s + i` is the micro-loop-sum shape: every iteration's
/// result is an uncached double that otherwise materializes a JsNumber.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "Gen0", "Gen1", "Gen2")]
public class PlainNumericAssignBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _sumAssign;
    private Prepared<Script> _divAssign;
    private Prepared<Script> _mixed;

    [GlobalSetup]
    public void Setup()
    {
        _sumAssign = Engine.PrepareScript("""
            function f() { var s = 0.5; for (var i = 0; i < 1000000; i++) { s = s + i; } return s; }
            f();
            """);

        _divAssign = Engine.PrepareScript("""
            function f() { var s = 1e30, h = 1.0000001; for (var i = 0; i < 1000000; i++) { s = s / h; if (s < 1) s = 1e30; } return s; }
            f();
            """);

        // lhs distinct from rhs operands: d = a - b with a moving accumulator
        _mixed = Engine.PrepareScript("""
            function f() { var a = 0.0, b = 0.25, d = 0.0; for (var i = 0; i < 1000000; i++) { a = a + b; d = a - b; } return d; }
            f();
            """);

        _engine = new Engine();
        _engine.Evaluate(_sumAssign);
        _engine.Evaluate(_divAssign);
        _engine.Evaluate(_mixed);
    }

    [Benchmark] public JsValue SumAssign() => _engine.Evaluate(_sumAssign);
    [Benchmark] public JsValue DivAssign() => _engine.Evaluate(_divAssign);
    [Benchmark] public JsValue Mixed() => _engine.Evaluate(_mixed);
}
