using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates `new Date()` current-time construction (the stopwatch.js hot allocation) and
/// guards the explicit-milliseconds constructor which must keep full TimeClip semantics.
///
/// <para><b>Engine isolation.</b> Each row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with both scripts, so each
/// row was measured on an engine already carrying the other row's handler-tree entries, call-site caches
/// and Date construction state — which makes a row's number depend on what a change did to its sibling.
/// Both scripts wrap their work in an IIFE, so no globals collided here, but the engine-level caches did.
/// The rows still measure warm construction, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class DateConstructionBenchmarks
{
    private IsolatedScript _newDateNow;
    private IsolatedScript _newDateMillis;

    private static Engine CreateEngine() => new(static options => options.Strict = true);

    [GlobalSetup]
    public void Setup()
    {
        _newDateNow = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var last = null;
                for (var i = 0; i < 100000; i++) {
                    last = new Date();
                }
                return last;
            })();
            """, strict: true), CreateEngine);

        _newDateMillis = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var last = null;
                for (var i = 0; i < 100000; i++) {
                    last = new Date(1717774870000 + i);
                }
                return last;
            })();
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue NewDateNow() => _newDateNow.Run();

    [Benchmark]
    public JsValue NewDateMillis() => _newDateMillis.Run();
}
