using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the value-kind array for-of hot path. A dense (and a holey) array is built once in
/// setup, then iterated with for-of in a tight repeated loop so the measured region is the
/// per-element step rather than array construction. Gates the ArrayIterator/ArrayLikeIterator
/// TryStepValue override that hands the element straight to the loop instead of allocating an
/// IteratorResult object per element — watch the Allocated column.
/// </summary>
[MemoryDiagnoser]
public class ForOfArrayBenchmark
{
    private const int ArraySize = 1000;
    private const int Repeat = 1000;

    private Engine _engine = null!;
    private Prepared<Script> _forOfDense;
    private Prepared<Script> _forOfHoley;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute($$"""
            var denseArr = [];
            for (var i = 0; i < {{ArraySize}}; i++) { denseArr[i] = i; }
            var holeyArr = [];
            for (var i = 0; i < {{ArraySize}}; i += 2) { holeyArr[i] = i; }
            """);

        _forOfDense = Engine.PrepareScript($$"""
            function f() {
                var s = 0;
                for (var k = 0; k < {{Repeat}}; k++) {
                    for (const x of denseArr) { s += x; }
                }
                return s;
            }
            f();
            """);

        _forOfHoley = Engine.PrepareScript($$"""
            function f() {
                var s = 0;
                for (var k = 0; k < {{Repeat}}; k++) {
                    for (const x of holeyArr) { if (x !== undefined) { s += x; } }
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_forOfDense);
        _engine.Evaluate(_forOfHoley);
    }

    [Benchmark]
    public JsValue ForOfDenseArray() => _engine.Evaluate(_forOfDense);

    [Benchmark]
    public JsValue ForOfHoleyArray() => _engine.Evaluate(_forOfHoley);
}
