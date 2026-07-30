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
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, built by <c>CreateEngine</c> (which
/// installs the shared <see cref="SetupSource"/> fixture and the three CLR-handler proxies every row
/// needs) and warmed with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to
/// be one shared engine warmed with all eighteen scripts, so each row was measured on an engine carrying
/// seventeen siblings' globals (every script declares <c>f</c>, <c>s</c> and <c>i</c>) plus their
/// handler-tree and call-site state. It also coupled the rows through the fixture: the set lanes drive
/// <c>target.x</c> to 99999, which every get lane then read. Isolation gives each row a pristine
/// <c>target.x === 1</c>, which is why the ClrTrapGet/ClrForwardGet sanity expectations below are now
/// 100000 rather than 9999900000 — the same 100000 reads, of 1 instead of 99999. The rows still measure
/// warm dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement. <b>Numbers from this class are not comparable to any published before the harness
/// changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ProxyBenchmark
{
    private IsolatedScript _trapGet;
    private IsolatedScript _trapSet;
    private IsolatedScript _trapHas;
    private IsolatedScript _forwardGet;
    private IsolatedScript _forwardSet;
    private IsolatedScript _ownKeysTrap;
    private IsolatedScript _ownKeysForward;
    private IsolatedScript _applyTrap;
    private IsolatedScript _applyForward;
    private IsolatedScript _constructTrap;
    private IsolatedScript _revocableCreate;
    private IsolatedScript _revokedTypeof;
    private IsolatedScript _clrTrapGet;
    private IsolatedScript _clrTrapSet;
    private IsolatedScript _clrTrapHas;
    private IsolatedScript _clrForwardGet;
    private IsolatedScript _clrApplyTrap;
    private IsolatedScript _clrConstructTrap;

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

    /// <summary>
    /// Builds a fresh engine carrying the shared <see cref="SetupSource"/> fixture and the CLR-handler
    /// proxies every row needs.
    /// </summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);

        // CLR-handler proxies over the same targets as the JS-handler lanes
        var target = (ObjectInstance) engine.GetValue("target");
        var fnTarget = (ObjectInstance) engine.GetValue("fnTarget");
        engine.SetValue("pClrTrap", engine.Advanced.CreateProxy(target, new TrappingClrHandler()));
        engine.SetValue("pClrForward", engine.Advanced.CreateProxy(target, new ForwardingClrHandler()));
        engine.SetValue("fClrTrap", engine.Advanced.CreateProxy(fnTarget, new ApplyClrHandler()));
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _trapGet = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pTrap.x;
                }
                return s;
            }
            f();
            """, CreateEngine);

        _trapSet = IsolatedScript.Warm("""
            function f() {
                for (var i = 0; i < 100000; i++) {
                    pTrap.x = i;
                }
                return pTrap.x;
            }
            f();
            """, CreateEngine);

        _trapHas = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if ('x' in pTrap) { s++; }
                }
                return s;
            }
            f();
            """, CreateEngine);

        _forwardGet = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pForward.x;
                }
                return s;
            }
            f();
            """, CreateEngine);

        _forwardSet = IsolatedScript.Warm("""
            function f() {
                for (var i = 0; i < 100000; i++) {
                    pForward.x = i;
                }
                return pForward.x;
            }
            f();
            """, CreateEngine);

        _ownKeysTrap = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += Object.keys(pTrap).length;
                }
                return s;
            }
            f();
            """, CreateEngine);

        _ownKeysForward = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += Object.keys(pForward).length;
                }
                return s;
            }
            f();
            """, CreateEngine);

        _applyTrap = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += fTrap(i, 1);
                }
                return s;
            }
            f();
            """, CreateEngine);

        _applyForward = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += fForward(i, 1);
                }
                return s;
            }
            f();
            """, CreateEngine);

        // two arguments on purpose: a single numeric ctor argument currently trips Array(n)
        // length semantics when JsProxy builds the trap's argumentsList (see JsProxy.Construct)
        _constructTrap = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += new fTrap(i, 1).v;
                }
                return s;
            }
            f();
            """, CreateEngine);

        _revocableCreate = IsolatedScript.Warm("""
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
            """, CreateEngine);

        // typeof does not consult the handler; it must stay 'function' and not throw after revoke
        _revokedTypeof = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if (typeof revokedFn.proxy === 'function') { s++; }
                }
                return s;
            }
            f();
            """, CreateEngine);

        _clrTrapGet = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pClrTrap.x;
                }
                return s;
            }
            f();
            """, CreateEngine);

        _clrTrapSet = IsolatedScript.Warm("""
            function f() {
                for (var i = 0; i < 100000; i++) {
                    pClrTrap.x = i;
                }
                return pClrTrap.x;
            }
            f();
            """, CreateEngine);

        _clrTrapHas = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    if ('x' in pClrTrap) { s++; }
                }
                return s;
            }
            f();
            """, CreateEngine);

        _clrForwardGet = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += pClrForward.x;
                }
                return s;
            }
            f();
            """, CreateEngine);

        _clrApplyTrap = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 100000; i++) {
                    s += fClrTrap(i, 1);
                }
                return s;
            }
            f();
            """, CreateEngine);

        _clrConstructTrap = IsolatedScript.Warm("""
            function f() {
                var s = 0;
                for (var i = 0; i < 10000; i++) {
                    s += new fClrTrap(i, 1).v;
                }
                return s;
            }
            f();
            """, CreateEngine);

        // Sanity-check the CLR trap wiring. Each lane re-runs its own script on its own already-warmed
        // engine, so no lane's fixture state can reach another's — which is why the get lanes now expect
        // 100000 (100000 reads of the pristine target.x === 1) instead of the 9999900000 they returned
        // when ClrTrapSet had first driven the shared target.x to 99999.
        AssertResult(_clrTrapSet.Run(), 99_999, nameof(ClrTrapSet));
        AssertResult(_clrTrapGet.Run(), 100_000, nameof(ClrTrapGet));
        AssertResult(_clrTrapHas.Run(), 100_000, nameof(ClrTrapHas));
        AssertResult(_clrForwardGet.Run(), 100_000, nameof(ClrForwardGet));
        AssertResult(_clrApplyTrap.Run(), 5_000_050_000, nameof(ClrApplyTrap));
        AssertResult(_clrConstructTrap.Run(), 49_995_000, nameof(ClrConstructTrap));
    }

    private static void AssertResult(JsValue actual, double expected, string lane)
    {
        if (!actual.IsNumber() || actual.AsNumber() != expected)
        {
            throw new InvalidOperationException($"{lane} returned {actual}, expected {expected}");
        }
    }

    [Benchmark]
    public JsValue TrapGet() => _trapGet.Run();

    [Benchmark]
    public JsValue TrapSet() => _trapSet.Run();

    [Benchmark]
    public JsValue TrapHas() => _trapHas.Run();

    [Benchmark]
    public JsValue ForwardGet() => _forwardGet.Run();

    [Benchmark]
    public JsValue ForwardSet() => _forwardSet.Run();

    [Benchmark]
    public JsValue OwnKeysTrap() => _ownKeysTrap.Run();

    [Benchmark]
    public JsValue OwnKeysForward() => _ownKeysForward.Run();

    [Benchmark]
    public JsValue ApplyTrap() => _applyTrap.Run();

    [Benchmark]
    public JsValue ApplyForward() => _applyForward.Run();

    [Benchmark]
    public JsValue ConstructTrap() => _constructTrap.Run();

    [Benchmark]
    public JsValue RevocableCreate() => _revocableCreate.Run();

    [Benchmark]
    public JsValue RevokedTypeof() => _revokedTypeof.Run();

    [Benchmark]
    public JsValue ClrTrapGet() => _clrTrapGet.Run();

    [Benchmark]
    public JsValue ClrTrapSet() => _clrTrapSet.Run();

    [Benchmark]
    public JsValue ClrTrapHas() => _clrTrapHas.Run();

    [Benchmark]
    public JsValue ClrForwardGet() => _clrForwardGet.Run();

    [Benchmark]
    public JsValue ClrApplyTrap() => _clrApplyTrap.Run();

    [Benchmark]
    public JsValue ClrConstructTrap() => _clrConstructTrap.Run();

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
