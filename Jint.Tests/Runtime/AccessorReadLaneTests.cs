#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Every read of an accessor property funnels through <c>ObjectInstance.UnwrapFromGetter</c>, which decides
/// whether the getter is a <c>Function</c> from the <c>InternalTypes.Function</c> flag alone and then
/// reinterprets it with <c>Unsafe.As</c>. Everything else — an absent getter, an <c>undefined</c> one, a
/// value that implements <c>ICallable</c> without being callable, and the <c>ICallable</c>s that are not
/// <c>Function</c>s — is served by a separate out-of-line arm.
/// </summary>
/// <remarks>
/// <para>
/// <c>CallableFlagTests</c> pins the flag/type agreement that makes the <c>Unsafe.As</c> sound. This pins
/// the other half from script: that every shape which can legally reach a getter slot still produces the
/// value it produced when the lane was one virtual <c>HasCall</c> plus an <c>ICallable</c> cast. The
/// distinction matters because an <c>Unsafe.As</c> onto the wrong type is memory corruption rather than an
/// exception, and the in-code <c>Debug.Assert</c> guarding it is compiled out of the Release builds this
/// repository's pipelines run.
/// </para>
/// </remarks>
public class AccessorReadLaneTests
{
    private readonly Engine _engine = new();

    [Test]
    public void AnOrdinaryGetterRunsWithTheReadReceiverAsThis()
    {
        _engine.Evaluate("""
            var proto = {};
            Object.defineProperty(proto, 'x', { get: function () { return this.tag; }, configurable: true });
            var o = Object.create(proto);
            o.tag = 'own';
            o.x;
            """).Should().Be("own");
    }

    [Test]
    public void AGetterThatIsAProxyOverAFunctionIsCalledThroughItsTrap()
    {
        // A callable Proxy is a legal getter and is an ICallable that is NOT a Function, so it must stay on
        // the out-of-line arm: reinterpreting it as a Function is exactly the mistake the flag prevents.
        _engine.Evaluate("""
            var calls = [];
            var getter = new Proxy(function () { return 'target'; }, {
                apply: function (target, thisArg, args) { calls.push(thisArg.tag); return 'trap'; }
            });
            var o = { tag: 'receiver' };
            Object.defineProperty(o, 'x', { get: getter });
            o.x + '/' + calls.join(',');
            """).Should().Be("trap/receiver");
    }

    [Test]
    public void AGetterThatIsARevokedProxyThrowsATypeError()
    {
        _engine.Evaluate("""
            var r = Proxy.revocable(function () { return 1; }, {});
            var o = {};
            Object.defineProperty(o, 'x', { get: r.proxy });
            r.revoke();
            try { o.x; 'no throw'; } catch (e) { e.constructor.name; }
            """).Should().Be("TypeError");
    }

    [Test]
    public void AGetterThatIsAProxyOverANonCallableTargetReadsUndefined()
    {
        // ToPropertyDescriptor accepts it (JsProxy implements ICallable whatever it wraps) but the proxy
        // reports no [[Call]], so the read answers undefined rather than throwing.
        _engine.Evaluate("""
            var o = {};
            Object.defineProperty(o, 'x', { get: new Proxy({}, {}) });
            typeof o.x;
            """).Should().Be("undefined");
    }

    [Test]
    public void AnObjectWithTheHtmlDdaSlotIsCallableAsAGetter()
    {
        // Annex B's [[IsHTMLDDA]] bearer is the third ICallable that is not a Function. Calling it with no
        // arguments answers null, which is what document.all() does for a missing name.
        var engine = new Engine();
        engine.SetValue("dda", (JsValue) new Jint.Native.IsHTMLDDA(engine, engine.Realm));

        engine.Evaluate("""
            var o = {};
            Object.defineProperty(o, 'x', { get: dda });
            o.x === null;
            """).Should().Be(true);
    }

    [Test]
    public void AnAccessorDeclaringOnlyASetterReadsUndefined()
    {
        // Two distinct shapes reach the lane here: an absent "get" leaves the descriptor's getter field
        // null, while an explicit `get: undefined` stores the undefined value in it.
        _engine.Evaluate("""
            var absent = {};
            Object.defineProperty(absent, 'x', { set: function (v) { } });
            var explicitUndefined = {};
            Object.defineProperty(explicitUndefined, 'x', { get: undefined, set: function (v) { } });
            typeof absent.x + '/' + typeof explicitUndefined.x;
            """).Should().Be("undefined/undefined");
    }

    [Test]
    public void AGetterKeepsItsIdentityThroughGetOwnPropertyDescriptor()
    {
        _engine.Evaluate("""
            var g = function () { return 1; };
            var o = {};
            Object.defineProperty(o, 'x', { get: g, configurable: true });
            var d = Object.getOwnPropertyDescriptor(o, 'x');
            (d.get === g) && (d.set === undefined) && (o.x === 1);
            """).Should().Be(true);
    }

    [Test]
    public void ABuiltInAccessorStillReadsThroughTheSameLane()
    {
        // A built-in shape materializes its accessors into the same descriptor type the generated Web IDL
        // attributes use, with a native function as the getter.
        _engine.Evaluate("new Map([[1, 2], [3, 4]]).size").Should().Be(2);
        _engine.Evaluate("Object.getOwnPropertyDescriptor(Map.prototype, 'size').get.name").Should().Be("get size");
        _engine.Evaluate("Object.getOwnPropertyDescriptor(Map.prototype, 'size').get.call(new Map())").Should().Be(0);
    }

    [Test]
    public void AGetterThatThrowsPropagatesAndLeavesTheCallStackBalanced()
    {
        _engine.Evaluate("""
            var o = {};
            Object.defineProperty(o, 'x', { get: function () { throw new RangeError('boom'); } });
            var first = '';
            try { o.x; } catch (e) { first = e.message; }
            // the frame the throw unwound must not still be on the stack: a second read behaves identically
            var second = '';
            try { o.x; } catch (e) { second = e.message; }
            first + '/' + second;
            """).Should().Be("boom/boom");
    }
}
