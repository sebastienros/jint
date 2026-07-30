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
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all six row
/// scripts, so each row was measured on an engine carrying the other five rows' handler-tree entries
/// and their per-call-site caches on <c>Math</c> — the very state that decides whether a variadic call
/// takes the fast lane — which makes a row's number depend on which siblings exist and on what a change
/// did to <em>them</em>. The rows still measure warm variadic dispatch, and engine construction and
/// warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not
/// comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class VariadicCoercionBenchmarks
{
    private IsolatedScript _max2;
    private IsolatedScript _max4;
    private IsolatedScript _max16;
    private IsolatedScript _max64;
    private IsolatedScript _min4;
    private IsolatedScript _hypot4;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _max2  = IsolatedScript.Warm("Math.max(1, 2)");
        _max4  = IsolatedScript.Warm("Math.max(1, 2, 3, 4)");
        _max16 = IsolatedScript.Warm("Math.max(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16)");
        _max64 = IsolatedScript.Warm(BuildVariadicCall("Math.max", 64));
        _min4  = IsolatedScript.Warm("Math.min(1, 2, 3, 4)");
        _hypot4 = IsolatedScript.Warm("Math.hypot(1, 2, 3, 4)");
    }

    [Benchmark] public JsValue Warm_Max2() => _max2.Run();
    [Benchmark] public JsValue Warm_Max4() => _max4.Run();
    [Benchmark] public JsValue Warm_Max16() => _max16.Run();
    [Benchmark] public JsValue Warm_Max64() => _max64.Run();
    [Benchmark] public JsValue Warm_Min4() => _min4.Run();
    [Benchmark] public JsValue Warm_Hypot4() => _hypot4.Run();

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
