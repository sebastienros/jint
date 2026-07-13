using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Proxy trap dispatch: get/set/has/ownKeys through an explicit handler versus an empty
/// (forwarding) handler, apply/construct on a function proxy, revocable creation + revoke
/// churn, and typeof on a revoked function proxy (which must not throw). Rows measure the
/// per-operation trap machinery — handler lookup, trap invocation and invariant checks.
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
}
