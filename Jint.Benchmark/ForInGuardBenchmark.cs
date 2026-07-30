using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Object-iteration and type-guard idioms: for-in with and without the lint-mandated
/// hasOwnProperty guard, prototype-chain filtering, for-in over arrays (dense, holey, and with
/// extra named own props), for-of over a string, and typeof / instanceof / in dispatch over
/// LCG-mixed inputs the branch predictor cannot memorize.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs <see cref="SetupSource"/> so each engine owns its own fixture objects — and warmed with its
/// own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with
/// all eleven row scripts, so each row was measured on an engine carrying the other ten rows' globals
/// (every one of them declares <c>function f</c>, so they collided outright) plus their handler-tree,
/// call-site and enumeration-cache state — which makes a row's number depend on which siblings exist and
/// on what a change did to <em>them</em>. The rows still measure warm iteration, and engine construction
/// and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not
/// comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ForInGuardBenchmark
{
    private IsolatedScript _forInSmall;
    private IsolatedScript _forInWide;
    private IsolatedScript _forInHasOwnGuard;
    private IsolatedScript _forInProtoChain;
    private IsolatedScript _forInDenseArray;
    private IsolatedScript _forInHoleyArray;
    private IsolatedScript _forInArrayExtraProps;
    private IsolatedScript _forOfString;
    private IsolatedScript _typeofSwitchMixed;
    private IsolatedScript _instanceofMixed;
    private IsolatedScript _inOperatorMixed;

    internal const string SetupSource = """
        var six = { a: 1, b: 2, c: 3, d: 4, e: 5, f: 6 };
        var wide = {};
        var protoObj = Object.create({ pa: 1, pb: 2, pc: 3, pd: 4, pe: 5, pf: 6 });
        var denseArr = [];
        var holeyArr = [];
        var namedArr = [];
        var mixedVals = [];
        var instObjs = [];
        var inObjs = [];
        var order = [];
        var str20k;
        class Base { constructor() { this.tag = 1; } }
        class Deriv extends Base { constructor() { super(); this.sub = 2; } }
        (function () {
            var seed = 20260711;
            for (var i = 0; i < 64; i++) { wide['w' + i] = i; }
            for (var i = 0; i < 1000; i++) { denseArr[i] = i; }
            for (var i = 0; i < 1000; i += 4) { holeyArr[i] = i; }
            for (var i = 0; i < 100; i++) { namedArr[i] = i; }
            namedArr.tag = 'x';
            namedArr.other = 'y';
            protoObj.oa = 1; protoObj.ob = 2; protoObj.oc = 3; protoObj.od = 4; protoObj.oe = 5; protoObj.of = 6;
            for (var i = 0; i < 1024; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                var pick = (seed >>> 4) & 3;
                if (pick === 0) { mixedVals.push(seed & 255); }
                else if (pick === 1) { mixedVals.push('s' + (seed & 15)); }
                else if (pick === 2) { mixedVals.push(undefined); }
                else { mixedVals.push({ v: i }); }
                instObjs.push(((seed >>> 6) & 1) === 0 ? new Deriv() : { tag: 0 });
                inObjs.push(((seed >>> 7) & 1) === 0 ? { x: 1, y: 2 } : { y: 2 });
            }
            for (var i = 0; i < 8192; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                order.push((seed >>> 7) & 1023);
            }
            var chunk = 'abcdefghijklmnopqrst';
            var parts = [];
            for (var i = 0; i < 1000; i++) { parts.push(chunk); }
            str20k = parts.join('');
        })();
        """;

    internal const string ForInHasOwnGuardSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 20000; i++) {
                for (var k in six) {
                    if (six.hasOwnProperty(k)) { s++; }
                }
            }
            return s;
        }
        f();
        """;

    internal const string TypeofSwitchMixedSource = """
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                var v = mixedVals[order[i & 8191]];
                var t = typeof v;
                if (t === 'number') { s += 1; }
                else if (t === 'string') { s += 2; }
                else if (t === 'undefined') { s += 3; }
                else { s += 4; }
            }
            return s;
        }
        f();
        """;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _forInSmall = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 20000; i++) {
                    for (var k in six) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _forInWide = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 2000; i++) {
                    for (var k in wide) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _forInHasOwnGuard = IsolatedScript.Warm(Engine.PrepareScript(ForInHasOwnGuardSource), CreateEngine);

        // 1,000 dense int-indexed keys, no holes, no named own props
        _forInDenseArray = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 200; i++) {
                    for (var k in denseArr) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        // every 4th index present; enumeration must skip the holes
        _forInHoleyArray = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 200; i++) {
                    for (var k in holeyArr) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        // 100 indices plus two named own props; order must stay indices-then-named
        _forInArrayExtraProps = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 2000; i++) {
                    for (var k in namedArr) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        // 6 own + 6 enumerable inherited keys; the guard filters the inherited half
        _forInProtoChain = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    for (var k in protoObj) {
                        if (protoObj.hasOwnProperty(k)) { s++; }
                    }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _forOfString = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var i = 0; i < 5; i++) {
                    for (var ch of str20k) { n++; }
                }
                return n;
            }
            f();
            """), CreateEngine);

        _typeofSwitchMixed = IsolatedScript.Warm(Engine.PrepareScript(TypeofSwitchMixedSource), CreateEngine);

        _instanceofMixed = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if (instObjs[order[i & 8191]] instanceof Base) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);

        _inOperatorMixed = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if ('x' in inObjs[order[i & 8191]]) { s++; }
                }
                return s;
            }
            f();
            """), CreateEngine);
    }

    [Benchmark]
    public JsValue ForInSmall() => _forInSmall.Run();

    [Benchmark]
    public JsValue ForInWide() => _forInWide.Run();

    [Benchmark]
    public JsValue ForInHasOwnGuard() => _forInHasOwnGuard.Run();

    [Benchmark]
    public JsValue ForInProtoChain() => _forInProtoChain.Run();

    [Benchmark]
    public JsValue ForInDenseArray() => _forInDenseArray.Run();

    [Benchmark]
    public JsValue ForInHoleyArray() => _forInHoleyArray.Run();

    [Benchmark]
    public JsValue ForInArrayExtraProps() => _forInArrayExtraProps.Run();

    [Benchmark]
    public JsValue ForOfString() => _forOfString.Run();

    [Benchmark]
    public JsValue TypeofSwitchMixed() => _typeofSwitchMixed.Run();

    [Benchmark]
    public JsValue InstanceofMixed() => _instanceofMixed.Run();

    [Benchmark]
    public JsValue InOperatorMixed() => _inOperatorMixed.Run();
}
