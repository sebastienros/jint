using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Traversal cost of holey (but still dense-backed) arrays versus packed ones. Reading a hole
/// falls off the dense fast path and probes the prototype chain per element (the spec requires a
/// HasProperty/Get walk), so index loops, join and indexOf pay a per-hole penalty that a packed
/// array never sees. The dense/holey lane pairs measure that gap; `in` exercises the raw
/// HasProperty probe.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ArrayHoleTraversalBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _sumDense;
    private Prepared<Script> _sumHoley;
    private Prepared<Script> _joinDense;
    private Prepared<Script> _joinHoley;
    private Prepared<Script> _indexOfMissDense;
    private Prepared<Script> _indexOfMissHoley;
    private Prepared<Script> _inOperatorHoley;

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

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _sumDense = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 20; n++) {
                    for (var i = 0; i < 8000; i++) { s += dense[i] | 0; }
                }
                return s;
            }
            f();
            """);

        _sumHoley = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 20; n++) {
                    for (var i = 0; i < 8000; i++) { s += holey[i] | 0; }
                }
                return s;
            }
            f();
            """);

        _joinDense = Engine.PrepareScript("""
            function f() {
                var len = 0;
                for (var n = 0; n < 10; n++) { len += dense.join(',').length; }
                return len;
            }
            f();
            """);

        _joinHoley = Engine.PrepareScript("""
            function f() {
                var len = 0;
                for (var n = 0; n < 10; n++) { len += holey.join(',').length; }
                return len;
            }
            f();
            """);

        _indexOfMissDense = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 50; n++) { s += dense.indexOf(-1); }
                return s;
            }
            f();
            """);

        _indexOfMissHoley = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 50; n++) { s += holey.indexOf(-1); }
                return s;
            }
            f();
            """);

        _inOperatorHoley = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var n = 0; n < 20; n++) {
                    for (var i = 0; i < 8000; i++) { if (i in holey) { s++; } }
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_sumDense);
        _engine.Evaluate(_sumHoley);
        _engine.Evaluate(_joinDense);
        _engine.Evaluate(_joinHoley);
        _engine.Evaluate(_indexOfMissDense);
        _engine.Evaluate(_indexOfMissHoley);
        _engine.Evaluate(_inOperatorHoley);
    }

    [Benchmark]
    public JsValue SumDense() => _engine.Evaluate(_sumDense);

    [Benchmark]
    public JsValue SumHoley() => _engine.Evaluate(_sumHoley);

    [Benchmark]
    public JsValue JoinDense() => _engine.Evaluate(_joinDense);

    [Benchmark]
    public JsValue JoinHoley() => _engine.Evaluate(_joinHoley);

    [Benchmark]
    public JsValue IndexOfMissDense() => _engine.Evaluate(_indexOfMissDense);

    [Benchmark]
    public JsValue IndexOfMissHoley() => _engine.Evaluate(_indexOfMissHoley);

    [Benchmark]
    public JsValue InOperatorHoley() => _engine.Evaluate(_inOperatorHoley);
}
