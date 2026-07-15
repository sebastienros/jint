using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Function-local numeric loop patterns: accumulators and counters held in declarative
/// environment slots. These are the workloads where transient JsNumber allocations dominate
/// (values outside the int cache allocate per write), which the rest of the suite under-covers
/// because its loops live at script top level where bindings are global-object properties.
/// </summary>
[MemoryDiagnoser]
public class FunctionLocalNumberLoopBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _doubleAccumulator;
    private Prepared<Script> _largeIntCounter;
    private Prepared<Script> _accumulatorWithCallArg;
    private Prepared<Script> _mixedArithmetic;
    private Prepared<Script> _whileAccumulator;
    private Prepared<Script> _doWhileCounter;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // pure accumulator: every += result is an uncached double
        _doubleAccumulator = Engine.PrepareScript("""
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
        _largeIntCounter = Engine.PrepareScript("""
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
        _accumulatorWithCallArg = Engine.PrepareScript("""
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
        _mixedArithmetic = Engine.PrepareScript("""
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
        _whileAccumulator = Engine.PrepareScript("""
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

        _doWhileCounter = Engine.PrepareScript("""
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

        _engine = new Engine();
        _engine.Evaluate(_doubleAccumulator);
        _engine.Evaluate(_largeIntCounter);
        _engine.Evaluate(_accumulatorWithCallArg);
        _engine.Evaluate(_mixedArithmetic);
        _engine.Evaluate(_whileAccumulator);
        _engine.Evaluate(_doWhileCounter);
    }

    [Benchmark]
    public JsValue DoubleAccumulator() => _engine.Evaluate(_doubleAccumulator);

    [Benchmark]
    public JsValue LargeIntCounter() => _engine.Evaluate(_largeIntCounter);

    [Benchmark]
    public JsValue AccumulatorWithCallArg() => _engine.Evaluate(_accumulatorWithCallArg);

    [Benchmark]
    public JsValue MixedArithmetic() => _engine.Evaluate(_mixedArithmetic);

    [Benchmark]
    public JsValue WhileAccumulator() => _engine.Evaluate(_whileAccumulator);

    [Benchmark]
    public JsValue DoWhileCounter() => _engine.Evaluate(_doWhileCounter);
}
