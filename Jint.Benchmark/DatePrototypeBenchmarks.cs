using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Date.prototype getter/setter methods. Post-source-gen the prototype
/// uses [JsFunction] for all 49 methods + [JsSymbolFunction] for [Symbol.toPrimitive]. The
/// toGMTString === toUTCString aliasing is preserved via post-init SetOwnProperty.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all five scripts, so a
/// row was measured on an engine that had already lazily initialized and cached the other four rows'
/// Date.prototype members and their call sites, plus <c>Warm_SetMonth</c>'s global <c>d</c> — which makes
/// a row's number depend on what a change did to its siblings. The rows still measure warm dispatch, and
/// engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from
/// this class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class DatePrototypeBenchmarks
{
    private IsolatedScript _now;
    private IsolatedScript _getTime;
    private IsolatedScript _toIso;
    private IsolatedScript _setMonth;
    private IsolatedScript _toString;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _now      = IsolatedScript.Warm("Date.now()");
        _getTime  = IsolatedScript.Warm("new Date(2026, 0, 1).getTime()");
        _toIso    = IsolatedScript.Warm("new Date(2026, 0, 1).toISOString()");
        _setMonth = IsolatedScript.Warm("var d = new Date(2026, 0, 1); d.setMonth(5); d.getMonth()");
        _toString = IsolatedScript.Warm("new Date(2026, 0, 1).toString()");
    }

    [Benchmark] public JsValue Warm_DateNow() => _now.Run();
    [Benchmark] public JsValue Warm_GetTime() => _getTime.Run();
    [Benchmark] public JsValue Warm_ToISOString() => _toIso.Run();
    [Benchmark] public JsValue Warm_SetMonth() => _setMonth.Run();
    [Benchmark] public JsValue Warm_ToString() => _toString.Run();
}
