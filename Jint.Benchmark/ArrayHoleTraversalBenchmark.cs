using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Traversal cost of holey (but still dense-backed) arrays versus packed ones. Reading a hole
/// falls off the dense fast path and probes the prototype chain per element (the spec requires a
/// HasProperty/Get walk), so index loops, join and indexOf pay a per-hole penalty that a packed
/// array never sees. The dense/holey lane pairs measure that gap; `in` exercises the raw
/// HasProperty probe.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs the <c>dense</c>/<c>holey</c> fixture — and warmed with its own script and nothing else (see
/// <see cref="IsolatedScript"/>). It used to be one engine warmed with all seven row scripts, so each
/// row was measured on an engine carrying the other six rows' handler-tree and call-site caches, and
/// every row's <c>function f</c> declaration collided on the shared global object. That matters most
/// for a paired benchmark like this one: the point of a dense/holey pair is that only one half should
/// move, which cross-row state can quietly break. The rows still measure warm traversal, and engine
/// construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this
/// class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ArrayHoleTraversalBenchmark
{
    private IsolatedScript _sumDense;
    private IsolatedScript _sumHoley;
    private IsolatedScript _joinDense;
    private IsolatedScript _joinHoley;
    private IsolatedScript _indexOfMissDense;
    private IsolatedScript _indexOfMissHoley;
    private IsolatedScript _inOperatorHoley;

    private const string SetupSource = """
        var dense = [];
        var holey = [];
        (function () {
            for (var i = 0; i < 8000; i++) { dense[i] = i; }
            // every 4th index present; same length as dense, 75% holes
            for (var i = 0; i < 8000; i += 4) { holey[i] = i; }
            holey[7999] = 7999;
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
        _sumDense = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 20; n++) {
                    for (var i = 0; i < 8000; i++) { s += dense[i] | 0; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _sumHoley = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 20; n++) {
                    for (var i = 0; i < 8000; i++) { s += holey[i] | 0; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _joinDense = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var len = 0;
                for (var n = 0; n < 10; n++) { len += dense.join(',').length; }
                return len;
            }
            f();
            """), CreateEngine);

        _joinHoley = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var len = 0;
                for (var n = 0; n < 10; n++) { len += holey.join(',').length; }
                return len;
            }
            f();
            """), CreateEngine);

        _indexOfMissDense = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 50; n++) { s += dense.indexOf(-1); }
                return s;
            }
            f();
            """), CreateEngine);

        _indexOfMissHoley = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 50; n++) { s += holey.indexOf(-1); }
                return s;
            }
            f();
            """), CreateEngine);

        _inOperatorHoley = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 20; n++) {
                    for (var i = 0; i < 8000; i++) { if (i in holey) { s++; } }
                }
                return s;
            }
            f();
            """), CreateEngine);
    }

    [Benchmark]
    public JsValue SumDense() => _sumDense.Run();

    [Benchmark]
    public JsValue SumHoley() => _sumHoley.Run();

    [Benchmark]
    public JsValue JoinDense() => _joinDense.Run();

    [Benchmark]
    public JsValue JoinHoley() => _joinHoley.Run();

    [Benchmark]
    public JsValue IndexOfMissDense() => _indexOfMissDense.Run();

    [Benchmark]
    public JsValue IndexOfMissHoley() => _indexOfMissHoley.Run();

    [Benchmark]
    public JsValue InOperatorHoley() => _inOperatorHoley.Run();
}
