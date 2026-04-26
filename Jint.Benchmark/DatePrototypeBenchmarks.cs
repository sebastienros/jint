using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Date.prototype getter/setter methods. Post-source-gen the prototype
/// uses [JsFunction] for all 49 methods + [JsSymbolFunction] for [Symbol.toPrimitive]. The
/// toGMTString === toUTCString aliasing is preserved via post-init SetOwnProperty.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class DatePrototypeBenchmarks
{
    private Engine _warm = null!;
    private Prepared<Script> _now;
    private Prepared<Script> _getTime;
    private Prepared<Script> _toIso;
    private Prepared<Script> _setMonth;
    private Prepared<Script> _toString;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _now      = Engine.PrepareScript("Date.now()");
        _getTime  = Engine.PrepareScript("new Date(2026, 0, 1).getTime()");
        _toIso    = Engine.PrepareScript("new Date(2026, 0, 1).toISOString()");
        _setMonth = Engine.PrepareScript("var d = new Date(2026, 0, 1); d.setMonth(5); d.getMonth()");
        _toString = Engine.PrepareScript("new Date(2026, 0, 1).toString()");

        _warm = new Engine();
        _warm.Evaluate(_now);
        _warm.Evaluate(_getTime);
        _warm.Evaluate(_toIso);
        _warm.Evaluate(_setMonth);
        _warm.Evaluate(_toString);
    }

    [Benchmark] public JsValue Warm_DateNow() => _warm.Evaluate(_now);
    [Benchmark] public JsValue Warm_GetTime() => _warm.Evaluate(_getTime);
    [Benchmark] public JsValue Warm_ToISOString() => _warm.Evaluate(_toIso);
    [Benchmark] public JsValue Warm_SetMonth() => _warm.Evaluate(_setMonth);
    [Benchmark] public JsValue Warm_ToString() => _warm.Evaluate(_toString);
}
