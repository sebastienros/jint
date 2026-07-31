using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Map/Set read/has/delete hot loops — the memoization-cache and dedup shapes real code runs.
/// Probe keys are prebuilt and selected with an inline LCG (50% absent on the Mixed rows) so rows
/// measure the lookup path, not key construction, and the branch predictor cannot memorize hits.
/// 100k operations per op inside a function frame.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <see cref="CreateEngine"/>,
/// which re-runs <see cref="SetupSource"/> so each engine owns its own maps, sets and probe-key arrays —
/// and warmed with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one
/// engine warmed with all six row scripts, so each row was measured on an engine carrying the other five
/// rows' globals (every one of them declares <c>function f</c>, so they collided outright) plus their
/// handler-tree and call-site state — and <see cref="MemoizePattern"/>'s warm-up additionally left the
/// shared <c>cache</c> map fully populated for everyone else. The rows still measure warm lookups, and
/// engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from
/// this class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class MapSetLookupBenchmark
{
    private IsolatedScript _mapGetHit;
    private IsolatedScript _mapGetMixed;
    private IsolatedScript _mapHasMixed;
    private IsolatedScript _setHasMixed;
    private IsolatedScript _mapSetDeleteChurn;
    private IsolatedScript _memoizePattern;

    // Mixing is precomputed at setup into `order` (see ModernOperatorsBenchmark note): a
    // per-iteration JS LCG boxes JsNumber transients that would dominate these lookup rows.
    internal const string SetupSource = """
        var m = new Map();
        var intSet = new Set();
        var cache = new Map();
        var hitKeys = [];
        var mixedKeys = [];
        var memoKeys = [];
        var probeInts = [];
        var order = [];
        (function () {
            var seed = 20260711;
            for (var i = 0; i < 10000; i++) {
                m.set('k' + i, i);
                intSet.add(i);
            }
            for (var i = 0; i < 1024; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                hitKeys.push('k' + ((seed >>> 4) % 10000));
                mixedKeys.push('k' + ((seed >>> 5) % 20000));
            }
            for (var i = 0; i < 2048; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                memoKeys.push('memo' + ((seed >>> 4) % 2048));
                probeInts.push((seed >>> 5) % 20000);
            }
            for (var i = 0; i < 8192; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                order.push((seed >>> 7) & 2047);
            }
        })();
        """;

    internal const string MapGetHitSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                s += m.get(hitKeys[order[i & 8191] & 1023]);
            }
            return s;
        }
        f();
        """;

    internal const string MemoizePatternSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                var k = memoKeys[order[i & 8191]];
                var v = cache.get(k);
                if (v === undefined) { v = k.length * 2; cache.set(k, v); }
                s += v;
            }
            return s;
        }
        f();
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
        _mapGetHit = IsolatedScript.Warm(Engine.PrepareScript(MapGetHitSource), CreateEngine);

        // ~50% of probe keys are absent
        _mapGetMixed = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    var v = m.get(mixedKeys[order[i & 8191] & 1023]);
                    s += (v === undefined) ? 0 : v;
                }
                return s;
            }
            f();
            """), CreateEngine);

        _mapHasMixed = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if (m.has(mixedKeys[order[i & 8191] & 1023])) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        // int keys: no key allocation at all
        _setHasMixed = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if (intSet.has(probeInts[order[i & 8191]])) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        // sliding window: add + delete keeping ~1k live entries
        _mapSetDeleteChurn = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var w = new Map();
                for (var i = 0; i < 100000; i++) {
                    w.set(i & 65535, i);
                    if (i >= 1000) { w.delete((i - 1000) & 65535); }
                }
                return w.size;
            }
            f();
            """), CreateEngine);

        _memoizePattern = IsolatedScript.Warm(Engine.PrepareScript(MemoizePatternSource), CreateEngine);
    }

    [Benchmark]
    public JsValue MapGetHit() => _mapGetHit.Run();

    [Benchmark]
    public JsValue MapGetMixed() => _mapGetMixed.Run();

    [Benchmark]
    public JsValue MapHasMixed() => _mapHasMixed.Run();

    [Benchmark]
    public JsValue SetHasMixed() => _setHasMixed.Run();

    [Benchmark]
    public JsValue MapSetDeleteChurn() => _mapSetDeleteChurn.Run();

    [Benchmark]
    public JsValue MemoizePattern() => _memoizePattern.Run();
}
