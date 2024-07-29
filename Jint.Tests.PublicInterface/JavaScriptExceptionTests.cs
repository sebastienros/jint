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
}