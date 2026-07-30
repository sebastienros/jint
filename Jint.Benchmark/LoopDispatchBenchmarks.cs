using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Decomposes the per-iteration cost of a hot function-local loop with every fast path engaged
/// (fixed slots, pooled env, slot caches, unboxed counters): the empty loop isolates the
/// for-statement machinery (test, update, statement dispatch), and each body row adds exactly
/// one statement on top so the delta attributes cost to that construct. 100k iterations per op.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all
/// fourteen row scripts, so each row was measured on an engine carrying the other thirteen rows'
/// globals — every one of them declares <c>function f</c>, so they collided outright, and the last
/// warm-up won — plus their handler-tree, call-site and environment-reuse state. A row's number then
/// depends on which siblings exist and on what a change did to <em>them</em>, which is exactly the
/// defect that made a call-path improvement read as a regression on
/// <see cref="MethodCallBenchmark"/>. The rows still measure warm dispatch, and engine construction
/// and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are
/// not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class LoopDispatchBenchmarks
{
    private IsolatedScript _emptyLoop;
    private IsolatedScript _variableBoundLoop;
    private IsolatedScript _counterAdd;
    private IsolatedScript _localCopy;
    private IsolatedScript _stringAppend;
    private IsolatedScript _comparisonOnly;
    private IsolatedScript _strictEqualTest;
    private IsolatedScript _looseEqualTest;
    private IsolatedScript _moduloEqualTest;
    private IsolatedScript _ifChainLoop;
    private IsolatedScript _varDeclBody;
    private IsolatedScript _xorAssignLoop;
    private IsolatedScript _arrayLengthBound;
    private IsolatedScript _stringLengthBound;

    [GlobalSetup]
    public void Setup()
    {
        // pure loop machinery: test + update + (empty) body dispatch
        _emptyLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { for (var i = 0; i < 100000; i++) { } return i; }
            f();
            """));

        // same machinery with a variable bound (i < n over two locals)
        _variableBoundLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var n = 100000; for (var i = 0; i < n; i++) { } return i; }
            f();
            """));

        // + one numeric compound assignment (unboxed discard lane)
        _counterAdd = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { n += 1; } return n; }
            f();
            """));

        // + one plain slot-to-slot assignment
        _localCopy = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var a = 1, b = 0; for (var i = 0; i < 100000; i++) { b = a; } return b; }
            f();
            """));

        // + one string compound assignment (rope append, slot lane) — the dromaeo-core-eval body shape
        _stringAppend = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var s = ''; for (var i = 0; i < 100000; i++) { s += 'a'; } return s.length; }
            f();
            """));

        // + one comparison whose result is discarded through an if with empty branches
        _comparisonOnly = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { if (i < 50000) { } } return n; }
            f();
            """));

        // + one strict-equality test (=== over a slot and a constant)
        _strictEqualTest = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { if (i === 50000) { } } return n; }
            f();
            """));

        // + one loose-equality test (== over a slot and a constant)
        _looseEqualTest = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { if (i == 50000) { } } return n; }
            f();
            """));

        // + the stopwatch.js if-chain shape: modulo of a slot vs a constant, loosely compared
        _moduloEqualTest = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { if (i % 2 == 0) { } } return n; }
            f();
            """));

        // the full stopwatch.js body shape: var-decl + 4-way modulo else-if chain whose branches
        // call tiny closures + two dead member-read var-decls
        _ifChainLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var c = { n: 0, a: function () { this.n++; }, b: function () { this.n--; } };
                for (var i = 0; i < 100000; i++) {
                    var z = i ^ 3;
                    if (z % 2 == 0) c.a();
                    else if (z % 3 == 0) c.b();
                    else if (z % 5 == 0) c.a();
                    else if (z % 7 == 0) c.b();
                    var v = c.n;
                }
                return c.n;
            }
            f();
            """));

        // + one var declaration with an initializer (the `var z = x ^ y` statement alone)
        _varDeclBody = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var n = 0; for (var i = 0; i < 100000; i++) { var z = i ^ 3; } return n; }
            f();
            """));

        // + identifier ^ identifier over two slot numbers (the stopwatch `var z = x ^ y` shape;
        // z stays inside the small-int cache so the measurement is operand handling, not boxing)
        _xorAssignLoop = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var m = 3; var n = 0; for (var i = 0; i < 100000; i++) { var z = i ^ m; } return n; }
            f();
            """));

        // the member-bound loop test: `i < a.length` re-reads the live length every iteration
        // (6250 × 16 = 100k inner iterations)
        _arrayLengthBound = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var a = []; for (var k = 0; k < 16; k++) a[k] = k; var n = 0; for (var r = 0; r < 6250; r++) { for (var i = 0; i < a.length; i++) { n++; } } return n; }
            f();
            """));

        // string variant: a 20k-char string's length exceeds the small-int cache, so the boxed
        // read allocates a JsNumber per iteration without the lane (5 × 20k = 100k iterations)
        _stringLengthBound = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() { var s = 'x'.repeat(20000); var n = 0; for (var r = 0; r < 5; r++) { for (var i = 0; i < s.length; i++) { n++; } } return n; }
            f();
            """));
    }

    [Benchmark(Baseline = true)]
    public JsValue EmptyLoop() => _emptyLoop.Run();

    [Benchmark]
    public JsValue VariableBoundLoop() => _variableBoundLoop.Run();

    [Benchmark]
    public JsValue CounterAdd() => _counterAdd.Run();

    [Benchmark]
    public JsValue LocalCopy() => _localCopy.Run();

    [Benchmark]
    public JsValue StringAppend() => _stringAppend.Run();

    [Benchmark]
    public JsValue ComparisonOnly() => _comparisonOnly.Run();

    [Benchmark]
    public JsValue StrictEqualTest() => _strictEqualTest.Run();

    [Benchmark]
    public JsValue LooseEqualTest() => _looseEqualTest.Run();

    [Benchmark]
    public JsValue ModuloEqualTest() => _moduloEqualTest.Run();

    [Benchmark]
    public JsValue IfChainLoop() => _ifChainLoop.Run();

    [Benchmark]
    public JsValue VarDeclBody() => _varDeclBody.Run();

    [Benchmark]
    public JsValue XorAssignLoop() => _xorAssignLoop.Run();

    [Benchmark]
    public JsValue ArrayLengthBound() => _arrayLengthBound.Run();

    [Benchmark]
    public JsValue StringLengthBound() => _stringLengthBound.Run();
}
