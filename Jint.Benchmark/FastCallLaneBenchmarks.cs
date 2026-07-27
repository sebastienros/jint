using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The two fast-call lane extensions, each with the control row that isolates it.
///
/// <para><b>Declared argument guards.</b> <c>String.prototype</c>'s <c>charCodeAt</c>, <c>charAt</c>,
/// <c>substring</c> and <c>slice</c> take their arguments as plain <c>JsValue</c> and coerce them in
/// the body. That used to block the frameless lane outright, because the generator cannot see the
/// coercion; a declared per-argument guard lets them take it for the values they are provably
/// leaf-safe for. The <c>*_Guarded</c> rows pass numbers and should elide the call-stack frame; the
/// <c>*_Object</c> rows pass an object whose <c>valueOf</c> is user code, fail the guard, and must
/// stay framed — they are the control, and a change there is environment drift, not a result.</para>
///
/// <para><b>Variadic lane.</b> A <c>[Rest]</c> built-in used to decline the lane entirely, because a
/// variadic tail does not fit two argument registers. It now takes it at the arities a call site can
/// state statically. <c>Math.max/min/hypot</c> additionally earn frame elision from their tail's
/// declared <c>ToNumber</c>; <c>Array.prototype.push</c> and <c>String.prototype.concat</c> take the
/// framed half only. The <c>*_Arity3</c> rows overflow the lane's two registers and decline, so they
/// are the control for the same built-in on the old path.</para>
///
/// <para><b>Reading the over-arity rows.</b> <c>MathMax_Arity3</c> is a pure control: its built-in
/// was already <c>[Rest]</c>, so nothing about its framed path changed. <c>ArrayPush_Arity3</c> and
/// <c>StringConcat_Arity3</c> are not — those two built-ins were migrated off the raw
/// <c>JsCallArguments</c> array onto a <c>[Rest]</c> tail to reach the lane at all, so their framed
/// path is newly reached through a span over the same pooled array. That span is constructed from
/// the array the caller already filled, with no copy and no second store, and these rows are what
/// proves it: an over-arity call must not pay for a lane it declines.</para>
///
/// <para><b>The object-argument rows are the accepted cost of the feature, not a defect.</b> A guard
/// has to be evaluated before the frameless lane can be declined, so a value that fails one pays for
/// the question and then takes the framed path anyway — a built-in that never claimed <c>Leaf</c>
/// never asked it. Reducing the guard to a single <c>InternalTypes</c> mask test took most of that
/// back but not all: measured against the pre-feature base, <c>CharCodeAt_Object</c> is about +5%
/// (the shortest such row, so the fixed cost is the largest share of it) while
/// <c>Substring_Object</c> is flat to better. Conforming arguments win roughly −5%, and the variadic
/// rows −12..−22%. If a future change makes an object argument materially worse than this, that is a
/// regression; the single-digit standing cost is the trade.</para>
///
/// Every row runs its call 1000 times inside one prepared script on an engine that has already
/// evaluated it, so what is measured is dispatch plus body rather than parse or first-call warmup —
/// and the lane is only reachable from a warm site in the first place.
/// </summary>
[MemoryDiagnoser]
public class FastCallLaneBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private Engine _engine = null!;

    private Prepared<Script> _charCodeAtGuarded;
    private Prepared<Script> _charCodeAtObject;
    private Prepared<Script> _charAtGuarded;
    private Prepared<Script> _substringGuarded;
    private Prepared<Script> _substringAbsentEnd;
    private Prepared<Script> _substringObject;
    private Prepared<Script> _sliceGuarded;

    private Prepared<Script> _max2;
    private Prepared<Script> _max1;
    private Prepared<Script> _maxArity3;
    private Prepared<Script> _min2;
    private Prepared<Script> _hypot2;
    private Prepared<Script> _push1;
    private Prepared<Script> _push2;
    private Prepared<Script> _pushArity3;
    private Prepared<Script> _concat1;
    private Prepared<Script> _concat2;
    private Prepared<Script> _concatArity3;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Declared argument guards: numeric arguments take the leaf branch, an object argument
        // cannot and is the control.
        _charCodeAtGuarded = Loop("s += \"abcdefghij\".charCodeAt(i % 10)");
        _charCodeAtObject = Loop("s += \"abcdefghij\".charCodeAt(o)", prelude: "var o = { valueOf: function () { return 3; } };");
        _charAtGuarded = Loop("s += \"abcdefghij\".charAt(i % 10).length");
        _substringGuarded = Loop("s += \"abcdefghij\".substring(i % 5, 8).length");
        // The single-argument form is the one a Number-only guard would have lost: the call site pads
        // the second register with undefined, which only a composed guard admits.
        _substringAbsentEnd = Loop("s += \"abcdefghij\".substring(i % 5).length");
        _substringObject = Loop("s += \"abcdefghij\".substring(o, 8).length", prelude: "var o = { valueOf: function () { return 2; } };");
        _sliceGuarded = Loop("s += \"abcdefghij\".slice(i % 5, 8).length");

        // Variadic lane. The arity-3 rows overflow the two registers and decline it.
        _max2 = Loop("s += Math.max(i, 500)");
        _max1 = Loop("s += Math.max(i)");
        _maxArity3 = Loop("s += Math.max(i, 500, 250)");
        _min2 = Loop("s += Math.min(i, 500)");
        _hypot2 = Loop("s += Math.hypot(i, 500)");
        _push1 = Loop("a.push(i); if (a.length > 64) a.length = 0", prelude: "var a = [];");
        _push2 = Loop("a.push(i, i); if (a.length > 64) a.length = 0", prelude: "var a = [];");
        _pushArity3 = Loop("a.push(i, i, i); if (a.length > 64) a.length = 0", prelude: "var a = [];");
        _concat1 = Loop("s += \"ab\".concat(\"cd\").length");
        _concat2 = Loop("s += \"ab\".concat(\"cd\", \"ef\").length");
        _concatArity3 = Loop("s += \"ab\".concat(\"cd\", \"ef\", \"gh\").length");

        _engine = new Engine();
        foreach (var script in AllScripts())
        {
            _engine.Evaluate(script);
        }
    }

    /// <summary>
    /// One call per iteration inside a warm loop. <c>s</c> accumulates so the call cannot be dropped,
    /// and the loop variable feeds the argument so the values are not constant-folded into the site.
    /// <para>
    /// Everything is declared with <c>var</c> on purpose: each row is evaluated many times on one
    /// engine, and a <c>const</c> would survive in the global lexical environment and make the second
    /// evaluation a redeclaration SyntaxError.
    /// </para>
    /// </summary>
    private static Prepared<Script> Loop(string body, string prelude = "")
        => Engine.PrepareScript($$"""
            {{prelude}}
            var s = 0;
            for (var i = 0; i < {{OperationsPerInvoke}}; i++) { {{body}}; }
            s;
            """);

    private IEnumerable<Prepared<Script>> AllScripts()
    {
        yield return _charCodeAtGuarded;
        yield return _charCodeAtObject;
        yield return _charAtGuarded;
        yield return _substringGuarded;
        yield return _substringAbsentEnd;
        yield return _substringObject;
        yield return _sliceGuarded;
        yield return _max2;
        yield return _max1;
        yield return _maxArity3;
        yield return _min2;
        yield return _hypot2;
        yield return _push1;
        yield return _push2;
        yield return _pushArity3;
        yield return _concat1;
        yield return _concat2;
        yield return _concatArity3;
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue CharCodeAt_Guarded() => _engine.Evaluate(_charCodeAtGuarded);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue CharCodeAt_Object() => _engine.Evaluate(_charCodeAtObject);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue CharAt_Guarded() => _engine.Evaluate(_charAtGuarded);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Substring_Guarded() => _engine.Evaluate(_substringGuarded);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Substring_AbsentEnd() => _engine.Evaluate(_substringAbsentEnd);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Substring_Object() => _engine.Evaluate(_substringObject);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Slice_Guarded() => _engine.Evaluate(_sliceGuarded);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMax_Arity1() => _engine.Evaluate(_max1);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMax_Arity2() => _engine.Evaluate(_max2);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMax_Arity3() => _engine.Evaluate(_maxArity3);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMin_Arity2() => _engine.Evaluate(_min2);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathHypot_Arity2() => _engine.Evaluate(_hypot2);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayPush_Arity1() => _engine.Evaluate(_push1);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayPush_Arity2() => _engine.Evaluate(_push2);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayPush_Arity3() => _engine.Evaluate(_pushArity3);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringConcat_Arity1() => _engine.Evaluate(_concat1);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringConcat_Arity2() => _engine.Evaluate(_concat2);
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringConcat_Arity3() => _engine.Evaluate(_concatArity3);
}
