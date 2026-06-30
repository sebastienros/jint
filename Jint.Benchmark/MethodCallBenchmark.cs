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
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class MethodCallBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _methodCallThis;
    private Prepared<Script> _methodCallCaptured;
    private Prepared<Script> _freeFunctionCall;

    [GlobalSetup]
    public void Setup()
    {
        // Values are masked to stay in the small-integer cache so JsNumber boxing does not
        // dominate/perturb the measurement — the signal is the call-dispatch + property
        // resolution cost, which the fast path removes (Reference rent + descriptor re-resolution).
        _methodCallThis = Engine.PrepareScript("""
            var o = { n: 0, tick: function () { this.n = (this.n + 1) & 1023; return this.n; } };
            var s = 0;
            for (var i = 0; i < 1000000; i++) { s = (s + o.tick()) & 1023; }
            s;
            """, strict: true);

        _methodCallCaptured = Engine.PrepareScript("""
            function makeCounter() {
                var n = 0;
                return { inc: function () { n = (n + 1) & 1023; return n; } };
            }
            var c = makeCounter();
            var s = 0;
            for (var i = 0; i < 1000000; i++) { s = (s + c.inc()) & 1023; }
            s;
            """, strict: true);

        _freeFunctionCall = Engine.PrepareScript("""
            function f(x) { return (x + 1) & 1023; }
            var s = 0;
            for (var i = 0; i < 1000000; i++) { s = f(s); }
            s;
            """, strict: true);

        _arrayPushPop = Engine.PrepareScript(
            "var a = []; for (var i = 0; i < 1000000; i++) { a.push(i); a.pop(); } a.length;", strict: true);
        _userProtoMethod = Engine.PrepareScript("""
            function C() { this.v = 0; }
            C.prototype.inc = function () { this.v = (this.v + 1) & 1023; return this.v; };
            var c = new C(); var s = 0;
            for (var i = 0; i < 1000000; i++) { s = (s + c.inc()) & 1023; }
            s;
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        _engine.Evaluate(_methodCallThis);
        _engine.Evaluate(_methodCallCaptured);
        _engine.Evaluate(_freeFunctionCall);
        _engine.Evaluate(_arrayPushPop);
        _engine.Evaluate(_userProtoMethod);
    }

    [Benchmark]
    public JsValue MethodCallThis() => _engine.Evaluate(_methodCallThis);

    [Benchmark]
    public JsValue MethodCallCaptured() => _engine.Evaluate(_methodCallCaptured);

    [Benchmark]
    public JsValue FreeFunctionCall() => _engine.Evaluate(_freeFunctionCall);

    // Prototype-method calls — resolved on the receiver's prototype, the case the prototype-method inline
    // cache targets (own-method calls above already hit the own-property cache). Prepared in Setup().
    private Prepared<Script> _arrayPushPop;
    private Prepared<Script> _userProtoMethod;

    [Benchmark]
    public JsValue ArrayPushPop() => _engine.Evaluate(_arrayPushPop);

    [Benchmark]
    public JsValue UserPrototypeMethod() => _engine.Evaluate(_userProtoMethod);
}
