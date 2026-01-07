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

        Assert.Throws<JavaScriptException>(() =>
        {
            engine.Evaluate(@"throw1()");
        });

        var result1 = engine.Evaluate(@"try { throw1() } catch (e) { return e; }");
        var error1 = Assert.IsType<JsError>(result1);
        Assert.Equal("message 1", error1.Get("message").ToString());

        var result2 = engine.Evaluate(@"try { throw2() } catch (e) { return e; }");
        var jsString = Assert.IsType<JsString>(result2);
        Assert.Equal("message 2", jsString.ToString());
    }

    [Fact]
    public void GetBaseExceptionReturnsJavaScriptException()
    {
        // https://github.com/sebastienros/jint/issues/2210
        try
        {
            new Engine().Evaluate("x * x");
            Assert.Fail("Expected JavaScriptException to be thrown");
        }
        catch (Exception ex)
        {
            // GetBaseException() should return the JavaScriptException itself,
            // not the private inner exception
            var baseException = ex.GetBaseException();
            Assert.IsType<JavaScriptException>(baseException);
            Assert.Same(ex, baseException);
        }
    }

    [Fact]
    public void GetBaseExceptionAllowsPatternMatching()
    {
        // https://github.com/sebastienros/jint/issues/2210
        try
        {
            new Engine().Evaluate("throw new Error('test error')");
            Assert.Fail("Expected JavaScriptException to be thrown");
        }
        catch (Exception ex)
        {
            // Pattern matching with GetBaseException() should work
            if (ex.GetBaseException() is JavaScriptException jsError)
            {
                Assert.NotNull(jsError.Error);
                Assert.Equal("test error", jsError.Error.AsObject().Get("message").ToString());
            }
            else
            {
                Assert.Fail("GetBaseException() should return JavaScriptException");
            }
        }
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

        try
        {
            engine.Evaluate("throwFromDotNet()");
            Assert.Fail("Expected JavaScriptException to be thrown");
        }
        catch (Exception ex)
        {
            var baseException = ex.GetBaseException();
            Assert.IsType<JavaScriptException>(baseException);

            if (baseException is JavaScriptException jsError)
            {
                Assert.Equal("from .NET", jsError.Error.AsObject().Get("message").ToString());
            }
        }
    }
}