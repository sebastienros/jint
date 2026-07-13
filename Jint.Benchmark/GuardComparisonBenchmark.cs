using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Guard-style strict comparisons over LCG-mixed inputs the branch predictor cannot memorize:
/// `v === undefined`, `v === null`, `v == null` and `typeof v === '...'` — the idioms defensive
/// library code (linq.js, lodash, handlebars) runs on nearly every call.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class GuardComparisonBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _isUndefinedGuard;
    private Prepared<Script> _isNullGuard;
    private Prepared<Script> _looseNullGuard;
    private Prepared<Script> _typeofStringGuard;
    private Prepared<Script> _typeofUndefinedGuard;

    private const string SetupSource = """
        var mixedVals = [];
        var order = [];
        (function () {
            var seed = 20260713;
            for (var i = 0; i < 1024; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                var pick = (seed >>> 4) & 3;
                if (pick === 0) { mixedVals.push(seed & 255); }
                else if (pick === 1) { mixedVals.push('s' + (seed & 15)); }
                else if (pick === 2) { mixedVals.push(undefined); }
                else { mixedVals.push(((seed >>> 6) & 1) === 0 ? null : { v: i }); }
            }
            for (var i = 0; i < 8192; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                order.push((seed >>> 7) & 1023);
            }
        })();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _isUndefinedGuard = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (v === undefined) { s++; }
                    if (v !== undefined) { s += 2; }
                }
                return s;
            }
            f();
            """);

        _isNullGuard = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (v === null) { s++; }
                    if (v !== null) { s += 2; }
                }
                return s;
            }
            f();
            """);

        _looseNullGuard = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (v == null) { s++; }
                }
                return s;
            }
            f();
            """);

        _typeofStringGuard = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (typeof v === 'string') { s++; }
                    if (typeof v !== 'number') { s += 2; }
                }
                return s;
            }
            f();
            """);

        _typeofUndefinedGuard = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (typeof v === 'undefined') { s++; }
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_isUndefinedGuard);
        _engine.Evaluate(_isNullGuard);
        _engine.Evaluate(_looseNullGuard);
        _engine.Evaluate(_typeofStringGuard);
        _engine.Evaluate(_typeofUndefinedGuard);
    }

    [Benchmark]
    public JsValue IsUndefinedGuard() => _engine.Evaluate(_isUndefinedGuard);

    [Benchmark]
    public JsValue IsNullGuard() => _engine.Evaluate(_isNullGuard);

    [Benchmark]
    public JsValue LooseNullGuard() => _engine.Evaluate(_looseNullGuard);

    [Benchmark]
    public JsValue TypeofStringGuard() => _engine.Evaluate(_typeofStringGuard);

    [Benchmark]
    public JsValue TypeofUndefinedGuard() => _engine.Evaluate(_typeofUndefinedGuard);
}
