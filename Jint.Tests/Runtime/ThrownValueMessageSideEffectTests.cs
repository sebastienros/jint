#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A throw that leaves a function, generator, <c>eval</c> or constructor body is re-raised as a
/// <see cref="JavaScriptException"/> at every frame it unwinds through, and building that exception's CLR
/// message must not read the thrown value's <c>message</c> through <c>[[Get]]</c>. A <c>throw</c> hands the
/// value over as it is and <c>catch</c> binds it as it is
/// (https://tc39.es/ecma262/#sec-runtime-semantics-catchclauseevaluation), so a getter or a proxy trap that
/// ran in between was a side effect no other engine has, and one that threw replaced the value being thrown.
/// </summary>
public class ThrownValueMessageSideEffectTests
{
    private const string ErrorWithMessageGetter = """
        var reads = 0;
        var error = new Error('original');
        Object.defineProperty(error, 'message', { get() { reads++; return 'from getter'; } });
        """;

    [TestCase("function f() { throw error; } try { f(); } catch (e) {}", TestName = "one function frame")]
    [TestCase("function a() { throw error; } function b() { a(); } function c() { b(); } function d() { c(); } try { d(); } catch (e) {}", TestName = "four function frames")]
    [TestCase("function* g() { throw error; } try { g().next(); } catch (e) {}", TestName = "a generator body")]
    [TestCase("try { eval('throw error'); } catch (e) {}", TestName = "an eval body")]
    [TestCase("class C { constructor() { throw error; } } try { new C(); } catch (e) {}", TestName = "a constructor body")]
    [TestCase("try { [1].map(function () { throw error; }); } catch (e) {}", TestName = "a callback a built-in invoked")]
    [TestCase("function f() { try { throw error; } finally { } } try { f(); } catch (e) {}", TestName = "a finally block on the way out")]
    public void AMessageGetterDoesNotRunWhileAThrowUnwinds(string script)
    {
        var engine = new Engine();
        engine.Execute(ErrorWithMessageGetter);

        engine.Execute(script);

        engine.Evaluate("reads").AsNumber().Should().Be(0);
    }

    [Test]
    public void AMessageGetterOnASubclassPrototypeDoesNotRunWhileAThrowUnwinds()
    {
        var engine = new Engine();

        engine.Execute("""
            var reads = 0;
            class Computed extends Error { get message() { reads++; return 'computed'; } }
            function f() { throw new Computed(); }
            try { f(); } catch (e) {}
            """);

        engine.Evaluate("reads").AsNumber().Should().Be(0);
    }

    /// <summary>The repro as it was reported: a get trap fired twice per frame the throw left.</summary>
    [Test]
    public void AThrownProxysGetTrapDoesNotFireWhileAThrowUnwinds()
    {
        var engine = new Engine();

        engine.Execute("""
            var n = 0;
            var thrown = new Proxy({}, { get() { n++; } });
            function f() { throw thrown; }
            var caught;
            try { f(); } catch (e) { caught = e; }
            """);

        engine.Evaluate("n").AsNumber().Should().Be(0);
        engine.Evaluate("caught === thrown").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// No trap is asked about <c>message</c>, and none of the traps a descriptor walk would reach — its own
    /// property or its prototype — is asked anything on the message's account either.
    /// </summary>
    [Test]
    public void NoTrapOfAThrownProxyIsAskedAboutMessage()
    {
        var engine = new Engine();

        engine.Execute("""
            var log = [];
            var handler = {};
            ['get', 'getOwnPropertyDescriptor', 'getPrototypeOf', 'has', 'ownKeys'].forEach(function (trap) {
                handler[trap] = function (target, key) {
                    log.push(trap + ':' + (typeof key === 'string' ? key : ''));
                    return Reflect[trap].apply(null, arguments);
                };
            });
            var thrown = new Proxy(new Error('proxied'), handler);
            function f() { throw thrown; }
            var caught;
            try { f(); } catch (e) { caught = e; }
            """);

        engine.Evaluate("caught === thrown").AsBoolean().Should().BeTrue();
        engine.Evaluate("log.filter(function (entry) { return entry.indexOf('message') >= 0; }).join(',')").AsString()
            .Should().BeEmpty();
        engine.Evaluate("log.filter(function (entry) { return entry.indexOf('getOwnPropertyDescriptor') === 0 || entry.indexOf('getPrototypeOf') === 0; }).join(',')").AsString()
            .Should().BeEmpty();
    }

    [Test]
    public void AThrowingMessageGetterDoesNotReplaceTheThrownValue()
    {
        var engine = new Engine();

        engine.Execute("""
            var error = new Error('original');
            Object.defineProperty(error, 'message', { get() { throw new RangeError('from getter'); } });
            function f() { throw error; }
            var caught;
            try { f(); } catch (e) { caught = e; }
            """);

        engine.Evaluate("caught === error").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A throw raised in a script function the host called back crosses the CLR stack twice: out of the host
    /// call as a <see cref="JavaScriptException"/>, and back into the script that made the host call.
    /// </summary>
    [Test]
    public void AThrowCrossingAHostCallDoesNotRunTheMessageGetter()
    {
        var engine = new Engine();
        engine.SetValue("callBack", new Func<JsValue, JsValue>(callback => callback.Call()));
        engine.Execute(ErrorWithMessageGetter);

        engine.Execute("""
            var caught;
            try { callBack(function () { throw error; }); } catch (e) { caught = e; }
            """);

        engine.Evaluate("reads").AsNumber().Should().Be(0);
        engine.Evaluate("caught === error").AsBoolean().Should().BeTrue();
    }

    [TestCase("throw error;", TestName = "a top-level throw")]
    [TestCase("function f() { throw error; } f();", TestName = "a throw out of a function")]
    public void AThrowTheHostCatchesDoesNotRunTheMessageGetter(string script)
    {
        var engine = new Engine();
        engine.Execute(ErrorWithMessageGetter);

        var exception = Invoking(() => engine.Execute(script)).Should().ThrowExactly<JavaScriptException>().Which;

        engine.Evaluate("reads").AsNumber().Should().Be(0);
        exception.Error.Should().BeSameAs(engine.GetValue("error"));
    }

    [Test]
    public void AThrowOutOfAnInvokedFunctionDoesNotRunTheMessageGetter()
    {
        var engine = new Engine();
        engine.Execute(ErrorWithMessageGetter);
        engine.Execute("function f() { throw error; }");

        var exception = Invoking(() => engine.Invoke("f")).Should().ThrowExactly<JavaScriptException>().Which;

        engine.Evaluate("reads").AsNumber().Should().Be(0);
        exception.Error.Should().BeSameAs(engine.GetValue("error"));
    }
}
