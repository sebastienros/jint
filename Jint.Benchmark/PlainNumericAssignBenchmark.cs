using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Function-local plain numeric assignment `lhs = a op b` (NOT compound `+=`, which already has an
/// unboxed slot path). The accumulator `s = s + i` is the micro-loop-sum shape: every iteration's
/// result is an uncached double that otherwise materializes a JsNumber.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one shared engine warmed with all three
/// scripts, so each row was measured on an engine carrying its siblings' globals (all three declare
/// <c>f</c>) and their handler-tree and call-site state. The rows still measure warm dispatch, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this
/// class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "Gen0", "Gen1", "Gen2")]
public class PlainNumericAssignBenchmark
{
    private IsolatedScript _sumAssign;
    private IsolatedScript _divAssign;
    private IsolatedScript _mixed;

    [GlobalSetup]
    public void Setup()
    {
        _sumAssign = IsolatedScript.Warm("""
            function f() { var s = 0.5; for (var i = 0; i < 1000000; i++) { s = s + i; } return s; }
            f();
            """);

        _divAssign = IsolatedScript.Warm("""
            function f() { var s = 1e30, h = 1.0000001; for (var i = 0; i < 1000000; i++) { s = s / h; if (s < 1) s = 1e30; } return s; }
            f();
            """);

        // lhs distinct from rhs operands: d = a - b with a moving accumulator
        _mixed = IsolatedScript.Warm("""
            function f() { var a = 0.0, b = 0.25, d = 0.0; for (var i = 0; i < 1000000; i++) { a = a + b; d = a - b; } return d; }
            f();
            """);
    }

    [Benchmark] public JsValue SumAssign() => _sumAssign.Run();
    [Benchmark] public JsValue DivAssign() => _divAssign.Run();
    [Benchmark] public JsValue Mixed() => _mixed.Run();
}
