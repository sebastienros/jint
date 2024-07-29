using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class EvaluateTests
{
    [Fact]
    public void EvalutesInCurrentContext()
    {
        var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            var evaluated = engine.Debugger.Evaluate("x");
            Assert.IsType<JsNumber>(evaluated);
            Assert.Equal(50, evaluated.AsNumber());
        });
    }

    [Fact]
    public void ThrowsIfNoCurrentContext()
    {
        var engine = new Engine(options => options.DebugMode());
        var exception = Assert.Throws<DebugEvaluationException>(() => engine.Debugger.Evaluate("let x = 1;"));
        Assert.Null(exception.InnerException); // Not a JavaScript or parser exception
    }

    [Fact]
    public void ThrowsOnRuntimeError()
    {
        var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            var exception = Assert.Throws<DebugEvaluationException>(() => engine.Debugger.Evaluate("y"));
            Assert.IsType<JavaScriptException>(exception.InnerException);
        });
    }

    [Fact]
    public void ThrowsOnExecutionError()
    {
        var script = @"
            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            var exception = Assert.Throws<DebugEvaluationException>(() =>
                engine.Debugger.Evaluate("this is a syntax error"));
            Assert.IsType<SyntaxErrorException>(exception.InnerException);
        });
    }

    [Fact]
    public void RestoresStackAfterEvaluation()
    {
        var script = @"
            function throws()
            {
                throw new Error('Take this!');
            }

            function test(x)
            {
                x *= 10;
                debugger;
            }

            test(5);
            ";

        TestHelpers.TestAtBreak(script, (engine, info) =>
        {
            Assert.Equal(1, engine.CallStack.Count);
            var frameBefore = engine.CallStack.Stack[0];

            Assert.Throws<DebugEvaluationException>(() => engine.Debugger.Evaluate("throws()"));
            Assert.Equal(1, engine.CallStack.Count);
            var frameAfter = engine.CallStack.Stack[0];
            // Stack frames and some of their properties are structs - can't check reference equality
            // Besides, even if we could, it would be no guarantee. Neither is the following, but it'll do for now.
            Assert.Equal(frameBefore.CallingExecutionContext.LexicalEnvironment, frameAfter.CallingExecutionContext.LexicalEnvironment);
            Assert.Equal(frameBefore.Arguments, frameAfter.Arguments);
            Assert.Equal(frameBefore.Expression, frameAfter.Expression);
            Assert.Equal(frameBefore.Location, frameAfter.Location);
            Assert.Equal(frameBefore.Function, frameAfter.Function);
        });
    }
}