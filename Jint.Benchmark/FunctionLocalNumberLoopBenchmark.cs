using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Function-local numeric loop patterns: accumulators and counters held in declarative
/// environment slots. These are the workloads where transient JsNumber allocations dominate
/// (values outside the int cache allocate per write), which the rest of the suite under-covers
/// because its loops live at script top level where bindings are global-object properties.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all six scripts, so each
/// row was measured on an engine carrying the other five rows' globals (every one of them declares
/// <c>function f</c>, so they collided outright) plus their handler-tree, call-site and
/// environment-reuse state — which makes a row's number depend on which siblings exist and on what a
/// change did to <em>them</em>. The rows still measure the warm loop, and engine construction and
/// warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not
/// comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class FunctionLocalNumberLoopBenchmark
{
    private IsolatedScript _doubleAccumulator;
    private IsolatedScript _largeIntCounter;
    private IsolatedScript _accumulatorWithCallArg;
    private IsolatedScript _mixedArithmetic;
    private IsolatedScript _whileAccumulator;
    private IsolatedScript _doWhileCounter;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // pure accumulator: every += result is an uncached double
        _doubleAccumulator = IsolatedScript.Warm("""
            function f() {
                var s = 0.5;
                for (var i = 0; i < 100000; i++) {
                    s += 0.25;
                }
                return s;
            }
            f();
            """);

        // counter beyond the interned-int range with a materializing loop test
        _largeIntCounter = IsolatedScript.Warm("""
            function f() {
                var n = 0;
                for (var i = 0; i < 100000; i++) {
                    n += 1;
                }
                return n;
            }
            f();
            """);

        // unboxed write followed by a materializing read every iteration
        _accumulatorWithCallArg = IsolatedScript.Warm("""
            function g(v) { return v > 0; }
            function f() {
                var s = 0.5;
                var hits = 0;
                for (var i = 0; i < 100000; i++) {
                    s += 0.25;
                    if (g(s)) { hits++; }
                }
                return hits;
            }
            f();
            """);

        // several locals updated per iteration (numeric kernel shape)
        _mixedArithmetic = IsolatedScript.Warm("""
            function f() {
                var x = 0.1;
                var y = 0.2;
                var sum = 0;
                for (var i = 0; i < 100000; i++) {
                    x *= 1.0000001;
                    y += x;
                    sum += y;
                    sum -= x * 0.5;
                }
                return sum;
            }
            f();
            """);

        // the while/do-while twins of the for-loop accumulator: same tight-lane body shapes,
        // different loop statements
        _whileAccumulator = IsolatedScript.Warm("""
            function f() {
                var s = 0.5;
                var i = 0;
                while (i < 100000) {
                    s += 0.25;
                    i++;
                }
                return s;
            }
            f();
            """);

        _doWhileCounter = IsolatedScript.Warm("""
            function f() {
                var n = 0;
                var i = 0;
                do {
                    n += 1;
                    i++;
                } while (i < 100000);
                return n;
            }
            f();
            """);
    }

    [Benchmark]
    public JsValue DoubleAccumulator() => _doubleAccumulator.Run();

    [Benchmark]
    public JsValue LargeIntCounter() => _largeIntCounter.Run();

    [Benchmark]
    public JsValue AccumulatorWithCallArg() => _accumulatorWithCallArg.Run();

    [Benchmark]
    public JsValue MixedArithmetic() => _mixedArithmetic.Run();

    [Benchmark]
    public JsValue WhileAccumulator() => _whileAccumulator.Run();

    [Benchmark]
    public JsValue DoWhileCounter() => _doWhileCounter.Run();
}
