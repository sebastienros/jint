#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;

namespace Jint.Benchmark;

/// <summary>
/// Proxy trap dispatch: get/set/has/ownKeys through an explicit handler versus an empty
/// (forwarding) handler, apply/construct on a function proxy, revocable creation + revoke
/// churn, and typeof on a revoked function proxy (which must not throw). Rows measure the
/// per-operation trap machinery — handler lookup, trap invocation and invariant checks.
/// The Clr* rows mirror the JS-handler rows 1:1 using the .NET <see cref="ProxyHandler"/>
/// trap API (Engine.Advanced.CreateProxy) over the same target objects.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ProxyBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _trapGet;
    private Prepared<Script> _trapSet;
    private Prepared<Script> _trapHas;
    private Prepared<Script> _forwardGet;
    private Prepared<Script> _forwardSet;
    private Prepared<Script> _ownKeysTrap;
    private Prepared<Script> _ownKeysForward;
    private Prepared<Script> _applyTrap;
    private Prepared<Script> _applyForward;
    private Prepared<Script> _constructTrap;
    private Prepared<Script> _revocableCreate;
    private Prepared<Script> _revokedTypeof;
    private Prepared<Script> _clrTrapGet;
    private Prepared<Script> _clrTrapSet;
    private Prepared<Script> _clrTrapHas;
    private Prepared<Script> _clrForwardGet;
    private Prepared<Script> _clrApplyTrap;
    private Prepared<Script> _clrConstructTrap;

    private const string SetupSource = """
        var target = { x: 1, a: 2, b: 3, c: 4, d: 5, e: 6, f: 7, g: 8, h: 9, k: 10 };
        var pTrap = new Proxy(target, {
            get: (t, k) => t[k],
            set: (t, k, v) => (t[k] = v, true),
            has: (t, k) => k in t,
            ownKeys: (t) => Reflect.ownKeys(t)
        });
        var pForward = new Proxy(target, {});
        function fnTarget(a, b) { return a + b; }
        var fTrap = new Proxy(fnTarget, {
            apply: (t, self, args) => t(args[0], args[1]),
            construct: (t, args) => ({ v: args[0] })
        });
        var fForward = new Proxy(fnTarget, {});
        var revokedFn = Proxy.revocable(function () {}, {});
        revokedFn.revoke();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        // CLR-handler proxies over the same targets as the JS-handler lanes
        var target = (ObjectInstance) _engine.GetValue("target");
        var fnTarget = (ObjectInstance) _engine.GetValue("fnTarget");
        _engine.SetValue("pClrTrap", _engine.Advanced.CreateProxy(target, new TrappingClrHandler()));
        _engine.SetValue("pClrForward", _engine.Advanced.CreateProxy(target, new ForwardingClrHandler()));
        _engine.SetValue("fClrTrap", _engine.Advanced.CreateProxy(fnTarget, new ApplyClrHandler()));

        _trapGet = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pTrap.x;
                }
                return s;
            }
            f();
            """);

        _trapSet = Engine.PrepareScript("""
            function f() {
                for (var i = 0; i < 100000; i++) {
                    pTrap.x = i;
                }
                return pTrap.x;
            }
            f();
            """);

        _trapHas = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if ('x' in pTrap) { s++; }
                }
                return s;
            }
            f();
            """);

        _forwardGet = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pForward.x;
                }
                return s;
            }
            f();
            """);

        _forwardSet = Engine.PrepareScript("""
            function f() {
                for (var i = 0; i < 100000; i++) {
                    pForward.x = i;
                }
                return pForward.x;
            }
            f();
            """);

        _ownKeysTrap = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += Object.keys(pTrap).length;
                }
                return s;
            }
            f();
            """);

        _ownKeysForward = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += Object.keys(pForward).length;
                }
                return s;
            }
            f();
            """);

        _applyTrap = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += fTrap(i, 1);
                }
                return s;
            }
            f();
            """);

        _applyForward = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += fForward(i, 1);
                }
                return s;
            }
            f();
            """);

        // two arguments on purpose: a single numeric ctor argument currently trips Array(n)
        // length semantics when JsProxy builds the trap's argumentsList (see JsProxy.Construct)
        _constructTrap = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += new fTrap(i, 1).v;
                }
                return s;
            }
            f();
            """);

        _revocableCreate = Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var i = 0; i < 10000; i++) {
                    var r = Proxy.revocable({}, {});
                    r.revoke();
                    n++;
                }
                return n;
            }
            f();
            """);

        // typeof does not consult the handler; it must stay 'function' and not throw after revoke
        _revokedTypeof = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if (typeof revokedFn.proxy === 'function') { s++; }
                }
                return s;
            }
            f();
            """);

        _clrTrapGet = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pClrTrap.x;
                }
                return s;
            }
            f();
            """);

        _clrTrapSet = Engine.PrepareScript("""
            function f() {
                for (var i = 0; i < 100000; i++) {
                    pClrTrap.x = i;
                }
                return pClrTrap.x;
            }
            f();
            """);

        _clrTrapHas = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if ('x' in pClrTrap) { s++; }
                }
                return s;
            }
            f();
            """);

        _clrForwardGet = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pClrForward.x;
                }
                return s;
            }
            f();
            """);

        _clrApplyTrap = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += fClrTrap(i, 1);
                }
                return s;
            }
            f();
            """);

        _clrConstructTrap = Engine.PrepareScript("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += new fClrTrap(i, 1).v;
                }
                return s;
            }
            f();
            """);

        _engine.Evaluate(_trapGet);
        _engine.Evaluate(_trapSet);
        _engine.Evaluate(_trapHas);
        _engine.Evaluate(_forwardGet);
        _engine.Evaluate(_forwardSet);
        _engine.Evaluate(_ownKeysTrap);
        _engine.Evaluate(_ownKeysForward);
        _engine.Evaluate(_applyTrap);
        _engine.Evaluate(_applyForward);
        _engine.Evaluate(_constructTrap);
        _engine.Evaluate(_revocableCreate);
        _engine.Evaluate(_revokedTypeof);

        // warm the CLR lanes and sanity-check the trap wiring; ClrTrapSet runs first so
        // target.x is a known 99999 before the get lanes read it
        AssertResult(_engine.Evaluate(_clrTrapSet), 99_999, nameof(ClrTrapSet));
        AssertResult(_engine.Evaluate(_clrTrapGet), 9_999_900_000, nameof(ClrTrapGet));
        AssertResult(_engine.Evaluate(_clrTrapHas), 100_000, nameof(ClrTrapHas));
        AssertResult(_engine.Evaluate(_clrForwardGet), 9_999_900_000, nameof(ClrForwardGet));
        AssertResult(_engine.Evaluate(_clrApplyTrap), 5_000_050_000, nameof(ClrApplyTrap));
        AssertResult(_engine.Evaluate(_clrConstructTrap), 49_995_000, nameof(ClrConstructTrap));
    }

    private static void AssertResult(JsValue actual, double expected, string lane)
    {
        if (!actual.IsNumber() || actual.AsNumber() != expected)
        {
            throw new InvalidOperationException($"{lane} returned {actual}, expected {expected}");
        }
    }

    [Benchmark]
    public JsValue TrapGet() => _engine.Evaluate(_trapGet);

    [Benchmark]
    public JsValue TrapSet() => _engine.Evaluate(_trapSet);

    [Benchmark]
    public JsValue TrapHas() => _engine.Evaluate(_trapHas);

    [Benchmark]
    public JsValue ForwardGet() => _engine.Evaluate(_forwardGet);

    [Benchmark]
    public JsValue ForwardSet() => _engine.Evaluate(_forwardSet);

    [Benchmark]
    public JsValue OwnKeysTrap() => _engine.Evaluate(_ownKeysTrap);

    [Benchmark]
    public JsValue OwnKeysForward() => _engine.Evaluate(_ownKeysForward);

    [Benchmark]
    public JsValue ApplyTrap() => _engine.Evaluate(_applyTrap);

    [Benchmark]
    public JsValue ApplyForward() => _engine.Evaluate(_applyForward);

    [Benchmark]
    public JsValue ConstructTrap() => _engine.Evaluate(_constructTrap);

    [Benchmark]
    public JsValue RevocableCreate() => _engine.Evaluate(_revocableCreate);

    [Benchmark]
    public JsValue RevokedTypeof() => _engine.Evaluate(_revokedTypeof);

    [Benchmark]
    public JsValue ClrTrapGet() => _engine.Evaluate(_clrTrapGet);

    [Benchmark]
    public JsValue ClrTrapSet() => _engine.Evaluate(_clrTrapSet);

    [Benchmark]
    public JsValue ClrTrapHas() => _engine.Evaluate(_clrTrapHas);

    [Benchmark]
    public JsValue ClrForwardGet() => _engine.Evaluate(_clrForwardGet);

    [Benchmark]
    public JsValue ClrApplyTrap() => _engine.Evaluate(_clrApplyTrap);

    [Benchmark]
    public JsValue ClrConstructTrap() => _engine.Evaluate(_clrConstructTrap);

    /// <summary>
    /// CLR equivalent of the JS handler <c>{ get: (t, k) => t[k], set: (t, k, v) => (t[k] = v, true), has: (t, k) => k in t }</c>.
    /// </summary>
    private sealed class TrappingClrHandler : ProxyHandler
    {
        public override JsValue? Get(ObjectInstance target, JsValue property, JsValue receiver) => target.Get(property, receiver);

        public override bool? Set(ObjectInstance target, JsValue property, JsValue value, JsValue receiver)
        {
            target.Set(property, value);
            return true;
        }

        public override bool? Has(ObjectInstance target, JsValue property) => target.HasProperty(property);
    }

    /// <summary>
    /// CLR equivalent of the empty JS handler <c>{}</c>: every trap forwards to the target.
    /// </summary>
    private sealed class ForwardingClrHandler : ProxyHandler
    {
    }

    /// <summary>
    /// CLR equivalent of the JS handler <c>{ apply: (t, self, args) => t(args[0], args[1]), construct: (t, args) => ({ v: args[0] }) }</c>.
    /// </summary>
    private sealed class ApplyClrHandler : ProxyHandler
    {
        public override JsValue? Apply(ObjectInstance target, JsValue thisObject, JsValue[] arguments) => target.Engine.Call(target, thisObject, arguments);

        public override ObjectInstance? Construct(ObjectInstance target, JsValue[] arguments, JsValue newTarget)
        {
            var result = new JsObject(target.Engine);
            result.Set("v", arguments[0]);
            return result;
        }
    }
}
