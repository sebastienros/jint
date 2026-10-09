using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="JavaScriptException"/> builds its CLR message from the thrown value's <c>message</c>. That read can run
/// script (a getter, a proxy <c>get</c> trap), so it must run once per exception and, when it throws, must not
/// replace the value being thrown. A 4.x-only fix for the throwing-getter half of #4186.
/// </summary>
public class ThrownValueMessageTests
{
    [Fact]
    public void AThrowingMessageGetterDoesNotReplaceTheThrownValue()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            const e = new Error('original');
            Object.defineProperty(e, 'message', { get() { throw new RangeError('from getter'); } });
            function f() { throw e; }
            let caught;
            try { f(); } catch (x) { caught = x; }
            caught === e;
            """);

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AThrowingProxyGetTrapDoesNotReplaceTheThrownValue()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            const p = new Proxy({}, { get() { throw new RangeError('from trap'); } });
            function f() { throw p; }
            let caught;
            try { f(); } catch (x) { caught = x; }
            caught === p;
            """);

        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void AHostCatchingAThrowWhoseMessageGetterThrowsGetsTheThrownValue()
    {
        var engine = new Engine();
        engine.Execute("""
            var e = new Error('original');
            Object.defineProperty(e, 'message', { get() { throw new RangeError('from getter'); } });
            """);
        var thrown = engine.GetValue("e");

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("function f() { throw e; } f();"));

        ex.Error.Should().BeSameAs(thrown);
        ex.Message.Should().BeEmpty();
    }

    [Fact]
    public void AHostCanWrapAValueWhoseGetTrapThrows()
    {
        var engine = new Engine();
        var proxy = engine.Evaluate("new Proxy({}, { get() { throw new RangeError('from trap'); } })");
        var revoked = engine.Evaluate("const r = Proxy.revocable({}, {}); r.revoke(); r.proxy");

        var fromProxy = new JavaScriptException(proxy);
        var fromRevoked = new JavaScriptException(revoked);

        fromProxy.Error.Should().BeSameAs(proxy);
        fromProxy.Message.Should().BeEmpty();
        fromRevoked.Error.Should().BeSameAs(revoked);
        fromRevoked.Message.Should().BeEmpty();
    }

    [Fact]
    public void TheMessageGetterRunsOncePerException()
    {
        var engine = new Engine();
        engine.Execute("""
            var reads = 0;
            var e = new Error('original');
            Object.defineProperty(e, 'message', { get() { reads++; return 'from getter'; } });
            """);

        var ex = new JavaScriptException(engine.GetValue("e"));

        engine.Evaluate("reads").AsNumber().Should().Be(1);
        ex.Message.Should().Be("from getter");
    }

    [Fact]
    public void AThrowThroughOneFunctionFrameReadsTheMessageOnce()
    {
        var engine = new Engine();

        var reads = engine.Evaluate("""
            let reads = 0;
            const e = new Error('original');
            Object.defineProperty(e, 'message', { get() { reads++; return 'from getter'; } });
            function f() { throw e; }
            try { f(); } catch {}
            reads;
            """);

        reads.AsNumber().Should().Be(1);
    }

    [Theory]
    [InlineData("new Error('boom')", "boom")]
    [InlineData("new TypeError('bad type')", "bad type")]
    [InlineData("({ message: 'plain' })", "plain")]
    [InlineData("({})", "undefined")]
    [InlineData("'a string'", "a string")]
    [InlineData("42", "42")]
    [InlineData("Object.defineProperty(new Error('x'), 'message', { get() { return 'from getter'; } })", "from getter")]
    [InlineData("new Proxy({}, { get(t, k) { return k === 'message' ? 'from trap' : undefined; } })", "from trap")]
    public void TheMessageOfAnOrdinaryThrowIsUnchanged(string thrown, string expected)
    {
        var engine = new Engine();

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute($"function f() {{ throw {thrown}; }} f();"));

        ex.Message.Should().Be(expected);
        ex.GetJavaScriptErrorString().Should().StartWith("Error: " + expected);
    }
}
