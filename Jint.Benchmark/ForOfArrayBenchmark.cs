using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the value-kind array for-of hot path. A dense (and a holey) array is built once in
/// setup, then iterated with for-of in a tight repeated loop so the measured region is the
/// per-element step rather than array construction. Gates the ArrayIterator/ArrayLikeIterator
/// TryStepValue override that hands the element straight to the loop instead of allocating an
/// IteratorResult object per element — watch the Allocated column.
///
/// <para><b>Engine isolation.</b> Each row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs the array fixture so each engine owns its own <c>denseArr</c>/<c>holeyArr</c> — and warmed
/// with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one engine
/// warmed with both scripts, so each row was measured on an engine already carrying the other row's
/// <c>function f</c> global (both scripts declare it, so they collided outright) and its handler-tree
/// and call-site caches, which makes a row's number depend on what a change did to its sibling. The
/// rows still measure the warm per-element step, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class ForOfArrayBenchmark
{
    private const int ArraySize = 1000;
    private const int Repeat = 1000;

    private IsolatedScript _forOfDense;
    private IsolatedScript _forOfHoley;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute($$"""
            var denseArr = [];
            for (var i = 0; i < {{ArraySize}}; i++) { denseArr[i] = i; }
            var holeyArr = [];
            for (var i = 0; i < {{ArraySize}}; i += 2) { holeyArr[i] = i; }
            """);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _forOfDense = IsolatedScript.Warm(Engine.PrepareScript($$"""
            function f() {
                var s = 0;
                for (var k = 0; k < {{Repeat}}; k++) {
                    for (const x of denseArr) { s += x; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _forOfHoley = IsolatedScript.Warm(Engine.PrepareScript($$"""
            function f() {
                var s = 0;
                for (var k = 0; k < {{Repeat}}; k++) {
                    for (const x of holeyArr) { if (x !== undefined) { s += x; } }
                }
                return s;
            }
            f();
            """), CreateEngine);
    }

    [Benchmark]
    public JsValue ForOfDenseArray() => _forOfDense.Run();

    [Benchmark]
    public JsValue ForOfHoleyArray() => _forOfHoley.Run();
}
