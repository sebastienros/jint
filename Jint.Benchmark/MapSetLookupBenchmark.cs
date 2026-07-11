using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Map/Set read/has/delete hot loops — the memoization-cache and dedup shapes real code runs.
/// Probe keys are prebuilt and selected with an inline LCG (50% absent on the Mixed rows) so rows
/// measure the lookup path, not key construction, and the branch predictor cannot memorize hits.
/// 100k operations per op inside a function frame.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class MapSetLookupBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _mapGetHit;
    private Prepared<Script> _mapGetMixed;
    private Prepared<Script> _mapHasMixed;
    private Prepared<Script> _setHasMixed;
    private Prepared<Script> _mapSetDeleteChurn;
    private Prepared<Script> _memoizePattern;

    internal const string SetupSource = """
        var m = new Map();
        var intSet = new Set();
        var cache = new Map();
        var hitKeys = [];
        var mixedKeys = [];
        var memoKeys = [];
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
            }
        })();
        """;

    internal const string MapGetHitSource = """
        function f() {
            var seed = 20260711;
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                s += m.get(hitKeys[(seed >>> 4) & 1023]);
            }
            return s;
        }
        f();
        """;

    internal const string MemoizePatternSource = """
        function f() {
            var seed = 20260711;
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                var k = memoKeys[(seed >>> 4) & 2047];
                var v = cache.get(k);
                if (v === undefined) { v = k.length * 2; cache.set(k, v); }
                s += v;
            }
            return s;
        }
        f();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _mapGetHit = Engine.PrepareScript(MapGetHitSource);

        // ~50% of probe keys are absent
        _mapGetMixed = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    var v = m.get(mixedKeys[(seed >>> 4) & 1023]);
                    s += (v === undefined) ? 0 : v;
                }
                return s;
            }
            f();
            """);

        _mapHasMixed = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    if (m.has(mixedKeys[(seed >>> 4) & 1023])) { s++; }
                }
                return s;
            }
            f();
            """);

        // int keys: no key allocation at all
        _setHasMixed = Engine.PrepareScript("""
            function f() {
                var seed = 20260711;
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    seed = (seed * 1664525 + 1013904223) | 0;
                    if (intSet.has((seed >>> 4) % 20000)) { s++; }
                }
                return s;
            }
            f();
            """);

        // sliding window: add + delete keeping ~1k live entries
        _mapSetDeleteChurn = Engine.PrepareScript("""
            function f() {
                var w = new Map();
                for (var i = 0; i < 100000; i++) {
                    w.set(i & 65535, i);
                    if (i >= 1000) { w.delete((i - 1000) & 65535); }
                }
                return w.size;
            }
            f();
            """);

        _memoizePattern = Engine.PrepareScript(MemoizePatternSource);

        _engine.Evaluate(_mapGetHit);
        _engine.Evaluate(_mapGetMixed);
        _engine.Evaluate(_mapHasMixed);
        _engine.Evaluate(_setHasMixed);
        _engine.Evaluate(_mapSetDeleteChurn);
        _engine.Evaluate(_memoizePattern);
    }

    [Benchmark]
    public JsValue MapGetHit() => _engine.Evaluate(_mapGetHit);

    [Benchmark]
    public JsValue MapGetMixed() => _engine.Evaluate(_mapGetMixed);

    [Benchmark]
    public JsValue MapHasMixed() => _engine.Evaluate(_mapHasMixed);

    [Benchmark]
    public JsValue SetHasMixed() => _engine.Evaluate(_setHasMixed);

    [Benchmark]
    public JsValue MapSetDeleteChurn() => _engine.Evaluate(_mapSetDeleteChurn);

    [Benchmark]
    public JsValue MemoizePattern() => _engine.Evaluate(_memoizePattern);
}
