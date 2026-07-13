using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Object-iteration and type-guard idioms: for-in with and without the lint-mandated
/// hasOwnProperty guard, prototype-chain filtering, for-in over arrays (dense, holey, and with
/// extra named own props), for-of over a string, and typeof / instanceof / in dispatch over
/// LCG-mixed inputs the branch predictor cannot memorize.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ForInGuardBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _forInSmall;
    private Prepared<Script> _forInWide;
    private Prepared<Script> _forInHasOwnGuard;
    private Prepared<Script> _forInProtoChain;
    private Prepared<Script> _forInDenseArray;
    private Prepared<Script> _forInHoleyArray;
    private Prepared<Script> _forInArrayExtraProps;
    private Prepared<Script> _forOfString;
    private Prepared<Script> _typeofSwitchMixed;
    private Prepared<Script> _instanceofMixed;
    private Prepared<Script> _inOperatorMixed;

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

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _forInSmall = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 20000; i++) {
                    for (var k in six) { s++; }
                }
                return s;
            }
            f();
            """);

        _forInWide = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 2000; i++) {
                    for (var k in wide) { s++; }
                }
                return s;
            }
            f();
            """);

        _forInHasOwnGuard = Engine.PrepareScript(ForInHasOwnGuardSource);

        // 1,000 dense int-indexed keys, no holes, no named own props
        _forInDenseArray = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 200; i++) {
                    for (var k in denseArr) { s++; }
                }
                return s;
            }
            f();
            """);

        // every 4th index present; enumeration must skip the holes
        _forInHoleyArray = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 200; i++) {
                    for (var k in holeyArr) { s++; }
                }
                return s;
            }
            f();
            """);

        // 100 indices plus two named own props; order must stay indices-then-named
        _forInArrayExtraProps = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 2000; i++) {
                    for (var k in namedArr) { s++; }
                }
                return s;
            }
            f();
            """);

        // 6 own + 6 enumerable inherited keys; the guard filters the inherited half
        _forInProtoChain = Engine.PrepareScript("""
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
            """);

        _forOfString = Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var i = 0; i < 5; i++) {
                    for (var ch of str20k) { n++; }
                }
                return n;
            }
            f();
            """);

        _typeofSwitchMixed = Engine.PrepareScript(TypeofSwitchMixedSource);

        _instanceofMixed = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if (instObjs[order[i & 8191]] instanceof Base) { s++; }
                }
                return s;
            }
            f();
            """);

        _inOperatorMixed = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if ('x' in inObjs[order[i & 8191]]) { s++; }
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_forInSmall);
        _engine.Evaluate(_forInWide);
        _engine.Evaluate(_forInHasOwnGuard);
        _engine.Evaluate(_forInProtoChain);
        _engine.Evaluate(_forInDenseArray);
        _engine.Evaluate(_forInHoleyArray);
        _engine.Evaluate(_forInArrayExtraProps);
        _engine.Evaluate(_forOfString);
        _engine.Evaluate(_typeofSwitchMixed);
        _engine.Evaluate(_instanceofMixed);
        _engine.Evaluate(_inOperatorMixed);
    }

    [Benchmark]
    public JsValue ForInSmall() => _engine.Evaluate(_forInSmall);

    [Benchmark]
    public JsValue ForInWide() => _engine.Evaluate(_forInWide);

    [Benchmark]
    public JsValue ForInHasOwnGuard() => _engine.Evaluate(_forInHasOwnGuard);

    [Benchmark]
    public JsValue ForInProtoChain() => _engine.Evaluate(_forInProtoChain);

    [Benchmark]
    public JsValue ForInDenseArray() => _engine.Evaluate(_forInDenseArray);

    [Benchmark]
    public JsValue ForInHoleyArray() => _engine.Evaluate(_forInHoleyArray);

    [Benchmark]
    public JsValue ForInArrayExtraProps() => _engine.Evaluate(_forInArrayExtraProps);

    [Benchmark]
    public JsValue ForOfString() => _engine.Evaluate(_forOfString);

    [Benchmark]
    public JsValue TypeofSwitchMixed() => _engine.Evaluate(_typeofSwitchMixed);

    [Benchmark]
    public JsValue InstanceofMixed() => _engine.Evaluate(_instanceofMixed);

    [Benchmark]
    public JsValue InOperatorMixed() => _engine.Evaluate(_inOperatorMixed);
}
