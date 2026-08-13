using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The three fast-call lane extensions, each with the control row that isolates it.
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
/// <para><b>Keyed-collection receiver guards.</b> <c>Map.prototype</c>'s <c>get</c>/<c>has</c>/
/// <c>set</c>/<c>delete</c> and <c>Set.prototype</c>'s <c>has</c>/<c>add</c>/<c>delete</c> already took
/// the register half of the lane; what is new is frame elision under a guard proving the brand their
/// body checks. The <c>Map*</c> and <c>Set*</c> rows below are the guarded happy paths.
/// <c>MapGet_ObjectKey</c> and <c>SetHas_ObjectValue</c> are not curiosities: the declared argument
/// guard is <c>AnyValue</c> — a key is hashed and compared and never converted, so no key can reach
/// user code — and object keys are exactly what that claim buys, where a String|Number guard would
/// have been equally safe and would have sent them back to the framed path.</para>
///
/// <para><b>Why those groups have no wrong-receiver row.</b> The rows above can pass an object where a
/// number is expected and watch the guard decline, because the built-in then coerces it and returns
/// normally. A keyed collection has no such value: its arguments are guarded by nothing at all, and
/// its only guard — the receiver's brand — is the exact condition its body raises a TypeError for. So
/// every call that fails it is a call that throws, and a loop of those would measure exception
/// construction rather than the lane. The declining path is pinned for behaviour instead, by
/// <c>AWarmedKeyedCollectionSiteFramesAWrongBrandReceiver</c> and
/// <c>AWarmedKeyedCollectionSiteFramesTheOtherCollection</c> in <c>FastCallLaneTests</c>. Their
/// control is <c>WeakMapHas_Framed</c>/<c>WeakSetHas_Framed</c>: same shape, same register lane,
/// deliberately never <c>Leaf</c> (their <c>set</c>/<c>add</c> siblings throw for a key that cannot be
/// held weakly, which no guard expresses), so a move there is environment drift, not a result.</para>
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
/// <para><b>Predicates and searches.</b> The third group covers the built-ins whose bodies are a type
/// test or a search rather than a coercion. <c>Number.isInteger</c> and its siblings inspect the
/// argument and never coerce it, so they are declared <c>FastCallGuard.AnyValue</c> and take the
/// frameless branch for every value — which is why <c>NumberIsInteger_Object</c> is the one
/// <c>*_Object</c> row in this class that is <em>not</em> a control: it is expected to improve with
/// its guarded sibling, and it is here to show that a body which only inspects its argument costs an
/// object nothing extra. Every other <c>*_Object</c> row is a control in the usual sense.
/// <c>IsNaN_String</c> is included because the global pair's guard admits strings, where the
/// String.prototype rows admit numbers, so it is the only row that exercises that arm.
/// <c>ArrayIsArray_Object</c> is a true control twice over: the guard names our own arrays, so a
/// plain object both fails it and is the answer-is-false case the frame still has to cover.</para>
///
/// Every row runs its call 1000 times inside one prepared script on an engine that has already
/// evaluated it, so what is measured is dispatch plus body rather than parse or first-call warmup —
/// and the lane is only reachable from a warm site in the first place.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>) — doubly load-bearing for the keyed-collection rows, which
/// share global names and several of which mutate the collection they read. The original 18 rows used
/// to be warmed on one engine by a single
/// <c>foreach</c> loop, so each was measured on an engine carrying the other 17 rows' globals — they
/// collide outright on <c>s</c> and <c>i</c>, and the three <c>push</c> rows all declare <c>a</c> and the
/// two object-argument rows both declare <c>o</c>, so a row inherited whichever sibling wrote last — plus
/// 17 other rows' handler-tree entries and call-site caches. Since the whole point of the class is which
/// call sites reach the fast-call lane, sibling state on those very sites is exactly the wrong thing to
/// carry. The rows still measure warm dispatch, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class FastCallLaneBenchmarks
{
    private const int OperationsPerInvoke = 1_000;

    private IsolatedScript _charCodeAtGuarded;
    private IsolatedScript _charCodeAtObject;
    private IsolatedScript _charAtGuarded;
    private IsolatedScript _substringGuarded;
    private IsolatedScript _substringAbsentEnd;
    private IsolatedScript _substringObject;
    private IsolatedScript _sliceGuarded;

    private IsolatedScript _max2;
    private IsolatedScript _max1;
    private IsolatedScript _maxArity3;
    private IsolatedScript _min2;
    private IsolatedScript _hypot2;
    private IsolatedScript _push1;
    private IsolatedScript _push2;
    private IsolatedScript _pushArity3;
    private IsolatedScript _concat1;
    private IsolatedScript _concat2;
    private IsolatedScript _concatArity3;

    private IsolatedScript _mapGetString;
    private IsolatedScript _mapGetObject;
    private IsolatedScript _mapGetMiss;
    private IsolatedScript _mapHasString;
    private IsolatedScript _mapSetString;
    private IsolatedScript _mapDeleteChurn;
    private IsolatedScript _setHasNumber;
    private IsolatedScript _setHasObject;
    private IsolatedScript _setAddChurn;
    private IsolatedScript _weakMapHas;
    private IsolatedScript _weakSetHas;

    /// <summary>
    /// The keyed-collection rows' fixture: 1024 string keys, 1024 object keys and the six collections
    /// holding them. Sized to the loop so a row indexes with <c>i &amp; 1023</c> and never constructs a
    /// key inside the measured loop.
    /// <para>
    /// Executed on the row's engine by <see cref="CollectionEngine"/> rather than prepended to the
    /// row's script, because a row's script is what <c>Run()</c> measures — building six 1024-element
    /// collections there would swamp the 1000 calls the row exists to time. The <c>prelude</c> the
    /// other rows use stays inside the measured script on purpose: theirs is one object literal.
    /// </para>
    /// </summary>
    private const string CollectionFixture = """
        var strKeys = [];
        var objKeys = [];
        var absentKeys = [];
        var m = new Map();
        var om = new Map();
        var numSet = new Set();
        var objSet = new Set();
        var wm = new WeakMap();
        var ws = new WeakSet();
        for (var n = 0; n < 1024; n++) {
            strKeys.push("k" + n);
            objKeys.push({ id: n });
            absentKeys.push("miss" + n);
            m.set(strKeys[n], n);
            om.set(objKeys[n], n);
            numSet.add(n);
            objSet.add(objKeys[n]);
            wm.set(objKeys[n], n);
            ws.add(objKeys[n]);
        }
        """;

    private IsolatedScript _numberIsIntegerNumber;
    private IsolatedScript _numberIsIntegerObject;
    private IsolatedScript _indexOfGuarded;
    private IsolatedScript _indexOfObject;
    private IsolatedScript _startsWithGuarded;
    private IsolatedScript _includesGuarded;
    private IsolatedScript _includesObject;
    private IsolatedScript _atGuarded;
    private IsolatedScript _substrGuarded;
    private IsolatedScript _atObject;
    private IsolatedScript _isNaNNumber;
    private IsolatedScript _isNaNString;
    private IsolatedScript _isNaNObject;
    private IsolatedScript _isArrayArray;
    private IsolatedScript _isArrayObject;

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

        // Keyed-collection receiver guards. The two mutating rows rebuild their own window collection
        // per evaluation — one empty-collection allocation against 1000 calls — so a row that is
        // evaluated hundreds of times does not measure an ever-growing table.
        _mapGetString = CollectionLoop("s += m.get(strKeys[i & 1023])");
        _mapGetObject = CollectionLoop("s += om.get(objKeys[i & 1023])");
        _mapGetMiss = CollectionLoop("if (m.get(absentKeys[i & 1023]) !== undefined) { s++; }");
        _mapHasString = CollectionLoop("if (m.has(strKeys[i & 1023])) { s++; }");
        _mapSetString = CollectionLoop("w.set(strKeys[i & 1023], i)", prelude: "var w = new Map();");
        _mapDeleteChurn = CollectionLoop("w.set(i & 1023, i); w.delete((i - 512) & 1023)", prelude: "var w = new Map();");
        _setHasNumber = CollectionLoop("if (numSet.has(i & 1023)) { s++; }");
        _setHasObject = CollectionLoop("if (objSet.has(objKeys[i & 1023])) { s++; }");
        _setAddChurn = CollectionLoop("w.add(i & 1023); w.delete((i - 512) & 1023)", prelude: "var w = new Set();");

        // Controls: same shape and same register lane, frame never elided, untouched by this change.
        _weakMapHas = CollectionLoop("if (wm.has(objKeys[i & 1023])) { s++; }");
        _weakSetHas = CollectionLoop("if (ws.has(objKeys[i & 1023])) { s++; }");

        // Predicates and searches. Number's four predicates share one shape, so isInteger stands for
        // the group; its object row takes the lane too, unlike every other *_Object row here.
        _numberIsIntegerNumber = Loop("if (Number.isInteger(i)) s++");
        _numberIsIntegerObject = Loop("if (Number.isInteger(o)) s++", prelude: "var o = { valueOf: function () { return 3; } };");
        _indexOfGuarded = Loop("s += \"abcdefghij\".indexOf(\"gh\", i % 5)");
        _indexOfObject = Loop("s += \"abcdefghij\".indexOf(o)", prelude: "var o = { toString: function () { return \"gh\"; } };");
        // startsWith stands beside includes because it reads the position register unconditionally
        // where includes tests it for undefined first.
        _startsWithGuarded = Loop("if (\"abcdefghij\".startsWith(\"cd\", i % 5)) s++");
        _includesGuarded = Loop("if (\"abcdefghij\".includes(\"gh\", i % 5)) s++");
        _includesObject = Loop("if (\"abcdefghij\".includes(o)) s++", prelude: "var o = { toString: function () { return \"gh\"; } };");
        _atGuarded = Loop("s += \"abcdefghij\".at(i % 10).length");
        _substrGuarded = Loop("s += \"abcdefghij\".substr(i % 5, 3).length");
        _atObject = Loop("s += \"abcdefghij\".at(o).length", prelude: "var o = { valueOf: function () { return 3; } };");
        _isNaNNumber = Loop("if (isNaN(i)) s++");
        _isNaNString = Loop("if (isNaN(\"12\")) s++");
        _isNaNObject = Loop("if (isNaN(o)) s++", prelude: "var o = { valueOf: function () { return 3; } };");
        _isArrayArray = Loop("if (Array.isArray(a)) s++", prelude: "var a = [1, 2, 3];");
        _isArrayObject = Loop("if (Array.isArray(o)) s++", prelude: "var o = { length: 3 };");
    }

    /// <summary>
    /// One call per iteration inside a warm loop, on the row's own engine. <c>s</c> accumulates so the
    /// call cannot be dropped, and the loop variable feeds the argument so the values are not
    /// constant-folded into the site.
    /// <para>
    /// Everything is declared with <c>var</c> on purpose: each row is evaluated many times on one
    /// engine, and a <c>const</c> would survive in the global lexical environment and make the second
    /// evaluation a redeclaration SyntaxError.
    /// </para>
    /// </summary>
    private static IsolatedScript Loop(string body, string prelude = "")
        => IsolatedScript.Warm(LoopScript(body, prelude));

    /// <summary>
    /// <see cref="Loop"/> for a row that needs <see cref="CollectionFixture"/>: the fixture is executed
    /// on the row's own fresh engine before the script is warmed, so it is set up once and stays out of
    /// what <c>Run()</c> times.
    /// </summary>
    private static IsolatedScript CollectionLoop(string body, string prelude = "")
        => IsolatedScript.Warm(LoopScript(body, prelude), static () =>
        {
            var engine = new Engine();
            engine.Execute(CollectionFixture);
            return engine;
        });

    private static Prepared<Script> LoopScript(string body, string prelude)
        => Engine.PrepareScript($$"""
            {{prelude}}
            var s = 0;
            for (var i = 0; i < {{OperationsPerInvoke}}; i++) { {{body}}; }
            s;
            """);

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue CharCodeAt_Guarded() => _charCodeAtGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue CharCodeAt_Object() => _charCodeAtObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue CharAt_Guarded() => _charAtGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Substring_Guarded() => _substringGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Substring_AbsentEnd() => _substringAbsentEnd.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Substring_Object() => _substringObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue Slice_Guarded() => _sliceGuarded.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMax_Arity1() => _max1.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMax_Arity2() => _max2.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMax_Arity3() => _maxArity3.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathMin_Arity2() => _min2.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MathHypot_Arity2() => _hypot2.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayPush_Arity1() => _push1.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayPush_Arity2() => _push2.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayPush_Arity3() => _pushArity3.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringConcat_Arity1() => _concat1.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringConcat_Arity2() => _concat2.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringConcat_Arity3() => _concatArity3.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MapGet_StringKey() => _mapGetString.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MapGet_ObjectKey() => _mapGetObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MapGet_Miss() => _mapGetMiss.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MapHas_StringKey() => _mapHasString.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MapSet_StringKey() => _mapSetString.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue MapDelete_Churn() => _mapDeleteChurn.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue SetHas_NumberValue() => _setHasNumber.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue SetHas_ObjectValue() => _setHasObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue SetAdd_Churn() => _setAddChurn.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue WeakMapHas_Framed() => _weakMapHas.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue WeakSetHas_Framed() => _weakSetHas.Run();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue NumberIsInteger_Number() => _numberIsIntegerNumber.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue NumberIsInteger_Object() => _numberIsIntegerObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringIndexOf_Guarded() => _indexOfGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringIndexOf_Object() => _indexOfObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringStartsWith_Guarded() => _startsWithGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringIncludes_Guarded() => _includesGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringIncludes_Object() => _includesObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringAt_Guarded() => _atGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringSubstr_Guarded() => _substrGuarded.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue StringAt_Object() => _atObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue IsNaN_Number() => _isNaNNumber.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue IsNaN_String() => _isNaNString.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue IsNaN_Object() => _isNaNObject.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayIsArray_Array() => _isArrayArray.Run();
    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)] public JsValue ArrayIsArray_Object() => _isArrayObject.Run();
}
