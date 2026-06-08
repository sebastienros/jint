using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates `new Date()` current-time construction (the stopwatch.js hot allocation) and
/// guards the explicit-milliseconds constructor which must keep full TimeClip semantics.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class DateConstructionBenchmarks
{
    private Engine _engine = null!;
    private Prepared<Script> _newDateNow;
    private Prepared<Script> _newDateMillis;

    [GlobalSetup]
    public void Setup()
    {
        _newDateNow = Engine.PrepareScript("""
            (function() {
                var last = null;
                for (var i = 0; i < 100000; i++) {
                    last = new Date();
                }
                return last;
            })();
            """, strict: true);

        _newDateMillis = Engine.PrepareScript("""
            (function() {
                var last = null;
                for (var i = 0; i < 100000; i++) {
                    last = new Date(1717774870000 + i);
                }
                return last;
            })();
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        _engine.Evaluate(_newDateNow);
        _engine.Evaluate(_newDateMillis);
    }

    [Benchmark]
    public JsValue NewDateNow() => _engine.Evaluate(_newDateNow);

    [Benchmark]
    public JsValue NewDateMillis() => _engine.Evaluate(_newDateMillis);
}
