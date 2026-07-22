using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public class JavaScriptExceptionTests
{
    [Fact]
    public void CanCreateAndThrowJavaScriptException()
    {
        var engine = new Engine();

        engine.SetValue("throw1", () =>
        {
            throw new JavaScriptException(engine.Intrinsics.Error, "message 1");
        });

        engine.SetValue("throw2", () =>
        {
            throw new JavaScriptException(new JsString("message 2"));
        });

        Invoking(() =>
        {
            engine.Evaluate(@"throw1()");
        }).Should().ThrowExactly<JavaScriptException>();

        var result1 = engine.Evaluate(@"try { throw1() } catch (e) { return e; }");
        var error1 = result1.Should().BeOfType<JsError>().Which;
        error1.Get("message").ToString().Should().Be("message 1");

        var result2 = engine.Evaluate(@"try { throw2() } catch (e) { return e; }");
        var jsString = result2.Should().BeOfType<JsString>().Which;
        jsString.ToString().Should().Be("message 2");
    }

    [Fact]
    public void GetBaseExceptionReturnsJavaScriptException()
    {
        // https://github.com/sebastienros/jint/issues/2210
        var ex = Invoking(() => new Engine().Evaluate("x * x")).Should().Throw<Exception>().Which;

        // GetBaseException() should return the JavaScriptException itself,
        // not the private inner exception
        var baseException = ex.GetBaseException();
        baseException.Should().BeOfType<JavaScriptException>();
        baseException.Should().BeSameAs(ex);
    }

    [Fact]
    public void GetBaseExceptionAllowsPatternMatching()
    {
        // https://github.com/sebastienros/jint/issues/2210
        var ex = Invoking(() => new Engine().Evaluate("throw new Error('test error')")).Should().Throw<Exception>().Which;

        // Pattern matching with GetBaseException() should work
        var jsError = ex.GetBaseException().Should().BeOfType<JavaScriptException>().Which;
        jsError.Error.Should().NotBeNull();
        jsError.Error.AsObject().Get("message").ToString().Should().Be("test error");
    }

    [Fact]
    public void GetBaseExceptionWorksWithExplicitlyThrownJavaScriptException()
    {
        // https://github.com/sebastienros/jint/issues/2210
        var engine = new Engine();

        engine.SetValue("throwFromDotNet", () =>
        {
            throw new JavaScriptException(engine.Intrinsics.Error, "from .NET");
        });

        var ex = Invoking(() => engine.Evaluate("throwFromDotNet()")).Should().Throw<Exception>().Which;

        var jsError = ex.GetBaseException().Should().BeOfType<JavaScriptException>().Which;
        jsError.Error.AsObject().Get("message").ToString().Should().Be("from .NET");
    }
}