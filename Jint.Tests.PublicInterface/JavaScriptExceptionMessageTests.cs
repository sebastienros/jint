#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Exception.Message"/> on a <see cref="JavaScriptException"/> is what a host logs, and it is built
/// from the thrown value without running any of the script's code: the text <c>ToString(value.message)</c>
/// produces whenever that can be known from data properties alone, and an empty string where only a getter, a
/// proxy trap or a <c>toString</c> could say.
/// </summary>
/// <remarks>
/// Every row runs twice, because the exception a host catches is built at two different sites: where a
/// top-level script completes with a throw, and where a throw leaves a function body.
/// </remarks>
public class JavaScriptExceptionMessageTests
{
    private static JavaScriptException Catch(Engine engine, string throwStatement, bool throughAFunction)
    {
        var script = throughAFunction
            ? "function thrower() { " + throwStatement + " } thrower();"
            : throwStatement;
        return Invoking(() => engine.Execute(script)).Should().ThrowExactly<JavaScriptException>().Which;
    }

    [TestCase("throw new Error('boom');", "boom")]
    [TestCase("throw new TypeError('bad type');", "bad type")]
    [TestCase("throw new Error();", "")]
    [TestCase("throw new AggregateError([], 'all failed');", "all failed")]
    [TestCase("class MyError extends Error { constructor(m) { super(m); this.name = 'MyError'; } } throw new MyError('subclassed');", "subclassed")]
    [TestCase("var e = new Error('before'); e.message = 'after'; throw e;", "after")]
    [TestCase("var e = new Error('x'); Object.defineProperty(e, 'message', { value: 'redefined' }); throw e;", "redefined")]
    [TestCase("var e = new Error('frozen'); Object.freeze(e); throw e;", "frozen")]
    [TestCase("var e = new Error('x'); delete e.message; throw e;", "")]
    [TestCase("throw Object.assign(Object.create(Error.prototype), { message: 'not constructed' });", "not constructed")]
    [TestCase("throw { message: 'plain' };", "plain")]
    [TestCase("throw { message: 42 };", "42")]
    [TestCase("throw { message: null };", "null")]
    [TestCase("throw { message: Symbol('described') };", "Symbol(described)")]
    [TestCase("throw Object.create({ message: 'inherited' });", "inherited")]
    [TestCase("throw {};", "undefined")]
    [TestCase("throw Object.create(null);", "undefined")]
    [TestCase("throw 'a string';", "a string")]
    [TestCase("throw 42;", "42")]
    [TestCase("throw 10n;", "10")]
    [TestCase("throw true;", "true")]
    [TestCase("throw undefined;", "undefined")]
    [TestCase("throw null;", "null")]
    [TestCase("throw Symbol('sym');", "Symbol(sym)")]
    [TestCase("null.x;", "Cannot read properties of null (reading 'x')")]
    [TestCase("undefinedVariable;", "undefinedVariable is not defined")]
    public void AnOrdinaryThrownValueKeepsItsMessage(string throwStatement, string expected)
    {
        foreach (var throughAFunction in new[] { false, true })
        {
            var engine = new Engine();

            Catch(engine, throwStatement, throughAFunction).Message.Should().Be(expected);
        }
    }

    [TestCase("var e = new Error('original'); Object.defineProperty(e, 'message', { get() { ran++; return 'from getter'; } }); throw e;")]
    [TestCase("var e = new Error('original'); Object.defineProperty(e, 'message', { get() { ran++; throw new RangeError('from getter'); } }); throw e;")]
    [TestCase("class Computed extends Error { get message() { ran++; return 'computed'; } } throw new Computed();")]
    [TestCase("throw Object.create({ get message() { ran++; return 'inherited getter'; } });")]
    [TestCase("throw { message: { toString() { ran++; return 'object message'; } } };")]
    [TestCase("throw { message: ['a', 'b'] };")]
    [TestCase("throw new Proxy({}, { get() { ran++; } });")]
    [TestCase("throw new Proxy(new Error('proxied'), { get(target, key, receiver) { if (key === 'message') ran++; return Reflect.get(target, key, receiver); } });")]
    [TestCase("throw Object.create(new Proxy({}, { getOwnPropertyDescriptor() { ran++; }, get() { ran++; return 'from a proxy prototype'; } }));")]
    public void AMessageOnlyScriptCouldProduceIsEmptyAndRunsNothing(string throwStatement)
    {
        foreach (var throughAFunction in new[] { false, true })
        {
            var engine = new Engine();
            engine.Execute("var ran = 0;");

            var exception = Catch(engine, throwStatement, throughAFunction);

            exception.Message.Should().BeEmpty();
            engine.Evaluate("ran").AsNumber().Should().Be(0);
        }
    }

    /// <summary>
    /// Wrapping a revoked proxy no longer throws: reading its <c>message</c> through <c>[[Get]]</c> raised the
    /// proxy's <c>TypeError</c> out of the constructor, in place of the exception the host was building.
    /// </summary>
    [Test]
    public void AHostCanWrapARevokedProxy()
    {
        var engine = new Engine();
        var proxy = engine.Evaluate("var revocable = Proxy.revocable({}, {}); revocable.revoke(); revocable.proxy;");

        JavaScriptException? exception = null;
        Caught.Exception(() => exception = new JavaScriptException(proxy)).Should().BeNull();

        exception!.Message.Should().BeEmpty();

        // Compared by reference, because an assertion failure would render the revoked proxy, and rendering
        // it is a [[Get]] that throws.
        ReferenceEquals(exception.Error, proxy).Should().BeTrue();
    }

    /// <summary>
    /// A member of a wrapped CLR object is the host's own code, not script, so it is still read — and the
    /// discouraged <c>new JavaScriptException(JsValue.FromObject(engine, ex))</c> keeps the CLR message.
    /// </summary>
    [Test]
    public void AWrappedClrObjectKeepsItsMessageMember()
    {
        var engine = new Engine();
        engine.SetValue("clrError", new InvalidOperationException("clr boom"));

        Catch(engine, "throw clrError;", throughAFunction: false).Message.Should().Be("clr boom");
        Catch(engine, "throw clrError;", throughAFunction: true).Message.Should().Be("clr boom");
        new JavaScriptException(JsValue.FromObject(engine, new InvalidOperationException("projected")))
            .Message.Should().Be("projected");
    }

    [Test]
    public void AHostConstructedExceptionReadsTheSameWay()
    {
        var engine = new Engine();
        var ordinary = engine.Evaluate("new Error('failure')");
        var withGetter = engine.Evaluate("""
            var ran = 0;
            var e = new Error('original');
            Object.defineProperty(e, 'message', { get() { ran++; return 'from getter'; } });
            e;
            """);

        new JavaScriptException(ordinary).Message.Should().Be("failure");
        new JavaScriptException(withGetter).Message.Should().BeEmpty();
        engine.Evaluate("ran").AsNumber().Should().Be(0);
    }

    [Test]
    public void TheRenderedErrorStringUsesTheSameMessage()
    {
        var engine = new Engine();
        engine.Execute("""
            var ran = 0;
            var e = new Error('original');
            Object.defineProperty(e, 'message', { get() { ran++; return 'from getter'; } });
            """);

        var ordinary = Catch(engine, "throw new Error('boom');", throughAFunction: true);
        var withGetter = Catch(engine, "throw e;", throughAFunction: true);

        ordinary.GetJavaScriptErrorString().Should().StartWith("Error: boom");
        withGetter.GetJavaScriptErrorString().Should().StartWith("Error" + Environment.NewLine);
        engine.Evaluate("ran").AsNumber().Should().Be(0);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// A <c>DOMException</c>'s <c>message</c> is a WebIDL prototype accessor by design, so it is answered from
    /// the instance's own slot — which is also what it answers when a script replaces that accessor.
    /// </summary>
    [Test]
    public void ADomExceptionKeepsItsMessage()
    {
        foreach (var throughAFunction in new[] { false, true })
        {
            var engine = new Engine(options => options.UseWebApis());

            Catch(engine, "throw new DOMException('aborted', 'AbortError');", throughAFunction)
                .Message.Should().Be("aborted");

            engine.Execute("""
                var ran = 0;
                Object.defineProperty(DOMException.prototype, 'message', { get() { ran++; return 'patched'; } });
                """);
            Catch(engine, "throw new DOMException('aborted', 'AbortError');", throughAFunction)
                .Message.Should().Be("aborted");
            engine.Evaluate("ran").AsNumber().Should().Be(0);
        }
    }
#endif
}
