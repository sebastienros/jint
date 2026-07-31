using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates object method-call dispatch — the stopwatch.js shape where <c>sw.Start()</c>,
/// <c>sw.Stop()</c> etc. are invoked hundreds of thousands of times. Each call resolves a
/// literal-named property on an object and invokes the resulting closure. <see cref="MethodCallThis"/>
/// reads/writes <c>this</c> state; <see cref="MethodCallCaptured"/> reads/writes closure-captured
/// state (the exact Stopwatch shape). <see cref="FreeFunctionCall"/> is the guard: plain
/// (non-member) calls must not regress.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). This class is where the defect that motivated
/// that was caught: it used to warm one shared engine with all five scripts, which put five sets of
/// colliding globals (<c>s</c> and <c>i</c> are declared by every one of them) and five rows' worth
/// of handler-tree and call-site caches on the engine each row was then measured on. A call-path
/// change that a faithful single-workload reproduction showed to be 2.5-3.0% <em>faster</em> was
/// reported here as <c>UserPrototypeMethod</c> +9.2%, <c>ArrayPushPop</c> +5.6% and
/// <c>MethodCallThis</c> +5.8% — reproducibly, in both A/B orderings. The rows still measure warm
/// dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class MethodCallBenchmark
{
    private IsolatedScript _methodCallThis;
    private IsolatedScript _methodCallCaptured;
    private IsolatedScript _freeFunctionCall;

    private static Engine CreateEngine() => new(static options => options.Strict());

    [GlobalSetup]
    public void Setup()
    {
        // Values are masked to stay in the small-integer cache so JsNumber boxing does not
        // dominate/perturb the measurement — the signal is the call-dispatch + property
        // resolution cost, which the fast path removes (Reference rent + descriptor re-resolution).
        _methodCallThis = IsolatedScript.Warm(Engine.PrepareScript("""
            var o = { n: 0, tick: function () { this.n = (this.n + 1) & 1023; return this.n; } };
            var s = 0;
            for (var i = 0; i < 1000000; i++) { s = (s + o.tick()) & 1023; }
            s;
            """, strict: true), CreateEngine);

        _methodCallCaptured = IsolatedScript.Warm(Engine.PrepareScript("""
            function makeCounter() {
                var n = 0;
                return { inc: function () { n = (n + 1) & 1023; return n; } };
            }
            var c = makeCounter();
            var s = 0;
            for (var i = 0; i < 1000000; i++) { s = (s + c.inc()) & 1023; }
            s;
            """, strict: true), CreateEngine);

        _freeFunctionCall = IsolatedScript.Warm(Engine.PrepareScript("""
            function f(x) { return (x + 1) & 1023; }
            var s = 0;
            for (var i = 0; i < 1000000; i++) { s = f(s); }
            s;
            """, strict: true), CreateEngine);

        _arrayPushPop = IsolatedScript.Warm(Engine.PrepareScript(
            "var a = []; for (var i = 0; i < 1000000; i++) { a.push(i); a.pop(); } a.length;", strict: true), CreateEngine);
        _userProtoMethod = IsolatedScript.Warm(Engine.PrepareScript("""
            function C() { this.v = 0; }
            C.prototype.inc = function () { this.v = (this.v + 1) & 1023; return this.v; };
            var c = new C(); var s = 0;
            for (var i = 0; i < 1000000; i++) { s = (s + c.inc()) & 1023; }
            s;
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue MethodCallThis() => _methodCallThis.Run();

    [Benchmark]
    public JsValue MethodCallCaptured() => _methodCallCaptured.Run();

    [Benchmark]
    public JsValue FreeFunctionCall() => _freeFunctionCall.Run();

    // Prototype-method calls — resolved on the receiver's prototype, the case the prototype-method inline
    // cache targets (own-method calls above already hit the own-property cache). Prepared in Setup().
    private IsolatedScript _arrayPushPop;
    private IsolatedScript _userProtoMethod;

    [Benchmark]
    public JsValue ArrayPushPop() => _arrayPushPop.Run();

    [Benchmark]
    public JsValue UserPrototypeMethod() => _userProtoMethod.Run();
}
