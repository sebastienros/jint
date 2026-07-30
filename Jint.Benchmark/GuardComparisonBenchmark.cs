using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Guard-style strict comparisons over LCG-mixed inputs the branch predictor cannot memorize:
/// `v === undefined`, `v === null`, `v == null` and `typeof v === '...'` — the idioms defensive
/// library code (linq.js, lodash, handlebars) runs on nearly every call.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs <see cref="SetupSource"/> so each engine owns its own <c>mixedVals</c>/<c>order</c> — and
/// warmed with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one
/// engine warmed with all six scripts, so each row was measured on an engine carrying the other five
/// rows' globals (every one of them declares <c>function f</c>, so they collided outright) plus their
/// handler-tree and call-site state, which makes a row's number depend on which siblings exist and on
/// what a change did to <em>them</em>. The rows still measure warm comparison dispatch, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this
/// class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class GuardComparisonBenchmark
{
    private IsolatedScript _isUndefinedGuard;
    private IsolatedScript _isNullGuard;
    private IsolatedScript _looseNullGuard;
    private IsolatedScript _typeofStringGuard;
    private IsolatedScript _typeofUndefinedGuard;
    private IsolatedScript _logicalGuard;

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

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _isUndefinedGuard = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """), CreateEngine);

        _isNullGuard = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """), CreateEngine);

        _looseNullGuard = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (v == null) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _typeofStringGuard = IsolatedScript.Warm(Engine.PrepareScript("""
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
            """), CreateEngine);

        _typeofUndefinedGuard = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (typeof v === 'undefined') { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        // Composed guards: `&&`/`||`/`!` over comparisons in a boolean `if` context. Each
        // comparison feeds the enclosing logical operator's unboxed GetBooleanValue, so the whole
        // condition stays off the JsBoolean-materialization path.
        _logicalGuard = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = mixedVals[order[i & 8191]];
                    if (v !== undefined && v !== null) { s++; }
                    if (v === undefined || v === null) { s += 2; }
                    if (typeof v === 'string' || typeof v === 'number') { s += 3; }
                    if (!(v === null)) { s += 4; }
                }
                return s;
            }
            f();
            """), CreateEngine);
    }

    [Benchmark]
    public JsValue IsUndefinedGuard() => _isUndefinedGuard.Run();

    [Benchmark]
    public JsValue IsNullGuard() => _isNullGuard.Run();

    [Benchmark]
    public JsValue LooseNullGuard() => _looseNullGuard.Run();

    [Benchmark]
    public JsValue TypeofStringGuard() => _typeofStringGuard.Run();

    [Benchmark]
    public JsValue TypeofUndefinedGuard() => _typeofUndefinedGuard.Run();

    [Benchmark]
    public JsValue LogicalGuard() => _logicalGuard.Run();
}
