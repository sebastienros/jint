using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Decomposes the per-iteration cost of a hot function-local loop with every fast path engaged
/// (fixed slots, pooled env, slot caches, unboxed counters): the empty loop isolates the
/// for-statement machinery (test, update, statement dispatch), and each body row adds exactly
/// one statement on top so the delta attributes cost to that construct. 100k iterations per op.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class LoopDispatchBenchmarks
{
    private Engine _engine = null!;
    private Prepared<Script> _emptyLoop;
    private Prepared<Script> _variableBoundLoop;
    private Prepared<Script> _counterAdd;
    private Prepared<Script> _localCopy;
    private Prepared<Script> _stringAppend;
    private Prepared<Script> _comparisonOnly;

    [GlobalSetup]
    public void Setup()
    {
        // pure loop machinery: test + update + (empty) body dispatch
        _emptyLoop = Engine.PrepareScript("""
            function f() { for (var i = 0; i < 100000; i++) { } return i; }
            f();
            """);

        // same machinery with a variable bound (i < n over two locals)
        _variableBoundLoop = Engine.PrepareScript("""
            function f() { var n = 100000; for (var i = 0; i < n; i++) { } return i; }
            f();
            """);

        // + one numeric compound assignment (unboxed discard lane)
        _counterAdd = Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { n += 1; } return n; }
            f();
            """);

        // + one plain slot-to-slot assignment
        _localCopy = Engine.PrepareScript("""
            function f() { var a = 1, b = 0; for (var i = 0; i < 100000; i++) { b = a; } return b; }
            f();
            """);

        // + one string compound assignment (rope append, slot lane) — the dromaeo-core-eval body shape
        _stringAppend = Engine.PrepareScript("""
            function f() { var s = ''; for (var i = 0; i < 100000; i++) { s += 'a'; } return s.length; }
            f();
            """);

        // + one comparison whose result is discarded through an if with empty branches
        _comparisonOnly = Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { if (i < 50000) { } } return n; }
            f();
            """);

        _engine = new Engine();
        _engine.Evaluate(_emptyLoop);
        _engine.Evaluate(_variableBoundLoop);
        _engine.Evaluate(_counterAdd);
        _engine.Evaluate(_localCopy);
        _engine.Evaluate(_stringAppend);
        _engine.Evaluate(_comparisonOnly);
    }

    [Benchmark(Baseline = true)]
    public JsValue EmptyLoop() => _engine.Evaluate(_emptyLoop);

    [Benchmark]
    public JsValue VariableBoundLoop() => _engine.Evaluate(_variableBoundLoop);

    [Benchmark]
    public JsValue CounterAdd() => _engine.Evaluate(_counterAdd);

    [Benchmark]
    public JsValue LocalCopy() => _engine.Evaluate(_localCopy);

    [Benchmark]
    public JsValue StringAppend() => _engine.Evaluate(_stringAppend);

    [Benchmark]
    public JsValue ComparisonOnly() => _engine.Evaluate(_comparisonOnly);
}
