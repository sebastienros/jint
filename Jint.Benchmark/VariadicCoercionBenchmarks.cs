using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Variadic coercion bench. Gates [Coerced&lt;T&gt;] (Phase 2b of the source-gen plan), which
/// formalises the "coerce-into-Span&lt;double&gt; first, then scan" pattern Math.max/min/hypot
/// already use hand-rolled. The arity sizes target three regimes:
///   - 2 args: stackalloc fast path (≤16 limit), no rented array.
///   - 16 args: stackalloc boundary.
///   - 64 args: forced rent from ArrayPool&lt;double&gt;.Shared.
/// All three should win equally from [Coerced&lt;T&gt;] vs the current emit which boxes into
/// arguments[] and re-coerces inside the host method.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class VariadicCoercionBenchmarks
{
    private Engine _warm = null!;
    private Prepared<Script> _max2;
    private Prepared<Script> _max4;
    private Prepared<Script> _max16;
    private Prepared<Script> _max64;
    private Prepared<Script> _min4;
    private Prepared<Script> _hypot4;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _max2  = Engine.PrepareScript("Math.max(1, 2)");
        _max4  = Engine.PrepareScript("Math.max(1, 2, 3, 4)");
        _max16 = Engine.PrepareScript("Math.max(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16)");
        _max64 = Engine.PrepareScript(BuildVariadicCall("Math.max", 64));
        _min4  = Engine.PrepareScript("Math.min(1, 2, 3, 4)");
        _hypot4 = Engine.PrepareScript("Math.hypot(1, 2, 3, 4)");

        _warm = new Engine();
        _warm.Evaluate(_max2);
        _warm.Evaluate(_max4);
        _warm.Evaluate(_max16);
        _warm.Evaluate(_max64);
        _warm.Evaluate(_min4);
        _warm.Evaluate(_hypot4);
    }

    [Benchmark] public JsValue Warm_Max2() => _warm.Evaluate(_max2);
    [Benchmark] public JsValue Warm_Max4() => _warm.Evaluate(_max4);
    [Benchmark] public JsValue Warm_Max16() => _warm.Evaluate(_max16);
    [Benchmark] public JsValue Warm_Max64() => _warm.Evaluate(_max64);
    [Benchmark] public JsValue Warm_Min4() => _warm.Evaluate(_min4);
    [Benchmark] public JsValue Warm_Hypot4() => _warm.Evaluate(_hypot4);

    private static string BuildVariadicCall(string fn, int arity)
    {
        var sb = new System.Text.StringBuilder(fn.Length + 6 * arity);
        sb.Append(fn).Append('(');
        for (var i = 0; i < arity; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(i + 1);
        }
        sb.Append(')');
        return sb.ToString();
    }
}
