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
            evaluated.Should().BeOfType<JsNumber>();
            evaluated.AsNumber().Should().Be(50);
        });
    }

    [Fact]
    public void ThrowsIfNoCurrentContext()
    {
        var engine = new Engine(options => options.DebugMode());
        var exception = Invoking(() => engine.Debugger.Evaluate("let x = 1;")).Should().ThrowExactly<DebugEvaluationException>().Which;
        exception.InnerException.Should().BeNull(); // Not a JavaScript or parser exception
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
            var exception = Invoking(() => engine.Debugger.Evaluate("y")).Should().ThrowExactly<DebugEvaluationException>().Which;
            exception.InnerException.Should().BeOfType<JavaScriptException>();
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
            var exception = Invoking(() =>
                engine.Debugger.Evaluate("this is a syntax error")).Should().ThrowExactly<DebugEvaluationException>().Which;
            exception.InnerException.Should().BeOfType<Acornima.SyntaxErrorException>();
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
            engine.CallStack.Count.Should().Be(1);
            var frameBefore = engine.CallStack.Stack[0];

            Invoking(() => engine.Debugger.Evaluate("throws()")).Should().ThrowExactly<DebugEvaluationException>();
            engine.CallStack.Count.Should().Be(1);
            var frameAfter = engine.CallStack.Stack[0];
            // Stack frames and some of their properties are structs - can't check reference equality
            // Besides, even if we could, it would be no guarantee. Neither is the following, but it'll do for now.
            frameAfter.CallingExecutionContext.LexicalEnvironment.Should().Be(frameBefore.CallingExecutionContext.LexicalEnvironment);
            frameAfter.Arguments.Should().Be(frameBefore.Arguments);
            frameAfter.Expression.Should().Be(frameBefore.Expression);
            frameAfter.Location.Should().Be(frameBefore.Location);
            frameAfter.Function.Should().Be(frameBefore.Function);
        });
    }
}
